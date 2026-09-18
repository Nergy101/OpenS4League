"""Exercise both legacy entry points after extracting the shared scene exporter."""
from pathlib import Path
import json
import os
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]
# Point S4_CLIENT_ZIP at your own unpacked Season-8 client ZIP; it is never committed.
ARCHIVE: str = os.environ.get('S4_CLIENT_ZIP', '')
requires_archive = unittest.skipUnless(ARCHIVE, 'Set S4_CLIENT_ZIP to your unpacked Season-8 client ZIP')

@requires_archive
class LegacyConversionTests(unittest.TestCase):
    def test_legacy_modes_reexport_identical_geometry_buffers(self):
        for stem, original, arguments in (
            ('character', 'Characters/BasicFemale', ['--character', str(ROOT / 'Tools/s4l-threejs-converter/characters/female-basic.json')]),
            ('station2', 'Maps/Station-2', ['--map', str(ROOT / 'Tools/s4l-threejs-converter/maps/station-2.json')]),
        ):
            with self.subTest(mode=stem), tempfile.TemporaryDirectory() as directory:
                command = ['dotnet', 'run', '-c', 'Release', '--project', str(ROOT / 'Tools/s4l-threejs-converter'), '--', ARCHIVE, directory, *arguments]
                result = subprocess.run(command, capture_output=True, text=True)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                generated = Path(directory)
                baseline = ROOT / 'Client/Models' / original
                self.assertEqual((generated / (stem + '.bin')).read_bytes(), (baseline / (stem + '.bin')).read_bytes())
                actual = json.loads((generated / (stem + '.json')).read_text())
                expected = json.loads((baseline / (stem + '.json')).read_text())
                self.assertEqual(actual['totals'], expected['totals'])
                self.assertEqual(actual.get('catalog'), expected.get('catalog'))
                verified = subprocess.run(command + ['--verify'], capture_output=True, text=True)
                self.assertEqual(verified.returncode, 0, verified.stdout + verified.stderr)
                self.assertIn('Source verification passed', verified.stdout)

if __name__ == '__main__':
    unittest.main()
