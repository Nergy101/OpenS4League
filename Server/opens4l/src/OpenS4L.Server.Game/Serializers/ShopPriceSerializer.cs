using System;
using System.Collections.Immutable;
using System.IO;
using OpenS4L.Blub.IO;
using OpenS4L.Blub.Serialization;
using OpenS4L.Server.Game.Data;
using ProudNet;

namespace OpenS4L.Server.Game.Serializers
{
    internal class ShopPriceSerializer : ISerializer<ImmutableDictionary<int, ShopPriceGroup>>
    {
        public bool CanHandle(Type type)
        {
            return type == typeof(ImmutableDictionary<int, ShopPriceGroup>);
        }

        public void Serialize(BlubSerializer blubSerializer, BinaryWriter writer, ImmutableDictionary<int, ShopPriceGroup> value)
        {
            writer.Write(value.Count);
            foreach (var group in value.Values)
            {
                writer.WriteProudString(group.Id.ToString());
                writer.WriteEnum(group.PriceType);

                writer.Write(group.Prices.Count);
                foreach (var price in group.Prices)
                {
                    writer.WriteEnum(price.PeriodType);
                    writer.Write(price.Period);
                    writer.Write(price.Price);
                    writer.Write(price.CanRefund);
                    writer.Write(price.Durability);
                    writer.Write(price.IsEnabled);
                }
            }
        }

        public ImmutableDictionary<int, ShopPriceGroup> Deserialize(BlubSerializer blubSerializer, BinaryReader reader)
        {
            // This is not needed
            throw new NotSupportedException();
        }
    }
}
