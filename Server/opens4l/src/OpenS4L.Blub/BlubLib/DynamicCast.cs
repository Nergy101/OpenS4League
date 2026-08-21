using System;
using System.Linq.Expressions;

namespace OpenS4L.Blub;

public static class DynamicCast<TTarget>
{
	private static class FunctionCache<TSource>
	{
		public static Func<TSource, TTarget> Function { get; } = Generate();

		private static Func<TSource, TTarget> Generate()
		{
			ParameterExpression parameterExpression = Expression.Parameter(typeof(TSource));
			return Expression.Lambda<Func<TSource, TTarget>>(Expression.ConvertChecked(parameterExpression, typeof(TTarget)), new ParameterExpression[1] { parameterExpression }).Compile();
		}
	}

	public static TTarget From<TSource>(TSource value)
	{
		return FunctionCache<TSource>.Function(value);
	}
}
