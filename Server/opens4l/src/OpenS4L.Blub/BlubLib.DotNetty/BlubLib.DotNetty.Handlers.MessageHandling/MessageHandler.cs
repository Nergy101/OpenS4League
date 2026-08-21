using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using OpenS4L.Blub.Collections.Generic;
using DotNetty.Transport.Channels;

namespace OpenS4L.Blub.DotNetty.Handlers.MessageHandling;

public abstract class MessageHandler<TKey> : IMessageHandler
{
	protected delegate void Handler(MessageHandler<TKey> handler, IChannelHandlerContext context, object message);

	protected delegate Task AsyncHandler(MessageHandler<TKey> handler, IChannelHandlerContext context, object message);

	protected IDictionary<TKey, Handler> Handlers { get; set; }

	protected IDictionary<TKey, AsyncHandler> AsyncHandlers { get; set; }

	protected MessageHandler()
	{
		RegisterFromAttribute();
	}

	public virtual Task<bool> OnMessageReceived(IChannelHandlerContext context, object message)
	{
		Handler handler = GetHandler(context, message);
		if (handler != null)
		{
			handler(this, context, message);
			return Task.FromResult(result: true);
		}
		AsyncHandler asyncHandler = GetAsyncHandler(context, message);
		if (asyncHandler == null)
		{
			return Task.FromResult(result: false);
		}
		return asyncHandler(this, context, message).ContinueWith(delegate(Task task, object _)
		{
			task.Exception?.Rethrow();
			return true;
		}, null, TaskContinuationOptions.ExecuteSynchronously);
	}

	protected virtual void RegisterHandler(object key, Handler handler)
	{
		if (Handlers == null)
		{
			Handlers = new Dictionary<TKey, Handler>();
		}
		Handlers.Add((TKey)key, handler);
	}

	protected virtual void RegisterAsyncHandler(object key, AsyncHandler handler)
	{
		if (AsyncHandlers == null)
		{
			AsyncHandlers = new Dictionary<TKey, AsyncHandler>();
		}
		AsyncHandlers.Add((TKey)key, handler);
	}

	protected abstract Handler GetHandler(IChannelHandlerContext context, object message);

	protected abstract AsyncHandler GetAsyncHandler(IChannelHandlerContext context, object message);

	protected virtual object GetMessageObject(object message)
	{
		return message;
	}

	protected virtual bool GetParameter<T>(IChannelHandlerContext context, object message, out T value)
	{
		value = default(T);
		return false;
	}

	private void RegisterFromAttribute()
	{
		MethodInfo[] methods = GetType().GetMethods();
		if (methods.Length == 0)
		{
			return;
		}
		MethodInfo[] array = methods;
		foreach (MethodInfo methodInfo in array)
		{
			MessageHandlerAttribute customAttribute = methodInfo.GetCustomAttribute<MessageHandlerAttribute>();
			if (customAttribute != null)
			{
				bool num = typeof(Task).IsAssignableFrom(methodInfo.ReturnType);
				LambdaExpression lambdaExpression = BuildFromMethod(methodInfo);
				if (num)
				{
					Func<MessageHandler<TKey>, IChannelHandlerContext, object, Task> func = (Func<MessageHandler<TKey>, IChannelHandlerContext, object, Task>)lambdaExpression.Compile();
					RegisterAsyncHandler(customAttribute.MessageId, func.Invoke);
				}
				else
				{
					Action<MessageHandler<TKey>, IChannelHandlerContext, object> action = (Action<MessageHandler<TKey>, IChannelHandlerContext, object>)lambdaExpression.Compile();
					RegisterHandler(customAttribute.MessageId, action.Invoke);
				}
			}
		}
	}

	private LambdaExpression BuildFromMethod(MethodInfo method)
	{
		ParameterExpression handlerParam = Expression.Parameter(typeof(MessageHandler<TKey>), "messageHandler");
		ParameterExpression contextParam = Expression.Parameter(typeof(IChannelHandlerContext), "channelHandlerContext");
		ParameterExpression messageParam = Expression.Parameter(typeof(object), "message");
		bool isAsync = typeof(Task).IsAssignableFrom(method.ReturnType);
		ParameterExpression @this = Expression.Variable(GetType(), "@this");
		List<ParameterExpression> list = new List<ParameterExpression> { @this };
		Expression[] expressions = GenerateBody(list).ToArray();
		return Expression.Lambda(Expression.Block(list, expressions), method.Name, new ParameterExpression[3] { handlerParam, contextParam, messageParam });
		IEnumerable<Expression> GenerateBody(IList<ParameterExpression> outVariables)
		{
			yield return Expression.Assign(@this, Expression.Convert(handlerParam, GetType()));
			List<Expression> parameters = new List<Expression>();
			foreach (Expression item in HandlerParameters(parameters, outVariables))
			{
				yield return item;
			}
			Expression handlerCall = Expression.Call(@this, method, parameters);
			if (isAsync)
			{
				LabelTarget target = Expression.Label(typeof(Task));
				LabelExpression arg = Expression.Label(target, Expression.Constant(null, typeof(Task)));
				yield return Expression.Block(Expression.Return(target, handlerCall), arg);
			}
			else
			{
				yield return handlerCall;
			}
		}
		IEnumerable<Expression> HandlerParameters(IList<Expression> parameters, IList<ParameterExpression> outVariables)
		{
			ParameterInfo[] @params = method.GetParameters();
			for (int i = 0; i < @params.Length; i++)
			{
				ParameterInfo parameterInfo = @params[i];
				if (typeof(IChannelHandlerContext).IsAssignableFrom(parameterInfo.ParameterType))
				{
					parameters.Add(Expression.Convert(contextParam, parameterInfo.ParameterType));
				}
				else if (typeof(IChannel).IsAssignableFrom(parameterInfo.ParameterType))
				{
					MemberExpression expression = Expression.Property(contextParam, "Channel");
					parameters.Add(Expression.Convert(expression, parameterInfo.ParameterType));
				}
				else
				{
					MethodInfo method2 = GetType().GetMethod("GetParameter", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(parameterInfo.ParameterType);
					MethodInfo method3 = GetType().GetMethod("GetMessageObject", BindingFlags.Instance | BindingFlags.NonPublic);
					ParameterExpression parameterExpression = Expression.Variable(parameterInfo.ParameterType, $"value{i}");
					outVariables.Add(parameterExpression);
					MethodCallExpression expression2 = Expression.Call(@this, method2, contextParam, messageParam, parameterExpression);
					parameters.Add(parameterExpression);
					yield return Expression.IfThen(Expression.IsFalse(expression2), Expression.Assign(parameterExpression, Expression.Convert(Expression.Call(@this, method3, messageParam), parameterInfo.ParameterType)));
				}
			}
		}
	}
}
public class MessageHandler : MessageHandler<Type>
{
	protected override Handler GetHandler(IChannelHandlerContext context, object message)
	{
		return base.Handlers?.GetValueOrDefault(message.GetType());
	}

	protected override AsyncHandler GetAsyncHandler(IChannelHandlerContext context, object message)
	{
		return base.AsyncHandlers?.GetValueOrDefault(message.GetType());
	}
}
