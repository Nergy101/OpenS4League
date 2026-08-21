using System;
using System.Reflection;
using OpenS4L;
using OpenS4L.Common;
using OpenS4L.Database.Game;
using OpenS4L.Network.Data.Chat;
using OpenS4L.Server.Chat;
using OpenS4L.Server.Chat.Mappers;

namespace OpenS4L.Server.Mapping.Tests
{
    /// <summary>
    /// Constructs real Chat domain objects (Mail, Deny, Friend, Player, ClanMemberInfo)
    /// for the differential tests. The Player's managers are never dereferenced by the
    /// mappers, so they're left null; Account (private set) is injected via reflection.
    /// </summary>
    internal static class ChatFixtures
    {
        public static Mail CreateMail(long id = 1, bool isNew = false, long sentDateUnix = 1700000000)
        {
            return new Mail(new PlayerMailEntity
            {
                Id = id,
                SenderPlayerId = 99,
                SentDate = sentDateUnix,
                Title = "Hello",
                Message = "Body",
                IsMailNew = isNew
            }, "senderNick");
        }

        public static Deny CreateDeny() => new(5, 5001, "DenyNick");

        public static Friend CreateFriend() => new(7, 7001, "FriendNick", FriendState.Friends);

        public static Player CreatePlayer(ushort serverGroupId)
        {
            var player = new Player(null, null, null, null, null);

            // Account is a private-set property; inject via reflection (mappers read it).
            var accountField = typeof(Player).GetProperty(
                nameof(Player.Account),
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            accountField!.SetValue(player, new Account(9001, "usr", "nick", SecurityLevel.User));

            player.TotalExperience = 1000;
            player.Level = 42;
            player.Channel = null;
            player.RoomId = 0;
            player.ClanId = 0;
            return player;
        }

        public static OpenS4L.Common.Messaging.ClanMemberInfo CreateClanMemberInfo()
        {
            return new OpenS4L.Common.Messaging.ClanMemberInfo
            {
                AccountId = 8001,
                Nickname = "clanmember",
                Role = ClubRole.Normal,
                LastLoginDate = DateTimeOffset.FromUnixTimeSeconds(1700000000),
                PresenceState = ClubMemberPresenceState.Online
            };
        }

        public static ChatMapper CreateMapper() => new(3);
    }
}
