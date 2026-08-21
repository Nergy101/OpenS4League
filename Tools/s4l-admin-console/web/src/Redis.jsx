import { useEffect, useState, useCallback } from 'react';

/* Read-only Redis browser.
   Left: summary strip (memory/clients/keys) + scrollable key nav grouped into
   "Queues" (Foundatio IQueue keys) and "All keys" by type.
   Right: the selected key's value rendered for its type. */

const TYPE_ORDER = ['string', 'list', 'set', 'hash', 'zset', 'stream', 'none'];

function fmtCell(v) {
  if (v == null || v === '') return <span className="cell-null">NULL</span>;
  const s = String(v);
  if (s.length > 500) return s.slice(0, 500) + '…';
  return s;
}

// Try to pretty-print JSON-ish string values.
function fmtString(v) {
  const s = String(v ?? '');
  if (s.length > 5000) return s.slice(0, 5000) + '…';
  try {
    const parsed = JSON.parse(s);
    if (parsed && typeof parsed === 'object') return JSON.stringify(parsed, null, 2);
  } catch { /* not JSON — show raw */ }
  return s;
}

export default function RedisView() {
  const [info, setInfo] = useState(null);
  const [keys, setKeys] = useState([]);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [sel, setSel] = useState(null); // key name
  const [keyData, setKeyData] = useState(null);
  const [keyLoading, setKeyLoading] = useState(false);
  const [infoLoading, setInfoLoading] = useState(true);

  const loadKeys = useCallback(async () => {
    try {
      const res = await fetch('/api/redis/keys');
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const j = await res.json();
      if (!j.ok) throw new Error(j.error || 'Failed to list keys');
      setKeys(j.keys || []);
      setError(null);
    } catch (e) {
      setError(String(e.message || e));
    } finally {
      setLoading(false);
    }
  }, []);

  const loadInfo = useCallback(async () => {
    try {
      const res = await fetch('/api/redis/info');
      if (!res.ok) return;
      const j = await res.json();
      if (j.ok) setInfo(j.info);
    } catch { /* ignore */ } finally {
      setInfoLoading(false);
    }
  }, []);

  useEffect(() => {
    loadKeys();
    loadInfo();
    const id = setInterval(() => { loadKeys(); loadInfo(); }, 5000);
    return () => clearInterval(id);
  }, [loadKeys, loadInfo]);

  // Default selection: first queue key, else first key.
  useEffect(() => {
    if (!loading && !sel && keys.length > 0) {
      const q = keys.find((k) => k.isQueue);
      setSel((q || keys[0]).key);
    }
  }, [loading, keys, sel]);

  const loadKey = useCallback(async () => {
    if (!sel) return;
    setKeyLoading(true);
    try {
      const res = await fetch(`/api/redis/key?key=${encodeURIComponent(sel)}`);
      const j = await res.json();
      if (j.ok) { setKeyData(j); setError(null); }
      else setError(j.error || 'Failed to load key');
    } catch (e) {
      setError(String(e.message || e));
    } finally {
      setKeyLoading(false);
    }
  }, [sel]);

  useEffect(() => { loadKey(); }, [loadKey]);

  // Group nav: Queues first, then remaining keys by type.
  const groups = useCallback(() => {
    const queues = keys.filter((k) => k.isQueue);
    const rest = keys.filter((k) => !k.isQueue);
    const byType = {};
    for (const k of rest) {
      if (!byType[k.type]) byType[k.type] = [];
      byType[k.type].push(k);
    }
    const out = [];
    if (queues.length) out.push({ title: 'Queues', items: queues });
    const types = TYPE_ORDER.filter((t) => byType[t]);
    for (const t of types) out.push({ title: t, items: byType[t] });
    return out;
  }, [keys]);

  const groupsList = groups();
  const infoData = info || {};

  return (
    <div className="app data-app">
      <header className="data-header">
        <h1>Redis</h1>
        <span className="meta">Read-only view of the Redis cache &amp; write-behind queues</span>
      </header>
      {error && <div className="banner">Error: {error}</div>}
      {loading && <p className="loading">Loading Redis…</p>}

      {!loading && (
        <div className="data-split">
          <aside className="data-nav">
            {/* Summary strip */}
            <div className="redis-summary">
              {!infoLoading && infoData.version ? (
                <>
                  <div className="redis-sum-row"><span>Version</span><b>{infoData.version}</b></div>
                  <div className="redis-sum-row"><span>Memory</span><b>{infoData.usedMemory || '—'}</b></div>
                  <div className="redis-sum-row"><span>Clients</span><b>{infoData.connectedClients ?? '—'}</b></div>
                  <div className="redis-sum-row"><span>Ops/s</span><b>{infoData.opsPerSec ?? '—'}</b></div>
                  <div className="redis-sum-row"><span>Keys (db0)</span><b>{infoData.db0 ? infoData.db0.keys : '—'}</b></div>
                </>
              ) : <div className="redis-sum-empty">Redis info unavailable</div>}
            </div>

            {groupsList.map((g) => (
              <div className="db-group" key={g.title}>
                <div className="db-head">{g.title}</div>
                {g.items.map((k) => (
                  <button
                    key={k.key}
                    className={`nav-table ${sel === k.key ? 'active' : ''}`}
                    onClick={() => setSel(k.key)}
                  >
                    <span className="table-name">{k.key}</span>
                    <span className="table-db">
                      {k.type}
                      {k.size != null ? ` · ${k.size}` : ''}
                    </span>
                  </button>
                ))}
              </div>
            ))}
            {groupsList.length === 0 && <div className="redis-empty-nav">No keys found.</div>}
          </aside>

          <div className="data-content">
            {!sel && <div className="data-empty">Select a key on the left to view its data.</div>}

            {sel && (
              <div className="viewer">
                <div className="viewer-top">
                  <h2 className="viewer-title">{sel}</h2>
                  {keyData && (
                    <span className="viewer-count">
                      {keyData.type}
                      {keyData.ttl != null && keyData.ttl >= 0 ? ` · TTL ${keyData.ttl}s` : ' · no TTL'}
                    </span>
                  )}
                </div>

                {keyLoading && <div className="table-loading"><span className="spinner" /></div>}

                {keyData && !keyLoading && (
                  <div className="redis-value">
                    {keyData.type === 'string' && (
                      <pre className="redis-pre">{fmtString(keyData.value)}</pre>
                    )}
                    {keyData.type === 'list' && (
                      <ol className="redis-list">
                        {keyData.value.map((v, i) => <li key={i}><code>{fmtCell(v)}</code></li>)}
                        {keyData.value.length === 0 && <li className="empty-row">(empty list)</li>}
                      </ol>
                    )}
                    {keyData.type === 'set' && (
                      <ul className="redis-list">
                        {keyData.value.map((v, i) => <li key={i}><code>{fmtCell(v)}</code></li>)}
                        {keyData.value.length === 0 && <li className="empty-row">(empty set)</li>}
                      </ul>
                    )}
                    {(keyData.type === 'hash' || keyData.type === 'zset') && (
                      <table className="data-table redis-table">
                        <thead>
                          <tr>
                            <th>{keyData.type === 'hash' ? 'Field' : 'Member'}</th>
                            <th>{keyData.type === 'hash' ? 'Value' : 'Score'}</th>
                          </tr>
                        </thead>
                        <tbody>
                          {keyData.value.length === 0 && (
                            <tr><td colSpan={2} className="empty-row">(empty)</td></tr>
                          )}
                          {keyData.value.map((p, i) => (
                            <tr key={i}>
                              <td className="redis-k">{fmtCell(p.field || p.member)}</td>
                              <td>{fmtCell(p.value != null ? p.value : p.score)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    )}
                    {keyData.type === 'stream' && (
                      <pre className="redis-pre">{keyData.value.join('\n')}</pre>
                    )}
                    {keyData.type === 'none' && <div className="empty-row">Key does not exist.</div>}
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
