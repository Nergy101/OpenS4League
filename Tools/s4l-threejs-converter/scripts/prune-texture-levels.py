#!/usr/bin/env python3
"""Remove texture levels from an indexed bundle — the variant entries and their files together.

`make threejs-asset-upscale` generates 1x and 4x only. A bundle converted before that can still
carry 2x/8x variants: dropping the entries but leaving the PNGs wastes gigabytes, and deleting the
PNGs but leaving the entries leaves the index pointing at missing textures. This does both, for the
manifest and for any orphaned file of those levels, and updates the recorded `textureQuality` levels
so the pipeline's re-conversion guard sees the bundle as already converted.

The 1x level is never removed — it is the decoded original, and the only level that is not
generated.

Usage: prune-texture-levels.py <bundle-dir> --levels 2x,8x [--manifest index.json] [--dry-run]
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from progress import emit, format_size, header  # noqa: E402

# The order levels are recorded in, and the ladder the runtime knows about.
LEVEL_ORDER = ['1x', '2x', '4x']

parser = argparse.ArgumentParser(description='Remove texture levels (and their files) from an indexed bundle.')
parser.add_argument('root', type=Path, help='bundle directory (the wardrobe, or one converted map)')
parser.add_argument('--manifest', default='index.json',
                    help='bundle manifest inside the bundle (default: index.json; a map uses its <stem>.json)')
parser.add_argument('--levels', default='', help='comma-separated levels to remove, e.g. 2x,8x')
parser.add_argument('--unreferenced', action='store_true',
                    help='also delete texture files no manifest variant references (e.g. left by an older naming convention)')
parser.add_argument('--dry-run', action='store_true', help='report what would be removed and change nothing')
args = parser.parse_args()

levels = [level.strip() for level in args.levels.split(',') if level.strip()]
if not levels and not args.unreferenced:
    raise SystemExit('Nothing to do: pass --levels <levels> and/or --unreferenced.')
if '1x' in levels:
    raise SystemExit('Refusing to remove 1x: it is the decoded original, not a generated level.')
unknown = [level for level in levels if level not in LEVEL_ORDER and level != '8x']
if unknown:
    raise SystemExit(f'Unknown level(s): {", ".join(unknown)}; this bundle format has 1x, 2x, 4x (and a legacy 8x).')

root = args.root.resolve()
manifest_path = root / args.manifest
if not manifest_path.is_file():
    raise SystemExit(f'No manifest at {manifest_path}')
manifest = json.loads(manifest_path.read_text())

header('Prune texture levels', [
    ('bundle', str(root)),
    ('manifest', manifest_path.name),
    ('levels', (','.join(levels) or 'none') + (' · dry run, nothing is deleted' if args.dry_run else '')),
])

removed_files = 0
freed_bytes = 0
removed_entries = 0
for level in levels:
    # Every file of that level goes, including any an interrupted run left unreferenced.
    for file in sorted(root.rglob(f'*.{level}.png')):
        if not file.resolve().is_relative_to(root):
            continue
        removed_files += 1
        freed_bytes += file.stat().st_size
        if not args.dry_run:
            file.unlink()
    for descriptor in manifest.get('textures', {}).values():
        if descriptor.get('variants', {}).pop(level, None) is not None:
            removed_entries += 1

remaining = [level for level in LEVEL_ORDER if any(level in descriptor.get('variants', {})
                                                   for descriptor in manifest.get('textures', {}).values())]
textures = manifest.get('textures', {})
missing = [name for name, descriptor in textures.items() if not descriptor.get('variants')]
if missing:
    raise SystemExit(f'{len(missing)} texture(s) would have no variants left (e.g. {missing[0]}); '
                     'refusing to write a manifest with unloadable textures.')

# Files the manifest does not reference: an older naming convention, or a level an interrupted run
# wrote before its entry landed. They cost disk and confuse the next conversion. Only the bundle's
# own `textures/` directory is swept — preserved `source/` files are never touched.
orphaned = 0
if args.unreferenced:
    referenced = {str((root / variant['file']).resolve())
                  for descriptor in textures.values()
                  for variant in descriptor.get('variants', {}).values() if variant.get('file')}
    for file in sorted((root / 'textures').rglob('*.png')) if (root / 'textures').is_dir() else []:
        if str(file.resolve()) in referenced or not file.resolve().is_relative_to(root):
            continue
        orphaned += 1
        freed_bytes += file.stat().st_size
        if not args.dry_run:
            file.unlink()

if not args.dry_run:
    quality = manifest.setdefault('coverage', {}).setdefault('textureQuality', {})
    quality['requested'] = remaining
    quality['pruned'] = sorted(set(quality.get('pruned', [])) | set(levels))
    manifest_path.write_text(json.dumps(manifest, indent=2) + '\n')
    # The sibling coverage record mirrors the level list; keep the two from disagreeing.
    coverage_path = root / 'coverage.json'
    if coverage_path.is_file():
        coverage = json.loads(coverage_path.read_text())
        mirror = coverage.setdefault('textureQuality', {})
        mirror['requested'] = remaining
        mirror['pruned'] = sorted(set(mirror.get('pruned', [])) | set(levels))
        coverage_path.write_text(json.dumps(coverage, indent=2) + '\n')

emit('')
emit(f'  levels removed   {", ".join(levels) or "none"}')
emit(f'  variant entries  {removed_entries} removed from {len(textures)} textures')
emit(f'  files            {removed_files} level file(s) and {orphaned} unreferenced file(s) {"would be deleted" if args.dry_run else "deleted"} · {format_size(freed_bytes)}')
emit(f'  levels remaining {", ".join(remaining) or "none"}')
emit(f'  manifest         {manifest_path.name} recorded requested={",".join(remaining)}{" (unchanged: dry run)" if args.dry_run else ""}')
emit('')
emit('Next: make threejs character-viewer (the select now offers exactly these levels)')