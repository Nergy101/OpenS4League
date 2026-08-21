using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenS4L.Blub.Threading.Tasks;

namespace OpenS4L.Blub.Collections.Generic;

public static class EnumerableExtensions
{
	public static T FirstOfType<T>(this IEnumerable<object> @this)
	{
		return @this.OfType<T>().First();
	}

	public static T FirstOfTypeOrDefault<T>(this IEnumerable<object> @this)
	{
		return @this.OfType<T>().FirstOrDefault();
	}

	public static void ForEach<T>(this IEnumerable<T> @this, Action<T> callback)
	{
		foreach (T item in @this)
		{
			callback(item);
		}
	}

	public static void ForEach<T>(this IEnumerable<T> @this, Action<int, T> callbackWithIndex)
	{
		int num = 0;
		foreach (T item in @this)
		{
			callbackWithIndex(num++, item);
		}
	}

	public static async Task ForEachAsync<T>(this IEnumerable<T> @this, Func<T, Task> callback)
	{
		foreach (T item in @this)
		{
			await callback(item).AnyContext();
		}
	}

	public static async Task ForEachAsync<T>(this IEnumerable<T> @this, Func<int, T, Task> callbackWithIndex)
	{
		int i = 0;
		foreach (T item in @this)
		{
			await callbackWithIndex(i++, item).AnyContext();
		}
	}
}
