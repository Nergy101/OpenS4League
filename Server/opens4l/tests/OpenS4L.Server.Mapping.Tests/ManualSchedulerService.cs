using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProudNet.Hosting.Services;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// An ISchedulerService that captures scheduled actions instead of running them on a timer,
    /// so tests can drive time-based game state transitions deterministically via RunScheduled().
    /// </summary>
    internal sealed class ManualSchedulerService : ISchedulerService
    {
        private readonly Queue<(Action<object, object> action, object context, object state)> _scheduled =
            new Queue<(Action<object, object>, object, object)>();

        public void Execute(Action action) => action();
        public void Execute(Action<object, object> action, object context, object state) => action(context, state);

        public Task ScheduleAsync(Action action, TimeSpan delay)
        {
            _scheduled.Enqueue(((_0, _1) => action(), null, null));
            return Task.CompletedTask;
        }

        public Task ScheduleAsync(Action<object, object> action, object context, object state, TimeSpan delay)
        {
            _scheduled.Enqueue((action, context, state));
            return Task.CompletedTask;
        }

        public Task ScheduleAsync(Action<object, object> action, object context, object state,
            TimeSpan delay, CancellationToken cancellationToken)
        {
            _scheduled.Enqueue((action, context, state));
            return Task.CompletedTask;
        }

        public Task<T> SubmitAsync<T>(Func<T> func) => Task.FromResult(func());
        public Task<T> SubmitAsync<T>(Func<T> func, CancellationToken cancellationToken) => Task.FromResult(func());
        public Task<T> SubmitAsync<T>(Func<object, T> func, object state) => Task.FromResult(func(state));
        public Task<T> SubmitAsync<T>(Func<object, T> func, object state, CancellationToken cancellationToken) => Task.FromResult(func(state));
        public Task<T> SubmitAsync<T>(Func<object, object, T> func, object context, object state) => Task.FromResult(func(context, state));
        public Task<T> SubmitAsync<T>(Func<object, object, T> func, object context, object state, CancellationToken cancellationToken)
            => Task.FromResult(func(context, state));

        /// <summary>Runs the next scheduled action (the game state machine's time-based triggers).</summary>
        public void RunNextScheduled()
        {
            if (_scheduled.Count == 0)
                return;
            var (action, context, state) = _scheduled.Dequeue();
            action(context, state);
        }

        /// <summary>Runs all pending scheduled actions.</summary>
        public void RunAllScheduled()
        {
            while (_scheduled.Count > 0)
                RunNextScheduled();
        }
    }
}
