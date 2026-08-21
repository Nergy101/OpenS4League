using System;
using System.Threading.Tasks;
using OpenS4L.Blub.Threading.Tasks;

namespace OpenS4L.Blub;

public static class ArrayExtensions
{
	public static void ForEach<T>(this T[] @this, Action<T> callback)
	{
		for (int i = 0; i < @this.Length; i++)
		{
			callback(@this[i]);
		}
	}

	public static void ForEach<T>(this T[] @this, Action<int, T> callbackWithIndex)
	{
		for (int i = 0; i < @this.Length; i++)
		{
			callbackWithIndex(i, @this[i]);
		}
	}

	public static async Task ForEachAsync<T>(this T[] @this, Func<T, Task> callback)
	{
		int i = 0;
		while (i < @this.Length)
		{
			await callback(@this[i]).AnyContext();
			int num = i + 1;
			i = num;
		}
	}

	public static async Task ForEachAsync<T>(this T[] @this, Func<int, T, Task> callbackWithIndex)
	{
		int i = 0;
		while (i < @this.Length)
		{
			await callbackWithIndex(i, @this[i]).AnyContext();
			int num = i + 1;
			i = num;
		}
	}
}
