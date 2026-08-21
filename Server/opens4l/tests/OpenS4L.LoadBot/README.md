# OpenS4L.LoadBot — real-game-protocol simulated players

A load harness that connects simulated players to the OpenS4L servers **over the real ProudNet
wire protocol** (not HTTP, not a fake transport). Each bot is a genuine ProudNet client: it does
the handshake (RSA/AES/RC4 key exchange), logs into auth, connects to the game server, creates a
character + nickname on first login, and enters a channel. Because they are real protocol
clients, bots appear in the game server's `PlayerManager`/`ChannelService` and therefore show up
live in the admin console (`/statistics`, `/players`, `/channels`).

The `ProudNetClient` lives in `src/ProudNet/ProudNetClient.cs` and reuses the in-repo protocol
building blocks (`Crypt`, the core message types, `CoreMessageEncoder/Decoder`, the
`MessageFactory` dispatch) — the same ones the servers use, so wire-compatibility is guaranteed
by construction. This is the client-side counterpart to the servers' ProudNet reimplementation,
which ships only the server half.

> **Location**: this project lives under `tests/` (not `src/`) — it is test/diagnostic tooling,
> not a shipped server component. It is part of the solution's `/tests/` folder so `make server`
> builds it too, but it is never published or deployed.

## Build

Part of the solution (`OpenS4L.Server.slnx`), so `make server` / `make build` builds it too. Or
directly:

```sh
dotnet build src/OpenS4L.LoadBot/OpenS4L.LoadBot.csproj -c Release
```

## Run (against the local stack)

From `Server/Docker` (stack must be up):

```sh
make loadbot                        # 1 bot, channel 4, account admin/admin
make loadbot BOTS=20 STAY=120       # 20 bots for 2 minutes
make loadbot BOTS=5 CHANNEL=4 ACCOUNT=myuser PASSWORD=mypass

# Scenario: 10 players in every channel (discovers channels from /channels)
make provision-bots BOTS=110        # one account per bot
make loadbot SCENARIO=all-channels PER_CHANNEL=10 USERPREFIX=bot STAY=180

# Scenario: bots that also chat on the chat server
make loadbot SCENARIO=chat BOTS=10 USERPREFIX=bot STAY=120
```

Directly:

```sh
dotnet run --project tests/OpenS4L.LoadBot -c Release -- --count 3 --stay 60 --channel 4
dotnet run --project tests/OpenS4L.LoadBot -c Release -- --scenario all-channels --per-channel 10 --user-prefix bot
dotnet run --project tests/OpenS4L.LoadBot -c Release -- --scenario chat --count 10 --user-prefix bot
```

While it runs, watch the admin console / WebApi reflect the bots:

```sh
curl http://localhost:22000/statistics   # PlayersOnline = N
curl http://localhost:22000/players      # the bot players
curl http://localhost:22000/channels     # the joined channel's PlayersOnline
```

The process exits `0` when every bot reached the channel (exit `1` otherwise).

## Options

| Flag              | Default             | Meaning |
|-------------------|---------------------|---------|
| `--auth`          | `127.0.0.1:28002`   | Auth server endpoint |
| `--game`          | from server list    | Game server endpoint (override for remote stacks) |
| `--pass`          | `admin`             | Account password |
| `--count`         | `1`                 | Number of bots (1-1000) |
| `--channel`       | `4`                 | Channel to enter |
| `--nick-prefix`   | `bot`               | Nickname prefix (default `bot` → `bot0`, `bot1`, …) |
| `--user-prefix`   | —                   | Each bot logs in as its own account `{prefix}{i}` (provision with `make provision-bots BOTS=N`) |
| `--stay`          | `0`                 | Seconds to stay online (0 = forever) |
| `--scenario`      | `single-channel`    | `single-channel` \| `all-channels` \| `chat` |
| `--per-channel`   | `0`                 | Players per channel (for `all-channels`) |
| `--chat-endpoint` | `127.0.0.1:28003`   | Chat server endpoint (for `chat`) |

## Notes / gotchas

- **Scenarios** (extensible via the `IScenario` interface in `Scenario.cs`):
  - `single-channel` — N bots into one channel.
  - `all-channels` — P players in every channel the game exposes (via `/channels`); with 11
    channels and P=10 that's 110 bots.
  - `chat` — N bots that also log into the chat server and send channel chat every few seconds.
    Each bot still logs into auth + game first (needed so its player record + nickname exist, and
    so it's registered in the game `PlayerManager` — chat login looks the player up there).
- **N concurrent bots need N accounts.** The game server rejects a second concurrent login for
  the same account (`TerminateOtherConnection`). Provision `bot0..botN-1` with
  `make provision-bots BOTS=N`, then pass `--user-prefix bot`.
- **Nicknames** must obey `NickRestrictions` (length 4-30, ASCII, max 3 consecutive identical
  chars). `bot0000` is rejected (`MaxRepeat: 3`); the default `bot<i>` is fine to 999 bots.
- **`MaxSessions`** in `config/game/config.hjson` caps concurrent game connections (default 100).
  Raise it (e.g. to 200) to run >100 concurrent bots; otherwise you get `ServerFull` rejections.
- The chat bot keeps its game connection open and in a channel while chatting — a chat bot that
  skips the game channel gets dropped because chat login can't find the player.
