using System;
using System.Threading;

namespace OpenS4L.Blub.Threading.Tasks;

public static class CancellationTokenExtensions
{
	public static CancellationTokenRegistration Register<TState>(this CancellationToken @this, Action<TState> callback, TState state)
	{
		return @this.Register(RegisterCallback<TState>, Tuple.Create(callback, state));
	}

	public static CancellationTokenRegistration Register<TState>(this CancellationToken @this, Action<TState> callback, TState state, bool useSynchronizationContext)
	{
		return @this.Register(RegisterCallback<TState>, Tuple.Create(callback, state), useSynchronizationContext);
	}

	private static void RegisterCallback<TState>(object state)
	{
		Tuple<Action<TState>, TState> tuple = (Tuple<Action<TState>, TState>)state;
		tuple.Item1(tuple.Item2);
	}
}
