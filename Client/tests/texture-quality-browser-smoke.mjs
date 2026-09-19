import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import { withBrowser } from './browser-harness.mjs';

const base = new URL('/character.html?noAnimations=1', process.env.VIEWER_URL ?? 'http://127.0.0.1:8132').href;
await withBrowser(base, 'window.characterReady === true', async ({ cdp, evaluate, delay, errors, failedRequests }) => {
  assert.equal(await evaluate("!!document.querySelector('#texture-quality')"), true);
  assert.equal(await evaluate("document.querySelector('#texture-quality').value"), '1x');
  const baseline = await cdp('Page.captureScreenshot', { format: 'png' });
  await writeFile('Client/Models/Characters/Wardrobe/verification/texture-quality-1x.png', Buffer.from(baseline.data, 'base64'));
  const loadedBytesIn = text => {
    const match = text.match(/([\d.]+) (MB|KB) loaded/);
    assert.ok(match, `The status line must report the loaded size, got "${text}"`);
    return match[2] === 'MB' ? Number(match[1]) * 1048576 : Number(match[1]) * 1024;
  };
  const bytesAt1x = await evaluate('window.characterViewer.loadedBytes()');
  assert.ok(bytesAt1x > 1024 * 1024, `The readout must count real fetches, got ${bytesAt1x} bytes`);
  assert.match(await evaluate("document.querySelector('#status').textContent"), /[\d.]+ (MB|KB) loaded/);
  const source = await evaluate("[...window.characterViewer.library.textureEntries.keys()][0]");
  const availableLevels = await evaluate(`Object.keys(window.characterViewer.library.index.textures[${JSON.stringify(source)}].variants)`);
  assert.ok(availableLevels.includes('4x'), `Expected generated 4× variants, got ${availableLevels.join(', ')}`);
  for (const quality of availableLevels.filter(level => level !== '1x')) {
    await evaluate(`window.characterViewer.selectTextureQuality('${quality}')`);
    await evaluate('window.characterViewer.whenIdle()');
    assert.equal(await evaluate('document.querySelector("#texture-quality").value'), quality);
    const loaded = await evaluate(`window.characterViewer.library.textureEntries.get(${JSON.stringify(source)}).quality`);
    assert.equal(loaded, quality, `A generated ${quality} variant must actually load`);
    const shot = await cdp('Page.captureScreenshot', { format: 'png' });
    await writeFile(`Client/Models/Characters/Wardrobe/verification/texture-quality-${quality}.png`, Buffer.from(shot.data, 'base64'));
  }
  // The readout must follow the level: a level is a different set of files, and the displayed number
  // is that same total. Note 4x need not be *larger* than 1x — once the generated levels are AVIF
  // they routinely weigh less than the 1x PNGs they replace.
  const bytesAt4x = await evaluate('window.characterViewer.loadedBytes()');
  assert.notEqual(bytesAt4x, bytesAt1x, `The readout must change with the level (${bytesAt4x} bytes at both)`);
  const displayed = loadedBytesIn(await evaluate("document.querySelector('#status').textContent"));
  assert.ok(Math.abs(displayed - bytesAt4x) < bytesAt4x * 0.01,
    `The status line must show the loaded total: displayed ${displayed}, measured ${bytesAt4x}`);
  // A missing level degrades to the highest generated one at or below the request.
  await evaluate(`delete window.characterViewer.library.index.textures[${JSON.stringify(source)}].variants['4x']`);
  await evaluate("window.characterViewer.selectTextureQuality('4x')");
  await evaluate('window.characterViewer.whenIdle()');
  assert.equal(await evaluate(`window.characterViewer.library.textureEntries.get(${JSON.stringify(source)}).quality`), '1x');
  assert.match(await evaluate('document.querySelector("#texture-quality-status").textContent'), /Requested: 4× · Loaded: 1×/);
  assert.deepEqual(errors, []); assert.deepEqual(failedRequests, []);
  // Reloading must apply the remembered level to the first loads. "Requested 4× · Loaded 1×"
  // after an F5 was a real defect: the stored preference only reached the dropdown, never the
  // library, so every texture was fetched at 1× and stayed there.
  await evaluate("window.characterViewer.selectTextureQuality('4x')");
  await evaluate('window.characterViewer.whenIdle()');
  errors.length = 0; failedRequests.length = 0;
  await cdp('Page.reload', { ignoreCache: true });
  for (let i = 0; i < 160; i++) {
    if (await evaluate('window.characterReady === true')) break;
    await delay(250);
  }
  await evaluate('window.characterViewer.whenIdle()');
  assert.equal(await evaluate("document.querySelector('#texture-quality').value"), '4x');
  const reloadedStatus = await evaluate("document.querySelector('#texture-quality-status').textContent");
  const reloadedLevels = await evaluate("[...new Set([...window.characterViewer.library.textureEntries.values()].map(entry => entry.quality))]");
  assert.deepEqual(reloadedLevels, ['4x'], `A reload must load the remembered level (status: ${reloadedStatus})`);
  assert.match(reloadedStatus, /Requested: 4× · Loaded: 4×/);
  assert.deepEqual(errors, []); assert.deepEqual(failedRequests, []);
  console.log(JSON.stringify({ source, requested: '4x', fallbackLoaded: '1x', reloaded: reloadedLevels.join(','), browserErrors: errors.length, failedRequests: failedRequests.length }));
});
