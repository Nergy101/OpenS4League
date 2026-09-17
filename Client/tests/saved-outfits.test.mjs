import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const moduleUrl = new URL('../src/SavedOutfits.js', import.meta.url);
test('named outfits persist only IDs, update by name, and can be removed', async () => {
  assert.ok(await readFile(moduleUrl).then(() => true, () => false), 'Saved outfits are not implemented');
  const { saveOutfit, loadOutfits, deleteOutfit, STORAGE_KEY } = await import(moduleUrl);
  const data = new Map();
  const storage = { getItem: key => data.get(key) ?? null, setItem: (key, value) => data.set(key, value) };
  const character = { bodyId: 'female', equipment: new Map([
    ['hair', { item: { id: '1000002', heavyGeometry: 'must not persist' }, variant: { id: '1', maps: { huge: 'not persisted' } } }],
  ]) };
  const saved = saveOutfit(storage, 'Blue outfit', character);
  assert.equal(loadOutfits(storage).length, 1);
  assert.deepEqual(loadOutfits(storage)[0].equipment, { hair: { itemId: '1000002', variantId: '1' } });
  assert.equal(data.get(STORAGE_KEY).includes('heavyGeometry'), false);
  character.equipment.get('hair').variant.id = '2';
  saveOutfit(storage, 'Blue outfit', character);
  assert.equal(loadOutfits(storage).length, 1);
  assert.equal(loadOutfits(storage)[0].equipment.hair.variantId, '2');
  deleteOutfit(storage, saved.id);
  assert.deepEqual(loadOutfits(storage), []);
});

test('invalid existing local data is reported without overwriting it', async () => {
  const { saveOutfit } = await import(moduleUrl);
  let written = false;
  const storage = { getItem: () => 'null', setItem: () => { written = true; } };
  assert.throws(() => saveOutfit(storage, 'Test', { bodyId: 'female', equipment: new Map() }), /Unsupported saved-outfit data/);
  assert.equal(written, false);
});

test('the last applied outfit ID is stored separately from outfit payloads', async () => {
  const { loadLastOutfitId, saveLastOutfitId, LAST_OUTFIT_KEY } = await import(moduleUrl);
  const data = new Map();
  const storage = { getItem: key => data.get(key) ?? null, setItem: (key, value) => data.set(key, value), removeItem: key => data.delete(key) };
  assert.equal(loadLastOutfitId(storage), '');
  saveLastOutfitId(storage, 'outfit-1');
  assert.equal(data.get(LAST_OUTFIT_KEY), 'outfit-1');
  assert.equal(loadLastOutfitId(storage), 'outfit-1');
  saveLastOutfitId(storage, '');
  assert.equal(loadLastOutfitId(storage), '');
});
