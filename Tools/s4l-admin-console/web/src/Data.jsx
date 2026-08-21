import { useEffect, useState, useCallback } from 'react';

/* Read-only database table browser.
   Left: scrollable nav with tables grouped by subject category.
   Right: data table that fills the available space. */

// Map each table to a subject category (fallback: Other).
const CATEGORY = {
  // Players / accounts
  accounts: 'Players',
  players: 'Players',
  player_settings: 'Players',
  player_characters: 'Players',
  player_items: 'Players',
  // History
  login_history: 'History',
  nickname_history: 'History',
  // Bans
  bans: 'Bans',
  clan_bans: 'Bans',
  player_deny: 'Bans',
  // Channels
  channels: 'Channels',
  // Clans
  clans: 'Clans',
  clan_members: 'Clans',
  clan_events: 'Clans',
  // Social
  player_friends: 'Social',
  player_mails: 'Social',
  // Shop & items
  shop_items: 'Shop',
  shop_iteminfos: 'Shop',
  shop_effects: 'Shop',
  shop_effect_groups: 'Shop',
  shop_prices: 'Shop',
  shop_price_groups: 'Shop',
  shop_version: 'Shop',
  start_items: 'Shop',
  level_rewards: 'Shop',
  // System / misc
  __EFMigrationsHistory: 'System',
};

// Category display order.
const CATEGORY_ORDER = ['Players', 'History', 'Bans', 'Channels', 'Clans', 'Social', 'Shop', 'System', 'Other'];

function categorize(table) {
  return CATEGORY[table] || 'Other';
}

function fmtCell(v) {
  if (v == null || v === '') return <span className="cell-null">NULL</span>;
  if (typeof v === 'string' && v.length > 200) return v.slice(0, 200) + '…';
  return v;
}

export default function DataView() {
  const [dbs, setDbs] = useState([]);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [sel, setSel] = useState(null); // { db, table }
  const [meta, setMeta] = useState(null);
  const [data, setData] = useState(null);
  const [dataLoading, setDataLoading] = useState(false);
  const [limit, setLimit] = useState(100);
  const [offset, setOffset] = useState(0);

  // Load the table list once.
  useEffect(() => {
    (async () => {
      try {
        const res = await fetch('/api/db/tables');
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const j = await res.json();
        setDbs(j.dbs || []);
        setError(null);
      } catch (e) {
        setError(String(e.message || e));
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  // Default selection: auth/accounts when available; otherwise the first table.
  useEffect(() => {
    if (!loading && !sel) {
      const auth = dbs.find((db) => db.name === 'auth');
      if (auth && auth.ok && auth.tables.includes('accounts')) {
        setSel({ db: 'auth', table: 'accounts' });
        return;
      }
      for (const db of dbs) {
        if (db.ok && db.tables.length > 0) {
          setSel({ db: db.name, table: db.tables[0] });
          break;
        }
      }
    }
  }, [loading, dbs, sel]);

  // Flatten tables with their db + category, grouped by category in display order.
  const grouped = useCallback(() => {
    const byCat = {};
    for (const db of dbs) {
      if (!db.ok) continue;
      for (const t of db.tables) {
        const cat = categorize(t);
        if (!byCat[cat]) byCat[cat] = [];
        byCat[cat].push({ db: db.name, table: t });
      }
    }
    const cats = CATEGORY_ORDER.filter((c) => byCat[c]);
    for (const c of Object.keys(byCat)) if (!cats.includes(c)) cats.push(c);
    return cats.map((c) => ({ cat: c, items: byCat[c] }));
  }, [dbs]);

  // Load table meta + data when a table is selected (or offset/limit changes).
  const loadTable = useCallback(async () => {
    if (!sel) return;
    setDataLoading(true);
    try {
      const [mRes, dRes] = await Promise.all([
        fetch(`/api/db/meta?db=${sel.db}&table=${sel.table}`),
        fetch(`/api/db/table?db=${sel.db}&table=${sel.table}&limit=${limit}&offset=${offset}`),
      ]);
      const m = await mRes.json();
      const d = await dRes.json();
      setMeta(m.ok ? m : null);
      setData(d.ok ? d : null);
      if (!m.ok) setError(m.error || 'Failed to load table meta');
      if (!d.ok) setError(d.error || 'Failed to load table data');
      else setError(null);
    } catch (e) {
      setError(String(e.message || e));
    } finally {
      setDataLoading(false);
    }
  }, [sel, limit, offset]);

  useEffect(() => { loadTable(); }, [loadTable]);

  const selectTable = (db, table) => {
    setSel({ db, table });
    setOffset(0);
  };

  const cats = grouped();

  return (
    <div className="app data-app">
      <header className="data-header">
        <h1>Data</h1>
        <span className="meta">Read-only view of the OpenS4L databases</span>
      </header>
      {error && <div className="banner">Error: {error}</div>}
      {loading && <p className="loading">Loading databases…</p>}

      {!loading && (
        <div className="data-split">
          {/* Left nav: tables grouped by category, scrollable */}
          <aside className="data-nav">
            {cats.map((g) => (
              <div className="db-group" key={g.cat}>
                <div className="db-head">{g.cat}</div>
                {g.items.map(({ db, table }) => (
                  <button
                    key={db + '/' + table}
                    className={`nav-table ${sel && sel.db === db && sel.table === table ? 'active' : ''}`}
                    onClick={() => selectTable(db, table)}
                  >
                    <span className="table-name">{table}</span>
                    <span className="table-db">{db}</span>
                  </button>
                ))}
              </div>
            ))}
          </aside>

          {/* Right: the data table, growing to fill the pane */}
          <div className="data-content">
            {!sel && (
              <div className="data-empty">Select a table on the left to view its data.</div>
            )}

            {sel && (
              <div className="viewer">
                <div className="viewer-top">
                  <h2 className="viewer-title">{sel.db} / {sel.table}</h2>
                  {meta && <span className="viewer-count">{meta.rows.toLocaleString()} rows</span>}
                </div>

                {meta && (
                  <div className="viewer-cols">
                    {meta.columns.map((c) => <code key={c.name} title={c.type}>{c.name}: {c.type}</code>)}
                  </div>
                )}

                <div className="viewer-controls">
                  <label>Rows
                    <select value={limit} onChange={(e) => { setOffset(0); setLimit(parseInt(e.target.value, 10)); }}>
                      {[50, 100, 250, 500, 1000].map((n) => <option key={n} value={n}>{n}</option>)}
                    </select>
                  </label>
                  <button disabled={offset <= 0} onClick={() => setOffset((o) => Math.max(0, o - limit))}>← Prev</button>
                  <span className="offset-label">rows {offset + 1}–{offset + (data ? data.count : 0)}</span>
                  <button disabled={!data || data.count < limit} onClick={() => setOffset((o) => o + limit)}>Next →</button>
                </div>

                {dataLoading && (
                  <div className="table-loading"><span className="spinner" /></div>
                )}

                <div className="table-scroll">
                  <table className="data-table">
                    <thead>
                      {data && data.columns && (
                        <tr>{data.columns.map((c) => <th key={c}>{c}</th>)}</tr>
                      )}
                    </thead>
                    <tbody>
                      {data && data.columns && data.rows.length === 0 && (
                        <tr><td colSpan={data.columns.length || 1} className="empty-row">No rows.</td></tr>
                      )}
                      {data && data.rows.map((row, i) => (
                        <tr key={i}>{row.map((cell, j) => <td key={j}>{fmtCell(cell)}</td>)}</tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                {data && data.columns && data.count >= limit && (
                  <p className="more-note">Showing {data.count} rows. Use Next to page further.</p>
                )}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
