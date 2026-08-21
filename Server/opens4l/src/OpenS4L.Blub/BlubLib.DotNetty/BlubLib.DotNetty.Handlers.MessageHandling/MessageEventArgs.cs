using System;

namespace OpenS4L.Blub.DotNetty.Handlers.MessageHandling;

public class MessageEventArgs : EventArgs
{
	public object Message { get; }

	public MessageEventArgs(object message)
	{
		Message = message;
	}
}
