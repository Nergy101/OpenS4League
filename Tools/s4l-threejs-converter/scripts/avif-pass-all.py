#!/usr/bin/env python3
"""Bring every asset to the final state: generated 4x levels, encoded as AVIF in place.

For the wardrobe and for every registered map this runs, in order:

1. `upscale-assets.py` — the Real-ESRGAN 4x pass (skipped when the manifest already records `1x,4x`).
2. `encode-texture-levels.py --in-place` — colour/alpha/lightmap `4x` levels re-encoded as AVIF, the
   replaced PNGs deleted, the bundle's own manifest updated. Nothing is left as a second bundle and no
   registry entry is added: the point of this run is the space.

It is resumable: a bundle already converted is skipped, and a map that fails is reported and the run
continues with the next one. Every step's log goes to the file given by --log, and the totals are
written as JSON so the saving can be reported without re-measuring.

Usage: avif-pass-all.py --source <client.zip> [--log <file>] [--report <file>]
       [--skip-wardrobe] [--skip-maps] [--map <id>]... [--format avif] [--dry-run]
"""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from progress import emit, format_size, header  # noqa: E402

ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = Path(__file__).resolve().parent
MAPS = ROOT / 'Client/Models/Maps'
WARDROBE = ROOT / 'Client/Models/Characters/Wardrobe'
# Trial/derived bundles: they exist to compare encodings, so the batch leaves them alone.
DERIVED_SUFFIXES = ('-optimizt', '-4x-avif', '-4x-webp')

parser = argparse.ArgumentParser(description='Run the Real-ESRGAN 4x pass and the in-place AVIF encode for every asset.')
parser.add_argument('--source', type=Path, help='your unpacked Season-8 client ZIP (needed when a 4x pass still has to run)')
parser.add_argument('--python', type=Path, default=Path(sys.executable),
                    help='interpreter for the child passes (default: the one running this script)')
parser.add_argument('--format', choices=('avif', 'webp'), default='avif')
parser.add_argument('--log', type=Path, default=Path('/tmp/opens4l-avif-pass.log'))
parser.add_argument('--report', type=Path, default=Path('/tmp/opens4l-avif-pass.json'))
parser.add_argument('--skip-wardrobe', action='store_true')
parser.add_argument('--skip-maps', action='store_true')
parser.add_argument('--map', action='append', default=[], help='restrict the map pass to these ids (repeatable)')
parser.add_argument('--dry-run', action='store_true', help='report what would run and change nothing')
args = parser.parse_args()

log = args.log.open('a', encoding='utf-8')


def run(label: str, command: list[object]) -> int:
    emit('')
    emit(f'== {label}')
    emit(f'   $ ' + ' '.join(str(part) for part in command))
    log.write(f'\n== {label}\n$ ' + ' '.join(str(part) for part in command) + '\n')
    started = time.monotonic()
    process = subprocess.run([str(part) for part in command], stdout=subprocess.PIPE,
                             stderr=subprocess.STDOUT, text=True, cwd=ROOT)
    for line in process.stdout.splitlines():
        log.write(line + '\n')
    log.flush()
    emit(f'   exit {process.returncode} in {time.monotonic() - started:.0f}s')
    return process.returncode


def four_x_bytes(bundle: Path, manifest_name: str) -> int:
    """What the generated level weighs right now: PNG and/or encoded files, as recorded."""
    manifest_path = bundle / manifest_name
    if not manifest_path.is_file():
        return 0
    manifest = json.loads(manifest_path.read_text())
    total = 0
    for texture in manifest.get('textures', {}).values():
        level = texture.get('variants', {}).get('4x')
        if not level:
            continue
        file = bundle / level['file']
        if file.is_file():
            total += file.stat().st_size
    return total


def bundle_total(bundle: Path) -> int:
    return sum(file.stat().st_size for file in bundle.rglob('*') if file.is_file()) if bundle.is_dir() else 0


def four_x_state(bundle: Path, manifest_name: str) -> tuple[int, int]:
    """How much of the generated level is already encoded (encoded count, total count)."""
    manifest_path = bundle / manifest_name
    if not manifest_path.is_file():
        return 0, 0
    extension = {'avif': '.avif', 'webp': '.webp'}[args.format]
    encoded = total = 0
    for texture in json.loads(manifest_path.read_text()).get('textures', {}).values():
        level = texture.get('variants', {}).get('4x')
        if not level:
            continue
        total += 1
        if Path(level['file']).suffix.lower() == extension:
            encoded += 1
    return encoded, total


def encode_args(bundle: Path, manifest_name: str, kinds: str) -> list[object]:
    return [args.python, SCRIPTS / 'encode-texture-levels.py', bundle, '--manifest', manifest_name,
            '--in-place', '--format', args.format, '--kinds', kinds]


def registry_entries() -> list[dict]:
    index_path = MAPS / 'index.json'
    if not index_path.is_file():
        return []
    return [entry for entry in json.loads(index_path.read_text()).get('maps', [])
            if not entry['id'].endswith(DERIVED_SUFFIXES)]


def levels_recorded(bundle: Path, manifest_name: str) -> str:
    result = subprocess.run([str(args.python), str(SCRIPTS / 'bundle-qualities.py'), str(bundle),
                             '--manifest', manifest_name], capture_output=True, text=True)
    return result.stdout.strip() if result.returncode == 0 else ''


def four_x_missing(bundle: Path, manifest_name: str) -> int:
    """Textures with no 4x variant. A killed pass leaves some behind while bundle-qualities already
    reports 1x,4x for the others, so the level count alone cannot be trusted to mean 'done'."""
    manifest_path = bundle / manifest_name
    if not manifest_path.is_file():
        return 1
    textures = json.loads(manifest_path.read_text()).get('textures', {})
    if not textures:
        return 1
    return sum(1 for texture in textures.values() if '4x' not in texture.get('variants', {}))


def needs_four_x(bundle: Path, manifest_name: str) -> bool:
    return levels_recorded(bundle, manifest_name) != '1x,4x' or four_x_missing(bundle, manifest_name) > 0


header('OpenS4L — 4× pass and AVIF encode for every asset', [
    ('format', args.format),
    ('log', str(args.log)),
    ('report', str(args.report)),
    ('mode', 'dry run, nothing is written' if args.dry_run else 'in place: generated PNGs are replaced by encoded files'),
])

results: list[dict] = []
failures: list[str] = []

assets: list[tuple[str, Path, str, str]] = []
if not args.skip_wardrobe:
    # Normal maps are part of the wardrobe's final state (--kinds names them explicitly, at q90);
    # nothing here encodes them by accident, the script's own default leaves them alone.
    assets.append(('wardrobe', WARDROBE, 'index.json', 'color,alpha,lightmap,normal'))
for entry in ([] if args.skip_maps else registry_entries()):
    if args.map and entry['id'] not in args.map:
        continue
    assets.append((entry['id'], MAPS / entry['directory'], entry['manifest'], 'color,alpha,lightmap,normal'))

if args.source:
    for label, bundle, manifest_name, _ in assets:
        if needs_four_x(bundle, manifest_name):
            emit(f'  {label}: 4x pass will run ({four_x_missing(bundle, manifest_name)} textures without a 4x level)')
elif any(needs_four_x(bundle, manifest_name) for _, bundle, manifest_name, _ in assets):
    raise SystemExit('A bundle is missing its 4x levels: pass --source <client.zip> so the ESRGAN pass can run.')


def summarise():
    ok = [row for row in results if row['status'] == 'ok']
    return {
        'format': args.format,
        'assets': results,
        'failures': failures,
        'fourXBeforeBytes': sum(row['fourXBeforeBytes'] for row in ok),
        'fourXAfterBytes': sum(row['fourXAfterBytes'] for row in ok),
        'bundleBeforeBytes': sum(row['bundleBeforeBytes'] for row in ok),
        'bundleAfterBytes': sum(row['bundleAfterBytes'] for row in ok),
        'converted': len(ok),
    }


def save_report():
    """Written after every asset, so a stopped or failed run still leaves the totals on disk."""
    if args.dry_run:
        return
    args.report.write_text(json.dumps(summarise(), indent=2) + '\n')


for label, bundle, manifest_name, kinds in assets:
    before_total = bundle_total(bundle)
    levels = levels_recorded(bundle, manifest_name)
    existing_four_x = four_x_bytes(bundle, manifest_name)
    encoded_count, level_count = four_x_state(bundle, manifest_name)
    if level_count and encoded_count == level_count:
        # Already converted: re-encoding would re-compress the encoded files and lose quality.
        emit(f'  {label:<24} already {args.format.upper()} ({encoded_count} of {level_count} textures) — skipped')
        results.append({'asset': label, 'status': 'already encoded', 'fourXBeforeBytes': existing_four_x,
                        'fourXAfterBytes': existing_four_x, 'bundleBeforeBytes': before_total,
                        'bundleAfterBytes': before_total})
        save_report()
        continue
    if args.dry_run:
        emit(f'  {label:<24} levels {levels or "none":<10} 4x {format_size(existing_four_x):>9} '
             f'bundle {format_size(before_total):>9} -> would run 4x pass + {args.format} encode')
        continue
    started = time.monotonic()
    if needs_four_x(bundle, manifest_name):
        code = run(f'{label}: Real-ESRGAN 4x pass',
                   [args.python, SCRIPTS / 'upscale-assets.py', '--map', label, '--source', args.source,
                    '--python', args.python, '--log', args.log.with_suffix(f'.{label}.upscale.log')])
        if code != 0:
            failures.append(f'{label} (4x pass, exit {code})')
            results.append({'asset': label, 'status': 'failed at 4x pass', 'exit': code})
            continue
    # The PNG this AVIF replaces: measured after the pass, before the encode, so a map that had no
    # generated level yet still reports what that level cost as PNG.
    png_four_x = four_x_bytes(bundle, manifest_name)
    code = run(f'{label}: {args.format} encode (in place)', encode_args(bundle, manifest_name, kinds))
    if code != 0:
        failures.append(f'{label} ({args.format} encode, exit {code})')
        results.append({'asset': label, 'status': f'failed at {args.format} encode', 'exit': code})
        continue
    after_four_x = four_x_bytes(bundle, manifest_name)
    after_total = bundle_total(bundle)
    results.append({'asset': label, 'status': 'ok', 'seconds': round(time.monotonic() - started),
                    'fourXBeforeBytes': png_four_x, 'fourXAfterBytes': after_four_x,
                    'bundleBeforeBytes': before_total, 'bundleAfterBytes': after_total})
    emit(f'  {label:<24} 4x {format_size(png_four_x):>9} -> {format_size(after_four_x):>9} · '
         f'bundle {format_size(before_total):>9} -> {format_size(after_total):>9}')
    save_report()


ok = [row for row in results if row['status'] == 'ok']
summary = summarise()
save_report()

emit('')
emit(f'  assets converted   {len(ok)} of {len(assets)}' + (f' · {len(failures)} failed' if failures else ''))
if ok:
    emit(f'  generated level    {format_size(summary["fourXBeforeBytes"])} -> {format_size(summary["fourXAfterBytes"])} '
         f'({summary["fourXBeforeBytes"] / max(1, summary["fourXAfterBytes"]):.1f}x smaller)')
    emit(f'  whole bundles      {format_size(summary["bundleBeforeBytes"])} -> {format_size(summary["bundleAfterBytes"])} '
         f'(saved {format_size(summary["bundleBeforeBytes"] - summary["bundleAfterBytes"])})')
for failure in failures:
    emit(f'  FAILED             {failure}')
emit(f'  report             {args.report}')
log.close()
sys.exit(1 if failures else 0)
