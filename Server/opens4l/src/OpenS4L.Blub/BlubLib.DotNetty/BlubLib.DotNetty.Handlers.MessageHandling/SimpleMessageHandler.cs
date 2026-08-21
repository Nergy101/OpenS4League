using System;
using System.Collections.Concurrent;
using OpenS4L.Blub.Collections.Concurrent;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;

namespace OpenS4L.Blub.DotNetty.Handlers.MessageHandling;

public class SimpleMessageHandler : ChannelHandlerAdapter
{
	private readonly ConcurrentDictionary<Type, IMessageHandler> _handlers = new ConcurrentDictionary<Type, IMessageHandler>();

	public event EventHandler<MessageEventArgs> MessageHandled;

	public event EventHandler<MessageEventArgs> MessageUnhandled;

	protected virtual void OnHandledMessage(object message)
	{
		MessageHandled?.Invoke(this, new MessageEventArgs(message));
	}

	protected virtual void OnUnhandledMessage(object message)
	{
		MessageUnhandled?.Invoke(this, new MessageEventArgs(message));
	}

	public override async void ChannelRead(IChannelHandlerContext context, object message)
	{
		bool release = true;
		try
		{
			bool handled = false;
			foreach (IMessageHandler value in _handlers.Values)
			{
				if (await value.OnMessageReceived(context, message))
				{
					handled = true;
				}
			}
			if (handled)
			{
				OnHandledMessage(message);
				return;
			}
			release = false;
			OnUnhandledMessage(message);
			base.ChannelRead(context, message);
		}
		catch (Exception cause)
		{
			context.Channel.Pipeline.FireExceptionCaught(cause);
		}
		finally
		{
			if (release)
			{
				ReferenceCountUtil.Release(message);
			}
		}
	}

	public SimpleMessageHandler Add(IMessageHandler handler)
	{
		if (!_handlers.TryAdd(handler.GetType(), handler))
		{
			throw new ArgumentException("Type already exists");
		}
		return this;
	}

	public T Get<T>() where T : IMessageHandler
	{
		_handlers.TryGetValue(typeof(T), out var value);
		return (T)value;
	}

	public void Remove<T>() where T : IMessageHandler
	{
		_handlers.Remove(typeof(T));
	}
}
