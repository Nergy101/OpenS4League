using System.Runtime.CompilerServices;

namespace OpenS4L.Blub;

public static class AttachedPropertiesExtensions
{
	private static readonly ConditionalWeakTable<object, AttachedProperties> s_properties = new ConditionalWeakTable<object, AttachedProperties>();

	public static AttachedProperties GetAttachedProperties(this object obj)
	{
		return s_properties.GetValue(obj, (object _) => new AttachedProperties());
	}

	public static object GetProperty(this object obj, string key)
	{
		return obj.GetAttachedProperties()[key];
	}

	public static T GetProperty<T>(this object obj, string key)
	{
		return DynamicCast<T>.From(obj.GetAttachedProperties()[key]);
	}

	public static void SetProperty(this object obj, string key, object value)
	{
		obj.GetAttachedProperties()[key] = value;
	}
}
