import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import { withBrowser } from './browser-harness.mjs';

const base = new URL('/character.html?noAnimations=1', process.env.VIEWER_URL ?? 'http://127.0.0.1:8132').href;
await withBrowser(base, 'window.characterReady === true', async ({ cdp, evaluate, delay, errors, failedRequests }) => {
  assert.equal(await evaluate("!!document.querySelector('#texture-quality')"), true);
  assert.equal(await evaluate("document.querySelector('#texture-quality').value"), '1x');
  const baseline = await cdp('Page.captureScreenshot', { format: 'png' });
  await writeFile('Client/Models/Characters/Wardrobe/verification/texture-quality-1x.png', Buffer.from(baseline.data, 'base64'));
  const source = await evaluate("[...window.characterViewer.library.textureEntries.keys()][0]");
  for (const quality of ['2x', '4x']) {
    await evaluate(`window.characterViewer.selectTextureQuality('${quality}')`);
    await evaluate('window.characterViewer.whenIdle()');
    assert.equal(await evaluate('document.querySelector("#texture-quality").value'), quality);
    const loaded = await evaluate(`window.characterViewer.library.textureEntries.get(${JSON.stringify(source)}).quality`);
    assert.equal(loaded, quality);
    const shot = await cdp('Page.captureScreenshot', { format: 'png' });
    await writeFile(`Client/Models/Characters/Wardrobe/verification/texture-quality-${quality}.png`, Buffer.from(shot.data, 'base64'));
  }
  await evaluate(`delete window.characterViewer.library.index.textures[${JSON.stringify(source)}].variants['4x']`);
  await evaluate("window.characterViewer.selectTextureQuality('4x')");
  await evaluate('window.characterViewer.whenIdle()');
  assert.equal(await evaluate(`window.characterViewer.library.textureEntries.get(${JSON.stringify(source)}).quality`), '2x');
  assert.match(await evaluate('document.querySelector("#texture-quality-status").textContent'), /Requested: 4× · Loaded: 2×/);
  assert.deepEqual(errors, []); assert.deepEqual(failedRequests, []);
  console.log(JSON.stringify({ source, requested: '4x', loaded: '2x', browserErrors: errors.length, failedRequests: failedRequests.length }));
});
