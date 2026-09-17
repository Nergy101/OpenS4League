import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { buildStation2 } from '../src/Station2Loader.js';

test('safe-line alpha follows original animation keys', async () => {
  const manifest = JSON.parse(await readFile(new URL('../Models/Maps/Station-2/station2.json', import.meta.url)));
  const bytes = await readFile(new URL('../Models/Maps/Station-2/station2.bin', import.meta.url));
  const result = buildStation2(manifest, bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), new Map());
  assert.equal(typeof result.update, 'function', 'Original animation playback is missing');
  const safe = result.root.children.find(s => s.name === 'ds7_safeline.scn');
  const node = safe.children.find(n => n.userData.animations?.[0]?.TransformKeyData2.FloatKeys.length > 1);
  const keys = node.userData.animations[0].TransformKeyData2.FloatKeys;
  const seconds = value => value.split(':').reduce((sum, part) => sum * 60 + Number(part), 0);
  for (const key of keys.slice(0, -1)) {
    result.update(seconds(key.Duration));
    assert.ok(Math.abs(node.material[0].opacity - key.Alpha) < 1e-5);
  }
});
