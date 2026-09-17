"""Real local Season-8 animation conversion integration tests."""
from pathlib import Path
import json
import os
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]
PROJECT = ROOT / 'Tools/s4l-threejs-converter'
PACK = ROOT / 'Client/Models/Characters/Animations/Female'
# Point S4_CLIENT_ZIP at your own unpacked Season-8 client ZIP; it is never committed.
ARCHIVE: str = os.environ.get('S4_CLIENT_ZIP', '')
requires_archive = unittest.skipUnless(ARCHIVE, 'Set S4_CLIENT_ZIP to your unpacked Season-8 client ZIP')

@requires_archive
class AnimationTests(unittest.TestCase):
    def test_standing_idle_is_original_female_preview_default(self):
        with tempfile.TemporaryDirectory() as directory:
            result = subprocess.run(['dotnet', 'run', '-c', 'Release', '--project', str(PROJECT), '--',
                                     ARCHIVE, directory, '--animations'], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            index = json.loads((Path(directory) / 'index.json').read_text())
            self.assertEqual(index['format'], 's4-character-animations')
            idle = next(c for c in index['clips'] if c['id'] == 'idle')
            self.assertEqual(idle['sourceClip'], '00029')
            clip = json.loads((Path(directory) / idle['url']).read_text())
            self.assertEqual(len(clip['tracks']), 82 * 3)
            self.assertGreater(clip['duration'], 0)

    def test_emotes_and_unarmed_movement_are_selected_from_source_mappings(self):
        import zipfile
        import xml.etree.ElementTree as ET
        with tempfile.TemporaryDirectory() as directory:
            result = subprocess.run(['dotnet', 'run', '-c', 'Release', '--project', str(PROJECT), '--',
                                     ARCHIVE, directory, '--animations'], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            index = json.loads((Path(directory) / 'index.json').read_text())
            with zipfile.ZipFile(ARCHIVE) as z:
                source = ET.fromstring(z.read('Game/language/_eu_default_option.x7'))
            expected = {e.get('animation') for e in source.iter('data') if e.get('animation') and e.get('useweapon') == 'false'}
            actual = {c['sourceClip'] for c in index['clips'] if c['category'] == 'Emotes'}
            missing = {c['sourceClip'] for c in index['unavailable']}
            self.assertEqual(actual | missing, expected)
            self.assertEqual(actual & missing, set())
            self.assertTrue({'O0003','O0007','O0015','O0016','O0017','O0018'} <= actual)
            walking = next(c for c in index['clips'] if c['id'] == 'walk')
            self.assertEqual(walking['sourceClip'], '00008')
            self.assertEqual(walking['mapping'].get('lowerCall', {}).get('arguments', [None])[0], '00008',
                             'Both body halves must come from the original WeaponUnused state')
            self.assertIn('RunState_WeaponUnused', json.dumps(index['provenance']))
            self.assertEqual(index['verification']['unresolvedSelectedCopies'], 0)
            self.assertGreater(index['verification'].get('libraryCopyRecords', 0), 0, 'Audit actual library aliases, not only alias-free selected clips')
            self.assertEqual(missing, set(), 'O0034 exists in the separate variant library')
            self.assertEqual(next(c for c in index['clips'] if c['sourceClip'] == 'O0034')['sourceScene'],
                             'resources/model/character/bip_female/female_bip_0000.scn')

    def test_copy_alias_replaces_channels_instead_of_using_base(self):
        with tempfile.TemporaryDirectory() as directory:
            work = Path(directory)
            (work / 'Test.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
              <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
              </PropertyGroup><ItemGroup><ProjectReference Include="{ROOT}/Tools/s4l-resource-tool/src/S4League.Scn/S4League.Scn.csproj" />
              <Compile Include="{PROJECT}/AnimationConverter.cs" Link="AnimationConverter.cs" />
              <Compile Include="{PROJECT}/ConversionAssets.cs" Link="ConversionAssets.cs" />
              </ItemGroup></Project>''')
            (work / 'Program.cs').write_text('''using S4League.Scn; using System.Numerics;
              var s = new SceneContainer(); var b = new BoneChunk(s) { Name = "Bip01 L Hand" }; s.Add(b);
              b.Animation.Add(new() { Name="BASE", TransformKeyData = new() { TransformKey = new() { Scale=Vector3.One } } });
              b.Animation.Add(new() { Name="actual", TransformKeyData = new() { Duration=TimeSpan.FromSeconds(2),
                TransformKey = new() { Translation=new Vector3(7,8,9), Scale=Vector3.One,
                  RKey=new List<RKey> { new() { Duration=TimeSpan.FromMilliseconds(500), Rotation=new Quaternion(0,0,0.6f,0.8f) } } } } });
              b.Animation.Add(new() { Name="alias", Copy="actual" });
              var actual = AnimationConverter.Export(s,"actual","test"); var alias = AnimationConverter.Export(s,"alias","test");
              if (System.Text.Json.JsonSerializer.Serialize(actual.tracks) != System.Text.Json.JsonSerializer.Serialize(alias.tracks) || actual.duration != alias.duration)
                throw new Exception("Copy alias lost original channels");
              Console.WriteLine("copy alias verified");''')
            result = subprocess.run(['dotnet', 'run', '--project', str(work)], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

class AnimationSourceChecks(unittest.TestCase):
    def check(self, mode):
        with tempfile.TemporaryDirectory() as directory:
            work = Path(directory)
            (work / 'Test.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
              <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
              </PropertyGroup><ItemGroup><ProjectReference Include="{ROOT}/Tools/s4l-resource-tool/src/S4League.Scn/S4League.Scn.csproj" />
              <Compile Include="{PROJECT}/AnimationConverter.cs" Link="AnimationConverter.cs" /></ItemGroup></Project>''')
            (work / 'Program.cs').write_text((PROJECT / 'tests/AnimationChecks.cs.txt').read_text())
            result = subprocess.run(['dotnet', 'run', '--project', str(work), '--', mode, ARCHIVE, str(PACK)], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            print(result.stdout.strip())

    @requires_archive
    def test_channels_match_every_decoded_source_value(self):
        self.check('source')

    def test_invalid_channels_and_missing_sources_fail(self):
        self.check('validation')

    @requires_archive
    def test_cross_library_copy_resolution(self):
        self.check('libraries')

@requires_archive
class AnimationVerificationTests(unittest.TestCase):
    def test_verifier_rejects_changed_source_values_times_and_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            command = ['dotnet', 'run', '-c', 'Release', '--project', str(PROJECT), '--', ARCHIVE, directory, '--animations']
            result = subprocess.run(command, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            original = (Path(directory) / 'clips/walk.json').read_text()
            changed = json.loads(original)
            changed['tracks'][0]['values'][0] += 100
            (Path(directory) / 'clips/walk.json').write_text(json.dumps(changed))
            result = subprocess.run(command + ['--verify'], capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0, 'Verifier silently overwrote or accepted corrupted source values')
            self.assertIn('Animation source mismatch', result.stderr)
            self.assertEqual(json.loads((Path(directory) / 'clips/walk.json').read_text()), changed,
                             'Verification must be read-only')
            (Path(directory) / 'clips/walk.json').write_text(original)
            for relative, mutate in [
                ('clips/walk.json', lambda x: next(t for t in x['tracks'] if len(t['times']) > 1)['times'].__setitem__(1, 0.123)),
                ('clips/cry.json', lambda x: x['tracks'][1]['values'].__setitem__(0, 0.25)),
                ('index.json', lambda x: x['clips'][0].__setitem__('sourceClip', 'idle')),
            ]:
                with self.subTest(target=relative):
                    file = Path(directory) / relative
                    saved = file.read_text()
                    changed = json.loads(saved)
                    mutate(changed)
                    file.write_text(json.dumps(changed))
                    result = subprocess.run(command + ['--verify'], capture_output=True, text=True)
                    self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
                    self.assertIn('Animation source mismatch', result.stderr)
                    file.write_text(saved)
            source_file = Path(directory) / 'source/resources/script/previewactoranimsetting.lua'
            original_source = source_file.read_bytes()
            source_file.write_bytes(original_source + b'corrupted')
            result = subprocess.run(command + ['--verify'], capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('Animation source dependency mismatch', result.stderr)
            source_file.write_bytes(original_source)
            result = subprocess.run(command + ['--verify'], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

if __name__ == '__main__':
    unittest.main()
