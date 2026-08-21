using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace OpenS4L.Blub.Threading.Tasks;

public static class TaskExtensions
{
	public static void Ignore(this Task @this)
	{
	}

	public static ConfiguredTaskAwaitable AnyContext(this Task @this)
	{
		return @this.ConfigureAwait(continueOnCapturedContext: false);
	}

	public static ConfiguredTaskAwaitable<TResult> AnyContext<TResult>(this Task<TResult> @this)
	{
		return @this.ConfigureAwait(continueOnCapturedContext: false);
	}

	public static Task WaitAsync(this Task @this, CancellationToken cancellationToken)
	{
		if (!cancellationToken.CanBeCanceled)
		{
			return @this;
		}
		if (cancellationToken.IsCancellationRequested)
		{
			return Task.FromCanceled(cancellationToken);
		}
		return WaitAsyncInternal(@this, cancellationToken);
	}

	public static Task<TResult> WaitAsync<TResult>(this Task<TResult> @this, CancellationToken cancellationToken)
	{
		if (!cancellationToken.CanBeCanceled)
		{
			return @this;
		}
		if (cancellationToken.IsCancellationRequested)
		{
			cancellationToken.ThrowIfCancellationRequested();
		}
		return WaitAsyncInternal<TResult>(@this, cancellationToken);
	}

	public static void WaitEx(this Task @this)
	{
		@this.GetAwaiter().GetResult();
	}

	public static void WaitEx(this Task @this, CancellationToken cancellationToken)
	{
		try
		{
			@this.Wait(cancellationToken);
		}
		catch (AggregateException ex)
		{
			throw ex.GetBaseException().Rethrow();
		}
	}

	public static bool WaitEx(this Task @this, TimeSpan timeout)
	{
		try
		{
			return @this.Wait(timeout);
		}
		catch (AggregateException ex)
		{
			throw ex.GetBaseException().Rethrow();
		}
	}

	public static bool WaitEx(this Task @this, int millisecondsTimeout)
	{
		try
		{
			return @this.Wait(millisecondsTimeout);
		}
		catch (AggregateException ex)
		{
			throw ex.GetBaseException().Rethrow();
		}
	}

	public static bool WaitEx(this Task @this, int millisecondsTimeout, CancellationToken cancellationToken)
	{
		try
		{
			return @this.Wait(millisecondsTimeout, cancellationToken);
		}
		catch (AggregateException ex)
		{
			throw ex.GetBaseException().Rethrow();
		}
	}

	public static TResult WaitEx<TResult>(this Task<TResult> @this)
	{
		return @this.GetAwaiter().GetResult();
	}

	public static TResult WaitEx<TResult>(this Task<TResult> @this, CancellationToken cancellationToken)
	{
		try
		{
			@this.Wait(cancellationToken);
			return @this.Result;
		}
		catch (AggregateException ex)
		{
			throw ex.GetBaseException().Rethrow();
		}
	}

	public static Task ContinueWith<TState>(this Task @this, Action<Task, TState> continuationAction, TState state)
	{
		return @this.ContinueWith(ContinueWithCallback<TState>, Tuple.Create(continuationAction, state));
	}

	public static Task ContinueWith<TState>(this Task @this, Action<Task, TState> continuationAction, TState state, CancellationToken cancellationToken)
	{
		return @this.ContinueWith(ContinueWithCallback<TState>, Tuple.Create(continuationAction, state), cancellationToken);
	}

	public static Task ContinueWith<TState>(this Task @this, Action<Task, TState> continuationAction, TState state, TaskScheduler scheduler)
	{
		return @this.ContinueWith(ContinueWithCallback<TState>, Tuple.Create(continuationAction, state), scheduler);
	}

	public static Task ContinueWith<TState>(this Task @this, Action<Task, TState> continuationAction, TState state, TaskContinuationOptions continuationOptions)
	{
		return @this.ContinueWith(ContinueWithCallback<TState>, Tuple.Create(continuationAction, state), continuationOptions);
	}

	public static Task ContinueWith<TState>(this Task @this, Action<Task, TState> continuationAction, TState state, CancellationToken cancellationToken, TaskContinuationOptions continuationOptions, TaskScheduler scheduler)
	{
		return @this.ContinueWith(ContinueWithCallback<TState>, Tuple.Create(continuationAction, state), cancellationToken, continuationOptions, scheduler);
	}

	public static Task<TResult> ContinueWith<TState, TResult>(this Task @this, Func<Task, TState, TResult> continuationAction, TState state)
	{
		return @this.ContinueWith((Func<Task, object, TResult>)ContinueWithCallback<TState, TResult>, (object)Tuple.Create(continuationAction, state));
	}

	public static Task<TResult> ContinueWith<TState, TResult>(this Task @this, Func<Task, TState, TResult> continuationAction, TState state, CancellationToken cancellationToken)
	{
		return @this.ContinueWith((Func<Task, object, TResult>)ContinueWithCallback<TState, TResult>, (object)Tuple.Create(continuationAction, state), cancellationToken);
	}

	public static Task<TResult> ContinueWith<TState, TResult>(this Task @this, Func<Task, TState, TResult> continuationAction, TState state, TaskScheduler scheduler)
	{
		return @this.ContinueWith((Func<Task, object, TResult>)ContinueWithCallback<TState, TResult>, (object)Tuple.Create(continuationAction, state), scheduler);
	}

	public static Task<TResult> ContinueWith<TState, TResult>(this Task @this, Func<Task, TState, TResult> continuationAction, TState state, TaskContinuationOptions continuationOptions)
	{
		return @this.ContinueWith((Func<Task, object, TResult>)ContinueWithCallback<TState, TResult>, (object)Tuple.Create(continuationAction, state), continuationOptions);
	}

	public static Task<TResult> ContinueWith<TState, TResult>(this Task @this, Func<Task, TState, TResult> continuationAction, TState state, CancellationToken cancellationToken, TaskContinuationOptions continuationOptions, TaskScheduler scheduler)
	{
		return @this.ContinueWith((Func<Task, object, TResult>)ContinueWithCallback<TState, TResult>, (object)Tuple.Create(continuationAction, state), cancellationToken, continuationOptions, scheduler);
	}

	private static async Task WaitAsyncInternal(Task task, CancellationToken cancellationToken)
	{
		TaskCompletionSource taskCompletionSource = new TaskCompletionSource();
		CancellationTokenRegistration cancellationTokenRegistration = cancellationToken.Register(delegate(Tuple<TaskCompletionSource, CancellationToken> state)
		{
			state.Item1.TrySetCanceled(state.Item2);
		}, Tuple.Create(taskCompletionSource, cancellationToken), useSynchronizationContext: false);
		using (cancellationTokenRegistration)
		{
			await (await Task.WhenAny(task, taskCompletionSource.Task).AnyContext()).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private static async Task<TResult> WaitAsyncInternal<TResult>(Task task, CancellationToken cancellationToken)
	{
		TaskCompletionSource taskCompletionSource = new TaskCompletionSource();
		CancellationTokenRegistration cancellationTokenRegistration = cancellationToken.Register(delegate(Tuple<TaskCompletionSource, CancellationToken> state)
		{
			state.Item1.TrySetCanceled(state.Item2);
		}, Tuple.Create(taskCompletionSource, cancellationToken), useSynchronizationContext: false);
		using (cancellationTokenRegistration)
		{
			Task completedTask = await Task.WhenAny(task, taskCompletionSource.Task).ConfigureAwait(continueOnCapturedContext: false);
			await completedTask.AnyContext();
			if (completedTask == task)
			{
				return ((Task<TResult>)completedTask).Result;
			}
		}
		throw new Exception("This should never happen");
	}

	private static void ContinueWithCallback<TState>(Task task, object state)
	{
		Tuple<Action<Task, TState>, TState> tuple = (Tuple<Action<Task, TState>, TState>)state;
		tuple.Item1(task, tuple.Item2);
	}

	private static TResult ContinueWithCallback<TState, TResult>(Task task, object state)
	{
		Tuple<Func<Task, TState, TResult>, TState> tuple = (Tuple<Func<Task, TState, TResult>, TState>)state;
		return tuple.Item1(task, tuple.Item2);
	}
}
