using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenS4L.Blub.Collections.Concurrent;
using OpenS4L.Blub.Collections.Generic;
using OpenS4L.Blub.IO;
using OpenS4L.Blub.Reflection;
using OpenS4L.Blub.Serialization.Serializers;
using Sigil;

namespace OpenS4L.Blub.Serialization;

public class BlubSerializer
{
	private static readonly MethodInfo s_serializeWithWriterMethod;

	private static readonly MethodInfo s_serializeWithStreamMethod;

	private static readonly MethodInfo s_deserializeWithReaderMethod;

	private static readonly MethodInfo s_deserializeWithStreamMethod;

	private readonly TypeModel _typeModel;

	private readonly IList<ISerializer> _serializers = new List<ISerializer>();

	private readonly IList<ISerializerCompiler> _compilers = new List<ISerializerCompiler>();

	private readonly IReadOnlyDictionary<Type, ISerializerCompiler> _primitiveCompilers;

	private readonly ConcurrentDictionary<Type, Action<BlubSerializer, BinaryWriter, object>> _serializeWithWriterCache = new ConcurrentDictionary<Type, Action<BlubSerializer, BinaryWriter, object>>();

	private readonly ConcurrentDictionary<Type, Action<BlubSerializer, Stream, object>> _serializeWithStreamCache = new ConcurrentDictionary<Type, Action<BlubSerializer, Stream, object>>();

	private readonly ConcurrentDictionary<Type, Func<BlubSerializer, BinaryReader, object>> _deserializeWithReaderCache = new ConcurrentDictionary<Type, Func<BlubSerializer, BinaryReader, object>>();

	private readonly ConcurrentDictionary<Type, Func<BlubSerializer, Stream, object>> _deserializeWithStreamCache = new ConcurrentDictionary<Type, Func<BlubSerializer, Stream, object>>();

	public static BlubSerializer Instance { get; }

	static BlubSerializer()
	{
		Instance = new BlubSerializer();
		s_serializeWithWriterMethod = ReflectionHelper.GetMethod((BlubSerializer _) => _.Serialize<object>((BinaryWriter)null, (object)null)).GetGenericMethodDefinition();
		s_serializeWithStreamMethod = ReflectionHelper.GetMethod((BlubSerializer _) => _.Serialize<object>((Stream)null, (object)null)).GetGenericMethodDefinition();
		s_deserializeWithReaderMethod = ReflectionHelper.GetMethod((BlubSerializer _) => _.Deserialize<object>((BinaryReader)null)).GetGenericMethodDefinition();
		s_deserializeWithStreamMethod = ReflectionHelper.GetMethod((BlubSerializer _) => _.Deserialize<object>((Stream)null)).GetGenericMethodDefinition();
	}

	public BlubSerializer()
	{
		_typeModel = new TypeModel(this);
		_primitiveCompilers = new Dictionary<Type, ISerializerCompiler>
		{
			{
				typeof(bool),
				new PrimitiveSerializer(typeof(bool))
			},
			{
				typeof(byte),
				new PrimitiveSerializer(typeof(byte))
			},
			{
				typeof(char),
				new PrimitiveSerializer(typeof(char))
			},
			{
				typeof(decimal),
				new PrimitiveSerializer(typeof(decimal))
			},
			{
				typeof(double),
				new PrimitiveSerializer(typeof(double))
			},
			{
				typeof(float),
				new PrimitiveSerializer(typeof(float))
			},
			{
				typeof(short),
				new PrimitiveSerializer(typeof(short))
			},
			{
				typeof(int),
				new PrimitiveSerializer(typeof(int))
			},
			{
				typeof(long),
				new PrimitiveSerializer(typeof(long))
			},
			{
				typeof(sbyte),
				new PrimitiveSerializer(typeof(sbyte))
			},
			{
				typeof(string),
				new PrimitiveSerializer(typeof(string))
			},
			{
				typeof(ushort),
				new PrimitiveSerializer(typeof(ushort))
			},
			{
				typeof(uint),
				new PrimitiveSerializer(typeof(uint))
			},
			{
				typeof(ulong),
				new PrimitiveSerializer(typeof(ulong))
			},
			{
				typeof(Guid),
				new GuidSerializer()
			}
		};
		AddSerializer(new EnumSerializer());
	}

	public void AddSerializer(ISerializerCompiler compiler)
	{
		if (_compilers.Any((ISerializerCompiler x) => x.GetType() == compiler.GetType()))
		{
			throw new ArgumentException("Serializer was already added", "compiler");
		}
		_compilers.Add(compiler);
	}

	public void AddSerializer<T>(ISerializer<T> serializer)
	{
		if (_serializers.Any((ISerializer x) => x.GetType() == serializer.GetType()))
		{
			throw new ArgumentException("Serializer was already added", "serializer");
		}
		_serializers.Add(serializer);
	}

	public ISerializer<T> GetSerializer<T>()
	{
		Type typeFromHandle = typeof(T);
		return (ISerializer<T>)(GetOrCreateSerializer(typeFromHandle) ?? throw new ArgumentException($"{typeFromHandle.FullName} has no properties to serialize"));
	}

	public void Serialize(BinaryWriter writer, object value)
	{
		Type type = value.GetType();
		Action<BlubSerializer, BinaryWriter, object> action = _serializeWithWriterCache.GetValueOrDefault(type);
		if (action != null)
		{
			action(this, writer, value);
			return;
		}
		lock (_serializeWithWriterCache)
		{
			action = _serializeWithWriterCache.GetValueOrDefault(type);
			if (action != null)
			{
				action(this, writer, value);
				return;
			}
			Emit<Action<BlubSerializer, BinaryWriter, object>> emit = Emit<Action<BlubSerializer, BinaryWriter, object>>.NewDynamicMethod();
			MethodInfo method = s_serializeWithWriterMethod.MakeGenericMethod(type);
			EmitSerialize(emit, type, method);
			action = emit.CreateDelegate();
			_serializeWithWriterCache.TryAdd(type, action);
		}
		action(this, writer, value);
	}

	public void Serialize(Stream stream, object value)
	{
		Type type = value.GetType();
		Action<BlubSerializer, Stream, object> action = _serializeWithStreamCache.GetValueOrDefault(type);
		if (action != null)
		{
			action(this, stream, value);
			return;
		}
		lock (_serializeWithStreamCache)
		{
			action = _serializeWithStreamCache.GetValueOrDefault(type);
			if (action != null)
			{
				action(this, stream, value);
				return;
			}
			Emit<Action<BlubSerializer, Stream, object>> emit = Emit<Action<BlubSerializer, Stream, object>>.NewDynamicMethod();
			MethodInfo method = s_serializeWithStreamMethod.MakeGenericMethod(type);
			EmitSerialize(emit, type, method);
			action = emit.CreateDelegate();
			_serializeWithStreamCache.TryAdd(type, action);
		}
		action(this, stream, value);
	}

	public void Serialize<T>(BinaryWriter writer, T value)
	{
		GetSerializer<T>().Serialize(this, writer, value);
	}

	public void Serialize<T>(Stream stream, T value)
	{
		using BinaryWriter writer = stream.ToBinaryWriter(leaveOpen: true);
		GetSerializer<T>().Serialize(this, writer, value);
	}

	public object Deserialize(BinaryReader reader, Type type)
	{
		Func<BlubSerializer, BinaryReader, object> func = _deserializeWithReaderCache.GetValueOrDefault(type);
		if (func != null)
		{
			return func(this, reader);
		}
		lock (_deserializeWithReaderCache)
		{
			func = _deserializeWithReaderCache.GetValueOrDefault(type);
			if (func != null)
			{
				return func(this, reader);
			}
			Emit<Func<BlubSerializer, BinaryReader, object>> emit = Emit<Func<BlubSerializer, BinaryReader, object>>.NewDynamicMethod();
			MethodInfo method = s_deserializeWithReaderMethod.MakeGenericMethod(type);
			EmitDeserialize(emit, type, method);
			func = emit.CreateDelegate();
			_deserializeWithReaderCache.TryAdd(type, func);
		}
		return func(this, reader);
	}

	public object Deserialize(Stream stream, Type type)
	{
		Func<BlubSerializer, Stream, object> func = _deserializeWithStreamCache.GetValueOrDefault(type);
		if (func != null)
		{
			return func(this, stream);
		}
		lock (_deserializeWithStreamCache)
		{
			func = _deserializeWithStreamCache.GetValueOrDefault(type);
			if (func != null)
			{
				return func(this, stream);
			}
			Emit<Func<BlubSerializer, Stream, object>> emit = Emit<Func<BlubSerializer, Stream, object>>.NewDynamicMethod();
			MethodInfo method = s_deserializeWithStreamMethod.MakeGenericMethod(type);
			EmitDeserialize(emit, type, method);
			func = emit.CreateDelegate();
			_deserializeWithStreamCache.TryAdd(type, func);
		}
		return func(this, stream);
	}

	public T Deserialize<T>(BinaryReader reader)
	{
		return GetSerializer<T>().Deserialize(this, reader);
	}

	public T Deserialize<T>(Stream stream)
	{
		using BinaryReader reader = stream.ToBinaryReader(leaveOpen: true);
		return GetSerializer<T>().Deserialize(this, reader);
	}

	internal ISerializer GetOrCreateSerializer(Type type)
	{
		Descriptor descriptor = _typeModel.GetDescriptor(type);
		if (descriptor?.Serializer != null)
		{
			return descriptor.Serializer;
		}
		lock (_serializers)
		{
			descriptor = _typeModel.GetOrCreateDescriptor(type);
			if (descriptor == null)
			{
				return null;
			}
			if (descriptor.Serializer != null)
			{
				return descriptor.Serializer;
			}
			ISerializer serializer = new SerializerGenerator(this, descriptor).Generate();
			if (serializer == null)
			{
				return null;
			}
			descriptor.Serializer = serializer;
			return serializer;
		}
	}

	internal ISerializerCompiler GetCompilerForType(Type type)
	{
		return _compilers.FirstOrDefault((ISerializerCompiler compiler) => compiler.CanHandle(type)) ?? DictionaryExtensions.GetValueOrDefault(_primitiveCompilers, type);
	}

	internal ISerializer GetSerializerForType(Type type)
	{
		return _serializers.FirstOrDefault((ISerializer serializer) => serializer.CanHandle(type));
	}

	private static void EmitSerialize(Emit<Action<BlubSerializer, BinaryWriter, object>> emiter, Type type, MethodInfo method)
	{
		emiter.LoadArgument(0);
		emiter.LoadArgument(1);
		emiter.LoadArgument(2);
		if (type.IsValueType)
		{
			emiter.UnboxAny(type);
		}
		else
		{
			emiter.CastClass(type);
		}
		emiter.Call(method);
		emiter.Return();
	}

	private static void EmitSerialize(Emit<Action<BlubSerializer, Stream, object>> emiter, Type type, MethodInfo method)
	{
		emiter.LoadArgument(0);
		emiter.LoadArgument(1);
		emiter.LoadArgument(2);
		if (type.IsValueType)
		{
			emiter.UnboxAny(type);
		}
		else
		{
			emiter.CastClass(type);
		}
		emiter.Call(method);
		emiter.Return();
	}

	private static void EmitDeserialize(Emit<Func<BlubSerializer, BinaryReader, object>> emiter, Type type, MethodInfo method)
	{
		emiter.LoadArgument(0);
		emiter.LoadArgument(1);
		emiter.Call(method);
		if (type.IsValueType)
		{
			emiter.Box(type);
		}
		else
		{
			emiter.CastClass<object>();
		}
		emiter.Return();
	}

	private static void EmitDeserialize(Emit<Func<BlubSerializer, Stream, object>> emiter, Type type, MethodInfo method)
	{
		emiter.LoadArgument(0);
		emiter.LoadArgument(1);
		emiter.Call(method);
		if (type.IsValueType)
		{
			emiter.Box(type);
		}
		else
		{
			emiter.CastClass<object>();
		}
		emiter.Return();
	}
}
