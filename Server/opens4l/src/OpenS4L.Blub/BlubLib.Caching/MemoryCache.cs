using System;
using System.Collections.Concurrent;
using OpenS4L.Blub.Collections.Concurrent;

namespace OpenS4L.Blub.Caching;

public sealed class MemoryCache : ICache, IDisposable
{
	private struct CacheEntry
	{
		public long ExpireTime { get; }

		public object Item { get; }

		public CacheEntry(long expireTime, object item)
		{
			this = default(CacheEntry);
			ExpireTime = expireTime;
			Item = item;
		}
	}

	private readonly ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>();

	public void Set(string key, object value)
	{
		Set(key, value, TimeSpan.Zero);
	}

	public void Set(string key, object value, TimeSpan ttl)
	{
		long expireTime = ((ttl != TimeSpan.Zero) ? (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)ttl.TotalSeconds) : 0);
		CacheEntry entry = new CacheEntry(expireTime, value);
		_cache.AddOrUpdate(key, entry, (string k, CacheEntry o) => entry);
	}

	public object Get(string key)
	{
		if (!_cache.TryGetValue(key, out var value))
		{
			return null;
		}
		if (value.ExpireTime == 0L)
		{
			return value.Item;
		}
		if (value.ExpireTime > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
		{
			return value.Item;
		}
		Remove(key);
		return null;
	}

	public T Get<T>(string key)
	{
		object obj = Get(key);
		if (obj != null)
		{
			return DynamicCast<T>.From(obj);
		}
		return default(T);
	}

	public bool Remove(string key)
	{
		return _cache.Remove(key);
	}

	public void Clear()
	{
		_cache.Clear();
	}

	public void Dispose()
	{
		Clear();
	}
}
