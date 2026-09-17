# Original female animation pack

The animation mode is independent of Station-2, BasicFemale, and Wardrobe. It does
not change their manifests, catalogs, geometry, or Client source. User-supplied
source assets and generated packs must remain local and ignored by Git.

## Convert and verify

From the repository root, with .NET 10 installed and `S4_CLIENT_ZIP` exported to your
own Season-8 client ZIP (user-supplied, never committed):

```sh
dotnet run -c Release --project Tools/s4l-threejs-converter -- \
  "$S4_CLIENT_ZIP" \
  Client/Models/Characters/Animations/Female --animations

dotnet run -c Release --project Tools/s4l-threejs-converter -- \
  "$S4_CLIENT_ZIP" \
  Client/Models/Characters/Animations/Female --animations --verify

python3 -m unittest discover -s Tools/s4l-threejs-converter/tests -v
node Tools/s4l-threejs-converter/tests/test_animation_three.mjs
```

`--verify` is read-only. It decodes the ZIP again, reconstructs every expected
clip and provenance document, compares their parsed JSON, and compares every
preserved source dependency byte-for-byte. It never repairs corrupted output.
The Python integration suite runs against the client ZIP in `S4_CLIENT_ZIP`
and skips the source-anchored checks when it is unset. The Three.js check uses the
Client's installed, pinned Three.js package; it does not modify Client files.

## Provenance, not guessed clip names

SCN names such as `idle`, `CRYING`, or `A0005` alone do not establish an action's
meaning. In this client the relevant configuration is compiled Lua 5.1, not
plain-text Lua. The converter statically parses the full bytecode, finds the
named function via `CLOSURE`/`SETGLOBAL`, and reads its constant `SELF`/`CALL`
arguments. **It does not execute client scripts.**

| Pack ID | Original clip | Evidence |
| --- | --- | --- |
| `idle` | `00029` | `PreviewActorAnimSetting`, `SetFemaleDefaultAnim("00029", 1)` in `resources/script/previewactoranimsetting.lua` |
| `walk` | `00008` | Both forward upper-body and lower-body `SetAnim` calls inside **`RunState_WeaponUnused`** in `resources/script/actorstates_runstates.lua` |
| `greet` | `O0003` | Original social configuration: Basic greeting, `/hi` |
| `cry` | `O0007` | Original social configuration: Sad, `/cry` |
| `scissors` | `O0015` | Original social configuration: Scissors, `/scissors` |
| `rock` | `O0016` | Original social configuration: Rock, `/rock` |
| `paper` | `O0017` | Original social configuration: Paper, `/paper` |
| `wave` | `O0018` | Original social configuration: Wave, `/wave` |

Movement caveat: `00008` is the original **unarmed locomotion** state, not an
armed clip with a hidden gun. The six left/right upper-arm, forearm, and hand
quaternion tracks each contain 15 authored samples. The engine calls the state
`RunState`; a distinct, slower walking-only state has not been established.
Consequently the label is **Walking / running (unarmed)**, not an assertion that
an independently verified slow walk exists. Both original forward calls are
stored in `mapping`: upper `["00008", 0, 2000, 0, "LOOP_TRUE", 1, "RESET_FALSE"]`
and lower `["00008", 0, 1000, 0, "LOOP_TRUE", 1, "RESET_FALSE"]`. Those engine
parameters are recorded, not reverse-engineered into a guessed time remapping.
Raw SCN duration is 3.2 seconds; idle is 8 seconds. Runtime preview speed may be
adjusted without changing the exported source samples.

Social selection joins `language/_eu_default_option.x7` entries marked
`useweapon="false"` with English strings from
`language/xml/default_option_string_table.x7`. It exports **all 28 mapped social
actions available in the two female libraries**:

- Win, Defeat, Provocation, Basic greeting, Say with shyness, Sad, Smile,
  Polite Greeting, Say, Yelling, Show anger, Applause;
- Scissors, Rock, Paper, Wave, Dance, High Five, Hug, Hit, Recommend,
  Humiliated, Gasp, Ridicule;
- Perform a New Year's bow, Dance 2, Warming up, and `/Dance 3`.

The last label, including its leading slash, is preserved from the original
English table; its label/command appear reversed there. Source label, command,
string keys, social ID, and clip ID remain available in each mapping record.
No menu sound, particles, or facial morphs are synthesized.

### Which library?

`resources/model/character/female_bip.scn` contains 735 distinct clip names.
`resources/model/character/bip_female/female_bip_0000.scn` contains 228 names;
only nine overlap. They are **not interchangeable animation libraries**.
The main library provides idle, unarmed movement, and the first 27 social
clips. **`O0034` exists only in the variant library** and is exported from that
exact library. Both have the compatible 82-bone named rig. Shared clip names
prefer the main/canonical library, not an arbitrary union of per-bone records.
Every clip declares `sourceScene`. Missing mapped clips would be placed in
`unavailable` with the exact reference and reason; they are never relabeled
substitutes. This supplied archive has zero unavailable mapped social actions.

## Output and playback contract

`index.json` has `format: "s4-character-animations"`, `version: 1`, `bodyId:
"female"`, and independent `clips`, `dependencies`, `provenance`, `verification`,
and `unavailable` records. Clip files are native `THREE.AnimationClip.parse()`
JSON:

- Track names are **exact bone names** followed by `.position`, `.quaternion`,
  or `.scale`. Spaces are retained, e.g. `Bip01 L Hand.quaternion`.
- Each clip has a deterministic UUIDv8 derived from its source-library/clip
  identity using SHA-256. This is a clip cache identity, **not** a runtime bone
  UUID reference. It is mandatory: Three.js r186 `AnimationClip.parse()` assigns
  `json.uuid` even when absent; omitting it makes every mixer action collide
  under the `undefined` cache key and later selections keep playing the first.
- Vector tracks use `type: "vector"`; rotation uses `type: "quaternion"`.
  Interpolation is `2301` (Three.js linear vector / quaternion SLERP).
- Key times are source milliseconds divided by 1000. No resampling, smoothing,
  quaternion normalization, additive conversion, key reduction, or retiming.
- Local source TRS values **replace** bone local transforms. They are not
  deltas multiplied by the bind matrix. Do not apply the world BASE bone matrix
  a second time. Coordinates and quaternions stay in original Y-up S4 units;
  the existing actor wrapper handles handedness and placement once.
- Missing channel samples use that animation's static TRS at time zero.
  Missing bone animation records, or a null transform key, use the source
  bone's BASE local TRS. Every exported clip provides all three channels for
  every rig bone, so changing animations cannot leave stale limb poses.
- `Copy` is resolved per bone, recursively, with cycle detection. Local clip
  libraries take precedence. A clip absent from the current library resolves
  only to a unique external library. A known target clip lacking that bone's
  channel uses BASE; an entirely absent/ambiguous source clip is an error.
  No case normalization, invented aliases, or discarded Copy records.
- Movement clips loop; social clips are one-shot. The runtime may replay them
  or clamp the final frame. No fake T-pose clip exists: the viewer's rest path
  is the T-pose/default selection.

### In-place movement

All exported root tracks are **raw**, including horizontal displacement.
`rootBone: "Bip01"` and `inPlace: true` on `walk` recommend an optional preview
correction. After mixer evaluation, countertranslate a **separate actor
placement wrapper** by the root's current minus clip-start displacement in
source x/z (converted to the wrapper's coordinates as needed). Leave all
mixer-tracked bone values, vertical y motion, and quaternions untouched. Compute
the offset from a fixed baseline every frame; do not accumulate it. Other
clips default to `inPlace: false`. This is runtime treatment, not baked output.

## Verification results for the supplied archive

- Both SCNs consumed completely: 30,526,084 and 7,160,568 bytes.
- 30 clips: two movement actions plus 28 emotes; 7,380 tracks.
- 135,116 source TRS keys plus 4,462 static-channel samples = 139,578 exported
  samples. There are 645 sparse bone/clip BASE fallbacks.
- Selected clips contain zero Copy records. To avoid an alias-free false
  confidence test, the converter also audits **all 3,605 original Copy records**
  in both libraries: all resolve, including 348 sparse target-bone BASE
  fallbacks. Independent tests compare the resolved original data objects and
  count 57,406 referenced keyed TRS samples. No unresolved library copies.
- An independent C# test decodes the original scenes and compares every
  exported time and component, including static TRS and actual quaternions.
  The read-only verifier rejects modified clip values, metadata, and raw
  dependencies. Synthetic tests cover alias replacement, cross-library
  resolution, local precedence, cycles, missing sources, nonfinite values,
  duplicate/unsorted times, and BASE fallback.
- Three.js r186 parses and validates all 30 clips, checks 30 unique deterministic
  UUIDs, binds exact names with spaces, and switches all clips on a **shared
  mixer**, including A → B → A. It evaluates every track at five times:
  **36,900 channel samples**, zero
  warnings, maximum difference from native track interpolation
  `0.00000762939453125`.
- The complete converter Python suite passed 23 tests, including legacy map,
  BasicFemale geometry/source comparisons, and Wardrobe coverage. Visual
  comparison against the original game remains separate from these numerical
  checks; the parent viewer workflow performs browser review.

Preserved dependencies live under `source/` with SHA-256 and byte counts in the
index. `provenance/bone-channels.json` stores selected per-bone base TRS, key
counts, fallback/copy chains, source durations, and unsupported Float/alpha
keys. `provenance/copy-audit.json` records every library Copy resolution and any
failure. These proprietary source/provenance artifacts remain local.
