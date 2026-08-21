using System;

namespace OpenS4L.Blub.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public class BlubSerializerAttribute : Attribute
{
	public Type SerializerType { get; set; }

	public object[] SerializerParameters { get; set; }

	public BlubSerializerAttribute(Type serializerType, params object[] serializerParameters)
	{
		if (serializerType == null)
		{
			throw new ArgumentNullException("serializerType");
		}
		SerializerType = serializerType;
		SerializerParameters = serializerParameters;
	}
}
