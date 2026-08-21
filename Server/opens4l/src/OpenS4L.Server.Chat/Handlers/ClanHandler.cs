using System;
using System.Linq;
using System.Threading.Tasks;
using Foundatio.Messaging;
using OpenS4L.Common;
using OpenS4L.Common.Messaging;
using OpenS4L.Network.Data.Chat;
using OpenS4L.Network.Message.Chat;
using OpenS4L.Server.Chat.Mappers;
using OpenS4L.Server.Chat.Rules;
using ProudNet;

namespace OpenS4L.Server.Chat.Handlers
{
    internal class ClanHandler : IHandle<ClubMemberListReqMessage>
    {
        private readonly IMessageBus _messageBus;
        private readonly ChatMapper _mapper;

        public ClanHandler(IMessageBus messageBus, ChatMapper mapper)
        {
            _messageBus = messageBus;
            _mapper = mapper;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        public async Task<bool> OnHandle(MessageContext context, ClubMemberListReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;

            var response = await _messageBus.PublishRequestAsync<ClanMemberListRequest, ClanMemberListResponse>(
                new ClanMemberListRequest(message.ClanId)
            );

            session.Send(new ClubMemberListAckMessage(
                response.Members.Select(x => _mapper.ToClubMemberDto(x)).ToArray()
            ));
            return true;
        }
    }
}
