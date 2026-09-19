import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile, readdir, stat } from 'node:fs/promises';

const crest = '../logos/os4l-crest-flat-dark.svg';
const pages = ['../index.html', '../character.html'];

test('both viewers use the project crest as favicon and in the header', async () => {
  for (const page of pages) {
    const html = await readFile(new URL(page, import.meta.url), 'utf8');
    assert.match(html, /<link rel="icon" type="image\/svg\+xml" href="\.\/logos\/os4l-crest-flat-dark\.svg">/,
      `${page} must use the OpenS4League flat crest as its favicon`);
    assert.ok(!html.includes('href="data:,"'), `${page} still carries the empty favicon`);
    assert.match(html, /<a id="brand"[^>]*>\s*<img src="\.\/logos\/os4l-crest-flat-dark\.svg"[^>]*>\s*<span>OpenS4League<\/span>/,
      `${page} must show the crest and project name so it reads as part of the project`);
  }
  assert.ok((await stat(new URL(crest, import.meta.url))).isFile());
});

test('the crest is the website asset, not a second copy of it', async () => {
  const [viewer, website] = await Promise.all([
    readFile(new URL(crest, import.meta.url)),
    readFile(new URL('../../Website/static/logos/os4l-crest-flat-dark.svg', import.meta.url)),
  ]);
  assert.ok(viewer.equals(website), 'Client/logos must stay byte-identical to the website logo');
  const files = await readdir(new URL('../logos/', import.meta.url));
  assert.deepEqual(files, ['os4l-crest-flat-dark.svg'], 'Keep one crest in Client/logos; the dark variant is the one that reads on this UI');
});

test('the preview server declares the image type', async () => {
  // Without this the viewer serves the SVG as octet-stream and nosniff makes browsers refuse it as an image.
  const server = await readFile(new URL('../serve.mjs', import.meta.url), 'utf8');
  assert.match(server, /'\.svg': 'image\/svg\+xml'/);
  // Same trap for the encoded texture levels: a bundle re-encoded as AVIF or WebP needs its own type.
  assert.match(server, /'\.avif': 'image\/avif'/, 'AVIF textures need the image type or nosniff silently refuses them');
  assert.match(server, /'\.webp': 'image\/webp'/, 'WebP textures need the image type or nosniff silently refuses them');
});

test('both viewers carry the same top-left main navigation', async () => {
  for (const [page, current, href] of [['../index.html', 'Maps', 'index'], ['../character.html', 'Characters', 'character']]) {
    const html = await readFile(new URL(page, import.meta.url), 'utf8');
    assert.match(html, /<nav id="main-nav" aria-label="OpenS4League previews">/, `${page} needs the app-level navigation`);
    assert.match(html, /<li><a href="\.\/index\.html"( aria-current="page")?>Maps<\/a><\/li>/, `${page} must link to the map viewer`);
    assert.match(html, /<li><a href="\.\/character\.html"( aria-current="page")?>Characters<\/a><\/li>/, `${page} must link to the character viewer`);
    assert.match(html, new RegExp(`<a href="\\./${href}\\.html" aria-current="page">${current}</a>`),
      `${page} must mark its own section as the current one`);
    assert.equal((html.match(/aria-current="page">/g) ?? []).length, 1, `${page} may mark exactly one section current`);
    assert.ok(!html.includes('id="menu"'), `${page} must not keep the ad-hoc "Menu ▾" dropdown`);
  }
});

test('page-specific selections are their own menus underneath the main navigation', async () => {
  const map = await readFile(new URL('../index.html', import.meta.url), 'utf8');
  assert.match(map, /<div class="controls">\s*<span class="label">Map<\/span>\s*<div class="menu">\s*<button id="maps"/,
    'The map roster is the map viewer\'s own menu, labelled under the main navigation');
  const character = await readFile(new URL('../character.html', import.meta.url), 'utf8');
  const rows = character.match(/<div class="page-menus">([\s\S]*?)<\/div>\n/g) ?? [];
  assert.equal(rows.length, 3, 'The character viewer has three menu rows under the main navigation, one control each');
  assert.match(rows[0], /<select id="body"/, 'Body type has its own row');
  assert.ok(!rows[0].includes('id="texture-quality"') && !rows[0].includes('id="lighting"'), 'Body type sits alone on its row');
  assert.match(rows[1], /<select id="texture-quality"/, 'Texture quality has its own row');
  assert.ok(!rows[1].includes('id="body"') && !rows[1].includes('id="lighting"'), 'Texture quality sits alone on its row');
  assert.match(rows[2], /<select id="lighting"/, 'Lighting has its own row below them');
  assert.equal((character.match(/id="body"/g) ?? []).length, 1, 'The body selector moved out of the sidebar, so it exists once');
  assert.equal((character.match(/id="texture-quality"/g) ?? []).length, 1, 'The quality selector moved out of the sidebar, so it exists once');
  // The stage header ignores pointer events for canvas drags, so these menus must opt back in.
  assert.match(character, /#main-nav, \.page-menus, \.page-menu, \.page-menu select, #texture-quality-status \{ pointer-events: auto; \}/,
    'The header menus must be clickable even though the header ignores pointer events');
});
