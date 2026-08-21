using System.Reflection;

namespace OpenS4L.Blub.Serialization;

internal class PropertyDescriptor : MemberDescriptor
{
	public PropertyInfo PropertyInfo { get; set; }
}
