using System;
using System.Threading.Tasks;

namespace OpenS4L.Blub.Threading.Tasks;

public class DeferralManager
{
	private class Deferral : IDisposable
	{
		private AsyncCountdownEvent _countdownEvent;

		public Deferral(AsyncCountdownEvent countdownEvent)
		{
			_countdownEvent = countdownEvent;
			_countdownEvent.AddCount();
		}

		public void Dispose()
		{
			_countdownEvent?.Signal();
			_countdownEvent = null;
		}
	}

	private readonly AsyncCountdownEvent _countdownEvent = new AsyncCountdownEvent(1);

	public IDisposable GetDeferral()
	{
		return new Deferral(_countdownEvent);
	}

	public void Wait()
	{
		_countdownEvent.Wait();
	}

	public void SignalAndWait()
	{
		_countdownEvent.SignalAndWait();
	}

	public Task WaitAsync()
	{
		return _countdownEvent.WaitAsync();
	}

	public Task SignalAndWaitAsync()
	{
		return _countdownEvent.SignalAndWaitAsync();
	}
}
