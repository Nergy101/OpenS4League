import test from 'node:test';
import assert from 'node:assert/strict';
import { readdir, readFile, stat } from 'node:fs/promises';
import { DEFAULT_MAP_ID, MAP_INDEX_URL, mapAssetUrl, mapLabel, requestedMapId, resolveMapEntry } from '../src/MapRegistry.js';

const mapsRoot = new URL('../Models/Maps/', import.meta.url);
const slug = text => text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
const readIndex = async () => JSON.parse(await readFile(new URL('index.json', mapsRoot), 'utf8'));

test('every converted map is registered with an existing bundle', async () => {
  const index = await readIndex();
  assert.equal(index.format, 's4-maps-index');
  assert.equal(index.version, 1);
  assert.ok(index.maps.length, 'No maps converted; run: make threejs convert-assets');
  assert.deepEqual(index.maps.map(entry => entry.id), [...new Set(index.maps.map(entry => entry.id))], 'duplicate map ids');
  for (const entry of index.maps) {
    assert.equal(entry.id, slug(entry.id), `Map id is not a slug: ${entry.id}`);
    const directory = new URL(entry.directory + '/', mapsRoot);
    const manifest = JSON.parse(await readFile(new URL(entry.manifest, directory), 'utf8'));
    assert.equal(entry.id, slug(manifest.name), `Map id must be the slug of the manifest name: ${entry.id}`);
    assert.equal(entry.name, manifest.name, `Registry name must match the manifest for ${entry.id}`);
    assert.equal(manifest.format, entry.bundleFormat, `Bundle format mismatch for ${entry.id}`);
    for (const file of [entry.manifest, entry.config, manifest.buffer]) {
      assert.ok((await stat(new URL(file, directory))).isFile(), `Missing ${entry.directory}/${file}`);
    }
  }
});

test('every converted map directory is registered', async () => {
  const ids = new Set((await readIndex()).maps.map(entry => entry.directory.toLowerCase()));
  const converted = [];
  for (const entry of await readdir(mapsRoot, { withFileTypes: true })) {
    if (!entry.isDirectory()) continue;
    const hasConfig = await stat(new URL(`${entry.name}/map-config.json`, mapsRoot)).then(() => true, () => false);
    if (hasConfig) converted.push(entry.name.toLowerCase());
  }
  assert.deepEqual(converted.filter(name => !ids.has(name)), [], 'Map was converted but is not in the index');
});

test('an id resolves case-insensitively and no id means the default map Station-2', async () => {
  const index = await readIndex();
  const first = index.maps[0];
  const station = index.maps.find(map => map.id === DEFAULT_MAP_ID);
  assert.ok(station, `${DEFAULT_MAP_ID} must be converted: the viewer opens it when no map is requested`);
  assert.equal(resolveMapEntry(index, ''), station, 'The viewer must open Station-2 by default');
  assert.equal(resolveMapEntry(index, station.id).id, station.id);
  assert.equal(resolveMapEntry({ maps: [{ id: 'other-map' }] }, '').id, 'other-map',
    'Without Station-2 the first converted map is still used');
  assert.equal(resolveMapEntry(index, first.id).id, first.id);
  assert.equal(resolveMapEntry(index, first.id.toUpperCase()).id, first.id);
  assert.equal(resolveMapEntry(index, first.directory).id, first.id);
  assert.equal(mapAssetUrl(first, first.manifest), `./Models/Maps/${first.directory}/${first.manifest}`);
  assert.equal(mapLabel(first), first.name);
  assert.equal(mapLabel({ id: 'unnamed-map' }), 'unnamed-map', 'A map without a registered name falls back to its id');
  assert.equal(requestedMapId('?map=' + first.id), first.id);
  assert.equal(requestedMapId(''), '');
});

test('a map that is not converted reports the available ids', async () => {
  const index = await readIndex();
  assert.throws(() => resolveMapEntry(index, 'no-such-map'), /Unknown map 'no-such-map'\. Available: /);
  assert.throws(() => resolveMapEntry({ maps: [] }, ''), /No converted maps/);
  assert.throws(() => resolveMapEntry({ maps: [{ id: 'a' }] }, 'b'), /Available: a/);
});

test('only the registry knows where map files live', async () => {
  for (const name of await readdir(new URL('../src/', import.meta.url)))
    if (name.endsWith('.js') && name !== 'MapRegistry.js')
      assert.ok(!(await readFile(new URL(`../src/${name}`, import.meta.url), 'utf8')).includes('Models/Maps/'),
        `${name} hardcodes a map path; resolve it through ${MAP_INDEX_URL}`);
  for (const file of ['../index.html', '../src/viewer.js'])
    assert.ok(!(await readFile(new URL(file, import.meta.url), 'utf8')).includes('Station-2'),
      `${file} still names one specific map`);
});
