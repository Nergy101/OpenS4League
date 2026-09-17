import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

test('character viewer has its own English entrypoint and uses the existing Three.js installation', async () => {
  const html = await readFile(new URL('../character.html', import.meta.url), 'utf8').catch(() => '');
  assert.ok(html.includes('character-viewer.js'), 'Character viewer page is not implemented');
  assert.ok(html.includes('lang="en"'));
  assert.ok(html.includes('id="animation-play"'), 'Animation playback controls are missing');
  assert.ok(html.includes('T-pose (default)'), 'T-pose rest option is missing');
  assert.ok(html.includes('Standing idle is the default'), 'Standing idle default is missing');
  assert.ok(html.includes('value="3">3×</option>'), '3× animation speed is missing');
  assert.ok(html.includes('value="4">4×</option>'), '4× animation speed is missing');
  assert.ok(html.includes('value="4.5" selected>4.5×</option>'), '4.5× animation speed is missing');
  assert.ok(html.includes('value="10">10×</option>'), '10× animation speed is missing');
  assert.ok(html.includes('id="animation-loop"'), 'Loop animation toggle is missing');
  assert.ok(html.includes('class="animation-clock-row"'), 'Animation timing needs its own dedicated row');
  assert.ok(html.includes('/node_modules/three/'));
  assert.ok(!/https?:\/\//.test(html));
});
