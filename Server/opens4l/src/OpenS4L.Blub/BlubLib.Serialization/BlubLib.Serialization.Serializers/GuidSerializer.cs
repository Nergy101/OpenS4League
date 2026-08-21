using System;
using System.IO;
using OpenS4L.Blub.Reflection;
using Sigil;

namespace OpenS4L.Blub.Serialization.Serializers;

public class GuidSerializer : ISerializerCompiler
{
	public bool CanHandle(Type type)
	{
		return type == typeof(Guid);
	}

	public void EmitDeserialize(CompilerContext ctx, Local value)
	{
		ctx.Emit.LoadReaderOrWriterParam();
		ctx.Emit.LoadConstant(16);
		ctx.Emit.CallVirtual(ReflectionHelper.GetMethod((BinaryReader _) => _.ReadBytes(0)));
		ctx.Emit.NewObject<Guid, byte[]>();
		ctx.Emit.StoreLocal(value);
	}

	public void EmitSerialize(CompilerContext ctx, Local value)
	{
		ctx.Emit.LoadReaderOrWriterParam();
		ctx.Emit.LoadLocalAddress(value);
		ctx.Emit.Call(ReflectionHelper.GetMethod((Guid _) => _.ToByteArray()));
		ctx.Emit.CallVirtual(ReflectionHelper.GetMethod((BinaryWriter _) => _.Write((byte[])null)));
	}
}
