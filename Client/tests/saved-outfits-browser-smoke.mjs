import assert from 'node:assert/strict';
import { withBrowser } from './browser-harness.mjs';

const base = new URL('/character.html?basic=1&noAnimations=1', process.env.VIEWER_URL ?? 'http://127.0.0.1:8132').href;
await withBrowser(base, 'window.characterReady === true', async ({ cdp, evaluate, delay, errors, failedRequests }) => {
  assert.equal(await evaluate("!!document.querySelector('#save-outfit')"), true, 'Saved outfit controls are missing');
  await evaluate(`(async () => {
    await window.characterViewer.selectEquipment('hair', '1000002', '1');
    document.querySelector('#outfit-name').value = 'Browser saved outfit';
    document.querySelector('#save-outfit').click();
  })()`);
  assert.equal(await evaluate("document.querySelector('#saved-outfits').options.length"), 2);
  await cdp('Page.reload');
  for (let i = 0; i < 100; i++) {
    if (await evaluate('window.characterReady === true')) break;
    await delay(100);
  }
  await evaluate(`(async () => {
    const select = document.querySelector('#saved-outfits');
    select.selectedIndex = 1; select.dispatchEvent(new Event('change'));
    document.querySelector('#apply-outfit').click();
    await window.characterViewer.whenIdle();
  })()`);
  assert.equal(await evaluate("window.characterViewer.character.equipment.get('hair').variant.id"), '1');
  await evaluate("document.querySelector('#delete-outfit').click()");
  assert.equal(await evaluate("document.querySelector('#saved-outfits').options.length"), 1);
  assert.deepEqual(errors, []); assert.deepEqual(failedRequests, []);
  console.log('Saved outfit UI: save, reload persistence, apply, and delete passed.');
});
