using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using OpenS4L.Blub.Buffers;

namespace OpenS4L.Blub.IO;

public static class BinaryWriterExtensions
{
	public static void Write(this BinaryWriter @this, IEnumerable<byte> values)
	{
		foreach (byte value in values)
		{
			@this.Write(value);
		}
	}

	public static void Write(this BinaryWriter @this, IEnumerable<bool> values)
	{
		foreach (bool value in values)
		{
			@this.Write(value);
		}
	}

	public static void Write(this BinaryWriter @this, IEnumerable<char> values)
	{
		foreach (char value in values)
		{
			@this.Write(value);
		}
	}

	public static void Write(this BinaryWriter @this, IEnumerable<sbyte> values)
	{
		foreach (sbyte value in values)
		{
			@this.Write(value);
		}
	}

	public static void Write(this BinaryWriter @this, IEnumerable<short> values)
	{
		foreach (short value in values)
		{
			@this.Write(value);
		}
	}

	public static void Write(this BinaryWriter @this, IEnumerable<ushort> values)
	{
		foreach (ushort value in values)
		{
			@this.Write(value);
		}
	}

	public static void Write(this BinaryWriter @this, IEnumerable<int> values)
	{
		foreach (int value in values)
		{
			@this.Write(value);
		}
	}

	public static void Write(this BinaryWriter @this, IEnumerable<uint> values)
	{
		foreach (uint value in values)
		{
			@this.Write(value);
		}
	}

	public static void Write(this BinaryWriter @this, IEnumerable<long> values)
	{
		foreach (long value in values)
		{
			@this.Write(value);
		}
	}

	public static void Write(this BinaryWriter @this, IEnumerable<ulong> values)
	{
		foreach (ulong value in values)
		{
			@this.Write(value);
		}
	}

	public static void Write(this BinaryWriter @this, IEnumerable<float> values)
	{
		foreach (float value in values)
		{
			@this.Write(value);
		}
	}

	public static void Write(this BinaryWriter @this, IEnumerable<double> values)
	{
		foreach (double value in values)
		{
			@this.Write(value);
		}
	}

	public static void Write(this BinaryWriter @this, IEnumerable<decimal> values)
	{
		foreach (decimal value in values)
		{
			@this.Write(value);
		}
	}

	public static void Write(this BinaryWriter @this, IEnumerable<string> values)
	{
		foreach (string value in values)
		{
			@this.Write(value);
		}
	}

	public static void WriteEnum<T>(this BinaryWriter @this, T value) where T : struct, IComparable, IConvertible
	{
		Type type = value.GetType();
		if (!type.GetTypeInfo().IsEnum)
		{
			throw new ArgumentException("T is not an enum");
		}
		switch (Enum.GetUnderlyingType(type).GetTypeCode())
		{
		case TypeCode.Byte:
			@this.Write(DynamicCast<byte>.From(value));
			break;
		case TypeCode.SByte:
			@this.Write(DynamicCast<sbyte>.From(value));
			break;
		case TypeCode.Int16:
			@this.Write(DynamicCast<short>.From(value));
			break;
		case TypeCode.UInt16:
			@this.Write(DynamicCast<ushort>.From(value));
			break;
		case TypeCode.Int32:
			@this.Write(DynamicCast<int>.From(value));
			break;
		case TypeCode.UInt32:
			@this.Write(DynamicCast<uint>.From(value));
			break;
		case TypeCode.Int64:
			@this.Write(DynamicCast<long>.From(value));
			break;
		case TypeCode.UInt64:
			@this.Write(DynamicCast<ulong>.From(value));
			break;
		case TypeCode.Single:
			@this.Write(DynamicCast<float>.From(value));
			break;
		case TypeCode.Double:
			@this.Write(DynamicCast<double>.From(value));
			break;
		case TypeCode.Decimal:
			@this.Write(DynamicCast<decimal>.From(value));
			break;
		default:
			throw new NotSupportedException("Type is not supported");
		}
	}

	public static void Serialize(this BinaryWriter @this, IManualSerializer value)
	{
		value.Serialize(@this.BaseStream);
	}

	public static void Serialize<T>(this BinaryWriter @this, IEnumerable<T> values) where T : IManualSerializer
	{
		foreach (T value in values)
		{
			@this.Serialize(value);
		}
	}

	public static void Write(this BinaryWriter @this, IPEndPoint value)
	{
		@this.Write(value.Address.GetAddressBytes());
		@this.Write((ushort)value.Port);
	}

	public static void Write(this BinaryWriter @this, ArraySegment<byte> segment)
	{
		@this.Write(segment.Array, segment.Offset, segment.Count);
	}

	public static void WriteCString(this BinaryWriter @this, string value)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(value + "\0");
		@this.Write(bytes);
	}

	public static void WriteCString(this BinaryWriter @this, string value, int maxLength)
	{
		value = value ?? "";
		byte[] bytes = Encoding.UTF8.GetBytes(value + "\0");
		if (bytes.Length > maxLength)
		{
			throw new ArgumentOutOfRangeException(string.Format("{0} is longer than {1}", "value", "maxLength"), "value");
		}
		@this.Write(bytes);
		@this.Fill(maxLength - bytes.Length);
	}

	public static void Fill(this BinaryWriter @this, int count)
	{
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException("count");
		}
		if (count == 0)
		{
			return;
		}
		if (count < 256)
		{
			for (int i = 0; i < count; i++)
			{
				@this.Write((byte)0);
			}
		}
		else
		{
			@this.Write(new byte[count]);
		}
	}

	public static bool IsEOF(this BinaryWriter @this)
	{
		return @this.BaseStream.IsEOF();
	}

	public static byte[] ToArray(this BinaryWriter @this)
	{
		Stream baseStream = @this.BaseStream;
		if (baseStream != null)
		{
			if (baseStream is MemoryStream memoryStream)
			{
				return memoryStream.ToArray();
			}
			if (baseStream is BufferStream bufferStream)
			{
				return bufferStream.ToArray();
			}
		}
		throw new InvalidOperationException("BaseStream must be a MemoryStream or BufferStream");
	}
}
