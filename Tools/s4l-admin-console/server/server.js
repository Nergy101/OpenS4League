'use strict';
// OpenS4L Server Admin Console - backend
// Zero-dependency Node http server: reads the four OpenS4L server config files,
// probes their TCP listeners for liveness, tails their log files, pulls live
// stats from the Game server's WebApi plugin, keeps a rolling history for
// graphs, and serves the built React dashboard.
// Run:  node server.js   (PORT / GAME_API env optional)

const http = require('http');
const fs = require('fs');
const path = require('path');
const net = require('net');
const { execFile } = require('child_process');

const PORT = process.env.PORT || 8020;
// The Game server's WebApi plugin (Kestrel) — serves /statistics, /channels, /players, /rooms.
const GAME_API = process.env.GAME_API || 'http://127.0.0.1:22000';
// The Chat server's tiny metrics endpoint — serves /statistics with MessagesSent etc.
const CHAT_API = process.env.CHAT_API || 'http://127.0.0.1:28006';

// Repo root is three levels up from this file (server/ -> s4l-admin-console -> Tools -> OpenS4L).
const REPO = path.join(__dirname, '..', '..', '..');

const SERVERS = [
  { key: 'Auth',  name: 'Auth'  },
  { key: 'Chat',  name: 'Chat'  },
  { key: 'Game',  name: 'Game'  },
  { key: 'Relay', name: 'Relay' },
];

function configPath(key) {
  return path.join(REPO, 'Server', 'opens4l', 'src', `OpenS4L.Server.${key}`, 'config.hjson');
}
function logDirPath(key) {
  return path.join(REPO, 'Server', 'opens4l', 'dist', key.toLowerCase(), 'logs');
}

// HJSON is JSON-with-comments/unquoted-keys; we only need the Listener line, so regex is enough.
function parseConfig(key) {
  const file = configPath(key);
  if (!fs.existsSync(file)) return null;
  let text;
  try { text = fs.readFileSync(file, 'utf8'); } catch { return null; }
  // Matches "Listener: 127.0.0.1:28002" both at top level and nested under Network.
  const m = /Listener\s*:\s*["']?([0-9.]+):(\d+)/.exec(text);
  if (!m) return null;
  return { address: m[1], port: parseInt(m[2], 10), file };
}

function probePort(address, port, timeoutMs = 800) {
  return new Promise((resolve) => {
    const start = Date.now();
    const socket = new net.Socket();
    let done = false;
    const finish = (up) => {
      if (done) return;
      done = true;
      socket.destroy();
      resolve({ up, latencyMs: Date.now() - start });
    };
    socket.setTimeout(timeoutMs);
    socket.once('connect', () => finish(true));
    socket.once('timeout', () => finish(false));
    socket.once('error', () => finish(false));
    socket.connect(port, address);
  });
}

// Minimal JSON GET helper for a WebApi / metrics HTTP endpoint (avoids external deps).
function apiGet(urlPath, timeoutMs = 3000, base = GAME_API) {
  return new Promise((resolve) => {
    const req = http.get(base + urlPath, { timeout: timeoutMs }, (res) => {
      let body = '';
      res.setEncoding('utf8');
      res.on('data', (c) => { body += c; });
      res.on('end', () => {
        if (res.statusCode !== 200) { resolve({ ok: false, status: res.statusCode }); return; }
        try { resolve({ ok: true, data: JSON.parse(body) }); }
        catch { resolve({ ok: false, status: res.statusCode, parse: true }); }
      });
    });
    req.on('timeout', () => { req.destroy(); resolve({ ok: false, timeout: true }); });
    req.on('error', () => resolve({ ok: false, error: true }));
  });
}

async function gameStats() {
  const base = { reachable: false, playersOnline: 0, peakPlayers: 0, uptime: 0, channels: [], players: [], rooms: [] };
  const [stat, channels, players] = await Promise.all([
    apiGet('/statistics'),
    apiGet('/channels'),
    apiGet('/players'),
  ]);
  if (!stat.ok) return base;
  base.reachable = true;
  base.playersOnline = (stat.data && stat.data.PlayersOnline) || 0;
  base.peakPlayers = (stat.data && stat.data.PeakPlayers) || 0;
  base.uptime = (stat.data && stat.data.Uptime) || 0;
  base.channels = (channels.ok && Array.isArray(channels.data)) ? channels.data : [];
  base.players = (players.ok && Array.isArray(players.data)) ? players.data : [];

  // Aggregate per-channel room + player counts.
  const channelsWithStats = [];
  for (const ch of base.channels) {
    const rooms = ch.Id != null ? await apiGet(`/rooms/${ch.Id}`) : null;
    const roomArr = (rooms && rooms.ok && Array.isArray(rooms.data)) ? rooms.data : [];
    channelsWithStats.push({
      id: ch.Id, name: ch.Name, playersOnline: ch.PlayersOnline || 0,
      playerLimit: ch.PlayerLimit || 0, roomCount: roomArr.length,
      playersInRooms: roomArr.reduce((a, r) => a + ((r.Players && r.Players.length) || 0), 0),
    });
  }
  base.channels = channelsWithStats;
  base.roomCount = base.channels.reduce((a, c) => a + c.roomCount, 0);
  base.playersInRooms = base.channels.reduce((a, c) => a + c.playersInRooms, 0);
  return base;
}

// Live chat metrics from the chat server's tiny /statistics endpoint (messages sent, etc.).
async function chatStats() {
  const base = { reachable: false, messagesSent: 0, whispersSent: 0, uptime: 0 };
  const res = await apiGet('/statistics', 3000, CHAT_API);
  if (!res.ok || !res.data) return base;
  base.reachable = true;
  base.messagesSent = res.data.MessagesSent || 0;
  base.whispersSent = res.data.WhispersSent || 0;
  base.uptime = res.data.Uptime || 0;
  return base;
}

async function serverStatus(server) {
  const cfg = parseConfig(server.key);
  const base = {
    name: server.name, key: server.key, address: null, port: null, up: false,
    latencyMs: null, configFound: false, logExists: false,
    logDir: logDirPath(server.key), stats: null, sessions: null, sessionsTotal: null, uptime: null,
    chatStats: null, messagesSent: null,
  };
  if (!cfg) return base;
  base.configFound = true;
  base.address = cfg.address;
  base.port = cfg.port;
  base.logExists = fs.existsSync(base.logDir) && fs.readdirSync(base.logDir).some((f) => /.log/i.test(f));
  const probe = await probePort(cfg.address, cfg.port);
  base.up = probe.up;
  base.latencyMs = probe.latencyMs;
  // Only the Game server exposes the WebApi stats plugin.
  if (server.key === 'Game') {
    base.stats = await gameStats();
    if (base.stats && base.stats.uptime) base.uptime = base.stats.uptime;
  } else if (server.key === 'Chat') {
    // Chat exposes a tiny metrics endpoint (messages sent).
    const cs = await chatStats();
    base.chatStats = cs;
    base.messagesSent = cs.reachable ? cs.messagesSent : null;
    base.uptime = (cs.reachable && cs.uptime) ? cs.uptime : deriveUptime(server.key);
  } else {
    base.uptime = deriveUptime(server.key);
  }
  // Derive live session count from the service's connection log (works for all four).
  const sess = deriveSessions(server.key);
  base.sessions = sess.sessions;
  base.sessionsTotal = sess.total;
  return base;
}

// Uptime (seconds) = now - the server's last "Starting - tcp=" boot event in its log.
// The log file appends across restarts within a day, so the most recent one is the live process.
function deriveUptime(key) {
  const dir = logDirPath(key);
  if (!fs.existsSync(dir)) return null;
  const files = fs.readdirSync(dir)
    .filter((f) => /\.json($|[^a-z0-9])/i.test(f) && fs.statSync(path.join(dir, f)).isFile())
    .map((f) => ({ f, t: fs.statSync(path.join(dir, f)).mtimeMs }))
    .sort((a, b) => a.t - b.t);
  let startMs = null;
  for (let i = files.length - 1; i >= 0 && startMs === null; i--) {
    let data;
    try { data = fs.readFileSync(path.join(dir, files[i].f), 'utf8'); } catch { continue; }
    // Scan in order; keep updating startMs on each "Starting - tcp=" so we end on the last one.
    for (const line of data.split(/\r?\n/)) {
      if (!line.includes('Starting - tcp=')) continue;
      try {
        const j = JSON.parse(line);
        const ts = j.Timestamp || j['@t'] || null;
        if (ts) startMs = new Date(ts).getTime();
      } catch { /* non-JSON line */ }
    }
  }
  if (startMs === null) return null;
  return Math.max(0, Math.floor((Date.now() - startMs) / 1000));
}

// Parse the newest log file(s) for ProudNet connect/disconnect events and derive
// (a) current open sessions (connects minus disconnects) and (b) total connects seen.
function deriveSessions(key) {
  const dir = logDirPath(key);
  const out = { sessions: 0, total: 0 };
  if (!fs.existsSync(dir)) return out;
  const files = fs.readdirSync(dir)
    .filter((f) => /\.json($|[^a-z0-9])/i.test(f) && fs.statSync(path.join(dir, f)).isFile())
    .map((f) => ({ f, t: fs.statSync(path.join(dir, f)).mtimeMs }))
    .sort((a, b) => a.t - b.t);
  let connects = 0, disconnects = 0;
  for (const { f } of files) {
    let data;
    try { data = fs.readFileSync(path.join(dir, f), 'utf8'); } catch { continue; }
    // Scan line by line; count message templates by substring (robust to both JSON and text logs).
    for (const line of data.split(/\r?\n/)) {
      if (!line.trim()) continue;
      if (line.includes('New incoming client') || line.includes('New incoming client(')) connects++;
      else if (line.includes('disconnected')) disconnects++;
    }
  }
  out.total = connects;
  out.sessions = Math.max(0, connects - disconnects);
  return out;
}

function readLogs(key, lines = 200) {
  const dir = logDirPath(key);
  if (!fs.existsSync(dir)) return { name: key, path: dir, lines: [] };
  const files = fs.readdirSync(dir)
    .filter((f) => /\.log($|[^a-z0-9])/i.test(f) && fs.statSync(path.join(dir, f)).isFile())
    .map((f) => ({ f, t: fs.statSync(path.join(dir, f)).mtimeMs }))
    .sort((a, b) => a.t - b.t);
  if (files.length === 0) return { name: key, path: dir, lines: [] };

  const out = [];
  for (let i = files.length - 1; i >= 0 && out.length < lines; i--) {
    const fp = path.join(dir, files[i].f);
    const data = fs.readFileSync(fp, 'utf8');
    const raw = data.split(/\r?\n/).filter((l) => l.trim().length > 0);
    const parsed = raw.map((line) => {
      try {
        const j = JSON.parse(line);
        const timestamp = j['@t'] || j.timestamp || j.Time || null;
        const text = j['@m'] || j.message || j.Message || line;
        return { timestamp, text: typeof text === 'string' ? text : JSON.stringify(text) };
      } catch {
        return { timestamp: null, text: line };
      }
    });
    out.unshift(...parsed);
  }
  return { name: key, path: dir, lines: out.slice(-lines) };
}

// ---- Rolling history for graphs ----
// Each sample: { t, playersOnline, up: {Auth,Chat,Game,Relay}, latency: {...} }
const history = [];
const HISTORY_MAX = 300; // ~15 min at 3s polls

function recordHistory(statuses) {
  const t = Date.now();
  const up = {};
  const latency = {};
  const sessions = {};
  let players = 0;
  for (const s of statuses) {
    up[s.key] = s.up;
    latency[s.key] = s.latencyMs;
    sessions[s.key] = s.sessions != null ? s.sessions : 0;
    if (s.stats) players = s.stats.playersOnline;
  }
  history.push({ t, playersOnline: players, up, latency, sessions });
  if (history.length > HISTORY_MAX) history.shift();
}

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.ico': 'image/x-icon',
  '.map': 'application/json',
};

function serveStatic(res, urlPath) {
  const dist = path.join(__dirname, '..', 'web', 'dist');
  let file = path.normalize(path.join(dist, urlPath));
  if (!file.startsWith(dist)) { res.writeHead(403); res.end('Forbidden'); return; }
  if (!fs.existsSync(file) || fs.statSync(file).isDirectory()) file = path.join(dist, 'index.html');
  if (!fs.existsSync(file)) { res.writeHead(404); res.end('Not found'); return; }
  const ext = path.extname(file).toLowerCase();
  res.writeHead(200, { 'Content-Type': MIME[ext] || 'application/octet-stream' });
  fs.createReadStream(file).pipe(res);
}

function sendJson(res, obj) {
  res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
  res.end(JSON.stringify(obj));
}

// ---- Database (read-only) API via the opens4l-postgres container ----
const DB_NAMES = ['auth', 'game'];
const PG_CONTAINER = process.env.PG_CONTAINER || 'opens4l-postgres';
const PG_USER = process.env.PG_USER || 'postgres';

function psql(db, sql) {
  return new Promise((resolve) => {
    // docker exec opens4l-postgres psql -U postgres -d <db> -A -t -F <sep> -c <sql>
    execFile('docker', ['exec', PG_CONTAINER, 'psql', '-U', PG_USER, '-d', db, '-A', '-t', '-F', '\t', '-c', sql],
      { timeout: 15000, maxBuffer: 50 * 1024 * 1024 },
      (err, stdout, stderr) => {
        if (err) return resolve({ ok: false, error: (stderr || err.message).trim() });
        resolve({ ok: true, stdout });
      });
  });
}

// Parse TSV output (from psql -A -t -F \t) into rows.
function parseTsv(stdout) {
  const lines = stdout.split('\n').filter((l) => l.trim().length > 0);
  return lines.map((l) => l.split('\t'));
}

async function listTables() {
  const dbs = [];
  for (const db of DB_NAMES) {
    const res = await psql(db,
      "SELECT table_name FROM information_schema.tables WHERE table_schema='public' ORDER BY table_name");
    if (!res.ok) { dbs.push({ name: db, ok: false, error: res.error, tables: [] }); continue; }
    const tables = parseTsv(res.stdout).map((r) => r[0]).filter(Boolean);
    dbs.push({ name: db, ok: true, tables });
  }
  return dbs;
}

// Column metadata (name, data_type) + a row estimate for a table.
async function tableMeta(db, table) {
  const res = await psql(db,
    `SELECT column_name, data_type FROM information_schema.columns WHERE table_schema='public' AND table_name='${table}' ORDER BY ordinal_position`);
  if (!res.ok) return { ok: false, error: res.error, columns: [], rows: 0 };
  const columns = parseTsv(res.stdout).map((r) => ({ name: r[0], type: r[1] || '' }));
  const cnt = await psql(db, `SELECT count(*) FROM "${table}"`);
  let rows = 0;
  if (cnt.ok) { const n = parseInt(cnt.stdout.trim(), 10); if (!isNaN(n)) rows = n; }
  return { ok: true, columns, rows };
}

// Read a table's data (read-only, capped) as CSV, parsed into columns + rows.
async function tableData(db, table, limit = 100, offset = 0) {
  // Guard: table must exist in this db (whitelist against injection).
  const listed = await listTables();
  const dbEntry = listed.find((d) => d.name === db);
  if (!dbEntry || !dbEntry.ok || !dbEntry.tables.includes(table)) {
    return { ok: false, error: `Unknown table ${db}.${table}` };
  }
  const sql = `COPY (SELECT * FROM "${table}" ORDER BY 1 NULLS LAST LIMIT ${Math.max(1, Math.min(1000, limit))} OFFSET ${Math.max(0, offset)}) TO STDOUT WITH (FORMAT csv, HEADER true)`;
  const res = await psql(db, sql);
  if (!res.ok) return { ok: false, error: res.error };
  // Parse CSV (headers + rows).
  const rows = [];
  let headers = [];
  const lines = res.stdout.split('\n').filter((l) => l.trim().length > 0);
  if (lines.length > 0) headers = parseCsvLine(lines[0]);
  for (let i = 1; i < lines.length; i++) rows.push(parseCsvLine(lines[i]));
  return { ok: true, columns: headers, rows, limit, offset, count: rows.length };
}

// Minimal RFC4180 CSV line parser (handles quotes, commas, escaped quotes, newlines-in-field are not supported by psql COPY with header single-line fields here).
function parseCsvLine(line) {
  const out = [];
  let cur = '';
  let inQ = false;
  for (let i = 0; i < line.length; i++) {
    const c = line[i];
    if (inQ) {
      if (c === '"') {
        if (line[i + 1] === '"') { cur += '"'; i++; }
        else inQ = false;
      } else cur += c;
    } else if (c === '"') inQ = true;
    else if (c === ',') { out.push(cur); cur = ''; }
    else cur += c;
  }
  out.push(cur);
  return out;
}

// ---- Moderation proxy (forwards to the Game WebApi admin endpoints) ----
// The browser calls these same-origin on 8020; the backend forwards to GAME_API
// (the Game server's WebApi on port 22000), avoiding CORS.
function proxyAdmin(action, formData, jsonBody) {
  return new Promise((resolve) => {
    const target = GAME_API + '/admin/' + action;
    let body;
    let contentType;
    if (formData) {
      body = new URLSearchParams(formData).toString();
      contentType = 'application/x-www-form-urlencoded';
    } else {
      body = JSON.stringify(jsonBody);
      contentType = 'application/json';
    }
    const req = http.request(target, {
      method: 'POST',
      timeout: 8000,
      headers: { 'Content-Type': contentType, 'Content-Length': Buffer.byteLength(body) },
    }, (res) => {
      let data = '';
      res.setEncoding('utf8');
      res.on('data', (c) => { data += c; });
      res.on('end', () => resolve({ status: res.statusCode, body: data }));
    });
    req.on('timeout', () => { req.destroy(); resolve({ status: 504, body: 'timeout' }); });
    req.on('error', (e) => resolve({ status: 502, body: String(e.message) }));
    req.write(body);
    req.end();
  });
}

function readBody(req) {
  return new Promise((resolve) => {
    let body = '';
    req.setEncoding('utf8');
    req.on('data', (c) => { body += c; if (body.length > 1e6) req.destroy(); });
    req.on('end', () => resolve(body));
    req.on('error', () => resolve(''));
  });
}

async function handleAdmin(req, res, action) {
  const body = await readBody(req);
  let payload = {};
  try { payload = body ? JSON.parse(body) : {}; } catch { /* fall back */ }
  const result = await proxyAdmin(action, null, payload);
  res.writeHead(result.status || 200, { 'Content-Type': 'application/json' });
  res.end(JSON.stringify({ ok: (result.status || 0) >= 200 && (result.status || 0) < 300, status: result.status, response: result.body }));
}

// ---- Redis read-only API via the opens4l-redis container ----
// Zero-dep: shells out to `redis-cli` inside the running container, mirroring the Postgres
// `psql` approach above. Read-only (SCAN/TYPE/TTL/GET/LRANGE/etc.) — never writes.
const REDIS_CONTAINER = process.env.REDIS_CONTAINER || 'opens4l-redis';
// Foundatio IQueue namespaces we want surfaced as "Queues" (e.g. the write-behind player-saves queue).
const REDIS_QUEUES = (process.env.REDIS_QUEUES || 'opens4l:player-saves').split(',').map((s) => s.trim()).filter(Boolean);

function redisCli(args, stdin = null) {
  return new Promise((resolve) => {
    const cmdArgs = ['exec'];
    if (stdin != null) cmdArgs.push('-i');
    cmdArgs.push(REDIS_CONTAINER, 'redis-cli', ...args);
    const child = execFile('docker', cmdArgs, { timeout: 15000, maxBuffer: 50 * 1024 * 1024 },
      (err, stdout, stderr) => {
        if (err) return resolve({ ok: false, error: (stderr || err.message).trim() });
        resolve({ ok: true, stdout });
      });
    if (stdin != null) { child.stdin.write(stdin); child.stdin.end(); }
  });
}

// redis-cli-safe quoted key for stdin-batched commands.
const rq = (k) => JSON.stringify(k);
const isQueueKey = (k) => REDIS_QUEUES.some((qn) => k === qn || k.startsWith(qn + ':'));

async function redisInfo() {
  const r = await redisCli(['INFO']);
  if (!r.ok) return { ok: false, error: r.error };
  const sections = {};
  let cur = null;
  for (const line of r.stdout.split('\n')) {
    const s = line.trim();
    if (s.startsWith('#')) { cur = s.slice(1).trim().toLowerCase(); sections[cur] = {}; continue; }
    if (cur && s.includes(':')) { const i = s.indexOf(':'); sections[cur][s.slice(0, i).trim()] = s.slice(i + 1).trim(); }
  }
  const mem = sections.memory || {};
  const cli = sections.clients || {};
  const st = sections.stats || {};
  const server = sections.server || {};
  let db0 = null;
  if (sections.keyspace && sections.keyspace.db0) {
    const m = /keys=(\d+).*?expires=(\d+)/.exec(sections.keyspace.db0);
    if (m) db0 = { keys: +m[1], expires: +m[2] };
  }
  return {
    ok: true, info: {
      version: server.redis_version || null,
      uptimeSec: server.uptime_in_seconds != null ? +server.uptime_in_seconds : null,
      usedMemory: mem.used_memory_human || null,
      usedMemoryPeak: mem.used_memory_peak_human || null,
      connectedClients: cli.connected_clients != null ? +cli.connected_clients : null,
      opsPerSec: st.instantaneous_ops_per_sec != null ? +st.instantaneous_ops_per_sec : null,
      totalCommands: st.total_commands_processed != null ? +st.total_commands_processed : null,
      hits: st.keyspace_hits != null ? +st.keyspace_hits : null,
      misses: st.keyspace_misses != null ? +st.keyspace_misses : null,
      db0,
    },
  };
}

// SCAN keys + batched TYPE/TTL/size. Capped so a huge keyspace stays cheap.
async function redisKeys() {
  const scan = await redisCli(['--scan', '--pattern', '*']);
  if (!scan.ok) return { ok: false, error: scan.error };
  const all = scan.stdout.split('\n').map((s) => s.trim()).filter(Boolean);
  const keys = all.slice(0, 200);
  if (keys.length === 0) return { ok: true, keys: [] };

  let ttCmd = '';
  for (const k of keys) ttCmd += `TYPE ${rq(k)}\nTTL ${rq(k)}\n`;
  const tt = await redisCli([], ttCmd);
  if (!tt.ok) return { ok: false, error: tt.error };
  const ttLines = tt.stdout.split('\n').map((s) => s.trim());
  const types = keys.map((_, i) => ttLines[i * 2] || 'none');
  const ttls = keys.map((_, i) => { const v = parseInt(ttLines[i * 2 + 1], 10); return isNaN(v) ? null : v; });

  const sizeCmdFor = { string: 'STRLEN', list: 'LLEN', set: 'SCARD', hash: 'HLEN', zset: 'ZCARD', stream: 'XLEN' };
  let sizeCmd = '';
  keys.forEach((k, i) => { if (sizeCmdFor[types[i]]) sizeCmd += `${sizeCmdFor[types[i]]} ${rq(k)}\n`; });
  let sizes = {};
  if (sizeCmd) {
    const sr = await redisCli([], sizeCmd);
    const sarr = sr.ok ? sr.stdout.split('\n').map((s) => s.trim()) : [];
    let si = 0;
    keys.forEach((k, i) => { if (sizeCmdFor[types[i]]) { const v = parseInt(sarr[si++], 10); sizes[k] = isNaN(v) ? null : v; } });
  }

  return {
    ok: true, keys: keys.map((k, i) => ({
      key: k, type: types[i], ttl: ttls[i], size: sizes[k] != null ? sizes[k] : null, isQueue: isQueueKey(k),
    })),
  };
}

async function redisKey(name) {
  const t = await redisCli(['TYPE', name]);
  if (!t.ok) return { ok: false, error: t.error };
  const type = t.stdout.trim();
  const ttlr = await redisCli(['TTL', name]);
  const ttl = ttlr.ok ? (() => { const v = parseInt(ttlr.stdout.trim(), 10); return isNaN(v) ? null : v; })() : null;

  const run = async (args) => {
    const r = await redisCli(args);
    if (!r.ok) return { ok: false, error: r.error };
    const out = r.stdout.replace(/\r/g, '').replace(/\n$/, '');
    return { ok: true, out };
  };

  const lines = (out) => (out === '' ? [] : out.split('\n'));

  switch (type) {
    case 'string': { const r = await run(['GET', name]); if (!r.ok) return r; return { ok: true, key: name, type, ttl, value: r.out }; }
    case 'list': { const r = await run(['LRANGE', name, '0', '-1']); if (!r.ok) return r; return { ok: true, key: name, type, ttl, value: lines(r.out) }; }
    case 'set': { const r = await run(['SMEMBERS', name]); if (!r.ok) return r; return { ok: true, key: name, type, ttl, value: lines(r.out) }; }
    case 'hash': {
      const r = await run(['HGETALL', name]); if (!r.ok) return r;
      const a = lines(r.out); const pairs = [];
      for (let i = 0; i + 1 < a.length; i += 2) pairs.push({ field: a[i], value: a[i + 1] });
      return { ok: true, key: name, type, ttl, value: pairs };
    }
    case 'zset': {
      const r = await run(['ZRANGE', name, '0', '-1', 'WITHSCORES']); if (!r.ok) return r;
      const a = lines(r.out); const pairs = [];
      for (let i = 0; i + 1 < a.length; i += 2) pairs.push({ member: a[i], score: a[i + 1] });
      return { ok: true, key: name, type, ttl, value: pairs };
    }
    case 'stream': { const r = await run(['XRANGE', name, '-', '+', 'COUNT', '100']); if (!r.ok) return r; return { ok: true, key: name, type, ttl, value: lines(r.out) }; }
    default: return { ok: true, key: name, type, ttl, value: null };
  }
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host}`);
  const p = url.pathname;

  if (p === '/api/servers') {
    const results = await Promise.all(SERVERS.map(serverStatus));
    recordHistory(results);
    sendJson(res, { generatedAt: Date.now(), servers: results });
    return;
  }

  const logMatch = /^\/api\/servers\/([A-Za-z]+)\/logs$/.exec(p);
  if (logMatch) {
    const key = logMatch[1].charAt(0).toUpperCase() + logMatch[1].slice(1).toLowerCase();
    const n = parseInt(url.searchParams.get('lines') || '200', 10) || 200;
    sendJson(res, readLogs(key, n));
    return;
  }

  if (p === '/api/history') {
    sendJson(res, { generatedAt: Date.now(), history });
    return;
  }

  // GET /api/db/tables — list all tables in each database.
  if (p === '/api/db/tables') {
    sendJson(res, { generatedAt: Date.now(), dbs: await listTables() });
    return;
  }

  // GET /api/db/table?db=auth&table=accounts&limit=100&offset=0 — read table data.
  if (p === '/api/db/table') {
    const db = url.searchParams.get('db') || '';
    const table = url.searchParams.get('table') || '';
    if (!DB_NAMES.includes(db) || !/^[a-z_][a-z0-9_]*$/.test(table)) {
      res.writeHead(400, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ ok: false, error: 'Invalid db/table' }));
      return;
    }
    const limit = parseInt(url.searchParams.get('limit') || '100', 10) || 100;
    const offset = parseInt(url.searchParams.get('offset') || '0', 10) || 0;
    const result = await tableData(db, table, limit, offset);
    if (!result.ok) { res.writeHead(400, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(result)); return; }
    sendJson(res, { generatedAt: Date.now(), ...result });
    return;
  }

  // GET /api/db/meta?db=auth&table=accounts — column info + row count.
  if (p === '/api/db/meta') {
    const db = url.searchParams.get('db') || '';
    const table = url.searchParams.get('table') || '';
    if (!DB_NAMES.includes(db) || !/^[a-z_][a-z0-9_]*$/.test(table)) {
      res.writeHead(400, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ ok: false, error: 'Invalid db/table' }));
      return;
    }
    sendJson(res, { generatedAt: Date.now(), ...(await tableMeta(db, table)) });
    return;
  }

  if (p === '/api/health') {
    sendJson(res, { ok: true });
    return;
  }

  // ---- Redis read-only browser ----
  if (p === '/api/redis/info') { sendJson(res, await redisInfo()); return; }
  if (p === '/api/redis/keys') { sendJson(res, await redisKeys()); return; }
  if (p === '/api/redis/key') {
    const key = url.searchParams.get('key') || '';
    if (!key) { res.writeHead(400, { 'Content-Type': 'application/json' }); res.end(JSON.stringify({ ok: false, error: 'Missing key' })); return; }
    sendJson(res, await redisKey(key));
    return;
  }

  // ---- Moderation actions (POST, proxied to Game WebApi) ----
  const adminMatch = /^\/api\/mod\/(kick|ban|roomkick|closeroom)$/.exec(p);
  if (req.method === 'POST' && adminMatch) {
    const action = adminMatch[1];
    if (action === 'kick') {
      // /admin/kick expects form-encoded playerId.
      const body = await readBody(req);
      let payload = {};
      try { payload = body ? JSON.parse(body) : {}; } catch { /* invalid json */ }
      const result = await proxyAdmin('kick', { playerId: String(payload.playerId ?? '') }, null);
      res.writeHead(result.status || 200, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ ok: (result.status || 0) >= 200 && (result.status || 0) < 300, status: result.status, response: result.body }));
      return;
    }
    await handleAdmin(req, res, action);
    return;
  }

  serveStatic(res, p === '/' ? '/index.html' : p);
});

server.listen(PORT, () => {
  console.log(`OpenS4L Admin Console backend on http://localhost:${PORT} (game API: ${GAME_API})`);
});
