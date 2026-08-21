using System;
using System.IO;
using System.Reflection;
using OpenS4L.Blub.IO;
using OpenS4L.Blub.Reflection;
using Sigil;

namespace OpenS4L.Blub.Serialization.Serializers;

public class CStringSerializer : ISerializerCompiler
{
	private static readonly MethodInfo s_writeMethod = ReflectionHelper.GetMethod(() => ((BinaryWriter)null).WriteCString(null));

	private static readonly MethodInfo s_writeMethod2 = ReflectionHelper.GetMethod(() => ((BinaryWriter)null).WriteCString(null, 0));

	private static readonly MethodInfo s_readMethod = ReflectionHelper.GetMethod(() => ((BinaryReader)null).ReadCString());

	private static readonly MethodInfo s_readMethod2 = ReflectionHelper.GetMethod(() => ((BinaryReader)null).ReadCString(0));

	private readonly int _length;

	public CStringSerializer()
	{
	}

	public CStringSerializer(int length)
	{
		if (length < 1)
		{
			throw new ArgumentOutOfRangeException("length");
		}
		_length = length;
	}

	public bool CanHandle(Type type)
	{
		return type == typeof(string);
	}

	public void EmitSerialize(CompilerContext ctx, Local value)
	{
		ctx.Emit.LoadReaderOrWriterParam();
		ctx.Emit.LoadLocal(value);
		if (_length > 0)
		{
			ctx.Emit.LoadConstant(_length);
			ctx.Emit.Call(s_writeMethod2);
		}
		else
		{
			ctx.Emit.Call(s_writeMethod);
		}
	}

	public void EmitDeserialize(CompilerContext ctx, Local value)
	{
		ctx.Emit.LoadReaderOrWriterParam();
		if (_length > 0)
		{
			ctx.Emit.LoadConstant(_length);
			ctx.Emit.Call(s_readMethod2);
		}
		else
		{
			ctx.Emit.Call(s_readMethod);
		}
		ctx.Emit.StoreLocal(value);
	}
}
