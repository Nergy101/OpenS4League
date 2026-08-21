using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenS4L.Blub.Threading.Tasks;

internal class AwaiterQueue
{
	private readonly LinkedList<TaskCompletionSource> _queue = new LinkedList<TaskCompletionSource>();

	public int Count => _queue.Count;

	public bool IsEmpty => _queue.Count < 1;

	public Task Enqueue()
	{
		TaskCompletionSource taskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_queue.AddLast(taskCompletionSource);
		return taskCompletionSource.Task;
	}

	public Task Enqueue(object mutex, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested)
		{
			return Task.FromCanceled(cancellationToken);
		}
		TaskCompletionSource taskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		if (cancellationToken.CanBeCanceled)
		{
			CancellationTokenRegistration cancellationTokenRegistration = cancellationToken.Register(delegate(Tuple<TaskCompletionSource, CancellationToken> state)
			{
				lock (mutex)
				{
					_queue.Remove(state.Item1);
					state.Item1.TrySetCanceled(state.Item2);
				}
			}, Tuple.Create(taskCompletionSource, cancellationToken), useSynchronizationContext: false);
			taskCompletionSource.Task.ContinueWith(delegate(Task _, IDisposable state)
			{
				state?.Dispose();
			}, cancellationTokenRegistration, CancellationToken.None);
		}
		_queue.AddLast(taskCompletionSource);
		return taskCompletionSource.Task;
	}

	public TaskCompletionSource Dequeue()
	{
		if (IsEmpty)
		{
			throw new InvalidOperationException("Queue is empty");
		}
		TaskCompletionSource value = _queue.First.Value;
		_queue.RemoveFirst();
		return value;
	}

	public bool CompleteOne()
	{
		return Dequeue().TrySetResult();
	}

	public void CompleteAll()
	{
		while (!IsEmpty)
		{
			CompleteOne();
		}
	}
}
internal class AwaiterQueue<T>
{
	private readonly LinkedList<TaskCompletionSource<T>> _queue = new LinkedList<TaskCompletionSource<T>>();

	public int Count => _queue.Count;

	public bool IsEmpty => _queue.Count < 1;

	public Task<T> Enqueue()
	{
		TaskCompletionSource<T> taskCompletionSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
		_queue.AddLast(taskCompletionSource);
		return taskCompletionSource.Task;
	}

	public Task<T> Enqueue(object mutex, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested)
		{
			return Task.FromCanceled<T>(cancellationToken);
		}
		TaskCompletionSource<T> taskCompletionSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
		if (cancellationToken.CanBeCanceled)
		{
			CancellationTokenRegistration cancellationTokenRegistration = cancellationToken.Register(delegate(Tuple<TaskCompletionSource<T>, CancellationToken> state)
			{
				lock (mutex)
				{
					_queue.Remove(state.Item1);
				}
				state.Item1.TrySetCanceled(state.Item2);
			}, Tuple.Create(taskCompletionSource, cancellationToken), useSynchronizationContext: false);
			taskCompletionSource.Task.ContinueWith(delegate(Task _, IDisposable state)
			{
				state?.Dispose();
			}, cancellationTokenRegistration, CancellationToken.None);
		}
		_queue.AddLast(taskCompletionSource);
		return taskCompletionSource.Task;
	}

	public TaskCompletionSource<T> Dequeue()
	{
		if (IsEmpty)
		{
			throw new InvalidOperationException("Queue is empty");
		}
		TaskCompletionSource<T> value = _queue.First.Value;
		_queue.RemoveFirst();
		return value;
	}

	public bool CompleteOne(T result)
	{
		return Dequeue().TrySetResult(result);
	}
}
