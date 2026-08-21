using System;

namespace OpenS4L.Blub.Serialization;

internal class MemberDescriptor
{
	public string Name { get; set; }

	public Type Type { get; set; }

	public ISerializer Serializer { get; set; }

	public ISerializerCompiler Compiler { get; set; }
}
