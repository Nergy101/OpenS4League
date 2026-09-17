import { mkdir, writeFile, readFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { withBrowser } from './browser-harness.mjs';

const base = new URL('/character.html?noAnimations=1', process.env.VIEWER_URL ?? 'http://127.0.0.1:8132').href;
const output = new URL('../Models/Characters/Wardrobe/verification/hair/', import.meta.url);
await mkdir(output, { recursive: true });
const items = [['1000034', 'Ponytail'], ['1000025', 'Fist Hair'], ['1000008', 'Double Tail']];
for (const [id, label] of items) {
  // A fresh viewer prevents cached geometry/poses from hiding an assembly regression.
  await withBrowser(base, 'window.characterReady === true', async ({ cdp, evaluate, errors, failedRequests }) => {
    const selected = await evaluate(`(async () => {
      const v = window.characterViewer;
      await v.selectEquipment('hair', '${id}');
      v.character.resetPose(); v.renderer.setAnimationLoop(null);
      v.controls.enableDamping = false;
      v.controls.target.copy(v.character.bones.get('Bip01 Head').getWorldPosition(v.controls.target));
      return v.character.equipment.get('hair').item.id;
    })()`);
    assert.equal(selected, id);
    const captures = [];
    for (const [direction, offset] of Object.entries({ front: [0, 0, 115], side: [115, 0, 0], back: [0, 0, -115] })) {
      const glError = await evaluate(`(() => {
        const v = window.characterViewer;
        v.camera.position.set(...${JSON.stringify(offset)}).add(v.controls.target);
        v.controls.update(); v.renderer.render(v.scene, v.camera);
        return v.renderer.getContext().getError();
      })()`);
      assert.equal(glError, 0, `${label}/${direction} WebGL error`);
      const capture = `${id}-${direction}.png`;
      const image = await cdp('Page.captureScreenshot', { format: 'png' });
      await writeFile(new URL(capture, output), Buffer.from(image.data, 'base64'));
      captures.push(capture);
    }
    assert.deepEqual(errors, [], `${label} browser/GLSL errors`);
    assert.deepEqual(failedRequests, [], `${label} missing assets`);
    await writeFile(new URL(`${id}.json`, output), JSON.stringify({ id, label, captures, errors, failedRequests }, null, 2));
  });
}
const reports = await Promise.all(items.map(async ([id]) => JSON.parse(await readFile(new URL(`${id}.json`, output)))));
assert.equal(reports.length, items.length);
assert.equal(new Set(reports.flatMap(report => report.captures)).size, items.length * 3);
console.log(JSON.stringify({ checked: reports.length, reports }, null, 2));
