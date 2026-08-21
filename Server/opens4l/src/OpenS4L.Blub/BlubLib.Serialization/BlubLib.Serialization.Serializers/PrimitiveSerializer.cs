using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OpenS4L.Blub.Reflection;
using Sigil;

namespace OpenS4L.Blub.Serialization.Serializers;

internal class PrimitiveSerializer : ISerializerCompiler
{
	private static readonly IReadOnlyDictionary<Type, (MethodInfo read, MethodInfo write)> s_primitiveMethods;

	private readonly Type _primitivetype;

	private readonly MethodInfo _writeMethod;

	private readonly MethodInfo _readMethod;

	static PrimitiveSerializer()
	{
		s_primitiveMethods = new Dictionary<Type, (MethodInfo, MethodInfo)>
		{
			[typeof(ulong)] = (ReflectionHelper.GetMethod((BinaryReader _) => _.ReadUInt64()), ReflectionHelper.GetMethod((BinaryWriter _) => _.Write(0uL))),
			[typeof(uint)] = (ReflectionHelper.GetMethod((BinaryReader _) => _.ReadUInt32()), ReflectionHelper.GetMethod((BinaryWriter _) => _.Write(0u))),
			[typeof(ushort)] = (ReflectionHelper.GetMethod((BinaryReader _) => _.ReadUInt16()), ReflectionHelper.GetMethod((BinaryWriter _) => _.Write((ushort)0))),
			[typeof(byte)] = (ReflectionHelper.GetMethod((BinaryReader _) => _.ReadByte()), ReflectionHelper.GetMethod((BinaryWriter _) => _.Write((byte)0))),
			[typeof(long)] = (ReflectionHelper.GetMethod((BinaryReader _) => _.ReadInt64()), ReflectionHelper.GetMethod((BinaryWriter _) => _.Write(0L))),
			[typeof(int)] = (ReflectionHelper.GetMethod((BinaryReader _) => _.ReadInt32()), ReflectionHelper.GetMethod((BinaryWriter _) => _.Write(0))),
			[typeof(short)] = (ReflectionHelper.GetMethod((BinaryReader _) => _.ReadInt16()), ReflectionHelper.GetMethod((BinaryWriter _) => _.Write((short)0))),
			[typeof(sbyte)] = (ReflectionHelper.GetMethod((BinaryReader _) => _.ReadSByte()), ReflectionHelper.GetMethod((BinaryWriter _) => _.Write((sbyte)0))),
			[typeof(decimal)] = (ReflectionHelper.GetMethod((BinaryReader _) => _.ReadDecimal()), ReflectionHelper.GetMethod((BinaryWriter _) => _.Write(0m))),
			[typeof(double)] = (ReflectionHelper.GetMethod((BinaryReader _) => _.ReadDouble()), ReflectionHelper.GetMethod((BinaryWriter _) => _.Write(0.0))),
			[typeof(float)] = (ReflectionHelper.GetMethod((BinaryReader _) => _.ReadSingle()), ReflectionHelper.GetMethod((BinaryWriter _) => _.Write(0f))),
			[typeof(char)] = (ReflectionHelper.GetMethod((BinaryReader _) => _.ReadChar()), ReflectionHelper.GetMethod((BinaryWriter _) => _.Write('\0'))),
			[typeof(bool)] = (ReflectionHelper.GetMethod((BinaryReader _) => _.ReadBoolean()), ReflectionHelper.GetMethod((BinaryWriter _) => _.Write(value: false))),
			[typeof(string)] = (ReflectionHelper.GetMethod((BinaryReader _) => _.ReadString()), ReflectionHelper.GetMethod((BinaryWriter _) => _.Write((string)null)))
		};
	}

	public PrimitiveSerializer(Type primitiveType)
	{
		if (!s_primitiveMethods.TryGetValue(primitiveType, out (MethodInfo, MethodInfo) value))
		{
			throw new ArgumentException("No Read/Write methods found for the given type", "primitiveType");
		}
		_primitivetype = primitiveType;
		_writeMethod = value.Item2;
		(_readMethod, _) = value;
	}

	public bool CanHandle(Type type)
	{
		return type == _primitivetype;
	}

	public void EmitSerialize(CompilerContext ctx, Local value)
	{
		ctx.Emit.LoadReaderOrWriterParam();
		ctx.Emit.LoadLocal(value);
		ctx.Emit.CallVirtual(_writeMethod);
	}

	public void EmitDeserialize(CompilerContext ctx, Local value)
	{
		ctx.Emit.LoadReaderOrWriterParam();
		ctx.Emit.CallVirtual(_readMethod);
		ctx.Emit.StoreLocal(value);
	}
}
