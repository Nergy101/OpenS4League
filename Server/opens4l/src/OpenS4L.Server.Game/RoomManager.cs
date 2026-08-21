using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using OpenS4L.Blub.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using OpenS4L.Common;
using OpenS4L.Network.Data.Game;
using OpenS4L.Network.Message.Game;
using OpenS4L.Server.Game.Mappers;
using OpenS4L.Server.Game.Services;

namespace OpenS4L.Server.Game
{
    public class RoomManager : IReadOnlyCollection<Room>
    {
        private static readonly EventPipeline<RoomCreateHookEventArgs>
            s_preCreateRoomEvent = new EventPipeline<RoomCreateHookEventArgs>();

        public static event EventPipeline<RoomCreateHookEventArgs>.SubscriberDelegate RoomCreateHook
        {
            add => s_preCreateRoomEvent.Subscribe(value);
            remove => s_preCreateRoomEvent.Unsubscribe(value);
        }
        public static event EventHandler<RoomEventArgs> RoomCreated;
        public static event EventHandler<RoomEventArgs> RoomRemoved;

        private static void OnRoomCreated(RoomManager roomManager, Room room)
        {
            RoomCreated?.Invoke(roomManager, new RoomEventArgs(room));
        }

        private static void OnRoomRemoved(RoomManager roomManager, Room room)
        {
            RoomRemoved?.Invoke(roomManager, new RoomEventArgs(room));
        }

        private readonly IServiceProvider _serviceProvider;
        private readonly GameDataService _gameDataService;
        private readonly GameRuleResolver _gameRuleResolver;
        private readonly GameMapper _mapper;
        private readonly ConcurrentDictionary<uint, Room> _rooms;
        private readonly CounterRecycler _idRecycler;

        public Channel Channel { get; private set; }
        public Room this[uint id] => Get(id);

        public RoomManager(IServiceProvider serviceProvider, GameDataService gameDataService, GameRuleResolver gameRuleResolver,
            Mappers.GameMapper mapper)
        {
            _serviceProvider = serviceProvider;
            _gameDataService = gameDataService;
            _gameRuleResolver = gameRuleResolver;
            _mapper = mapper;
            _rooms = new ConcurrentDictionary<uint, Room>();
            _idRecycler = new CounterRecycler();
        }

        internal void Initialize(Channel channel)
        {
            Channel = channel;
        }

        public Room Get(uint id)
        {
            _rooms.TryGetValue(id, out var room);
            return room;
        }

        public (Room, RoomCreateError) Create(RoomCreationOptions options)
        {
            var eventArgs = new RoomCreateHookEventArgs(this, options);
            s_preCreateRoomEvent.Invoke(eventArgs);
            if (eventArgs.Error != RoomCreateError.OK)
                return (null, eventArgs.Error);

            if (!_gameRuleResolver.HasGameRule(options))
                return (null, RoomCreateError.InvalidGameRule);

            var map = _gameDataService.Maps.FirstOrDefault(x => x.Id == options.Map);
            if (map == null)
                return (null, RoomCreateError.InvalidMap);

            if (map.GameRule != options.GameRule)
                return (null, RoomCreateError.InvalidGameRule);

            var room = _serviceProvider.GetRequiredService<Room>();
            room.Initialize(this, _idRecycler.GetId(), options);
            _rooms.TryAdd(room.Id, room);
            Channel.Broadcast(new RoomDeployAck2Message(_mapper.ToRoom2Dto(room)));
            OnRoomCreated(this, room);
            return (room, RoomCreateError.OK);
        }

        public bool Remove(Room room)
        {
            if (room.Players.Count > 0)
                return false;

            _rooms.Remove(room.Id);
            _idRecycler.Return(room.Id);
            Channel.Broadcast(new RoomDisposeAckMessage(room.Id));
            OnRoomRemoved(this, room);
            return true;
        }

        #region IReadOnlyCollection
        public int Count => _rooms.Count;

        public IEnumerator<Room> GetEnumerator()
        {
            return _rooms.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion
    }

    public class RoomCreationOptions
    {
        public string Name { get; set; }
        public bool IsSpectatingEnabled => SpectatorLimit > 0;
        public GameRule GameRule { get; set; }
        public byte Map { get; set; }
        public int PlayerLimit { get; set; }
        public int SpectatorLimit { get; set; }
        public TimeSpan TimeLimit { get; set; }
        public ushort ScoreLimit { get; set; }
        public string Password { get; set; }
        public int EquipLimit { get; set; }
        public IPEndPoint RelayEndPoint { get; set; }
        public bool IsFriendly { get; set; }
    }
}
