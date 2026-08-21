using System.Threading.Tasks;
using DotNetty.Transport.Channels;

namespace OpenS4L.Blub.DotNetty.Handlers.MessageHandling;

public interface IMessageHandler
{
	Task<bool> OnMessageReceived(IChannelHandlerContext context, object message);
}
