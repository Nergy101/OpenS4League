using System.Linq;
using System.Threading.Tasks;
using Logging;
using Microsoft.EntityFrameworkCore;
using OpenS4L.Common;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network.Data.Chat;
using OpenS4L.Network.Message.Chat;
using OpenS4L.Server.Chat.Mappers;
using OpenS4L.Server.Chat.Rules;
using ProudNet;

namespace OpenS4L.Server.Chat.Handlers
{
    internal class FriendHandler : IHandle<FriendActionReqMessage>
    {
        private readonly PlayerManager _playerManager;
        private readonly ILogger _logger;
        private readonly DatabaseService _databaseService;
        private readonly IdGeneratorService _idGeneratorService;
        private readonly ChatMapper _mapper;

        public FriendHandler(PlayerManager playerManager, ILogger logger,
            DatabaseService databaseService, IdGeneratorService idGeneratorService, ChatMapper mapper)
        {
            _playerManager = playerManager;
            _logger = logger;
            _databaseService = databaseService;
            _idGeneratorService = idGeneratorService;
            _mapper = mapper;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Inline]
        public async Task<bool> OnHandle(MessageContext context, FriendActionReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;

            _logger.Debug("{@message}", message);

            if (message.AccountId == plr.Account.Id)
                return true;

            switch (message.Action)
            {
                case FriendAction.Add:
                    await ProcessFriendAdd(plr, message);
                    break;

                case FriendAction.AcceptRequest:
                    await ProcessFriendAccept(plr, message);
                    break;

                case FriendAction.DenyRequest:
                case FriendAction.Remove:
                    await ProcessFriendDeny(plr, message);
                    break;
            }

            return true;
        }

        private async Task ProcessFriendAdd(Player plr, FriendActionReqMessage message)
        {
            var session = plr.Session;

            // We already are friends or sent a request
            if (plr.Friends.Contains(message.AccountId))
                return;

            AccountEntity targetAccount;
            using (var db = _databaseService.Open<AuthContext>())
            {
                var targetId = (int)message.AccountId;
                targetAccount = await db.Accounts.FirstOrDefaultAsync(x => x.Id == targetId)
                                ?? await db.Accounts.FirstOrDefaultAsync(x => x.Nickname == message.Nickname);

                if (targetAccount == null || string.IsNullOrWhiteSpace(targetAccount.Nickname))
                {
                    session.Send(new FriendActionAckMessage(FriendActionResult.UserDoesNotExist, message.Action));
                    return;
                }
            }

            // Ignore request if we blocked the target
            if (plr.Ignore.Contains((ulong)targetAccount.Id))
                return;

            var targetPlayer = _playerManager[(ulong)targetAccount.Id];
            var targetSettings = targetPlayer?.Settings;
            var isBlocked = targetPlayer?.Ignore.Contains(plr.Account.Id) ?? false;

            // Lookup settings/ignores when the target player is currently offline
            if (targetPlayer == null)
            {
                using (var db = _databaseService.Open<GameContext>())
                {
                    var requestId = (int)plr.Account.Id;
                    isBlocked = await db.PlayerIgnores.AnyAsync(
                        x => x.PlayerId == targetAccount.Id && x.DenyPlayerId == requestId
                    );

                    var settings = await db.PlayerSettings.Where(x => x.PlayerId == targetAccount.Id).ToListAsync();
                    targetSettings = new PlayerSettingManager(null);
                    targetSettings.Initialize(null, new PlayerEntity
                    {
                        Settings = settings
                    });
                }
            }

            // The target player blocked us
            if (isBlocked)
            {
                session.Send(new FriendActionAckMessage(FriendActionResult.UserDoesNotExist, message.Action));
                return;
            }

            if (targetSettings.Contains(nameof(PlayerSetting.AllowFriendRequest)))
            {
                switch (targetSettings.Get<CommunitySetting>(nameof(PlayerSetting.AllowFriendRequest)))
                {
                    // Target player disabled friend requests
                    case CommunitySetting.FriendOnly:
                    case CommunitySetting.Deny:
                        session.Send(new FriendActionAckMessage(FriendActionResult.UserDoesNotExist, message.Action));
                        return;
                }
            }

            var friend = plr.Friends.Add((ulong)targetAccount.Id, targetAccount.Nickname, FriendState.Requested);
            session.Send(new FriendActionAckMessage(
                FriendActionResult.Success,
                message.Action,
                _mapper.ToFriendDto(friend)
            ));

            if (targetPlayer != null)
            {
                friend = targetPlayer.Friends.Add(plr.Account.Id, plr.Account.Nickname, FriendState.IncomingRequest);
                targetPlayer.Session.Send(new FriendActionAckMessage(
                    FriendActionResult.Success,
                    message.Action,
                    _mapper.ToFriendDto(friend)
                ));
            }
            else
            {
                using (var db = _databaseService.Open<GameContext>())
                {
                    db.PlayerFriends.Add(new PlayerFriendEntity
                    {
                        Id = _idGeneratorService.GetNextId(IdKind.Friend),
                        PlayerId = targetAccount.Id,
                        FriendPlayerId = (int)plr.Account.Id,
                        State = (byte)FriendState.IncomingRequest
                    });
                    await db.SaveChangesAsync();
                }
            }
        }

        private async Task ProcessFriendDeny(Player plr, FriendActionReqMessage message)
        {
            var friend = plr.Friends[message.AccountId];
            if (friend == null)
                return;

            plr.Friends.Remove(friend);

            var requesterPlayer = _playerManager[message.AccountId];
            if (requesterPlayer != null)
            {
                requesterPlayer.Friends.Remove(plr.Account.Id);
            }
            else
            {
                using (var db = _databaseService.Open<GameContext>())
                {
                    var friendId = (int)friend.FriendId;
                    var myId = (int)plr.Account.Id;
                    await db.PlayerFriends.Where(x => x.PlayerId == friendId && x.FriendPlayerId == myId).ExecuteDeleteAsync();
                }
            }
        }

        private async Task ProcessFriendAccept(Player plr, FriendActionReqMessage message)
        {
            var friend = plr.Friends[message.AccountId];
            if (friend == null)
                return;

            friend.State = FriendState.Friends;
            plr.Session.Send(new FriendActionAckMessage(
                FriendActionResult.Success,
                FriendAction.AcceptRequest,
                _mapper.ToFriendDto(friend)
            ));

            var requesterPlayer = _playerManager[message.AccountId];
            if (requesterPlayer != null)
            {
                var requesterFriend = requesterPlayer.Friends[plr.Account.Id];
                if (requesterFriend != null)
                {
                    requesterFriend.State = FriendState.Friends;
                    requesterPlayer.Session.Send(new FriendActionAckMessage(
                        FriendActionResult.Success,
                        FriendAction.AcceptRequest,
                        _mapper.ToFriendDto(requesterFriend)
                    ));
                }
            }
            else
            {
                using (var db = _databaseService.Open<GameContext>())
                {
                    var friendId = (int)friend.FriendId;
                    var myId = (int)plr.Account.Id;
                    var newState = (byte)friend.State;
                    await db.PlayerFriends
                        .Where(x => x.PlayerId == friendId && x.FriendPlayerId == myId)
                        .ExecuteUpdateAsync(x => x.SetProperty(e => e.State, newState));
                }
            }
        }
    }
}
