using System.Threading.Tasks;
using OpenS4L.Network.Data.Chat;
using OpenS4L.Network.Message.Chat;
using OpenS4L.Server.Chat.Mappers;
using OpenS4L.Server.Chat.Rules;
using ProudNet;

namespace OpenS4L.Server.Chat.Handlers
{
    internal class DenyHandler : IHandle<DenyActionReqMessage>
    {
        private readonly PlayerManager _playerManager;
        private readonly ChatMapper _mapper;

        public DenyHandler(PlayerManager playerManager, ChatMapper mapper)
        {
            _playerManager = playerManager;
            _mapper = mapper;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Inline]
        public async Task<bool> OnHandle(MessageContext context, DenyActionReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;

            if (message.Deny.AccountId == plr.Account.Id)
                return true;

            Deny deny;
            switch (message.Action)
            {
                case DenyAction.Add:
                    if (plr.Ignore.Contains(message.Deny.AccountId))
                        return true;

                    var target = _playerManager[message.Deny.AccountId];
                    if (target == null)
                        return true;

                    deny = plr.Ignore.Add(target.Account.Id, target.Account.Nickname);
                    session.Send(new DenyActionAckMessage(0, DenyAction.Add, _mapper.ToDenyDto(deny)));
                    break;

                case DenyAction.Remove:
                    deny = plr.Ignore[message.Deny.AccountId];
                    if (deny == null)
                        return true;

                    plr.Ignore.Remove(message.Deny.AccountId);
                    session.Send(new DenyActionAckMessage(0, DenyAction.Remove, _mapper.ToDenyDto(deny)));
                    break;
            }

            return true;
        }
    }
}
