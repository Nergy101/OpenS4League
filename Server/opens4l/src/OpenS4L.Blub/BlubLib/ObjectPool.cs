using System;
using System.Collections.Concurrent;

namespace OpenS4L.Blub;

public class ObjectPool<T> : IDisposable where T : class
{
	private readonly ConcurrentStack<T> _objects = new ConcurrentStack<T>();

	private readonly Func<T> _factory;

	private readonly Action<T> _resetCallback;

	private readonly Action<T> _releaseCallback;

	private bool _disposed;

	public int Count => _objects.Count;

	public int MinimumSize { get; }

	public int MaximumSize { get; }

	public ObjectPool(int minimumSize, int maximumSize, Func<T> factory, Action<T> resetCallback = null, Action<T> releaseCallback = null)
	{
		if (minimumSize < 0)
		{
			throw new ArgumentOutOfRangeException("minimumSize");
		}
		if (maximumSize < 1)
		{
			throw new ArgumentOutOfRangeException("maximumSize");
		}
		if (factory == null)
		{
			throw new ArgumentNullException("factory");
		}
		MinimumSize = minimumSize;
		MaximumSize = maximumSize;
		_factory = factory;
		_resetCallback = resetCallback;
		_releaseCallback = releaseCallback;
	}

	public T Rent()
	{
		ThrowIfDisposed();
		if (!_objects.TryPop(out var result))
		{
			return _factory();
		}
		return result;
	}

	public void Return(T obj)
	{
		ThrowIfDisposed();
		_resetCallback?.Invoke(obj);
		if (_objects.Count < MaximumSize)
		{
			_objects.Push(obj);
		}
		else
		{
			_releaseCallback?.Invoke(obj);
		}
	}

	public void Flush()
	{
		ThrowIfDisposed();
		while (_objects.Count > MinimumSize)
		{
			if (_objects.TryPop(out var result))
			{
				_releaseCallback?.Invoke(result);
			}
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		while (_objects.Count > 0)
		{
			if (_objects.TryPop(out var result))
			{
				_releaseCallback?.Invoke(result);
			}
		}
		_disposed = true;
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(GetType().FullName);
		}
	}
}
