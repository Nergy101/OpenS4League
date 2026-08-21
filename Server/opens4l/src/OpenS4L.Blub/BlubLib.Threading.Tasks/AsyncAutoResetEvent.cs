using System.Threading;
using System.Threading.Tasks;

namespace OpenS4L.Blub.Threading.Tasks;

public class AsyncAutoResetEvent
{
	private readonly AwaiterQueue _awaiterQueue = new AwaiterQueue();

	private readonly object _mutex = new object();

	private volatile bool _isSet;

	public bool IsSet => _isSet;

	public AsyncAutoResetEvent()
		: this(set: false)
	{
	}

	public AsyncAutoResetEvent(bool set)
	{
		_isSet = set;
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
			if (IsSet)
			{
				Reset();
				return Task.CompletedTask;
			}
			return _awaiterQueue.Enqueue(_mutex, cancellationToken);
		}
	}

	public void Set()
	{
		lock (_mutex)
		{
			if (_awaiterQueue.IsEmpty)
			{
				_isSet = true;
			}
			else
			{
				_awaiterQueue.CompleteOne();
			}
		}
	}

	public void Reset()
	{
		lock (_mutex)
		{
			_isSet = false;
		}
	}
}
