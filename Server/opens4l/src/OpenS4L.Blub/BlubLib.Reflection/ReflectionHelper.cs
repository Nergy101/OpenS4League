using System;
using System.Linq.Expressions;
using System.Reflection;

namespace OpenS4L.Blub.Reflection;

public static class ReflectionHelper
{
	public static MethodInfo GetMethod(Expression<Action> expression)
	{
		return ExtractMethod(expression.Body);
	}

	public static MethodInfo GetMethod<TArg0>(Expression<Action<TArg0>> expression)
	{
		return ExtractMethod(expression.Body);
	}

	public static MethodInfo GetMethod<TArg0, TArg1>(Expression<Action<TArg0, TArg1>> expression)
	{
		return ExtractMethod(expression.Body);
	}

	public static MethodInfo GetMethod<TArg0, TArg1, TArg2>(Expression<Action<TArg0, TArg1, TArg2>> expression)
	{
		return ExtractMethod(expression.Body);
	}

	public static MethodInfo GetMethod<TArg0, TArg1, TArg2, TArg3>(Expression<Action<TArg0, TArg1, TArg2, TArg3>> expression)
	{
		return ExtractMethod(expression.Body);
	}

	public static MethodInfo GetMethod<TArg0, TArg1, TArg2, TArg3, TArg4>(Expression<Action<TArg0, TArg1, TArg2, TArg3, TArg4>> expression)
	{
		return ExtractMethod(expression.Body);
	}

	public static MethodInfo GetMethod<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5>(Expression<Action<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5>> expression)
	{
		return ExtractMethod(expression.Body);
	}

	public static MethodInfo GetMethod<TReturn>(Expression<Func<TReturn>> expression)
	{
		return ExtractMethod(expression.Body);
	}

	public static MethodInfo GetMethod<TArg0, TReturn>(Expression<Func<TArg0, TReturn>> expression)
	{
		return ExtractMethod(expression.Body);
	}

	public static MethodInfo GetMethod<TArg0, TArg1, TReturn>(Expression<Func<TArg0, TArg1, TReturn>> expression)
	{
		return ExtractMethod(expression.Body);
	}

	public static MethodInfo GetMethod<TArg0, TArg1, TArg2, TReturn>(Expression<Func<TArg0, TArg1, TArg2, TReturn>> expression)
	{
		return ExtractMethod(expression.Body);
	}

	public static MethodInfo GetMethod<TArg0, TArg1, TArg2, TArg3, TReturn>(Expression<Func<TArg0, TArg1, TArg2, TArg3, TReturn>> expression)
	{
		return ExtractMethod(expression.Body);
	}

	public static MethodInfo GetMethod<TArg0, TArg1, TArg2, TArg3, TArg4, TReturn>(Expression<Func<TArg0, TArg1, TArg2, TArg3, TArg4, TReturn>> expression)
	{
		return ExtractMethod(expression.Body);
	}

	public static MethodInfo GetMethod<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TReturn>(Expression<Func<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TReturn>> expression)
	{
		return ExtractMethod(expression.Body);
	}

	private static MethodInfo ExtractMethod(Expression expression)
	{
		return (expression as MethodCallExpression)?.Method;
	}
}
