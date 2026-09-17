import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import * as THREE from 'three';

const loaderUrl = new URL('../src/CharacterModel.js', import.meta.url);
const base = new URL('../Models/Characters/BasicFemale/', import.meta.url);

test('basic female assembles as independently equipped parts on a skeleton', async () => {
  assert.ok(await readFile(loaderUrl).then(() => true, () => false), 'Character assembly is not implemented');
  const { CharacterModel } = await import(loaderUrl);
  const manifest = JSON.parse(await readFile(new URL('character.json', base)));
  const b = await readFile(new URL('character.bin', base));
  const textures = new Map(Object.keys(manifest.textures).map(path => { const t = new THREE.Texture(); t.name = path; return [path, t]; }));
  const model = new CharacterModel(manifest, b.buffer.slice(b.byteOffset, b.byteOffset + b.byteLength), textures);
  assert.equal(model.bodyId, 'female');
  assert.equal(model.animationId, 'rest');
  assert.equal(model.equipment.size, 6);
  assert.ok(model.bones.has('Bip01 Head'));
  assert.ok(model.bones.get('Bip01 Head').isBone);
  let skinned = 0;
  model.root.traverse(node => {
    if (!node.isSkinnedMesh) return;
    skinned++;
    assert.equal(node.geometry.attributes.skinIndex.count, node.geometry.attributes.position.count);
    assert.equal(node.geometry.attributes.skinWeight.count, node.geometry.attributes.position.count);
    assert.ok(node.skeleton.bones.every(bone => bone.isBone));
  });
  assert.ok(skinned >= 5, 'Clothing must retain skinning, not be baked into static geometry');
  model.dispose();
});
