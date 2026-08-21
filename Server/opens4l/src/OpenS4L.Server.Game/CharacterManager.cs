using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Logging;
using Microsoft.EntityFrameworkCore;
using OpenS4L.Common;
using OpenS4L.Database;
using OpenS4L.Database.Game;
using OpenS4L.Database.Helpers;
using OpenS4L.Network.Message.Game;
using OpenS4L.Server.Game.Services;

namespace OpenS4L.Server.Game
{
    public class CharacterManager : IReadOnlyCollection<Character>
    {
        private ILogger _logger;
        private readonly IdGeneratorService _idGeneratorService;
        private readonly GameDataService _gameDataService;
        private readonly ILoggerFactory _loggerFactory;
        private readonly Dictionary<byte, Character> _characters;
        private readonly ConcurrentStack<Character> _charactersToRemove;
        // ReSharper disable once NotAccessedField.Local

        public Player Player { get; private set; }
        public Character CurrentCharacter => GetCharacter(CurrentSlot);
        public byte CurrentSlot { get; private set; }
        public int Count => _characters.Count;

        /// <summary>
        /// True when any character is dirty or queued for removal (i.e. this character set has
        /// unsaved changes that need flushing on the next save cycle).
        /// </summary>
        public bool HasPendingChanges => !_charactersToRemove.IsEmpty || _characters.Values.Any(x => x.IsDirty);

        /// <summary>
        /// Returns the character on the given slot.
        /// Returns null if the character does not exist
        /// </summary>
        public Character this[byte slot] => GetCharacter(slot);

        public CharacterManager(ILogger<CharacterManager> logger, IdGeneratorService idGeneratorService,
            GameDataService gameDataService, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _idGeneratorService = idGeneratorService;
            _gameDataService = gameDataService;
            _loggerFactory = loggerFactory;
            _characters = new Dictionary<byte, Character>();
            _charactersToRemove = new ConcurrentStack<Character>();
        }

        internal void Initialize(Player plr, PlayerEntity entity)
        {
            _logger = plr.AddContextToLogger(_logger);
            Player = plr;
            CurrentSlot = entity.CurrentCharacterSlot;

            foreach (var @char in entity.Characters.Select(@char =>
                new Character(_loggerFactory.CreateLogger<Character>(), this, @char, _gameDataService)))
            {
                if (!_characters.TryAdd(@char.Slot, @char))
                    _logger.Warning("Multiple characters on slot={Slot}", @char.Slot);
            }
        }

        /// <summary>
        /// Returns the character on the given slot.
        /// Returns null if the character does not exist
        /// </summary>
        public Character GetCharacter(byte slot)
        {
            return _characters.GetValueOrDefault(slot);
        }

        /// <summary>
        /// Creates a new character
        /// </summary>
        public (Character character, CharacterCreateResult result) Create(byte slot, CharacterGender gender,
            byte hair, byte face, byte shirt, byte pants, byte gloves, byte shoes)
        {
            var logger = _logger.ForContext(
                ("Method", "Create"),
                ("Slot", slot),
                ("Gender", gender),
                ("Hair", hair),
                ("Face", face),
                ("Shirt", shirt),
                ("Pants", pants),
                ("Gloves", gloves),
                ("Shoes", shoes));

            if (Count >= 3)
                return (null, CharacterCreateResult.LimitReached);

            if (_characters.ContainsKey(slot))
                return (null, CharacterCreateResult.SlotInUse);

            var defaultHair = _gameDataService.GetDefaultItem(gender, CostumeSlot.Hair, hair);
            if (defaultHair == null)
            {
                logger.Warning("Invalid hair");
                return (null, CharacterCreateResult.InvalidDefaultItem);
            }

            var defaultFace = _gameDataService.GetDefaultItem(gender, CostumeSlot.Face, face);
            if (defaultFace == null)
            {
                logger.Warning("Invalid face");
                return (null, CharacterCreateResult.InvalidDefaultItem);
            }

            var defaultShirt = _gameDataService.GetDefaultItem(gender, CostumeSlot.Shirt, shirt);
            if (defaultShirt == null)
            {
                logger.Warning("Invalid shirt");
                return (null, CharacterCreateResult.InvalidDefaultItem);
            }

            var defaultPants = _gameDataService.GetDefaultItem(gender, CostumeSlot.Pants, pants);
            if (defaultPants == null)
            {
                logger.Warning("Invalid pants");
                return (null, CharacterCreateResult.InvalidDefaultItem);
            }

            var defaultGloves = _gameDataService.GetDefaultItem(gender, CostumeSlot.Gloves, gloves);
            if (defaultGloves == null)
            {
                logger.Warning("Invalid gloves");
                return (null, CharacterCreateResult.InvalidDefaultItem);
            }

            var defaultShoes = _gameDataService.GetDefaultItem(gender, CostumeSlot.Shoes, shoes);
            if (defaultShoes == null)
            {
                logger.Warning("Invalid shoes");
                return (null, CharacterCreateResult.InvalidDefaultItem);
            }

            var character = new Character(this, _idGeneratorService.GetNextId(IdKind.Character),
                slot, gender, defaultHair, defaultFace, defaultShirt, defaultPants, defaultGloves, defaultShoes);
            _characters.Add(slot, character);

            var charStyle = new CharacterStyle(character.Gender, character.Slot,
                character.Hair.Variation, character.Face.Variation,
                character.Shirt.Variation, character.Pants.Variation);
            Player.Session.Send(new CSuccessCreateCharacterAckMessage(character.Slot, charStyle));

            return (character, CharacterCreateResult.Success);
        }

        /// <summary>
        /// Selects the character on the given slot
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public bool Select(byte slot)
        {
            if (!Contains(slot))
                return false;

            if (CurrentSlot != slot)
                Player.SetDirtyState(true);

            CurrentSlot = slot;
            Player.Session.Send(new CharacterSelectAckMessage(CurrentSlot));
            return true;
        }

        /// <summary>
        /// Removes the character
        /// </summary>
        public void Remove(Character character)
        {
            Remove(character.Slot);
        }

        /// <summary>
        /// Removes the character on the given slot
        /// </summary>
        public bool Remove(byte slot)
        {
            var character = GetCharacter(slot);
            if (character == null)
                return false;

            _characters.Remove(slot);
            if (character.Exists)
                _charactersToRemove.Push(character);

            character.Weapons.Clear();
            character.Skills.Clear();
            character.Costumes.Clear();
            Player.Session.Send(new CharacterDeleteAckMessage(slot));
            return true;
        }

        /// <summary>
        /// Captures the character set's unsaved state into a snapshot (pure read — does not
        /// mutate the live characters). New and dirty-existing characters are included so the
        /// writer can reproduce the old <c>Save</c> exactly; unchanged existing ones are skipped.
        /// </summary>
        public void BuildSnapshot(PlayerSaveSnapshot s)
        {
            foreach (var characterToRemove in _charactersToRemove)
                s.CharacterIdsToRemove.Add(characterToRemove.Id);

            foreach (var character in _characters.Values)
            {
                if (character.Exists && !character.IsDirty)
                    continue;

                var sc = new SnapshotCharacter
                {
                    Id = character.Id,
                    Slot = character.Slot,
                    Gender = (byte)character.Gender,
                    BasicHair = character.Hair.Variation,
                    BasicFace = character.Face.Variation,
                    BasicShirt = character.Shirt.Variation,
                    BasicPants = character.Pants.Variation,
                    Exists = character.Exists
                };
                ApplyEquipToSnapshot(character, sc);
                s.Characters.Add(sc);
            }
        }

        /// <summary>
        /// Marks the captured snapshot as persisted: drains the remove-stack, flags new
        /// characters as existing, and clears character dirty flags. Called only after the
        /// snapshot was durably enqueued (or written directly), so a failed publish leaves state
        /// dirty for retry.
        /// </summary>
        public void ClearPendingChanges()
        {
            while (_charactersToRemove.TryPop(out _)) { }

            foreach (var character in _characters.Values)
            {
                if (!character.Exists)
                    character.SetExistsState(true);
                else
                    character.SetDirtyState(false);
            }
        }

        public bool Contains(byte slot)
        {
            return _characters.ContainsKey(slot);
        }

        public IEnumerator<Character> GetEnumerator()
        {
            return _characters.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private static void ApplyEquipToSnapshot(Character character, SnapshotCharacter sc)
        {
            // Weapons
            var items = character.Weapons.GetItems();
            if (items.Length > 0) sc.Weapon1Id = (long?)items[0]?.Id;
            if (items.Length > 1) sc.Weapon2Id = (long?)items[1]?.Id;
            if (items.Length > 2) sc.Weapon3Id = (long?)items[2]?.Id;

            // Skills (matches the legacy mapping, which reads index 0 unconditionally)
            items = character.Skills.GetItems();
            sc.SkillId = items.Length > 0 ? (long?)items[0]?.Id : null;

            // Costumes
            items = character.Costumes.GetItems();
            for (var slot = 0; slot < items.Length; ++slot)
            {
                var itemId = (long?)items[slot]?.Id;

                switch ((CostumeSlot)slot)
                {
                    case CostumeSlot.Hair:
                        sc.HairId = itemId;
                        break;

                    case CostumeSlot.Face:
                        sc.FaceId = itemId;
                        break;

                    case CostumeSlot.Shirt:
                        sc.ShirtId = itemId;
                        break;

                    case CostumeSlot.Pants:
                        sc.PantsId = itemId;
                        break;

                    case CostumeSlot.Gloves:
                        sc.GlovesId = itemId;
                        break;

                    case CostumeSlot.Shoes:
                        sc.ShoesId = itemId;
                        break;

                    case CostumeSlot.Accessory:
                        sc.AccessoryId = itemId;
                        break;

                    case CostumeSlot.Pet:
                        sc.PetId = itemId;
                        break;
                }
            }
        }
    }
}
