using System.Threading.Tasks;
using Foundatio.Messaging;
using OpenS4L.Common.Messaging;
using OpenS4L.Network.Message.P2P;
using ProudNet;

namespace OpenS4L.Server.Relay.Handlers
{
    internal class P2PHandler : IHandle<PlayerSpawnReqMessage>
    {
        private readonly IMessageBus _messageBus;

        public P2PHandler(IMessageBus messageBus)
        {
            _messageBus = messageBus;
        }

        [Inline]
        public async Task<bool> OnHandle(MessageContext context, PlayerSpawnReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;

            await _messageBus.PublishAsync(new PlayerPeerIdMessage(plr.Account.Id, message.Character.Id.PeerId));
            return true;
        }
    }
}
