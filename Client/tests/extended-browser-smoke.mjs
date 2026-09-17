import assert from 'node:assert/strict';
import { withBrowser } from './browser-harness.mjs';

const url = new URL('/character.html?basic=1&noAnimations=1', process.env.VIEWER_URL ?? 'http://127.0.0.1:8132').href;
await withBrowser(url, 'window.characterReady === true', async ({ evaluate, delay, errors }) => {
  await evaluate(`(() => {
    const v = window.characterViewer;
    const hair = v.assets.manifest.scenes.find(s => s.source.includes('/hair/')).nodes.find(n => n.geometry);
    const original = hair.details.bones[0];
    const vertex = original.Weight[0].Vertex;
    original.Weight[0].Weight = 0.2;
    for (let i = 1; i < 5; i++) {
      const bone = structuredClone(original); bone.Weight = [{ Vertex: vertex, Weight: 0.2 }];
      bone.Matrix.M41 += i; hair.details.bones.push(bone);
    }
    v.character.setBody('female');
  })()`);
  await delay(700);
  assert.deepEqual(errors, [], 'Extended skinning failed GLSL compilation/rendering');
  const result = await evaluate(`(() => {
    const v = window.characterViewer;
    const mesh = v.character.equipment.get('hair').parts[0].group.getObjectByProperty('isMesh', true);
    return { extended: mesh.userData.skinInfluences, renderCalls: v.renderer.info.render.calls,
      invalidPrograms: v.renderer.info.programs.filter(p => p.diagnostics && !p.diagnostics.runnable).length,
      shadowMaterial: !!mesh.customDepthMaterial };
  })()`);
  assert.equal(result.extended, 8); assert.equal(result.invalidPrograms, 0); assert.ok(result.renderCalls > 0); assert.equal(result.shadowMaterial, true);
  console.log(JSON.stringify({ fixture: 'synthetic five-influence regression', ...result }));
});
