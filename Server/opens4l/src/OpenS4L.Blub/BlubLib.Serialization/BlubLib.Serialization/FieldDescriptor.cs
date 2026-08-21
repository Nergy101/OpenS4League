using System.Reflection;

namespace OpenS4L.Blub.Serialization;

internal class FieldDescriptor : MemberDescriptor
{
	public FieldInfo FieldInfo { get; set; }
}
