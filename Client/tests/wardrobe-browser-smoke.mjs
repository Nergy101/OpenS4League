import { mkdir, readFile, writeFile, appendFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { withBrowser } from './browser-harness.mjs';

const base = new URL('/character.html?noAnimations=1', process.env.VIEWER_URL ?? 'http://127.0.0.1:8132').href;
const output = new URL('../Models/Characters/Wardrobe/verification/', import.meta.url);
await mkdir(output, { recursive: true });
const journal = new URL('browser-items.jsonl', output);
if (!process.argv.includes('--resume')) await writeFile(journal, '');
const previous = (await readFile(journal, 'utf8').catch(() => '')).trim().split('\n').filter(Boolean).map(line => JSON.parse(line));
const already = new Set(previous.filter(row => row.status === 'passed').map(row => `${row.body ?? 'female'}/${row.item}/${row.variant}`));
await withBrowser(base, 'window.characterReady === true', async ({ cdp, evaluate, delay, errors, failedRequests }) => {
  const initial = await evaluate(`(() => {
    const v = window.characterViewer;
    return { items: v.character.body.items.length, inventory: v.library.index.inventory.length,
      converted: v.library.index.inventory.filter(i => i.status === 'converted').length,
      unavailable: v.library.index.inventory.filter(i => i.status === 'unavailable').length,
      bodies: v.library.index.catalog.bodies.map(body => ({ id: body.id, items: body.items.length })),
      cachedScenes: v.library.sceneBuffers.size, cachedTextures: v.library.textures.size,
      allScenes: Object.keys(v.library.index.scenes).length, allTextures: Object.keys(v.library.index.textures).length };
  })()`);
  assert.ok(initial.items > 500);
  assert.deepEqual(initial.bodies.map(body => body.id), ['female', 'male']);
  assert.equal(initial.inventory, initial.converted + initial.unavailable, 'Every inventory row is converted or has a reason');
  assert.ok(initial.cachedScenes < initial.allScenes / 10, 'Scenes loaded eagerly');
  assert.ok(initial.cachedTextures < initial.allTextures / 10, 'Textures loaded eagerly');
  await delay(300);
  const capture = async name => {
    const image = await cdp('Page.captureScreenshot', { format: 'png' });
    await writeFile(new URL(name + '.png', output), Buffer.from(image.data, 'base64'));
  };
  await capture('wardrobe');
  const allJobs = await evaluate('window.characterViewer.assets.manifest.catalog.bodies.flatMap(body => body.items.flatMap(item => item.variants.map(variant => ({body:body.id,item:item.id,slot:item.slot,variant:variant.id}))))');
  let jobs = allJobs.filter(job => !already.has(`${job.body}/${job.item}/${job.variant}`));
  if (process.env.WARDROBE_BROWSER_LIMIT) jobs = jobs.slice(0, Number(process.env.WARDROBE_BROWSER_LIMIT));
  // Low-resolution real WebGL rendering keeps exhaustive validation practical.
  // Every case is drawn and read back. Original-resolution screenshots follow.
  await evaluate(`(() => {
    const v = window.characterViewer;
    v.renderer.setAnimationLoop(null); v.renderer.setPixelRatio(1); v.renderer.setSize(480,480,false);
    v.renderer.shadowMap.enabled = false;
    window.validationBody = null; window.validationSlot = null;
  })()`);
  for (let start = 0; start < jobs.length; start += 8) {
    const batch = jobs.slice(start, start + 8);
    const rows = await evaluate(`(async () => {
      const v = window.characterViewer; const rows = [];
      for (const job of ${JSON.stringify(batch)}) {
        try {
          if (window.validationBody !== job.body) { await v.library.setBody(job.body); window.validationBody = job.body; window.validationSlot = null; }
          if (window.validationSlot !== job.slot) { window.validationSlot = job.slot; }
          await v.library.equip(job.slot, job.item, job.variant);
          v.character.root.updateMatrixWorld(true);
          v.renderer.render(v.scene, v.camera);
          const gl = v.renderer.getContext(); const pixel = new Uint8Array(4);
          gl.readPixels(240,240,1,1,gl.RGBA,gl.UNSIGNED_BYTE,pixel);
          if (gl.getError() !== gl.NO_ERROR) throw new Error('WebGL error');
          if (v.renderer.info.programs.some(p => p.diagnostics && !p.diagnostics.runnable)) throw new Error('Shader compilation failed');
          const s = v.character.equipment.get(job.slot);
          if (s.item.id !== job.item || s.variant.id !== job.variant) throw new Error('Wrong equipped item');
          const maps = [];
          for (const part of s.parts) part.group.traverse(n => { if (n.isMesh) for (const m of n.material) if (m.map) maps.push(m.map.name); });
          for (const path of Object.values(s.variant.maps)) if (!maps.includes(path)) throw new Error('Unbound variant texture ' + path);
          rows.push({...job,status:'passed',scenes:v.library.sceneBuffers.size,textures:v.library.textures.size});
        } catch (error) { rows.push({...job,status:'failed',reason:error.message}); }
      }
      return rows;
    })()`);
    await appendFile(journal, rows.map(row => JSON.stringify(row)).join('\n') + '\n');
    if (start % 80 === 0) console.log(`WebGL checked ${Math.min(start + batch.length, jobs.length)}/${jobs.length}`);
  }
  await evaluate(`(async () => {
    const v = window.characterViewer;
    await v.library.setBody('female');
    v.renderer.shadowMap.enabled = true; v.renderer.setPixelRatio(Math.min(devicePixelRatio,2));
    const stage = document.querySelector('#stage'); v.renderer.setSize(stage.clientWidth,stage.clientHeight);
    v.renderer.setAnimationLoop(() => v.renderer.render(v.scene,v.camera));
    document.querySelector('#reset-outfit').click(); await v.whenIdle();
  })()`);
  // Source five-influence garments also render with real shadows enabled.
  for (const id of ['1020053', '1020058', '1021070']) {
    await evaluate(`window.characterViewer.selectEquipment('shirt', '${id}')`);
    await delay(200);
    assert.deepEqual(errors, [], `Errors rendering extended skinning item ${id}`);
  }
  await capture('extended-skinning');
  await evaluate(`(async () => {
    const v = window.characterViewer;
    document.querySelector('#equipment-search').value = '1020058';
    document.querySelector('#equipment-search').dispatchEvent(new Event('input'));
    const select = document.querySelector('#slot-shirt'); select.value = '1020058'; select.dispatchEvent(new Event('change'));
    await v.whenIdle();
    if (v.character.equipment.get('shirt').item.id !== '1020058') throw new Error('Search/equip UI failed');
    document.querySelector('#outfit-name').value = 'Full wardrobe test'; document.querySelector('#save-outfit').click();
  })()`);
  await cdp('Page.reload');
  for (let i = 0; i < 150; i++) { if (await evaluate('window.characterReady === true')) break; await delay(100); }
  await evaluate(`(async () => {
    const v = window.characterViewer;
    const select = document.querySelector('#saved-outfits'); select.selectedIndex = 1; select.dispatchEvent(new Event('change'));
    document.querySelector('#apply-outfit').click(); await v.whenIdle();
    if (v.character.equipment.get('shirt').item.id !== '1020058') throw new Error('Saved wardrobe outfit was not restored after reload');
  })()`);
  await capture('saved-outfit');
  const rows = (await readFile(journal, 'utf8')).trim().split('\n').filter(Boolean).map(line => JSON.parse(line));
  const latest = [...new Map(rows.map(row => [`${row.body ?? 'female'}/${row.item}/${row.variant}`, row])).values()];
  const report = { initial, expectedVariants: allJobs.length, checkedVariants: latest.length,
    complete: latest.length === allJobs.length,
    failures: latest.filter(row => row.status !== 'passed'),
    maxResidentScenes: Math.max(...latest.map(row => row.scenes ?? 0)),
    maxResidentTextures: Math.max(...latest.map(row => row.textures ?? 0)),
    savedOutfitReload: true, searchUi: true, browserErrors: errors, failedRequests };
  await writeFile(new URL('browser-summary.json', output), JSON.stringify(report, null, 2) + '\n');
  assert.equal(report.failures.length, 0, JSON.stringify(report.failures));
  assert.deepEqual(errors, []); assert.deepEqual(failedRequests, []);
  if (!process.env.WARDROBE_BROWSER_LIMIT) assert.equal(report.complete, true);
  console.log(JSON.stringify(report, null, 2));
});
