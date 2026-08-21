using System.Collections.Generic;

namespace OpenS4L.Server.Game.Services
{
    /// <summary>
    /// Self-contained, serializable snapshot of everything the player-save path persists.
    /// It is built from live game state (when <see cref="GameDataService"/> and the live
    /// <c>Player</c> are available) and enqueued on a Redis write-behind queue so a separate
    /// consumer can flush it to Postgres even after the owning game instance has died. It
    /// therefore must not reference live objects — only resolved values.
    /// </summary>
    public class PlayerSaveSnapshot
    {
        public int AccountId { get; set; }

        // Player scalars.
        public byte TutorialState { get; set; }
        public int TotalExperience { get; set; }
        public int PEN { get; set; }
        public int AP { get; set; }
        public int Coins1 { get; set; }
        public int Coins2 { get; set; }
        public byte CurrentCharacterSlot { get; set; }

        // Only true when the scalar player row changed (mirrors the old IsDirty gate on Player.Save).
        public bool PlayerRowDirty { get; set; }

        public List<long> ItemIdsToRemove { get; set; } = new List<long>();
        public List<SnapshotItem> Items { get; set; } = new List<SnapshotItem>();
        public List<long> CharacterIdsToRemove { get; set; } = new List<long>();
        public List<SnapshotCharacter> Characters { get; set; } = new List<SnapshotCharacter>();
    }

    public class SnapshotItem
    {
        public long Id { get; set; }
        public int ShopItemInfoId { get; set; }
        public int ShopPriceId { get; set; }
        public string Effects { get; set; } // JSON array of numbers.
        public byte Color { get; set; }
        public long PurchaseDate { get; set; }
        public int Durability { get; set; }
        public int MP { get; set; }
        public int MPLevel { get; set; }
        public bool Exists { get; set; }
    }

    public class SnapshotCharacter
    {
        public long Id { get; set; }
        public byte Slot { get; set; }
        public byte Gender { get; set; }
        public byte BasicHair { get; set; }
        public byte BasicFace { get; set; }
        public byte BasicShirt { get; set; }
        public byte BasicPants { get; set; }
        public bool Exists { get; set; }

        public long? Weapon1Id { get; set; }
        public long? Weapon2Id { get; set; }
        public long? Weapon3Id { get; set; }
        public long? SkillId { get; set; }
        public long? HairId { get; set; }
        public long? FaceId { get; set; }
        public long? ShirtId { get; set; }
        public long? PantsId { get; set; }
        public long? GlovesId { get; set; }
        public long? ShoesId { get; set; }
        public long? AccessoryId { get; set; }
        public long? PetId { get; set; }
    }
}
