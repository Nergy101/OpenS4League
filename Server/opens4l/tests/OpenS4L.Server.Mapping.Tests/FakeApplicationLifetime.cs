using System;
using System.Threading;
using Microsoft.Extensions.Hosting;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>Stub IApplicationLifetime for the Game command handlers in tests.</summary>
    internal sealed class FakeApplicationLifetime : IApplicationLifetime
    {
        public CancellationToken ApplicationStarted { get; } = CancellationToken.None;
        public CancellationToken ApplicationStopping { get; } = CancellationToken.None;
        public CancellationToken ApplicationStopped { get; } = CancellationToken.None;
        public void StopApplication() { }
    }
}
