import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const loaderUrl = new URL('../src/MapLoader.js', import.meta.url);
test('original binary geometry becomes a complete Three.js hierarchy', async () => {
  const available = await readFile(loaderUrl).then(() => true, () => false);
  assert.ok(available, 'Three.js map loader has not been implemented');
  const { buildMap } = await import(loaderUrl);
  const manifest = JSON.parse(await readFile(new URL('../Models/Maps/Station-2/station2.json', import.meta.url)));
  const bytes = await readFile(new URL('../Models/Maps/Station-2/station2.bin', import.meta.url));
  const buffer = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
  const { root } = buildMap(manifest, buffer, new Map(), 's4-map-threejs');
  let models = 0, vertices = 0, indices = 0;
  root.traverse(node => {
    if (!node.isMesh) return;
    models++;
    vertices += node.geometry.attributes.position.count;
    indices += node.geometry.index.count;
    assert.ok(node.matrix.elements.every(Number.isFinite));
  });
  assert.equal(models, manifest.totals.models);
  assert.equal(vertices, manifest.totals.vertices);
  assert.equal(indices, manifest.totals.triangles * 3);
  assert.equal(root.children.length, manifest.totals.scenes);
  root.traverse(node => {
    if (node.isMesh && /^oct_/i.test(node.name)) assert.equal(node.visible, false, node.name);
  });
});
