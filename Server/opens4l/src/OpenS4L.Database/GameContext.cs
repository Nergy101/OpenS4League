using Microsoft.EntityFrameworkCore;
using OpenS4L.Database.Game;

namespace OpenS4L.Database
{
    public class GameContext : DbContext
    {
        public DbSet<PlayerEntity> Players { get; set; }
        public DbSet<PlayerCharacterEntity> PlayerCharacters { get; set; }
        public DbSet<PlayerDenyEntity> PlayerIgnores { get; set; }
        public DbSet<PlayerFriendEntity> PlayerFriends { get; set; }
        public DbSet<PlayerItemEntity> PlayerItems { get; set; }
        public DbSet<PlayerMailEntity> PlayerMails { get; set; }
        public DbSet<PlayerSettingEntity> PlayerSettings { get; set; }
        public DbSet<ShopEffectGroupEntity> EffectGroups { get; set; }
        public DbSet<ShopEffectEntity> Effects { get; set; }
        public DbSet<ShopPriceGroupEntity> PriceGroups { get; set; }
        public DbSet<ShopPriceEntity> Prices { get; set; }
        public DbSet<ShopItemEntity> Items { get; set; }
        public DbSet<ShopItemInfoEntity> ItemInfos { get; set; }
        public DbSet<ShopVersionEntity> ShopVersion { get; set; }
        public DbSet<StartItemEntity> StartItems { get; set; }
        public DbSet<LevelRewardEntity> LevelRewards { get; set; }
        public DbSet<ChannelEntity> Channels { get; set; }
        public DbSet<ClanEntity> Clans { get; set; }
        public DbSet<ClanMemberEntity> ClanMembers { get; set; }
        public DbSet<ClanBanEntity> ClanBans { get; set; }
        public DbSet<ClanEventEntity> ClanEvents { get; set; }

        public GameContext(DbContextOptions<GameContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ShopEffectGroupEntity>()
                .HasIndex(x => x.Name).IsUnique();

            modelBuilder.Entity<ShopPriceGroupEntity>()
                .HasIndex(x => x.Name).IsUnique();

            modelBuilder.Entity<PlayerDenyEntity>()
                .HasOne(x => x.Player)
                .WithMany(x => x.Ignores);

            modelBuilder.Entity<PlayerEntity>()
                .HasOne(x => x.ClanMember)
                .WithOne(x => x.Player);

            modelBuilder.Entity<PlayerDenyEntity>()
                .HasOne(x => x.DenyPlayer);

            modelBuilder.Entity<PlayerFriendEntity>()
                .HasOne(x => x.Player)
                .WithMany(x => x.Friends);

            modelBuilder.Entity<PlayerFriendEntity>()
                .HasOne(x => x.FriendPlayer);

            modelBuilder.Entity<PlayerMailEntity>()
                .HasOne(x => x.Player)
                .WithMany(x => x.Inbox);

            modelBuilder.Entity<PlayerMailEntity>()
                .HasOne(x => x.SenderPlayer);

            modelBuilder.Entity<ClanEntity>()
                .HasIndex(x => x.Name).IsUnique();

            modelBuilder.Entity<ClanEntity>()
                .HasOne(x => x.Owner);

            modelBuilder.Entity<ClanEntity>()
                .HasMany(x => x.Members)
                .WithOne(x => x.Clan);

            modelBuilder.Entity<ClanEntity>()
                .HasMany(x => x.Bans)
                .WithOne(x => x.Clan);

            modelBuilder.Entity<ClanEntity>()
                .HasMany(x => x.Events)
                .WithOne(x => x.Clan);
        }
    }
}
