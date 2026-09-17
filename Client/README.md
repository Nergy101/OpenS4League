# Client — Three.js asset viewers

Local conversion of the original Station-2 map from the user-supplied Season-8 client ZIP
(export `S4_CLIENT_ZIP` to its path). No client data is committed. No recreated geometry, new
textures, or simplified replacement models. This map viewer is not a game client
and does not connect to the servers.

## Character viewer

The female [character viewer](http://127.0.0.1:8132/character.html) has a searchable wardrobe
of 604 converted items, lazy Original/2×/4× texture variants, local saved outfits, orbit controls,
and the original rest-pose rig. All 646 source wearable IDs are accounted for; 42 incomplete
entries have explicit unavailable reasons. The small basic viewer remains at `character.html?basic=1`.
See [CHARACTERS.md](CHARACTERS.md) for conversion commands, model files, and the catalog/API
for future outfits, male rigs, and animation clips. Both previews use the same `npm start` server.

## Station-2 preview and flying controls

Run `npm ci`, then `npm start` in this directory. Open http://127.0.0.1:8132.
The server binds to loopback only. There are no CDN requests.

- Click the map or **Fly** to capture the mouse.
- Mouse: look around. WASD: move; W/S follows the viewing direction.
- Q/E: down/up. Shift: faster. Esc: release the mouse.
- No gravity, collisions, or character controller. Fly through any part of the map.
- Overview, In-game view, and the original camera presets only reposition the camera.

## Model files

Generated files live locally in `Models/Maps/Station-2/` and are git-ignored:

| File | Contents |
| --- | --- |
| `station2.three.json` | Standalone Three.js ObjectLoader model with embedded textures, in its time-zero pose. |
| `station2.json` + `station2.bin` | Complete scene hierarchy, original float32 geometry, index buffers, materials, helpers, and animation data. |
| `textures/` | Original-resolution PNG conversions, including lightmaps and available enemy texture variants. |
| `map-config.json` | Readable map settings: fog, cameras, sectors, spawn configuration, and other settings. |
| `source/` | Unmodified source SCNs and resolved dependencies, including configuration, octree, audio, and effect sequence. |
| `verification/` | Real browser screenshots and verification report. |

For a static model, load `station2.three.json` with `THREE.ObjectLoader.loadAsync`.
For animation, use `loadStation2` from `src/Station2Loader.js`, add `root` to your
scene, and call `update(elapsedSeconds)`. The loader requires Three.js 0.186.0.
Coordinates retain original S4 units; the loader reflects Z once for Three.js.

## What has been verified?

- All nine SCNs from the original map configuration's SKY/STATIC/DYNAMIC sections.
- 286 nodes, 245 meshes, 135,891 vertices, and 69,683 triangles, including preserved hidden helpers.
- 1,259,358 float32 values and 209,049 indices compared with freshly parsed source SCNs.
- Transforms, parent names, material ranges, render flags, and model/bone animations checked.
- 53 textures decoded; 71 source files checked against the ZIP using SHA-256.
- Real WebGL rendering with no browser/shader errors or failed asset requests.
- Mouse capture, forward flight, vertical movement, and mouse release exercised in the browser.
- Standalone model reloaded through Three.js ObjectLoader.

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
- Audio and the resolved `.seq` are preserved, not automatically played or ported to a particle engine.
- `station2.three.json` is a time-zero export; the bundled loader plays the moving
  arrows and pulsing barriers.

See [the conversion tool](../Tools/s4l-threejs-converter/README.md) for regeneration and tests.

## Original game client

The server protocol still targets the Windows client `S4ClientLocal.exe`, version
`0.8.32.26995`, ProudNet GUID `{beb92241-8333-4117-ab92-9b4af78c688f}`.
This Three.js viewer does not change that protocol. Game assets are not committed or published.
