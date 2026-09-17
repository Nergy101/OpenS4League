"""Integration checks against a locally converted, user-supplied Station-2."""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[3]
BUNDLE = ROOT / 'Client/Models/Maps/Station-2'

class BundleTests(unittest.TestCase):
    def test_map_contains_every_configured_scene(self):
        self.assertTrue((BUNDLE / 'station2.json').is_file(), 'Station-2 has not been converted')
        data = json.loads((BUNDLE / 'station2.json').read_text())
        self.assertEqual({s['name'] for s in data['scenes'] if s['role'] != 'addon'}, {
            'sky_bluesky.scn', 'ds5_station.scn', 'ds7_fullscenerendertarget.scn',
            'st02_spawn_death.scn', 'ds5_station_octadd.scn', 'ds6_station02_occlusion.scn',
            'ds6_station2_camera.scn', 'ds7_safeline.scn', 'ds5_goal_arrow.scn'})

    def test_team_texture_variants_are_included(self):
        data = json.loads((BUNDLE / 'station2.json').read_text())
        for path in data['textures']:
            if '_atex' in path:
                self.assertTrue(path.replace('_atex', '_etex') in data['textures'], path)

    def test_goal_arrow_parent_animations_are_preserved(self):
        data = json.loads((BUNDLE / 'station2.json').read_text())
        scene = next(s for s in data['scenes'] if s['name'] == 'ds5_goal_arrow.scn')
        bones = [n for n in scene['nodes'] if n['type'] == 'Bone']
        self.assertTrue(bones)
        self.assertTrue(all(n['details'] and n['details'].get('animations') for n in bones),
                        'Arrow parent animation data was dropped')

    def test_every_referenced_texture_is_a_valid_png(self):
        import struct
        import zlib
        data = json.loads((BUNDLE / 'station2.json').read_text())
        self.assertTrue('textures' in data, 'Browser-readable texture conversion is missing')
        self.assertEqual(set(data['textures']), set(data['texturePaths']))
        for key, texture in data['textures'].items():
            raw = (BUNDLE / texture['file']).read_bytes()
            self.assertEqual(raw[:8], b'\x89PNG\r\n\x1a\n', key)
            cursor = 8
            compressed = bytearray()
            while cursor < len(raw):
                size = struct.unpack_from('>I', raw, cursor)[0]
                chunk = raw[cursor + 4:cursor + 8 + size]
                crc = struct.unpack_from('>I', raw, cursor + 8 + size)[0]
                self.assertEqual(zlib.crc32(chunk), crc, key)
                if chunk[:4] == b'IHDR':
                    self.assertEqual(struct.unpack_from('>II', chunk, 4), (texture['width'], texture['height']))
                if chunk[:4] == b'IDAT': compressed.extend(chunk[4:])
                cursor += size + 12
            self.assertEqual(len(zlib.decompress(compressed)), texture['height'] * (1 + texture['width'] * 4))

if __name__ == '__main__':
    unittest.main()
