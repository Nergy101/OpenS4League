import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import * as THREE from 'three';
import { CharacterModel } from '../src/CharacterModel.js';
import { scnMatrix } from '../src/ScnAssets.js';

const base = new URL('../Models/Characters/BasicFemale/', import.meta.url);
test('five positive influences are preserved in CPU, GPU, and shadow skinning', async () => {
  const manifest = JSON.parse(await readFile(new URL('character.json', base)));
  const bytes = await readFile(new URL('character.bin', base));
  const hair = manifest.scenes.find(s => s.source.includes('/hair/')).nodes.find(n => n.geometry);
  const first = hair.details.bones[0];
  const vertex = first.Weight[0].Vertex;
  first.Weight[0].Weight = 0.2;
  for (let i = 1; i < 5; i++) {
    const bone = structuredClone(first); bone.Weight = [{ Vertex: vertex, Weight: 0.2 }];
    bone.Matrix.M41 += i;
    hair.details.bones.push(bone);
  }
  const model = new CharacterModel(manifest, bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), new Map());
  const mesh = model.equipment.get('hair').parts[0].group.getObjectByProperty('isMesh', true);
  assert.ok(mesh.geometry.attributes.skinWeight2, 'Extra influences were not retained');
  const raw = new THREE.Vector3().fromBufferAttribute(mesh.geometry.attributes.position, vertex);
  const expected = new THREE.Vector3();
  hair.details.bones.forEach((bone, i) => {
    const value = bone.Weight.find(w => w.Vertex === vertex);
    if (value) expected.addScaledVector(raw.clone().applyMatrix4(scnMatrix(bone.Matrix)).applyMatrix4(mesh.skeleton.bones[i].matrixWorld), value.Weight);
  });
  const actual = mesh.applyBoneTransform(vertex, raw.clone()).applyMatrix4(mesh.matrixWorld);
  assert.ok(actual.distanceTo(expected) < 0.001);
  for (const material of [...mesh.material, mesh.customDepthMaterial, mesh.customDistanceMaterial]) {
    assert.ok(material);
    const shader = { vertexShader: THREE.ShaderLib.phong.vertexShader };
    material.onBeforeCompile(shader);
    assert.ok(shader.vertexShader.includes('skinWeight2'));
    assert.ok(shader.vertexShader.includes('skinned += boneMatX2'));
  }
  model.dispose();
});
