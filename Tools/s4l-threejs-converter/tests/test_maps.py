"""Recipe / bundle / viewer-index contract for the maps under Client/Models/Maps.

Runs offline: a recipe whose map has not been converted locally is skipped, exactly
like the browser viewer skips it.
"""
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[3]
RECIPES = ROOT / 'Tools/s4l-threejs-converter/maps'
MAPS = ROOT / 'Client/Models/Maps'


def slug(text: str) -> str:
    return ''.join(c if c.isalnum() or c == '-' else '-' for c in text.lower()).strip('-')


def recipe_files():
    return sorted(RECIPES.glob('*.json'))


class MapRecipeTests(unittest.TestCase):
    def test_every_recipe_describes_a_convertable_map(self):
        self.assertTrue(recipe_files(), 'No map recipes found')
        for path in recipe_files():
            recipe = json.loads(path.read_text())
            self.assertEqual(path.stem, slug(recipe['name']), f'{path.name}: the id is the slug of the name')
            self.assertTrue(recipe['config'].startswith('resources/mapinfo/'), path.name)
            self.assertTrue(recipe['bundle'], path.name)

    def test_converted_maps_match_their_recipe_and_the_viewer_index(self):
        index = json.loads((MAPS / 'index.json').read_text())
        self.assertEqual(index['format'], 's4-maps-index')
        for path in recipe_files():
            recipe = json.loads(path.read_text())
            bundle = MAPS / recipe['name'] / (recipe['bundle'] + '.json')
            if not bundle.is_file():
                continue
            manifest = json.loads(bundle.read_text())
            self.assertEqual(manifest['name'], recipe['name'])
            self.assertEqual(manifest['format'], 's4-map-threejs')
            self.assertEqual(manifest['sourceConfig'].lower(), recipe['config'])
            self.assertGreater(manifest['totals']['scenes'], 0, f"{recipe['name']}: no scene converted")
            self.assertEqual(manifest['totals']['scenes'], len(manifest['scenes']))
            entries = [m for m in index['maps'] if m['directory'] == recipe['name']]
            self.assertEqual(len(entries), 1, f"{recipe['name']} is not registered exactly once")
            entry = entries[0]
            self.assertEqual(entry['id'], path.stem)
            self.assertEqual(entry['name'], recipe['name'])
            self.assertEqual(entry['manifest'], recipe['bundle'] + '.json')
            self.assertEqual(entry['config'], 'map-config.json')
            self.assertEqual(entry['bundleFormat'], manifest['format'])


if __name__ == '__main__':
    unittest.main()
