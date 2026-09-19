import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { withBrowser } from './browser-harness.mjs';

const shots = 'Client/Models/Maps/Station-2/verification';
const base = new URL('/?map=station-2', process.env.VIEWER_URL ?? 'http://127.0.0.1:8132').href;
await withBrowser(base, 'window.mapReady === true', async ({ cdp, evaluate, errors, failedRequests }) => {
  assert.equal(await evaluate("!!document.querySelector('#texture-quality')"), true);
  assert.equal(await evaluate("document.querySelector('#texture-quality').value"), '1x');
  const levels = await evaluate('[...window.s4map.levels().values()]');
  assert.deepEqual([...new Set(levels)], ['1x'], 'A map must open at its decoded original level');
  await mkdir(shots, { recursive: true });
  const baseline = await cdp('Page.captureScreenshot', { format: 'png' });
  await writeFile(`${shots}/texture-quality-1x.png`, Buffer.from(baseline.data, 'base64'));

  assert.equal(await evaluate("window.s4map.selectTextureQuality('4x')"), true);
  const counts = {};
  for (const level of await evaluate('[...window.s4map.levels().values()]')) counts[level] = (counts[level] ?? 0) + 1;
  assert.equal(counts['1x'], undefined, `Every texture must load its generated level, got ${JSON.stringify(counts)}`);
  assert.ok(counts['4x'] > 0, `Expected generated 4x levels, got ${JSON.stringify(counts)}`);
  const status = await evaluate("document.querySelector('#texture-quality-status').textContent");
  assert.match(status, /Requested: 4× · Loaded: 4× on all \d+ textures/, status);
  // The generated level must be the file actually fetched, not just a relabelled original.
  const loaded = await evaluate(`(() => {
    const textures = [...window.s4map.textures().values()];
    return { count: textures.length, largest: Math.max(...textures.map(t => t.image?.width ?? 0)) };
  })()`);
  assert.equal(loaded.count, 53, 'Station-2 has 53 indexed textures');
  assert.ok(loaded.largest >= 2048, `A loaded 4x texture must be its generated size, got ${loaded.largest}`);
  const shot = await cdp('Page.captureScreenshot', { format: 'png' });
  await writeFile(`${shots}/texture-quality-4x.png`, Buffer.from(shot.data, 'base64'));

  await evaluate("window.s4map.selectTextureQuality('1x')");
  assert.deepEqual([...new Set(await evaluate('[...window.s4map.levels().values()]'))], ['1x'],
    'Switching back must return every texture to its decoded original');
  assert.deepEqual(errors, []); assert.deepEqual(failedRequests, []);
  console.log(JSON.stringify({ map: 'station-2', counts, status, largest: loaded.largest,
    browserErrors: errors.length, failedRequests: failedRequests.length }));
});
