using System;
using Sigil;

namespace OpenS4L.Blub.Serialization.Serializers;

public class EnumSerializer : ISerializerCompiler
{
	private readonly Type _serializeAsType;

	public EnumSerializer()
	{
	}

	public EnumSerializer(Type serializeAsType)
	{
		TypeCode typeCode = serializeAsType.GetTypeCode();
		if ((uint)(typeCode - 5) > 7u)
		{
			throw new ArgumentException("Supported types are int8, uint8, int16, uint16, int32, uint32, int64, uint64");
		}
		_serializeAsType = serializeAsType;
	}

	public bool CanHandle(Type type)
	{
		return type.IsEnum;
	}

	public void EmitDeserialize(CompilerContext ctx, Local value)
	{
		Type enumUnderlyingType = value.LocalType.GetEnumUnderlyingType();
		Type type = _serializeAsType ?? enumUnderlyingType;
		using Local local = ctx.Emit.DeclareLocal(type);
		ctx.EmitDeserialize(local);
		ctx.Emit.LoadLocal(local);
		if (enumUnderlyingType != type)
		{
			ctx.Emit.Convert(enumUnderlyingType);
		}
		ctx.Emit.StoreLocal(value);
	}

	public void EmitSerialize(CompilerContext ctx, Local value)
	{
		Type enumUnderlyingType = value.LocalType.GetEnumUnderlyingType();
		Type type = _serializeAsType ?? enumUnderlyingType;
		using Local local = ctx.Emit.DeclareLocal(type);
		ctx.Emit.LoadLocal(value);
		if (enumUnderlyingType != type)
		{
			ctx.Emit.Convert(enumUnderlyingType);
		}
		ctx.Emit.StoreLocal(local);
		ctx.EmitSerialize(local);
	}
}
