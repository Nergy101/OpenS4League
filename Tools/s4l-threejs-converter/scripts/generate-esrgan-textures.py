"""Generate the 4x (Real-ESRGAN x4plus) colour/alpha texture levels for an indexed bundle.

Run by `make threejs-asset-upscale` inside the local Real-ESRGAN venv. The pass is resumable: an
entry whose recorded `sourceSha256`/`generatedSha256` still match its file is reused untouched, so
an interrupted run continues instead of starting over. It reports a plan up front, one line per
generated level, a periodic progress line with rate and ETA, and a closing summary.

Only the 4x level is generated: an 8x level was tried and looked worse than 4x, so it is not
produced any more. Entries a bundle still records for 2x/8x are left untouched here —
`scripts/prune-texture-levels.py` removes those variants, files and index entries together.
"""

import argparse
import hashlib
import json
import sys
import time
from pathlib import Path
from typing import NamedTuple

import cv2
import numpy as np
from PIL import Image
import torch
import types
from torchvision.transforms.functional import rgb_to_grayscale

sys.path.insert(0, str(Path(__file__).resolve().parent))

from progress import RULE, emit, format_duration, format_size, header  # noqa: E402

shim = types.ModuleType('torchvision.transforms.functional_tensor')
shim.rgb_to_grayscale = rgb_to_grayscale
sys.modules['torchvision.transforms.functional_tensor'] = shim
from basicsr.archs.rrdbnet_arch import RRDBNet
from realesrgan import RealESRGANer

ALGORITHM = 'realesrgan-x4plus-rgb-alpha-lanczos'
# Pinned weights, bootstrapped by `make threejs-asset-upscale`. Passing --weights is optional.
CACHE = Path(__file__).resolve().parents[3] / '.cache/opens4l-realesrgan'
DEFAULT_WEIGHTS = CACHE / 'RealESRGAN_x4plus.pth'

parser = argparse.ArgumentParser(description='Generate the 4x (Real-ESRGAN x4plus) colour/alpha texture levels for an indexed bundle.')
parser.add_argument('--root', type=Path, required=True,
                    help='bundle directory (the wardrobe, or one converted map)')
parser.add_argument('--manifest', default='index.json',
                    help="bundle manifest inside --root (default: index.json; a map uses its <stem>.json)")
parser.add_argument('--weights', type=Path, default=DEFAULT_WEIGHTS,
                    help=f'x4 model weights (default: {DEFAULT_WEIGHTS})')
parser.add_argument('--tile', type=int, default=0)
parser.add_argument('--progress-every', type=int, default=25,
                    help='Print a progress line every N entries (default: 25)')
args = parser.parse_args()

if not args.weights.is_file():
    raise SystemExit(f'x4 model weights not found: {args.weights}\n'
                     'Run `make threejs-asset-upscale` from the repository root to download them, or pass --weights <file>.')

index_path = args.root / args.manifest
index = json.loads(index_path.read_text())
device = torch.device('mps' if torch.backends.mps.is_available() else 'cpu')
model = RRDBNet(num_in_ch=3, num_out_ch=3, num_feat=64, num_block=23, num_grow_ch=32, scale=4)
upsampler = RealESRGANer(scale=4, model_path=str(args.weights), model=model, tile=args.tile, tile_pad=10, pre_pad=0, half=False, device=device)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def upscale(source_path, target_path):
    """One Real-ESRGAN x4 inference pass over the decoded source; alpha is resampled separately."""
    with Image.open(source_path).convert('RGBA') as image:
        rgba = np.array(image)
    bgr = cv2.cvtColor(rgba[:, :, :3], cv2.COLOR_RGB2BGR)
    output, _ = upsampler.enhance(bgr, outscale=4)
    rgb = cv2.cvtColor(output, cv2.COLOR_BGR2RGB)
    alpha = cv2.resize(rgba[:, :, 3], (rgba.shape[1] * 4, rgba.shape[0] * 4), interpolation=cv2.INTER_LANCZOS4)
    result = np.dstack((rgb, alpha))
    target_path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(result, 'RGBA').save(target_path, compress_level=6)
    return result.shape[1], result.shape[0]


def up_to_date(entry, source_sha, target):
    return (
        target.exists()
        and entry.get('sourceSha256') == source_sha
        and entry.get('algorithm') == ALGORITHM
        and entry.get('generatedSha256') == sha(target)
    )


class Work(NamedTuple):
    """One indexed colour/alpha texture and the 4x level it still needs."""

    name: str
    source_file: Path
    source_sha: str
    source_width: int
    source_height: int
    entry4: dict
    target4: Path
    needs4: bool


entries = [(path, descriptor) for path, descriptor in index['textures'].items() if descriptor.get('kind') in ('color', 'alpha')]
total = len(entries)
header('Real-ESRGAN colour/alpha levels', [
    ('bundle', str(args.root)),
    ('manifest', index_path.name),
    ('device', str(device)),
    ('entries', f'{total} colour/alpha textures indexed'),
    ('x4 model', f'{args.weights.name} ({format_size(args.weights.stat().st_size)}) · {ALGORITHM}'),
])

# ── Plan ────────────────────────────────────────────────────────────────────────────────────────
# Each entry is checked against its recorded hashes and dimensions before any inference runs, so
# the run starts by saying exactly how much work is left. This is the same verification the pass
# performs anyway; its result is carried into the pass, so nothing is hashed twice.
emit('')
emit('Plan — checking every indexed entry against its recorded hashes')
scan_start = time.monotonic()
plan: list[Work] = []
for number, (name, descriptor) in enumerate(entries, 1):
    variants = descriptor['variants']
    source_file = args.root / variants['1x']['file']
    source_sha = sha(source_file)
    # A 4x level the converter did not emit (e.g. a 1x-only index) is created here from the naming
    # convention, so this pass always produces the enhanced colour/alpha set.
    entry4 = variants.setdefault('4x', {'file': variants['1x']['file'].replace('.1x.png', '.4x.png')})
    if not entry4.get('file'):
        entry4['file'] = variants['1x']['file'].replace('.1x.png', '.4x.png')
    target4 = args.root / entry4['file']
    if target4.resolve() == source_file.resolve():
        raise SystemExit(f"{name}: the recorded 4x file is the 1x source ({entry4['file']}); "
                         'refusing to overwrite the decoded original — fix the manifest variant files first.')
    source_width, source_height = variants['1x']['width'], variants['1x']['height']
    plan.append(Work(name, source_file, source_sha, source_width, source_height, entry4, target4,
                     not up_to_date(entry4, source_sha, target4)))
    if number % 250 == 0 or number == total:
        emit(f'      scanned {number}/{total} · {sum(w.needs4 for w in plan)} 4x levels still to generate')

pending4 = sum(work.needs4 for work in plan)
total_units = pending4
scan_time = time.monotonic() - scan_start
emit(f'      plan ready in {format_duration(scan_time)}: {total_units} levels to generate, '
     f'{total - total_units} of {total} already up to date')

upscaled = 0    # newly generated levels this run
reused = 0      # shared a source with an entry already generated this run
skipped = 0     # already up to date, untouched
failures = 0
done_units = 0
aborted = False
generated = {}  # source_file -> (width, height), reused for entries sharing a source within this run
start_time = time.monotonic()
number = 0

if total_units:
    emit('')
    emit(f'Generating {total_units} levels · one line per generated level, progress every {args.progress_every} entries')
else:
    emit('')
    emit('Nothing to generate: every indexed level already matches its recorded hashes.')
try:
    for number, work in enumerate(plan, 1):
        entry_progress = f'[{number}/{total}]'
        if not work.needs4:
            skipped += 1
        else:
            level_start = time.monotonic()
            try:
                if work.source_file in generated:
                    width4, height4 = generated[work.source_file]
                    reused += 1
                    detail = f'shared source, level recorded as {width4}x{height4}'
                else:
                    width4, height4 = upscale(work.source_file, work.target4)
                    generated[work.source_file] = (width4, height4)
                    upscaled += 1
                    detail = f'{width4}x{height4} in {format_duration(time.monotonic() - level_start)}'
                work.entry4.update({'width': width4, 'height': height4, 'algorithm': ALGORITHM,
                                    'sourceWidth': work.source_width, 'sourceHeight': work.source_height,
                                    'sourceSha256': work.source_sha, 'generatedSha256': sha(work.target4)})
                work.entry4.pop('derivedFromSha256', None)
                # Persist after every generated entry (not just on batch boundaries) so a Ctrl-C
                # never loses already-upscaled work: rerunning the make target resumes from here.
                index_path.write_text(json.dumps(index, indent=2) + '\n')
                emit(f'      {entry_progress} 4x {detail} · {work.name}')
            except Exception as error:  # one unreadable texture must not end an hours-long pass
                failures += 1
                emit(f'      {entry_progress} 4x FAILED · {work.name} · {type(error).__name__}: {error}')
            else:
                done_units += 1

        if total_units and (number % args.progress_every == 0 or number == total):
            elapsed = time.monotonic() - start_time
            rate = done_units / elapsed if elapsed > 0 else 0
            remaining = (total_units - done_units) / rate if rate > 0 else 0
            percent = done_units / total_units * 100 if total_units else 100
            emit(f'      progress {number}/{total} entries · {done_units}/{total_units} levels ({percent:.0f}%) · '
                 f'{format_duration(elapsed)} elapsed · eta {format_duration(remaining)}')
            emit(f'               4x {upscaled} generated, {reused} shared source, {skipped} up to date · {failures} failed')

        # Five failures before anything has succeeded means the model or the device is broken
        # rather than the assets, so the run stops instead of failing once per texture.
        if failures >= 5 and done_units == 0:
            emit('')
            emit(f'      {failures} levels failed before a single success: the model or device looks broken, not the assets.')
            aborted = True
            break
except KeyboardInterrupt:
    emit('')
    emit(f'      Interrupted at {number}/{total} — progress saved, rerun make threejs-asset-upscale to resume.')
    sys.exit(130)

elapsed = time.monotonic() - start_time
emit('')
emit(RULE)
emit(f'Real-ESRGAN pass {"aborted early" if aborted else "finished"} in {format_duration(elapsed)}')
emit(f'  4x   {upscaled} generated · {reused} shared source · {skipped} already up to date')
emit(f'  plan {total_units} of {total} levels needed work; verifying them took {format_duration(scan_time)}')
if failures:
    emit(f'  {failures} level(s) FAILED and are recorded unmodified in the index; rerun to retry them')
if aborted:
    emit('  the model or device looks broken; the pass stopped instead of failing the whole bundle')
emit(f'  wrote {index_path}')
sys.exit(3 if aborted else 1 if failures else 0)