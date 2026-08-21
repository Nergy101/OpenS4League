using System;
using OpenS4L.Blub.Serialization;
using OpenS4L.Network.Serializers;

namespace OpenS4L.Network.Data.GameRule
{
    [BlubContract]
    public class ArenaSyncDto
    {
        [BlubMember(0)]
        public int Unk1 { get; set; }

        [BlubMember(1)]
        public long Unk2 { get; set; }
    }
}
