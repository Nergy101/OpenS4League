"""Tests for `encode-texture-levels.py` — the AVIF/WebP pass that makes its own map entry.

They run on a two-texture fixture bundle (one colour, one lightmap) and are skipped when the codec
binary is absent, because the tool deliberately drives `avifenc`/`cwebp` rather than a Python codec.
"""
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / 'Tools/s4l-threejs-converter/scripts'
sys.path.insert(0, str(SCRIPTS))

requires_avif = unittest.skipUnless(shutil.which('avifenc'), 'avifenc (brew install libavif) is not installed')


def png(path, width, height, colour):
    """A tiny valid RGBA PNG without pulling in Pillow."""
    import struct

    def chunk(tag, data):
        body = tag + data
        return struct.pack('>I', len(data)) + body + struct.pack('>I', zlib.crc32(body))

    raw = b''.join(b'\x00' + bytes(colour) * width for _ in range(height))
    header = struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', header)
                     + chunk(b'IDAT', zlib.compress(raw)) + chunk(b'IEND', b''))


class EncodeTests(unittest.TestCase):
    def build(self, directory):
        maps = Path(directory) / 'Maps'
        bundle = maps / 'Test-Map'
        for name, colour in (('wall', (200, 40, 40, 255)), ('light', (10, 10, 10, 255)), ('bump', (128, 128, 255, 255))):
            png(bundle / f'textures/{name}.1x.png', 32, 32, colour)
            png(bundle / f'textures/{name}.4x.png', 128, 128, colour)
        (bundle / 'testmap.json').write_text(json.dumps({
            'format': 's4-map-threejs', 'version': 1, 'name': 'Test Map',
            'buffer': 'testmap.bin', 'scenes': [], 'texturePaths': ['wall.dds', 'light.dds', 'bump.dds'],
            'coverage': {'textureQuality': {'requested': ['1x', '4x']}},
            'textures': {
                'wall.dds': {'kind': 'color', 'variants': {
                    '1x': {'file': 'textures/wall.1x.png', 'width': 32, 'height': 32},
                    '4x': {'file': 'textures/wall.4x.png', 'width': 128, 'height': 128,
                           'algorithm': 'realesrgan-x4plus-rgb-alpha-lanczos', 'sourceSha256': 'a', 'generatedSha256': 'b'}}},
                'light.dds': {'kind': 'lightmap', 'variants': {
                    '1x': {'file': 'textures/light.1x.png', 'width': 32, 'height': 32},
                    '4x': {'file': 'textures/light.4x.png', 'width': 128, 'height': 128,
                           'algorithm': 'deterministic-bilinear', 'sourceSha256': 'c', 'generatedSha256': 'd'}}},
                'bump.dds': {'kind': 'normal', 'variants': {
                    '1x': {'file': 'textures/bump.1x.png', 'width': 32, 'height': 32},
                    '4x': {'file': 'textures/bump.4x.png', 'width': 128, 'height': 128,
                           'algorithm': 'deterministic-bilinear', 'sourceSha256': 'e', 'generatedSha256': 'f'}}},
            },
        }, indent=2) + '\n')
        (maps / 'index.json').write_text(json.dumps({
            'format': 's4-maps-index', 'version': 1,
            'maps': [{'id': 'test-map', 'name': 'Test Map', 'directory': 'Test-Map',
                      'manifest': 'testmap.json', 'config': 'map-config.json', 'bundleFormat': 's4-map-threejs'}],
        }, indent=2) + '\n')
        return maps, bundle

    @requires_avif
    def test_every_encodable_kind_becomes_its_own_entry_and_normals_stay_png(self):
        with tempfile.TemporaryDirectory() as directory:
            maps, bundle = self.build(directory)
            result = subprocess.run([sys.executable, str(SCRIPTS / 'encode-texture-levels.py'),
                                     str(bundle), '--format', 'avif', '--maps-dir', str(maps)],
                                    capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            clone = maps / 'Test-Map-4x-AVIF'
            manifest = json.loads((clone / 'testmap.json').read_text())
            colour = manifest['textures']['wall.dds']['variants']['4x']
            lightmap = manifest['textures']['light.dds']['variants']['4x']
            bump = manifest['textures']['bump.dds']['variants']['4x']
            self.assertTrue(colour['file'].endswith('.4x.avif'), colour)
            self.assertEqual(colour['codec'], 'avif')
            self.assertEqual(colour['quality'], 60)
            self.assertEqual(colour['algorithm'], 'realesrgan-x4plus-rgb-alpha-lanczos+avif-q60')
            self.assertEqual(colour['encodedFromSha256'], 'b', 'the encoded file must name the PNG it came from')
            self.assertTrue(colour['generatedSha256'])
            self.assertTrue((clone / colour['file']).is_file())
            self.assertFalse((clone / 'textures/wall.4x.png').exists(), 'the replaced PNG should not linger')
            # A lightmap is encoded too, but at its own quality: its values multiply the lighting.
            self.assertTrue(lightmap['file'].endswith('.4x.avif'), lightmap)
            self.assertEqual(lightmap['quality'], 90)
            self.assertEqual(lightmap['algorithm'], 'deterministic-bilinear+avif-q90')
            # A normal map holds a vector, so it is not encoded unless --kinds names it.
            self.assertEqual(bump['file'], 'textures/bump.4x.png')
            self.assertNotIn('codec', bump)
            self.assertTrue((clone / bump['file']).is_file())
            encoded = manifest['coverage']['textureQuality']['encoded']
            self.assertEqual(encoded['files'], 2)
            self.assertEqual(encoded['quality'], {'color': 60, 'lightmap': 90})
            registry = json.loads((maps / 'index.json').read_text())
            entries = [item for item in registry['maps'] if item['id'] == 'test-map-4x-avif']
            self.assertEqual(len(entries), 1, 'the encoded bundle must be its own map entry')
            self.assertEqual(entries[0]['name'], 'Test Map 4x AVIF')
            self.assertEqual(entries[0]['directory'], 'Test-Map-4x-AVIF')

    @requires_avif
    def test_kinds_can_narrow_the_pass_to_colour_only(self):
        with tempfile.TemporaryDirectory() as directory:
            maps, bundle = self.build(directory)
            result = subprocess.run([sys.executable, str(SCRIPTS / 'encode-texture-levels.py'),
                                     str(bundle), '--format', 'avif', '--kinds', 'color,alpha',
                                     '--maps-dir', str(maps)], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            manifest = json.loads((maps / 'Test-Map-4x-AVIF/testmap.json').read_text())
            self.assertTrue(manifest['textures']['wall.dds']['variants']['4x']['file'].endswith('.avif'))
            self.assertEqual(manifest['textures']['light.dds']['variants']['4x']['file'], 'textures/light.4x.png')
            self.assertIn('2 texture(s) not in --kinds', result.stdout)

    @requires_avif
    def test_an_unknown_kind_is_refused(self):
        with tempfile.TemporaryDirectory() as directory:
            maps, bundle = self.build(directory)
            result = subprocess.run([sys.executable, str(SCRIPTS / 'encode-texture-levels.py'),
                                     str(bundle), '--format', 'avif', '--kinds', 'specular',
                                     '--maps-dir', str(maps)], capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('Unknown kind(s): specular', result.stdout + result.stderr)

    @requires_avif
    def test_in_place_rewrites_the_bundle_itself(self):
        with tempfile.TemporaryDirectory() as directory:
            maps, bundle = self.build(directory)
            before = (bundle / 'testmap.json').read_text()
            result = subprocess.run([sys.executable, str(SCRIPTS / 'encode-texture-levels.py'),
                                     str(bundle), '--format', 'avif', '--in-place', '--manifest', 'testmap.json',
                                     '--maps-dir', str(maps)], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            manifest = json.loads((bundle / 'testmap.json').read_text())
            self.assertEqual(manifest['name'], 'Test Map', 'in-place must not rename the bundle')
            self.assertTrue(manifest['textures']['wall.dds']['variants']['4x']['file'].endswith('.avif'))
            self.assertFalse((bundle / 'textures/wall.4x.png').exists(), 'the PNG this level replaced goes')
            self.assertEqual(manifest['textures']['bump.dds']['variants']['4x']['file'], 'textures/bump.4x.png')
            self.assertIn('encodedInPlace', manifest['provenance'])
            registry = json.loads((maps / 'index.json').read_text())
            self.assertEqual([item['id'] for item in registry['maps']], ['test-map'],
                             'in-place must not add a registry entry')
            self.assertNotEqual((bundle / 'testmap.json').read_text(), before)
            self.assertTrue((bundle / 'textures/wall.1x.png').exists(), 'the decoded original is untouched')

    @requires_avif
    def test_re_running_over_a_converted_bundle_changes_nothing(self):
        """The whole run must be re-runnable: encoding an already-encoded file re-compresses it."""
        with tempfile.TemporaryDirectory() as directory:
            maps, bundle = self.build(directory)
            arguments = [sys.executable, str(SCRIPTS / 'encode-texture-levels.py'), str(bundle),
                         '--format', 'avif', '--in-place', '--manifest', 'testmap.json', '--maps-dir', str(maps)]
            first = subprocess.run(arguments, capture_output=True, text=True)
            self.assertEqual(first.returncode, 0, first.stdout + first.stderr)
            encoded = bundle / json.loads((bundle / 'testmap.json').read_text())['textures']['wall.dds']['variants']['4x']['file']
            after_first = encoded.read_bytes()
            second = subprocess.run(arguments, capture_output=True, text=True)
            self.assertEqual(second.returncode, 0, second.stdout + second.stderr)
            self.assertIn('nothing to do', second.stdout)
            self.assertNotIn('nothing to do', first.stdout, 'the first pass had nothing encoded yet')
            self.assertEqual(encoded.read_bytes(), after_first, 'the encoded file must not be touched again')
            self.assertTrue(encoded.is_file(), 'a rerun must never delete an encoded level')

    @requires_avif
    def test_normals_are_encoded_only_when_asked_and_at_their_own_quality(self):
        with tempfile.TemporaryDirectory() as directory:
            maps, bundle = self.build(directory)
            result = subprocess.run([sys.executable, str(SCRIPTS / 'encode-texture-levels.py'),
                                     str(bundle), '--format', 'avif', '--in-place', '--manifest', 'testmap.json',
                                     '--kinds', 'color,alpha,lightmap,normal', '--maps-dir', str(maps)],
                                    capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            manifest = json.loads((bundle / 'testmap.json').read_text())
            bump = manifest['textures']['bump.dds']['variants']['4x']
            self.assertTrue(bump['file'].endswith('.4x.avif'), bump)
            self.assertEqual(bump['quality'], 90, 'a normal map gets its own quality, not the colour one')
            self.assertEqual(bump['algorithm'], 'deterministic-bilinear+avif-q90')
            self.assertFalse((bundle / 'textures/bump.4x.png').exists())

    @requires_avif
    def test_a_dry_run_writes_nothing(self):
        with tempfile.TemporaryDirectory() as directory:
            maps, bundle = self.build(directory)
            result = subprocess.run([sys.executable, str(SCRIPTS / 'encode-texture-levels.py'),
                                     str(bundle), '--format', 'avif', '--dry-run', '--maps-dir', str(maps)],
                                    capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn('dry run', result.stdout)
            self.assertFalse((maps / 'Test-Map-4x-AVIF').exists())
            self.assertTrue((bundle / 'textures/wall.4x.png').exists())

    def test_an_unregistered_bundle_is_refused(self):
        with tempfile.TemporaryDirectory() as directory:
            maps = Path(directory) / 'Maps'
            bundle = maps / 'Test-Map'
            bundle.mkdir(parents=True)
            (bundle / 'testmap.json').write_text('{}')
            (maps / 'index.json').write_text('{"maps": []}')
            result = subprocess.run([sys.executable, str(SCRIPTS / 'encode-texture-levels.py'),
                                     str(bundle), '--format', 'avif'], capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('not a registered map bundle', result.stdout + result.stderr)


if __name__ == '__main__':
    unittest.main()
