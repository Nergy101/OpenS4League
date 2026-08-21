using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using OpenS4L.Blub.Collections.Generic;
using OpenS4L.Network.Data.Chat;
using OpenS4L.Network.Message.Chat;
using OpenS4L.Server.Chat.Mappers;

namespace OpenS4L.Server.Chat
{
    public class Channel
    {
        private readonly PlayerManager _playerManager;
        private readonly ChatMapper _mapper;
        private readonly IDictionary<ulong, Player> _players = new ConcurrentDictionary<ulong, Player>();

        public uint Id { get; }
        public IReadOnlyDictionary<ulong, Player> Players => (IReadOnlyDictionary<ulong, Player>)_players;

        public event EventHandler<ChannelEventArgs> PlayerJoined;
        public event EventHandler<ChannelEventArgs> PlayerLeft;

        protected virtual void OnPlayerJoined(Player plr)
        {
            PlayerJoined?.Invoke(this, new ChannelEventArgs(this, plr));
        }

        protected virtual void OnPlayerLeft(Player plr)
        {
            PlayerLeft?.Invoke(this, new ChannelEventArgs(this, plr));
        }

        public Channel(uint id, PlayerManager playerManager, ChatMapper mapper)
        {
            _playerManager = playerManager;
            _mapper = mapper;
            Id = id;
        }

        public void Join(Player plr)
        {
            _players.Add(plr.Account.Id, plr);
            plr.Channel = this;
            Broadcast(new ChannelEnterPlayerAckMessage(_mapper.ToPlayerInfoShortDto(plr)));
            plr.Session.Send(new ChannelPlayerListAckMessage(
                Players.Values.Where(x => x.RoomId == 0).Select(x => _mapper.ToPlayerInfoShortDto(x)).ToArray()
            ));
            _playerManager.Where(x => x.Channel == null).ForEach(x =>
                x.Session.Send(new ChannelLeavePlayerAckMessage(plr.Account.Id))
            );
            OnPlayerJoined(plr);
        }

        public void Leave(Player plr)
        {
            _players.Remove(plr.Account.Id);
            plr.Channel = null;
            plr.SentPlayerList = false;
            Broadcast(new ChannelLeavePlayerAckMessage(plr.Account.Id));
            plr.Session.Send(new ChannelPlayerListAckMessage(
                _playerManager.Where(x => x.Channel == null).Select(x => _mapper.ToPlayerInfoShortDto(x)).ToArray()
            ));
            _playerManager.Where(x => x.Channel == null).ForEach(x =>
                x.Session.Send(new ChannelEnterPlayerAckMessage(_mapper.ToPlayerInfoShortDto(plr)))
            );
            OnPlayerLeft(plr);
        }

        public void Broadcast(IChatMessage message, bool excludeRooms = false)
        {
            foreach (var plr in Players.Values.Where(plr => !excludeRooms || plr.RoomId == 0))
                plr.Session.Send(message);
        }

        public void SendChatMessage(Player sender, string message)
        {
            Broadcast(new MessageChatAckMessage(ChatType.Channel, sender.Account.Id, sender.Account.Nickname, message), true);
        }
    }
}
