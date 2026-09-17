import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

test('local viewer has a self-contained entrypoint with no CDN dependency', async () => {
  const page = await readFile(new URL('../index.html', import.meta.url), 'utf8').catch(() => '');
  assert.ok(page.includes('./src/viewer.js'), 'Runnable viewer entrypoint is missing');
  assert.ok(page.includes('/node_modules/three/'));
  assert.ok(!/https?:\/\//.test(page));
  const code = await readFile(new URL('../src/viewer.js', import.meta.url), 'utf8');
  assert.ok(!code.includes('OrbitControls'), 'Traversal must use only a flying camera');
  assert.ok(code.includes("renderer.domElement.addEventListener('click'"), 'Click-to-fly must be available');
});
