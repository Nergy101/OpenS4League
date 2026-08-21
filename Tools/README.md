# Tools

Cross-platform utility tooling for the S4 League ecosystem.

| Tool | Description | Framework |
|---|---|---|
| `s4l-resource-tool/` | Browse / edit `resource.s4hd` archives; preview S4 resources — text, images, and interactive 3D `.scn` scenes — plus texture upscaling/export (Avalonia GUI). | .NET 10 + Avalonia |
| `s4l-character-viewer/` | View S4 character models, clothes and animations; orbit/zoom 3D preview, simple animation playback. | .NET 10 + Avalonia |
| `s4l-map-editor/` | Create/edit S4 `.scn` maps: edit chunk transforms, add/duplicate/delete chunks, live 3D preview, save. | .NET 10 + Avalonia |
| `s4l-client-configurator/` | Set client UI art — startup movies, loading/login background art — by replacing resources in `resource.s4hd`, with preview and a remembered screen→resource map. | .NET 10 + Avalonia |
| `s4l-admin-console/` | Web dashboard over the OpenS4L servers: up/down status, config summary, live log tail. | React + Node |
| `s4l-animation-creator/` | Edit character/skeletal animations: timeline of translation keyframes, live 3D playback, save back to `.scn`. | .NET 10 + Avalonia |
| `s4l-resource-diff/` | Compare two `resource.s4hd`, list added/removed/changed entries, export a delta package, or apply B→A. | .NET 10 + Avalonia |
| `s4l-client-mod-packer/` | Bundle selected archive entries into a redistributable `.s4mod` package and install it into a client. | .NET 10 + Avalonia |
| `s4l-localisation-editor/` | Manage `language/*.x7` files across locales: edit strings, report missing keys, copy missing keys. | .NET 10 + Avalonia |
| `s4l-item-editor/` | Edit XML data files (items/shop/weapon) as a record table: load, edit attributes, add/delete rows, save. | .NET 10 + Avalonia |
| `s4l-server-config-tool/` | Edit the four server `config.hjson` files and assemble a deploy zip (configs + plugins). | .NET 10 + Avalonia |
| `s4l-legacy-migration/` | CLI: convert a legacy NetspherePirates MySQL data dump into PostgreSQL-friendly SQL. | .NET 10 CLI |

## Current status (v1.0)

All of the tools below are built as working **v1.0**. The Avalonia tools share the
`S4League.Scn` / `S4League.Resource` parsers and the `S4League.View` library (renderer +
texture loading + 3D preview control).

| Tool | Status | What works today |
|---|---|---|
| `s4l-resource-tool` | Ready | Archive browse/edit, text/image/`.scn` preview, texture upscaling/export. |
| `s4l-character-viewer` | v1.0 | Open `.scn` (file or from archive), list models/bones/animations, textured 3D view, rigid-body animation playback. |
| `s4l-map-editor` | v1.0 | Open `.scn` (file or archive), chunk tree, transform editor (translation/rotation/scale), add box/shape, duplicate/delete chunks, 3D preview, save. |
| `s4l-client-configurator` | v1.0 | Open `resource.s4hd`, per-screen target mapping, browse/`Set from file…` to replace art, preview, save. |
| `s4l-admin-console` | v1.0 | Backend API + React dashboard: server status by port probe, config readout, auto-refreshing log tail. |
| `s4l-animation-creator` | v1.0 | Open `.scn`, pick a model/animation, edit translation keyframes (add/remove/apply), playback, save. |
| `s4l-resource-diff` | v1.0 | Compare two archives; added/removed/changed list; export `.s4delta`; apply B→A. |
| `s4l-client-mod-packer` | v1.0 | Load archive entries, build `.s4mod`, install into a target client, preview a `.s4mod`. |
| `s4l-localisation-editor` | v1.0 | Open archive, list `language/*.x7`, edit decoded text, missing-key report, copy missing keys. |
| `s4l-item-editor` | v1.0 | Load XML data (from disk or archive), record table with editable attribute columns, add/delete rows, save. |
| `s4l-server-config-tool` | v1.0 | Auto-detect repo, edit all four `config.hjson`, save, build deploy zip. |
| `s4l-legacy-migration` | v1.0 | Convert MySQL data dump → Postgres SQL (verified on a sample dump). |

## Roadmap — status

All roadmap tools are now scaffolded and building as **v1.0**. Ideas for future iterations are
noted inline. Each tool sits beside `s4l-resource-tool` and reuses the shared
`S4League.Scn` / `S4League.Resource` / `S4League.View` foundations.

### Editors & viewers

| Tool | Status | Notes / next steps |
|---|---|---|
| **Map / Scenario Editor** | v1.0 | Transform editing, add/duplicate/delete chunks, 3D preview, save. Next: collision/spawn-point editing, lights, triggers. |
| **CharacterViewer** | v1.0 | Model/bone/animation listing, textured 3D view, rigid-body playback. Next: true vertex skinning from `WeightBone` + `BoneChunk`. |
| **AnimationCreator** | v1.0 | Translation-keyframe timeline, playback, save. Next: rotation/scale keyframes, curves, keyframe timing UI, transitions. |
| **Item / Shop / Weapon Editor** | v1.0 | Generic XML record-table editor (load, edit attributes, add/delete rows, save). Next: items.xml/shop-specific schema knowledge + validation. |

### Server & operations

| Tool | Status | Notes / next steps |
|---|---|---|
| **Server Admin Console** | v1.0 | Status by port probe, config readout, log tail. Next: live player/channel data, kick/ban/mute, room controls. |
| **Server Config & Deploy Tool** | v1.0 | Edit all four `config.hjson`, assemble deploy zip (configs + plugins). Next: Docker wrapper, HJSON-aware editing. |
| **Legacy Data Migration Tool** | v1.0 | CLI converts a MySQL data dump → Postgres SQL. Next: direct MySQL/Postgres connection, schema-aware mapping. |

### Content & packaging

| Tool | Status | Notes / next steps |
|---|---|---|
| **Resource Diff / Sync** | v1.0 | Compare two archives, added/removed/changed, export `.s4delta`, apply B→A. |
| **ClientConfigurator** | v1.0 | Screen→resource mapping, browse/`Set from file…`, preview, save. Next: media re-encoding, XUI layout targeting. |
| **Client Mod Packer** | v1.0 | Build `.s4mod`, install into a client, preview a mod. |
| **Localisation Editor** | v1.0 | Edit `language/*.x7`, missing-key report, copy missing keys. Next: locale-aware source format detection. |

### Status checklist (Tools)

- [x] `s4l-resource-tool/` present (build artifacts stripped).
- [x] Verified it builds on this machine: `make -C s4l-resource-tool build`.
- [x] `extract-tool/` CLI builds: `dotnet build -C .../s4l-resource-tool/extract-tool`.

## Build & run

```bash
cd s4l-resource-tool
make build     # or: dotnet build -c Release
make run       # or: dotnet run --project src/S4LResourceTool.App
make test      # selftest + headless UI tests
```

Other useful targets: `make open ARCHIVE=<path>` (dump entries from an archive),
`make extract CLIENT=<path> OUT=<data-dir>` (extract server data), `make publish RID=<rid>`.

See `s4l-resource-tool/README.md` for the full guide and publish instructions.

## Build & run (new tools)

Each Avalonia tool builds standalone and shares `S4League.Scn` / `S4League.Resource` /
`S4League.View`. Build all of them at once from the repo root with:

```bash
make tools     # builds the resource tool + all Avalonia tools + the migration CLI
```

Or individually:

```bash
cd s4l-character-viewer && dotnet build && dotnet run
cd s4l-map-editor       && dotnet build && dotnet run
cd s4l-client-configurator && dotnet build && dotnet run
cd s4l-animation-creator   && dotnet build && dotnet run
cd s4l-resource-diff       && dotnet build && dotnet run
cd s4l-client-mod-packer   && dotnet build && dotnet run
cd s4l-localisation-editor && dotnet build && dotnet run
cd s4l-item-editor         && dotnet build && dotnet run
cd s4l-server-config-tool  && dotnet build && dotnet run
```

The legacy migration tool is a CLI:

```bash
cd s4l-legacy-migration && dotnet build
dotnet run --no-build -- <input.sql> [output.sql] [--table <name>]
```

The admin console is React + Node (see `s4l-admin-console/README.md` for full instructions):

```bash
cd s4l-admin-console/web && pnpm install && pnpm run build   # build the dashboard
cd s4l-admin-console     && node server/server.js          # serve API + dashboard on :8020
```
