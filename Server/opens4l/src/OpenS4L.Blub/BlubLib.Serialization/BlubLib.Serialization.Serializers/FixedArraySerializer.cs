using System;
using System.IO;
using OpenS4L.Blub.Reflection;
using Sigil;

namespace OpenS4L.Blub.Serialization.Serializers;

public class FixedArraySerializer : ISerializerCompiler
{
	private readonly int _length;

	public FixedArraySerializer(int length)
	{
		if (length < 0)
		{
			throw new ArgumentOutOfRangeException("length");
		}
		_length = length;
	}

	public bool CanHandle(Type type)
	{
		return type.IsArray;
	}

	public void EmitDeserialize(CompilerContext ctx, Local value)
	{
		Type elementType = value.LocalType.GetElementType();
		if (_length <= 0)
		{
			ctx.Emit.Call(typeof(Array).GetMethod("Empty").GetGenericMethodDefinition().MakeGenericMethod(elementType));
			ctx.Emit.StoreLocal(value);
			return;
		}
		if (elementType == typeof(byte))
		{
			ctx.Emit.LoadReaderOrWriterParam();
			ctx.Emit.LoadConstant(_length);
			ctx.Emit.CallVirtual(ReflectionHelper.GetMethod((BinaryReader _) => _.ReadBytes(0)));
			ctx.Emit.StoreLocal(value);
			return;
		}
		Label label = ctx.Emit.DefineLabel();
		Label label2 = ctx.Emit.DefineLabel();
		ctx.Emit.LoadConstant(_length);
		ctx.Emit.NewArray(elementType);
		ctx.Emit.StoreLocal(value);
		using Local local = ctx.Emit.DeclareLocal(elementType, "element");
		using Local local2 = ctx.Emit.DeclareLocal<int>("i");
		ctx.Emit.MarkLabel(label);
		ctx.EmitDeserialize(local);
		ctx.Emit.LoadLocal(value);
		ctx.Emit.LoadLocal(local2);
		ctx.Emit.LoadLocal(local);
		ctx.Emit.StoreElement(elementType);
		ctx.Emit.LoadLocal(local2);
		ctx.Emit.LoadConstant(1);
		ctx.Emit.Add();
		ctx.Emit.StoreLocal(local2);
		ctx.Emit.MarkLabel(label2);
		ctx.Emit.LoadLocal(local2);
		ctx.Emit.LoadConstant(_length);
		ctx.Emit.BranchIfLess(label);
	}

	public void EmitSerialize(CompilerContext ctx, Local value)
	{
		Type elementType = value.LocalType.GetElementType();
		if (_length <= 0)
		{
			return;
		}
		if (elementType == typeof(byte))
		{
			ctx.Emit.LoadReaderOrWriterParam();
			ctx.Emit.LoadLocal(value);
			ctx.Emit.CallVirtual(ReflectionHelper.GetMethod((BinaryWriter _) => _.Write((byte[])null)));
			return;
		}
		Label label = ctx.Emit.DefineLabel();
		Label label2 = ctx.Emit.DefineLabel();
		using Local local = ctx.Emit.DeclareLocal(elementType, "element");
		using Local local2 = ctx.Emit.DeclareLocal<int>("i");
		ctx.Emit.Branch(label2);
		ctx.Emit.MarkLabel(label);
		ctx.Emit.LoadLocal(value);
		ctx.Emit.LoadLocal(local2);
		ctx.Emit.LoadElement(elementType);
		ctx.Emit.StoreLocal(local);
		ctx.EmitSerialize(local);
		ctx.Emit.LoadLocal(local2);
		ctx.Emit.LoadConstant(1);
		ctx.Emit.Add();
		ctx.Emit.StoreLocal(local2);
		ctx.Emit.MarkLabel(label2);
		ctx.Emit.LoadLocal(local2);
		ctx.Emit.LoadConstant(_length);
		ctx.Emit.BranchIfLess(label);
	}
}
