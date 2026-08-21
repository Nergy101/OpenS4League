using System.Collections.Concurrent;

namespace OpenS4L.Blub.Collections.Concurrent;

public class AsyncStack<T> : AsyncCollection<T>
{
	public AsyncStack()
		: base((IProducerConsumerCollection<T>)new ConcurrentStack<T>())
	{
	}
}
