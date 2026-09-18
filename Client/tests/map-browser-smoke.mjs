// Generic map browser verification: real WebGL, manifest parity, flying controls, screenshots.
// Usage: node tests/map-browser-smoke.mjs [map-id]   (default: neden-1, needs `npm start`)
import { mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { withBrowser } from './browser-harness.mjs';

const id = process.argv[2] ?? 'neden-1';
const origin = process.env.VIEWER_URL ?? 'http://127.0.0.1:8132';
const index = await (await fetch(new URL('Models/Maps/index.json', origin))).json();
const entry = index.maps.find(map => map.id === id);
assert.ok(entry, `Unknown map ${id}; available: ${index.maps.map(map => map.id).join(', ')}`);
const output = new URL(`../Models/Maps/${entry.directory}/verification/`, import.meta.url);
await mkdir(output, { recursive: true });

await withBrowser(`${origin}/?map=${id}`, 'window.mapReady === true', async ({ cdp, evaluate, delay, errors, failedRequests }) => {
  const report = await evaluate(`(() => {
    const m = window.s4map; let models = 0, vertices = 0, triangles = 0;
    const textures = new Set();
    m.root.traverse(n => {
      if (!n.isMesh) return;
      models++; vertices += n.geometry.attributes.position.count; triangles += n.geometry.index.count / 3;
      for (const material of Array.isArray(n.material) ? n.material : [n.material]) {
        if (material.map) textures.add(material.map.name);
        if (material.lightMap) textures.add(material.lightMap.name);
      }
    });
    return { id: m.entry.id, name: m.manifest.name, models, vertices, triangles, sceneTextures: textures.size,
      loadedTextures: Object.keys(m.manifest.textures).length, unresolved: m.manifest.unresolved,
      drawCalls: m.renderer.info.render.calls, renderedTriangles: m.renderer.info.render.triangles,
      heading: document.querySelector('#map-name').textContent, title: document.title,
      renderer: m.renderer.getContext().getParameter(m.renderer.getContext().RENDERER) };
  })()`);
  assert.equal(report.id, id);
  assert.equal(report.heading, report.name, 'The heading must come from the map manifest');
  assert.ok(report.drawCalls > 0, 'Nothing was drawn');
  assert.equal(report.models, await evaluate('window.s4map.manifest.totals.models'));
  assert.equal(report.vertices, await evaluate('window.s4map.manifest.totals.vertices'));
  assert.equal(report.triangles, await evaluate('window.s4map.manifest.totals.triangles'));

  // The Maps menu lists exactly what the registry offers and marks the open map.
  const menu = await evaluate(`(() => ({
    open: document.querySelector('#map-list').classList.contains('open'),
    items: [...document.querySelectorAll('#map-list a')].map(a => ({ label: a.textContent, href: a.getAttribute('href'), current: a.hasAttribute('aria-current') })),
  }))()`);
  assert.equal(menu.open, false, 'The map menu must start closed');
  assert.deepEqual(menu.items, index.maps.map(map => ({ label: map.name || map.id, href: `?map=${map.id}`, current: map.id === id })));
  await evaluate("document.querySelector('#maps').click()");
  assert.equal(await evaluate("document.querySelector('#map-list').classList.contains('open')"), true, 'The Maps button did not open the menu');
  assert.equal(await evaluate("document.querySelector('#maps').getAttribute('aria-expanded')"), 'true');
  assert.equal(await evaluate('document.activeElement.id'), 'map-search', 'Opening the menu must focus the filter');

  // The project crest must actually render (a wrong content type under nosniff breaks it silently).
  const brand = await evaluate(`(async () => {
    const image = document.querySelector('#brand img');
    const link = document.querySelector('link[rel="icon"]').href;
    const response = await fetch(link);
    return { natural: image.naturalWidth, complete: image.complete, alt: image.alt,
      faviconStatus: response.status, faviconType: response.headers.get('content-type'),
      brandText: document.querySelector('#brand span').textContent };
  })()`);
  assert.ok(brand.natural > 0 && brand.complete, 'The header crest did not load');
  assert.equal(brand.alt, 'OpenS4League');
  assert.equal(brand.brandText, 'OpenS4League');
  assert.equal(brand.faviconStatus, 200);
  assert.match(brand.faviconType, /image\/svg\+xml/, 'The favicon must be served as an SVG image, not octet-stream');

  // The roster overflows the panel, so the list has to scroll, and the filter must work by name and id.
  const scrolling = await evaluate(`(() => {
    const list = document.querySelector('#map-items');
    return { overflowY: getComputedStyle(list).overflowY, clientHeight: list.clientHeight, scrollHeight: list.scrollHeight };
  })()`);
  assert.equal(scrolling.overflowY, 'auto', 'The map list must scroll instead of growing with the roster');
  assert.ok(scrolling.scrollHeight > scrolling.clientHeight, 'The map list does not overflow its scroll area');
  const filter = await evaluate(`(() => {
    const search = document.querySelector('#map-search');
    const visible = () => [...document.querySelectorAll('#map-items a')].filter(a => !a.hidden).map(a => a.textContent);
    const query = value => { search.value = value; search.dispatchEvent(new Event('input')); return visible(); };
    const byName = query('sta');
    const none = query('no-such-map-anywhere');
    const emptyShown = !document.querySelector('#map-empty').hidden;
    const byId = query('neden-j');
    const all = query('').length;
    return { byName, none, emptyShown, byId, all };
  })()`);
  const expectedByName = index.maps
    .filter(map => (map.name ?? map.id).toLowerCase().includes('sta') || map.id.includes('sta'))
    .map(map => map.name ?? map.id);
  assert.deepEqual(filter.byName, expectedByName, 'Filtering by name must show exactly the matching maps');
  assert.equal(filter.none.length, 0);
  assert.equal(filter.emptyShown, true, 'An empty result must say so');
  assert.deepEqual(filter.byId, ['Neden-J'], 'Filtering by map id must work');
  assert.equal(filter.all, index.maps.length, 'Clearing the filter must show every map');

  const capture = async name => {
    await delay(600);
    const shot = await cdp('Page.captureScreenshot', { format: 'png' });
    await writeFile(new URL(name + '.png', output), Buffer.from(shot.data, 'base64'));
  };
  await evaluate('window.s4map.overview()');
  await capture('overview');
  await evaluate('window.s4map.ingame()');
  await capture('ingame');

  // Real mouse capture and camera movement, not just button presence.
  const before = await evaluate('window.s4map.camera.position.toArray()');
  await cdp('Emulation.setFocusEmulationEnabled', { enabled: true });
  await cdp('Page.bringToFront');
  await cdp('Input.dispatchMouseEvent', { type: 'mousePressed', x: 720, y: 450, button: 'left', clickCount: 1 });
  await cdp('Input.dispatchMouseEvent', { type: 'mouseReleased', x: 720, y: 450, button: 'left', clickCount: 1 });
  await delay(200);
  assert.equal(await evaluate('window.s4map.fly.isLocked'), true, 'Flying camera did not capture the mouse');
  await cdp('Input.dispatchKeyEvent', { type: 'keyDown', key: 'w', code: 'KeyW', windowsVirtualKeyCode: 87 });
  await delay(350);
  await cdp('Input.dispatchKeyEvent', { type: 'keyUp', key: 'w', code: 'KeyW', windowsVirtualKeyCode: 87 });
  const after = await evaluate('window.s4map.camera.position.toArray()');
  assert.ok(Math.hypot(...after.map((n, i) => n - before[i])) > 20, 'W did not move the flying camera');
  await cdp('Input.dispatchKeyEvent', { type: 'keyDown', key: 'e', code: 'KeyE', windowsVirtualKeyCode: 69 });
  await delay(200);
  await cdp('Input.dispatchKeyEvent', { type: 'keyUp', key: 'e', code: 'KeyE', windowsVirtualKeyCode: 69 });
  assert.ok(await evaluate('window.s4map.camera.position.y') > after[1] + 20, 'E did not fly upward');
  await evaluate('document.exitPointerLock()');
  await delay(100);
  assert.equal(await evaluate('window.s4map.fly.isLocked'), false);
  report.flyingCamera = { mouseCapture: true, forward: true, vertical: true, release: true };

  // Choosing another map in the menu really loads it.
  const target = index.maps.find(map => map.id !== id);
  if (target) {
    await evaluate(`document.querySelector('#map-list a[href="?map=${target.id}"]').click()`);
    for (let attempt = 0; attempt < 120; attempt++) {
      if (await evaluate(`window.mapReady === true && window.s4map?.entry?.id === '${target.id}'`)) break;
      await delay(250);
    }
    assert.equal(await evaluate('window.s4map?.entry?.id'), target.id, 'The menu did not load the selected map');
    assert.equal(await evaluate("document.querySelector('#map-name').textContent"), target.name || target.id);
    report.menuSwitch = { from: id, to: target.id };
  }

  assert.deepEqual(errors, [], 'Runtime/GLSL errors');
  assert.deepEqual(failedRequests, [], 'Network failures');
  report.browserErrors = errors;
  report.failedRequests = failedRequests;
  await writeFile(new URL('browser-report.json', output), JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify(report, null, 2));
});
