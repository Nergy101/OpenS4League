# Indexed female wardrobe

## Build and verify

From the repository root, using the locally supplied, unpacked Season-8 ZIP
(export `S4_CLIENT_ZIP` to its path — never committed):

```sh
dotnet run -c Release --project Tools/s4l-threejs-converter -- \
  "$S4_CLIENT_ZIP" \
  Client/Models/Characters/Wardrobe --wardrobe

dotnet run -c Release --project Tools/s4l-threejs-converter -- \
  "$S4_CLIENT_ZIP" \
  Client/Models/Characters/Wardrobe --wardrobe --verify

python3 -m unittest discover -s Tools/s4l-threejs-converter/tests -v
```

Generated files remain local and ignored. Do not commit game data. The converter
never changes the source ZIP or the browser code. The existing `--character`
recipe mode and Station-2 mode still use the same SCN serialization path.

`--wardrobe` selects source `woman` and `unisex` costume categories 0–6. It does not
select male-only equipment, pet-category items, or weapons. Items are selected
from XML, not filenames or inferred model numbers. A requested wearable may
reference a file in an unexpected folder; see the relocation provenance below.

## Browser contract

The entry point is `Client/Models/Characters/Wardrobe/index.json`:

```text
{
  format: "s4-wardrobe",
  version: 1,
  catalog: {
    name: "Female wardrobe", defaultBody: "female", defaultPose: "rest",
    bodies: [{
      id: "female", label: "Female", sourceSex: "woman",
      skeleton: "resources/model/character/female_bip.scn",
      defaults: {
        hair: "1000002", face: "1010001", shirt: "1020001",
        pants: "1030001", gloves: "1040001", shoes: "1050001"
      },
      items: [{
        id, label, slot, hides: [source hiding rules],
        parts: [{scene: canonical source path, attachmentBone: string | null}],
        variants: [{id, label, maps: {original texture path: replacement path}}]
      }],
      animations: []
    }]
  },
  scenes: {sourcePath: {json: "scenes/ID.json", bin: "scenes/ID.bin"}},
  textures: {sourcePath: {kind: "color" | "normal" | "lightmap" | "alpha", variants: {
    "1x": {file, width, height, algorithm, sourceWidth, sourceHeight, sourceSha256, generatedSha256},
    "2x": {file, width, height, algorithm, sourceWidth, sourceHeight, sourceSha256, generatedSha256},
    "4x": {file, width, height, algorithm, sourceWidth, sourceHeight, sourceSha256, generatedSha256}
  }}},

`1x` is the decoded original-resolution PNG and is required whenever decoding succeeds. `2x` and `4x` are optional generated variants; missing quality never substitutes another item texture. The runtime selects the highest available quality at or below the request and reports downward fallback. `kind` is semantic: color/alpha use sRGB, while normal/lightmap data is not color-corrected. `sourceSha256` hashes decoded source bytes and `generatedSha256` hashes each emitted PNG.

The repeatable ESRGAN pass is run from the repository root with `make threejs-asset-upscale`. It bootstraps a local `.cache/opens4l-realesrgan` environment, downloads the pinned `RealESRGAN_x4plus` weights, converts the wardrobe if necessary, and regenerates 2×/4× color and alpha textures. Alpha is resized with Lanczos; normal maps and lightmaps remain data-safe non-generative variants. The 1× decoded originals are never changed. Generated outputs are labeled `realesrgan-x4plus-rgb-alpha-lanczos`; they are an enhancement pass, not claims of recovered source detail. Upscaling does not change scenes, geometry, UVs, weights, animation, alpha semantics, normal maps, or lightmaps. Four-times dimensions can consume about sixteen-times the uncompressed texture memory, and browser `MAX_TEXTURE_SIZE` limits cause explicit downward fallback.
  inventory: [{id, label, slot, sex, status: "converted" | "unavailable", reason}],
  coverage: {requestedItems, convertedItems, unavailableItems, ...},
  provenance: {sceneResolutions: [{requestedReference, requestedPath,
                                 resolvedPath, method, reason}]},
  dependencies: {sourcePath: {file: "source/...", bytes, sha256}},
  aliases: {original legacy texture reference: canonical replacement path},
  sourceArchive, sourceConfig, coordinates
}
```

Every scene JSON is **one scene object**, not another bundle:
`{name, role, animationStorage, source, header, matrix, nodes, sourceBytes,
consumedBytes}`. Each geometry `byteOffset` addresses **that scene's own `.bin`**,
starting at zero. Geometry channel descriptors retain `count`, `itemSize`, and
`Float32Array`/`Uint32Array` type. Nodes retain names, parents, matrices, geometry,
material groups, flags, original weights, inverse binds, and animation details.
Duplicate node names do not collapse chunks. All dictionary source keys are
lower-case canonical archive paths; filenames use a stable path hash.

`catalog.bodies[0].items` contains usable conversions only. `inventory` contains
every requested XML record, including no-graphic/no-part definitions. An empty
part list is unavailable, never a successful zero-mesh item. A missing default
rig or any of the six default items fails the entire command.

The rig has source-only bone-animation metadata. Its full animation payload stays
in the preserved source SCN, not in every equipment scene. No playable clips are
claimed: the catalog animation list remains empty.

## Resolution, variants, and failures

`ConversionAssets` resolves exact paths, context paths, then a genuinely unique
basename. It never arbitrarily chooses among duplicates. Legacy `.tga` → `.dds`
substitutions use the existing resolver and are retained as aliases. Malformed
item identifiers fail validation before lookup; missing references are preserved
verbatim in diagnostics. Case canonicalization is not filename repair.

`CharacterCatalog.ResolveItem` supplies shared slot mapping, multipart scenes,
attachments, labels, and hiding rules. `SceneExporter` is shared with both old
modes; there is no second SCN parser. All positive skin weights are retained,
including meshes with more than four influences. The loader must support the
reported maximum; it must not silently prune or renormalize the source data.

Variants include available numeric color-family suffixes and `_atex`/`_etex`
team alternatives, including families whose source texture already has a numeric
suffix. `_n` maps are not offered as color skins. Available PNG/TGA/BMP/DDS color
candidates use the existing decoder. An undecodable or ambiguous optional variant
is omitted and recorded in `coverage.variantIssues`; it does not invalidate the
base item. Missing conventional team counterparts are also reported explicitly.
Numeric gaps are not invented as missing variants: their existence is unknown.

A missing or ambiguous required scene texture makes the affected item unavailable.
Missing optional team textures are a different condition. Coverage distinguishes:

- `failedScenes`: canonical scene → concrete conversion/dependency error.
- `missingSceneReferences`, `missingTextureReferences`: exact reference → reason.
- `failedTextures`: canonical texture → decode error; count excludes absent files.
- `variantIssues`: per-item optional counterpart/decode/ambiguity diagnostics.
- `skinWarnings`: source scene/node, maximum positive influences, affected vertices.
- `bySex` and `bySlot`: requested/converted/unavailable inventory accounting.
- `scenes`, `attemptedScenes`, `parsedScenes`: emitted scenes, attempted existing
  scene paths, and successfully serialized scenes respectively. Absent scene
  filenames are counted separately, not as existing SCNs.

Successful scenes and textures are written immediately and deduplicated. Failed
scenes leave no partial JSON/bin pair. `inventory.jsonl` is flushed every 25 XML
records. Final inventory and totals are read back from that saved journal; the
final `index.json` and `coverage.json` are replaced atomically after defaults pass.
A run is repeatable but does not resume from an interrupted journal. Use a fresh
output directory when changing source archives; unrelated stale files from a
different archive are not automatically deleted.

## Verified Season-8 result

The source-anchored integration test independently reads `Game/xml/item.x7` and
asserts **646 unique requested IDs**: 626 female and 20 unisex.

| Slot | Requested | Converted | Unavailable |
|---|---:|---:|---:|
| Hair | 60 | 55 | 5 |
| Face | 36 | 33 | 3 |
| Shirt | 116 | 107 | 9 |
| Pants | 115 | 107 | 8 |
| Gloves | 110 | 101 | 9 |
| Shoes | 111 | 105 | 6 |
| Accessory | 98 | 96 | 2 |
| **Total** | **646** | **604** | **42** |

- 586 emitted scenes; 592 existing paths attempted, including the rig and one
  unique-basename relocation. Six scene dependency failures, no SCN parser failure.
- 2,001 decoded textures, 2,225 item variant choices including Original entries,
  2,596 retained source dependencies. No decoder failures in this archive.
- 48 optional missing-team-counterpart issues; these do not disable base items.
- 1,032 meshes, 247,450 vertices, 268,644 triangles.
- 42 unavailable IDs: 20 without renderable scene references, 16 with genuinely
  absent scenes, five with missing required textures, one with ambiguous texture.
- Five-influence shirts remain converted: `1020053` (`34_female_body.scn`, two
  vertices), `1020058` (`32_female_body.scn`, four vertices), and `1021070`
  (`67_female_body.scn`, eleven vertices). Every original weight is retained.

The six required-texture failures are:

| Item | Scene | Required texture problem |
|---|---|---|
| 1021137 | body/41_female_body_parts3.scn | Missing 41_female_body_acc_atex.dds |
| 1021082 | body/72_female_body.scn | Missing 72_female_body_n.dds |
| 1040010 | hand/01_female_hand.scn | Missing 01_female_hand_n.tga (DDS replacement also absent) |
| 1041064 | hand/36_female_hand.scn | Missing 36_female_hand_atex.dds |
| 1031082 | leg/72_female_leg.scn | Missing 72_female_leg_n.dds |
| 1060000 | pet/acc_virus.scn | virus_helmet.dds ambiguous between character/acc and monster |

`1060000` requests `resources/model/character/acc/acc_virus.scn`. The only matching
scene basename exists at `resources/model/character/pet/acc_virus.scn`; this is
recorded as `method: "unique-basename"` with the exact requested and resolved paths.
It is still unavailable because its texture basename is not unique. The converter
does not guess a texture merely because the wearable was requested from `acc/`.

`--verify` reparses original ZIP scenes and checks every channel bit-for-bit,
matrices, names/parents, animations, material ranges, raw weights/inverse binds,
and every preserved dependency SHA-256. The verified wardrobe contains 8,619 nodes,
2,167,085 float32 values, and 805,932 indices. Regression tests deliberately corrupt
vertex bytes and weights, exercise malformed identifiers/ambiguous references and
bad DDS variants, verify per-scene offsets and PNG headers, and re-export the two
legacy modes to temporary directories to compare their complete geometry buffers.
