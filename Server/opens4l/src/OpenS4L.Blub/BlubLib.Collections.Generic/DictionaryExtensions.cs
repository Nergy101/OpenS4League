using System;
using System.Collections.Generic;

namespace OpenS4L.Blub.Collections.Generic;

public static class DictionaryExtensions
{
	public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, TValue value)
	{
		if (@this.ContainsKey(key))
		{
			return false;
		}
		@this.Add(key, value);
		return true;
	}

	public static bool TryRemove<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, out TValue value)
	{
		if (@this.TryGetValue(key, out value))
		{
			return @this.Remove(key);
		}
		return false;
	}

	public static TValue AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, Func<TKey, TValue> addValueFactory, Func<TKey, TValue, TValue> updateValueFactory)
	{
		if (@this.TryGetValue(key, out var value))
		{
			return @this[key] = updateValueFactory(key, value);
		}
		TValue val2 = addValueFactory(key);
		@this.Add(key, val2);
		return val2;
	}

	public static TValue AddOrUpdate<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory)
	{
		if (@this.TryGetValue(key, out var value))
		{
			return @this[key] = updateValueFactory(key, value);
		}
		@this.Add(key, addValue);
		return addValue;
	}

	public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key)
	{
		if (!@this.TryGetValue(key, out var value))
		{
			return default(TValue);
		}
		return value;
	}

	public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> @this, TKey key)
	{
		if (!@this.TryGetValue(key, out var value))
		{
			return default(TValue);
		}
		return value;
	}

	public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> @this, TKey key)
	{
		if (!@this.TryGetValue(key, out var value))
		{
			return default(TValue);
		}
		return value;
	}
}
