using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpenS4L.Database.Migrations.Game
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "channels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerLimit = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Color = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    MinLevel = table.Column<int>(type: "integer", nullable: false),
                    MaxLevel = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "level_rewards",
                columns: table => new
                {
                    Level = table.Column<int>(type: "integer", nullable: false),
                    MoneyType = table.Column<byte>(type: "smallint", nullable: false),
                    Money = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_level_rewards", x => x.Level);
                });

            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TutorialState = table.Column<byte>(type: "smallint", nullable: false),
                    TotalExperience = table.Column<int>(type: "integer", nullable: false),
                    PEN = table.Column<int>(type: "integer", nullable: false),
                    AP = table.Column<int>(type: "integer", nullable: false),
                    Coins1 = table.Column<int>(type: "integer", nullable: false),
                    Coins2 = table.Column<int>(type: "integer", nullable: false),
                    CurrentCharacterSlot = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shop_effect_groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PreviewEffect = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_effect_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shop_items",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequiredGender = table.Column<byte>(type: "smallint", nullable: false),
                    RequiredLicense = table.Column<byte>(type: "smallint", nullable: false),
                    Colors = table.Column<byte>(type: "smallint", nullable: false),
                    UniqueColors = table.Column<byte>(type: "smallint", nullable: false),
                    RequiredLevel = table.Column<byte>(type: "smallint", nullable: false),
                    LevelLimit = table.Column<byte>(type: "smallint", nullable: false),
                    RequiredMasterLevel = table.Column<byte>(type: "smallint", nullable: false),
                    IsOneTimeUse = table.Column<bool>(type: "boolean", nullable: false),
                    IsDestroyable = table.Column<bool>(type: "boolean", nullable: false),
                    MainTab = table.Column<byte>(type: "smallint", nullable: false),
                    SubTab = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shop_price_groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PriceType = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_price_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shop_version",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_version", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "clans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerId = table.Column<int>(type: "integer", nullable: false),
                    CreationDate = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Icon = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Description = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Area = table.Column<byte>(type: "smallint", nullable: false),
                    Activity = table.Column<byte>(type: "smallint", nullable: false),
                    Question1 = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Question2 = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Question3 = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Question4 = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Question5 = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Class = table.Column<byte>(type: "smallint", nullable: false),
                    Announcement = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredLevel = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clans_players_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "player_deny",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    DenyPlayerId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_deny", x => x.Id);
                    table.ForeignKey(
                        name: "FK_player_deny_players_DenyPlayerId",
                        column: x => x.DenyPlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_player_deny_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "player_friends",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    FriendPlayerId = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_friends", x => x.Id);
                    table.ForeignKey(
                        name: "FK_player_friends_players_FriendPlayerId",
                        column: x => x.FriendPlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_player_friends_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "player_mails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    SenderPlayerId = table.Column<int>(type: "integer", nullable: false),
                    SentDate = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsMailNew = table.Column<bool>(type: "boolean", nullable: false),
                    IsMailDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_mails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_player_mails_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_player_mails_players_SenderPlayerId",
                        column: x => x.SenderPlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "player_settings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Setting = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_player_settings_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_effects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EffectGroupId = table.Column<int>(type: "integer", nullable: false),
                    Effect = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_effects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shop_effects_shop_effect_groups_EffectGroupId",
                        column: x => x.EffectGroupId,
                        principalTable: "shop_effect_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_iteminfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShopItemId = table.Column<long>(type: "bigint", nullable: false),
                    PriceGroupId = table.Column<int>(type: "integer", nullable: false),
                    EffectGroupId = table.Column<int>(type: "integer", nullable: false),
                    DiscountPercentage = table.Column<byte>(type: "smallint", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_iteminfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shop_iteminfos_shop_effect_groups_EffectGroupId",
                        column: x => x.EffectGroupId,
                        principalTable: "shop_effect_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shop_iteminfos_shop_items_ShopItemId",
                        column: x => x.ShopItemId,
                        principalTable: "shop_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shop_iteminfos_shop_price_groups_PriceGroupId",
                        column: x => x.PriceGroupId,
                        principalTable: "shop_price_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shop_prices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PriceGroupId = table.Column<int>(type: "integer", nullable: false),
                    PeriodType = table.Column<byte>(type: "smallint", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    IsRefundable = table.Column<bool>(type: "boolean", nullable: false),
                    Durability = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_prices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shop_prices_shop_price_groups_PriceGroupId",
                        column: x => x.PriceGroupId,
                        principalTable: "shop_price_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clan_bans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClanId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clan_bans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clan_bans_clans_ClanId",
                        column: x => x.ClanId,
                        principalTable: "clans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_clan_bans_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clan_events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClanId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<byte>(type: "smallint", nullable: false),
                    Value1 = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clan_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clan_events_clans_ClanId",
                        column: x => x.ClanId,
                        principalTable: "clans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_clan_events_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clan_members",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClanId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    JoinDate = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<byte>(type: "smallint", nullable: false),
                    Role = table.Column<byte>(type: "smallint", nullable: false),
                    LastLoginDate = table.Column<long>(type: "bigint", nullable: false),
                    Answer1 = table.Column<string>(type: "text", nullable: true),
                    Answer2 = table.Column<string>(type: "text", nullable: true),
                    Answer3 = table.Column<string>(type: "text", nullable: true),
                    Answer4 = table.Column<string>(type: "text", nullable: true),
                    Answer5 = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clan_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clan_members_clans_ClanId",
                        column: x => x.ClanId,
                        principalTable: "clans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_clan_members_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "player_items",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    ShopItemInfoId = table.Column<int>(type: "integer", nullable: false),
                    ShopPriceId = table.Column<int>(type: "integer", nullable: false),
                    Effects = table.Column<string>(type: "text", nullable: true),
                    Color = table.Column<byte>(type: "smallint", nullable: false),
                    PurchaseDate = table.Column<long>(type: "bigint", nullable: false),
                    Durability = table.Column<int>(type: "integer", nullable: false),
                    MP = table.Column<int>(type: "integer", nullable: false),
                    MPLevel = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_player_items_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_player_items_shop_iteminfos_ShopItemInfoId",
                        column: x => x.ShopItemInfoId,
                        principalTable: "shop_iteminfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_player_items_shop_prices_ShopPriceId",
                        column: x => x.ShopPriceId,
                        principalTable: "shop_prices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "start_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ShopItemInfoId = table.Column<int>(type: "integer", nullable: false),
                    ShopPriceId = table.Column<int>(type: "integer", nullable: false),
                    Color = table.Column<byte>(type: "smallint", nullable: false),
                    RequiredSecurityLevel = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_start_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_start_items_shop_iteminfos_ShopItemInfoId",
                        column: x => x.ShopItemInfoId,
                        principalTable: "shop_iteminfos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_start_items_shop_prices_ShopPriceId",
                        column: x => x.ShopPriceId,
                        principalTable: "shop_prices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "player_characters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Slot = table.Column<byte>(type: "smallint", nullable: false),
                    Gender = table.Column<byte>(type: "smallint", nullable: false),
                    BasicHair = table.Column<byte>(type: "smallint", nullable: false),
                    BasicFace = table.Column<byte>(type: "smallint", nullable: false),
                    BasicShirt = table.Column<byte>(type: "smallint", nullable: false),
                    BasicPants = table.Column<byte>(type: "smallint", nullable: false),
                    Weapon1Id = table.Column<long>(type: "bigint", nullable: true),
                    Weapon2Id = table.Column<long>(type: "bigint", nullable: true),
                    Weapon3Id = table.Column<long>(type: "bigint", nullable: true),
                    SkillId = table.Column<long>(type: "bigint", nullable: true),
                    HairId = table.Column<long>(type: "bigint", nullable: true),
                    FaceId = table.Column<long>(type: "bigint", nullable: true),
                    ShirtId = table.Column<long>(type: "bigint", nullable: true),
                    PantsId = table.Column<long>(type: "bigint", nullable: true),
                    GlovesId = table.Column<long>(type: "bigint", nullable: true),
                    ShoesId = table.Column<long>(type: "bigint", nullable: true),
                    AccessoryId = table.Column<long>(type: "bigint", nullable: true),
                    PetId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_characters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_player_characters_player_items_AccessoryId",
                        column: x => x.AccessoryId,
                        principalTable: "player_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_player_characters_player_items_FaceId",
                        column: x => x.FaceId,
                        principalTable: "player_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_player_characters_player_items_GlovesId",
                        column: x => x.GlovesId,
                        principalTable: "player_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_player_characters_player_items_HairId",
                        column: x => x.HairId,
                        principalTable: "player_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_player_characters_player_items_PantsId",
                        column: x => x.PantsId,
                        principalTable: "player_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_player_characters_player_items_PetId",
                        column: x => x.PetId,
                        principalTable: "player_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_player_characters_player_items_ShirtId",
                        column: x => x.ShirtId,
                        principalTable: "player_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_player_characters_player_items_ShoesId",
                        column: x => x.ShoesId,
                        principalTable: "player_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_player_characters_player_items_SkillId",
                        column: x => x.SkillId,
                        principalTable: "player_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_player_characters_player_items_Weapon1Id",
                        column: x => x.Weapon1Id,
                        principalTable: "player_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_player_characters_player_items_Weapon2Id",
                        column: x => x.Weapon2Id,
                        principalTable: "player_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_player_characters_player_items_Weapon3Id",
                        column: x => x.Weapon3Id,
                        principalTable: "player_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_player_characters_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clan_bans_ClanId",
                table: "clan_bans",
                column: "ClanId");

            migrationBuilder.CreateIndex(
                name: "IX_clan_bans_PlayerId",
                table: "clan_bans",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_clan_events_ClanId",
                table: "clan_events",
                column: "ClanId");

            migrationBuilder.CreateIndex(
                name: "IX_clan_events_PlayerId",
                table: "clan_events",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_clan_members_ClanId",
                table: "clan_members",
                column: "ClanId");

            migrationBuilder.CreateIndex(
                name: "IX_clan_members_PlayerId",
                table: "clan_members",
                column: "PlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clans_Name",
                table: "clans",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clans_OwnerId",
                table: "clans",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_player_characters_AccessoryId",
                table: "player_characters",
                column: "AccessoryId");

            migrationBuilder.CreateIndex(
                name: "IX_player_characters_FaceId",
                table: "player_characters",
                column: "FaceId");

            migrationBuilder.CreateIndex(
                name: "IX_player_characters_GlovesId",
                table: "player_characters",
                column: "GlovesId");

            migrationBuilder.CreateIndex(
                name: "IX_player_characters_HairId",
                table: "player_characters",
                column: "HairId");

            migrationBuilder.CreateIndex(
                name: "IX_player_characters_PantsId",
                table: "player_characters",
                column: "PantsId");

            migrationBuilder.CreateIndex(
                name: "IX_player_characters_PetId",
                table: "player_characters",
                column: "PetId");

            migrationBuilder.CreateIndex(
                name: "IX_player_characters_PlayerId",
                table: "player_characters",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_player_characters_ShirtId",
                table: "player_characters",
                column: "ShirtId");

            migrationBuilder.CreateIndex(
                name: "IX_player_characters_ShoesId",
                table: "player_characters",
                column: "ShoesId");

            migrationBuilder.CreateIndex(
                name: "IX_player_characters_SkillId",
                table: "player_characters",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_player_characters_Weapon1Id",
                table: "player_characters",
                column: "Weapon1Id");

            migrationBuilder.CreateIndex(
                name: "IX_player_characters_Weapon2Id",
                table: "player_characters",
                column: "Weapon2Id");

            migrationBuilder.CreateIndex(
                name: "IX_player_characters_Weapon3Id",
                table: "player_characters",
                column: "Weapon3Id");

            migrationBuilder.CreateIndex(
                name: "IX_player_deny_DenyPlayerId",
                table: "player_deny",
                column: "DenyPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_player_deny_PlayerId",
                table: "player_deny",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_player_friends_FriendPlayerId",
                table: "player_friends",
                column: "FriendPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_player_friends_PlayerId",
                table: "player_friends",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_player_items_PlayerId",
                table: "player_items",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_player_items_ShopItemInfoId",
                table: "player_items",
                column: "ShopItemInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_player_items_ShopPriceId",
                table: "player_items",
                column: "ShopPriceId");

            migrationBuilder.CreateIndex(
                name: "IX_player_mails_PlayerId",
                table: "player_mails",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_player_mails_SenderPlayerId",
                table: "player_mails",
                column: "SenderPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_player_settings_PlayerId",
                table: "player_settings",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_effect_groups_Name",
                table: "shop_effect_groups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shop_effects_EffectGroupId",
                table: "shop_effects",
                column: "EffectGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_iteminfos_EffectGroupId",
                table: "shop_iteminfos",
                column: "EffectGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_iteminfos_PriceGroupId",
                table: "shop_iteminfos",
                column: "PriceGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_iteminfos_ShopItemId",
                table: "shop_iteminfos",
                column: "ShopItemId");

            migrationBuilder.CreateIndex(
                name: "IX_shop_price_groups_Name",
                table: "shop_price_groups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shop_prices_PriceGroupId",
                table: "shop_prices",
                column: "PriceGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_start_items_ShopItemInfoId",
                table: "start_items",
                column: "ShopItemInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_start_items_ShopPriceId",
                table: "start_items",
                column: "ShopPriceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channels");

            migrationBuilder.DropTable(
                name: "clan_bans");

            migrationBuilder.DropTable(
                name: "clan_events");

            migrationBuilder.DropTable(
                name: "clan_members");

            migrationBuilder.DropTable(
                name: "level_rewards");

            migrationBuilder.DropTable(
                name: "player_characters");

            migrationBuilder.DropTable(
                name: "player_deny");

            migrationBuilder.DropTable(
                name: "player_friends");

            migrationBuilder.DropTable(
                name: "player_mails");

            migrationBuilder.DropTable(
                name: "player_settings");

            migrationBuilder.DropTable(
                name: "shop_effects");

            migrationBuilder.DropTable(
                name: "shop_version");

            migrationBuilder.DropTable(
                name: "start_items");

            migrationBuilder.DropTable(
                name: "clans");

            migrationBuilder.DropTable(
                name: "player_items");

            migrationBuilder.DropTable(
                name: "players");

            migrationBuilder.DropTable(
                name: "shop_iteminfos");

            migrationBuilder.DropTable(
                name: "shop_prices");

            migrationBuilder.DropTable(
                name: "shop_effect_groups");

            migrationBuilder.DropTable(
                name: "shop_items");

            migrationBuilder.DropTable(
                name: "shop_price_groups");
        }
    }
}
