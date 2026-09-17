"""Checks for the locally converted basic female outfit."""
from pathlib import Path
import json
import os
import unittest

ROOT = Path(__file__).resolve().parents[3]
BUNDLE = ROOT / 'Client/Models/Characters/BasicFemale'
# Point S4_CLIENT_ZIP at your own unpacked Season-8 client ZIP; it is never committed.
ARCHIVE: str = os.environ.get('S4_CLIENT_ZIP', '')
requires_archive = unittest.skipUnless(ARCHIVE, 'Set S4_CLIENT_ZIP to your unpacked Season-8 client ZIP')

class CharacterBundleTests(unittest.TestCase):
    def test_original_default_outfit_is_modular(self):
        self.assertTrue((BUNDLE / 'character.json').is_file(), 'Female character has not been converted')
        bundle = json.loads((BUNDLE / 'character.json').read_text())
        self.assertEqual(bundle['format'], 's4-character-threejs')
        body = bundle['catalog']['bodies'][0]
        self.assertEqual(body['id'], 'female')
        self.assertEqual(body['defaults'], {'hair':'1000002', 'face':'1010001', 'shirt':'1020001',
                                          'pants':'1030001', 'gloves':'1040001', 'shoes':'1050001'})
        self.assertEqual(len(body['items']), 6)
        self.assertEqual(bundle['unresolved'], [])
        by_id = {item['id']: item for item in body['items']}
        self.assertEqual(by_id['1000002']['parts'][0]['attachmentBone'], 'Bip01 Head')
        self.assertIn('gloves_part1', by_id['1020001']['hides'])
        self.assertTrue(any(len(item['variants']) > 1 for item in body['items']))

    def verify_bundle(self, directory):
        import subprocess
        return subprocess.run(['dotnet', 'run', '-c', 'Release', '--project', str(ROOT / 'Tools/s4l-threejs-converter'), '--',
                               ARCHIVE, str(directory), '--character', str(ROOT / 'Tools/s4l-threejs-converter/characters/female-basic.json'), '--verify'],
                              capture_output=True, text=True)

    @requires_archive
    def test_character_source_verification(self):
        result = self.verify_bundle(BUNDLE)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    @requires_archive
    def test_changed_skin_bind_matrix_is_rejected(self):
        import shutil
        import tempfile
        bundle = json.loads((BUNDLE / 'character.json').read_text())
        mesh = next(n for s in bundle['scenes'] if s['role'] == 'equipment' for n in s['nodes']
                    if n['geometry'] and n['details']['bones'])
        mesh['details']['bones'][0]['Matrix']['M41'] += 1
        bundle['dependencies'] = {}
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory)
            (target / 'character.json').write_text(json.dumps(bundle))
            shutil.copyfile(BUNDLE / 'character.bin', target / 'character.bin')
            result = self.verify_bundle(target)
            self.assertNotEqual(result.returncode, 0, 'Altered source inverse binds were accepted')
            self.assertIn('skin weights and inverse binds', result.stderr)

if __name__ == '__main__':
    unittest.main()
