using System.Collections.Concurrent;

namespace OpenS4L.Blub.Collections.Concurrent;

public static class ConcurrentDictionaryExtensions
{
	public static bool Remove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> @this, TKey key)
	{
		TValue value;
		return @this.TryRemove(key, out value);
	}

	public static TValue GetValueOrDefault<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> @this, TKey key)
	{
		if (!@this.TryGetValue(key, out var value))
		{
			return default(TValue);
		}
		return value;
	}
}
