using System;
using System.IO;
using OpenS4L.Blub.Reflection;
using OpenS4L.Blub.Serialization;
using Sigil;

namespace OpenS4L.Network.Serializers
{
    /// <summary>
    /// Serializes <see cref="PeerId"/> as int16
    /// </summary>
    public class PeerIdSerializer : ISerializerCompiler
    {
        public bool CanHandle(Type type)
        {
            return type == typeof(PeerId);
        }

        public void EmitSerialize(CompilerContext context, Local value)
        {
            // BinaryWriter.Write(value)

            context.Emit.LoadReaderOrWriterParam();
            context.Emit.LoadLocal(value);
            context.Emit.Call(typeof(PeerId).GetMethod("op_Implicit", new[] { typeof(PeerId) }));
            context.Emit.CallVirtual(ReflectionHelper.GetMethod((BinaryWriter _) => _.Write(default(ushort))));
        }

        public void EmitDeserialize(CompilerContext context, Local value)
        {
            // value = BinaryReader.ReadUInt16()

            context.Emit.LoadReaderOrWriterParam();
            context.Emit.CallVirtual(ReflectionHelper.GetMethod((BinaryReader _) => _.ReadUInt16()));
            context.Emit.Call(typeof(PeerId).GetMethod("op_Implicit", new[] { typeof(ushort) }));
            context.Emit.StoreLocal(value);
        }
    }
}
