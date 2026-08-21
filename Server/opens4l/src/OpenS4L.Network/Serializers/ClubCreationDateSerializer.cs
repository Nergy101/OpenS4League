using System;
using System.Globalization;
using System.IO;
using OpenS4L.Blub.Serialization;
using ProudNet;

namespace OpenS4L.Network.Serializers
{
    public class ClubCreationDateSerializer : ISerializer<DateTimeOffset>
    {
        public bool CanHandle(Type type)
        {
            return typeof(DateTimeOffset) == type;
        }

        public void Serialize(BlubSerializer blubSerializer, BinaryWriter writer, DateTimeOffset value)
        {
            writer.WriteProudString(value.ToString("yyyyMMddHHmmss"));
        }

        public DateTimeOffset Deserialize(BlubSerializer blubSerializer, BinaryReader reader)
        {
            return DateTimeOffset.ParseExact(reader.ReadProudString(), "yyyyMMddHHmmss", CultureInfo.CurrentCulture);
        }
    }
}
