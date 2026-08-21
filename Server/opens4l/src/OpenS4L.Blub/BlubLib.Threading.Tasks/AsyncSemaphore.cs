using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenS4L.Blub.Threading.Tasks;

public class AsyncSemaphore
{
	private readonly AwaiterQueue _awaiterQueue = new AwaiterQueue();

	private readonly object _mutex = new object();

	private volatile int _currentCount;

	public int CurrentCount => _currentCount;

	public int MaxCount { get; }

	public bool IsAvailable => CurrentCount > 0;

	public AsyncSemaphore(int initialCount)
		: this(initialCount, int.MaxValue)
	{
	}

	public AsyncSemaphore(int initialCount, int maxCount)
	{
		if (initialCount < 0 || initialCount > maxCount)
		{
			throw new ArgumentOutOfRangeException("initialCount");
		}
		if (maxCount <= 0)
		{
			throw new ArgumentOutOfRangeException("maxCount");
		}
		_currentCount = initialCount;
		MaxCount = maxCount;
	}

	public void Wait()
	{
		Wait(CancellationToken.None);
	}

	public void Wait(CancellationToken cancellationToken)
	{
		WaitAsync(cancellationToken).WaitEx(CancellationToken.None);
	}

	public Task WaitAsync()
	{
		return WaitAsync(CancellationToken.None);
	}

	public Task WaitAsync(CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested)
		{
			return Task.FromCanceled(cancellationToken);
		}
		lock (_mutex)
		{
			if (_currentCount < 1)
			{
				return _awaiterQueue.Enqueue(_mutex, cancellationToken);
			}
			_currentCount--;
			return Task.CompletedTask;
		}
	}

	public void Release()
	{
		Release(1);
	}

	public void Release(int releaseCount)
	{
		if (releaseCount < 1)
		{
			throw new ArgumentOutOfRangeException("releaseCount");
		}
		lock (_mutex)
		{
			while (releaseCount > 0 && _currentCount < MaxCount)
			{
				if (_awaiterQueue.IsEmpty)
				{
					_currentCount++;
				}
				else
				{
					_awaiterQueue.CompleteOne();
				}
				releaseCount--;
			}
		}
	}
}
