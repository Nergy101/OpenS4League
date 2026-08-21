using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpenS4L.Database;
using OpenS4L.Database.Game;

namespace OpenS4L.Server.Game.Services
{
    /// <summary>
    /// The EF persistence half of the player-save path. It operates purely on a
    /// <see cref="PlayerSaveSnapshot"/> — never on live <c>Player</c>/<c>GameDataService</c>
    /// objects — so the same code is used by both the direct save path (on logout) and the
    /// Redis write-behind consumer (which may run after the owning game instance has died).
    ///
    /// This reproduces, byte-for-byte, the entity construction previously inside
    /// <c>Player.Save</c>/<c>PlayerInventory.Save</c>/<c>CharacterManager.Save</c>, including the
    /// character equip-id mapping. <c>SaveChangesAsync</c> is intentionally NOT called here; the
    /// caller batches many players and flushes once.
    /// </summary>
    public static class PlayerSaveWriter
    {
        public static void WritePlayer(GameContext db, PlayerSaveSnapshot s)
        {
            // Only touch the player row when its scalar stats changed (mirrors the old IsDirty gate).
            if (!s.PlayerRowDirty)
                return;

            db.Players.Update(new PlayerEntity
            {
                Id = s.AccountId,
                TutorialState = s.TutorialState,
                TotalExperience = s.TotalExperience,
                PEN = s.PEN,
                AP = s.AP,
                Coins1 = s.Coins1,
                Coins2 = s.Coins2,
                CurrentCharacterSlot = s.CurrentCharacterSlot
            });
        }

        public static async Task WriteInventory(GameContext db, PlayerSaveSnapshot s)
        {
            if (s.ItemIdsToRemove.Count > 0)
            {
                var idsToRemove = s.ItemIdsToRemove;
                await db.PlayerItems.Where(x => idsToRemove.Contains(x.Id)).ExecuteDeleteAsync();
            }

            foreach (var item in s.Items)
            {
                if (!item.Exists)
                {
                    db.PlayerItems.Add(new PlayerItemEntity
                    {
                        Id = item.Id,
                        PlayerId = s.AccountId,
                        ShopItemInfoId = item.ShopItemInfoId,
                        ShopPriceId = item.ShopPriceId,
                        Effects = item.Effects,
                        Color = item.Color,
                        PurchaseDate = item.PurchaseDate,
                        Durability = item.Durability,
                        MP = item.MP,
                        MPLevel = item.MPLevel
                    });
                }
                else
                {
                    // Note: the legacy update path deliberately omits MP/MPLevel (only new items
                    // persist them); reproduced faithfully so enchanting MP isn't overwritten here.
                    db.PlayerItems.Update(new PlayerItemEntity
                    {
                        Id = item.Id,
                        PlayerId = s.AccountId,
                        ShopItemInfoId = item.ShopItemInfoId,
                        ShopPriceId = item.ShopPriceId,
                        Effects = item.Effects,
                        Color = item.Color,
                        PurchaseDate = item.PurchaseDate,
                        Durability = item.Durability
                    });
                }
            }
        }

        public static async Task WriteCharacters(GameContext db, PlayerSaveSnapshot s)
        {
            if (s.CharacterIdsToRemove.Count > 0)
            {
                var idsToRemove = s.CharacterIdsToRemove;
                await db.PlayerCharacters.Where(x => idsToRemove.Contains(x.Id)).ExecuteDeleteAsync();
            }

            foreach (var character in s.Characters)
            {
                var entity = new PlayerCharacterEntity
                {
                    Id = character.Id,
                    PlayerId = s.AccountId,
                    Slot = character.Slot,
                    Gender = character.Gender,
                    BasicHair = character.BasicHair,
                    BasicFace = character.BasicFace,
                    BasicShirt = character.BasicShirt,
                    BasicPants = character.BasicPants,
                    Weapon1Id = character.Weapon1Id,
                    Weapon2Id = character.Weapon2Id,
                    Weapon3Id = character.Weapon3Id,
                    SkillId = character.SkillId,
                    HairId = character.HairId,
                    FaceId = character.FaceId,
                    ShirtId = character.ShirtId,
                    PantsId = character.PantsId,
                    GlovesId = character.GlovesId,
                    ShoesId = character.ShoesId,
                    AccessoryId = character.AccessoryId,
                    PetId = character.PetId
                };

                if (!character.Exists)
                    db.PlayerCharacters.Add(entity);
                else
                    db.PlayerCharacters.Update(entity);
            }
        }
    }
}
