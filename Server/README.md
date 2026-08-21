# Server — OpenS4L rebuild on .NET 10

The **OpenS4L** server: four servers (Auth, Chat, Game, Relay), rebuilt from the original
**NetspherePirates** S4 League emulator (which was on **.NET Core 2.1**) to modern **.NET 10**
(`net10.0`), with namespaces + project names renamed from `Netsphere` to **OpenS4L**.

This file is the living checklist for the rebuild.

## What this is

- **Vanilla path.** We compile the four servers from `opens4l/` source ourselves and run them with
  the already-exposed client data. The prebuilt **PirateShip** package (2.1 binaries) is not used
  — it cannot run on .NET 10.
- **Source-level migration, not roll-forward.** `.NET 8+` cannot host the 2.1-era EF Core
  (`Sequence contains more than one matching element`), so this is a real code rebuild.

## Layout

```
opens4l/
  OpenS4L.Server.slnx        # solution (new .slnx format)
  src/
    OpenS4L.Server.{Auth,Chat,Game,Relay}   # the four servers
    OpenS4L.{Common,Database,Network,Resource}
    OpenS4L.Blub/             # replaces the abandoned wtfblub BlubLib packages (ported)
    Logging/  ProudNet/       # support libs
    plugins/                  # OpenS4L.Plugins.{EquipLimitExtended,ExamplePlugin,SoloMode,WebApi} — see PLUGINS.md
  NuGet.Config                # feed config (nuget.org + abandoned wtfblub myget)
```

> **OpenS4L.Blub** (`src/OpenS4L.Blub/`) replaces the `BlubLib`, `BlubLib.Serialization` and
> `BlubLib.DotNetty` NuGet packages. Because the BlubLib **source was never published** (it's
> private/copyrighted), it was recovered by decompiling the compiled DLLs from the old build and
> ported into this project with namespaces renamed to `OpenS4L.Blub.*`. **Licensing caveat:** this
> reproduces BlubLib's (copyrighted) code for this self-hosted fork. It still depends on two
> external packages — **Sigil** (IL-emit used by the serializer) and **DotNetty** (transport, to be
> removed by the Phase 4 `System.IO.Pipelines` port).

## Migration checklist

### Phase 1 — Make it compile on net10.0 (dependency spike)

- [x] Copy `netsphere/` source into `Server/opens4l/`.
- [x] Retarget every `.csproj` to `net10.0`, `LangVersion latest`.
- [x] Rename namespaces `Netsphere.*` → `OpenS4L.*` and project folders/projects to `OpenS4L.*`.
- [x] Remove legacy cruft: `Rules.ruleset`/`Rules.targets` (obsolete analyzers), Nuke
      (`.build/`, `build.ps1/sh`, `.nuke`), `.gitlab-ci.yml`, `.DotSettings`, old `Dockerfile`,
      `.gitattributes`, old `NetspherePirates.sln`, `src/tools/` (legacy WinForms tools), `libs/`.
- [ ] Bump NuGet packages to modern versions (see §3.3 of the plan): EF Core, Npgsql, Foundatio,
      Newtonsoft, Serilog, Stateless, Polly, IdGen, BouncyCastle; drop MEF.
      *(Build already goes green on the legacy package versions; the bumps will clear the ~300
      vulnerability/deprecation warnings.)*
- [x] `dotnet build OpenS4L.Server.slnx -c Release` → **green** (0 errors).
- [ ] Smoke test: each server reaches `[INF Main] Press Ctrl + C to shutdown`.

### Phase 2 — Application fixes

- [ ] Hosting API: `IApplicationLifetime` → `IHostApplicationLifetime`; drop `UseConsoleLifetime()`.
- [ ] Plugin host: **`ScanPluginHost`** (simple DI-scan) replaces `MefPluginHost`
      (`opens4l/src/OpenS4L.Common/Plugins/ScanPluginHost.cs` — written; wire-up done in the 4
      `Program.cs`). Remove `System.Composition` package.
- [ ] Mapper: replace `HolopaMir.ExpressMapper` with **Mapperly** (source-generated) in each
      `ConfigureMapper()`.
- [ ] `Z.EntityFramework.Plus` (`BatchDelete`/`BatchUpdate`) → EF Core `ExecuteDelete`/`ExecuteUpdate`.

### Phase 3 — Database (PostgreSQL/Npgsql)

- [x] Provider: `Pomelo.EntityFrameworkCore.MySql` → **`Npgsql.EntityFrameworkCore.PostgreSQL`**;
      `UseMySql(...)` → `UseNpgsql(...)`. (Bumped EF Core 2.1 → **10**; replaced Z.EntityFramework.Plus
      `DeleteAsync`/`UpdateAsync` with native `ExecuteDeleteAsync`/`ExecuteUpdateAsync`.)
- [x] Regenerate migrations with `dotnet ef` (**PostgreSQL**, EF Core 10; old MySQL data dropped).
- [x] Verify against PostgreSQL in Docker (`Port=5432`): the Auth server connects, runs its EF Core
      migrations, and creates the schema (`accounts`, `bans`, `login_history`, …). Full boot needs
      the client data in `Docker/data/`.

### Phase 4 — Networking (long-run)

- [x] **Replace the wtfblub `BlubLib*` packages** with the in-repo `OpenS4L.Blub` project (ported,
      namespaces → `OpenS4L.Blub.*`). Build is green with no `BlubLib*.dll` shipped.
      *(Remaining external deps: **Sigil** for the serializer's IL-emit, and **DotNetty**.)*
- [ ] Port the **ProudNet** transport from DotNetty to **`System.IO.Pipelines`** (protocol kept
      byte-for-byte identical; version GUID `{beb92241-8333-4117-ab92-9b4af78c688f}`, client
      `0.8.32.26995`). This also lets us drop DotNetty and, ideally, Sigil.
- [ ] Keep or modernize `SharpLzo` / `Iconic.Zlib` wrappers.

### Phase 5 — End-to-end

- [ ] `Redis` on 6379, `PostgreSQL` on 5432.
- [ ] All 4 servers bind: `netstat -ano | grep -E ':2800[2-5]|:2900[01]'`.
- [ ] Client connects; login, character creation, room join (relay), shop purchase all succeed.

## Plugins

The server is extensible via plugins — standalone DLLs implementing `IPlugin`, loaded from the
server's `plugins/` folder by `ScanPluginHost`. See **[PLUGINS.md](PLUGINS.md)** for how they
work, how to write one, and the namespace convention (`OpenS4L.Plugins.*`).

## Building (Windows-first)

```bash
make build    # dotnet build -c Release
make server   # alias of build
make publish  # dotnet publish each of the 4 servers
make clean
```

> Targets use only portable `dotnet` commands, so they also run on macOS/Linux `make`.
