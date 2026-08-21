using System;
using Sigil;
using Sigil.NonGeneric;

namespace OpenS4L.Blub.Serialization;

public static class EmitExtensions
{
	public static void LoadBlubSerializer(this Emit @this)
	{
		@this.LoadArgument(1);
	}

	public static void LoadReaderOrWriterParam(this Emit @this)
	{
		@this.LoadArgument(2);
	}

	internal static void LoadValueParam(this Emit @this)
	{
		@this.LoadArgument(3);
	}

	public static void EmitSerialize(this CompilerContext @this, ISerializer serializer, Local value)
	{
		if (serializer == null)
		{
			throw new ArgumentNullException("serializer");
		}
		if (value == null)
		{
			throw new ArgumentNullException("value");
		}
		GeneratedField generatedField = @this.Generator.CreateField(serializer);
		@this.Emit.LoadField(generatedField.FieldBuilder);
		@this.Emit.LoadBlubSerializer();
		@this.Emit.LoadReaderOrWriterParam();
		@this.Emit.LoadLocal(value);
		@this.Emit.CallVirtual(serializer.GetType().GetMethod("Serialize"));
	}

	public static void EmitSerialize(this CompilerContext @this, Local value)
	{
		if (value == null)
		{
			throw new ArgumentNullException("value");
		}
		ISerializerCompiler compilerForType = @this.BlubSerializer.GetCompilerForType(value.LocalType);
		if (compilerForType != null)
		{
			compilerForType.EmitSerialize(@this, value);
			return;
		}
		ISerializer orCreateSerializer = @this.BlubSerializer.GetOrCreateSerializer(value.LocalType);
		if (orCreateSerializer == null)
		{
			throw new ArgumentException($"No serializer for {value.LocalType.FullName} available", "LocalType");
		}
		@this.EmitSerialize(orCreateSerializer, value);
	}

	public static void EmitDeserialize(this CompilerContext @this, ISerializer serializer, Local value)
	{
		if (serializer == null)
		{
			throw new ArgumentNullException("serializer");
		}
		if (value == null)
		{
			throw new ArgumentNullException("value");
		}
		GeneratedField generatedField = @this.Generator.CreateField(serializer);
		@this.Emit.LoadField(generatedField.FieldBuilder);
		@this.Emit.LoadBlubSerializer();
		@this.Emit.LoadReaderOrWriterParam();
		@this.Emit.CallVirtual(serializer.GetType().GetMethod("Deserialize"));
		@this.Emit.StoreLocal(value);
	}

	public static void EmitDeserialize(this CompilerContext @this, Local value)
	{
		if (value == null)
		{
			throw new ArgumentNullException("value");
		}
		ISerializerCompiler compilerForType = @this.BlubSerializer.GetCompilerForType(value.LocalType);
		if (compilerForType != null)
		{
			compilerForType.EmitDeserialize(@this, value);
			return;
		}
		ISerializer orCreateSerializer = @this.BlubSerializer.GetOrCreateSerializer(value.LocalType);
		if (orCreateSerializer == null)
		{
			throw new ArgumentException($"No serializer for {value.LocalType.FullName} available", "LocalType");
		}
		@this.EmitDeserialize(orCreateSerializer, value);
	}
}
