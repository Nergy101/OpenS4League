import { mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { withBrowser } from './browser-harness.mjs';

const base = new URL('/character.html?basic=1&noAnimations=1', process.env.VIEWER_URL ?? 'http://127.0.0.1:8132').href;
await withBrowser(base, 'window.characterReady === true', async ({ cdp, evaluate, delay, errors, failedRequests }) => {
  const output = new URL('../Models/Characters/BasicFemale/verification/', import.meta.url);
  await mkdir(output, { recursive: true });
  // The project crest must actually render here too.
  const brand = await evaluate(`(async () => {
    const image = document.querySelector('#brand img');
    const response = await fetch(document.querySelector('link[rel="icon"]').href);
    return { natural: image.naturalWidth, brandText: document.querySelector('#brand span').textContent,
      faviconStatus: response.status, faviconType: response.headers.get('content-type') };
  })()`);
  assert.ok(brand.natural > 0, 'The character viewer header crest did not load');
  assert.equal(brand.brandText, 'OpenS4League');
  assert.equal(brand.faviconStatus, 200);
  assert.match(brand.faviconType, /image\/svg\+xml/);
  const capture = async name => {
    await delay(300);
    const image = await cdp('Page.captureScreenshot', { format: 'png' });
    await writeFile(new URL(name + '.png', output), Buffer.from(image.data, 'base64'));
  };
  const report = await evaluate(`(() => {
    const v = window.characterViewer; let meshes = 0, skinnedMeshes = 0, vertices = 0;
    v.character.root.traverse(node => {
      if (!node.isMesh) return;
      meshes++; vertices += node.geometry.attributes.position.count;
      if (node.isSkinnedMesh) skinnedMeshes++;
    });
    const box = v.bounds();
    return { meshes, skinnedMeshes, vertices, bones: v.character.bones.size,
      equippedSlots: v.character.equipment.size, textures: v.assets.textures.size,
      bounds: { min: box.min.toArray(), max: box.max.toArray() },
      renderCalls: v.renderer.info.render.calls, triangles: v.renderer.info.render.triangles,
      bodyTypes: [...document.querySelector('#body').options].map(o => o.value),
      animations: [...document.querySelector('#animation').options].map(o => o.value) };
  })()`);
  assert.equal(report.equippedSlots, 6);
  assert.equal(report.meshes, 8);
  assert.equal(report.skinnedMeshes, 6);
  assert.equal(report.vertices, 1621);
  assert.equal(report.bones, 82);
  assert.deepEqual(report.bodyTypes, ['female']);
  assert.deepEqual(report.animations, ['rest']);
  assert.ok(report.bounds.max[1] - report.bounds.min[1] < 350, 'Character exploded vertically');
  assert.ok(report.bounds.max[0] - report.bounds.min[0] < 350, 'Character exploded horizontally');
  assert.ok(report.renderCalls > 0);
  await capture('front');
  await evaluate("document.querySelector('#side').click()"); await capture('side');
  await evaluate("document.querySelector('#back').click()"); await capture('back');
  await evaluate("document.querySelector('#front').click()");

  // Exercise every imported appearance through the actual UI, not only the model API.
  const variants = await evaluate(`(async () => {
    const viewer = window.characterViewer;
    const checks = [];
    for (const item of viewer.character.body.items) for (const variant of item.variants) {
      const select = document.querySelector('#variant-' + item.slot);
      select.value = variant.id; select.dispatchEvent(new Event('change'));
      await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
      const selection = viewer.character.equipment.get(item.slot);
      if (selection.variant.id !== variant.id) throw new Error('Variant UI did not apply');
      const textures = [];
      for (const part of selection.parts) part.group.traverse(n => {
        if (n.isMesh) textures.push(...n.material.filter(m => m.map).map(m => m.map.name));
      });
      for (const path of Object.values(variant.maps)) if (!textures.includes(path)) throw new Error('Variant texture not bound: ' + path);
      checks.push({ item: item.id, variant: variant.id });
    }
    document.querySelector('#reset-outfit').click();
    return checks;
  })()`);
  report.variantChecks = variants;
  report.expectedVariantCount = await evaluate('window.characterViewer.character.body.items.reduce((n, item) => n + item.variants.length, 0)');
  assert.equal(variants.length, report.expectedVariantCount);
  await evaluate("document.querySelector('#variant-hair').value = '1'; document.querySelector('#variant-hair').dispatchEvent(new Event('change'))");
  await capture('hair-variant');
  await evaluate("document.querySelector('#reset-outfit').click()");
  await evaluate("document.querySelector('#skeleton').click()");
  await capture('skeleton');
  await evaluate("document.querySelector('#skeleton').click()");

  // A normal drag must rotate the camera; no pointer lock is needed for this viewer.
  const before = await evaluate('window.characterViewer.camera.position.toArray()');
  await cdp('Input.dispatchMouseEvent', { type: 'mousePressed', x: 500, y: 420, button: 'left', clickCount: 1 });
  await cdp('Input.dispatchMouseEvent', { type: 'mouseMoved', x: 690, y: 470, button: 'left', buttons: 1 });
  await cdp('Input.dispatchMouseEvent', { type: 'mouseReleased', x: 690, y: 470, button: 'left', clickCount: 1 });
  await delay(400);
  const after = await evaluate('window.characterViewer.camera.position.toArray()');
  assert.ok(Math.hypot(...after.map((n, i) => n - before[i])) > 10, 'Orbit controls did not move');
  report.orbitControls = true;
  await evaluate('window.characterViewer.frame()');

  const native = await evaluate(`(() => {
    const model = window.characterViewer.character;
    model.resetPose();
    return JSON.stringify(model.root.toJSON());
  })()`);
  await writeFile(new URL('../female-basic.three.json', output), native);
  report.nativeRoundTrip = await evaluate(`(async () => {
    const THREE = await import('three');
    const json = await (await fetch('./Models/Characters/BasicFemale/female-basic.three.json')).json();
    const object = await new THREE.ObjectLoader().parseAsync(json);
    object.updateMatrixWorld(true);
    let skinned = 0, meshes = 0;
    object.traverse(node => { if (node.isMesh) meshes++; if (node.isSkinnedMesh) { skinned++; if (!node.skeleton) throw new Error('Lost skeleton'); } });
    return { meshes, skinned };
  })()`);
  assert.equal(report.nativeRoundTrip.meshes, report.meshes);
  assert.equal(report.nativeRoundTrip.skinned, report.skinnedMeshes);
  assert.deepEqual(errors, [], 'Browser/GLSL errors');
  assert.deepEqual(failedRequests, [], 'Missing character assets');
  report.browserErrors = errors; report.failedRequests = failedRequests;
  await writeFile(new URL('browser-report.json', output), JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify(report, null, 2));
});
