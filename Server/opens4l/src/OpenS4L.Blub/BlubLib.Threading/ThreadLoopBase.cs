using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OpenS4L.Blub.Threading;

public abstract class ThreadLoopBase : ILoop
{
	private const float WaitTolerance = 0.9585f;

	private CancellationTokenSource _cts;

	private double _lastTick;

	private double _lastUpdate;

	private int _tickCount;

	private Stopwatch _time;

	public Thread Thread { get; protected set; }

	public bool IsRunning { get; protected set; }

	public int TicksPerSecond { get; private set; }

	public TimeSpan TickRate { get; }

	private double Elapsed => _time.Elapsed.TotalMilliseconds - _lastTick;

	protected ThreadLoopBase(TimeSpan tickRate)
	{
		TickRate = tickRate;
	}

	public virtual void Start()
	{
		if (!IsRunning)
		{
			_cts = new CancellationTokenSource();
			IsRunning = true;
			Thread = new Thread(InternalLoop)
			{
				IsBackground = true
			};
			Thread.Start();
		}
	}

	public virtual void Stop()
	{
		Stop(Timeout.InfiniteTimeSpan);
	}

	public virtual void Stop(TimeSpan timeout)
	{
		if (IsRunning && !_cts.IsCancellationRequested)
		{
			if (Thread.CurrentThread == Thread)
			{
				timeout = TimeSpan.Zero;
			}
			_cts.Cancel();
			Thread?.Join((int)timeout.TotalMilliseconds);
		}
	}

	protected abstract void OnTick(TimeSpan elapsed);

	private void InternalLoop()
	{
		_time = Stopwatch.StartNew();
		_lastTick = _time.Elapsed.TotalMilliseconds;
		_tickCount = 0;
		_lastUpdate = 0.0;
		TicksPerSecond = 0;
		bool flag = true;
		while (!_cts.IsCancellationRequested)
		{
			if (TickRate.TotalMilliseconds > 0.0 && !flag)
			{
				try
				{
					WaitTickRequested();
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
			if (flag)
			{
				flag = false;
			}
			double elapsed = Elapsed;
			_lastTick = _time.Elapsed.TotalMilliseconds;
			_tickCount++;
			UpdateTicksPerSecond();
			OnTick(TimeSpan.FromMilliseconds(elapsed));
		}
		_time.Stop();
		Thread = null;
		IsRunning = false;
	}

	private void WaitTickRequested()
	{
		while (true)
		{
			double num = TickRate.TotalMilliseconds * 0.9585000276565552;
			double elapsed = Elapsed;
			if (elapsed >= num)
			{
				break;
			}
			Task.Delay((int)(num - elapsed)).Wait(_cts.Token);
		}
	}

	private void UpdateTicksPerSecond()
	{
		double num = _time.Elapsed.TotalSeconds - _lastUpdate;
		TicksPerSecond = (int)((double)_tickCount / num);
		_lastUpdate = _time.Elapsed.TotalSeconds;
		_tickCount = 0;
	}
}
