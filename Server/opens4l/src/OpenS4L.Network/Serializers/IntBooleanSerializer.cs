using System;
using System.IO;
using OpenS4L.Blub.Reflection;
using OpenS4L.Blub.Serialization;
using Sigil;

namespace OpenS4L.Network.Serializers
{
    /// <summary>
    /// Serializes <see cref="bool"/> as a <see cref="int"/>. True is serialized as 1 and false as 0
    /// </summary>
    public class IntBooleanSerializer : ISerializerCompiler
    {
        public bool CanHandle(Type type)
        {
            return type == typeof(bool);
        }

        public void EmitSerialize(CompilerContext context, Local value)
        {
            context.Emit.LoadReaderOrWriterParam();
            context.Emit.LoadLocal(value);
            context.Emit.LoadConstant(true);
            context.Emit.CompareEqual();
            context.Emit.CallVirtual(ReflectionHelper.GetMethod((BinaryWriter _) => _.Write(default(int))));
        }

        public void EmitDeserialize(CompilerContext context, Local value)
        {
            context.Emit.LoadReaderOrWriterParam();
            context.Emit.CallVirtual(ReflectionHelper.GetMethod((BinaryReader _) => _.ReadInt32()));
            context.Emit.Convert<bool>();
            context.Emit.StoreLocal(value);
        }
    }
}
