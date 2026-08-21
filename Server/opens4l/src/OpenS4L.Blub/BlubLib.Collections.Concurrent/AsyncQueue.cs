using System.Collections.Concurrent;

namespace OpenS4L.Blub.Collections.Concurrent;

public class AsyncQueue<T> : AsyncCollection<T>
{
	public AsyncQueue()
		: base((IProducerConsumerCollection<T>)new ConcurrentQueue<T>())
	{
	}
}
