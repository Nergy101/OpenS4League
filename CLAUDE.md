# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

OpenS4L — an unofficial S4 League server emulator (four servers: Auth, Chat, Game, Relay) rebuilt
on .NET 10 from the .NET Core 2.1 **NetspherePirates** codebase, plus a cross-platform toolset for
the game's client data. Three top-level areas: `Server/` (the emulator), `Tools/` (a dozen
Avalonia/CLI/web utilities), `Client/` (empty; reserved for user-supplied game data).

The overriding constraint: **the wire protocol must stay byte-identical with the real 2013 client**
(`0.8.32.26995`, ProudNet version GUID `{beb92241-8333-4117-ab92-9b4af78c688f}`). Anything touching
`ProudNet/`, `OpenS4L.Network/`, serializers or mappers is protocol surface — change it only
deliberately.

## Commands

Everything is driven by Makefiles that wrap portable `dotnet` commands (they work on macOS/Linux
too, despite the "Windows-first" comments). Every Makefile has a `help` target listing its targets.

```bash
# root
make build            # tools + server
make server           # server solution only  (= make -C Server build)
make tools            # resource tool + all Avalonia tools + migration CLI
make admin            # admin console frontend (needs pnpm)
make test             # server unit tests
make coverage         # tests + coverlet -> Server/opens4l/tests/coverage-results/
make cleanup          # drop coverage/test artifacts, keep build output

# Server/
make -C Server publish   # publishes the 4 servers to Server/opens4l/dist/{auth,chat,game,relay}
```

### Tests

There is one test project, `Server/opens4l/tests/OpenS4L.Server.Mapping.Tests` (xunit + VSTest),
despite the name it covers the whole server domain layer.

```bash
dotnet test Server/opens4l/tests/OpenS4L.Server.Mapping.Tests/OpenS4L.Server.Mapping.Tests.csproj -c Release

# a single class / test
dotnet test .../OpenS4L.Server.Mapping.Tests.csproj -c Release --filter "FullyQualifiedName~GameRoomTests"
dotnet test .../OpenS4L.Server.Mapping.Tests.csproj -c Release --filter "FullyQualifiedName~GameRoomTests.Room_join_leave"
```

`*PostgresTests` need Docker (Testcontainers spins up Postgres 16). Everything else runs offline.

### Running the stack (Server/Docker)

```bash
make -C Server/Docker bootstrap   # publish + stage plugins + compose up + provision admin/admin
make -C Server/Docker data SRC=/path/to/extracted-client-data   # fills the opens4l_clientdata volume
make -C Server/Docker logs        # follow the game server
make -C Server/Docker loadbot-help  # load-bot scenarios and parameters
make -C Server/Docker loadtest-smoke  # k6 against the WebApi plugin (port 22000)
```

Ports: auth 28002, chat 28003/28006, game 28004, relay 28005, ipc 29000-29001, WebApi 22000,
Postgres 5432, Redis 6379 (published on 6380 by compose). `make publish` must run before `up` —
containers mount the published `dist/` output, they don't build.

### Tools

```bash
make -C Tools/s4l-resource-tool build|run|test   # test = crypto self-test + headless Avalonia UI tests
dotnet build -c Release Tools/s4l-map-editor     # the other Avalonia tools have no Makefile
cd Tools/s4l-admin-console/web && pnpm run build && node ../server/server.js   # dashboard on :8020
```

## Server architecture

`Server/opens4l/OpenS4L.Server.slnx` (new `.slnx` format) — `src/`, `src/plugins/`, `tests/`.

**Layering.** `ProudNet` (transport) → `OpenS4L.Network` (S4 message/opcode/serializer definitions)
→ the four `OpenS4L.Server.*` hosts. Shared: `OpenS4L.Common` (config, value types, crypto, plugin
host), `OpenS4L.Database` (EF Core 10 / Npgsql, `AuthContext` + `GameContext` + migrations),
`OpenS4L.Resource` (client data files), `OpenS4L.Blub` (in-repo port of the never-published BlubLib
serialization/DotNetty helpers), `Logging`.

**ProudNet** (`src/ProudNet/`) is an in-repo reimplementation of Nettention's proprietary
middleware, still on DotNetty (a `System.IO.Pipelines` port is planned but not done).
`ProudNetClient` is the client half, used by the load bot and tests.

**Server hosts** are generic-host apps (`Program.cs` per server) wiring DI, EF contexts, Foundatio
(Redis-backed `IMessageBus`/`ICacheClient`), the handler resolver and the plugin host. Handlers live
in `Handlers/`, firewall rules in `Rules/`, hosted background work in `Services/`.

**Cross-server IPC** is Foundatio message-bus over Redis (`IpcService` in Game/Chat/Auth), not
direct calls — e.g. the game server announces itself to auth via `ServerlistService`.

**Game domain** (`OpenS4L.Server.Game`) is the heavy part: `Channel`/`Room`/`RoomManager`,
`GameRuleBase` + `GameRules/` + a **Stateless**-based `GameRuleStateMachine`, `Player` with
`PlayerInventory`/`CharacterInventory`, `ClanManager`, `TeamManager`, `Commands/`.

**Player persistence uses a Redis write-behind queue** (`PlayerSaveService`, `PlayerSaveWriter`,
`PlayerSaveFlushService`, `PlayerSaveSnapshot`): saves are batched and clean players are skipped, so
writes stay off the hot path. Don't reintroduce synchronous per-mutation DB writes.

**Plugins** (`src/plugins/`) are drop-in DLLs implementing `IPlugin`, discovered by reflection scan
(`ScanPluginHost`) from the server's `plugins/` folder — no server rebuild needed. They extend via DI
registration plus static hook events (`RoomManager.RoomCreateHook`, `Channel.JoinHook`,
`GameRuleBase.CanStartGameHook`, …). The `{Auth,Chat,Game,Relay}PluginBase.targets` add a
`Private="False"` reference to the host server, which is why plugin output contains no
`OpenS4L.Server.*.dll` — that is intentional, don't "fix" it. Bundled: WebApi (Kestrel HTTP API over
the game server), SoloMode, EquipLimitExtended, ExamplePlugin. See `Server/PLUGINS.md`.

**Config** is HJSON: one `config.hjson` per server (source copy next to `Program.cs`, deployment
copies under `Server/Docker/config/<server>/`).

## Conventions and gotchas

- Ported code is legacy-shaped: `<Nullable>` is off, `LangVersion latest`, `NoWarn 1998`. Match the
  surrounding style rather than modernizing opportunistically.
- **Mapping is Mapperly** (source-generated), and the differential tests assert the new mappers
  produce byte-identical output to the old ExpressMapper config. Several legacy mappings are
  *wrong* and are reproduced faithfully on purpose — they're tabulated in
  `Server/opens4l/tests/OpenS4L.Server.Mapping.Tests/README.md`. If you fix one, update the mapper,
  the test and that table together; never let a fix slip in as a migration side effect.
- Same for the other pinned latent bugs (WebApi `/gamedata/items/{id}` throwing instead of 404,
  `PeerId.Equals(object)`, `ClubCreationDateSerializer` timezone loss) — the tests pin *actual*
  behaviour, so a change there must flip a test deliberately.
- ExpressMapper's `Mapper` is global static state; differential tests `Mapper.Reset()` and are in
  `[Collection("Serial")]`.
- WebApi DTO contracts are Verify snapshots (`*.verified.txt`). A first run writes `.received.txt` —
  inspect, then rename if intended.
- Test harnesses to reuse rather than reinvent: `GameTestContext`/`ChatTestContext` (full DI graph
  with in-memory Foundatio + EF InMemory), `FakeSocketChannel`/`FakeSessionManager` (drive real
  handlers over an in-memory ProudNet transport), `PostgresFixture` (template-database cloning for
  the `ExecuteUpdate`/`ExecuteDelete` paths that InMemory can't run), `ManualSchedulerService`.
- EF Core: use `ExecuteUpdateAsync`/`ExecuteDeleteAsync` (the Z.EntityFramework.Plus
  `BatchUpdate`/`BatchDelete` calls were removed). Migrations are Postgres-only.
- No client assets or game data are in this repo, and none should be added.

## Tools structure

The Avalonia tools share three libraries that live inside `Tools/s4l-resource-tool/src/`:
`S4League.Resource` (the `resource.s4hd` archive format + crypto), `S4League.Scn` (scene/model/
animation parsing), `S4League.View` (renderer, texture loading, 3D preview control). Changes there
affect every tool.

`Tools/s4l-resource-tool/src/S4League.Resource/MiniLzo.cs` is **GPL v2+** (C# LZO port). It is
compiled into the resource tool, so distributed resource-tool binaries fall under GPLv2 while the
rest of the repo is MIT. Keep GPL code isolated in its own files; see `CONTRIBUTING.md` before
adding any copyleft dependency.
