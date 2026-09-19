#!/usr/bin/env python3
"""Re-encode a converted bundle's generated level into AVIF or WebP as its own bundle.

`make threejs-map-encode MAP=station-2 FORMAT=avif` takes the bundle's generated `4x` files and
writes a sibling bundle whose `4x` variants point at the encoded files, so the two can be compared
side by side in the viewer as separate map entries. The decoded originals (`1x`) always stay as they
are. Colour, alpha and lightmaps are encoded by default, each at its own quality — a lightmap's values
multiply the scene lighting, so its error would show as blotchy light rather than a shifted pixel, and
a normal map stores a vector, so it is only encoded when `--kinds` names it explicitly (its error is
an angle, and it lands on every shaded pixel).

Nothing is claimed about fidelity here: the manifest records the codec, its quality, and the
`sourceSha256`/`generatedSha256` of both the PNG it came from and the file it wrote, so a re-run is
idempotent and every encoded file names its provenance.

Usage: encode-texture-levels.py <bundle-dir> [--manifest index.json] --format avif|webp
       [--quality 60] [--level 4x] [--directory <dir>] [--name "<viewer name>"] [--dry-run]
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from progress import emit, format_size, header  # noqa: E402

ROOT = Path(__file__).resolve().parents[3]
ENCODERS = {
    # command builder, output extension, algorithm suffix
    'avif': (lambda source, target, quality: ['avifenc', '-q', str(quality), '--speed', '6', str(source), str(target)], 'avif', 'avif'),
    'webp': (lambda source, target, quality: ['cwebp', '-quiet', '-q', str(quality), str(source), '-o', str(target)], 'webp', 'webp'),
}
# Colour and alpha are detail the eye forgives; a lightmap's values multiply the scene lighting, so
# its quantization error shows up as blotchy light rather than a slightly different pixel. A normal
# map stores a vector, so its error is an angle — it is *not* encoded unless you ask for it with
# `--kinds ...,normal`, and then at its own quality, because that error lands on every shaded pixel.
DEFAULT_QUALITY = {'color': 60, 'alpha': 60, 'lightmap': 90, 'normal': 90}
ENCODABLE_KINDS = ['color', 'alpha', 'lightmap', 'normal']
DEFAULT_KINDS = ['color', 'alpha', 'lightmap']

parser = argparse.ArgumentParser(description="Re-encode a bundle's generated texture level as AVIF/WebP in its own bundle.")
parser.add_argument('root', type=Path, nargs='?', help='bundle directory of a converted map')
parser.add_argument('--map', default=None, help='a converted map id from the maps index (e.g. station-2) instead of a path')
parser.add_argument('--manifest', default=None, help='manifest inside the bundle (default: the one the maps index names)')
parser.add_argument('--format', choices=sorted(ENCODERS), default='avif')
parser.add_argument('--quality', type=int, default=None, help='codec quality for colour/alpha (default: 60 avif, 90 webp)')
parser.add_argument('--quality-lightmap', type=int, default=None, help='codec quality for lightmaps (default: 90)')
parser.add_argument('--quality-normal', type=int, default=None,
                    help='codec quality for normal maps when --kinds lists normal (default: 90)')
parser.add_argument('--kinds', default=','.join(DEFAULT_KINDS),
                    help=f"texture kinds to encode (default: {','.join(DEFAULT_KINDS)}; add normal to "
                         'encode normal maps too, at --quality-normal)')
parser.add_argument('--level', default='4x', help='level to re-encode (default: 4x)')
parser.add_argument('--in-place', action='store_true',
                    help="rewrite this bundle's own manifest and delete the replaced PNGs (no sibling bundle, "
                         'no registry entry) — the mode that actually reclaims the generated PNGs')
parser.add_argument('--directory', type=Path, default=None, help='output bundle directory (default: <bundle>-<level>-<FORMAT>)')
parser.add_argument('--name', default=None, help='name shown in the map menu (default: "<map> <level> <FORMAT>")')
parser.add_argument('--maps-dir', type=Path, default=None,
                    help='directory holding the converted maps and their index.json (default: Client/Models/Maps)')
parser.add_argument('--dry-run', action='store_true', help='report what would be encoded and change nothing')
args = parser.parse_args()

def relative(path: Path) -> str:
    """Repo-relative when it is inside the repository, absolute otherwise."""
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def slug(text: str) -> str:
    """The map-id rule the viewer's registry test pins: lowercase, non-alphanumerics collapse to '-'."""
    return re.sub(r'^-|-$', '', re.sub(r'[^a-z0-9]+', '-', text.lower()))


MAPS = (args.maps_dir or (ROOT / 'Client/Models/Maps')).resolve()
build_command, extension, suffix = ENCODERS[args.format]
kinds = [kind.strip() for kind in args.kinds.split(',') if kind.strip()]
unknown_kinds = [kind for kind in kinds if kind not in ENCODABLE_KINDS]
if unknown_kinds:
    raise SystemExit(f"Unknown kind(s): {', '.join(unknown_kinds)}; this tool encodes "
                     f"{', '.join(ENCODABLE_KINDS)}.")
quality_for = {kind: (args.quality if args.quality is not None else DEFAULT_QUALITY[kind]) for kind in ('color', 'alpha')}
quality_for['lightmap'] = args.quality_lightmap if args.quality_lightmap is not None else DEFAULT_QUALITY['lightmap']
quality_for['normal'] = args.quality_normal if args.quality_normal is not None else DEFAULT_QUALITY['normal']

# The maps index names each bundle's manifest and display name, so read it rather than guessing.
# A plain bundle (the wardrobe) and --in-place need no registry entry at all.
index_path = MAPS / 'index.json'
registry = json.loads(index_path.read_text()) if index_path.is_file() else {'format': 's4-maps-index', 'version': 1, 'maps': []}
entry = None
if args.map:
    entry = next((item for item in registry['maps'] if item['id'] == args.map), None)
    if entry is None:
        raise SystemExit(f"Unknown map '{args.map}'. Run `make threejs map-viewer` to list the converted maps.")
    root = (MAPS / entry['directory']).resolve()
elif args.root:
    root = args.root.resolve()
    entry = next((item for item in registry['maps'] if (MAPS / item['directory']).resolve() == root), None)
    if entry is None and not args.in_place and args.manifest is None:
        raise SystemExit(f'{root} is not a registered map bundle; the maps index names each bundle\'s manifest. '
                         'Pass --manifest (and --in-place, or --directory/--name) for a bundle outside the registry.')
else:
    raise SystemExit('Pass a bundle directory or --map <id>.')
manifest_name = args.manifest or (entry['manifest'] if entry else 'index.json')
target_directory = root if args.in_place else \
    (args.directory or (root.parent / f'{root.name}-{args.level}-{args.format.upper()}')).resolve()
display_name = args.name or (f"{entry['name']} {args.level} {args.format.upper()}" if entry else root.name)
next_step_hint = (f'open the map and switch Texture quality between 1× and {args.level} — the encoded files are '
                  f'what {root.name} serves now' if args.in_place else
                  f'open the map and pick "{display_name}" in the Maps menu, then compare it with the PNG entry')
# The registry's own invariant: an entry's name is the bundle manifest's `name`, and its id is the
# slug of that name. A re-encoded bundle is its own map, so its manifest says so.
target_id = slug(display_name)

manifest = json.loads((root / manifest_name).read_text())
textures = manifest.get('textures', {})


def already_encoded(texture):
    """A level whose recorded file is already this format: re-running must never re-encode it."""
    level = texture.get('variants', {}).get(args.level)
    return bool(level) and Path(level['file']).suffix.lower() == f'.{extension}'


work = [(path, texture) for path, texture in sorted(textures.items())
        if texture.get('kind') in kinds and args.level in texture.get('variants', {}) and not already_encoded(texture)]
already = [path for path, texture in sorted(textures.items())
           if texture.get('kind') in kinds and args.level in texture.get('variants', {}) and already_encoded(texture)]
skipped = [(path, texture.get('kind')) for path, texture in sorted(textures.items())
           if texture.get('kind') not in kinds and args.level in texture.get('variants', {})]
by_kind = {kind: sum(1 for _, texture in work if texture['kind'] == kind) for kind in kinds}
header(f'Re-encode {args.level} textures as {args.format.upper()}', [
    ('bundle', str(root)),
    ('manifest', manifest_name),
    ('level', f'{args.level} · ' + ' · '.join(f'{count} {kind}' for kind, count in by_kind.items() if count) or 'nothing to encode'),
    ('already', f'{len(already)} of this level are already {args.format.upper()}'),
    ('skipped', f'{len(skipped)} left as PNG ({", ".join(sorted({kind or "unknown" for _, kind in skipped})) or "none"})'),
    ('format', f'{args.format} at ' + ' · '.join(f'{kind} q{quality_for[kind]}' for kind in kinds)),
    ('output', str(target_directory) + (' · dry run, nothing is written' if args.dry_run else '')),
    ('menu name', display_name),
])
if not work:
    # Re-running over a converted bundle is a no-op, not an error: the files already are this format
    # and encoding them again would re-compress them and lose quality.
    if already:
        emit('')
        emit(f'  nothing to do: all {len(already)} {args.level} colour/alpha textures are already '
             f'{args.format.upper()} ({", ".join(kinds)})')
        sys.exit(0)
    raise SystemExit(f'{root} records no {args.level} colour/alpha textures; convert that level first '
                     f'(`make threejs-map-upscale MAP={entry["id"]}`).')
encoder = {'avif': 'avifenc', 'webp': 'cwebp'}[args.format]
if shutil.which(encoder) is None:
    packages = {'avif': ('brew install libavif', 'apt install libavif-bin', 'libavif'),
                'webp': ('brew install webp', 'apt install webp', 'libwebp')}[args.format]
    raise SystemExit(f'{encoder} is not installed and is what does the encoding. macOS: {packages[0]}. '
                     f'Linux: {packages[1]}. Windows: install the {packages[2]} build and put '
                     f'{encoder}.exe on PATH.')

if args.dry_run:
    before = sum((root / texture['variants'][args.level]['file']).stat().st_size for _, texture in work)
    emit('')
    emit(f'  would encode {len(work)} files ({format_size(before)}) into {target_directory.name} as {args.format}')
    for kind in kinds:
        if by_kind.get(kind):
            emit(f'    {by_kind[kind]:>3} {kind} at q{quality_for[kind]}')
    emit(f'  would register "{display_name}" (id {target_id}) in {relative(index_path)}')
    sys.exit(0)

if args.in_place:
    start = time.monotonic()
    emit('')
    emit(f'  in place: rewriting {root.name}\'s own manifest and replacing the {args.level} PNGs with encoded files')
else:
    if target_directory.exists():
        raise SystemExit(f'{target_directory} already exists; remove it to re-encode from scratch.')
    start = time.monotonic()
    # A copy-on-write clone: the clone shares every unchanged file with its source until a file is
    # rewritten, so a trial bundle costs only the encoded files. `cp -c` is macOS-only (an APFS
    # clone), so everywhere else the files are genuinely copied — slower, same result.
    if sys.platform == 'darwin':
        subprocess.run(['cp', '-cR', str(root), str(target_directory)], check=True)
    else:
        shutil.copytree(root, target_directory)
    emit('')
    emit(f'  cloned {root.name} -> {target_directory.name} in {time.monotonic() - start:.1f}s'
         + (' (copy-on-write)' if sys.platform == 'darwin' else ' (full copy)'))

encoded = 0
before_total = 0
after_total = 0
failures = []
for path, texture in work:
    variant = texture['variants'][args.level]
    source = target_directory / variant['file']
    before_total += source.stat().st_size

for path, texture in work:
    variant = texture['variants'][args.level]
    source = target_directory / variant['file']
    target = source.with_suffix(f'.{extension}')
    quality = quality_for[texture['kind']]
    command = build_command(source, target, quality)
    result = subprocess.run(command, capture_output=True)
    if result.returncode != 0:
        failures.append((path, result.stderr.decode().strip()[:120]))
        continue
    encoded += 1
    after_total += target.stat().st_size
    emit(f'  {path.split("/")[-1]:<44} {format_size(source.stat().st_size):>9} -> {format_size(target.stat().st_size):>9}')
    # Provenance: the encoded file names the PNG it came from, and the recorded hashes let a re-run
    # see whether the source changed under it.
    variant['file'] = target.relative_to(target_directory).as_posix()
    variant['codec'] = args.format
    variant['quality'] = quality
    variant['encodedFromSha256'] = variant.get('generatedSha256')
    variant['algorithm'] = f"{variant.get('algorithm', 'deterministic-bilinear')}+{suffix}-q{quality}"
    variant['generatedSha256'] = hashlib.sha256(target.read_bytes()).hexdigest()
    source.unlink()   # the point of the pass: the PNG of this level is replaced by the encoded file
    # Keep the manifest in step with the files: a variant is rewritten before its PNG is deleted, and
    # the whole manifest is persisted every 25 files so a reload mid-run never asks for a missing file.
    if encoded % 25 == 0:
        (target_directory / manifest_name).write_text(json.dumps(manifest, indent=2) + '\n')

manifest['coverage'] = manifest.get('coverage', {})
manifest['coverage']['textureQuality'] = {
    **manifest['coverage'].get('textureQuality', {}),
    'encoded': {'level': args.level, 'format': args.format,
                'quality': {kind: quality_for[kind] for kind in kinds if by_kind.get(kind)},
                'kinds': [kind for kind in kinds if by_kind.get(kind)],
                'files': encoded, 'bytesBefore': before_total, 'bytesAfter': after_total, 'producer': encoder},
}
manifest.setdefault('provenance', {})
if args.in_place:
    # The bundle stays itself: same manifest name, same registry entry, only the level's files and
    # their variant records change.
    manifest['provenance']['encodedInPlace'] = f'{args.level} {args.format} q' + \
        '/'.join(f'{kind}:{quality_for[kind]}' for kind in kinds if by_kind.get(kind))
else:
    manifest['name'] = display_name
    manifest['provenance']['encodedFrom'] = f'{entry["directory"]}/{manifest_name}'
    manifest['provenance']['sourceMapName'] = entry['name']
(target_directory / manifest_name).write_text(json.dumps(manifest, indent=2) + '\n')

if not args.in_place:
    registry['maps'] = [item for item in registry['maps'] if item['id'] != target_id]
    clone_entry = dict(entry)
    clone_entry.update({'id': target_id, 'name': display_name, 'directory': target_directory.name,
                        'manifest': manifest_name})
    registry['maps'].append(clone_entry)
    registry['maps'].sort(key=lambda item: item['id'])
    index_path.write_text(json.dumps(registry, indent=2) + '\n')

emit('')
emit(f'  encoded          {encoded} of {len(work)} files · {format_size(before_total)} -> {format_size(after_total)} '
     f'({before_total / after_total:.2f}x smaller)' if after_total else '  encoded          0 files')
emit(f'  left as PNG      {len(skipped)} texture(s) not in --kinds ({", ".join(sorted({kind or "unknown" for _, kind in skipped})) or "none"})')
emit(f'  deterministic    {"re-run skips work whose encodedFromSha256 still matches" if not failures else "n/a"}')
if failures:
    emit(f'  FAILED           {len(failures)}: ' + ', '.join(name for name, _ in failures[:3]))
    for name, error in failures[:3]:
        emit(f'                   {name}: {error}')
if args.in_place:
    destination = 'the bundle keeps its own name and registry entry'
else:
    destination = f'"{display_name}" (id {target_id}) in {relative(index_path)}'
emit(f'  {"in place" if args.in_place else "registered":<16} {destination}')
emit(f'  total time       {time.monotonic() - start:.1f}s')
emit('')
emit(f'Next: {next_step_hint}')
sys.exit(1 if failures else 0)
