import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import * as THREE from 'three';
import { CharacterModel } from '../src/CharacterModel.js';
import { scnMatrix } from '../src/ScnAssets.js';

const base = new URL('../Models/Characters/BasicFemale/', import.meta.url);
async function fixture() {
  const manifest = JSON.parse(await readFile(new URL('character.json', base)));
  const bytes = await readFile(new URL('character.bin', base));
  const textures = new Map(Object.keys(manifest.textures).map(path => { const texture = new THREE.Texture(); texture.name = path; return [path, texture]; }));
  const model = new CharacterModel(manifest, bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), textures);
  return { model, manifest };
}

test('rig world binds and every skinned vertex follow the original source equation', async () => {
  const { model, manifest } = await fixture();
  const rig = manifest.scenes.find(s => s.role === 'skeleton');
  for (const data of rig.nodes.filter(n => n.type === 'Bone')) {
    const expected = model.root.matrixWorld.clone().multiply(scnMatrix(data.matrix));
    const actual = model.bones.get(data.name).matrixWorld;
    assert.ok(actual.elements.every((value, i) => Math.abs(value - expected.elements[i]) < 0.001), data.name);
  }
  for (const moved of [false, true]) {
    if (moved) {
      model.root.position.set(37, -14, 91); model.root.scale.set(0.01, 0.01, -0.01);
      const pelvis = model.bones.get('Bip01 Pelvis'); pelvis.matrixAutoUpdate = true; pelvis.rotateZ(0.2);
    }
    model.root.updateMatrixWorld(true);
    model.root.traverse(mesh => {
      if (!mesh.isSkinnedMesh) return;
      const source = mesh.userData.source.details.bones;
      source.forEach((bone, i) => assert.deepEqual(mesh.skeleton.boneInverses[i].elements, scnMatrix(bone.Matrix).elements));
      mesh.skeleton.update();
      const position = mesh.geometry.attributes.position;
      const expected = Array.from({ length: position.count }, () => new THREE.Vector3());
      source.forEach((bone, i) => {
        const transform = mesh.skeleton.bones[i].matrixWorld.clone().multiply(scnMatrix(bone.Matrix));
        for (const weight of bone.Weight) {
          const raw = new THREE.Vector3().fromBufferAttribute(position, weight.Vertex);
          expected[weight.Vertex].addScaledVector(raw.applyMatrix4(transform), weight.Weight);
        }
      });
      for (let i = 0; i < position.count; i++) {
        const actual = mesh.applyBoneTransform(i, new THREE.Vector3().fromBufferAttribute(position, i)).applyMatrix4(mesh.matrixWorld);
        assert.ok(actual.distanceTo(expected[i]) < 0.001, `${mesh.name} vertex ${i}`);
      }
    });
  }
  model.dispose();
});

test('equipment recoloring replaces only the chosen slot and releases its old geometry', async () => {
  const { model } = await fixture();
  const hair = model.equipment.get('hair');
  const face = model.equipment.get('face');
  const oldMesh = hair.parts[0].group.getObjectByProperty('isMesh', true);
  let disposed = false;
  oldMesh.geometry.addEventListener('dispose', () => { disposed = true; });
  model.setItem('hair', hair.item.id, '1');
  assert.equal(model.equipment.size, 6);
  assert.equal(model.equipment.get('face'), face);
  assert.equal(disposed, true);
  const newMesh = model.equipment.get('hair').parts[0].group.getObjectByProperty('isMesh', true);
  assert.match(newMesh.material[0].map.name, /00_female_hair_1\.dds$/);
  assert.throws(() => model.setItem('hair', 'missing'), /not available/);
  assert.equal(model.equipment.get('hair').variant.id, '1');
  assert.notEqual(model.equipment.get('hair').parts[0].group.getObjectByName('Hair_Bone_Dummy'), face.parts[0].group.getObjectByName('Hair_Bone_Dummy'));
  model.dispose();
});

test('optional equipment can be removed and restores parts hidden by the outfit', async () => {
  const { model } = await fixture();
  const item = structuredClone(model.body.items.find(item => item.slot === 'hair'));
  item.id = 'test-accessory'; item.slot = 'accessory'; item.hides = ['00_Female_FacePart_Face'];
  model.body.items.push(item);
  const face = model.equipment.get('face').parts[0].group.getObjectByName('00_Female_FacePart_Face');
  model.setItem('accessory', item.id);
  assert.equal(face.visible, false);
  model.setItem('accessory', null);
  assert.equal(model.equipment.has('accessory'), false);
  assert.equal(face.visible, true);
  model.dispose();
});

test('semantic hide tokens are slot-aware and recognize authored part-name variants', async () => {
  const { model } = await fixture();
  const hat = structuredClone(model.body.items.find(item => item.slot === 'hair'));
  hat.id = 'test-hat'; hat.slot = 'accessory'; hat.hides = ['hair_all', 'face_all', 'gloves_part1', 'shoes_part4'];
  model.body.items.push(hat);
  const glove = model.equipment.get('gloves').parts[0].group.getObjectByProperty('isMesh', true);
  const shoe = model.equipment.get('shoes').parts[0].group.getObjectByProperty('isMesh', true);
  glove.name = 'test_hand_hide_parts_1_nocull'; shoe.name = 'test_foot_hide_part4_alphablend1';
  model.setItem('accessory', hat.id);
  for (const slot of ['hair', 'face']) model.equipment.get(slot).parts[0].group.traverse(node => {
    if (node.isMesh) assert.equal(node.visible, false, slot);
  });
  assert.equal(model.equipment.get('accessory').parts[0].group.getObjectByProperty('isMesh', true).visible, true);
  assert.equal(glove.visible, false); assert.equal(shoe.visible, false);
  model.setItem('accessory', null);
  assert.equal(model.equipment.get('hair').parts[0].group.getObjectByProperty('isMesh', true).visible, true);
  model.dispose();
});

test('body selection is catalog-driven and animation playback can reset exactly to rest', async () => {
  const { model, manifest } = await fixture();
  const other = structuredClone(manifest.catalog.bodies[0]);
  other.id = 'test-other-body'; other.label = 'Test fixture body';
  manifest.catalog.bodies.push(other);
  model.setBody(other.id);
  assert.equal(model.bodyId, other.id);
  assert.equal(model.equipment.size, 6);
  const head = model.bones.get('Bip01 Head');
  const rest = head.matrix.clone();
  const start = head.position.toArray();
  const clip = new THREE.AnimationClip('test-motion', 1, [new THREE.VectorKeyframeTrack(`${head.uuid}.position`, [0, 1], [...start, start[0] + 10, start[1], start[2]])]);
  model.registerAnimation('test-motion', clip);
  model.playAnimation('test-motion');
  model.update(0.5);
  assert.ok(Math.abs(head.position.x - start[0] - 5) < 0.001);
  model.resetPose();
  assert.deepEqual(head.matrix.elements, rest.elements);
  assert.equal(model.animationId, 'rest');
  assert.throws(() => model.setBody('not-imported'), /not imported/);
  assert.equal(model.bodyId, other.id);
  model.dispose();
});
