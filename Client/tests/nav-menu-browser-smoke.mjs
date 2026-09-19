// The app-level navigation on both previews, plus the page-specific menu on each: real browser,
// real clicks — the header ignores pointer events on the character page, so this must be checked
// in a browser rather than by reading markup.
import assert from 'node:assert/strict';
import { withBrowser } from './browser-harness.mjs';

const origin = process.env.VIEWER_URL ?? 'http://127.0.0.1:8132';

await withBrowser(`${origin}/index.html?map=office`, '!!document.querySelector("#maps")', async ({ cdp, evaluate, delay, errors, failedRequests }) => {
  const nav = await evaluate(`(() => {
    const links = [...document.querySelectorAll('#main-nav ul a')];
    return { labels: links.map(a => a.textContent), hrefs: links.map(a => a.getAttribute('href')),
      current: links.filter(a => a.getAttribute('aria-current') === 'page').map(a => a.textContent),
      brand: document.querySelector('#brand span').textContent };
  })()`);
  assert.deepEqual(nav.labels, ['Maps', 'Characters']);
  assert.deepEqual(nav.hrefs, ['./index.html', './character.html']);
  assert.deepEqual(nav.current, ['Maps'], 'The map viewer must mark Maps as the current section');
  assert.equal(nav.brand, 'OpenS4League');

  // The map viewer's own menu stays underneath the main navigation and keeps working.
  assert.equal(await evaluate("document.querySelector('#maps').closest('.controls') !== null"), true, 'The roster menu belongs to the page-specific row');
  await evaluate("document.querySelector('#maps').click()");
  await delay(150);
  assert.equal(await evaluate("document.querySelector('#map-list').classList.contains('open')"), true, 'The roster menu must open');
  assert.ok(await evaluate("document.querySelectorAll('#map-items a').length") > 40, 'The roster must still be populated');
  await cdp('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
  await cdp('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
  await delay(150);
  assert.equal(await evaluate("document.querySelector('#map-list').classList.contains('open')"), false, 'Escape must close it');

  // Character viewer: same navigation, its own menus, and they must actually be clickable.
  await cdp('Page.navigate', { url: `${origin}/character.html` });
  for (let i = 0; i < 160; i++) {
    if (await evaluate('window.characterReady === true')) break;
    await delay(250);
  }
  const characterNav = await evaluate(`(() => {
    const links = [...document.querySelectorAll('#main-nav ul a')];
    const menus = [...document.querySelectorAll('.page-menus')];
    const style = getComputedStyle(menus[0]);
    return { current: links.filter(a => a.getAttribute('aria-current') === 'page').map(a => a.textContent),
      labels: links.map(a => a.textContent), menuPointerEvents: style.pointerEvents,
      bodyPointerEvents: getComputedStyle(document.querySelector('#body')).pointerEvents,
      // Body and Texture quality are each their own row now, not sharing one.
      bodyInMenu: menus.some(menu => menu.contains(document.querySelector('#body'))),
      qualityInMenu: menus.some(menu => menu.contains(document.querySelector('#texture-quality'))),
      bodyBox: document.querySelector('#body').getBoundingClientRect().width };
  })()`);
  assert.deepEqual(characterNav.current, ['Characters'], 'The character viewer must mark Characters as the current section');
  assert.deepEqual(characterNav.labels, ['Maps', 'Characters']);
  assert.equal(characterNav.bodyInMenu && characterNav.qualityInMenu, true, 'Body and quality must each belong to a page menu row');
  assert.equal(characterNav.bodyPointerEvents, 'auto', 'The header ignores pointer events, so its menus must opt back in');
  assert.ok(characterNav.bodyBox > 40, `The body menu must be laid out and clickable, got ${characterNav.bodyBox}px`);

  // Switching rig through the top-left menu must still drive the viewer.
  await evaluate(`(() => { const select = document.querySelector('#body'); select.value = 'male'; select.dispatchEvent(new Event('change')); })()`);
  await evaluate('window.characterViewer.whenIdle()');
  await delay(1500);
  assert.equal(await evaluate('window.characterViewer.character.bodyId'), 'male', 'The header body menu must switch the rig');
  assert.deepEqual(errors, []); assert.deepEqual(failedRequests, []);
  console.log(JSON.stringify({ mapViewerNav: 'ok', characterViewerNav: 'ok', pageMenus: 'ok', browserErrors: errors.length, failedRequests: failedRequests.length }));
});
