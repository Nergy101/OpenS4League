// Real Station-2 WebGL, flying controls, and native model round trip.
import { writeFile, mkdir } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { withBrowser } from './browser-harness.mjs';

const base = process.env.VIEWER_URL ?? 'http://127.0.0.1:8132';
await withBrowser(base, 'window.station2Ready === true', async ({ cdp, evaluate, delay, errors, failedRequests }) => {
  assert.equal(await evaluate(`(() => {
    const m = window.station2;
    const sky = m.skies[0].children[0];
    sky.geometry.computeBoundingSphere();
    const sphere = sky.geometry.boundingSphere.clone().applyMatrix4(sky.matrixWorld);
    return m.camera.far > sphere.radius + sphere.center.distanceTo(m.camera.position);
  })()`), true, 'Original sky is outside the camera far plane');
  const output = new URL('../Models/Maps/Station-2/verification/', import.meta.url);
  await mkdir(output, { recursive: true });
  const report = await evaluate(`(() => {
    const m = window.station2; let models = 0, vertices = 0, triangles = 0;
    const textures = new Set();
    m.root.traverse(n => {
      if (!n.isMesh) return;
      models++; vertices += n.geometry.attributes.position.count; triangles += n.geometry.index.count / 3;
      for (const material of Array.isArray(n.material) ? n.material : [n.material]) {
        if (material.map) textures.add(material.map.name);
        if (material.lightMap) textures.add(material.lightMap.name);
      }
    });
    return { models, vertices, triangles, sceneTextures: textures.size, loadedTextures: Object.keys(m.manifest.textures).length,
      drawCalls: m.renderer.info.render.calls, renderedTriangles: m.renderer.info.render.triangles, unresolved: m.manifest.unresolved,
      renderer: m.renderer.getContext().getParameter(m.renderer.getContext().RENDERER) };
  })()`);
  assert.ok(report.drawCalls > 0);
  assert.equal(report.models, await evaluate('window.station2.manifest.totals.models'));
  const capture = async name => {
    await delay(600);
    const shot = await cdp('Page.captureScreenshot', { format: 'png' });
    await writeFile(new URL(name + '.png', output), Buffer.from(shot.data, 'base64'));
  };
  await evaluate('window.station2.overview()');
  await capture('overview');
  await evaluate('window.station2.ingame()');
  await capture('ingame');
  await evaluate("document.querySelector('#camera').value = 'BROADCASTINGCAMERA_06'; document.querySelector('#camera').onchange()");
  await capture('platform');
  // Exercise real mouse capture and camera movement, not just button presence.
  const before = await evaluate('window.station2.camera.position.toArray()');
  await cdp('Emulation.setFocusEmulationEnabled', { enabled: true });
  await cdp('Page.bringToFront');
  await cdp('Input.dispatchMouseEvent', { type: 'mousePressed', x: 720, y: 450, button: 'left', clickCount: 1 });
  await cdp('Input.dispatchMouseEvent', { type: 'mouseReleased', x: 720, y: 450, button: 'left', clickCount: 1 });
  await delay(200);
  assert.equal(await evaluate('window.station2.fly.isLocked'), true, 'Flying camera did not capture the mouse');
  await cdp('Input.dispatchKeyEvent', { type: 'keyDown', key: 'w', code: 'KeyW', windowsVirtualKeyCode: 87 });
  await delay(350);
  await cdp('Input.dispatchKeyEvent', { type: 'keyUp', key: 'w', code: 'KeyW', windowsVirtualKeyCode: 87 });
  const after = await evaluate('window.station2.camera.position.toArray()');
  assert.ok(Math.hypot(...after.map((n, i) => n - before[i])) > 20, 'W did not move the flying camera');
  const lowerY = after[1];
  await cdp('Input.dispatchKeyEvent', { type: 'keyDown', key: 'e', code: 'KeyE', windowsVirtualKeyCode: 69 });
  await delay(200);
  await cdp('Input.dispatchKeyEvent', { type: 'keyUp', key: 'e', code: 'KeyE', windowsVirtualKeyCode: 69 });
  assert.ok(await evaluate('window.station2.camera.position.y') > lowerY + 20, 'E did not fly upward');
  await evaluate('document.exitPointerLock()');
  await delay(100);
  assert.equal(await evaluate('window.station2.fly.isLocked'), false);
  report.flyingCamera = { mouseCapture: true, forward: true, vertical: true, release: true };

  // Also produce a portable native Three.js ObjectLoader model. Runtime animation
  // stays in the SCN bundle/loader; this standalone model is its time-zero pose.
  const native = await evaluate(`(() => {
    window.station2.update(0);
    window.station2.root.updateMatrixWorld(true);
    return JSON.stringify(window.station2.root.toJSON());
  })()`);
  await writeFile(new URL('../station2.three.json', output), native);
  const roundTrip = await evaluate(`(async () => {
    const THREE = await import('three');
    const json = await (await fetch('./Models/Maps/Station-2/station2.three.json')).json();
    const root = await new THREE.ObjectLoader().parseAsync(json);
    let models = 0, vertices = 0;
    root.traverse(n => { if (n.isMesh) { models++; vertices += n.geometry.attributes.position.count; } });
    return { models, vertices };
  })()`);
  assert.equal(roundTrip.models, report.models);
  assert.equal(roundTrip.vertices, report.vertices);
  report.nativeObjectLoader = roundTrip;
  assert.deepEqual(errors, [], 'Runtime/GLSL errors after camera changes');
  assert.deepEqual(failedRequests, [], 'Network failures after camera changes');
  report.browserErrors = errors; report.failedRequests = failedRequests;
  await writeFile(new URL('browser-report.json', output), JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify(report, null, 2));
});

