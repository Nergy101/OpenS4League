using System;
using System.IO;
using OpenS4L.Blub.Reflection;
using OpenS4L.Blub.Serialization;
using Sigil;

namespace OpenS4L.Network.Serializers
{
    /// <summary>
    /// Serializes <see cref="CharacterStyle"/> as int32
    /// </summary>
    public class CharacterStyleSerializer : ISerializerCompiler
    {
        public bool CanHandle(Type type)
        {
            return type == typeof(CharacterStyle);
        }

        public void EmitSerialize(CompilerContext context, Local value)
        {
            context.Emit.LoadReaderOrWriterParam();
            context.Emit.LoadLocalAddress(value);
            context.Emit.Call(typeof(CharacterStyle).GetProperty(nameof(CharacterStyle.Value)).GetMethod);
            context.Emit.CallVirtual(ReflectionHelper.GetMethod((BinaryWriter _) => _.Write(default(uint))));
        }

        public void EmitDeserialize(CompilerContext context, Local value)
        {
            context.Emit.LoadReaderOrWriterParam();
            context.Emit.CallVirtual(ReflectionHelper.GetMethod((BinaryReader _) => _.ReadUInt32()));
            context.Emit.NewObject<CharacterStyle, uint>();
            context.Emit.StoreLocal(value);
        }
    }
}
