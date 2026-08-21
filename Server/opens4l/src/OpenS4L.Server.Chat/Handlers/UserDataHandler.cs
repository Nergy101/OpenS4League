using System.Threading.Tasks;
using OpenS4L.Network.Data.Chat;
using OpenS4L.Network.Message.Chat;
using OpenS4L.Server.Chat.Mappers;
using OpenS4L.Server.Chat.Rules;
using ProudNet;

namespace OpenS4L.Server.Chat.Handlers
{
    internal class UserDataHandler : IHandle<UserDataOneReqMessage>
    {
        private readonly PlayerManager _playerManager;
        private readonly ChatMapper _mapper;

        public UserDataHandler(PlayerManager playerManager, ChatMapper mapper)
        {
            _playerManager = playerManager;
            _mapper = mapper;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Inline]
        public async Task<bool> OnHandle(MessageContext context, UserDataOneReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;

            if (plr.Account.Id == message.AccountId)
            {
                session.Send(new UserDataFourAckMessage(25, _mapper.ToUserDataDto(plr)));
                return true;
            }

            var target = _playerManager[message.AccountId];
            if (plr.Channel != target.Channel)
                return true;

            session.Send(new UserDataFourAckMessage(25, _mapper.ToUserDataDto(target)));
            return true;
        }
    }
}
