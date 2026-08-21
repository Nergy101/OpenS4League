using System;
using System.Linq.Expressions;

namespace OpenS4L.Blub;

public static class FastActivator<T>
{
	private static readonly Lazy<Func<T>> s_func = new Lazy<Func<T>>(() => CreateExpression().Compile());

	private static readonly Lazy<Func<int, T[]>> s_arrayFunc = new Lazy<Func<int, T[]>>(() => CreateArrayExpression().Compile());

	public static T Create()
	{
		return s_func.Value();
	}

	public static T[] CreateArray(int length)
	{
		return s_arrayFunc.Value(length);
	}

	private static Expression<Func<T>> CreateExpression()
	{
		return Expression.Lambda<Func<T>>(Expression.New(typeof(T)), Array.Empty<ParameterExpression>());
	}

	private static Expression<Func<int, T[]>> CreateArrayExpression()
	{
		return (int length) => new T[length];
	}
}
