# OpenS4L Server

The OpenS4L server — a modern **.NET 10** rebuild of the NetspherePirates S4 League server
emulator. Four servers (Auth, Chat, Game, Relay) built from source and run with the exposed
client data.

- Namespaces & projects renamed from `Netsphere` → **OpenS4L**.
- Solution: `OpenS4L.Server.slnx` (new format).
- Database: PostgreSQL / Npgsql (see the migration checklist).

See the parent [`../README.md`](../README.md) for the build steps and the full migration
checklist.
