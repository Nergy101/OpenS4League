using System;

namespace OpenS4L.Blub.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public class BlubContractAttribute : Attribute
{
}
