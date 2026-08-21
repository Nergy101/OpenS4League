using System;
using System.Threading;
using System.Threading.Tasks;
using ProudNet.Hosting.Services;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Minimal in-memory ISchedulerService for tests. Does nothing (the state machine's
    /// scheduled actions drive gameplay timers, which the unit tests don't need to run).
    /// </summary>
    internal sealed class NoopSchedulerService : ISchedulerService
    {
        public void Execute(Action action) { }
        public void Execute(Action<object, object> action, object context, object state) { }

        public Task ScheduleAsync(Action action, TimeSpan delay) => Task.CompletedTask;
        public Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay) => Task.CompletedTask;
        public Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<T> SubmitAsync<T>(Func<T> func) => Task.FromResult(func());
        public Task<T> SubmitAsync<T>(Func<T> func, CancellationToken cancellationToken) => Task.FromResult(func());
        public Task<T> SubmitAsync<T>(Func<object, T> func, object state) => Task.FromResult(func(state));
        public Task<T> SubmitAsync<T>(Func<object, T> func, object state, CancellationToken cancellationToken) => Task.FromResult(func(state));
        public Task<T> SubmitAsync<T>(Func<object, object, T> func, object context, object state) => Task.FromResult(func(context, state));
        public Task<T> SubmitAsync<T>(Func<object, object, T> func, object context, object state, CancellationToken cancellationToken) => Task.FromResult(func(context, state));
    }
}
