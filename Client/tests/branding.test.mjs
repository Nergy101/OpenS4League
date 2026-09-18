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
});
