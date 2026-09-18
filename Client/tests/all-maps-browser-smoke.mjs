// Render every registered map in real WebGL and report per-map result.
// Usage: node tests/all-maps-browser-smoke.mjs   (needs `npm start` on VIEWER_URL)
import { mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import { withBrowser } from './browser-harness.mjs';

const origin = process.env.VIEWER_URL ?? 'http://127.0.0.1:8132';
const index = await (await fetch(new URL('Models/Maps/index.json', origin))).json();
const output = new URL('../Models/Maps/verification/', import.meta.url);
await mkdir(output, { recursive: true });

const report = { origin, maps: [], failures: [] };
for (const entry of index.maps) {
  try {
    await withBrowser(`${origin}/?map=${entry.id}`, 'window.mapReady === true', async ({ evaluate }) => {
      const rendered = await evaluate(`(() => {
        const m = window.s4map;
        let meshes = 0;
        m.root.traverse(n => { if (n.isMesh) meshes++; });
        return { id: m.entry.id, heading: document.querySelector('#map-name').textContent,
          drawCalls: m.renderer.info.render.calls, triangles: m.renderer.info.render.triangles,
          meshes, manifestMeshes: m.manifest.totals.models, menuItems: document.querySelectorAll('#map-list a').length };
      })()`);
      assert.equal(rendered.id, entry.id);
      assert.equal(rendered.heading, entry.name);
      assert.equal(rendered.meshes, rendered.manifestMeshes, 'Rendered mesh count differs from the manifest');
      assert.equal(rendered.menuItems, index.maps.length, 'The map menu does not list every registered map');
      assert.ok(rendered.drawCalls > 0 && rendered.triangles > 0, 'Nothing was drawn');
      report.maps.push({ id: entry.id, name: entry.name, drawCalls: rendered.drawCalls, triangles: rendered.triangles });
      console.log(`ok  ${entry.id.padEnd(14)} ${String(rendered.drawCalls).padStart(4)} draw calls  ${rendered.triangles} triangles`);
    });
  } catch (error) {
    report.failures.push({ id: entry.id, error: String(error.message).slice(0, 400) });
    console.log(`FAIL ${entry.id.padEnd(14)} ${String(error.message).slice(0, 200)}`);
  }
}

report.menuItems = index.maps.length;
await writeFile(new URL('browser-all-maps.json', output), JSON.stringify(report, null, 2) + '\n');
console.log(`\n${report.maps.length}/${index.maps.length} maps rendered; ${report.failures.length} failures`);
if (report.failures.length) process.exitCode = 1;
