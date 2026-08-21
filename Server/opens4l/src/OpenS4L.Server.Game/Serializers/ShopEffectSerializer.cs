using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using OpenS4L.Blub.Serialization;
using OpenS4L.Server.Game.Data;
using ProudNet;

namespace OpenS4L.Server.Game.Serializers
{
    internal class ShopEffectSerializer : ISerializer<ImmutableDictionary<int, ShopEffectGroup>>
    {
        public bool CanHandle(Type type)
        {
            return type == typeof(ImmutableDictionary<int, ShopEffectGroup>);
        }

        public void Serialize(BlubSerializer blubSerializer, BinaryWriter writer, ImmutableDictionary<int, ShopEffectGroup> value)
        {
            writer.Write(value.Count);
            foreach (var group in value.Values)
            {
                writer.Write(group.PreviewEffect);

                writer.Write(group.Effects.Count);
                foreach (var effect in group.Effects.OrderBy(x => x.Effect))
                    writer.Write(effect.Effect);
            }
        }

        public ImmutableDictionary<int, ShopEffectGroup> Deserialize(BlubSerializer blubSerializer, BinaryReader reader)
        {
            // This is not needed
            throw new NotSupportedException();
        }
    }
}
