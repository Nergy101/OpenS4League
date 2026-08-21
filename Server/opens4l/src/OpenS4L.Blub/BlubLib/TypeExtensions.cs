using System;
using System.Reflection;

namespace OpenS4L.Blub;

public static class TypeExtensions
{
	public static TypeCode GetTypeCode(this Type @this)
	{
		if (@this == null)
		{
			return TypeCode.Empty;
		}
		if (@this == typeof(bool))
		{
			return TypeCode.Boolean;
		}
		if (@this == typeof(char))
		{
			return TypeCode.Char;
		}
		if (@this == typeof(sbyte))
		{
			return TypeCode.SByte;
		}
		if (@this == typeof(byte))
		{
			return TypeCode.Byte;
		}
		if (@this == typeof(short))
		{
			return TypeCode.Int16;
		}
		if (@this == typeof(ushort))
		{
			return TypeCode.UInt16;
		}
		if (@this == typeof(int))
		{
			return TypeCode.Int32;
		}
		if (@this == typeof(uint))
		{
			return TypeCode.UInt32;
		}
		if (@this == typeof(long))
		{
			return TypeCode.Int64;
		}
		if (@this == typeof(ulong))
		{
			return TypeCode.UInt64;
		}
		if (@this == typeof(float))
		{
			return TypeCode.Single;
		}
		if (@this == typeof(double))
		{
			return TypeCode.Double;
		}
		if (@this == typeof(decimal))
		{
			return TypeCode.Decimal;
		}
		if (@this == typeof(DateTime))
		{
			return TypeCode.DateTime;
		}
		if (@this == typeof(string))
		{
			return TypeCode.String;
		}
		if (@this.GetTypeInfo().IsEnum)
		{
			return Enum.GetUnderlyingType(@this).GetTypeCode();
		}
		return TypeCode.Object;
	}
}
