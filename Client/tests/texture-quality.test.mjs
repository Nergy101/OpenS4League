import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { TextureVariantStore, selectTextureVariant } from '../src/TextureVariantStore.js';

test('selects the requested level and degrades downward through what the bundle has', () => {
  const descriptor = { source: 'hair.dds', kind: 'color', variants: {
    '1x': { file: 'textures/source.png', width: 128, height: 128 },
    '4x': { file: 'textures/4x.png', width: 512, height: 512 },
  } };
  assert.equal(selectTextureVariant(descriptor, '4x').width, 512);
  assert.equal(selectTextureVariant(descriptor, '1x').width, 128);
});

test('a bundle that still carries a legacy 2x serves it between 4x and 1x', () => {
  const descriptor = { source: 'hair.dds', kind: 'color', variants: {
    '1x': { file: 'textures/source.png', width: 128, height: 128 },
    '2x': { file: 'textures/2x.png', width: 256, height: 256 },
    '4x': { file: 'textures/4x.png', width: 512, height: 512 },
  } };
  assert.equal(selectTextureVariant(descriptor, '4x').width, 512);
  assert.equal(selectTextureVariant({ ...descriptor, variants: { '1x': descriptor.variants['1x'], '2x': descriptor.variants['2x'] } }, '4x').width, 256,
    'Never silently drop back to the original when a generated level exists');
});

test('a missing 4x degrades to 1x and respects the GPU limit', () => {
  const descriptor = { source: 'hair.dds', kind: 'color', variants: {
    '1x': { file: 'textures/source.png', width: 128, height: 128 },
    '4x': { file: 'textures/4x.png', width: 512, height: 512 },
  } };
  assert.equal(selectTextureVariant(descriptor, '4x').width, 512);
  assert.equal(selectTextureVariant({ ...descriptor, variants: { '1x': descriptor.variants['1x'] } }, '4x').width, 128);
  assert.equal(selectTextureVariant(descriptor, '4x', 300).width, 128, 'A 4x request over the GPU limit must degrade, not fail');
});

test('8x is not a level this build knows about', () => {
  const descriptor = { source: 'hair.dds', kind: 'color', variants: {
    '1x': { file: 'textures/source.png', width: 128, height: 128 },
  } };
  assert.throws(() => selectTextureVariant(descriptor, '8x'), /Unknown texture quality '8x'/);
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
