using System;

namespace OpenS4L.Blub.Threading;

public sealed class ThreadLoop : ThreadLoopBase
{
	private readonly Action<TimeSpan> _callback;

	public ThreadLoop(TimeSpan tickRate, Action<TimeSpan> callback)
		: base(tickRate)
	{
		if (callback == null)
		{
			throw new ArgumentNullException("callback");
		}
		_callback = callback;
	}

	public ThreadLoop(TimeSpan tickRate, Action callback)
		: base(tickRate)
	{
		if (callback == null)
		{
			throw new ArgumentNullException("callback");
		}
		_callback = delegate
		{
			callback();
		};
	}

	protected override void OnTick(TimeSpan elapsed)
	{
		_callback(elapsed);
	}
}
