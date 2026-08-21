using System;
using OpenS4L.Blub.Serialization;
using OpenS4L.Blub.Serialization.Serializers;

namespace OpenS4L.Network.Data.GameRule
{
    [BlubContract]
    public class EquipCheckDto
    {
        [BlubMember(0)]
        [BlubSerializer(typeof(FixedArraySerializer), 3)]
        public ItemCheckDto[] Weapons { get; set; }

        [BlubMember(1)]
        public ItemCheckDto Skill { get; set; }

        [BlubMember(2)]
        [BlubSerializer(typeof(FixedArraySerializer), 8)]
        public ItemCheckDto[] Costumes { get; set; }

        [BlubMember(3)]
        public uint MovementSpeed { get; set; }

        public EquipCheckDto()
        {
            Weapons = Array.Empty<ItemCheckDto>();
            Skill = new ItemCheckDto();
            Costumes = Array.Empty<ItemCheckDto>();
            MovementSpeed = 1100;
        }
    }
}
