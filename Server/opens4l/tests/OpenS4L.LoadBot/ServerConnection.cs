using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenS4L.Blub.Serialization;
using ProudNet;
using ProudNet.Serialization;

namespace OpenS4L.LoadBot
{
    /// <summary>A ProudNet client connection with a typed wait-for-ack helper.</summary>
    public class ServerConnection : IDisposable
    {
        private readonly object _lock = new object();
        private readonly List<(Type Type, TaskCompletionSource<object> Tcs)> _waiters =
            new List<(Type, TaskCompletionSource<object>)>();

        public ProudNetClient Client { get; }

        public ServerConnection(BlubSerializer serializer, MessageFactory[] factories)
        {
            Client = new ProudNetClient(serializer, factories);
            Client.MessageReceived += OnMessage;
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            (Type, TaskCompletionSource<object>)[] matched;
            lock (_lock)
            {
                matched = _waiters
                    .Where(w => w.Type.IsInstanceOfType(e.Message))
                    .ToArray();

                foreach (var w in matched)
                    _waiters.Remove(w);
            }

            foreach (var w in matched)
                w.Item2.TrySetResult(e.Message);
        }

        /// <summary>Wait for the next inbound message of type <typeparamref name="T"/>.</summary>
        public Task<T> WaitFor<T>(TimeSpan timeout) where T : class
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_lock)
                _waiters.Add((typeof(T), tcs));

            return tcs.Task.WaitAsync(timeout).ContinueWith(t => (T)t.Result);
        }

        public void Dispose()
        {
            Client.MessageReceived -= OnMessage;
            Client.Dispose();
        }
    }
}
