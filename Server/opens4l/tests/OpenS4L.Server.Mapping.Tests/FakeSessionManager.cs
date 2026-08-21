using System;
using System.Collections.Generic;
using ProudNet;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Minimal in-memory ISessionManager for the harness. Lets tests simulate connect/disconnect
    /// by raising the Added/Removed events (which PlayerManager subscribes to). Only the members
    /// the server code actually touches are wired.
    /// </summary>
    internal sealed class FakeSessionManager : ISessionManager
    {
        private readonly Dictionary<uint, ProudSession> _sessions = new Dictionary<uint, ProudSession>();

        public event EventHandler<SessionEventArgs> Added;
        public event EventHandler<SessionEventArgs> Removed;

        public IReadOnlyDictionary<uint, ProudSession> Sessions => _sessions;

        public ProudSession GetSession(uint key) => _sessions.GetValueOrDefault(key);

        public void AddSession(ProudSession session)
        {
            _sessions[session.HostId] = session;
            Added?.Invoke(this, new SessionEventArgs(session));
        }

        public void RemoveSession(uint hostId)
        {
            if (_sessions.Remove(hostId, out var session))
                Removed?.Invoke(this, new SessionEventArgs(session));
        }

        public void Broadcast(object message)
        {
            foreach (var s in _sessions.Values) s.Send(message);
        }

        public void Broadcast(object message, Predicate<ProudSession> predicate)
        {
            foreach (var s in _sessions.Values)
                if (predicate(s)) s.Send(message);
        }
    }
}
