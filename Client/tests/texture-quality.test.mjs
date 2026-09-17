import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { TextureVariantStore, selectTextureVariant } from '../src/TextureVariantStore.js';

test('selects requested variant and falls back downward', () => {
  const descriptor = { source: 'hair.dds', kind: 'color', variants: {
    '1x': { file: 'textures/source.png', width: 128, height: 128 },
    '2x': { file: 'textures/2x.png', width: 256, height: 256 },
    '4x': { file: 'textures/4x.png', width: 512, height: 512 },
  } };
  assert.equal(selectTextureVariant(descriptor, '2x').width, 256);
  assert.equal(selectTextureVariant(descriptor, '8x').width, 128);
});

test('shares pending loads and disposes only after release', async () => {
  const descriptor = { file: 'textures/source.png', width: 8, height: 4, kind: 'color', variants: {
    '1x': { file: 'textures/source.png', width: 8, height: 4 },
    '2x': { file: 'textures/2x.png', width: 16, height: 8 },
  } };
  let loads = 0;
  const texture = new THREE.Texture();
  texture.dispose = () => { texture.userData.disposed = true; };
  const store = new TextureVariantStore({ hair: descriptor }, 'http://assets.test/', {
    loadAsync: async () => { loads++; return texture; },
  });
  const [a, b] = await Promise.all([store.acquire('hair', '2x'), store.acquire('hair', '2x')]);
  assert.equal(a.texture, texture); assert.equal(b.texture, texture);
  assert.equal(loads, 1); assert.equal(a.quality, '2x');
  store.release('hair', '2x'); assert.equal(texture.userData.disposed, undefined);
  store.release('hair', '2x'); assert.equal(texture.userData.disposed, true);
});

test('reports actual fallback quality', async () => {
  const texture = new THREE.Texture();
  const store = new TextureVariantStore({ hair: { kind: 'color', variants: {
    '1x': { file: 'one.png', width: 4, height: 4 },
  } } }, 'http://assets.test/', { loadAsync: async () => texture });
  const result = await store.acquire('hair', '4x');
  assert.equal(result.quality, '1x');
  assert.equal(result.requestedQuality, '4x');
  store.release('hair', '4x');
});
