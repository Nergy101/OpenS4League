# OpenS4L — k6 load tests

[k6](https://k6.io) load tests for the **WebApi HTTP plugin** (port `22000`). They hit the
read-only observability/admin endpoints every deployment serves under load: `/statistics`,
`/channels`, `/players`, `/gamedata/maps`.

> **Scope / honest caveat.** The four game servers (auth, chat, game, relay) speak the
> custom **ProudNet binary protocol** over TCP/UDP, which k6 cannot drive. So these tests
> exercise the **HTTP plane** (the WebApi and its admin/observability surface), not real
> gameplay. Real player load would need a ProudNet bot harness — a separate effort. The
> admin write endpoints (`/admin/kick|ban|roomkick|closeroom`) are also excluded from the
> steady-state test because they need live players and `ban` writes to the DB.

## How it runs

k6 runs from the official `grafana/k6` Docker image, piped the self-contained
`script.js` over stdin. No local k6 install, and no volume mounts — so the exact same
commands work on **macOS, Windows (git-bash/MSYS), and Linux / CI**.

## Run against the local Docker stack

Make sure the stack is up and the WebApi is reachable first:

```sh
make up           # (from Server/Docker) — or `make bootstrap` for a first bring-up
curl http://localhost:22000/statistics   # expect {"Uptime":...,"PlayersOnline":...}
```

Then, from `Server/Docker`:

```sh
make loadtest:smoke   # quick sanity: 5 VUs x 20 iterations
make loadtest:load    # ramp to 50 VUs, hold 3m, ramp down
make loadtest:soak    # hold 30 VUs for 30m (leak / latency-creep check)
```

The default target is `http://host.docker.internal:22000` — the Docker-host alias, so the
k6 container reaches the stack's port published on `127.0.0.1:22000`. This works on
macOS/Windows Docker Desktop out of the box.

## Run against a cloud / remote deployment (e.g. Azure)

Just point `TARGET_URL` at your deployment. The WebApi is normally fronted by a load
balancer on 443:

```sh
make loadtest:load TARGET_URL=https://game.example.com
```

Run the load generator from a host that can reach it (a test VM in the same region, or CI
on a runner near the deployment). The k6 container only needs outbound HTTP(S) to
`TARGET_URL` — nothing else.

## Parameters (k6 env vars)

| Var           | smoke | load    | soak        |
|---------------|-------|---------|-------------|
| `LOAD_PROFILE`| smoke | load    | soak        |
| `VUS`         | 5     | 50      | 30          |
| `DURATION`    | —     | —       | 30m         |
| `ITERATIONS`  | 20    | —       | —           |
| `TARGET_URL`  | local | local   | local       |

Override any of them via the Makefile, e.g.:

```sh
make loadtest:soak VUS=80 DURATION=1h TARGET_URL=https://game.example.com
```

## Thresholds

The script fails the run if either threshold is breached:

- `http_req_failed` rate < 1%
- `http_req_duration` p(95) < 1000 ms

Tune them in `script.js` (`options.thresholds`) — raise the p(95) budget for a slow
network between the generator and a far-away cloud, or tighten it for a local benchmark.
