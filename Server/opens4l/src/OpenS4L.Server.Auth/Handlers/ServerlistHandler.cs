using System.Threading.Tasks;
using OpenS4L.Network.Message.Auth;
using OpenS4L.Server.Auth.Rules;
using OpenS4L.Server.Auth.Services;
using ProudNet;

namespace OpenS4L.Server.Auth.Handlers
{
    internal class ServerlistHandler : IHandle<ServerListReqMessage>
    {
        private readonly ServerlistService _serverlistService;

        public ServerlistHandler(ServerlistService serverlistService)
        {
            _serverlistService = serverlistService;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Inline]
        public async Task<bool> OnHandle(MessageContext context, ServerListReqMessage message)
        {
            var session = context.Session;

            var servers = _serverlistService.GetServerList();
            session.Send(new ServerListAckMessage(servers));
            return true;
        }
    }
}
