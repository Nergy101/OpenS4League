# SCN assets → Three.js

Station-2 and modular character conversion for a locally supplied Season-8 client ZIP.
Uses the existing `S4League.Scn` parser and Pfim 0.11.4. No archive decryption is needed:
the supported ZIP contains unpacked `Game/resources/` files. The source is read-only.
The shared parser, renderers, and servers are unchanged.

Every command below takes your own client ZIP as its first argument. Export its path once
and pass `"$S4_CLIENT_ZIP"`; neither the archive, the extracted assets, nor the generated
bundles belong in Git.

## Bulk female wardrobe

Use `--wardrobe` to produce the indexed, lazy-load female/unisex costume library
under `Client/Models/Characters/Wardrobe`. See [WARDROBE.md](WARDROBE.md) for the
commands, schema, source-anchored coverage, strict resolution rules, and failures.
Append `--verify` to compare every scene's own buffer and original skin data with
the source ZIP. `SceneExporter` and `ConversionAssets` are shared by all modes.

## Character conversion

Use the same executable with `--character <recipe.json>`:

```sh
dotnet run -c Release --project Tools/s4l-threejs-converter -- \
  "$S4_CLIENT_ZIP" \
  Client/Models/Characters/BasicFemale \
  --character Tools/s4l-threejs-converter/characters/female-basic.json
```

Append `--verify` to check the character bundle against the source, including original
weights and inverse binds. The recipe resolves actual item XML rather than guessing models
from item numbers. See [Client/CHARACTERS.md](../../Client/CHARACTERS.md) for the catalog,
future extension points, and `character-browser-smoke.mjs`, which also exports a native
Three.js model. Character assets are locally ignored, like map assets.

## Map conversion

One recipe per map lives in `Tools/s4l-threejs-converter/maps/` and names the map, the base
name of its bundle files, and the map configuration the client ships for it:

```json
{
  "name": "Neden-1",
  "bundle": "neden1",
  "config": "resources/mapinfo/bginfo-neden01.ini"
}
```

The 45 recipes are derived from the client's own roster, not typed by hand:
`xml/map.x7` lists every map with its configuration, player limit and region flags, and
`language/xml/gameinfo_string_table.x7` holds the `MAPNAME_*` names. Regenerate them with

```sh
python3 Tools/s4l-threejs-converter/scripts/propose-map-recipes.py --archive "$S4_CLIENT_ZIP" [--write]
```

which keeps one recipe per unique map name, preferring the region-enabled configuration with
the most players and no mode/variant token in its file name. Dev `Test`/`Random` entries and
the single-player `Licence`/`Tutorial`/`Training Center` names are skipped, so the set is the
roster's maps rather than its modes.

From the repository root, per map:

```sh
dotnet run -c Release --project Tools/s4l-threejs-converter -- \
  "$S4_CLIENT_ZIP" \
  Client/Models/Maps/Neden-1 \
  --map Tools/s4l-threejs-converter/maps/neden-1.json
```

`make threejs convert-assets` converts every recipe; `make threejs convert-assets neden-1`
converts one. A failing map no longer stops the batch: it is reported at the end and the
other maps still convert.

This writes `neden1.json`, `neden1.bin`, `map-config.json`, PNG textures, and
unmodified source dependencies. Re-running overwrites generated files. Missing
references are explicit in the manifest; ambiguous filenames and incompletely
consumed SCNs fail the conversion. A scene the client build does not ship at all is
recorded in `unresolved` and skipped — never substituted — and a map that resolves no
scene fails.

It also registers the map in `Client/Models/Maps/index.json`, the list the Three.js
map viewer reads (the preview server has no directory listing). Adding a map means
writing a recipe and converting it; nothing else has to know its files.

This client's `.tga` references often resolve to existing `.dds` files. The resolver
tries the full/context path first, then a unique basename, and only then the DDS
variant. Resolved aliases remain in the manifest.

## Verify source fidelity

```sh
dotnet run -c Release --project Tools/s4l-threejs-converter -- \
  "$S4_CLIENT_ZIP" \
  Client/Models/Maps/Station-2 \
  --map Tools/s4l-threejs-converter/maps/station-2.json --verify
python3 -m unittest discover -s Tools/s4l-threejs-converter/tests -v
npm --prefix Client ci
npm --prefix Client test
```

`--verify` does not write files. It compares every geometry channel bit-for-bit with
freshly parsed SCNs, plus matrices, parent names, animations, material ranges, and
SHA-256 hashes of preserved source files. The Python tests read the client ZIP from
`S4_CLIENT_ZIP` and skip the source-anchored checks when it is unset. They intentionally
require real local assets. `test_source.py` runs `--verify` for every registered map, so a
full suite run is slow (minutes) but covers the whole roster; `test_maps.py` checks the
recipe/bundle/registry contract and runs without the archive.

## Browser verification and standalone model

Start `npm --prefix Client start` in another terminal, then run:

```sh
node Client/tests/browser-smoke.mjs               # Station-2, plus its ObjectLoader export
node Client/tests/map-browser-smoke.mjs neden-1   # any other map, by id
node Client/tests/all-maps-browser-smoke.mjs      # every registered map, one Chrome run each
```

The tests start a temporary Chrome instance, render the real map, check shader/network
errors, exercise mouse capture and flying controls, and write screenshots plus
`browser-report.json` to `Client/Models/Maps/<Map>/verification/`. Station-2's test also
exports `station2.three.json` with embedded textures and verifies it through a real
`THREE.ObjectLoader` round trip.

The default browser path targets Chrome on macOS. Set `CHROME_PATH` on Windows/Linux.
Override the viewer URL with `VIEWER_URL`, or the local server port with `PORT`.
No browser automation packages are needed: Node uses the Chrome DevTools Protocol.

## Preservation and interpretation

- Positions, normals, tangents, both UV channels, and triangles retain their original
  precision. No scale normalization, simplification, atlasing, or mesh merging.
- Matrices are stored in System.Numerics field order, representing the corresponding
  column-major matrix in Three.js. The loader applies one Z reflection.
- The parser already converts V. PNGs retain decoder row order; Three.js uses
  `flipY=true`. No additional DDS row inversion from the old software preview is applied.
- Lightmaps use UV1. The viewer uses multiplicative MeshBasic materials, fog/depth/alpha/
  additive-blending flags, and original transform/alpha keys.
- Node identity is preserved even when names are duplicated.
- Game assets remain local and are ignored by `Client/Models/Maps/.gitignore`.

See [Client/README.md](../../Client/README.md) for explicit visual limitations and
unresolved effect references. This conversion does not claim to fully port the original
renderer, particle engine, or game rules.
