// The character viewer's lighting environments: options, real scene changes, and persistence.
import assert from 'node:assert/strict';
import { withBrowser } from './browser-harness.mjs';

const origin = process.env.VIEWER_URL ?? 'http://127.0.0.1:8132';

const readLighting = `(() => {
  const v = window.characterViewer;
  const lights = [];
  v.scene.traverse(node => { if (node.isLight) lights.push(node); });
  const hemisphere = lights.find(light => light.isHemisphereLight);
  const directional = lights.filter(light => light.isDirectionalLight);
  return { selected: document.querySelector('#lighting').value,
    options: [...document.querySelector('#lighting').options].map(option => ({ value: option.value, label: option.textContent })),
    background: getComputedStyle(document.querySelector('#stage')).backgroundImage,
    hemisphere: { sky: hemisphere.color.getHexString(), ground: hemisphere.groundColor.getHexString(), intensity: hemisphere.intensity },
    directional: directional.map(light => ({ color: light.color.getHexString(), intensity: light.intensity, position: light.position.toArray() })),
    shadowOpacity: v.scene.children.find(node => node.material && node.material.isShadowMaterial)?.material.opacity };
})()`;

await withBrowser(`${origin}/character.html`, 'window.characterReady === true', async ({ cdp, evaluate, delay, errors, failedRequests }) => {
  const studio = await evaluate(readLighting);
  assert.deepEqual(studio.options.map(option => option.value), ['studio', 'daylight', 'sunset', 'night', 'showroom']);
  assert.deepEqual(studio.options.map(option => option.label), ['Studio', 'Daylight', 'Sunset', 'Night', 'Showroom']);
  assert.equal(studio.selected, 'studio', 'The viewer starts in its original lighting');
  assert.equal(studio.directional.length, 2, 'Key and rim lights');

  // Body, texture quality, and lighting each sit in their own row, stacked in that order.
  const rows = await evaluate(`[...document.querySelectorAll('.page-menus')].map(row => {
    const box = row.getBoundingClientRect();
    return { top: Math.round(box.top), bottom: Math.round(box.bottom), selects: [...row.querySelectorAll('select')].map(select => select.id) };
  })`);
  assert.equal(rows.length, 3, 'Three menu rows');
  assert.deepEqual(rows[0].selects, ['body']);
  assert.deepEqual(rows[1].selects, ['texture-quality']);
  assert.deepEqual(rows[2].selects, ['lighting']);
  assert.ok(rows[1].top >= rows[0].bottom - 2, `Texture quality must be on its own row below Body (${rows[1].top} < ${rows[0].bottom})`);
  assert.ok(rows[2].top >= rows[1].bottom - 2, `Lighting must be on its own row below the others (${rows[2].top} < ${rows[1].bottom})`);

  const applied = new Map([['studio', studio]]);
  for (const id of ['sunset', 'showroom', 'night']) {
    await evaluate(`(() => { const select = document.querySelector('#lighting'); select.value = '${id}'; select.dispatchEvent(new Event('change')); })()`);
    await delay(200);
    const state = await evaluate(readLighting);
    assert.equal(state.selected, id, `The menu must select ${id}`);
    assert.notDeepEqual(state.directional, studio.directional, `${id}: the lights must actually change`);
    assert.notEqual(state.background, studio.background, `${id}: the stage background must change`);
    applied.set(id, state);
  }
  assert.equal(new Set([...applied.values()].map(state => state.background)).size, 4, 'Every environment has its own background');

  // The choice must survive a reload, like the texture quality preference.
  await evaluate("window.characterViewer.lighting.apply('sunset')");
  errors.length = 0; failedRequests.length = 0;
  await cdp('Page.reload', { ignoreCache: true });
  for (let i = 0; i < 160; i++) {
    if (await evaluate('window.characterReady === true')) break;
    await delay(250);
  }
  const reloaded = await evaluate(readLighting);
  const sunset = applied.get('sunset');
  assert.equal(reloaded.selected, 'sunset', 'The remembered lighting environment must be restored');
  assert.deepEqual(reloaded.hemisphere, sunset.hemisphere, 'The remembered environment must be applied in full');
  assert.deepEqual(reloaded.directional, sunset.directional, 'The remembered light rig must be restored');
  assert.equal(reloaded.shadowOpacity, sunset.shadowOpacity, 'The remembered shadow strength must be restored');
  assert.deepEqual(errors, []); assert.deepEqual(failedRequests, []);
  console.log(JSON.stringify({ environments: studio.options.map(option => option.label), applied: [...applied.keys()], reloaded: reloaded.selected, browserErrors: errors.length, failedRequests: failedRequests.length }));
});
