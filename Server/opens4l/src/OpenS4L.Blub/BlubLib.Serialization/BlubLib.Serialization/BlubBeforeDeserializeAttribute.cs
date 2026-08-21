using System;

namespace OpenS4L.Blub.Serialization;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class BlubBeforeDeserializeAttribute : Attribute
{
}
