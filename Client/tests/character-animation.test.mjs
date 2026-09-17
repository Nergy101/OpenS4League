import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import * as THREE from 'three';
import { CharacterModel } from '../src/CharacterModel.js';

async function fixture() {
  const base = new URL('../Models/Characters/BasicFemale/', import.meta.url);
  const manifest = JSON.parse(await readFile(new URL('character.json', base)));
  const bytes = await readFile(new URL('character.bin', base));
  return new CharacterModel(manifest, bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), new Map());
}

test('named bone clips support pause, scrub, speed, one-shot playback and in-place motion', async () => {
  const model = await fixture();
  const root = model.bones.get('Bip01');
  const start = root.position.clone();
  const clip = new THREE.AnimationClip('test-walk', 1, [new THREE.VectorKeyframeTrack('Bip01.position', [0, 1],
    [...start.toArray(), start.x + 100, start.y + 10, start.z + 100])]);
  model.registerAnimation('test-walk', clip);
  model.playAnimation('test-walk', { loop: false, inPlace: true, rootBone: 'Bip01' });
  model.update(0.25);
  assert.ok(Math.abs(root.getWorldPosition(new THREE.Vector3()).x - start.x) < 0.001, 'In-place playback must remove horizontal root movement');
  assert.ok(Math.abs(root.position.y - start.y - 2.5) < 0.001, 'Vertical source motion must remain');
  model.setAnimationPaused(true); model.update(0.25);
  assert.ok(Math.abs(model.animationTime - 0.25) < 0.001);
  model.seekAnimation(0.5);
  assert.ok(Math.abs(root.position.y - start.y - 5) < 0.001);
  model.setAnimationInPlace(false);
  assert.ok(Math.abs(root.position.x - start.x - 50) < 0.001);
  model.setAnimationSpeed(2); model.setAnimationPaused(false); model.update(0.3);
  assert.ok(Math.abs(model.animationTime - 1) < 0.001);
  assert.equal(model.animationPaused, true, 'A one-shot emote should finish and hold its last pose');
  model.resetPose(); assert.equal(model.animationId, 'rest');
  assert.deepEqual(root.position.toArray(), start.toArray());
  model.dispose();
});

test('switching imported clips without UUIDs plays A then B then A', async () => {
  const model = await fixture();
  const hand = model.bones.get('Bip01 L Hand');
  const start = hand.position.clone();
  for (const [id, distance] of [['a', 10], ['b', 30]]) {
    const clip = THREE.AnimationClip.parse({ name: id, duration: 1, tracks: [
      { name: 'Bip01 L Hand.position', type: 'vector', times: [0, 1],
        values: [...start.toArray(), start.x, start.y + distance, start.z] },
    ] });
    model.registerAnimation(id, clip);
  }
  for (const [id, expected] of [['a', 5], ['b', 15], ['a', 5]]) {
    model.playAnimation(id); model.update(0.5);
    assert.equal(model.activeAction.getClip().name, id, 'Mixer reused another clip action');
    assert.ok(Math.abs(hand.position.y - start.y - expected) < 0.001);
  }
  assert.notEqual(model.animations.get('a').uuid, model.animations.get('b').uuid);
  model.dispose();
});

test('canonical bone names with spaces bind and equipment changes keep playing', async () => {
  const model = await fixture();
  const hand = model.bones.get('Bip01 L Hand');
  const start = hand.position.clone();
  model.registerAnimation('test-hand', new THREE.AnimationClip('test-hand', 2, [
    new THREE.VectorKeyframeTrack('Bip01 L Hand.position', [0,2], [...start.toArray(), start.x, start.y + 20, start.z]),
  ]));
  model.playAnimation('test-hand'); model.update(0.5);
  assert.ok(Math.abs(hand.position.y - start.y - 5) < 0.001);
  model.setItem('hair', '1000002', '1'); model.update(0.5);
  assert.ok(Math.abs(hand.position.y - start.y - 10) < 0.001);
  assert.equal(model.animationId, 'test-hand');
  model.dispose();
});
