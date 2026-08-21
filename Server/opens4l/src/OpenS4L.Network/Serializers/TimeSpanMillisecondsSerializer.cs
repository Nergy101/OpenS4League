using System;
using System.IO;
using System.Runtime.CompilerServices;
using OpenS4L.Blub.Serialization;

namespace OpenS4L.Network.Serializers
{
    public class TimeSpanMillisecondsSerializer : ISerializer<TimeSpan>
    {
        public bool CanHandle(Type type)
        {
            return typeof(TimeSpan) == type;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Serialize(BlubSerializer blubSerializer, BinaryWriter writer, TimeSpan value)
        {
            writer.Write((uint)value.TotalMilliseconds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSpan Deserialize(BlubSerializer blubSerializer, BinaryReader reader)
        {
            return TimeSpan.FromMilliseconds(reader.ReadUInt32());
        }
    }
}
