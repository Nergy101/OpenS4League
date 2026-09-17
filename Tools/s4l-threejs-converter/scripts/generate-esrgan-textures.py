import argparse, hashlib, json, sys, time
from pathlib import Path
import cv2
import numpy as np
from PIL import Image
import torch
import types
from torchvision.transforms.functional import rgb_to_grayscale
shim = types.ModuleType('torchvision.transforms.functional_tensor')
shim.rgb_to_grayscale = rgb_to_grayscale
sys.modules['torchvision.transforms.functional_tensor'] = shim
from basicsr.archs.rrdbnet_arch import RRDBNet
from realesrgan import RealESRGANer

ALGORITHM = 'realesrgan-x4plus-rgb-alpha-lanczos'

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, required=True)
parser.add_argument('--weights', type=Path, required=True)
parser.add_argument('--tile', type=int, default=0)
args = parser.parse_args()

index_path = args.root / 'index.json'
index = json.loads(index_path.read_text())
model = RRDBNet(num_in_ch=3, num_out_ch=3, num_feat=64, num_block=23, num_grow_ch=32, scale=4)
device = torch.device('mps' if torch.backends.mps.is_available() else 'cpu')
print(f'Using device: {device}', flush=True)
upsampler = RealESRGANer(scale=4, model_path=str(args.weights), model=model, tile=args.tile, tile_pad=10, pre_pad=0, half=False, device=device)

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def upscale(source_path, target_path, scale):
    with Image.open(source_path).convert('RGBA') as image:
        rgba = np.array(image)
    bgr = cv2.cvtColor(rgba[:, :, :3], cv2.COLOR_RGB2BGR)
    output, _ = upsampler.enhance(bgr, outscale=scale)
    rgb = cv2.cvtColor(output, cv2.COLOR_BGR2RGB)
    alpha = cv2.resize(rgba[:, :, 3], (rgba.shape[1] * scale, rgba.shape[0] * scale), interpolation=cv2.INTER_LANCZOS4)
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

def format_duration(seconds):
    seconds = max(0, int(seconds))
    hours, remainder = divmod(seconds, 3600)
    minutes, seconds = divmod(remainder, 60)
    if hours: return f'{hours}h{minutes:02d}m{seconds:02d}s'
    if minutes: return f'{minutes}m{seconds:02d}s'
    return f'{seconds}s'

entries = [(path, descriptor) for path, descriptor in index['textures'].items() if descriptor.get('kind') in ('color', 'alpha')]
total = len(entries)
print(f'ESRGAN color textures: {total}', flush=True)
generated = {}  # source_file -> (width, height), reused for entries sharing a source within this run
upscaled = 0   # newly generated this run
reused = 0     # shared a source with an entry already generated this run
skipped = 0    # already up to date, untouched
start_time = time.monotonic()
batch_start = start_time
batch_skipped = 0
batch_count = 0
number = 0
try:
    for number, (source, descriptor) in enumerate(entries, 1):
        variants = descriptor['variants']
        source_file = args.root / variants['1x']['file']
        source_sha = sha(source_file)
        target4 = args.root / variants['4x']['file']
        entry4 = variants['4x']

        if up_to_date(entry4, source_sha, target4):
            skipped += 1
            batch_skipped += 1
        else:
            if source_file in generated:
                width4, height4 = generated[source_file]
                reused += 1
            else:
                width4, height4 = upscale(source_file, target4, 4)
                generated[source_file] = (width4, height4)
                upscaled += 1

            entry4.update({'width': width4, 'height': height4, 'algorithm': ALGORITHM, 'sourceWidth': variants['1x']['width'], 'sourceHeight': variants['1x']['height'], 'sourceSha256': source_sha, 'generatedSha256': sha(target4)})
            # Persist after every generated entry (not just on batch boundaries) so a Ctrl-C
            # never loses already-upscaled work: rerunning the make target resumes from here.
            index_path.write_text(json.dumps(index, indent=2) + '\n')

        batch_count += 1
        if number % 10 == 0 or number == total:
            now = time.monotonic()
            batch_time = now - batch_start
            elapsed = now - start_time
            # Use the recent batch's rate, not the cumulative average since start: a run
            # resumed after mostly-skipped (already up to date) entries would otherwise have
            # its average dragged down by that near-instant skip time, understating the ETA
            # once real upscale work follows.
            rate = batch_time / batch_count if batch_count else 0
            remaining = rate * (total - number)
            print(f'{number}/{total} {source} | batch {format_duration(batch_time)} (skipped {batch_skipped}) | elapsed {format_duration(elapsed)} | eta {format_duration(remaining)}', flush=True)
            batch_start = now
            batch_skipped = 0
            batch_count = 0
except KeyboardInterrupt:
    print(f'\nInterrupted at {number}/{total} — progress saved, rerun make threejs-asset-upscale to resume.', flush=True)
    sys.exit(130)

print(
    f'Done in {format_duration(time.monotonic() - start_time)}: '
    f'{total} textures total, {upscaled} upscaled, {reused} reused (shared source), {skipped} already up to date (skipped).',
    flush=True,
)
