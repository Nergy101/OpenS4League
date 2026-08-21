using System.Collections.Concurrent;

namespace OpenS4L.Blub.Collections.Concurrent;

public class AsyncBag<T> : AsyncCollection<T>
{
	public AsyncBag()
		: base((IProducerConsumerCollection<T>)new ConcurrentBag<T>())
	{
	}
}
