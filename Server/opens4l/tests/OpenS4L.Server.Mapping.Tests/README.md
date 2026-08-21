# ExpressMapper → Mapperly migration

The WebApi / Game / Chat / Relay servers previously used **HolopaMir.ExpressMapper** to map
domain objects → network/API DTOs. This migration replaces it with **Riok.Mapperly**
(source-generated, no reflection).

The differential test suite in `tests/OpenS4L.Server.Mapping.Tests` runs the **live**
ExpressMapper config and the new Mapperly mappers on identical source objects and asserts the
serialized DTOs are byte-identical. That is the acceptance test for "identical results".

> The migration's job is **not to fix bugs silently**. If a legacy mapping produces a wrong
> value, Mapperly reproduces that *exact* wrong value so the wire format is unchanged, and the
> bug is recorded below so it can be fixed deliberately (not as a side effect of the migration).

## Latent bugs surfaced by the differential harness

These are pre-existing bugs in the legacy ExpressMapper config. The Mapperly mappers reproduce
them faithfully (to keep the migration wire-identical); each is a candidate for a deliberate,
separate fix.

| # | Mapping | Legacy behaviour (preserved) | What it should be | Location |
|---|---------|------------------------------|-------------------|----------|
| 1 | `PlayerItem → ItemDto.ExpireTime` (Game) | ExpressMapper assigns `src.ExpireDate.ToUnixTimeSeconds()` (a `long`) into a `DateTimeOffset`-typed member. It has no `long → DateTimeOffset` conversion, so it silently leaves the field at `DateTimeOffset.MinValue`. | The item's actual expiry as a timestamp. | `Server/opens4l/src/OpenS4L.Server.Game/Mappers/GameMapper.cs` (ToItemDto) |
| 2 | `MapInfo → MapDto.GameRules` (WebApi) | `Register<MapInfo, MapDto>()` had no explicit members, so ExpressMapper auto-mapped same-named props only. `MapInfo` has no `GameRules` property → `GameRules` is always `null`. | `GameRules` populated from `map.GameRule`. | `Server/opens4l/src/plugins/OpenS4L.Plugins.WebApi/Mappers/WebApiMapper.cs` (ToMapDto) |

## Other latent bugs surfaced by contract tests

Separate from the migration, the WebApi route contract tests (`WebApiEndpointContractTests`)
and the pure-logic tests surfaced bugs in non-mapping code. These are pinned as ACTUAL behaviour
so a future fix has to flip the test deliberately.

| # | Where | Bug | Test pinning it |
|---|-------|-----|-----------------|
| 3 | `Endpoints.cs` `/gamedata/items/{id}` | Uses `Items[(uint)itemId]` (dictionary indexer) → `KeyNotFoundException` for a missing id (500 under Kestrel) instead of the intended 404. The `== null` check after it is dead code. | `WebApiEndpointContractTests.Get_gamedata_items_byId_missing_throwsKeyNotFound` |
| 4 | `PeerId.Equals(object)` / `LongPeerId.Equals(object)` (Common) | Compares a packed `ushort`/`ulong` against a boxed `PeerId`/`LongPeerId`, which is never equal. Typed `Equals`/`==` operators work. | `CommonValueTypeTests.PeerId_equalsAndOperators` / `LongPeerId_equalsAndOperators` |
| 5 | `ClubCreationDateSerializer` (Network) | Stores local wall-clock ("yyyyMMddHHmmss") with NO timezone offset → lossy across timezones. | `WireSerializerRoundTripTests.ClubCreationDate_roundtrips` |

## How to add to this list

When a differential test fails, it prints the ExpressMapper output and the Mapperly output.
If the divergence is because the legacy config produced a clearly-wrong value (type mismatch,
never-populated member, dropped field), then:

1. Make the Mapperly mapper reproduce the legacy value (add a `// NOTE:` comment at the site).
2. Add a row to the table above.
3. Keep the differential test asserting equality — it is now a characterization test pinning
   the legacy behaviour, so a future deliberate fix has to update both the mapper and this table.

## Running the tests

```sh
cd Server/opens4l
dotnet test tests/OpenS4L.Server.Mapping.Tests/OpenS4L.Server.Mapping.Tests.csproj -c Release
```

Note: ExpressMapper's `Mapper` is global static state, so every differential test calls
`Mapper.Reset()` then re-registers the config from a clean slate. The test classes are marked
`[Collection("Serial")]` to avoid cross-test interference.

## Snapshot tests (Verify)

The WebApi contract is pinned with snapshot tests (`WebApiContractSnapshotTests`) using
**Verify.Xunit**. First run writes `.received.txt` files in the test directory; inspect them and,
if the new output is intended, rename to `.verified.txt` (or delete the `.received.txt` and the
test will fail showing the diff). Any accidental change to the HTTP DTO contract now shows up as
a snapshot diff.

## Transport-fake harness

The heavy Game/Chat handler + domain layer is now reachable in-process via a **fake-transport
harness** (no network):

- `FakeSocketChannel` — an in-memory DotNetty `ISocketChannel` over which real `ProudNet`
  Sessions construct. `ProudSession.Send(...)` hands outbound messages to the fake channel,
  which captures them for assertions.
- `FakeSessionManager` — an `ISessionManager` that raises connect/disconnect events.
- `ChatTestContext` / `GameTestContext` — build each server's DI graph with in-memory fakes
  (Foundatio `InMemoryMessageBus`/`InMemoryCacheClient`, EF `UseInMemoryDatabase` with a shared
  root, custom `Logging.ILogger`), auto-register handlers via `DefaultMessageHandlerResolver`,
  and register the `GameDataService` reflection-populated fixture. Tests drive real handlers via
  `OnHandle(new MessageContext { Session = realSession }, message)`.

This unlocks the login flows (Chat `AuthenticationHandler`, Game `AuthenticationHandler`),
message handlers (Chat/Deny/Friend/PrivateMessage/UserData/Clan), the Channel/ChannelManager
lifecycle, the firewall Rules, the hosted IPC services, manager persistence, the Game `Player`
domain methods, and the Game **Room/RoomManager/Channel + game-rule state-machine** lifecycle
(create/join/leave/change-rules/start-game), the Game Character layer (CharacterManager
create/select/remove + first-create handler), the Game ClanManager (name-check, clan create,
join/leave, club-info), the Game TeamManager/Team + briefing (change-team/mode, get-briefing,
change-master), the game state machine through Loading/Starting (via a ManualSchedulerService),
the GameMaster/Admin commands (gm/announce/kick + permission gate), and the Chat friend
accept/deny + player-save persistence flows.

## Real-Postgres harness (Testcontainers)

The EF `ExecuteUpdateAsync`/`ExecuteDeleteAsync` persistence paths (ClanManager approve/kick/ban/
unban/role/info, Chat friend-accept/deny offline branches, Mailbox delete) **cannot run on the
InMemory EF provider** — they execute as SQL and need a real relational provider. `PostgresFixture`
starts one shared **Postgres 16** container (Testcontainers), migrates a `s4l_template` database
once, and clones it per test context via `CREATE DATABASE ... TEMPLATE` for fast, fully-isolated
databases. `GameTestContext`/`ChatTestContext` accept an optional `PostgresDatabase`; the
Postgres-backed tests (`GameClanPostgresTests`) drive the previously-blocked paths against real
Npgsql. This also covers the whole `OpenS4L.Database` assembly via the applied migrations.

Measured coverage (via `make coverage`):
- **OpenS4L.Plugins.WebApi ~82%** ✅ (>80%; route handlers + WebApiService lifecycle + full mapper paths)
- **OpenS4L.Server.Chat ~81%** ✅ (>80%)
- **OpenS4L.Database ~96%**
- **OpenS4L.Common ~83%**
- **OpenS4L.Server.Game ~43%** (was 4.8%; the game-rule variants + Playing-state flow are the remaining blocker)

## Coverage

`OpenS4L.Common` (the self-contained config/value/converters library) is covered to **~81% line**
coverage. The two next-simplest projects were brought up next:
- **OpenS4L.Plugins.WebApi → ~45%** (all DTOs/options, plus in-process route contract tests over
  ASP.NET TestServer for the `/gamedata/*` endpoints).
- **OpenS4L.Server.Chat → ~14%** (pure-logic surface: PlayerSettingManager, converters, event
  args, AppOptions).

Run the suite with coverage collection via:

```sh
make coverage    # writes to Server/opens4l/tests/coverage-results/
```

Targets: the mapping differential harness, wire codecs, pure-logic types, the state machine,
the WebApi DTO/contract tests, and the Chat pure-logic layer. The heavy Game/Chat handler /
Room / Database / Network surface remains low coverage — it needs the transport-fake harness
(in-memory ProudNet session/transport over the full DI graph), which is the next phase.
