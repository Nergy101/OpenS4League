# SCN assets → Three.js

Station-2 and modular character conversion for a locally supplied Season-8 client ZIP.
Uses the existing `S4League.Scn` parser and Pfim 0.11.4. No archive decryption is needed:
the supported ZIP contains unpacked `Game/resources/` files. The source is read-only.
The shared parser, renderers, and servers are unchanged.

Every command below takes your own client ZIP as its first argument. Export its path once
and pass `"$S4_CLIENT_ZIP"`; neither the archive, the extracted assets, nor the generated
bundles belong in Git.

## Bulk wardrobe (both rigs)

Use `--wardrobe` to produce the indexed, lazy-load female and male costume library
under `Client/Models/Characters/Wardrobe`, with `1x` decoded originals plus optional
`4x` variants (`make threejs-asset-upscale` regenerates the enhanced levels). Scene dumps are
written compact, not indented: they carry the mesh data and are the bulk of the bundle (1117 files,
970 MB indented against 348 MB compact), while the manifests stay readable because they are small and
they are the format's contract. See
[WARDROBE.md](WARDROBE.md) for the
commands, schema, source-anchored coverage, strict resolution rules, and failures.
Append `--verify` to compare every scene's own buffer and original skin data with
the source ZIP. `SceneExporter` and `ConversionAssets` are shared by all modes.

## Animation packs (both rigs)

Use `--animations --rig female|male` to export one rig's original clips from that rig's own
SCN libraries under `Client/Models/Characters/Animations/<Rig>`. See
[ANIMATIONS.md](ANIMATIONS.md) for the mapping evidence, the bytecode reader, the playback
contract, and the per-rig verification results.

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

### Map texture levels

`--texture-quality 1x,4x` gives a map the same variant contract the wardrobe uses:
every texture records `kind` and one entry per level, `1x` being the decoded original.
`kind` is semantic and decides what may be generated: `lightmap` comes from the texture's
use in a material's lightMap slot (baked lighting) and `normal` from the `_n`/`normal`
name pattern, and neither goes through a generative model — their 4× level is a
deterministic resize, and the ESRGAN pass never lists them.

```sh
make threejs-map-upscale MAP=station-2   # convert the map, then its 4x colour/alpha pass
```

`scripts/upscale-assets.py` drives that target exactly as it drives the wardrobe one
(same phases, same log, same resume behaviour) and `scripts/prune-texture-levels.py`
removes a level — files and manifest entries together — from a bundle converted before
this policy, plus any texture file no variant references any more.

### The GPU the pass runs on

The pass's speed is the GPU, and pip's default torch wheel has none on Windows and Linux: it is the
CPU build, so a laptop with an NVIDIA card would upscale on one core and look merely slow. The driver
therefore installs torch from the CUDA index (`cu124`, or `--torch-index-url` / `TORCH_INDEX=…` for an
older driver, e.g. `cu121`) when the driver reports an NVIDIA GPU, and leaves macOS alone — its default
wheel already carries MPS. The torch step is not behind the `.installed` marker, so a venv that was
bootstrapped on the CPU wheel is repaired on the next run instead of staying slow forever.

```sh
make threejs-avif-all DRY_RUN=1        # then the real run
make threejs-map-upscale MAP=station-2 TORCH_INDEX=https://download.pytorch.org/whl/cu121
python3 Tools/s4l-threejs-converter/scripts/upscale-assets.py --print-plan   # what this machine would install
```

`--print-plan` needs no client archive, and reports the wheel the detected platform would use; set
`OPENS4L_PLATFORM`/`OPENS4L_CUDA` to preview another machine's plan.

An **encoding is not a level**. `make threejs-map-encode MAP=<map> [FORMAT=avif|webp] [IN_PLACE=1]`
(`scripts/encode-texture-levels.py`) re-encodes a bundle's generated `4x` files. Colour and alpha use
`--quality` (default 60); **lightmaps are encoded too**, at their own `--lightmap-quality` (default
90), because their values multiply the lighting — a lossy lightmap shows as blotchy light across a
across a surface rather than a shifted pixel (measured on the worst lightmap: 49.1 dB visible RGB, 49/255 max
channel delta at q90). A **normal map is not encoded unless asked for**: it holds a vector, so its error
is an angle rather than a pixel — AVIF q90 moves the decoded normal by 0.54° on average and 3.4° at the
worst 1-in-1000 pixel, against the ~0.3° an 8-bit normal already carries — which is why the bulk targets
name it explicitly (`--kinds color,alpha,lightmap,normal`, `--quality-normal`, default 90) and a manual
`threejs-map-encode` leaves it alone. By default the encoded level lands in a sibling bundle registered as **its
own map entry** (`Station-2 4x AVIF`), so the PNG and encoded versions can be compared in the viewer
by switching entries; `IN_PLACE=1` rewrites the bundle itself instead. The clone is copy-on-write, the
replaced PNGs of that level are removed, and each variant records `codec`, `quality` and
`encodedFromSha256` (the PNG it came from). Measured on Station-2: colour `4x` 104.1 MB of PNG →
**2.9 MB of AVIF q60** (35.7×), at 35.2 dB worst-file visible PSNR and 42.4 dB on the rendered frame.
`--quality` 80 or 90 trades size back for fidelity (5.5 MB / 9.6 MB); `avifenc` and `cwebp`
are the encoders (`brew install libavif webp`).

A re-run is a **no-op**, not a second encoding: a level that already is the target format is skipped,
because encoding an encoded file re-compresses it and loses quality.

### The whole library, in one run

```sh
make threejs-avif-all                              # wardrobe + every registered map
make threejs-avif-all ONLY=maps MAP=station-2      # just this map
make threejs-avif-all DRY_RUN=1                    # report what would run, change nothing
```

`scripts/avif-pass-all.py` runs both steps per asset: the Real-ESRGAN `4x` pass where the generated
level is missing **or incomplete** (a killed pass leaves some textures behind while the manifest
already claims `1x,4x`), then the in-place encode described above. It skips a bundle whose levels are
already encoded, so it is re-runnable and finishes an interrupted run; a map that fails is reported and
the run continues with the next one. Per-asset logs go to `LOG` (default
`/tmp/opens4l-avif-pass.log`), and the totals are rewritten to `REPORT` (default
`/tmp/opens4l-avif-pass.json`) after every asset, so a stopped run still leaves its numbers. A map
takes minutes to twenty (its `4x` pass dominates) and the roster is hours. One pass at a time: two runs
writing the same bundle tear its manifest.

Encoded textures need their MIME type: `Client/serve.mjs` declares `image/avif` and
`image/webp` because it sends `X-Content-Type-Options: nosniff`, which makes a browser
silently refuse an image served as `application/octet-stream`.

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
