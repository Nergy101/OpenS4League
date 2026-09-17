import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import * as THREE from 'three';
import { buildStation2 } from '../src/Station2Loader.js';

test('legacy lightmaps are multiplicative, not divided by PI', async () => {
  const manifest = JSON.parse(await readFile(new URL('../Models/Maps/Station-2/station2.json', import.meta.url)));
  const b = await readFile(new URL('../Models/Maps/Station-2/station2.bin', import.meta.url));
  const textures = new Map(Object.keys(manifest.textures).map(path => [path, new THREE.Texture()]));
  const { root } = buildStation2(manifest, b.buffer.slice(b.byteOffset, b.byteOffset + b.byteLength), textures);
  const material = root.getObjectByName('Object11404').material.find(m => m.lightMap);
  assert.equal(material.lightMap.channel, 1);
  // three r186 MeshBasicMaterial multiplies its lightmap by RECIPROCAL_PI.
  assert.equal(material.lightMapIntensity, Math.PI);
  root.traverse(node => {
    if (node.isMesh && /sector\d+_enemy_/i.test(node.name))
      assert.equal(node.visible, false, 'Do not overlay both faction effects in the static preview');
  });
});
