"""Print the texture levels a converted bundle was converted with (one comma-separated line).

Used by `make threejs-asset-upscale` / `make threejs-map-upscale` to decide whether the converter
still has to run (a missing manifest, or an index that does not record a level this build emits),
and read back afterwards to report what the bundle actually records.

Usage: bundle-qualities.py <bundle-dir> [--manifest index.json]
"""
import argparse
import json
import sys
from pathlib import Path

parser = argparse.ArgumentParser(description='Print the texture levels a converted bundle records.')
parser.add_argument('root', type=Path, help='bundle directory (the wardrobe, or one converted map)')
parser.add_argument('--manifest', default='index.json',
                    help='bundle manifest inside the bundle (default: index.json; a map uses its <stem>.json)')
args = parser.parse_args()


def main() -> int:
    manifest = args.root / args.manifest
    if not manifest.is_file():
        return 1
    coverage = json.loads(manifest.read_text()).get('coverage', {})
    requested = coverage.get('textureQuality', {}).get('requested')
    if not requested:
        return 1
    print(','.join(requested))
    return 0


if __name__ == '__main__':
    sys.exit(main())
