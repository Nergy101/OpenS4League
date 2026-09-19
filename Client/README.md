# Client — Three.js asset viewers

Local conversion of the client's own map roster (45 maps) from the user-supplied Season-8
client ZIP (export `S4_CLIENT_ZIP` to its path); one recipe per map in
`../Tools/s4l-threejs-converter/maps/`, derived from `xml/map.x7`. No client data is committed.
No recreated geometry, new textures, or simplified replacement models. This map viewer is not a
game client and does not connect to the servers.

## Character viewer

The [character viewer](http://127.0.0.1:8132/character.html) has a searchable wardrobe
of 1,186 converted items for both shipped rigs (604 female, 601 male, 20 unisex shared), a Body
type selector, lazy Original/4× texture variants, local saved outfits, orbit controls,
and the original rest-pose rig. All 1,260 source wearable IDs are accounted for; 74 incomplete
entries have explicit unavailable reasons. Both rigs have their own source-backed animation pack
(30 clips each, from the client's own male and female libraries); a rig without a generated pack
says so instead of playing another rig's tracks. The small basic female viewer
remains at `character.html?basic=1`.
See [CHARACTERS.md](CHARACTERS.md) for conversion commands, model files, and the catalog/API
for future outfits, rigs, and animation clips. Both previews use the same `npm start` server.

## Map viewer and flying controls

List the converted maps, then open one:

```sh
make threejs map-viewer            # lists every converted map
make threejs map-viewer station-2  # starts the server and opens that map
make threejs map-viewer neden-1
```

Without the Makefile: `npm ci` and `npm start` in this directory, then open
http://127.0.0.1:8132/?map=station-2 (with no `map` parameter the first converted map
opens; an unknown id reports the available ones). **Maps** in the page header lists every
converted map in a scrollable panel with a search field (by name or id) and loads the one you
pick; the list is the registry, so it stays in step with what has actually been converted. The
server binds to loopback only. There are no CDN requests.

- Texture quality selects `Original (1×)` or `Enhanced (4×)`: the level is resolved per texture, so
  a map converted without generated levels simply has the 4× option disabled, and lightmaps and
  normal maps keep their decoded original because generated pixels are never substituted for
  semantic data. The choice is remembered as `opens4l.texture-quality.v1`.
- A map can also be re-encoded as AVIF/WebP: `make threejs-map-encode MAP=station-2 FORMAT=avif`
  writes colour/alpha `4×` levels as AVIF and registers the result as its own map entry
  (`Station-2 4x AVIF`), so the two encodings can be compared by switching maps in the **Maps** menu.
  The 1× originals and every lightmap/normal stay PNG.
- Click the map or **Fly** to capture the mouse.
- Mouse: look around. WASD: move; W/S follows the viewing direction.
- Q/E: down/up. Shift: faster. Esc: release the mouse.
- No gravity, collisions, or character controller. Fly through any part of the map.
- Overview, In-game view, and the original camera presets only reposition the camera.
  Overview is fitted to the map's own geometry; the in-game view and the camera presets
  come from the map's configuration.

## Branding

Both viewers carry the project's flat O4 crest: as favicon (`image/svg+xml`) and in the page
header next to the "OpenS4League" name, so it is obvious the previews belong to the project.
The files live in `Client/logos/` and are byte-identical copies of
`Website/static/logos/os4l-crest-flat-dark.svg` (`tests/branding.test.mjs` fails if they drift);
the dark-background variant is the one that reads on these pages. `serve.mjs` must declare
`.svg` as `image/svg+xml` — it sends `X-Content-Type-Options: nosniff`, so an SVG served as
`application/octet-stream` is refused as an image.

## Model files

Generated files live locally in `Models/Maps/<Map>/` and are git-ignored. A map's bundle
base name comes from its recipe (`station2` for Station-2, `neden1` for Neden-1):

| File | Contents |
| --- | --- |
| `<bundle>.three.json` | Standalone Three.js ObjectLoader model with embedded textures, in its time-zero pose (Station-2; written by the browser smoke test). |
| `<bundle>.json` + `<bundle>.bin` | Complete scene hierarchy, original float32 geometry, index buffers, materials, helpers, and animation data. |
| `textures/` | Original-resolution PNG conversions, including lightmaps and available enemy texture variants. |
| `map-config.json` | Readable map settings: fog, cameras, sectors, spawn configuration, and other settings. |
| `source/` | Unmodified source SCNs and resolved dependencies, including configuration, octree, audio, and effect sequence. |
| `verification/` | Real browser screenshots and verification report. |

`Models/Maps/index.json` (git-ignored, written by the converter) lists the converted maps
and names the files of each one; the viewer resolves a map through it and knows nothing
about individual maps. Adding a map is a recipe plus a conversion, not a viewer change.

For a static model, load `station2.three.json` with `THREE.ObjectLoader.loadAsync`.
For animation, call `loadMap(mapAssetUrl(entry, entry.manifest), entry.bundleFormat)` from
`src/MapLoader.js`, add `root` to your scene, and call `update(elapsedSeconds)`, where
`entry` comes from `resolveMapEntry` in `src/MapRegistry.js`. The loader requires Three.js 0.186.0.
Coordinates retain original S4 units; the loader reflects Z once for Three.js.

## What has been verified?

The map set is the client's own roster: 45 maps, one per unique map name (mode variants and the
dev/training entries are not converted — see `../Tools/s4l-threejs-converter/README.md`).

Every converted map:

- Source geometry compared channel-by-channel with freshly parsed SCNs by `--verify`, which also
  re-hashes every preserved source dependency
  (`Tools/s4l-threejs-converter/tests/test_source.py` runs it for every registered map).
- Rendered in real WebGL by `node tests/all-maps-browser-smoke.mjs`, which loads each map from the
  registry, checks the rendered mesh count against the manifest, confirms the page menu lists every
  map, and fails on any browser/shader error or failed asset request. Result:
  `Models/Maps/verification/browser-all-maps.json` (45/45).
- Registered in `Models/Maps/index.json`; `Tools/s4l-threejs-converter/tests/test_maps.py` and
  `tests/map-registry.test.mjs` check the recipe → bundle → registry contract.

Station-2 additionally, as the reference conversion:

- All nine SCNs from its map configuration's SKY/STATIC/DYNAMIC sections.
- 286 nodes, 245 meshes, 135,891 vertices, and 69,683 triangles, including preserved hidden helpers.
- 1,259,358 float32 values and 209,049 indices compared with freshly parsed source SCNs.
- Transforms, parent names, material ranges, render flags, and model/bone animations checked.
- Standalone model reloaded through Three.js ObjectLoader.

Neden-1 additionally: 459 nodes, 336 meshes, 109,241 vertices, 54,990 triangles, 1,023,546 float32
values and 164,970 indices compared with source SCNs.

## Limits of the 1:1 claim

The geometry is a direct conversion, but **pixel-perfect parity with the original
client has not been established**. No matching original-client capture is available.

- Lightmaps use UV1 and multiply the diffuse texture; this is not a PBR replacement.
  Three.js's internal 1/PI factor is compensated. The original gamma, lightmap gain,
  alpha-test, blend, and glow settings have not been fully reconstructed.
- Collision/occlusion meshes are preserved but hidden; textured collision meshes
  duplicate existing surfaces. The special full-scene-render-target mesh is also hidden.
- The preview shows the allied sector-effect variant rather than overlaying red and blue.
  Actual team/sector ownership, destruction, and particles require game/effect logic
  that is not simulated here. Original data remains available.
- The source configuration references `mapeffect_goalgate_green`, `noise.bmp`, and
  `Resources/Image/loading_death_station1.tga`, which could not be resolved in this ZIP.
  They are explicitly recorded in `station2.json:unresolved`; no invented substitutes were added.
- Neden-1 references `neden1_water_oct.scn` in `[STATIC]`, which this client build does not ship
  at all. It is skipped and recorded in `neden1.json:unresolved` (with `ds4_sky.tga` and
  `noise.bmp`); nothing was substituted, so that one scene is absent from the preview.
- Each map records its own unresolved references in its manifest (typically a `.tga`/`.bmp` this
  build stores only as `.dds`, or an effect identifier that is not a filename). A map's preview
  therefore shows what the build actually ships, and every gap is listed rather than filled in.
- The roster's mode variants (`ms_*`, `btc_*`, `_death`, `_pvp`, `_ffa`, `_sl`, `_ct`, `_surv`,
  captain, training, licence, tutorial and other dev maps) are **not** converted; the viewer shows
  the main map per name.
- Audio and the resolved `.seq` are preserved, not automatically played or ported to a particle engine.
- `station2.three.json` is a time-zero export; the bundled loader plays the moving
  arrows and pulsing barriers.

See [the conversion tool](../Tools/s4l-threejs-converter/README.md) for regeneration and tests.

## Original game client

The server protocol still targets the Windows client `S4ClientLocal.exe`, version
`0.8.32.26995`, ProudNet GUID `{beb92241-8333-4117-ab92-9b4af78c688f}`.
This Three.js viewer does not change that protocol. Game assets are not committed or published.
