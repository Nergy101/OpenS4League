using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Foundatio.Caching;
using Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenS4L.Common;
using OpenS4L.Common.Configuration;
using OpenS4L.Database;
using OpenS4L.Database.Auth;
using OpenS4L.Database.Game;
using OpenS4L.Network;
using OpenS4L.Network.Data.Game;
using OpenS4L.Network.Message.Game;
using OpenS4L.Server.Game.Rules;
using OpenS4L.Server.Game.Services;
using ProudNet;
using Constants = OpenS4L.Common.Constants;

namespace OpenS4L.Server.Game.Handlers
{
    internal class AuthenticationHandler
        : IHandle<LoginRequestReqMessage>,
          IHandle<CharacterFirstCreateReqMessage>
    {
        private readonly ILogger _logger;
        private readonly NetworkOptions _networkOptions;
        private readonly IOptionsMonitor<AppOptions> _appOptions;
        private readonly GameOptions _gameOptions;
        private readonly ICacheClient _cacheClient;
        private readonly ISessionManager _sessionManager;
        private readonly DatabaseService _databaseService;
        private readonly IServiceProvider _serviceProvider;
        private readonly PlayerManager _playerManager;
        private readonly GameDataService _gameDataService;

        public AuthenticationHandler(ILogger<AuthenticationHandler> logger,
            IOptions<NetworkOptions> networkOptions, IOptionsMonitor<AppOptions> appOptions, IOptions<GameOptions> gameOptions,
            ICacheClient cacheClient, ISessionManager sessionManager, DatabaseService databaseService,
            IServiceProvider serviceProvider, PlayerManager playerManager, GameDataService gameDataService)
        {
            _logger = logger;
            _networkOptions = networkOptions.Value;
            _appOptions = appOptions;
            _gameOptions = gameOptions.Value;
            _cacheClient = cacheClient;
            _sessionManager = sessionManager;
            _databaseService = databaseService;
            _serviceProvider = serviceProvider;
            _playerManager = playerManager;
            _gameDataService = gameDataService;
        }

        [Firewall(typeof(MustBeLoggedIn), Invert = true)]
        [Inline]
        public async Task<bool> OnHandle(MessageContext context, LoginRequestReqMessage message)
        {
            var session = context.GetSession<Session>();
            var logger = _logger
                .ForContext("RemoteEndPoint", session.RemoteEndPoint.ToString())
                .ForContext("ClientMessage", message, true);

            logger.Debug("Login");

            var allowedVersions = _appOptions.CurrentValue.ClientVersions;
            if (allowedVersions.All(x => message.Version != x))
            {
                logger.Information("Invalid client version={Version} supported versions are {SupportedVersions}",
                    message.Version.ToString(), string.Join(",", allowedVersions.Select(x => x.ToString())));
                session.Send(new LoginReguestAckMessage(GameLoginResult.WrongVersion));
                await session.CloseAsync();
                return true;
            }

            if (_sessionManager.Sessions.Count >= _networkOptions.MaxSessions)
            {
                session.Send(new LoginReguestAckMessage(GameLoginResult.ServerFull));
                return true;
            }

            // Validate session
            var sessionId = await _cacheClient.GetAsync<string>(Constants.Cache.SessionKey(message.AccountId));
            if (!sessionId.HasValue || !sessionId.Value.Equals(message.SessionId))
            {
                logger.Information("Invalid session id");
                session.Send(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                return true;
            }

            AccountEntity accountEntity;
            using (var db = _databaseService.Open<AuthContext>())
            {
                var accountId = (long)message.AccountId;
                accountEntity = await db.Accounts
                    .Include(x => x.Bans)
                    .FirstOrDefaultAsync(x => x.Id == accountId);
            }

            if (accountEntity == null)
            {
                logger.Information("Wrong login");
                session.Send(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                return true;
            }

            // Check ban status
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var ban = accountEntity.Bans.FirstOrDefault(x => x.Duration == null || x.Date + x.Duration > now);
            if (ban != null)
            {
                var unbanDate = DateTimeOffset.MinValue;
                if (ban.Duration != null)
                    unbanDate = DateTimeOffset.FromUnixTimeSeconds(ban.Date + (ban.Duration ?? 0));

                logger.Information("Account is banned until {UnbanDate}", unbanDate);
                session.Send(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                return true;
            }

            var account = new Account((ulong)accountEntity.Id, accountEntity.Username, accountEntity.Nickname,
                (SecurityLevel)accountEntity.SecurityLevel);

            if (message.KickConnection)
            {
                var oldPlr = _playerManager[account.Id];
                if (oldPlr != null)
                {
                    logger.Information("Kicking old connection hostId={HostId}", oldPlr.Session.HostId);
                    await oldPlr.DisconnectAsync();
                }
            }

            if (_playerManager.Contains(account.Id))
            {
                // TODO Check if logged in on another server

                logger.Information("Account is already logged in");
                session.Send(new LoginReguestAckMessage(GameLoginResult.TerminateOtherConnection));
                return true;
            }

            using (var db = _databaseService.Open<GameContext>())
            {
                var plr = await db.Players
                    .Include(x => x.Characters)
                    .Include(x => x.Items)
                    .Include(x => x.ClanMember)
                    .FirstOrDefaultAsync(x => x.Id == accountEntity.Id);

                if (plr == null)
                {
                    var levelInfo = _gameDataService.Levels.GetValueOrDefault(_gameOptions.StartLevel);
                    if (levelInfo == null)
                        logger.Warning("Invalid StartLevel={StartLevel} in config", _gameOptions.StartLevel);

                    plr = new PlayerEntity
                    {
                        Id = (int)account.Id,
                        AP = _gameOptions.StartAP,
                        PEN = _gameOptions.StartPEN,
                        Coins1 = _gameOptions.StartCoins1,
                        Coins2 = _gameOptions.StartCoins2,
                        TotalExperience = (int)(levelInfo?.TotalExperience ?? 0)
                    };

                    db.Players.Add(plr);
                    await db.SaveChangesAsync();
                }

                session.Player = _serviceProvider.GetRequiredService<Player>();
                session.Player.Initialize(session, account, plr);
                session.SessionId = message.SessionId;
            }

            _playerManager.Add(session.Player);
            logger.Information("Login success");

            var result = string.IsNullOrWhiteSpace(account.Nickname)
                ? GameLoginResult.ChooseNickname
                : GameLoginResult.OK;
            session.Send(new LoginReguestAckMessage(result, account.Id));

            if (!string.IsNullOrWhiteSpace(account.Nickname))
                await session.Player.SendAccountInformation();

            return true;
        }

        [Inline]
        public async Task<bool> OnHandle(MessageContext context, CharacterFirstCreateReqMessage message)
        {
            var session = context.GetSession<Session>();
            var plr = session.Player;

            if (plr == null)
                return true;

            var logger = plr.AddContextToLogger(_logger);

            if (plr.CharacterManager.Count > 0 && !string.IsNullOrWhiteSpace(plr.Account.Nickname))
                return true;

            logger.Information("Creating first character {@Message}", message.ToJson());

            var items = new List<PlayerItem>();
            Character character = null;
            if (plr.CharacterManager.Count == 0)
            {
                var (created, result) = plr.CharacterManager.Create(
                    0, // Slot
                    message.Style.Gender,
                    0, 0, 0, 0, 0, 0
                );
                character = created;

                if (result != CharacterCreateResult.Success)
                {
                    logger.Information("Failed to create first character result={Result}", result);
                    session.Send(new ServerResultAckMessage(ServerResult.CreateCharacterFailed));
                    return true;
                }

                IEnumerable<StartItemEntity> startItems;
                using (var db = _databaseService.Open<GameContext>())
                {
                    var securityLevel = (byte)plr.Account.SecurityLevel;
                    startItems = await db.StartItems.Where(x => x.RequiredSecurityLevel <= securityLevel).ToArrayAsync();
                }

                foreach (var startItem in startItems)
                {
                    var item = _gameDataService.ShopItems.Values.FirstOrDefault(group =>
                        group.GetItemInfo(startItem.ShopItemInfoId) != null);
                    var itemInfo = item?.GetItemInfo(startItem.ShopItemInfoId);

                    if (itemInfo == null)
                    {
                        _logger.Warning("Cant find ShopItemInfo for Start item {startItemId} - Forgot to reload the cache?",
                            startItem.Id);
                        continue;
                    }

                    var price = itemInfo.PriceGroup.GetPrice(startItem.ShopPriceId);
                    if (price == null)
                    {
                        _logger.Warning("Cant find ShopPrice for Start item {startItemId} - Forgot to reload the cache?",
                            startItem.Id);
                        continue;
                    }

                    var color = startItem.Color;
                    if (color > item.ColorGroup)
                    {
                        _logger.Warning("Start item {startItemId} has an invalid color {color}", startItem.Id, color);
                        color = 0;
                    }

                    // Only create items the player chose
                    if (message.Items.Contains(item.ItemNumber))
                    {
                        // Check if gender is correct
                        if (item.Gender == Gender.Male && message.Style.Gender != CharacterGender.Male ||
                            item.Gender == Gender.Female && message.Style.Gender != CharacterGender.Female)
                        {
                            continue;
                        }

                        var playerItem = plr.Inventory.Create(
                            itemInfo,
                            price,
                            color,
                            itemInfo.EffectGroup.Effects.Select(x => x.Effect).ToArray(),
                            false
                        );
                        items.Add(playerItem);
                    }
                }

                // The client does not send equip requests after first character creation, so the
                // granted starter items must be equipped here or the character preview stays empty
                // and the client refuses to enter a channel ("acquire the weapon and skill license").
                // Equip each granted item to its correct slot (costume sub-category maps to the
                // costume slot; weapons to Weapon1).
                foreach (var playerItem in items)
                {
                    switch (playerItem.ItemNumber.Category)
                    {
                        case ItemCategory.Costume:
                            character.Equip(playerItem, playerItem.ItemNumber.SubCategory);
                            break;

                        case ItemCategory.Weapon:
                            character.Equip(playerItem, (byte)WeaponSlot.Weapon1);
                            break;

                        case ItemCategory.Skill:
                            character.Equip(playerItem, (byte)SkillSlot.Skill);
                            break;
                    }
                }

                // Ensure the character has a weapon and a skill equipped (the client gates channel
                // entry on it). The reference stack granted the dagger + wings as defaults; if the
                // player's chosen outfit didn't include any, grant+equip the stock dagger (2000006)
                // and a basic skill (3050001 shield) so the character is channel-ready.
                if (character.Weapons.GetItems().All(x => x == null))
                {
                    var dagger = plr.Inventory.Create((ItemNumber)2000006, ItemPriceType.PEN,
                        ItemPeriodType.None, 0, 0, Array.Empty<uint>(), 1, false);
                    character.Equip(dagger, (byte)WeaponSlot.Weapon1);
                    items.Add(dagger);
                }

                if (character.Skills.GetItems().All(x => x == null))
                {
                    var skill = plr.Inventory.Create((ItemNumber)3050001, ItemPriceType.PEN,
                        ItemPeriodType.None, 0, 0, Array.Empty<uint>(), 1, false);
                    character.Equip(skill, (byte)SkillSlot.Skill);
                    items.Add(skill);
                }
            }

            if (string.IsNullOrWhiteSpace(plr.Account.Nickname))
            {
                var available = await IsNickAvailableAsync(message.Nickname);
                if (!available)
                {
                    logger.Debug("Nickname not available");
                    session.Send(new NickCheckAckMessage(true));
                    return true;
                }

                plr.Account.Nickname = message.Nickname;
                using (var db = _databaseService.Open<AuthContext>())
                {
                    var accountId = (long)plr.Account.Id;
                    await db.Accounts
                        .Where(x => x.Id == accountId)
                        .ExecuteUpdateAsync(x => x.SetProperty(e => e.Nickname, message.Nickname));
                }

                plr.OnNicknameCreated(message.Nickname);
            }

            if (items.Count > 0)
            {
                session.Send(new RequitalGiveItemResultAckMessage(
                    items.Select(x => new RequitalGiveItemResultDto(x.ItemNumber, 0)).ToArray()
                ));
            }

            await plr.SendAccountInformation();
            return true;
        }

        private async Task<bool> IsNickAvailableAsync(string nickname)
        {
            var minLength = _gameOptions.NickRestrictions.MinLength;
            var maxLength = _gameOptions.NickRestrictions.MaxLength;
            var whitespace = _gameOptions.NickRestrictions.WhitespaceAllowed;
            var ascii = _gameOptions.NickRestrictions.AsciiOnly;
            if (string.IsNullOrWhiteSpace(nickname) ||
                !whitespace && nickname.Contains(" ") ||
                nickname.Length < minLength ||
                nickname.Length > maxLength ||
                ascii && Encoding.UTF8.GetByteCount(nickname) != nickname.Length)
            {
                return false;
            }

            // check for repeating chars example: (AAAHello, HeLLLLo)
            var maxRepeat = _gameOptions.NickRestrictions.MaxRepeat;
            if (maxRepeat > 0)
            {
                var counter = 1;
                var current = nickname[0];
                for (var i = 1; i < nickname.Length; i++)
                {
                    if (current != nickname[i])
                    {
                        if (counter > maxRepeat)
                            return false;

                        counter = 0;
                        current = nickname[i];
                    }

                    ++counter;
                }

                if (counter > maxRepeat)
                    return false;
            }

            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            using (var db = _databaseService.Open<AuthContext>())
            {
                var nickExists = await db.Accounts.AnyAsync(x => x.Nickname == nickname);
                var nickReserved = await db.Nicknames.AnyAsync(x =>
                    x.Nickname == nickname && (x.ExpireDate == -1 || x.ExpireDate > now));
                return !nickExists && !nickReserved;
            }
        }
    }
}
