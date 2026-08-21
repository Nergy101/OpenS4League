using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Logging;
using Microsoft.Extensions.Options;
using OpenS4L.Common;
using OpenS4L.Common.Configuration;
using OpenS4L.Database;
using OpenS4L.Database.Game;
using OpenS4L.Database.Helpers;
using OpenS4L.Network;
using OpenS4L.Network.Data.Club;
using OpenS4L.Network.Data.Game;
using OpenS4L.Network.Message.Club;
using OpenS4L.Network.Message.Game;
using OpenS4L.Network.Message.GameRule;
using OpenS4L.Server.Game.Mappers;
using OpenS4L.Server.Game.Services;

namespace OpenS4L.Server.Game
{
    public class Player : DatabaseObject, ISaveable
    {
        private ILogger _logger;
        private readonly GameOptions _gameOptions;
        private readonly GameDataService _gameDataService;
        private readonly ClanManager _clanManager;
        private readonly NicknameLookupService _nicknameLookupService;
        private readonly GameMapper _mapper;
        private byte _tutorialState;
        private uint _totalExperience;
        private uint _pen;
        private uint _ap;
        private uint _coins1;
        private uint _coins2;
        private PlayerState _state;

        public Session Session { get; private set; }
        public Account Account { get; private set; }
        public CharacterManager CharacterManager { get; }
        public PlayerInventory Inventory { get; }

        /// <summary>
        /// True when this player has any unsaved changes (scalar stats, inventory or characters).
        /// This is the write-behind coalescing signal: only players with pending changes are
        /// flushed on the periodic save cycle.
        /// </summary>
        public bool HasPendingChanges => IsDirty || Inventory.HasPendingChanges || CharacterManager.HasPendingChanges;
        public Clan Clan { get; internal set; }
        public ClanMember ClanMember => Clan?.GetMember(Account.Id);
        public byte TutorialState
        {
            get => _tutorialState;
            set => SetIfChanged(ref _tutorialState, value);
        }
        public uint TotalExperience
        {
            get => _totalExperience;
            set => SetIfChanged(ref _totalExperience, value);
        }
        public uint PEN
        {
            get => _pen;
            set => SetIfChanged(ref _pen, value);
        }
        public uint AP
        {
            get => _ap;
            set => SetIfChanged(ref _ap, value);
        }
        public uint Coins1
        {
            get => _coins1;
            set => SetIfChanged(ref _coins1, value);
        }
        public uint Coins2
        {
            get => _coins2;
            set => SetIfChanged(ref _coins2, value);
        }
        public Channel Channel { get; internal set; }
        public int Level => _gameDataService.GetLevelFromExperience(_totalExperience).Level;

        public Room Room { get; internal set; }
        public byte Slot { get; internal set; }
        public PlayerState State
        {
            get => _state;
            internal set
            {
                if (_state == value)
                    return;

                _state = value;
                OnStateChanged();
            }
        }
        public PlayerGameMode Mode { get; internal set; }
        public bool IsConnectingToRoom { get; internal set; }
        public bool IsReady { get; internal set; }
        public Team Team { get; internal set; }
        public PlayerScore Score { get; internal set; }
        public LongPeerId PeerId { get; internal set; }
        public DateTimeOffset StartPlayTime { get; internal set; }
        public DateTimeOffset[] CharacterStartPlayTime { get; internal set; }
        public bool IsInGMMode { get; set; }
        public bool IsLoading { get; internal set; }

        public event EventHandler<PlayerEventArgs> Disconnected;
        public event EventHandler<PlayerEventArgs> StateChanged;
        public event EventHandler<NicknameEventArgs> NicknameCreated;
        public event EventHandler<ChannelEventArgs> ChannelJoined;
        public event EventHandler<ChannelEventArgs> ChannelLeft;
        public event EventHandler<RoomPlayerEventArgs> RoomJoined;
        public event EventHandler<RoomPlayerEventArgs> RoomLeft;

        internal void OnDisconnected()
        {
            Room?.Leave(this);
            Channel?.Leave(this);

            Disconnected?.Invoke(this, new PlayerEventArgs(this));
        }

        protected virtual void OnStateChanged()
        {
            StateChanged?.Invoke(this, new PlayerEventArgs(this));
        }

        protected internal virtual void OnNicknameCreated(string nickname)
        {
            NicknameCreated?.Invoke(this, new NicknameEventArgs(this, nickname));
        }

        protected internal virtual void OnChannelJoined(Channel channel)
        {
            ChannelJoined?.Invoke(this, new ChannelEventArgs(channel, this));
        }

        protected internal virtual void OnChannelLeft(Channel channel)
        {
            ChannelLeft?.Invoke(this, new ChannelEventArgs(channel, this));
        }

        protected internal virtual void OnRoomJoined(Room room)
        {
            RoomJoined?.Invoke(this, new RoomPlayerEventArgs(room, this));
        }

        protected internal virtual void OnRoomLeft(Room room)
        {
            RoomLeft?.Invoke(this, new RoomPlayerEventArgs(room, this));
        }

        public Player(ILogger<Player> logger, IOptions<GameOptions> gameOptions, GameDataService gameDataService,
            CharacterManager characterManager, PlayerInventory inventory, ClanManager clanManager,
            NicknameLookupService nicknameLookupService, GameMapper mapper)
        {
            _logger = logger;
            _gameOptions = gameOptions.Value;
            _gameDataService = gameDataService;
            _clanManager = clanManager;
            _nicknameLookupService = nicknameLookupService;
            _mapper = mapper;
            CharacterManager = characterManager;
            Inventory = inventory;
            CharacterStartPlayTime = new DateTimeOffset[3];
        }

        internal void Initialize(Session session, Account account, PlayerEntity entity)
        {
            Session = session;
            Account = account;
            _logger = AddContextToLogger(_logger);
            _tutorialState = entity.TutorialState;
            _totalExperience = (uint)entity.TotalExperience;
            _pen = (uint)entity.PEN;
            _ap = (uint)entity.AP;
            _coins1 = (uint)entity.Coins1;
            _coins2 = (uint)entity.Coins2;

            if (entity.ClanMember != null)
                Clan = _clanManager[(uint)entity.ClanMember.ClanId];

            Inventory.Initialize(this, entity);
            CharacterManager.Initialize(this, entity);
        }

        public void Disconnect()
        {
            _ = DisconnectAsync();
        }

        public Task DisconnectAsync()
        {
            return Session.CloseAsync();
        }

        /// <summary>
        /// Gains experiences and levels up if the player earned enough experience
        /// </summary>
        /// <param name="amount">Amount of experience to earn</param>
        /// <returns>true if the player leveled up</returns>
        public bool GainExperience(uint amount)
        {
            _logger.Debug("Gained {Amount} experience", amount);

            var levels = _gameDataService.Levels;
            var levelInfo = levels.GetValueOrDefault(Level);
            if (levelInfo == null)
            {
                _logger.Warning("Level={Level} not found", Level);
                return false;
            }

            // We cant earn experience when we reached max level
            if (levelInfo.ExperienceToNextLevel == 0 || Level >= _gameOptions.MaxLevel)
                return false;

            var leveledUp = false;
            TotalExperience += amount;

            // Did we level up?
            // Using a loop for multiple level ups
            while (levelInfo.ExperienceToNextLevel != 0 &&
                   levelInfo.ExperienceToNextLevel <= (int)(TotalExperience - levelInfo.TotalExperience) &&
                   levelInfo.Level < _gameOptions.MaxLevel)
            {
                var newLevel = Level + 1;
                levelInfo = levels.GetValueOrDefault(newLevel);

                if (levelInfo == null)
                {
                    _logger.Warning("Can't level up because level={Level} not found", newLevel);
                    break;
                }

                _logger.Debug("Leveled up to {Level}", newLevel);

                var reward = _gameDataService.LevelRewards.GetValueOrDefault(newLevel);
                if (reward != null)
                {
                    _logger.Debug("Level reward type={MoneyType} value={Value}", reward.Type, reward.Money);
                    switch (reward.Type)
                    {
                        case MoneyType.PEN:
                            PEN += (uint)reward.Money;
                            break;

                        case MoneyType.AP:
                            AP += (uint)reward.Money;
                            break;

                        default:
                            _logger.Warning("Unknown moneyType={MoneyType}", reward.Type);
                            break;
                    }

                    SendMoneyUpdate();
                }

                leveledUp = true;
            }

            if (!leveledUp)
                return false;

            // TODO Update chat server
            // TODO Do we need this?
            // Session.Send(new SBeginAccountInfoAckMessage())

            return true;
        }

        public TimeSpan GetCurrentPlayTime()
        {
            return DateTimeOffset.Now - StartPlayTime;
        }

        public TimeSpan GetCharacterPlayTime(byte slot)
        {
            if (slot >= CharacterStartPlayTime.Length)
                return default;

            return DateTimeOffset.Now - CharacterStartPlayTime[slot];
        }

        /// <summary>
        /// Gets the maximum hp for the current character
        /// </summary>
        public float GetMaxHP()
        {
            return _gameDataService.GameTempos["GAMETEMPO_FREE"].ActorDefaultHPMax +
                   GetAttributeValue(EffectType.HP);
        }

        /// <summary>
        /// Gets the total attribute value for the current character
        /// </summary>
        /// <param name="attribute">The attribute to retrieve</param>
        /// <returns></returns>
        public float GetAttributeValue(EffectType attribute)
        {
            if (CharacterManager.CurrentCharacter == null)
                return 0;

            var character = CharacterManager.CurrentCharacter;
            var value = GetAttributeValueFromItems(attribute, character.Weapons.GetItems());
            value += GetAttributeValueFromItems(attribute, character.Skills.GetItems());
            value += GetAttributeValueFromItems(attribute, character.Costumes.GetItems());

            return value;
        }

        /// <summary>
        /// Gets the total attribute rate for the current character
        /// </summary>
        /// <param name="attribute">The attribute to retrieve</param>
        /// <returns></returns>
        public float GetAttributeRate(EffectType attribute)
        {
            if (CharacterManager.CurrentCharacter == null)
                return 0;

            var character = CharacterManager.CurrentCharacter;
            var value = GetAttributeRateFromItems(attribute, character.Weapons.GetItems());
            value += GetAttributeRateFromItems(attribute, character.Skills.GetItems());
            value += GetAttributeRateFromItems(attribute, character.Costumes.GetItems());

            return value;
        }

        public async Task SendAccountInformation()
        {
            // S4 League unlocks character slots 2 & 3 via the cash-shop "Character Slot Created"
            // coupon (item 6000015). The client unlocks one extra slot per coupon owned (retail
            // auto-grants it: "Will be paid automatically"). MaxSlots=3 below is the total, but the
            // client still shows slots as locked unless the account owns the coupons. Grant 2 of
            // them so all 3 slots are usable.
            var slotCouponCount = Inventory.Count(x => x.ItemNumber == (ItemNumber)6000015);
            for (var i = slotCouponCount; i < 2; i++)
            {
                // Guard: only grant the slot coupon if the shop actually carries it. If it isn't
                // in GameDataService.ShopItems (e.g. the shop reload didn't include it), creating
                // it would throw "Item not found" and crash the whole login (a bot/player on
                // re-login would be disconnected). The coupon just unlocks extra character slots,
                // so skipping it is safe rather than fatal.
                if (_gameDataService.GetShopItemInfo((ItemNumber)6000015, ItemPriceType.PEN) == null)
                    break;

                Inventory.Create((ItemNumber)6000015, ItemPriceType.PEN, ItemPeriodType.None, 0, 0,
                    Array.Empty<uint>(), 1, false);
            }

            Session.Send(new ItemInventoryInfoAckMessage
            {
                Items = Inventory.Select(x => _mapper.ToItemDto(x)).ToArray()
            });

            Session.Send(new CharacterCurrentSlotInfoAckMessage
            {
                ActiveCharacter = CharacterManager.CurrentSlot, CharacterCount = (byte)CharacterManager.Count, MaxSlots = 3
            });

            foreach (var character in CharacterManager)
            {
                Session.Send(new CharacterCurrentInfoAckMessage
                {
                    Slot = character.Slot,
                    Style = new CharacterStyle(character.Gender, character.Slot,
                        character.Hair.Variation, character.Face.Variation,
                        character.Shirt.Variation, character.Pants.Variation)
                });

                var message = new CharacterCurrentItemInfoAckMessage
                {
                    Slot = character.Slot,
                    Weapons = character.Weapons.GetItems().Select(x => x?.Id ?? 0).ToArray(),
                    Skills = new[]
                    {
                        character.Skills.GetItem(0).Item1?.Id ?? 0
                    },
                    Clothes = character.Costumes.GetItems().Select(x => x?.Id ?? 0).ToArray()
                };

                Session.Send(message);
            }

            SendMoneyUpdate();
            Session.Send(new ServerResultAckMessage(ServerResult.WelcomeToS4World));

            // The client gates channel entry on the character having weapon+skill licenses
            // (its "you cannot enter this channel ... acquire the weapon and skill license"
            // message). It learns those licenses from LicenseMyInfoAckMessage; if we never send
            // it, the client assumes the player owns no licenses and refuses to enter any
            // channel. The reference stack shipped with EnableLicenseRequirement:false and
            // effectively granted all licenses — mirror that by sending every license.
            Session.Send(new LicenseMyInfoAckMessage(
                Enum.GetValues(typeof(ItemLicense))
                    .Cast<ItemLicense>()
                    .Where(x => x != ItemLicense.None)
                    .Select(x => (uint)x)
                    .ToArray()
            ));

            Session.Send(new PlayerAccountInfoAckMessage(new PlayerAccountInfoDto
            {
                Level = (byte)Level,
                TotalExperience = TotalExperience,
                AP = AP,
                PEN = PEN,
                TutorialState = (uint)(_gameOptions.EnableTutorial ? TutorialState : 2),
                Nickname = Account.Nickname,
                IsGM = Account.SecurityLevel > SecurityLevel.User
            }));

            SendClubInfo();
        }

        public void SendClubInfo()
        {
            Session.Send(new ClubMyInfoAckMessage
            {
                ClanId = Clan?.Id ?? 0,
                ClanIcon = Clan?.Icon,
                ClanName = Clan?.Name,
                State = ClanMember?.State ?? ClubMemberState.None,
                Role = ClanMember?.Role ?? ClubRole.Normal
            });
        }

        public void SendMoneyUpdate()
        {
            Session.Send(new MoneyRefreshCashInfoAckMessage(PEN, AP));
            Session.Send(new MoenyRefreshCoinInfoAckMessage(Coins1, Coins2));
        }

        public void SendClanLeaveEvents()
        {
            if (Clan == null)
                return;

            var entries = Clan.Events
                .Where(x => x.Event == ClanEvent.Leave && Clan.GetMember(x.AccountId) == null ||
                            x.Event == ClanEvent.Kick && Clan.GetMember((ulong)x.Value1) == null ||
                            x.Event == ClanEvent.Ban && Clan.Bans.Contains((ulong)x.Value1))
                .GroupBy(x => x.Event == ClanEvent.Leave ? x.AccountId : (ulong)x.Value1)
                .Select(x =>
                {
                    var eventEntry = x.OrderByDescending(_ => _.Date).First();
                    var dto = new MemberLeftDto
                    {
                        AccountId = eventEntry.Event == ClanEvent.Leave ? (uint)eventEntry.AccountId : (uint)eventEntry.Value1,
                        Date = eventEntry.Date
                    };
                    dto.Name = _nicknameLookupService.GetNickname(dto.AccountId);
                    switch (eventEntry.Event)
                    {
                        case ClanEvent.Leave:
                            dto.Reason = ClubLeaveReason.Leave;
                            break;

                        case ClanEvent.Kick:
                            dto.Reason = ClubLeaveReason.Kick;
                            break;

                        case ClanEvent.Ban:
                            dto.Reason = ClubLeaveReason.Ban;
                            break;
                    }

                    return dto;
                })
                .ToArray();
            Session.Send(new ClubUnjoinerListAckMessage(entries));
        }

        public void SendClanJoinEvents()
        {
            if (Clan == null)
                return;

            var newMembers = Clan
                .Where(x => x.State == ClubMemberState.Joined)
                .OrderByDescending(x => x.JoinDate)
                .Select(x => _mapper.ToNewMemberInfoDto(x))
                .ToArray();
            Session.Send(new ClubNewJoinMemberInfoAckMessage(newMembers));
        }

        /// <summary>
        /// Sends a message to the game master console
        /// </summary>
        /// <param name="message">The message to send</param>
        public void SendConsoleMessage(string message)
        {
            Session.Send(new AdminActionAckMessage(0, message));
        }

        /// <summary>
        /// Sends a notice message
        /// </summary>
        /// <param name="message">The message to send</param>
        public void SendNotice(string message)
        {
            Session.Send(new NoticeAdminMessageAckMessage(message));
        }

        public void SendBriefing()
        {
            if (Room == null)
                return;

            var briefing = Room.GetBriefing();
            Session.Send(new GameBriefingInfoAckMessage(false, false, briefing.GetData()));
        }

        public async Task Save(GameContext db)
        {
            var snapshot = BuildSaveSnapshot();
            PlayerSaveWriter.WritePlayer(db, snapshot);
            await PlayerSaveWriter.WriteInventory(db, snapshot);
            await PlayerSaveWriter.WriteCharacters(db, snapshot);
            ClearPendingChanges();
        }

        /// <summary>
        /// Captures the player's unsaved state (scalars + inventory + characters) into a
        /// self-contained snapshot. Pure read — does not mutate live state, so a failed enqueue
        /// leaves everything dirty for the next publish tick.
        /// </summary>
        public PlayerSaveSnapshot BuildSaveSnapshot()
        {
            var s = new PlayerSaveSnapshot
            {
                AccountId = (int)Account.Id,
                TutorialState = TutorialState,
                TotalExperience = (int)TotalExperience,
                PEN = (int)PEN,
                AP = (int)AP,
                Coins1 = (int)Coins1,
                Coins2 = (int)Coins2,
                CurrentCharacterSlot = CharacterManager.CurrentSlot,
                PlayerRowDirty = IsDirty
            };

            Inventory.BuildSnapshot(s);
            CharacterManager.BuildSnapshot(s);
            return s;
        }

        /// <summary>
        /// Marks the captured snapshot as persisted: clears the scalar dirty flag and the
        /// inventory/character pending changes. Called only after the snapshot was durably
        /// enqueued (or written directly) so a failed publish leaves state dirty for retry.
        /// </summary>
        public void ClearPendingChanges()
        {
            SetDirtyState(false);
            Inventory.ClearPendingChanges();
            CharacterManager.ClearPendingChanges();
        }

        public ILogger AddContextToLogger(ILogger logger)
        {
            return logger.ForContext(
                ("AccountId", Account.Id),
                ("HostId", Session.HostId),
                ("EndPoint", Session.RemoteEndPoint.ToString())
            );
        }

        private static float GetAttributeValueFromItems(EffectType attribute, IEnumerable<PlayerItem> items)
        {
            return items.Where(item => item != null)
                .SelectMany(item => item.GetItemEffects())
                .Where(effect => effect != null && effect.EffectType == attribute)
                .Sum(attrib => attrib.Value);
        }

        private static float GetAttributeRateFromItems(EffectType attribute, IEnumerable<PlayerItem> items)
        {
            return items.Where(item => item != null)
                .SelectMany(item => item.GetItemEffects())
                .Where(effect => effect != null && effect.EffectType == attribute)
                .Sum(attrib => attrib.Rate);
        }
    }
}
