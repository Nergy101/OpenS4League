# OpenS4L

A modern, self-contained rebuild of the **S4 League** server and tooling stack.

OpenS4L is an independent project: a from-scratch **.NET 10** server (Auth, Chat, Game,
Relay) plus a cross-platform toolkit for editing the game's resource archives, maps,
animations, items and configs. It is an unofficial, non-commercial community emulator — see
[**Credits & Attribution**](#credits--attribution) for the upstream projects it builds on.

## What this is

- **A modern server emulator.** Four servers (Auth, Chat, Game, Relay) compiled from source
  on **.NET 10** and run against the client data you provide yourself. Databases use
  **PostgreSQL** (via Npgsql / EF Core 10), with **Redis** for caching/queueing.
- **A cross-platform toolset.** A dozen GUI + CLI utilities (Avalonia/.NET 10) for working
  with S4 League's `resource.s4hd` archives, `.scn` scenes, animations, XML data and server
  configs.
- **Dockerized.** The whole server stack (postgres, redis, the four servers, provisioner)
  runs under `Server/Docker`.

> OpenS4L emulates the server protocol only. It does **not** redistribute the S4 League
> game client or any game content — you supply those yourself.

## What's new on top of NetspherePirates

The original NetspherePirates emulator was a **.NET Core 2.1** codebase whose server half
used the never-published **BlubLib** packages, the **MEF** plugin host, **ExpressMapper**, an
**EF Core 2.1 / MySQL** data layer, and shipped only a legacy WinForms tooling set. OpenS4L
keeps the wire protocol identical (so the real 2013 client still connects) but rebuilds
everything around it on a modern stack. What was added or changed on top of the original:

### Server platform & dependencies
- **.NET Core 2.1 → .NET 10.** Every project retargeted to `net10.0` with `LangVersion latest`.
- **MySQL/Pomelo → PostgreSQL/Npgsql** with **EF Core 10**, regenerated migrations, and
  `BatchDelete/BatchUpdate` replaced by native `ExecuteDeleteAsync`/`ExecuteUpdateAsync`.
- **ExpressMapper → Mapperly** (source-generated, no reflection) for object→DTO mapping.
- **MEF plugin host → `ScanPluginHost`**, a plain reflection scan — no `System.Composition`
  dependency. Plugins are drop-in DLLs implementing `IPlugin`, deployed to a `plugins/`
  folder, no server rebuild required (see `Server/PLUGINS.md`).
- **BlubLib NuGet packages → in-repo `OpenS4L.Blub`** project (namespaces renamed
  `OpenS4L.Blub.*`), since the BlubLib source was never published.
- **Bundled plugins** — `WebApi` (HTTP API over the game server), `SoloMode`,
  `EquipLimitExtended`, plus an `ExamplePlugin` reference.

### Networking
- **ProudNet reimplemented in-repo** (`src/ProudNet/`) and kept byte-compatible with the
  client (`0.8.32.26995`). The servers ship the server half; `ProudNetClient` is the
  client-side counterpart used by the load bot.
- Modern hosting (`.NET 10` `IHostApplicationLifetime`, hosted IPC services) in place of the
  2.1-era hosting API.

### Databases & services
- **Redis** for caching and messaging, with a **write-behind queue** that batches player
  saves into single writes and skips clean players — taking DB write load off the hot path.
- **EF Core 10** schema for Auth (accounts, bans, login history) and Game (players,
  characters, inventory, shop, clans, mail, …) against PostgreSQL.

### Testing & diagnostics (all new)
- **Transport-fake harness** — drives the real Game/Chat handlers, room + game-rule state
  machine, clans, teams, GM commands and IPC services in-process over an in-memory ProudNet
  transport (no network), covering the heaviest domain layer.
- **Real-Postgres harness** (Testcontainers) — spins up Postgres 16 and exercises the EF
  `ExecuteUpdate/ExecuteDelete` persistence paths that can't run on the in-memory provider.
- **Differential mapping tests** — run the legacy ExpressMapper config and the new Mapperly
  mappers on identical sources and assert byte-identical DTOs (the "identical results"
  acceptance test), pinning latent legacy bugs as characterization tests.
- **Verify snapshot tests** — the WebApi HTTP DTO contract is pinned so any accidental
  contract change shows up as a diff.
- **Load bot (`OpenS4L.LoadBot`)** — real-protocol simulated players (genuine ProudNet
  clients doing the RSA/AES/RC4 handshake, auth login, character creation, channel join, and
  optional chat) for load testing; bots appear live in the admin console.
- **k6 load tests** (`Server/Docker/load/`) — smoke / load / soak profiles against the WebApi
  HTTP plane.
- **Code coverage** via `make coverage` — currently WebApi ~82%, Chat ~81%, Database ~96%,
  Common ~83%, Game ~43%.

### Tooling (all new — see `Tools/README.md`)
- **A dozen cross-platform utilities** (Avalonia/.NET 10) replacing the legacy WinForms
  tools: `s4l-resource-tool`, character viewer, map editor, animation creator, item/shop
  editor, client configurator, client mod packer, resource diff, localisation editor, server
  config & deploy tool, legacy MySQL→PostgreSQL migration CLI, and the **React admin
  console** (server status, config readout, live log tail, read-only Redis browser, chat
  metrics + peak player chart).

### Operations
- **Dockerized stack** (`Server/Docker/`): postgres, redis, the four servers, and a
  provisioner that creates the admin account and loads the free shop — plus a client-data
  named volume populated from your extracted game data.
- **Windows-first Makefiles** using only portable `dotnet` commands, so every target also
  runs on macOS/Linux.

## Layout

| Path | Contents | Status |
|---|---|---|
| `Server/` | The server source (`Server/opens4l/`) on **.NET 10** with projects/namespaces named `OpenS4L.*`, plus `Docker/` (postgres, redis, the four servers, provisioner) and `PLUGINS.md`. | Rebuild mostly done |
| `Tools/` | Cross-platform utility tooling — a dozen v1.0 tools (see below and `Tools/README.md`). | v1.0 |
| `Client/` | Reserved for the 2013 S4 League client (not part of the emulator). | Empty for now |

### Tools

All twelve tools are built as working **v1.0** (see `Tools/README.md` for per-tool detail).

| Tool | Status | Description |
|---|---|---|
| `s4l-resource-tool` | Ready | Cross-platform resource editor (**.NET 10 + Avalonia**): browse/edit `resource.s4hd`, preview text, images and 3D `.scn` scenes, texture upscaling/export. |
| `s4l-character-viewer` | v1.0 | View characters, clothes and animations: model/bone/animation listing, textured 3D view, playback. |
| `s4l-map-editor` | v1.0 | Create/edit S4 `.scn` maps: chunk transform editing, add/duplicate/delete chunks, 3D preview, save. |
| `s4l-animation-creator` | v1.0 | Edit character/skeletal animations: translation-keyframe timeline, playback, save. |
| `s4l-item-editor` | v1.0 | Edit XML data files (items/shop/weapon) as a record table; save to disk or archive. |
| `s4l-client-configurator` | v1.0 | Set startup movies, loading/login background art by replacing resources in `resource.s4hd`. |
| `s4l-client-mod-packer` | v1.0 | Bundle selected entries into a `.s4mod` package and install it into a client. |
| `s4l-admin-console` | v1.0 | Web dashboard over running servers: up/down status, config readout, live log tail. |
| `s4l-server-config-tool` | v1.0 | Edit the four `config.hjson` files; assemble a deploy zip. |
| `s4l-legacy-migration` | v1.0 | CLI converting a legacy MySQL data dump to PostgreSQL SQL. |
| `s4l-resource-diff` | v1.0 | Compare two archives, list added/removed/changed, export a delta or apply B→A. |
| `s4l-localisation-editor` | v1.0 | Manage `language/*.x7` across locales: edit strings, missing-key report. |

## Status checklist (root)

- [x] `Server/` — .NET 10 rebuild compiling and working (see `Server/README.md`).
- [x] Dockerize — server stack containerized (`Server/Docker`: postgres, redis, the four servers, provisioner).
- [x] Mapping — ExpressMapper replaced by **Mapperly**, verified byte-identical via the differential test suite.
- [x] Plugins — **ScanPluginHost** replacing MEF; WebApi, SoloMode, EquipLimitExtended + ExamplePlugin bundled.
- [x] Persistence — **Redis write-behind** player-save queue (batched writes, clean-player skip) + PostgreSQL/EF Core 10 schema.
- [x] Tests — transport-fake harness, real-Postgres (Testcontainers) harness, differential + Verify snapshot tests, LoadBot, k6 load profiles; `make test` / `make coverage`.
- [x] `Tools/s4l-resource-tool` — verified build (`make -C Tools/s4l-resource-tool build`).
- [x] `Tools/` v1.0 toolset — resource tool, CharacterViewer, Map Editor, AnimationCreator, Item Editor, ClientConfigurator, Client Mod Packer, Admin Console, Server Config, Data Migration, Resource Diff, Localisation Editor: all build (`make tools`) and the desktop apps launch.
- [ ] `Client/` — populated with the client data.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (required by the Server and the tools).
- `make` (optional — every target below has an equivalent `dotnet` command you can run directly).
- `pnpm` (for the `s4l-admin-console` web dashboard).

## Building

```bash
# everything (tools + server)
make build

# just the server
make server

# just the tools
make tools

# the admin console web dashboard (needs pnpm)
make admin

# run the server unit tests
make test
```

> These Makefiles target **Windows** (git-bash / MSYS `make` + `dotnet`) but use only
> portable `dotnet` commands, so the same targets work on macOS/Linux.

## Running the server

See `Server/README.md` for the migration checklist and build steps, and `Server/Docker/`
for the containerized stack. The four servers bind `:28002`–`:28005` and `:29000`–`:29001`;
they expect PostgreSQL on `5432` and Redis on `6379`, and need your client data under
`Server/Docker/data/`.

## Credits & Attribution

OpenS4L is an independent project, but it builds directly on prior work and we credit it
fully. The most important upstream:

- **NetspherePirates** (by wtfblub) — the original S4 League server emulator, from which the
  server and `OpenS4L.Blub` are derived/ported.
- **BlubLib** (by wtfblub) — utility/serialization/transport libraries, ported in-repo
  (`OpenS4L.Blub`), recovered by decompilation since upstream source was never published.
- **ProudNet** (by Nettention) — the proprietary networking middleware the S4 League client
  speaks; `src/ProudNet/` is a byte-compatible protocol reimplementation.
- **LZO / MiniLZO** (Markus F.X.J. Oberhumer; C# port by Frank Razenberg) — **GPL v2+**; see
  the legal notes.

S4 League and its assets are the property of **Nexon** and/or its licensors.

See **[CONTRIBUTING.md](CONTRIBUTING.md)** for the full credits, license terms of every
bundled and upstream component, and important **attribution & legal notes** (including the
GPLv2 terms that apply to distributed `s4l-resource-tool` binaries). The repository is
licensed under the MIT License — see [LICENSE](LICENSE).
