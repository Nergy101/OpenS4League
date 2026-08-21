using System;
using System.Linq;
using System.Threading.Tasks;
using Logging;
using OpenS4L.Network;
using OpenS4L.Network.Data.Club;
using OpenS4L.Network.Message.Club;
using OpenS4L.Server.Game.Mappers;
using OpenS4L.Server.Game.Rules;
using OpenS4L.Server.Game.Services;
using ProudNet;

namespace OpenS4L.Server.Game.Handlers
{
    internal class ClanHandler
        : IHandle<ClubSearchReqMessage>, IHandle<ClubInfoReqMessage>, IHandle<ClubNameCheckReqMessage>,
          IHandle<ClubCreateReqMessage>, IHandle<ClubCloseReqMessage>, IHandle<ClubJoinConditionInfoReqMessage>,
          IHandle<ClubJoinReqMessage>, IHandle<ClubUnjoinReqMessage>, IHandle<ClubJoinWaiterInfoReqMessage>,
          IHandle<ClubAdminJoinCommandReqMessage>, IHandle<ClubNewJoinMemberInfoReqMessage>,
          IHandle<ClubUnjoinerListReqMessage>, IHandle<ClubAdminNoticeChangeReqMessage>,
          IHandle<ClubAdminInfoModifyReqMessage>, IHandle<ClubAdminJoinConditionModifyReqMessage>,
          IHandle<ClubAdminGradeChangeReqMessage>
    {
        private readonly ILogger _logger;
        private readonly ClanManager _clanManager;
        private readonly NicknameLookupService _nicknameLookupService;
        private readonly GameMapper _mapper;

        public ClanHandler(ILogger<ClanHandler> logger, ClanManager clanManager, NicknameLookupService nicknameLookupService,
            GameMapper mapper)
        {
            _logger = logger;
            _clanManager = clanManager;
            _nicknameLookupService = nicknameLookupService;
            _mapper = mapper;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        public async Task<bool> OnHandle(MessageContext context, ClubInfoReqMessage message)
        {
            var session = context.GetSession<Session>();
            var clan = _clanManager[message.ClubId];
            session.Send(await clan.GetClubInfo());
            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        public async Task<bool> OnHandle(MessageContext context, ClubSearchReqMessage message)
        {
            var session = context.GetSession<Session>();

            // TODO Better queries, pages and sorting

            var result = _clanManager
                .Where(x => x.Name.Contains(message.Query, StringComparison.OrdinalIgnoreCase))
                .Select(x => _mapper.ToClubSearchResultDto(x))
                .ToArray();

            session.Send(new ClubSearchAckMessage(result));
            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Firewall(typeof(MustBeInClan), Invert = true)]
        public async Task<bool> OnHandle(MessageContext context, ClubNameCheckReqMessage message)
        {
            context.Session.Send(new ClubNameCheckAckMessage(
                _clanManager.CheckClanName(message.Name)
            ));
            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Firewall(typeof(MustBeInClan), Invert = true)]
        public async Task<bool> OnHandle(MessageContext context, ClubCreateReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;

            if (_clanManager.CheckClanName(message.Name) != ClubNameCheckResult.Available)
                session.Send(new ClubCreateAckMessage(ClubCreateResult.Failed));

            var (_, result) = await _clanManager.CreateClan(
                plr,
                message.Name, message.Description,
                message.Area, message.Activity,
                message.Question1, message.Question2, message.Question3, message.Question4, message.Question5
            );

            session.Send(result == ClanCreateError.None
                ? new ClubCreateAckMessage(ClubCreateResult.Success)
                : new ClubCreateAckMessage(ClubCreateResult.Failed));

            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Firewall(typeof(MustBeInClan))]
        public async Task<bool> OnHandle(MessageContext context, ClubCloseReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;

            if (plr.ClanMember.Role != ClubRole.Master)
            {
                session.Send(new ClubCloseAckMessage(ClubCloseResult.MasterRequired));
                return true;
            }

            if (plr.Clan.Members.Count() > 1)
            {
                session.Send(new ClubCloseAckMessage(ClubCloseResult.ClanNotEmpty));
                return true;
            }

            await plr.Clan.Close();
            session.Send(new ClubCloseAckMessage(ClubCloseResult.Success));
            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        public async Task<bool> OnHandle(MessageContext context, ClubJoinConditionInfoReqMessage message)
        {
            var session = context.GetSession<Session>();
            var clan = _clanManager[message.ClubId];

            if (clan == null)
            {
                session.Send(new Network.Message.Game.ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return true;
            }

            session.Send(new ClubJoinConditionInfoAckMessage
            {
                JoinType = clan.IsPublic ? 2 : 1,
                RequiredLevel = clan.RequiredLevel,
                Question1 = clan.Question1,
                Question2 = clan.Question2,
                Question3 = clan.Question3,
                Question4 = clan.Question4,
                Question5 = clan.Question5
            });
            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        public async Task<bool> OnHandle(MessageContext context, ClubJoinReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;
            var clan = _clanManager[message.ClubId];

            if (clan == null)
            {
                session.Send(new ClubJoinAckMessage(ClubJoinResult.Failed));
                return true;
            }

            var result = await clan.Join(
                plr,
                message.Answer1,
                message.Answer2,
                message.Answer3,
                message.Answer4,
                message.Answer5
            );
            session.Send(new ClubJoinAckMessage(result));
            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Firewall(typeof(MustBeInClan))]
        public async Task<bool> OnHandle(MessageContext context, ClubUnjoinReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;

            var result = await plr.Clan.Leave(plr);
            session.Send(new ClubUnjoinAckMessage(result ? ClubLeaveResult.Success : ClubLeaveResult.Failed));
            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Firewall(typeof(MustBeInClan))]
        public async Task<bool> OnHandle(MessageContext context, ClubJoinWaiterInfoReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;
            var clan = _clanManager[message.ClubId];

            if (plr.ClanMember.Role > ClubRole.Staff)
            {
                session.Send(new Network.Message.Game.ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return true;
            }

            session.Send(new ClubJoinWaiterInfoAckMessage
            {
                Waiters = clan
                    .Where(x => x.State == ClubMemberState.JoinRequested)
                    .Select(x => _mapper.ToJoinWaiterInfoDto(x))
                    .ToArray()
            });
            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Firewall(typeof(MustBeInClan))]
        public async Task<bool> OnHandle(MessageContext context, ClubAdminJoinCommandReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;
            var clan = plr.Clan;

            if (plr.ClanMember.Role > ClubRole.Staff)
            {
                session.Send(new ClubAdminJoinCommandAckMessage(ClubCommandResult.PermissionDenied));
                return true;
            }

            if (message.AccountIds.Length > 1)
            {
                session.Send(new ClubAdminJoinCommandAckMessage(ClubCommandResult.MemberNotFound));
                return true;
            }

            ClubCommandResult result;
            switch (message.Command)
            {
                case ClubCommand.Accept:
                    result = await clan.Approve(plr, message.AccountIds[0]);
                    plr.SendClanJoinEvents();
                    break;

                case ClubCommand.Decline:
                    result = await clan.Decline(plr, message.AccountIds[0]);
                    break;

                case ClubCommand.Kick:
                {
                    var targetMember = clan.GetMember(message.AccountIds[0]);
                    if (targetMember.Role <= plr.ClanMember.Role)
                        result = ClubCommandResult.PermissionDenied;
                    else
                        result = await clan.Kick(plr, message.AccountIds[0]);

                    plr.SendClanLeaveEvents();
                    break;
                }

                case ClubCommand.Ban:
                {
                    var targetMember = clan.GetMember(message.AccountIds[0]);
                    if (targetMember.Role <= plr.ClanMember.Role)
                        result = ClubCommandResult.PermissionDenied;
                    else
                        result = await clan.Ban(plr, message.AccountIds[0]);

                    plr.SendClanLeaveEvents();
                    break;
                }

                case ClubCommand.Unban:
                    result = await clan.Unban(plr, message.AccountIds[0]);
                    plr.SendClanLeaveEvents();
                    break;

                default:
                    plr.AddContextToLogger(_logger).Warning("Unknown join command={command}", message.Command);
                    result = ClubCommandResult.MemberNotFound;
                    break;
            }

            session.Send(new ClubAdminJoinCommandAckMessage(result));
            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Firewall(typeof(MustBeInClan))]
        public async Task<bool> OnHandle(MessageContext context, ClubNewJoinMemberInfoReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;
            var clan = plr.Clan;

            if (plr.ClanMember.Role > ClubRole.Staff)
            {
                session.Send(new Network.Message.Game.ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return true;
            }

            plr.SendClanJoinEvents();
            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Firewall(typeof(MustBeInClan))]
        public async Task<bool> OnHandle(MessageContext context, ClubUnjoinerListReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;

            if (plr.ClanMember.Role > ClubRole.Staff)
            {
                session.Send(new Network.Message.Game.ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return true;
            }

            plr.SendClanLeaveEvents();
            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Firewall(typeof(MustBeInClan))]
        public async Task<bool> OnHandle(MessageContext context, ClubAdminNoticeChangeReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;
            var clan = plr.Clan;

            if (plr.ClanMember.Role != ClubRole.Master)
            {
                session.Send(new ClubAdminNoticeChangeAckMessage(ClubNoticeChangeResult.NoMatchFound));
                return true;
            }

            await clan.ChangeAnnouncement(message.Notice);
            session.Send(new ClubAdminNoticeChangeAckMessage(ClubNoticeChangeResult.Success));
            await clan.Broadcast(await clan.GetClubInfo());
            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Firewall(typeof(MustBeInClan))]
        public async Task<bool> OnHandle(MessageContext context, ClubAdminInfoModifyReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;
            var clan = plr.Clan;

            if (plr.ClanMember.Role != ClubRole.Master)
            {
                session.Send(new ClubAdminInfoModifyAckMessage(ClubAdminInfoModifyResult.NoMatchFound));
                return true;
            }

            await clan.ChangeInfo(message.Area, message.Activity, message.Description);
            session.Send(new ClubAdminInfoModifyAckMessage(ClubAdminInfoModifyResult.Success));
            await clan.Broadcast(await clan.GetClubInfo());
            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Firewall(typeof(MustBeInClan))]
        public async Task<bool> OnHandle(MessageContext context, ClubAdminJoinConditionModifyReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;
            var clan = plr.Clan;

            if (plr.ClanMember.Role != ClubRole.Master)
            {
                session.Send(new ClubAdminJoinConditionModifyAckMessage(ClubAdminJoinConditionModifyResult.NoMatchFound));
                return true;
            }

            await clan.ChangeJoinCondition(
                message.JoinType == 2,
                (byte)message.RequiredLevel,
                message.Question1,
                message.Question2,
                message.Question3,
                message.Question4,
                message.Question5
            );
            session.Send(new ClubAdminJoinConditionModifyAckMessage(ClubAdminJoinConditionModifyResult.Success));
            return true;
        }

        [Firewall(typeof(MustBeLoggedIn))]
        [Firewall(typeof(MustBeInClan))]
        public async Task<bool> OnHandle(MessageContext context, ClubAdminGradeChangeReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;
            var clan = plr.Clan;

            if (plr.ClanMember.Role != ClubRole.Master)
            {
                session.Send(new ClubAdminGradeChangeAckMessage(ClubAdminChangeRoleResult.PermissionDenied));
                return true;
            }

            foreach (var roleChange in message.Grades)
            {
                var member = clan.GetMember(roleChange.AccountId);
                if (member == null)
                {
                    session.Send(new ClubAdminGradeChangeAckMessage(ClubAdminChangeRoleResult.MemberNotFound));
                    return true;
                }

                if (member == plr.ClanMember ||
                    member.Role == ClubRole.Master ||
                    roleChange.Role == ClubRole.Master ||
                    roleChange.Role < ClubRole.Master || roleChange.Role > ClubRole.BadManner)
                {
                    session.Send(new ClubAdminGradeChangeAckMessage(ClubAdminChangeRoleResult.CantChangeRank));
                    return true;
                }

                if (member.Role <= plr.ClanMember.Role)
                {
                    session.Send(new ClubAdminGradeChangeAckMessage(ClubAdminChangeRoleResult.PermissionDenied));
                    return true;
                }

                await member.ChangeRole(roleChange.Role);
            }

            session.Send(new ClubAdminGradeChangeAckMessage(
                ClubAdminChangeRoleResult.Success,
                message.Grades.Select(x => x.AccountId).ToArray()
            ));
            return true;
        }
    }
}
