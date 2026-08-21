using System;

namespace OpenS4L.Blub.Serialization;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public class BlubIgnoreAttribute : Attribute
{
}
