using System;

namespace OpenS4L.Blub.Serialization;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public class BlubMemberAttribute : Attribute
{
	public uint? Order { get; set; }

	public BlubMemberAttribute()
	{
	}

	public BlubMemberAttribute(uint order)
	{
		Order = order;
	}
}
