import { useState } from 'react';

/* Moderation tab: collapsible cards for Game WebApi admin actions.
   All calls go through the backend proxy (/api/mod/...) to avoid CORS. */

const LEAVE_REASONS = [
  { v: 'Left', n: 0 },
  { v: 'Kicked', n: 1 },
  { v: 'MasterAFK', n: 2 },
  { v: 'AFK', n: 3 },
  { v: 'ModeratorKick', n: 4 },
  { v: 'VoteKick', n: 5 },
];

function Field({ label, value, onChange, placeholder = '', type = 'text', select }) {
  return (
    <label className="mod-field">
      <span>{label}</span>
      {select ? (
        <select value={value} onChange={(e) => onChange(e.target.value)}>
          {select.map((o) => <option key={o.v} value={o.v}>{o.v}</option>)}
        </select>
      ) : (
        <input type={type} value={value} onChange={(e) => onChange(e.target.value)} placeholder={placeholder} />
      )}
    </label>
  );
}

function ModCard({ title, desc, danger, children, onAction }) {
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState(null);

  const run = async () => {
    setBusy(true);
    setResult(null);
    try {
      const r = await onAction();
      setResult(r);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className={`mod-card ${danger ? 'danger' : ''}`}>
      <button className={`mod-head ${open ? 'open' : ''}`} onClick={() => setOpen((o) => !o)}>
        <span className="mod-title">{title}</span>
        <span className="mod-chevron">{open ? '−' : '+'}</span>
      </button>
      {open && (
        <div className="mod-body">
          {desc && <p className="mod-desc">{desc}</p>}
          {children}
          <div className="mod-actions">
            <button className="mod-run" onClick={run} disabled={busy}>{busy ? 'Sending…' : 'Execute'}</button>
            {result && (
              <span className={`mod-result ${result.ok ? 'ok' : 'err'}`}>
                HTTP {result.status}: {result.response || (result.ok ? 'success' : 'error')}
              </span>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

async function post(path, body) {
  const res = await fetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  let data;
  try { data = await res.json(); } catch { data = {}; }
  return { ok: data.ok, status: data.status ?? res.status, response: data.response };
}

export default function Moderation() {
  const [kickId, setKickId] = useState('');
  const [banId, setBanId] = useState('');
  const [banDur, setBanDur] = useState('3600');
  const [banReason, setBanReason] = useState('');
  const [rkId, setRkId] = useState('');
  const [rkReason, setRkReason] = useState('ModeratorKick');
  const [crChannel, setCrChannel] = useState('1');
  const [crRoom, setCrRoom] = useState('');
  const [crReason, setCrReason] = useState('ModeratorKick');

  return (
    <div className="app mod-app">
      <header className="data-header">
        <h1>Moderation</h1>
        <span className="meta">Game server actions via the WebApi plugin</span>
      </header>

      <div className="mod-list">
        <ModCard
          title="Kick player"
          desc="Disconnect a player from the Game server."
          danger
          onAction={() => post('/api/mod/kick', { playerId: kickId })}
        >
          <Field label="Player ID" value={kickId} onChange={setKickId} placeholder="e.g. 1" />
        </ModCard>

        <ModCard
          title="Ban player"
          desc="Ban an account and disconnect them. Duration is in seconds (0 = permanent)."
          danger
          onAction={() => post('/api/mod/ban', { PlayerId: banId, Duration: parseInt(banDur, 10) || 0, Reason: banReason })}
        >
          <Field label="Player ID" value={banId} onChange={setBanId} placeholder="e.g. 1" />
          <Field label="Duration (seconds)" value={banDur} onChange={setBanDur} type="number" placeholder="3600" />
          <Field label="Reason" value={banReason} onChange={setBanReason} placeholder="Optional" />
        </ModCard>

        <ModCard
          title="Kick player from room"
          desc="Remove a player from their current room."
          danger
          onAction={() => post('/api/mod/roomkick', { PlayerId: rkId, Reason: LEAVE_REASONS.find((r) => r.v === rkReason).n })}
        >
          <Field label="Player ID" value={rkId} onChange={setRkId} placeholder="e.g. 1" />
          <Field label="Leave reason" value={rkReason} onChange={setRkReason} select={LEAVE_REASONS} />
        </ModCard>

        <ModCard
          title="Close room"
          desc="Kick all players from a room and close it."
          danger
          onAction={() => post('/api/mod/closeroom', { ChannelId: parseInt(crChannel, 10) || 1, RoomId: parseInt(crRoom, 10) || 0, Reason: LEAVE_REASONS.find((r) => r.v === crReason).n })}
        >
          <Field label="Channel ID" value={crChannel} onChange={setCrChannel} type="number" placeholder="1" />
          <Field label="Room ID" value={crRoom} onChange={setCrRoom} type="number" placeholder="e.g. 2" />
          <Field label="Leave reason" value={crReason} onChange={setCrReason} select={LEAVE_REASONS} />
        </ModCard>
      </div>
    </div>
  );
}
