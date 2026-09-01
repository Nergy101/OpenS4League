using System;
using System.Net.Sockets;
using DotNetty.Transport.Channels;
using Logging;
using ProudNet.Hosting.Services;

namespace ProudNet.DotNetty.Handlers
{
    internal class ErrorHandler : ChannelHandlerAdapter
    {
        private readonly ILogger _logger;
        private readonly IProudNetServerService _server;

        public ErrorHandler(ILogger<ErrorHandler> logger, IProudNetServerService server)
        {
            _logger = logger;
            _server = server;
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            var session = context.Channel.GetAttribute(ChannelAttributes.Session).Get();
            if (exception is SocketException socketException)
            {
                if (socketException.SocketErrorCode == SocketError.ConnectionReset ||
                    socketException.SocketErrorCode == SocketError.TimedOut ||
                    socketException.SocketErrorCode == SocketError.HostUnreachable)
                {
                    session?.CloseAsync();
                    return;
                }
            }

            _logger.Error(exception, "Unhandled exception");
            _server.RaiseError(new ErrorEventArgs(session, exception));
            session?.CloseAsync();
        }
    }
}
