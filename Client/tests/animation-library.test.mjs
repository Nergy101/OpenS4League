import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const moduleUrl = new URL('../src/CharacterAnimations.js', import.meta.url);
test('animation packs preserve descriptors and load clips only when selected', async () => {
  assert.ok(await readFile(moduleUrl).then(() => true, () => false), 'Animation pack loading is missing');
  const { CharacterAnimations } = await import(moduleUrl);
  const requested = [];
  const pack = new CharacterAnimations({ format: 's4-character-animations', version: 1, bodyId: 'female',
    clips: [{ id: 'test-idle', label: 'Test idle', category: 'Movement', url: 'clips/idle.json', loop: true }] },
    new URL('http://animation.test/female/index.json'), async url => {
      requested.push(url.href);
      return { name: 'test-idle', duration: 1, tracks: [{ name: 'Bip01.position', type: 'vector', times: [0, 1], values: [0, 0, 0, 0, 1, 0] }] };
    });
  assert.equal(requested.length, 0);
  assert.equal(pack.clips[0].label, 'Test idle');
  const [a,b] = await Promise.all([pack.get('test-idle'), pack.get('test-idle')]);
  assert.equal(a,b); assert.equal(requested.length, 1);
  assert.equal(a.tracks[0].name, 'Bip01.position');
  await assert.rejects(pack.get('missing'), /not imported/);
});
