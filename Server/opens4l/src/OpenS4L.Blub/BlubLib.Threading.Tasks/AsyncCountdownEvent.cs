using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenS4L.Blub.Threading.Tasks;

public class AsyncCountdownEvent
{
	private readonly AsyncManualResetEvent _finishedEvent;

	private int _currentCount;

	public int CurrentCount => _currentCount;

	public bool IsSet => _finishedEvent.IsSet;

	public AsyncCountdownEvent()
		: this(0)
	{
	}

	public AsyncCountdownEvent(int initialCount)
	{
		if (initialCount < 0)
		{
			throw new ArgumentOutOfRangeException("initialCount");
		}
		_finishedEvent = new AsyncManualResetEvent();
		_currentCount = initialCount;
		if (initialCount < 1)
		{
			_finishedEvent.Set();
		}
	}

	public void Wait()
	{
		Wait(CancellationToken.None);
	}

	public void Wait(CancellationToken cancellationToken)
	{
		WaitAsync(cancellationToken).WaitEx(CancellationToken.None);
	}

	public void SignalAndWait()
	{
		SignalAndWait(CancellationToken.None);
	}

	public void SignalAndWait(CancellationToken cancellationToken)
	{
		SignalAndWaitAsync(cancellationToken).WaitEx(cancellationToken);
	}

	public Task WaitAsync()
	{
		return WaitAsync(CancellationToken.None);
	}

	public Task WaitAsync(CancellationToken cancellationToken)
	{
		return _finishedEvent.WaitAsync(cancellationToken);
	}

	public Task SignalAndWaitAsync()
	{
		return SignalAndWaitAsync(CancellationToken.None);
	}

	public Task SignalAndWaitAsync(CancellationToken cancellationToken)
	{
		Signal();
		return WaitAsync(cancellationToken);
	}

	public void AddCount()
	{
		AddCount(1);
	}

	public void AddCount(int signalCount)
	{
		if (signalCount < 1)
		{
			throw new ArgumentOutOfRangeException("signalCount");
		}
		ModifyCount(signalCount);
	}

	public void Signal()
	{
		Signal(1);
	}

	public void Signal(int signalCount)
	{
		if (signalCount < 1)
		{
			throw new ArgumentOutOfRangeException("signalCount");
		}
		ModifyCount(-signalCount);
	}

	private void ModifyCount(int signalCount)
	{
		SpinWait spinWait = default(SpinWait);
		int currentCount;
		int num;
		while (true)
		{
			currentCount = _currentCount;
			num = currentCount + signalCount;
			if (num < 0)
			{
				num = 0;
			}
			if (Interlocked.CompareExchange(ref _currentCount, num, currentCount) == currentCount)
			{
				break;
			}
			spinWait.SpinOnce();
		}
		if (currentCount < 1 && num > 0)
		{
			_finishedEvent.Reset();
		}
		if (currentCount > 0 && num < 1)
		{
			_finishedEvent.Set();
		}
	}
}
