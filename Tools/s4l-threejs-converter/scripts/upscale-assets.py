#!/usr/bin/env python3
"""Run a Three.js texture-upscale pipeline with a structured progress log.

Two entry points use this script:

* `make threejs-asset-upscale` (no `--map`) — the character wardrobe.
* `make threejs-map-upscale MAP=<stem>` (`--map`) — one converted map, e.g. Station-2.

The pipeline is six phases: validate the source archive, bootstrap a Real-ESRGAN environment,
install the pinned packages, download the pinned model weights, convert the bundle to 1x/4x when its
manifest does not already record those levels, and finally regenerate the 4x colour and alpha levels
with Real-ESRGAN.

This script changes nothing about *what* the pipeline produces — same interpreter selection, same
`realesrgan==0.3.0` pin, same weights, same converter invocation, same resume behaviour. It only
reports each phase's start, inputs, streamed output, outcome and duration, so an hours-long run is
legible and a failure names the phase that died instead of ending in silence. Every line is written
to a log file as well, and an interrupt still prints what finished and what is left.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from progress import (  # noqa: E402
    Step, close, emit, format_clock, format_duration, format_size, header, now, set_log, summary,
)

ROOT = Path(__file__).resolve().parents[3]
CONVERTER = ROOT / 'Tools/s4l-threejs-converter'
SCRIPTS = CONVERTER / 'scripts'
MAPS = ROOT / 'Client/Models/Maps'
# Pinned for reproducibility: the x4 model is Real-ESRGAN x4plus. There is no x8 pass any more —
# an 8x level was generated once, looked worse than the 4x level, and was dropped.
WEIGHTS_URL = 'https://github.com/xinntao/Real-ESRGAN/releases/download/v0.1.0/RealESRGAN_x4plus.pth'
REQUIRED_LEVELS = '1x,4x'
PHASES = 6

parser = argparse.ArgumentParser(description='Run a Three.js texture-upscale pipeline with a structured progress log.')
parser.add_argument('--source', type=Path, default=None, help='your unpacked Season-8 client ZIP (never committed)')
parser.add_argument('--map', default=None,
                    help="convert and upscale one map (its recipe stem, e.g. station-2) instead of the wardrobe")
parser.add_argument('--assets', type=Path, default=ROOT / 'Client/Models/Characters/Wardrobe',
                    help='wardrobe bundle directory (ignored with --map)')
parser.add_argument('--cache', type=Path, default=ROOT / '.cache/opens4l-realesrgan')
parser.add_argument('--python', type=Path, default=Path(sys.executable), help='interpreter used to create the Real-ESRGAN venv')
parser.add_argument('--log', type=Path, default=None,
                    help='log file (default: <cache>/logs/upscale-<timestamp>.log; every line is written there too)')
parser.add_argument('--torch-index-url', default=None,
                    help='pip index for the torch wheel, overriding the detection (e.g. '
                         'https://download.pytorch.org/whl/cu121 for an older driver)')
parser.add_argument('--print-plan', action='store_true',
                    help='print which torch wheel this machine would install and exit (no archive needed)')
args = parser.parse_args()

# A machine with an NVIDIA GPU must get torch from the CUDA index: pip's default is the CPU-only wheel
# on Windows and Linux, which turns the 4x pass into a crawl with no hint why. macOS is left alone —
# its default wheel already carries MPS. Set OPENS4L_PLATFORM/OPENS4L_CUDA to preview another machine's
# plan (`--print-plan`), which is also what the tests assert against.
TORCH_CUDA_INDEX = 'https://download.pytorch.org/whl/cu124'


def platform_name() -> str:
    return os.environ.get('OPENS4L_PLATFORM') or sys.platform


def cuda_gpu_present() -> bool:
    """True when the driver reports an NVIDIA GPU. nvidia-smi ships with the driver on Windows and
    Linux; without a driver a CUDA wheel would not run anyway, so its absence is a valid 'no'."""
    override = os.environ.get('OPENS4L_CUDA')
    if override in ('0', '1'):
        return override == '1'
    smi = shutil.which('nvidia-smi')
    if smi is None:
        return False
    try:
        listed = subprocess.run([smi, '-L'], capture_output=True, text=True, timeout=30)
    except (OSError, subprocess.SubprocessError):
        return False
    return listed.returncode == 0 and 'GPU' in listed.stdout


def torch_cuda_available(venv_python: Path) -> bool:
    """Whether the venv's installed torch already has CUDA. A missing torch answers False."""
    if not venv_python.exists():
        return False
    try:
        result = subprocess.run([str(venv_python), '-c', 'import torch;print(int(torch.cuda.is_available()))'],
                                capture_output=True, text=True, timeout=180)
    except (OSError, subprocess.SubprocessError):
        return False
    return result.returncode == 0 and result.stdout.strip() == '1'


def torch_install_plan(platform: str, cuda_gpu: bool, override: str | None, installed_cuda: bool) -> list[str] | None:
    """The pip arguments that put the right torch in the venv, or None when the default wheel is right."""
    if platform == 'darwin' or not cuda_gpu:
        return None
    if installed_cuda and override is None:
        return None
    return ['-m', 'pip', 'install', '--index-url', override or TORCH_CUDA_INDEX, '--upgrade', 'torch', 'torchvision']


if args.print_plan:
    cuda = cuda_gpu_present()
    plan = torch_install_plan(platform_name(), cuda, args.torch_index_url, installed_cuda=False)
    print(f'  platform        {platform_name()}')
    print(f'  nvidia gpu      {"yes" if cuda else "no"}')
    print(f'  torch wheel     {" ".join(plan) if plan else "the default PyPI wheel (CPU on Windows/Linux, MPS on macOS)"}')
    raise SystemExit(0)


def resolve_map(stem: str) -> tuple[Path, str]:
    """The directory and manifest of the map a recipe stem refers to."""
    index_path = MAPS / 'index.json'
    if index_path.is_file():
        entry = next((entry for entry in json.loads(index_path.read_text()).get('maps', [])
                      if entry.get('id') == stem), None)
        if entry:
            return MAPS / entry['directory'], entry['manifest']
    recipe_path = CONVERTER / 'maps' / f'{stem}.json'
    if not recipe_path.is_file():
        raise SystemExit(f"Unknown map '{stem}': no converted map or recipe by that name. "
                         'Run `make threejs map-viewer` to list the converted maps.')
    recipe = json.loads(recipe_path.read_text())
    return MAPS / recipe['name'], f"{recipe['bundle']}.json"


if args.map:
    assets, manifest_name = resolve_map(args.map)
    title = f'OpenS4L — Three.js map upscale ({assets.name})'
    resume = f'make threejs-map-upscale MAP={args.map}'
    next_step_hint = f'make threejs map-viewer {args.map}'
else:
    assets = args.assets if args.assets.is_absolute() else ROOT / args.assets
    manifest_name = 'index.json'
    title = 'OpenS4L — Three.js wardrobe upscale'
    resume = 'make threejs-asset-upscale'
    next_step_hint = 'make threejs character-viewer'

manifest_path = assets / manifest_name
cache = args.cache if args.cache.is_absolute() else ROOT / args.cache
log_path = args.log or (cache / 'logs' / f'upscale-{time.strftime("%Y%m%d-%H%M%S")}.log')
if not log_path.is_absolute():
    log_path = ROOT / log_path
venv = cache / 'venv'
venv_python = venv / ('Scripts/python.exe' if os.name == 'nt' else 'bin/python')
weights_x4 = cache / 'RealESRGAN_x4plus.pth'
esrgan_script = SCRIPTS / 'generate-esrgan-textures.py'
qualities_script = SCRIPTS / 'bundle-qualities.py'

STEPS: list[Step] = []
CURRENT: Step | None = None
STARTED = time.monotonic()


def relative(path: Path) -> str:
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def python_version(interpreter: Path) -> str:
    try:
        result = subprocess.run([str(interpreter), '--version'], capture_output=True, text=True)
    except OSError:
        return 'unknown'
    return (result.stdout or result.stderr).strip() or 'unknown'


def index_levels() -> str:
    """The texture levels the existing manifest records, or '' when there is no usable manifest."""
    result = subprocess.run([str(args.python), str(qualities_script), str(assets), '--manifest', manifest_name],
                            capture_output=True, text=True)
    return result.stdout.strip() if result.returncode == 0 else ''


def manifest_textures() -> list[dict]:
    """Every indexed texture descriptor, or [] when there is no manifest to read."""
    if not manifest_path.is_file():
        return []
    try:
        index = json.loads(manifest_path.read_text())
    except (OSError, ValueError):
        return []
    return list(index.get('textures', {}).values())


def indexed_textures() -> list[dict]:
    """The colour/alpha descriptors — the only kinds a generative pass may touch."""
    return [descriptor for descriptor in manifest_textures() if descriptor.get('kind') in ('color', 'alpha')]


def level_files_present(descriptors: list[dict], level: str) -> int:
    """How many of those descriptors already have a file on disk for `level` (an advisory count)."""
    present = 0
    for descriptor in descriptors:
        file = descriptor.get('variants', {}).get(level, {}).get('file')
        if file and (assets / file).is_file():
            present += 1
    return present


def download(step: Step, url: str, target: Path) -> int:
    target.parent.mkdir(parents=True, exist_ok=True)
    step.note(f'downloading {target.name} from {url}')
    return step.stream(['curl', '-L', '--fail', '--retry', '3', '-o', str(target), url])


def next_step(label: str) -> Step:
    global CURRENT
    CURRENT = Step(len(STEPS) + 1, PHASES, label)
    STEPS.append(CURRENT)
    return CURRENT


def conversion_command() -> list[object]:
    """The converter invocation for the bundle this run targets."""
    if args.map:
        recipe = CONVERTER / 'maps' / f'{args.map}.json'
        return ['dotnet', 'run', '-c', 'Release', '--project', CONVERTER, '--',
                args.source, assets, '--map', recipe, '--texture-quality', REQUIRED_LEVELS]
    return ['dotnet', 'run', '-c', 'Release', '--project', CONVERTER, '--',
            args.source, assets, '--wardrobe', '--texture-quality', REQUIRED_LEVELS]


def main() -> int:
    levels = index_levels()
    set_log(log_path)
    target_row = ('map', f'{assets.name} · {relative(assets)}') if args.map else ('assets', relative(assets))
    header(title, [
        ('started', now()),
        ('source', f'{relative(args.source)} ({format_size(args.source.stat().st_size)})' if args.source.is_file() else str(args.source)),
        target_row,
        ('manifest', f'{manifest_name}' + (f' · records {levels}' if levels else ' · not converted yet')),
        ('cache', relative(cache)),
        ('python', f'{relative(args.python)} ({python_version(args.python)})'),
        ('levels', f'{REQUIRED_LEVELS} (deterministic) → colour/alpha 4x Real-ESRGAN x4plus'),
        ('log', relative(log_path)),
    ])

    # 1 — the source archive is user-supplied and never committed, so it is validated up front.
    step = next_step('Source archive')
    if args.source is None or not args.source.is_file():
        step.failed(1, f'not found: {args.source or "not set"}. Set S4_CLIENT_ZIP=/path/to/your Season-8 client ZIP.')
        return finish(1)
    step.ok(f'{format_size(args.source.stat().st_size)}')

    # 2 — a venv of its own: the pinned realesrgan/basicsr stack needs a Python the system one may
    # not be, and a venv left behind by an incompatible interpreter is moved aside, never deleted.
    step = next_step('Real-ESRGAN python environment')
    if venv_python.exists() and 'Python 3.14' in python_version(venv_python):
        archived = cache / f'venv-incompatible-{time.strftime("%Y%m%d%H%M%S")}'
        step.note(f'{relative(venv_python)} runs Python 3.14, which the pinned packages do not support')
        venv.rename(archived)
        (cache / '.installed').unlink(missing_ok=True)
        step.note(f'moved it to {relative(archived)}; the packages will be installed again')
    if venv_python.exists():
        step.ok(f'reused {relative(venv)} ({python_version(venv_python)})')
    elif step.stream([args.python, '-m', 'venv', venv]) == 0:
        step.ok(f'created {relative(venv)} ({python_version(venv_python)})')
    else:
        step.failed(1, f'could not create a venv with {relative(args.python)}')
        return finish(1)

    # 3 — torch first, then the pinned packages behind a marker file so an existing environment is
    # never reinstalled every run. The torch step is *not* behind the marker: a venv that bootstrapped
    # before the GPU was set up holds the CPU wheel, and pip would never replace it on its own.
    step = next_step('Real-ESRGAN packages')
    install = torch_install_plan(platform_name(), cuda_gpu_present(), args.torch_index_url,
                                 torch_cuda_available(venv_python))
    if install is None:
        step.note('torch: the default wheel is the right one here'
                  if platform_name() == 'darwin' or not cuda_gpu_present()
                  else 'torch: already built with CUDA')
    elif step.stream([venv_python, *install]) == 0:
        step.ok(f'torch installed from {args.torch_index_url or TORCH_CUDA_INDEX}')
    else:
        step.failed(1, 'installing the CUDA torch wheel failed; see its output above. An older driver '
                       'may need an earlier index, e.g. --torch-index-url '
                       'https://download.pytorch.org/whl/cu121')
        return finish(1)
    if (cache / '.installed').is_file():
        step.ok('already installed (realesrgan==0.3.0; remove .installed to reinstall)')
    elif step.stream([venv_python, '-m', 'pip', 'install', '--upgrade', 'pip', 'realesrgan==0.3.0']) == 0:
        (cache / '.installed').touch()
        step.ok('installed realesrgan==0.3.0')
    else:
        step.failed(1, f'pip failed; see its output above, then rerun `{resume}`')
        return finish(1)

    # 4 — pinned weights; the file is fetched once and only when it is missing.
    step = next_step('Model weights')
    if weights_x4.is_file():
        step.note(f'reusing {relative(weights_x4)} ({format_size(weights_x4.stat().st_size)})')
    elif (code := download(step, WEIGHTS_URL, weights_x4)) != 0:
        step.failed(1, f'download failed (exit {code}); check the network connection, then rerun `{resume}`')
        return finish(1)
    step.ok(f'{relative(weights_x4)}')

    # 5 — the conversion is skipped when the manifest already records every level it would emit; it
    # resumes per texture either way, so a rerun only encodes what is missing or stale.
    subject = f'Map conversion ({assets.name}, {REQUIRED_LEVELS})' if args.map else f'Wardrobe conversion ({REQUIRED_LEVELS})'
    step = next_step(subject)
    levels = index_levels()
    if manifest_path.is_file() and levels == REQUIRED_LEVELS:
        step.skipped(f'{relative(manifest_path)} already records {levels}')
    else:
        step.note(f'converting {assets.name}; `dotnet run` builds the converter first when it is out of date')
        step.note('this is the long phase: it reports texture/scene counts as it goes, and resumes where it stopped')
        code = step.stream(conversion_command(), cwd=ROOT)
        if code != 0:
            step.failed(code, f'fix the reported conversion error, then rerun `{resume}` to resume')
            return finish(1)
        step.ok(f'{manifest_name} records {index_levels() or "nothing"}')

    # 6 — the ESRGAN pass has its own structured log: a plan, per-texture lines, a periodic progress
    # line with rate and ETA, and a closing summary. Lightmaps and normal maps are indexed as such
    # and are not in its work list: generated pixels are never substituted for semantic data.
    step = next_step('Real-ESRGAN colour/alpha levels (4x)')
    descriptors = indexed_textures()
    step.note(f'{len(descriptors)} of {len(manifest_textures())} indexed textures are colour/alpha (the only kinds the pass may touch) · '
              f'4x file present on {level_files_present(descriptors, "4x")} of them '
              f'(an advisory check — the pass re-hashes every entry before generating)')
    code = step.stream([venv_python, esrgan_script, '--root', assets, '--manifest', manifest_name,
                        '--weights', weights_x4], cwd=ROOT)
    if code != 0:
        step.failed(code, f'an interrupted pass keeps its finished levels; rerun `{resume}` to resume')
        return finish(code if code > 0 else 1)
    step.ok(f'{relative(manifest_path)} updated')
    return finish(0)


def finish(code: int) -> int:
    total = format_duration(time.monotonic() - STARTED)
    failed = [step for step in STEPS if step.status.startswith('FAILED')]
    if code == 0:
        title_line = f'{len(STEPS)}/{PHASES} phases ok in {total}'
    elif code == 130:
        title_line = f'interrupted in phase {len(STEPS)}/{PHASES} after {total}'
    else:
        title_line = f'stopped in phase {len(STEPS)}/{PHASES} after {total}'
    summary(title_line, [(step.label, step.status, format_duration(step.duration)) for step in STEPS], total)
    if code == 0:
        emit(f'Bundle: {relative(assets)} ({manifest_name} holds the record)')
        emit(f'Next:   {next_step_hint}')
    elif failed:
        emit(f'Fix what failed above, then rerun `{resume}` to resume.')
    emit(f'Log:    {relative(log_path)}')
    close()
    return code


if __name__ == '__main__':
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        emit('')
        emit(f'Interrupted after {format_clock(time.monotonic() - STARTED)} — the phase below was stopped.')
        if CURRENT is not None and CURRENT.status == 'pending':
            CURRENT.interrupted('stopped by the user')
        emit(f'Finished levels stay on disk; rerun `{resume}` to resume where this stopped.')
        sys.exit(finish(130))
