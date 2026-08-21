using System.Linq;
using System.Threading.Tasks;
using Logging;
using OpenS4L.Network;
using OpenS4L.Network.Data.Game;
using OpenS4L.Network.Message.Game;
using OpenS4L.Server.Game.Mappers;
using OpenS4L.Server.Game.Rules;
using OpenS4L.Server.Game.Services;
using ProudNet;

namespace OpenS4L.Server.Game.Handlers
{
    internal class ChannelHandler
        : IHandle<ChannelInfoReqMessage>, IHandle<ChannelEnterReqMessage>, IHandle<ChannelLeaveReqMessage>
    {
        private readonly ILogger _logger;
        private readonly ChannelService _channelService;
        private readonly GameMapper _mapper;

        public ChannelHandler(ILogger<ChannelHandler> logger, ChannelService channelService, GameMapper mapper)
        {
            _logger = logger;
            _channelService = channelService;
            _mapper = mapper;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        public async Task<bool> OnHandle(MessageContext context, ChannelInfoReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;

            switch (message.Request)
            {
                case ChannelInfoRequest.RoomList:
                case ChannelInfoRequest.RoomList2:
                    if (plr.Channel != null)
                    {
                        var rooms = plr.Channel.RoomManager.Select(x => _mapper.ToRoom2Dto(x)).ToArray();
                        plr.AddContextToLogger(_logger).Information("Sending RoomListInfoAck2 {Count} rooms", rooms.Length);
                        session.Send(new RoomListInfoAck2Message(rooms));
                    }

                    break;

                case ChannelInfoRequest.ChannelList:
                    if (plr.Channel == null)
                    {
                        var channels = _channelService.Select(x => _mapper.ToChannelInfoDto(x)).ToArray();
                        plr.AddContextToLogger(_logger).Information("Sending ChannelListInfoAck {Count} channels", channels.Length);
                        session.Send(new ChannelListInfoAckMessage(channels));
                    }

                    break;

                default:
                    plr.AddContextToLogger(_logger).Warning("Invalid channel info request {Request}", message.Request);

                    break;
            }

            return true;
        }

        [Firewall(typeof(MustBeInChannel), Invert = true)]
        public async Task<bool> OnHandle(MessageContext context, ChannelEnterReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;

            var channel = _channelService[message.Channel];
            if (channel == null)
            {
                session.Send(new ServerResultAckMessage(ServerResult.NonExistingChannel));
                return true;
            }

            var result = channel.Join(plr);
            switch (result)
            {
                case ChannelJoinError.OK:
                    plr.Session.Send(new ServerResultAckMessage(ServerResult.ChannelEnter));
                    break;

                case ChannelJoinError.AlreadyInChannel:
                    plr.Session.Send(new ServerResultAckMessage(ServerResult.JoinChannelFailed));
                    break;

                case ChannelJoinError.ChannelFull:
                    plr.Session.Send(new ServerResultAckMessage(ServerResult.ChannelLimitReached));
                    break;
            }

            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Firewall(typeof(MustBeInRoom), Invert = true)]
        public async Task<bool> OnHandle(MessageContext context, ChannelLeaveReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;

            plr.Channel?.Leave(plr);
            plr.Session.Send(new ServerResultAckMessage(ServerResult.ChannelLeave));
            return true;
        }
    }
}
