using System;
using System.IO;
using OpenS4L.Blub.Serialization;

namespace OpenS4L.Network.Serializers
{
    public class UnixTimeSerializer : ISerializer<DateTimeOffset>
    {
        public bool CanHandle(Type type)
        {
            return typeof(DateTimeOffset) == type;
        }

        public void Serialize(BlubSerializer blubSerializer, BinaryWriter writer, DateTimeOffset value)
        {
            writer.Write(value.ToUnixTimeSeconds());
        }

        public DateTimeOffset Deserialize(BlubSerializer blubSerializer, BinaryReader reader)
        {
            return DateTimeOffset.FromUnixTimeSeconds(reader.ReadInt64());
        }
    }
}
