import { mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { withBrowser } from './browser-harness.mjs';

const url = new URL('/character.html', process.env.VIEWER_URL ?? 'http://127.0.0.1:8132').href;
const output = new URL('../Models/Characters/Animations/Female/verification/', import.meta.url);
await mkdir(output, { recursive: true });
await withBrowser(url, 'window.characterReady === true && window.characterViewer.animationReady === true && window.characterViewer.character.animationId === "idle"', async ({ cdp, evaluate, delay, errors, failedRequests }) => {
  assert.equal(await evaluate('window.characterViewer.character.animationId'), 'idle');
  assert.equal(await evaluate('window.characterViewer.character.animationSpeed'), 4.5);
  assert.equal(await evaluate('window.characterViewer.character.animations.size'), 1, 'Default idle should load exactly one clip');
  const descriptors = await evaluate('window.characterViewer.animationDefinitions');
  const labels = await evaluate("[...document.querySelector('#animation').options].map(option => option.textContent).join(' ')");
  for (const required of [/idle/i, /walk/i, /wave|greet|hello/i, /cry/i, /rock/i, /paper/i, /scissor/i]) assert.match(labels, required);
  const report = [];
  for (const descriptor of descriptors) {
    const result = await evaluate(`(async () => {
      const v = window.characterViewer, model = v.character;
      const select = document.querySelector('#animation'); select.value = ${JSON.stringify(descriptor.id)};
      select.dispatchEvent(new Event('change')); await v.whenIdle();
      if (model.animationId !== ${JSON.stringify(descriptor.id)}) throw new Error('Animation selection failed: ' + document.querySelector('#status').textContent);
      if (model.activeAction.getClip() !== model.animations.get(model.animationId)) throw new Error('Mixer reused the previous animation');
      model.setAnimationPaused(true);
      const THREE = await import('three');
      const clip = model.animations.get(model.animationId);
      for (const track of clip.tracks) {
        const parsed = THREE.PropertyBinding.parseTrackName(track.name);
        const target = THREE.PropertyBinding.findNode(model.root, parsed.nodeName);
        if (!target?.isBone) throw new Error('Track does not bind to a real rig bone: ' + track.name);
      }
      const samples = [];
      for (const fraction of [0, 0.25, 0.5, 0.75, 0.99]) {
        model.seekAnimation(clip.duration * fraction);
        const box = v.bounds();
        const finite = [...box.min.toArray(), ...box.max.toArray()].every(Number.isFinite);
        if (!finite) throw new Error('Nonfinite animated bounds');
        if (box.max.y - box.min.y > 1000) throw new Error('Exploded animation hierarchy');
        samples.push({ fraction, bounds: {min:box.min.toArray(),max:box.max.toArray()},
          leftHand:model.bones.get('Bip01 L Hand').getWorldPosition(new THREE.Vector3()).toArray(),
          rightHand:model.bones.get('Bip01 R Hand').getWorldPosition(new THREE.Vector3()).toArray(),
          root:model.bones.get('Bip01').getWorldPosition(new THREE.Vector3()).toArray() });
      }
      model.seekAnimation(clip.duration * 0.4);
      return { id:model.animationId, duration:clip.duration, tracks:clip.tracks.length, samples };
    })()`);
    await delay(150);
    const screenshot = await cdp('Page.captureScreenshot', { format: 'png' });
    await writeFile(new URL(descriptor.id.replace(/[^a-z0-9_-]/gi, '_') + '.png', output), Buffer.from(screenshot.data, 'base64'));
    report.push({ ...result, label: descriptor.label, sourceClip: descriptor.sourceClip });
    await writeFile(new URL('browser-samples.json', output), JSON.stringify(report, null, 2) + '\n');
    assert.deepEqual(errors, [], `Browser errors in ${descriptor.id}`);
  }
  // Real UI playback controls and keeping a clip active through a wardrobe swap.
  const walk = descriptors.find(d => /walk/i.test(d.label));
  await evaluate(`window.characterViewer.selectAnimation(${JSON.stringify(walk.id)})`);
  await evaluate("document.querySelector('#animation-play').click()");
  assert.equal(await evaluate('window.characterViewer.character.animationPaused'), true);
  await evaluate("document.querySelector('#animation-time').value = window.characterViewer.character.animationDuration / 2; document.querySelector('#animation-time').dispatchEvent(new Event('input'))");
  const time = await evaluate('window.characterViewer.character.animationTime');
  assert.ok(time > 0);
  await evaluate("document.querySelector('#animation-speed').value = '10'; document.querySelector('#animation-speed').dispatchEvent(new Event('change'))");
  assert.equal(await evaluate('window.characterViewer.character.animationSpeed'), 10);
  await evaluate("const toggle = document.querySelector('#animation-loop'); toggle.checked = !toggle.checked; toggle.dispatchEvent(new Event('change'))");
  assert.equal(await evaluate('window.characterViewer.character.activeAction.loop === 2200'), true);
  await evaluate("document.querySelector('#animation-loop').click()");
  assert.equal(await evaluate('window.characterViewer.character.activeAction.loop === 2201'), true);
  await evaluate("document.querySelector('#animation-play').click()");
  await delay(200);
  await evaluate("window.characterViewer.selectEquipment('shirt', '1020058')");
  assert.equal(await evaluate('window.characterViewer.character.animationId'), walk.id);
  await delay(200);
  assert.deepEqual(errors, [], 'Animating five-influence clothing failed');
  await evaluate("window.characterViewer.selectAnimation('rest')");
  assert.equal(await evaluate('window.characterViewer.character.animationId'), 'rest');
  assert.equal(await evaluate("document.querySelector('#animation-play').disabled"), true);
  assert.deepEqual(errors, []); assert.deepEqual(failedRequests, []);
  assert.equal(report.length, descriptors.length);
  await writeFile(new URL('browser-summary.json', output), JSON.stringify({ clips:report.length, defaultPose:'rest', controls:true, wardrobeSwap:true, browserErrors:errors, failedRequests }, null, 2));
  console.log(JSON.stringify({ clips:report.map(c => ({id:c.id,label:c.label,duration:c.duration,tracks:c.tracks})), defaultPose:'rest', controls:true, wardrobeSwap:true, browserErrors:errors, failedRequests }, null, 2));
});
