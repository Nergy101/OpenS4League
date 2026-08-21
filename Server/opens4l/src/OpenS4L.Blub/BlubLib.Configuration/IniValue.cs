using System;
using System.Collections;
using System.Globalization;

namespace OpenS4L.Blub.Configuration;

public sealed class IniValue : IComparable, IConvertible, IFormattable
{
	public string Value { get; set; }

	public CultureInfo CultureInfo { get; set; }

	public IniValue()
		: this("")
	{
	}

	public IniValue(string value)
	{
		Value = value;
		CultureInfo = CultureInfo.InvariantCulture;
	}

	public static implicit operator string(IniValue value)
	{
		return value.Value;
	}

	public static implicit operator char(IniValue value)
	{
		return value.ToChar(value.CultureInfo);
	}

	public static implicit operator byte(IniValue value)
	{
		return value.ToByte(value.CultureInfo);
	}

	public static implicit operator bool(IniValue value)
	{
		return value.ToBoolean(value.CultureInfo);
	}

	public static implicit operator short(IniValue value)
	{
		return value.ToInt16(value.CultureInfo);
	}

	public static implicit operator int(IniValue value)
	{
		return value.ToInt32(value.CultureInfo);
	}

	public static implicit operator long(IniValue value)
	{
		return value.ToInt64(value.CultureInfo);
	}

	public static implicit operator ushort(IniValue value)
	{
		return value.ToUInt16(value.CultureInfo);
	}

	public static implicit operator uint(IniValue value)
	{
		return value.ToUInt32(value.CultureInfo);
	}

	public static implicit operator ulong(IniValue value)
	{
		return value.ToUInt64(value.CultureInfo);
	}

	public static implicit operator float(IniValue value)
	{
		return value.ToSingle(value.CultureInfo);
	}

	public static implicit operator double(IniValue value)
	{
		return value.ToDouble(value.CultureInfo);
	}

	public static implicit operator IniValue(string value)
	{
		return new IniValue(value);
	}

	public static implicit operator IniValue(char value)
	{
		return new IniValue(value.ToString());
	}

	public static implicit operator IniValue(byte value)
	{
		return new IniValue(value.ToString(CultureInfo.InvariantCulture));
	}

	public static implicit operator IniValue(bool value)
	{
		return new IniValue(value.ToString(CultureInfo.InvariantCulture));
	}

	public static implicit operator IniValue(short value)
	{
		return new IniValue(value.ToString(CultureInfo.InvariantCulture));
	}

	public static implicit operator IniValue(int value)
	{
		return new IniValue(value.ToString(CultureInfo.InvariantCulture));
	}

	public static implicit operator IniValue(long value)
	{
		return new IniValue(value.ToString(CultureInfo.InvariantCulture));
	}

	public static implicit operator IniValue(ushort value)
	{
		return new IniValue(value.ToString(CultureInfo.InvariantCulture));
	}

	public static implicit operator IniValue(uint value)
	{
		return new IniValue(value.ToString(CultureInfo.InvariantCulture));
	}

	public static implicit operator IniValue(ulong value)
	{
		return new IniValue(value.ToString(CultureInfo.InvariantCulture));
	}

	public static implicit operator IniValue(float value)
	{
		return new IniValue(value.ToString(CultureInfo.InvariantCulture));
	}

	public static implicit operator IniValue(double value)
	{
		return new IniValue(value.ToString(CultureInfo.InvariantCulture));
	}

	public override string ToString()
	{
		return Value;
	}

	public string ToString(string format, IFormatProvider formatProvider)
	{
		return Value;
	}

	public int CompareTo(object obj)
	{
		object a = Convert.ChangeType(Value, obj.GetType());
		return Comparer.DefaultInvariant.Compare(a, obj);
	}

	public TypeCode GetTypeCode()
	{
		return TypeCode.Object;
	}

	public bool ToBoolean(IFormatProvider provider)
	{
		if (bool.TryParse(Value, out var result))
		{
			return result;
		}
		if (Value.Equals("y", StringComparison.InvariantCultureIgnoreCase) || Value.Equals("yes", StringComparison.InvariantCultureIgnoreCase))
		{
			return true;
		}
		if (Value.Equals("n", StringComparison.InvariantCultureIgnoreCase) || Value.Equals("no", StringComparison.InvariantCultureIgnoreCase))
		{
			return false;
		}
		return ToInt32(provider) > 0;
	}

	public char ToChar(IFormatProvider provider)
	{
		if (!char.TryParse(Value, out var result))
		{
			return ' ';
		}
		return result;
	}

	public sbyte ToSByte(IFormatProvider provider)
	{
		sbyte result;
		return (sbyte)(sbyte.TryParse(Value, out result) ? result : 0);
	}

	public byte ToByte(IFormatProvider provider)
	{
		byte result;
		return (byte)(byte.TryParse(Value, out result) ? result : 0);
	}

	public short ToInt16(IFormatProvider provider)
	{
		short result;
		return (short)(short.TryParse(Value, out result) ? result : 0);
	}

	public ushort ToUInt16(IFormatProvider provider)
	{
		ushort result;
		return (ushort)(ushort.TryParse(Value, out result) ? result : 0);
	}

	public int ToInt32(IFormatProvider provider)
	{
		if (!int.TryParse(Value, out var result))
		{
			return 0;
		}
		return result;
	}

	public uint ToUInt32(IFormatProvider provider)
	{
		if (!uint.TryParse(Value, out var result))
		{
			return 0u;
		}
		return result;
	}

	public long ToInt64(IFormatProvider provider)
	{
		if (!long.TryParse(Value, out var result))
		{
			return 0L;
		}
		return result;
	}

	public ulong ToUInt64(IFormatProvider provider)
	{
		if (!ulong.TryParse(Value, out var result))
		{
			return 0uL;
		}
		return result;
	}

	public float ToSingle(IFormatProvider provider)
	{
		if (!float.TryParse(Value, NumberStyles.Float, provider, out var result))
		{
			return 0f;
		}
		return result;
	}

	public double ToDouble(IFormatProvider provider)
	{
		if (!double.TryParse(Value, NumberStyles.Float, provider, out var result))
		{
			return 0.0;
		}
		return result;
	}

	public decimal ToDecimal(IFormatProvider provider)
	{
		if (!decimal.TryParse(Value, NumberStyles.Float, provider, out var result))
		{
			return 0m;
		}
		return result;
	}

	public DateTime ToDateTime(IFormatProvider provider)
	{
		throw new NotSupportedException();
	}

	public string ToString(IFormatProvider provider)
	{
		return Value;
	}

	public object ToType(Type conversionType, IFormatProvider provider)
	{
		return Convert.ChangeType(Value, conversionType, provider);
	}
}
