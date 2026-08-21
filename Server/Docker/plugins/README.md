# plugins/

Per-server runtime plugin directories, mounted into each server container at `/app/plugins`.

- `plugins/auth/`, `plugins/chat/`, `plugins/game/`, `plugins/relay/` — one per server.
- Plugins are **server-specific** (each references the host server assembly, e.g.
  `OpenS4L.Server.Game`), so each server only loads its own folder. The bundled plugins
  (`EquipLimitExtended`, `ExamplePlugin`, `SoloMode`, `WebApi`) all target the **Game** server, so
  they live in `plugins/game/`.

These folders are **populated automatically** by `make plugins` (it copies the built
`OpenS4L.Plugins.*.dll` + their `.hjson` config + runtime deps into `plugins/game/`).

You can drop extra plugin DLLs (+ their config files) into the matching server folder manually.
Each server's `ScanPluginHost` loads every `*.dll` there and instantiates any type implementing
`IPlugin` (see `../PLUGINS.md`).
