# S4L Admin Console

A web dashboard for the **OpenS4L** server stack. It reads the four server config files,
probes their TCP listeners for liveness, tails their log files, and presents everything in a
React dashboard — with no changes needed to the servers themselves.

## Stack

- **Backend** — zero-dependency Node `http` server (`server/server.js`). No `pnpm install`
  required for the backend.
- **Frontend** — React + Vite (`web/`, pnpm).

## Quick start

```bash
# 1. Build the React frontend (first time and after edits)
cd web
pnpm install
pnpm run build

# 2. Run the backend (serves the built dashboard too)
cd ..
node server/server.js
# -> http://localhost:8020
```

Open http://localhost:8020 — you should see a card for each of the four servers
(Auth / Chat / Game / Relay) with an online/offline dot and a collapsible, auto-refreshing log
panel. If the OpenS4L servers are not currently running, every card will correctly show
**Offline**.

## Dev mode

Run the backend in one terminal:

```bash
node server/server.js
```

Run the Vite dev server (proxies `/api` to the backend on port 8020) in another:

```bash
cd web
pnpm run dev
# -> http://localhost:5173
```

## API

| Endpoint | Description |
|---|---|
| `GET /api/servers` | Status of all four servers: name, address, port, `up`, latency, whether config/logs exist. |
| `GET /api/servers/:name/logs?lines=200` | Tail of a server's log files (`name` is case-insensitive: auth, chat, game, relay). |
| `GET /api/health` | Liveness check for the backend. |

## Configuration

- Port: set the `PORT` env var (default `8020`).
- Server configs are read from `Server/opens4l/src/OpenS4L.Server.<Name>/config.hjson`;
  logs are tailed from `Server/opens4l/dist/<name>/logs`.
