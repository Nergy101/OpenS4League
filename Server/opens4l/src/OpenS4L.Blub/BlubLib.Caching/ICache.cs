using System;

namespace OpenS4L.Blub.Caching;

public interface ICache : IDisposable
{
	void Set(string key, object value);

	void Set(string key, object value, TimeSpan ttl);

	object Get(string key);

	T Get<T>(string key);

	bool Remove(string key);

	void Clear();
}
