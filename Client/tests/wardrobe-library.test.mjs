import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import * as THREE from 'three';

const loader = new URL('../src/WardrobeLibrary.js', import.meta.url);
const base = new URL('../Models/Characters/BasicFemale/', import.meta.url);

async function fixture() {
  const bundle = JSON.parse(await readFile(new URL('character.json', base)));
  const bytes = await readFile(new URL('character.bin', base));
  const buffer = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
  const catalog = structuredClone(bundle.catalog);
  const extra = structuredClone(catalog.bodies[0].items.find(item => item.slot === 'hair'));
  extra.id = 'test-extra-hair'; extra.parts[0].scene = 'test/extra-hair.scn';
  catalog.bodies[0].items.push(extra);
  const scenes = new Map(bundle.scenes.map(scene => [scene.source, scene]));
  scenes.set(extra.parts[0].scene, { ...structuredClone(bundle.scenes.find(scene => scene.source.includes('/hair/'))), source: extra.parts[0].scene });
  const index = { format: 's4-wardrobe', version: 1, catalog, textures: bundle.textures,
    scenes: Object.fromEntries([...scenes].map(([key]) => [key, { json: key + '.json', bin: key + '.bin' }])), inventory: [] };
  const requests = [];
  const adapters = {
    json: async url => { requests.push(url.pathname); const path = url.pathname.slice(1).replace(/\.json$/, ''); return structuredClone(scenes.get(path)); },
    binary: async url => { requests.push(url.pathname); return buffer; },
    texture: async url => { requests.push(url.pathname); return new THREE.Texture(); },
  };
  return { index, adapters, requests, extra };
}

test('wardrobe loads only equipped assets and releases unused scene and texture caches', async () => {
  assert.ok(await readFile(loader).then(() => true, () => false), 'Lazy wardrobe loading is not implemented');
  const { WardrobeLibrary } = await import(loader);
  const { index, adapters, requests, extra } = await fixture();
  const library = new WardrobeLibrary(index, new URL('http://wardrobe.test/index.json'), adapters);
  const model = await library.createModel();
  assert.equal(model.equipment.size, 6);
  assert.equal(requests.some(path => path.includes('extra-hair')), false);
  assert.ok(library.textures.size < Object.keys(index.textures).length, 'Startup eagerly loaded every skin');
  await library.equip('hair', extra.id);
  assert.equal(model.equipment.get('hair').item.id, extra.id);
  assert.equal(requests.some(path => path.includes('extra-hair')), true);
  assert.equal(model.scenes.has('resources/model/character/hair/00_female_hair.scn'), false, 'Old scene was retained unnecessarily');
  await library.equip('hair', extra.id, '1');
  assert.equal(library.textures.has('resources/model/character/hair/00_female_hair.dds'), false);
  assert.equal(library.textures.has('resources/model/character/hair/00_female_hair_1.dds'), true);
  assert.ok(library.sceneBuffers.size <= 7);
  library.dispose();
  assert.equal(library.textures.size, 0);
});

test('saved outfits load lazily and apply as a complete selection', async () => {
  const { WardrobeLibrary } = await import(loader);
  const { index, adapters, extra } = await fixture();
  const library = new WardrobeLibrary(index, new URL('http://wardrobe.test/index.json'), adapters);
  const model = await library.createModel();
  const outfit = { id: 'saved-test', name: 'Test outfit', bodyId: model.bodyId,
    equipment: Object.fromEntries([...model.equipment].map(([slot, s]) => [slot, { itemId: s.item.id, variantId: s.variant.id }])) };
  outfit.equipment.hair = { itemId: extra.id, variantId: '2' };
  const root = model.root;
  await library.applyOutfit(outfit);
  assert.equal(model.root, root, 'Applying an outfit should not replace the viewer root');
  assert.equal(model.equipment.get('hair').item.id, extra.id);
  assert.equal(model.equipment.get('hair').variant.id, '2');
  const invalid = structuredClone(outfit); invalid.equipment.shoes.itemId = 'missing';
  await assert.rejects(library.applyOutfit(invalid), /not available/);
  assert.equal(model.equipment.get('hair').item.id, extra.id);
  library.dispose();
});

test('a slow old selection cannot overwrite a newer skin choice', async () => {
  const { WardrobeLibrary } = await import(loader);
  const { index, adapters } = await fixture();
  const original = adapters.texture;
  let release, started;
  const waiting = new Promise(resolve => { started = resolve; });
  adapters.texture = async url => {
    if (url.pathname.endsWith('00_female_hair_1.dds.png')) {
      started(); await new Promise(resolve => { release = resolve; });
    }
    return original(url);
  };
  const library = new WardrobeLibrary(index, new URL('http://wardrobe.test/index.json'), adapters);
  const model = await library.createModel();
  const slow = library.equip('hair', '1000002', '1');
  await waiting;
  assert.equal(await library.equip('hair', '1000002', '2'), true);
  release();
  assert.equal(await slow, false);
  assert.equal(model.equipment.get('hair').variant.id, '2');
  assert.equal(library.textures.has('resources/model/character/hair/00_female_hair_1.dds'), false);
  library.dispose();
});
