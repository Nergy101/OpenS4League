// Exhaustive CPU-side assembly and texture-file checks; real WebGL is checked separately.
import { readFile, mkdir, writeFile, appendFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { WardrobeLibrary } from '../src/WardrobeLibrary.js';

const base = new URL('../Models/Characters/Wardrobe/index.json', import.meta.url);
const index = JSON.parse(await readFile(base));
const output = new URL('./verification/', base);
await mkdir(output, { recursive: true });
const journal = new URL('runtime-items.jsonl', output);
await writeFile(journal, '');
const library = new WardrobeLibrary(index, base, {
  json: async url => JSON.parse(await readFile(url)),
  binary: async url => { const b = await readFile(url); return b.buffer.slice(b.byteOffset, b.byteOffset + b.byteLength); },
  texture: async url => {
    const data = await readFile(url);
    assert.deepEqual([...data.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10], `Invalid PNG: ${url.pathname}`);
    return new THREE.Texture(); // No GPU/color claims: geometry-only test adapter.
  },
});
const model = await library.createModel();
const items = model.body.items;
let batch = [];
for (const item of items) {
  const variants = [];
  for (const variant of item.variants) {
    try {
      await library.equip(item.slot, item.id, variant.id);
      const selection = model.equipment.get(item.slot);
      assert.equal(selection.item.id, item.id);
      assert.equal(selection.variant.id, variant.id);
      let meshes = 0;
      for (const part of selection.parts) part.group.traverse(node => {
        assert.ok(node.matrixWorld.elements.every(Number.isFinite), `Nonfinite transform: ${node.name}`);
        if (!node.isMesh) return;
        meshes++;
        const position = node.geometry.attributes.position;
        assert.ok(position.array.every(Number.isFinite), `Nonfinite vertices: ${node.name}`);
        if (node.isSkinnedMesh) {
          node.skeleton.update();
          for (let v = 0; v < position.count; v++) {
            const p = node.applyBoneTransform(v, new THREE.Vector3().fromBufferAttribute(position, v)).applyMatrix4(node.matrixWorld);
            assert.ok(p.toArray().every(Number.isFinite), `Nonfinite skinning: ${node.name}/${v}`);
          }
        }
        for (const material of node.material) if (material.map) assert.ok(library.textures.has(material.map.name), 'Material points at an unloaded texture');
      });
      assert.ok(meshes > 0, 'Item has no renderable meshes');
      variants.push({ id: variant.id, status: 'passed', meshes });
    } catch (error) {
      variants.push({ id: variant.id, status: 'failed', error: error.message });
    }
  }
  batch.push({ id: item.id, slot: item.slot, variants, status: variants.every(v => v.status === 'passed') ? 'passed' : 'failed' });
  if (batch.length >= 25) {
    await appendFile(journal, batch.map(row => JSON.stringify(row)).join('\n') + '\n');
    console.log(`Validated through ${item.id}`); batch = [];
  }
}
if (batch.length) await appendFile(journal, batch.map(row => JSON.stringify(row)).join('\n') + '\n');
const rows = (await readFile(journal, 'utf8')).trim().split('\n').filter(Boolean).map(line => JSON.parse(line));
assert.equal(rows.length, items.length);
assert.equal(new Set(rows.map(row => row.id)).size, new Set(items.map(item => item.id)).size);
const summary = {
  items: rows.length, passed: rows.filter(row => row.status === 'passed').length,
  variants: rows.reduce((n, row) => n + row.variants.length, 0),
  failures: rows.filter(row => row.status !== 'passed'),
  residentScenes: library.sceneBuffers.size, residentTextures: library.textures.size,
};
await writeFile(new URL('runtime-summary.json', output), JSON.stringify(summary, null, 2) + '\n');
library.dispose();
console.log(JSON.stringify(summary, null, 2));
if (summary.failures.length) process.exitCode = 1;
