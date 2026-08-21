import { useEffect, useState, useCallback, useMemo } from 'react';
import DataView from './Data.jsx';
import RedisView from './Redis.jsx';
import Moderation from './Moderation.jsx';

/* ---------- helpers ---------- */
function fmtUptime(sec) {
  if (sec == null || sec < 0) return '—';
  const d = Math.floor(sec / 86400);
  const h = Math.floor((sec % 86400) / 3600);
  const m = Math.floor((sec % 3600) / 60);
  const s = Math.floor(sec % 60);
  if (d > 0) return `${d}d ${h}h ${m}m`;
  if (h > 0) return `${h}h ${m}m ${s}s`;
  if (m > 0) return `${m}m ${s}s`;
  return `${s}s`;
}

/* ---------- SVG line chart ---------- */
function LineChart({ series, height = 90, color = '#4a7dff', fill = true, label = 'players' }) {
  const W = 260;
  const H = height;
  const pad = 6;
  if (!series || series.length < 2) {
    return (
      <svg viewBox={`0 0 ${W} ${H}`} className="chart" preserveAspectRatio="none">
        <rect x={0} y={0} width={W} height={H} rx={6} className="chart-bg" />
        <text x={W / 2} y={H / 2} textAnchor="middle" className="chart-empty">collecting data…</text>
      </svg>
    );
  }
  const vals = series.map((s) => s);
  const max = Math.max(...vals, 1);
  const step = (W - pad * 2) / (vals.length - 1);
  const pts = vals.map((v, i) => [pad + i * step, H - pad - ((H - pad * 2) * v) / max]);
  const line = pts.map((p) => p.join(',')).join(' ');
  const area = `M ${pad} ${H - pad} L ${pts.map((p) => p.join(' ')).join(' L ')} L ${pad + (vals.length - 1) * step} ${H - pad} Z`;
  const last = vals[vals.length - 1];
  return (
    <div className="chart-wrap">
      <svg viewBox={`0 0 ${W} ${H}`} className="chart" preserveAspectRatio="none">
        {fill && <path d={area} className="chart-area" />}
        <polyline points={line} fill="none" stroke={color} strokeWidth={2} className="chart-line" />
        {pts.length && <circle cx={pts[pts.length - 1][0]} cy={pts[pts.length - 1][1]} r={3} fill={color} />}
      </svg>
      <div className="chart-label">{label} <b>{last}</b></div>
    </div>
  );
}

/* ---------- channel card ---------- */
function ChannelCard({ ch }) {
  const pct = ch.playerLimit ? Math.min(100, (ch.playersOnline / ch.playerLimit) * 100) : 0;
  return (
    <div className="card card-channel">
      <div className="card-header">
        <h2>{ch.name}</h2>
        <span className="status">{ch.playersOnline}/{ch.playerLimit}</span>
      </div>
      <div className="card-body">
        <div className="channel-track">
          <div className="channel-fill" style={{ width: `${pct}%` }} />
        </div>
        <div className="channel-sub">
          {ch.playersOnline} online · {ch.roomCount} room{ch.roomCount === 1 ? '' : 's'} · {ch.playersInRooms} in rooms
        </div>
      </div>
    </div>
  );
}

/* ---------- server card ---------- */
function ServerCard({ server, history }) {
  const [showLogs, setShowLogs] = useState(false);
  const [logs, setLogs] = useState([]);
  const [logError, setLogError] = useState(null);

  const loadLogs = useCallback(async () => {
    try {
      const res = await fetch(`/api/servers/${server.key}/logs?lines=200`);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = await res.json();
      setLogs(data.lines || []);
      setLogError(null);
    } catch (e) {
      setLogError(String(e.message || e));
    }
  }, [server.key]);

  useEffect(() => {
    if (!showLogs) return;
    loadLogs();
    const id = setInterval(loadLogs, 3000);
    return () => clearInterval(id);
  }, [showLogs, loadLogs]);

  const statusText = server.up ? 'Online' : 'Offline';
  const latency = server.latencyMs != null ? `${server.latencyMs} ms` : '—';
  const stats = server.stats;
  const isGame = server.key === 'Game';

  const latencySeries = useMemo(
    () => history.filter((h) => h.latency[server.key] != null).map((h) => h.latency[server.key]),
    [history, server.key]
  );

  const sessionsSeries = useMemo(
    () => history.filter((h) => h.sessions && h.sessions[server.key] != null).map((h) => h.sessions[server.key]),
    [history, server.key]
  );

  return (
    <div className={`card ${server.up ? 'up' : 'down'} ${isGame ? 'card-game' : ''}`}>
      <div className="card-header">
        <span className={`dot ${server.up ? 'dot-up' : 'dot-down'}`} />
        <h2>{server.name}</h2>
        <span className="status">{statusText}</span>
      </div>
      <div className="card-body">
        <div className="row"><span>Address</span><code>{server.configFound ? `${server.address}:${server.port}` : 'config not found'}</code></div>
        <div className="row"><span>Latency</span><span className={server.up ? 'num-green' : ''}>{latency}</span></div>

        {!isGame && server.sessions != null && (
          <div className="stat-grid">
            <div className="stat">
              <div className="stat-num">{server.sessions}</div>
              <div className="stat-label">Open sessions</div>
            </div>
            <div className="stat">
              <div className="stat-num">{server.sessionsTotal != null ? server.sessionsTotal : '—'}</div>
              <div className="stat-label">Total connects</div>
            </div>
            {server.key === 'Chat' && (
              <div className="stat">
                <div className="stat-num">{server.messagesSent != null ? server.messagesSent : '—'}</div>
                <div className="stat-label">Messages sent</div>
              </div>
            )}
            <div className="stat">
              <div className="stat-num">{fmtUptime(server.uptime)}</div>
              <div className="stat-label">Uptime</div>
            </div>
          </div>
        )}

        {latencySeries.length >= 2 && (
          <LineChart series={latencySeries} height={70} color="#3ddc84" label="latency ms" />
        )}

        {!isGame && sessionsSeries.length >= 2 && (
          <LineChart series={sessionsSeries} height={60} color="#c58bff" label="sessions" />
        )}

        {isGame && stats && stats.reachable && (
          <div className="game-stats">
            <div className="stat-grid">
              <div className="stat">
                <div className="stat-num">{stats.playersOnline}</div>
                <div className="stat-label">Players online</div>
              </div>
              <div className="stat">
                <div className="stat-num">{stats.peakPlayers}</div>
                <div className="stat-label">Peak</div>
              </div>
              <div className="stat">
                <div className="stat-num">{stats.roomCount}</div>
                <div className="stat-label">Open rooms</div>
              </div>
              <div className="stat">
                <div className="stat-num">{fmtUptime(stats.uptime)}</div>
                <div className="stat-label">Uptime</div>
              </div>
            </div>
          </div>
        )}

        {isGame && stats && !stats.reachable && (
          <div className="row"><span>Web API</span><span className="text-red">unreachable</span></div>
        )}

        <button onClick={() => setShowLogs((v) => !v)}>
          {showLogs ? 'Hide logs' : 'View logs'}
        </button>
      </div>
      {showLogs && (
        <div className="logs">
          {logError && <div className="log-error">Failed to load logs: {logError}</div>}
          {!logError && logs.length === 0 && <div className="log-empty">No log entries.</div>}
          {logs.map((l, i) => (
            <div className="log-line" key={i}>
              {l.timestamp ? <span className="log-time">{l.timestamp}</span> : null}
              <span className="log-text">{l.text}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

/* ---------- dashboard ---------- */
function Dashboard() {
  const [data, setData] = useState({ servers: [] });
  const [history, setHistory] = useState([]);
  const [error, setError] = useState(null);
  const [lastUpdated, setLastUpdated] = useState(null);

  const refresh = useCallback(async () => {
    try {
      const res = await fetch('/api/servers');
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const d = await res.json();
      setData(d);
      setError(null);
      setLastUpdated(new Date().toLocaleTimeString());
    } catch (e) {
      setError(String(e.message || e));
    }
  }, []);

  const fetchHistory = useCallback(async () => {
    try {
      const res = await fetch('/api/history');
      if (!res.ok) return;
      const d = await res.json();
      if (Array.isArray(d.history)) setHistory(d.history);
    } catch { /* ignore */ }
  }, []);

  useEffect(() => {
    refresh();
    fetchHistory();
    const id = setInterval(refresh, 3000);
    const id2 = setInterval(fetchHistory, 3000);
    return () => { clearInterval(id); clearInterval(id2); };
  }, [refresh, fetchHistory]);

  const servers = data.servers || [];
  const upCount = servers.filter((s) => s.up).length;
  const game = servers.find((s) => s.key === 'Game');
  const totalPlayers = game?.stats?.playersOnline ?? 0;
  const totalRooms = game?.stats?.roomCount ?? 0;

  const playerSeries = useMemo(
    () => history.map((h) => h.playersOnline),
    [history]
  );

  return (
    <div className="app">
      <header>
        <div>
          <h1>OpenS4L Admin Console</h1>
          <div className="meta">
            <span>{upCount}/{servers.length} online</span>
            <span><b>{totalPlayers}</b> players online</span>
            <span><b>{totalRooms}</b> open rooms</span>
            <span className="updated">updated {lastUpdated || '…'}</span>
          </div>
        </div>
        <button onClick={refresh}>Refresh now</button>
      </header>

      {error && <div className="banner">Backend error: {error}</div>}

      <div className="hero-chart">
        <div className="hero-chart-head">
          <span className="hero-title">Players online over time</span>
          <span className="hero-current">{totalPlayers} now</span>
        </div>
        <LineChart series={playerSeries} height={110} color="#4a7dff" />
      </div>

      <main className="grid grid-services">
        {servers.map((s) => <ServerCard key={s.key} server={s} history={history} />)}
        {servers.length === 0 && !error && <p className="loading">Loading servers…</p>}
      </main>

      {game?.stats?.channels?.length > 0 && (
        <section className="channels-section">
          <h2 className="section-title">Game Channels</h2>
          <div className="grid">
            {game.stats.channels.map((ch) => <ChannelCard key={ch.id} ch={ch} />)}
          </div>
        </section>
      )}

      <footer>
        <span className="foot-note">Game stats via WebApi plugin · history kept {Math.round(history.length * 3 / 60)} min</span>
      </footer>
    </div>
  );
}

/* ---------- app shell with top nav ---------- */
export default function App() {
  const [view, setView] = useState('dashboard');
  return (
    <div className="shell">
      <nav className="topnav">
        <span className="brand">OpenS4L Admin</span>
        <button
          className={`nav-item ${view === 'dashboard' ? 'active' : ''}`}
          onClick={() => setView('dashboard')}
        >Dashboard</button>
        <button
          className={`nav-item ${view === 'data' ? 'active' : ''}`}
          onClick={() => setView('data')}
        >Data</button>
        <button
          className={`nav-item ${view === 'redis' ? 'active' : ''}`}
          onClick={() => setView('redis')}
        >Redis</button>
        <button
          className={`nav-item ${view === 'moderation' ? 'active' : ''}`}
          onClick={() => setView('moderation')}
        >Moderation</button>
      </nav>
      {view === 'dashboard' && <Dashboard />}
      {view === 'data' && <DataView />}
      {view === 'redis' && <RedisView />}
      {view === 'moderation' && <Moderation />}
    </div>
  );
}
