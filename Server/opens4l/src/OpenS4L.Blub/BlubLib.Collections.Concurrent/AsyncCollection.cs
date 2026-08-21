using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenS4L.Blub.Threading.Tasks;

namespace OpenS4L.Blub.Collections.Concurrent;

public class AsyncCollection<T> : IProducerConsumerCollection<T>, IEnumerable<T>, IEnumerable, ICollection
{
	private readonly IProducerConsumerCollection<T> _items;

	private readonly object _mutex = new object();

	private readonly AwaiterQueue<T> _awaiterQueue = new AwaiterQueue<T>();

	public int Count => _items.Count;

	public bool IsEmpty => Count == 0;

	object ICollection.SyncRoot
	{
		get
		{
			throw new NotSupportedException();
		}
	}

	bool ICollection.IsSynchronized => false;

	public AsyncCollection()
		: this((IProducerConsumerCollection<T>)new ConcurrentQueue<T>())
	{
	}

	public AsyncCollection(IProducerConsumerCollection<T> items)
	{
		_items = items;
	}

	public void Add(T item)
	{
		SpinWait spinWait = default(SpinWait);
		while (!TryAdd(item))
		{
			spinWait.SpinOnce();
		}
	}

	public bool TryAdd(T item)
	{
		lock (_mutex)
		{
			if (_awaiterQueue.IsEmpty)
			{
				return _items.TryAdd(item);
			}
			return _awaiterQueue.CompleteOne(item);
		}
	}

	public T Take()
	{
		return Take(CancellationToken.None);
	}

	public T Take(CancellationToken cancellationToken)
	{
		return TakeAsync(cancellationToken).WaitEx();
	}

	public Task<T> TakeAsync()
	{
		return TakeAsync(CancellationToken.None);
	}

	public Task<T> TakeAsync(CancellationToken cancellationToken)
	{
		lock (_mutex)
		{
			if (_items.Count > 0)
			{
				SpinWait spinWait = default(SpinWait);
				T item;
				while (!_items.TryTake(out item))
				{
					spinWait.SpinOnce();
				}
				return Task.FromResult(item);
			}
			return _awaiterQueue.Enqueue(_mutex, cancellationToken);
		}
	}

	public bool TryTake(out T item)
	{
		lock (_mutex)
		{
			if (_items.Count > 0)
			{
				SpinWait spinWait = default(SpinWait);
				while (!_items.TryTake(out item))
				{
					spinWait.SpinOnce();
				}
				return true;
			}
			item = default(T);
			return false;
		}
	}

	public void CopyTo(Array array, int index)
	{
		_items.CopyTo(array, index);
	}

	public void CopyTo(T[] array, int index)
	{
		_items.CopyTo(array, index);
	}

	public T[] ToArray()
	{
		return _items.ToArray();
	}

	public IEnumerator<T> GetEnumerator()
	{
		return _items.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
