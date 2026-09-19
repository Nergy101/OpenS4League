# Character viewer

Open http://127.0.0.1:8132/character.html after running `npm start` in `Client/`.
Uses the same server, Three.js installation, PNG decoder, binary geometry format,
material utilities, and browser-test harness as Station-2.

## Full wardrobe

The default viewer loads the indexed wardrobe for **both shipped rigs**: **1,186 converted
items**, **4,331 item/skin choices**, and **3,830 textures**, from 1,260 wearable IDs in the
local archive (626 female, 614 male, 20 unisex). The 74 remaining IDs are listed with explicit
reasons: items without renderable references, with genuinely absent scenes, missing required
textures, or ambiguous texture references. Pet-category companions and weapons are outside this
wardrobe pass.

Switch rigs with the Body type selector. The female body carries 604 items and the male body
601; unisex accessories belong to both, and each rig keeps its own skeleton and client defaults.

Search by item name or ID. Items and skins load on demand; unused source buffers and
textures are released after changes. Startup needs only the current rig/outfit (seven
scenes and six textures), not the entire library. Accessories are optional and support None.
The Unavailable source items panel shows incomplete entries instead of silently hiding them.

The index, per-scene JSON/bin files, decoded textures, preserved originals, and coverage
report are in `Client/Models/Characters/Wardrobe/`. See
[the bulk converter documentation](../Tools/s4l-threejs-converter/WARDROBE.md) for
source-anchored coverage, exact failure reasons, and regeneration commands.

### Saved outfits

Use Saved outfits to save/update a named outfit, apply it, or delete it. Reusing a name
updates that entry. Presets survive reloads in the same browser and origin, under
`opens4l.character.outfits.v1`. They contain only body/item/skin IDs, not model data,
textures, camera settings, or animation state. Different hosts/ports have separate storage.

Applying an outfit validates every item and skin and preloads its assets before replacing
the current selection. Missing or incompatible saved IDs report an error without partially
changing the character. Applying an outfit restores the rest pose. Presets are not synced
or uploaded anywhere; clearing site data removes them.

## Basic outfit

The female character is assembled in its original BASE/rest T-pose from the default
items in the supplied client's `xml/default_item.x7`, resolved through `xml/item.x7`:

| Slot | Item ID | Source model |
| --- | --- | --- |
| Hair | `1000002` | `hair/00_female_hair.scn` |
| Face | `1010001` | `face/00_female_face.scn` |
| Shirt | `1020001` | `body/30_female_body.scn` |
| Pants | `1030001` | `leg/30_female_leg.scn` |
| Gloves/hands | `1040001` | `hand/00_female_hand.scn` |
| Shoes | `1050001` | `foot/00_female_foot.scn` |

The male rig uses the same file's `<male>` group, resolved the same way:

| Slot | Item ID | Source model |
| --- | --- | --- |
| Hair | `1000001` | `hair/01_male_hair.scn` |
| Face | `1010003` | `face/00_male_face.scn` |
| Shirt | `1020002` | `body/26_male_body.scn` |
| Pants | `1030002` | `leg/26_male_leg.scn` |
| Gloves/hands | `1040002` | `hand/00_male_hand.scn` |
| Shoes | `1050002` | `foot/00_male_foot.scn` |

Both rigs come from `resources/model/character/{female,male}_bip.scn` (82 bones for the female
rig; the male rig is its own source skeleton).

The source ZIP replaces several of these named files with meshes/textures named
`48_female_*`. The conversion follows their actual contents instead of guessing from
filenames. The English item labels are copied from the client, including its spelling.
“Basic W Hand” is the hand-slot item, not a promise of opaque gloves.

The assembled character has eight equipment meshes, six of them skinned, with 1,621
vertices and a full 82-bone rig. Hair and face also keep their private attachment nodes.
The original small bundle remains available at `character.html?basic=1`, with 15 decoded
textures and 13 item/variant choices. It is retained as a lightweight reference/regression
fixture; the default URL now uses the full indexed wardrobe.

### Attachment rest transforms

Attached SCNs can mix rigid scalp meshes with skinned tails. Their stored node matrices
may be scene-space binds, while the source `base` animation contains parent-local rest
TRS. `CharacterModel` uses that authored local pose for attached bones and rigid meshes,
falling back to the original node matrix when no base channel exists. Applying scene-space
matrices as locals rotates Ponytail through the head and lifts Fist Hair/Double Tail scalps.
Do not correct this with per-item offsets or a global inverse-parent transform: older
assets, including faces, already have local node matrices.

Raw vertices and authored inverse binds stay unchanged. Regression checks use the actual
three hair assets, including head rotation and actor transforms. With the viewer running,
`node Client/tests/hair-browser-smoke.mjs` captures fresh front/side/back views under
`Models/Characters/Wardrobe/verification/hair/` and checks browser/GLSL/asset errors.

## Controls

- Drag to orbit, scroll to zoom, right-drag to pan.
- Front, Side, Back, and Frame character reposition the inspection camera.
- Equipment and Skin selectors are populated from the catalog.
- Reset outfit restores the body's default items and the selected texture quality.
- Lighting selects one of five preview environments — Studio (the original rig), Daylight, Sunset, Night, and Showroom — changing the hemisphere/key/rim lights, the stage background and the floor shadow strength only; no asset data is touched. The choice is stored as `opens4l.lighting.v1`.
- Texture quality selects `Original (1×)` or `Enhanced (4×)` when a generated variant exists; a missing level falls back to the highest one at or below the request. The preference is stored as `opens4l.texture-quality.v1`; saved outfits still contain IDs only.

### Texture quality and provenance

The original decoded PNG is always the `1x` variant and remains the default. The optional `4x` PNGs are generated offline — deterministically at first (Lanczos-style resampling), then upgraded for colour/alpha with Real-ESRGAN — and they are not source-authentic detail: they do not alter geometry, UVs, skinning, animation data, or shader semantics. The index records each variant's `algorithm`, source and generated dimensions, and SHA-256 hashes. Runtime loading is lazy and falls back downward (4x → 1x, or 2x in a bundle that still records it), exposing requested versus loaded quality in the viewer.

Color and alpha maps retain sRGB semantics. Normal maps are tagged `normal` and use `NoColorSpace`; lightmaps remain separate and use clamp wrapping and UV1. They must not be processed as color data. A 4× texture has roughly 16× the uncompressed pixel memory of 1×, so a device texture-size limit can trigger fallback.

To generate variants, use `--texture-quality 1x,4x --upscale-algorithm deterministic-bilinear`. Generated files and manifests remain in ignored local asset directories; the source archive and existing 1× assets are never overwritten. A variant that another pass generated (the `realesrgan-…` upscaling) is preserved with its file and provenance when the wardrobe is converted again, and a level that is already on disk is reused rather than re-encoded, so `make threejs-asset-upscale` resumes instead of starting over. That target upgrades the colour and alpha levels with the **Real-ESRGAN x4plus** model applied to the decoded source, and records that model as the level's `algorithm`. Only `1x` and `4x` exist: an `8x` level was generated once and looked worse than the 4× one, so it is no longer produced, and `2x` was dropped as an in-between level. `scripts/prune-texture-levels.py <bundle> --levels 8x` removes a level's files and index entries together from a bundle converted before that.

The viewer cannot claim improved original fidelity: upscaling enlarges texture pixels only and cannot recover geometry or original-engine detail.
- Optional skeleton, wireframe, and automatic rotation views.
- Only Rest pose (BASE) is available now. The animation selector remains disabled
  until real clips are imported. No male model or placeholder animations are shown.

## Files

All converted/proprietary assets are ignored. The original basic bundle is under
`Client/Models/Characters/BasicFemale/`:

- `character.json`: versioned catalog, scene hierarchy, source references, weights,
  inverse binds, material groups, texture variants, and available source-animation metadata.
- `character.bin`: original float32 positions/normals/UVs/tangents and triangle indices.
- `textures/`: original-resolution PNGs; no generated or upscaled textures.
- `source/`: unchanged SCNs, item definitions, labels, and referenced original textures.
- `female-basic.three.json`: standalone Three.js ObjectLoader export, including bones,
  skin attributes, inverse binds, embedded textures, and the currently equipped rest-pose outfit.
- `verification/`: browser screenshots and an actual WebGL/UI test report.

The standalone export does not contain the whole interchangeable catalog. Use
`CharacterModel` plus the bundle for equipment changes; use ObjectLoader for an
already assembled character. Both retain skinning rather than flattening the model.

## Rebuild and verify

For the full wardrobe, from the repository root (export `S4_CLIENT_ZIP` to your own
Season-8 client ZIP; it and everything generated from it stays out of Git):

```sh
dotnet run -c Release --project Tools/s4l-threejs-converter -- \
  "$S4_CLIENT_ZIP" \
  Client/Models/Characters/Wardrobe --wardrobe

dotnet run -c Release --project Tools/s4l-threejs-converter -- \
  "$S4_CLIENT_ZIP" \
  Client/Models/Characters/Wardrobe --wardrobe --verify

node Client/tests/verify-wardrobe.mjs
node Client/tests/wardrobe-browser-smoke.mjs
node Client/tests/character-bodies-browser-smoke.mjs
```

The CPU verifier assembles every converted item/skin choice of both rigs. The browser verifier
draws and reads back every choice in WebGL, exercises search, extended skinning with shadows,
and saved-outfit persistence. Both append per-item journals and aggregate the saved records.
Browser verification can resume after interruption with `--resume`; a partial run is marked
`complete: false` and must not be reported as exhaustive validation.

For the original basic bundle:

```sh
dotnet run -c Release --project Tools/s4l-threejs-converter -- \
  "$S4_CLIENT_ZIP" \
  Client/Models/Characters/BasicFemale \
  --character Tools/s4l-threejs-converter/characters/female-basic.json

dotnet run -c Release --project Tools/s4l-threejs-converter -- \
  "$S4_CLIENT_ZIP" \
  Client/Models/Characters/BasicFemale \
  --character Tools/s4l-threejs-converter/characters/female-basic.json --verify

python3 -m unittest discover -s Tools/s4l-threejs-converter/tests -v
npm --prefix Client test
```

With the local preview server running:

```sh
node Client/tests/character-browser-smoke.mjs
node Client/tests/saved-outfits-browser-smoke.mjs
node Client/tests/extended-browser-smoke.mjs
node Client/tests/browser-smoke.mjs
```

The character browser test generates the standalone export and reloads it with
ObjectLoader. It also switches every imported skin through the actual UI and exercises
orbit controls. The Station-2 test guards the reused code against map regressions.
Set `S4_CLIENT_ZIP` to your client ZIP for the Python tests (they skip without it),
`CHROME_PATH` for another Chrome installation, or `VIEWER_URL` for another local server.

## Extending the catalog

The catalog uses game-domain objects rather than female-specific rendering branches:

- `bodies[]`: body ID/label, `sourceSex`, skeleton source, default equipment, items,
  and animation descriptors. `defaultBody` selects the initial rig.
- `items[]`: original item ID, equipment slot, label, one or more scene parts, named
  parts to hide, and texture variants.
- `parts[]`: scene source plus optional attachment-bone name. Supports both weighted
  clothing and attached models such as hair, faces, and accessories.
- `variants[]`: an ID/label and a dictionary mapping original texture paths to replacement
  paths. Different materials in the same item can change together without replacing geometry.
- `animations[]`: imported clip ID/label and a URL for Three.js AnimationClip JSON.

### More clothes or uniforms

The `--wardrobe` mode automatically imports all matching source wearable IDs. For a curated
subset, extend a body's `importItems` in a converter recipe and rerun conversion. The converter
resolves item graphics, multiple scene parts, head attachments, English labels, and
`hiding_option` from the actual item XML. Slots absent from `defaults` are optional and
can be unequipped. Preset/default items remain required so a default body cannot lose
its essential mesh partitions accidentally.

Numeric texture suffixes and available `_atex`/`_etex` counterparts are discovered
automatically. Arbitrarily named/custom skins can use the same output catalog's explicit
variant-map contract, but need an explicit importer rule or catalog entry; filenames are
not assumed to encode every possible skin scheme. `_n` textures are not treated as
color variants. Side textures with `ExtraUV=2` are normal maps, not lightmaps.

The runtime retains literal hiding and adds slot-aware `hair_all`, `face_all`, and
`gloves_partN`/`shoes_partN` semantics for authored `hide_partN`, `hide_part_N`, and
`hide_parts_N` mesh names. An accessory is not hidden merely because its mesh name says
hair. These conventions are source-name-supported, not original-client visual parity
claims; more elaborate engine-specific splitting rules still need validation.

### Male characters

Both shipped rigs are converted: the index carries a `female` and a `male` body, each with its
own skeleton, its defaults from `xml/default_item.x7`, and the items its `sourceSex` permits
(unisex items are shared). The viewer's Body type selector switches between them, and each body's
item definitions are checked against its `sourceSex` during conversion. Convert a single rig with
`--rig female` or `--rig male` when a full pass is not needed.

The male rig has its own animation pack. `Models/Characters/Animations/Male/` is converted from
that rig's own libraries (`male_bip.scn`, `bip_male/male_bip_0000.scn`) with the male preview
default `00074` and the shared unarmed `RunState_WeaponUnused` locomotion, so all 30 clips
(same 28 social actions and two movement states as the female pack) are source-backed and bound to
the 81-bone male rig. Each catalog body names its pack; a rig whose pack was not generated reports
that instead of playing another rig's tracks. See `ANIMATIONS.md` for the conversion commands. The
lightweight `?basic=1` fixture is still the female reference bundle
(`Models/Characters/BasicFemale/`).

### Selectable animations

`CharacterModel` has `registerAnimation(id, clip)`, `playAnimation(id)`, `update(deltaSeconds)`,
and `resetPose()`. The viewer loads catalog-listed AnimationClip JSON on demand and exposes
those entries in its selector. Bone transforms, attachments, and per-mesh inverse binds
remain available to these clips. Tests exercise playback and exact rest-pose restoration
with a test clip; that fixture is not presented as a converted game animation.

The original rig animation libraries are converted per rig by the `--animations` mode, which
chooses the correct library from the game's own bytecode and XML configuration and translates its
local tracks to that rig's assembled bones (see `Tools/s4l-threejs-converter/ANIMATIONS.md`).
`female_bip.scn` and `bip_female/female_bip_0000.scn` are different animation libraries despite
similar bind rigs, exactly as `male_bip.scn` and `bip_male/male_bip_0000.scn` are: a clip present
in only one library is exported from that exact source, and a clip missing from a rig's own
libraries is reported in `unavailable` instead of being substituted from the other rig.

## Binding correctness and limitations

- The main rig's matrices are character-world BASE transforms. Bone locals are computed
  as `inverse(parentBindWorld) * boneBindWorld`. Clothing-node matrices are local.
- Raw skinned positions already use character bind coordinates. Model transforms are not
  baked into them; doing so inverts the pants and misplaces the hair.
- Each mesh uses a separate Three.js Skeleton with shared character bones and its own
  original inverse binds. `bind(skeleton, identity)` plus attached mode preserves the source
  skinning equation without applying the model/attachment transform twice.
- Hair and face own separate `Hair_Bone_Dummy` nodes. They are not merged by name.
- Basic assets use at most three influences; three imported shirts use five. The viewer
  preserves these with a second four-slot attribute set, matching CPU positions, GPU
  positions/normals, and shadow skinning. No weights are pruned or renormalized. More than
  eight influences is rejected. Native ObjectLoader imports of an eight-slot mesh need
  `enableExtendedSkinning(mesh)` from `ExtendedSkinning.js` reapplied: shader callbacks
  are not serialized by Three.js. Such meshes carry `userData.skinInfluences = 8`.
- Source geometry, indices, matrices, animation payloads retained inline, weights/inverse
  binds, and dependency hashes are verified against freshly read source files. Negative
  tests reject changed geometry and bind matrices. Numerical runtime tests compare every
  skinned vertex with the source equation in rest and transformed poses.
- Lighting is a neutral studio preview, not a reproduction of the original game's complete
  character/toon/effect shader stack. No claim of pixel-perfect original-engine rendering.
