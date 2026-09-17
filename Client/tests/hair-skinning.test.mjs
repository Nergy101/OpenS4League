import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import * as THREE from 'three';
import { CharacterModel } from '../src/CharacterModel.js';
import { scnMatrix } from '../src/ScnAssets.js';

const basic = new URL('../Models/Characters/BasicFemale/', import.meta.url);
const wardrobe = new URL('../Models/Characters/Wardrobe/', import.meta.url);
const binary = async url => {
  const bytes = await readFile(url);
  return bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
};
async function fixture(itemId = '1000034') {
  const manifest = JSON.parse(await readFile(new URL('character.json', basic)));
  const index = JSON.parse(await readFile(new URL('index.json', wardrobe)));
  const item = index.catalog.bodies.find(body => body.id === 'female').items.find(item => item.id === itemId);
  const path = item.parts[0].scene;
  const descriptor = index.scenes[path];
  const source = JSON.parse(await readFile(new URL(descriptor.json, wardrobe)));
  manifest.catalog.bodies[0].items.push(item);
  manifest.scenes.push(source);
  const model = new CharacterModel(manifest, await binary(new URL('character.bin', basic)), new Map(),
    new Map([[path, await binary(new URL(descriptor.bin, wardrobe))]]));
  model.setItem('hair', item.id);
  return { model, source };
}

function baseMatrix(node) {
  const animation = node.details.animations.find(animation => animation.Name === 'base');
  const key = (animation.TransformKeyData ?? animation.TransformKeyData2).TransformKey;
  return new THREE.Matrix4().compose(
    new THREE.Vector3(key.Translation.X, key.Translation.Y, key.Translation.Z),
    new THREE.Quaternion(key.Rotation.X, key.Rotation.Y, key.Rotation.Z, key.Rotation.W),
    new THREE.Vector3(key.Scale.X, key.Scale.Y, key.Scale.Z));
}

for (const [id, label] of [['1000025', 'Fist Hair'], ['1000008', 'Double Tail']]) {
  test(`${label} rigid scalp stays at its authored head-relative position`, async () => {
    const { model, source } = await fixture(id);
    try {
      const head = model.bones.get('Bip01 Head');
      const cap = source.nodes.find(node => node.geometry && !node.details.bones.length);
      const mesh = model.root.getObjectByName(cap.name);
      const parent = source.nodes.find(node => node.name === cap.parent);
      closeMatrix(scnMatrix(parent.matrix).invert().multiply(scnMatrix(cap.matrix)), baseMatrix(cap), 'Source scalp base');
      for (const posed of [false, true]) {
        if (posed) { head.matrixAutoUpdate = true; head.rotateY(0.3); }
        model.update(0);
        const expected = head.matrixWorld.clone().multiply(scnMatrix(source.matrix)).multiply(scnMatrix(cap.matrix));
        closeMatrix(mesh.matrixWorld, expected, `${label} scalp, posed=${posed}`);
      }
      model.resetPose();
      closeMatrix(mesh.matrix, baseMatrix(cap), `${label} reset`);
    } finally { model.dispose(); }
  });
}

function closeMatrix(actual, expected, message) {
  const error = Math.max(...actual.elements.map((value, i) => Math.abs(value - expected.elements[i])));
  assert.ok(error < 0.001, `${message}: maximum matrix error ${error}`);
}

test('Ponytail child bone uses the authored parent-local base pose, not the scene-space bind as a local', async () => {
  const { model, source } = await fixture();
  try {
    const tail = source.nodes.find(node => node.name === '38_Female_Hair_Bone');
    const parent = source.nodes.find(node => node.name === tail.parent);
    // Independent source channels agree: scene bind made parent-relative equals base animation TRS.
    closeMatrix(scnMatrix(parent.matrix).invert().multiply(scnMatrix(tail.matrix)), baseMatrix(tail), 'Source base pose');
    const bone = model.root.getObjectByName(tail.name);
    closeMatrix(bone.matrix, baseMatrix(tail), 'Rendered ponytail local pose');
    model.resetPose();
    closeMatrix(bone.matrix, baseMatrix(tail), 'Ponytail reset pose');
  } finally { model.dispose(); }
});

test('Ponytail vertices use scene-space private bone binds exactly once, including a posed head and transformed actor', async () => {
  const { model, source } = await fixture();
  try {
    const mesh = model.equipment.get('hair').parts[0].group.getObjectByProperty('isSkinnedMesh', true);
    const head = model.bones.get('Bip01 Head');
    for (const moved of [false, true]) {
      if (moved) {
        model.root.position.set(37, -14, 91); model.root.scale.set(0.7, 0.7, -0.7);
        head.matrixAutoUpdate = true; head.rotateZ(0.3); head.rotateY(-0.2);
      }
      model.update(0);
      const expected = Array.from({ length: mesh.geometry.attributes.position.count }, () => new THREE.Vector3());
      for (const weight of mesh.userData.source.details.bones) {
        const bone = source.nodes.find(node => node.name === weight.Name);
        const transform = head.matrixWorld.clone().multiply(scnMatrix(source.matrix))
          .multiply(scnMatrix(bone.matrix)).multiply(scnMatrix(weight.Matrix));
        for (const influence of weight.Weight) {
          const raw = new THREE.Vector3().fromBufferAttribute(mesh.geometry.attributes.position, influence.Vertex);
          expected[influence.Vertex].addScaledVector(raw.applyMatrix4(transform), influence.Weight);
        }
      }
      for (let i = 0; i < expected.length; i++) {
        const actual = mesh.getVertexPosition(i, new THREE.Vector3()).applyMatrix4(mesh.matrixWorld);
        assert.ok(actual.distanceTo(expected[i]) < 0.001, `Ponytail vertex ${i}, posed=${moved}, error=${actual.distanceTo(expected[i])}`);
      }
    }
  } finally { model.dispose(); }
});
