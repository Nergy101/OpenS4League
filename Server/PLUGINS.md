# OpenS4L Plugins

How the plugin system works, how to write a plugin, and how plugins are loaded and built.

## What a plugin is

A plugin is a **standalone assembly (DLL)** that implements the `IPlugin` lifecycle contract and
is loaded at runtime by the server's plugin host. Plugins let you extend the server — register DI
services, add custom game rules, and hook into game events — **without modifying the server core**.

## The contract

Every plugin implements `IPlugin` from `OpenS4L.Common.Plugins`:

```csharp
namespace OpenS4L.Plugins.YourPlugin;   // see "Naming" below

public class YourPlugin : IPlugin
{
    public void OnInitialize(IConfiguration appConfiguration) { }        // startup, has server config
    public void OnConfigure(IServiceCollection services) { }             // register services into DI
    public void OnShutdown() { }                                         // cleanup on shutdown
}
```

The `IPlugin` interface (in `src/OpenS4L.Common/Plugins/IPlugin.cs`):

| Member | When it runs | Purpose |
|---|---|---|
| `OnInitialize(IConfiguration)` | Once, at plugin load | Read config; set up anything needed before DI wiring |
| `OnConfigure(IServiceCollection)` | Once, during host setup | Register the plugin's own services (hosted services, options, game rules) |
| `OnShutdown()` | On server shutdown | Cleanup (unsubscribe hooks, dispose resources) |

## How plugins are discovered and loaded

The host is **`ScanPluginHost`** (`src/OpenS4L.Common/Plugins/ScanPluginHost.cs`), used by all four
servers. At startup each server calls:

```csharp
IPluginHost pluginHost = new ScanPluginHost();
pluginHost.Initialize(configuration, Path.Combine(BaseDirectory, "plugins"));
```

`ScanPluginHost`:

1. Scans the `plugins/` directory (next to the server) for every `*.dll`.
2. Loads each assembly via `AssemblyLoadContext`.
3. Finds every **public, concrete** type that implements `IPlugin` and has a public parameterless
   constructor (abstract / non-public / no-ctor types are skipped).
4. Instantiates one instance of each and drives `OnInitialize → OnConfigure → OnShutdown`.

So to deploy a plugin: build it, drop the DLL (plus any of its own non-server deps) into the
server's `plugins/` folder, and restart the server. No rebuild of the server is needed.

> This replaced the old MEF-based `MefPluginHost`. `ScanPluginHost` is a plain reflection scan —
> behavior-identical, with no `System.Composition` dependency.

## How a plugin integrates with the server

Plugins extend the server through two mechanisms:

1. **Dependency injection** — `OnConfigure(IServiceCollection services)` registers the plugin's
   services into the server's DI container (e.g. `services.AddTransient<...>()`,
   `services.AddHostedServiceEx<...>()`, `services.Configure<...>(...)`).

2. **Hooks** — the server exposes static hook events on its managers/rules that a plugin can
   subscribe to (typically from an `IHostedService` registered in `OnConfigure`), e.g.:
   `RoomManager.RoomCreateHook`, `Channel.JoinHook`, `GameRuleBase.CanStartGameHook`,
   `GameRuleBase.HasEnoughPlayersHook`, `GameRuleStateMachine.ScheduleTriggerHook`. Returning
   `true`/`false` from a hook handler controls whether the default behaviour runs.

## Naming convention

Plugin projects and namespaces use the **`OpenS4L.Plugins.*`** prefix, matching the server core's
`OpenS4L.*` convention:

- Project folder: `src/plugins/OpenS4L.Plugins.<Name>/`
- Project file: `OpenS4L.Plugins.<Name>.csproj`
- Root namespace: `OpenS4L.Plugins.<Name>` (sub-namespaces like `.Controllers` / `.Models` are fine)

Bundled plugins: `OpenS4L.Plugins.EquipLimitExtended`, `OpenS4L.Plugins.ExamplePlugin`,
`OpenS4L.Plugins.SoloMode`, `OpenS4L.Plugins.WebApi`.

## Writing a plugin — step by step

1. **Create the project** under `src/plugins/`:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net10.0</TargetFramework>
     </PropertyGroup>
     <Import Project="..\GamePluginBase.targets" />   <!-- or Auth/Chat/Relay variant -->
   </Project>
   ```
   (Add a `PackageReference` to `OpenS4L.Server.*` if the `.targets` import doesn't already give
   you the host server — the bundled `*PluginBase.targets` add that reference for you.)

2. **Implement `IPlugin`** (see contract above), in namespace `OpenS4L.Plugins.<Name>`.

3. **Register services** in `OnConfigure` (hosted services, options, etc.) and **subscribe to
   hooks** from a hosted service to change behaviour.

4. **Add the project** to `OpenS4L.Server.slnx` under the `/plugins/` folder.

5. **Build**: `make build` (from `Server/`). The plugin is produced as `OpenS4L.Plugins.<Name>.dll`.

6. **Deploy**: copy the DLL into the server's `plugins/` folder and start the server.

## Build wiring

- The `*PluginBase.targets` files (`src/plugins/{Auth,Chat,Game,Relay}PluginBase.targets`) add a
  `Private="False"` project reference to the matching host server, so a plugin can call the
  server's types at compile time **without** bundling the server DLLs into its output (they load
  from the already-running server's `bin/` at runtime). This is why plugin output directories
  don't contain `OpenS4L.Server.*.dll` / `OpenS4L.Blub.dll` — that's intentional.
- Each plugin targets `net10.0` and shares the server's other assemblies transitively.

## Reference: bundled plugins

| Plugin | What it does |
|---|---|
| `OpenS4L.Plugins.ExamplePlugin` | Reference plugin: registers a hosted service + a custom `Touchdown`-derived game rule, and shows hook subscription (room create, channel join, can-start-game, enough-players). |
| `OpenS4L.Plugins.EquipLimitExtended` | Extends equipment limits (`EquipLimitExtendedOptions`). |
| `OpenS4L.Plugins.SoloMode` | Adds a solo-mode variant (`SoloModeOptions`). |
| `OpenS4L.Plugins.WebApi` | HTTP API over the Game server: controllers + models exposing channels, players, rooms, game data, statistics. |

## Where the pieces live

- Contract: `src/OpenS4L.Common/Plugins/IPlugin.cs`, `IPluginHost.cs`
- Host: `src/OpenS4L.Common/Plugins/ScanPluginHost.cs`
- Build wiring: `src/plugins/*PluginBase.targets`
- Example: `src/plugins/OpenS4L.Plugins.ExamplePlugin/`
