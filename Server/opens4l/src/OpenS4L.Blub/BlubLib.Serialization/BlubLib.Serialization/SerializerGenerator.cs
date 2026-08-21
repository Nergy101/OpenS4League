using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using OpenS4L.Blub.Reflection;
using Sigil;
using Sigil.NonGeneric;

namespace OpenS4L.Blub.Serialization;

internal class SerializerGenerator
{
	private static readonly MethodInfo s_getTypeFromHandleMethod = ReflectionHelper.GetMethod(() => Type.GetTypeFromHandle(default(RuntimeTypeHandle)));

	private readonly BlubSerializer _serializer;

	private readonly Descriptor _descriptor;

	private TypeBuilder _typeBuilder;

	public IList<GeneratedField> Fields { get; }

	public SerializerGenerator(BlubSerializer serializer, Descriptor descriptor)
	{
		if (serializer == null)
		{
			throw new ArgumentNullException("serializer");
		}
		if (descriptor == null)
		{
			throw new ArgumentNullException("descriptor");
		}
		_serializer = serializer;
		_descriptor = descriptor;
		Fields = new List<GeneratedField>();
	}

	public ISerializer Generate()
	{
		if (_descriptor.Serializer != null)
		{
			return _descriptor.Serializer;
		}
		_typeBuilder = TypeBuilderFactory.Create(_descriptor.Type.FullName);
		_typeBuilder.AddInterfaceImplementation(typeof(ISerializer<>).MakeGenericType(_descriptor.Type));
		GenerateSerialize(_typeBuilder);
		GenerateDeserialize(_typeBuilder);
		GenerateCanHandle(_typeBuilder);
		GenerateConstructor(_typeBuilder);
		TypeInfo type = _typeBuilder.CreateTypeInfo();
		object[] args = ((IEnumerable<GeneratedField>)Fields).Select((Func<GeneratedField, object>)((GeneratedField f) => f.Serializer)).ToArray();
		return (ISerializer)Activator.CreateInstance(type, args);
	}

	public GeneratedField CreateField(ISerializer serializer)
	{
		GeneratedField generatedField = Fields.FirstOrDefault((GeneratedField x) => x.Serializer == serializer);
		if (generatedField == null)
		{
			generatedField = new GeneratedField(_typeBuilder.DefineField($"_{Guid.NewGuid():N}", serializer.GetType(), FieldAttributes.Private | FieldAttributes.Static), serializer);
			Fields.Add(generatedField);
		}
		return generatedField;
	}

	private void GenerateConstructor(TypeBuilder typeBuilder)
	{
		Emit emit = Emit.BuildConstructor(Fields.Select((GeneratedField f) => f.Serializer.GetType()).ToArray(), typeBuilder, MethodAttributes.Public);
		for (int num = 0; num < Fields.Count; num++)
		{
			GeneratedField generatedField = Fields[num];
			emit.LoadArgument((ushort)(num + 1));
			emit.StoreField(generatedField.FieldBuilder);
		}
		emit.Return();
		emit.CreateConstructor();
	}

	private void GenerateSerialize(TypeBuilder typeBuilder)
	{
		Emit emit = Emit.BuildInstanceMethod(typeof(void), new Type[3]
		{
			typeof(BlubSerializer),
			typeof(BinaryWriter),
			_descriptor.Type
		}, typeBuilder, "Serialize", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.VtableLayoutMask);
		CompilerContext compilerContext = new CompilerContext(_serializer, emit, this);
		using (Local local = emit.DeclareLocal(_descriptor.Type, "objectToSerialize"))
		{
			emit.LoadValueParam();
			emit.StoreLocal(local);
			if (_descriptor.Compiler != null)
			{
				_descriptor.Compiler.EmitSerialize(compilerContext, local);
			}
			else if (_descriptor.Serializer != null)
			{
				compilerContext.EmitSerialize(_descriptor.Serializer, local);
			}
			else
			{
				foreach (Descriptor item in _descriptor.GetTree())
				{
					foreach (MemberDescriptor value in item.Members.Values)
					{
						Type type = value.Type;
						Sigil.Label label = emit.DefineLabel();
						foreach (MethodInfo beforeSerializeMethod in item.BeforeSerializeMethods)
						{
							emit.LoadLocal(local);
							emit.LoadBlubSerializer();
							emit.LoadReaderOrWriterParam();
							emit.LoadConstant(value.Name);
							emit.Call(beforeSerializeMethod);
							emit.BranchIfTrue(label);
						}
						using (Local local2 = emit.DeclareLocal(type, "value"))
						{
							if (_descriptor.Type.IsValueType)
							{
								emit.LoadLocalAddress(local);
							}
							else
							{
								emit.LoadLocal(local);
							}
							MemberDescriptor memberDescriptor = value;
							if (memberDescriptor != null)
							{
								if (!(memberDescriptor is PropertyDescriptor propertyDescriptor))
								{
									if (memberDescriptor is FieldDescriptor fieldDescriptor)
									{
										FieldDescriptor fieldDescriptor2 = fieldDescriptor;
										emit.LoadField(fieldDescriptor2.FieldInfo);
									}
								}
								else
								{
									PropertyDescriptor propertyDescriptor2 = propertyDescriptor;
									emit.Call(propertyDescriptor2.PropertyInfo.GetMethod);
								}
							}
							emit.StoreLocal(local2);
							if (value.Serializer != null)
							{
								compilerContext.EmitSerialize(value.Serializer, local2);
							}
							else if (value.Compiler != null)
							{
								value.Compiler.EmitSerialize(compilerContext, local2);
							}
						}
						foreach (MethodInfo afterSerializeMethod in item.AfterSerializeMethods)
						{
							emit.LoadLocal(local);
							emit.LoadBlubSerializer();
							emit.LoadReaderOrWriterParam();
							emit.LoadConstant(value.Name);
							emit.Call(afterSerializeMethod);
						}
						emit.MarkLabel(label);
					}
				}
			}
		}
		emit.Return();
		MethodBuilder methodInfoBody = emit.CreateMethod();
		Type type2 = typeof(ISerializer<>).MakeGenericType(_descriptor.Type);
		typeBuilder.DefineMethodOverride(methodInfoBody, type2.GetMethod("Serialize"));
	}

	private void GenerateDeserialize(TypeBuilder typeBuilder)
	{
		Emit emit = Emit.BuildInstanceMethod(_descriptor.Type, new Type[2]
		{
			typeof(BlubSerializer),
			typeof(BinaryReader)
		}, typeBuilder, "Deserialize", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.VtableLayoutMask);
		CompilerContext compilerContext = new CompilerContext(_serializer, emit, this);
		using (Local local = emit.DeclareLocal(_descriptor.Type, "objectToDeserialize"))
		{
			if (_descriptor.Compiler != null)
			{
				_descriptor.Compiler.EmitDeserialize(compilerContext, local);
			}
			else if (_descriptor.Serializer != null)
			{
				compilerContext.EmitDeserialize(_descriptor.Serializer, local);
			}
			else
			{
				if (_descriptor.Type.IsValueType)
				{
					emit.LoadLocalAddress(local);
					emit.InitializeObject(_descriptor.Type);
				}
				else
				{
					emit.NewObject(_descriptor.Type);
					emit.StoreLocal(local);
				}
				foreach (Descriptor item in _descriptor.GetTree())
				{
					foreach (MemberDescriptor value in item.Members.Values)
					{
						Type type = value.Type;
						Sigil.Label label = emit.DefineLabel();
						foreach (MethodInfo beforeDeserializeMethod in item.BeforeDeserializeMethods)
						{
							emit.LoadLocal(local);
							emit.LoadBlubSerializer();
							emit.LoadReaderOrWriterParam();
							emit.LoadConstant(value.Name);
							emit.Call(beforeDeserializeMethod);
							emit.BranchIfTrue(label);
						}
						using (Local local2 = emit.DeclareLocal(type, "value"))
						{
							if (value.Serializer != null)
							{
								compilerContext.EmitDeserialize(value.Serializer, local2);
							}
							else if (value.Compiler != null)
							{
								value.Compiler.EmitDeserialize(compilerContext, local2);
							}
							if (_descriptor.Type.IsValueType)
							{
								emit.LoadLocalAddress(local);
							}
							else
							{
								emit.LoadLocal(local);
							}
							emit.LoadLocal(local2);
							MemberDescriptor memberDescriptor = value;
							if (memberDescriptor != null)
							{
								if (!(memberDescriptor is PropertyDescriptor propertyDescriptor))
								{
									if (memberDescriptor is FieldDescriptor fieldDescriptor)
									{
										FieldDescriptor fieldDescriptor2 = fieldDescriptor;
										emit.StoreField(fieldDescriptor2.FieldInfo);
									}
								}
								else
								{
									PropertyDescriptor propertyDescriptor2 = propertyDescriptor;
									emit.Call(propertyDescriptor2.PropertyInfo.SetMethod);
								}
							}
						}
						foreach (MethodInfo afterDeserializeMethod in item.AfterDeserializeMethods)
						{
							emit.LoadLocal(local);
							emit.LoadBlubSerializer();
							emit.LoadReaderOrWriterParam();
							emit.LoadConstant(value.Name);
							emit.Call(afterDeserializeMethod);
						}
						emit.MarkLabel(label);
					}
				}
			}
			emit.LoadLocal(local);
			emit.Return();
		}
		MethodBuilder methodInfoBody = emit.CreateMethod();
		Type type2 = typeof(ISerializer<>).MakeGenericType(_descriptor.Type);
		typeBuilder.DefineMethodOverride(methodInfoBody, type2.GetMethod("Deserialize"));
	}

	private void GenerateCanHandle(TypeBuilder typeBuilder)
	{
		Emit<Func<Type, bool>> emit = Emit<Func<Type, bool>>.BuildInstanceMethod(typeBuilder, "CanHandle", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.VtableLayoutMask);
		emit.LoadArgument(1);
		emit.LoadConstant(_descriptor.Type);
		emit.Call(s_getTypeFromHandleMethod);
		emit.CompareEqual();
		emit.Return();
		MethodBuilder methodInfoBody = emit.CreateMethod();
		typeBuilder.DefineMethodOverride(methodInfoBody, typeof(ISerializer).GetMethod("CanHandle"));
	}
}
