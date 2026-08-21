using System;
using System.Threading.Tasks;

namespace OpenS4L.Blub.Threading.Tasks;

public class AsyncLock
{
	private sealed class Releaser : IDisposable
	{
		private readonly AsyncLock _toRelease;

		public Releaser(AsyncLock toRelease)
		{
			_toRelease = toRelease;
		}

		public void Dispose()
		{
			_toRelease.Release();
		}
	}

	private readonly AsyncSemaphore _semaphore = new AsyncSemaphore(1, 1);

	private readonly IDisposable _releaser;

	public AsyncLock()
	{
		_releaser = new Releaser(this);
	}

	public IDisposable Lock()
	{
		_semaphore.Wait();
		return _releaser;
	}

	public Task<IDisposable> LockAsync()
	{
		return _semaphore.WaitAsync().ContinueWith((Task _, IDisposable state) => state, _releaser);
	}

	internal void Release()
	{
		_semaphore.Release();
	}
}
