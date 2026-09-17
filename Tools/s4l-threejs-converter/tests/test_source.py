"""Verify converted geometry directly against the user-supplied source ZIP."""
from pathlib import Path
import os
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[3]
# Point S4_CLIENT_ZIP at your own unpacked Season-8 client ZIP; it is never committed.
ARCHIVE: str = os.environ.get('S4_CLIENT_ZIP', '')
requires_archive = unittest.skipUnless(ARCHIVE, 'Set S4_CLIENT_ZIP to your unpacked Season-8 client ZIP')

@requires_archive
class SourceTests(unittest.TestCase):
    def test_float_and_index_data_matches_original_scenes(self):
        result = subprocess.run(['dotnet', 'run', '-c', 'Release', '--project', str(ROOT / 'Tools/s4l-threejs-converter'), '--',
                                 ARCHIVE, str(ROOT / 'Client/Models/Maps/Station-2'), '--verify'], text=True, capture_output=True)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn('Source verification passed', result.stdout)

    def test_verifier_rejects_changed_vertex_data(self):
        import tempfile
        import shutil
        bundle = ROOT / 'Client/Models/Maps/Station-2'
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory)
            shutil.copyfile(bundle / 'station2.json', target / 'station2.json')
            buffer = bytearray((bundle / 'station2.bin').read_bytes())
            buffer[0] ^= 1
            (target / 'station2.bin').write_bytes(buffer)
            result = subprocess.run(['dotnet', 'run', '-c', 'Release', '--no-build', '--project', str(ROOT / 'Tools/s4l-threejs-converter'), '--',
                                     ARCHIVE, directory, '--verify'], text=True, capture_output=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('Source verification failed:', result.stderr)
            self.assertIn('positions', result.stderr)

if __name__ == '__main__':
    unittest.main()
