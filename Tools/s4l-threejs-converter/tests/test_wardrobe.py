"""Integration coverage against the real, locally supplied Season-8 XML."""
from pathlib import Path
import json
import os
import unittest
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[3]
BUNDLE = ROOT / 'Client/Models/Characters/Wardrobe'
# Point S4_CLIENT_ZIP at your own unpacked Season-8 client ZIP; it is never committed.
ARCHIVE: str = os.environ.get('S4_CLIENT_ZIP', '')
requires_archive = unittest.skipUnless(ARCHIVE, 'Set S4_CLIENT_ZIP to your unpacked Season-8 client ZIP')

@requires_archive
class WardrobeTests(unittest.TestCase):
    def test_complete_female_inventory_is_anchored_to_source_xml(self):
        with zipfile.ZipFile(ARCHIVE) as archive:
            root = ET.fromstring(archive.read('Game/xml/item.x7'))
        source = {e.get('item_key'): e for e in root.findall('item')
                  if e.get('item_key', '').isdigit() and len(e.get('item_key')) == 7
                  and int(e.get('item_key')) // 1000000 == 1
                  and (int(e.get('item_key')) // 10000) % 100 <= 6
                  and e.find('base') is not None and e.find('base').get('sex') in ('woman', 'unisex')}
        self.assertEqual(len(source), 646, 'Source XML population changed; review scope explicitly')
        self.assertTrue((BUNDLE / 'index.json').is_file(), 'Bulk --wardrobe converter has not produced the index')
        index = json.loads((BUNDLE / 'index.json').read_text())
        self.assertEqual((index['format'], index['version']), ('s4-wardrobe', 1))
        self.assertEqual({x['id'] for x in index['inventory']}, set(source))
        self.assertEqual(len(index['inventory']), len(source))
        converted = {x['id'] for x in index['inventory'] if x['status'] == 'converted'}
        unavailable = [x for x in index['inventory'] if x['status'] == 'unavailable']
        self.assertEqual(len(converted) + len(unavailable), 646)
        self.assertTrue(all(x.get('reason') for x in unavailable))
        body = index['catalog']['bodies'][0]
        self.assertEqual({x['id'] for x in body['items']}, converted)
        self.assertTrue(set(body['defaults'].values()) <= converted)
        self.assertEqual(body['animations'], [])
        self.assertIn(body['skeleton'], index['scenes'])
        self.assertEqual(index['coverage']['requestedItems'], 646)
        self.assertEqual(index['coverage']['convertedItems'], len(converted))
        self.assertEqual(index['coverage']['unavailableItems'], len(unavailable))
        for item in body['items']:
            self.assertTrue(item['parts'])
            for part in item['parts']:
                self.assertIn(part['scene'], index['scenes'])
            for variant in item['variants']:
                self.assertTrue(set(variant['maps'].values()) <= set(index['textures']))

    def test_malformed_references_and_variant_failures_are_explicit(self):
        import subprocess
        import tempfile
        index = json.loads((BUNDLE / 'index.json').read_text())
        basic = json.loads((ROOT / 'Client/Models/Characters/BasicFemale/character.json').read_text())
        defaults = set(index['catalog']['bodies'][0]['defaults'].values())
        with tempfile.TemporaryDirectory() as directory, zipfile.ZipFile(ARCHIVE) as source:
            root = ET.fromstring(source.read('Game/xml/item.x7'))
            for item in list(root):
                if item.get('item_key') not in defaults:
                    root.remove(item)
            for identifier, reference in [('1009001', 'DO_NOT_REPAIR .scn'), ('10090x3', '00_female_hair.scn'),
                                          ('1009004', None), ('1009005', 'ambiguous.scn')]:
                item = ET.SubElement(root, 'item', item_key=identifier)
                ET.SubElement(item, 'base', sex='woman')
                if reference is not None:
                    ET.SubElement(item, 'graphic', to_node_scene_file1=reference, to_node_parent_node1='Bip01 Head')
            fixture = Path(directory) / 'fixture.zip'
            original_texture = next(x for x in basic['textures'] if x.endswith('.dds') and '_n.' not in x)
            variant = str(Path(original_texture).with_suffix('')) + '_987.dds'
            with zipfile.ZipFile(fixture, 'w') as target:
                for key in basic['dependencies']:
                    if key != 'xml/item.x7':
                        target.writestr('Game/' + key, source.read('Game/' + key))
                target.writestr('Game/xml/item.x7', ET.tostring(root, encoding='utf-8'))
                hair = source.read('Game/resources/model/character/hair/00_female_hair.scn')
                target.writestr('Game/test-a/ambiguous.scn', hair)
                target.writestr('Game/test-b/ambiguous.scn', hair)
                target.writestr('Game/' + variant, b'corrupt DDS test fixture')
                color = 'resources/model/character/hair/00_female_hair'
                for suffix in ('_atex', '_etex', '_n'):
                    target.writestr('Game/' + color + suffix + '.dds', source.read('Game/' + color + '.dds'))
            result = subprocess.run(['dotnet', 'run', '-c', 'Release', '--project', str(ROOT / 'Tools/s4l-threejs-converter'), '--',
                                     str(fixture), str(Path(directory) / 'out'), '--wardrobe'], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            actual = json.loads((Path(directory) / 'out/index.json').read_text())
            records = {x['id']: x for x in actual['inventory']}
            self.assertIn('DO_NOT_REPAIR .scn', records['1009001']['reason'])
            self.assertIn('Malformed costume identifier: 10090x3', records['10090x3']['reason'])
            self.assertIn('No graphic', records['1009004']['reason'])
            self.assertIn('Ambiguous reference ambiguous.scn', records['1009005']['reason'])
            self.assertEqual(actual['coverage']['failedTextureCount'], 1)
            self.assertGreater(actual['coverage']['variantIssueCount'], 0)
            self.assertNotIn(variant, actual['textures'])
            # Failed source references are first-class coverage, not only free-text item errors.
            self.assertIn('DO_NOT_REPAIR .scn', actual['coverage'].get('missingSceneReferences', {}))
            hair_item = next(i for i in actual['catalog']['bodies'][0]['items'] if i['id'] == '1000002')
            choices = {p for v in hair_item['variants'] for p in v['maps'].values()}
            self.assertIn(color + '_atex.dds', choices)
            self.assertIn(color + '_etex.dds', choices)
            self.assertNotIn(color + '_n.dds', choices)
            self.assertTrue(any(issue.get('kind') == 'missing-team-texture'
                                and issue.get('texture') == 'resources/model/character/foot/48_female_foot_etex.dds'
                                for issue in actual['coverage']['variantIssues']))

    def test_unique_basename_relocations_have_explicit_provenance(self):
        index = json.loads((BUNDLE / 'index.json').read_text())
        resolutions = index.get('provenance', {}).get('sceneResolutions', [])
        match = next((r for r in resolutions if r['requestedReference'] == 'acc_virus.scn'), None)
        self.assertIsNotNone(match, 'Unique-basename scene relocations must be auditable')
        self.assertEqual(match['requestedPath'], 'resources/model/character/acc/acc_virus.scn')
        self.assertEqual(match['resolvedPath'], 'resources/model/character/pet/acc_virus.scn')
        self.assertEqual(match['method'], 'unique-basename')
        self.assertTrue(match['reason'])
        record = next(x for x in index['inventory'] if x['id'] == '1060000')
        self.assertEqual(record['status'], 'unavailable')
        self.assertIn('Ambiguous reference virus_helmet.dds', record['reason'])

    def test_lazy_buffers_and_texture_files_are_complete(self):
        import struct
        index = json.loads((BUNDLE / 'index.json').read_text())
        expected_files = set()
        for key, files in index['scenes'].items():
            expected_files.update(files.values())
            scene = json.loads((BUNDLE / files['json']).read_text())
            self.assertEqual(scene['source'], key)
            self.assertEqual(scene['sourceBytes'], scene['consumedBytes'])
            offset = 0
            for node in scene['nodes']:
                geometry = node['geometry']
                if geometry is None:
                    continue
                for name in ('positions', 'normals', 'uv', 'uv1', 'tangents', 'indices'):
                    field = geometry[name]
                    self.assertEqual(field['byteOffset'], offset)
                    offset += field['count'] * field['itemSize'] * 4
                for group in geometry['groups']:
                    for channel in ('map', 'lightMap'):
                        if group[channel]:
                            self.assertIn(group[channel], index['textures'])
            self.assertEqual((BUNDLE / files['bin']).stat().st_size, offset)
        for texture in index['textures'].values():
            variants = texture.get('variants', {'1x': texture})
            self.assertIn(texture.get('kind', 'color'), {'color', 'normal', 'lightmap', 'alpha'})
            self.assertIn('1x', variants)
            source_width = variants['1x']['width']; source_height = variants['1x']['height']
            for quality, variant in variants.items():
                data = (BUNDLE / variant['file']).read_bytes()
                self.assertEqual(data[:8], b'\x89PNG\r\n\x1a\n')
                self.assertEqual(struct.unpack('>II', data[16:24]), (variant['width'], variant['height']))
                scale = int(quality[:-1])
                self.assertEqual((variant['width'], variant['height']), (source_width * scale, source_height * scale))
                self.assertEqual(variant['sourceWidth'], source_width)
                self.assertEqual(variant['sourceHeight'], source_height)
                self.assertTrue(variant['sourceSha256'])
                self.assertTrue(variant['generatedSha256'])
        self.assertEqual(index['coverage']['textureQuality']['requested'], ['1x', '2x', '4x'])
        self.assertEqual(index['coverage']['textureQuality']['algorithm'], 'deterministic-bilinear')
        actual_files = {str(p.relative_to(BUNDLE)) for p in (BUNDLE / 'scenes').glob('*') if p.is_file()}
        self.assertEqual(actual_files, expected_files, 'Unindexed partial scene files must not survive failed conversions')

    def test_lazy_verifier_rejects_changed_weights_and_vertex_bytes(self):
        import copy
        import tempfile
        index = json.loads((BUNDLE / 'index.json').read_text())
        key = 'resources/model/character/body/34_female_body.scn'
        files = index['scenes'][key]
        original = json.loads((BUNDLE / files['json']).read_text())
        mesh = next(n for n in original['nodes'] if n['geometry'] and n['details']['bones'])
        warnings = [w for w in index['coverage']['skinWarnings'] if w['scene'] == key]
        self.assertTrue(warnings)
        self.assertEqual(warnings[0]['maxInfluences'], 5)
        for mutation in ('weight', 'vertex'):
            with tempfile.TemporaryDirectory() as directory:
                target = Path(directory)
                (target / 'scenes').mkdir()
                changed = copy.deepcopy(original)
                data = bytearray((BUNDLE / files['bin']).read_bytes())
                if mutation == 'weight':
                    node = next(n for n in changed['nodes'] if n['name'] == mesh['name'])
                    node['details']['bones'][0]['Weight'][0]['Weight'] += 0.125
                else:
                    data[0] ^= 1
                (target / files['json']).write_text(json.dumps(changed))
                (target / files['bin']).write_bytes(data)
                (target / 'index.json').write_text(json.dumps({'scenes': {key: files}, 'dependencies': {}}))
                result = self.verify_bundle(target)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn('skin weights and inverse binds' if mutation == 'weight' else 'positions', result.stderr)

    def verify_bundle(self, directory):
        import subprocess
        return subprocess.run(['dotnet', 'run', '-c', 'Release', '--project', str(ROOT / 'Tools/s4l-threejs-converter'), '--',
                               ARCHIVE, str(directory), '--wardrobe', '--verify'], capture_output=True, text=True)

    def test_every_scene_buffer_and_skin_bind_matches_source(self):
        result = self.verify_bundle(BUNDLE)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn('Source verification passed', result.stdout)

if __name__ == '__main__':
    unittest.main()
