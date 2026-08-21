using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using OpenS4L.Blub.Collections.Concurrent;

namespace OpenS4L.Blub.Serialization;

internal class TypeModel
{
	private readonly BlubSerializer _serializer;

	private readonly ConcurrentDictionary<Type, Descriptor> _descriptors = new ConcurrentDictionary<Type, Descriptor>();

	public TypeModel(BlubSerializer serializer)
	{
		_serializer = serializer;
	}

	public Descriptor GetDescriptor(Type type)
	{
		return _descriptors.GetValueOrDefault(type);
	}

	public Descriptor GetOrCreateDescriptor(Type type)
	{
		Descriptor descriptor = GetDescriptor(type) ?? CreateDescriptor(type);
		_descriptors.TryAdd(type, descriptor);
		return descriptor;
	}

	private Descriptor CreateDescriptor(Type type)
	{
		Descriptor parent = null;
		if (type.BaseType != typeof(object) && type.BaseType != typeof(ValueType) && type.BaseType != typeof(Enum))
		{
			parent = GetOrCreateDescriptor(type.BaseType);
		}
		BlubContractAttribute customAttribute = type.GetCustomAttribute<BlubContractAttribute>();
		BlubSerializerAttribute customAttribute2 = type.GetCustomAttribute<BlubSerializerAttribute>();
		Descriptor descriptor = new Descriptor
		{
			Type = type,
			Parent = parent
		};
		if (customAttribute2 != null)
		{
			if (typeof(ISerializer).IsAssignableFrom(customAttribute2.SerializerType))
			{
				descriptor.Serializer = (ISerializer)Activator.CreateInstance(customAttribute2.SerializerType, customAttribute2.SerializerParameters);
				return descriptor;
			}
			if (typeof(ISerializerCompiler).IsAssignableFrom(customAttribute2.SerializerType))
			{
				descriptor.Compiler = (ISerializerCompiler)Activator.CreateInstance(customAttribute2.SerializerType, customAttribute2.SerializerParameters);
				return descriptor;
			}
			throw new Exception($"Invalid serializer assigned to {type.FullName}");
		}
		descriptor.Serializer = _serializer.GetSerializerForType(type);
		if (descriptor.Serializer == null)
		{
			descriptor.Compiler = _serializer.GetCompilerForType(type);
		}
		if (descriptor.Serializer != null || descriptor.Compiler != null)
		{
			return descriptor;
		}
		TypeInfo typeInfo = type.GetTypeInfo();
		descriptor.Members = new SortedList<uint, MemberDescriptor>();
		descriptor.BeforeSerializeMethods = new List<MethodInfo>();
		descriptor.AfterSerializeMethods = new List<MethodInfo>();
		descriptor.BeforeDeserializeMethods = new List<MethodInfo>();
		descriptor.AfterDeserializeMethods = new List<MethodInfo>();
		HashSet<uint> hashSet = new HashSet<uint>();
		MemberInfo[] members = typeInfo.GetMembers(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (MemberInfo memberInfo in members)
		{
			PropertyInfo propertyInfo = memberInfo as PropertyInfo;
			FieldInfo fieldInfo = memberInfo as FieldInfo;
			MethodInfo methodInfo = memberInfo as MethodInfo;
			if ((propertyInfo == null && fieldInfo == null && methodInfo == null) || memberInfo.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
			{
				continue;
			}
			if (methodInfo != null)
			{
				Type[] first = (from x in methodInfo.GetParameters()
					select x.ParameterType).ToArray();
				if (memberInfo.GetCustomAttribute<BlubBeforeSerializeAttribute>() != null)
				{
					if (methodInfo.ReturnType != typeof(bool) || !Enumerable.SequenceEqual(first, new Type[3]
					{
						typeof(BlubSerializer),
						typeof(BinaryWriter),
						typeof(string)
					}))
					{
						throw new Exception(string.Format("Methods marked with {0} need the signature bool ({1}, BinaryWriter, string)", "BlubBeforeSerializeAttribute", "BlubSerializer"));
					}
					descriptor.BeforeSerializeMethods.Add(methodInfo);
				}
				if (memberInfo.GetCustomAttribute<BlubAfterSerializeAttribute>() != null)
				{
					if (methodInfo.ReturnType != typeof(void) || !Enumerable.SequenceEqual(first, new Type[3]
					{
						typeof(BlubSerializer),
						typeof(BinaryWriter),
						typeof(string)
					}))
					{
						throw new Exception(string.Format("Methods marked with {0} need the signature void ({1}, BinaryWriter, string)", "BlubAfterSerializeAttribute", "BlubSerializer"));
					}
					descriptor.AfterSerializeMethods.Add(methodInfo);
				}
				if (memberInfo.GetCustomAttribute<BlubBeforeDeserializeAttribute>() != null)
				{
					if (methodInfo.ReturnType != typeof(bool) || !Enumerable.SequenceEqual(first, new Type[3]
					{
						typeof(BlubSerializer),
						typeof(BinaryReader),
						typeof(string)
					}))
					{
						throw new Exception(string.Format("Methods marked with {0} need the signature bool ({1}, BinaryReader, string)", "BlubBeforeDeserializeAttribute", "BlubSerializer"));
					}
					descriptor.BeforeDeserializeMethods.Add(methodInfo);
				}
				if (memberInfo.GetCustomAttribute<BlubAfterDeserializeAttribute>() != null)
				{
					if (methodInfo.ReturnType != typeof(void) || !Enumerable.SequenceEqual(first, new Type[3]
					{
						typeof(BlubSerializer),
						typeof(BinaryReader),
						typeof(string)
					}))
					{
						throw new Exception(string.Format("Methods marked with {0} need the signature void ({1}, BinaryReader, string)", "BlubAfterDeserializeAttribute", "BlubSerializer"));
					}
					descriptor.AfterDeserializeMethods.Add(methodInfo);
				}
			}
			else
			{
				if (memberInfo.GetCustomAttribute<BlubIgnoreAttribute>() != null)
				{
					continue;
				}
				BlubMemberAttribute customAttribute3 = memberInfo.GetCustomAttribute<BlubMemberAttribute>();
				if (customAttribute == null || customAttribute3 != null)
				{
					if (propertyInfo?.PropertyType == memberInfo.DeclaringType || fieldInfo?.FieldType == memberInfo.DeclaringType)
					{
						throw new Exception("The declaring type cant be used as a member");
					}
					if (propertyInfo != null && (!propertyInfo.CanWrite || !propertyInfo.CanRead))
					{
						throw new Exception($"Property({memberInfo.DeclaringType.FullName}.{memberInfo.Name}) needs a getter and setter");
					}
					if ((customAttribute3 == null || !customAttribute3.Order.HasValue) && hashSet.Count > 0)
					{
						throw new Exception("Member order has to be provided on every member if used");
					}
					if (customAttribute3 != null && customAttribute3.Order.HasValue && !hashSet.Add(customAttribute3.Order.Value))
					{
						throw new Exception("Member order has to be unique");
					}
					descriptor.Members.Add((uint)(((int?)customAttribute3?.Order) ?? descriptor.Members.Count), CreateDescriptorFromMember(memberInfo));
				}
			}
		}
		return descriptor;
	}

	private MemberDescriptor CreateDescriptorFromMember(MemberInfo member)
	{
		MemberDescriptor memberDescriptor = null;
		Type type = null;
		if ((object)member != null)
		{
			if (!(member is PropertyInfo propertyInfo))
			{
				if (!(member is FieldInfo fieldInfo))
				{
					goto IL_007f;
				}
				FieldInfo fieldInfo2 = fieldInfo;
				type = fieldInfo2.FieldType;
				memberDescriptor = new FieldDescriptor
				{
					Name = member.Name,
					Type = type,
					FieldInfo = fieldInfo2
				};
			}
			else
			{
				PropertyInfo propertyInfo2 = propertyInfo;
				type = propertyInfo2.PropertyType;
				memberDescriptor = new PropertyDescriptor
				{
					Name = member.Name,
					Type = type,
					PropertyInfo = propertyInfo2
				};
			}
			BlubSerializerAttribute customAttribute = member.GetCustomAttribute<BlubSerializerAttribute>();
			if (customAttribute != null)
			{
				if (typeof(ISerializer).IsAssignableFrom(customAttribute.SerializerType))
				{
					memberDescriptor.Serializer = (ISerializer)Activator.CreateInstance(customAttribute.SerializerType, customAttribute.SerializerParameters);
				}
				else
				{
					if (!typeof(ISerializerCompiler).IsAssignableFrom(customAttribute.SerializerType))
					{
						throw new Exception($"Invalid serializer assigned to {member.DeclaringType.FullName}.{member.Name}");
					}
					memberDescriptor.Compiler = (ISerializerCompiler)Activator.CreateInstance(customAttribute.SerializerType, customAttribute.SerializerParameters);
				}
			}
			else
			{
				memberDescriptor.Compiler = _serializer.GetCompilerForType(type);
				if (memberDescriptor.Compiler == null)
				{
					memberDescriptor.Serializer = _serializer.GetOrCreateSerializer(type);
					if (memberDescriptor.Serializer == null)
					{
						throw new Exception($"No serializer available for {member.DeclaringType.FullName}.{member.Name}");
					}
				}
			}
			return memberDescriptor;
		}
		goto IL_007f;
		IL_007f:
		throw new ArgumentException("Only property or field members are supported", "member");
	}
}
