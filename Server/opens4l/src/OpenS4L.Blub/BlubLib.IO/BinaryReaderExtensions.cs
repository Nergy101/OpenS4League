using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OpenS4L.Blub.IO;

public static class BinaryReaderExtensions
{
	public static byte[] ReadToEnd(this BinaryReader @this)
	{
		return @this.BaseStream.ReadToEnd();
	}

	public static Task<byte[]> ReadToEndAsync(this BinaryReader @this)
	{
		return @this.BaseStream.ReadToEndAsync();
	}

	public static string[] ReadStrings(this BinaryReader @this, int count)
	{
		string[] array = new string[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = @this.ReadString();
		}
		return array;
	}

	public static T[] ReadArray<T>(this BinaryReader @this, int count) where T : struct, IComparable, IConvertible
	{
		Type typeFromHandle = typeof(T);
		T[] array = FastActivator<T>.CreateArray(count);
		byte[] array2 = typeFromHandle.GetTypeCode() switch
		{
			TypeCode.Boolean => @this.ReadBytes(count), 
			TypeCode.Char => @this.ReadBytes(2 * count), 
			TypeCode.Byte => @this.ReadBytes(count), 
			TypeCode.SByte => @this.ReadBytes(count), 
			TypeCode.Int16 => @this.ReadBytes(2 * count), 
			TypeCode.Int32 => @this.ReadBytes(4 * count), 
			TypeCode.Int64 => @this.ReadBytes(8 * count), 
			TypeCode.UInt16 => @this.ReadBytes(2 * count), 
			TypeCode.UInt32 => @this.ReadBytes(4 * count), 
			TypeCode.UInt64 => @this.ReadBytes(8 * count), 
			TypeCode.Single => @this.ReadBytes(4 * count), 
			TypeCode.Double => @this.ReadBytes(8 * count), 
			TypeCode.Decimal => @this.ReadBytes(16 * count), 
			_ => throw new NotSupportedException("Type is not supported"), 
		};
		Buffer.BlockCopy(array2, 0, array, 0, array2.Length);
		return array;
	}

	public static T ReadEnum<T>(this BinaryReader @this) where T : struct, IComparable, IConvertible
	{
		Type typeFromHandle = typeof(T);
		if (!typeFromHandle.GetTypeInfo().IsEnum)
		{
			throw new ArgumentException("T is not an enum");
		}
		return Enum.GetUnderlyingType(typeFromHandle).GetTypeCode() switch
		{
			TypeCode.Byte => DynamicCast<T>.From(@this.ReadByte()), 
			TypeCode.SByte => DynamicCast<T>.From(@this.ReadSByte()), 
			TypeCode.Int16 => DynamicCast<T>.From(@this.ReadInt16()), 
			TypeCode.Int32 => DynamicCast<T>.From(@this.ReadInt32()), 
			TypeCode.Int64 => DynamicCast<T>.From(@this.ReadInt64()), 
			TypeCode.UInt16 => DynamicCast<T>.From(@this.ReadUInt16()), 
			TypeCode.UInt32 => DynamicCast<T>.From(@this.ReadUInt32()), 
			TypeCode.UInt64 => DynamicCast<T>.From(@this.ReadUInt64()), 
			_ => throw new NotSupportedException("Type is not supported"), 
		};
	}

	public static T[] ReadEnums<T>(this BinaryReader @this, int count) where T : struct, IComparable, IConvertible
	{
		if (!typeof(T).GetTypeInfo().IsEnum)
		{
			throw new ArgumentException("T is not an enum");
		}
		T[] array = FastActivator<T>.CreateArray(count);
		for (int i = 0; i < count; i++)
		{
			array[i] = @this.ReadEnum<T>();
		}
		return array;
	}

	public static T Deserialize<T>(this BinaryReader @this) where T : IManualSerializer, new()
	{
		T result = FastActivator<T>.Create();
		result.Deserialize(@this.BaseStream);
		return result;
	}

	public static T[] DeserializeArray<T>(this BinaryReader @this, int count) where T : IManualSerializer, new()
	{
		T[] array = FastActivator<T>.CreateArray(count);
		for (int i = 0; i < count; i++)
		{
			array[i] = @this.Deserialize<T>();
		}
		return array;
	}

	public static IPEndPoint ReadIPEndPoint(this BinaryReader @this)
	{
		return new IPEndPoint(new IPAddress(@this.ReadBytes(4)), @this.ReadUInt16());
	}

	public static string ReadCString(this BinaryReader @this)
	{
		StringBuilder stringBuilder = new StringBuilder();
		char value;
		while ((value = @this.ReadChar()) != 0)
		{
			stringBuilder.Append(value);
		}
		return stringBuilder.ToString();
	}

	public static string ReadCString(this BinaryReader @this, int length)
	{
		if (length < 1)
		{
			throw new ArgumentOutOfRangeException("length");
		}
		return Encoding.UTF8.GetString(@this.ReadBytes(length)).TrimEnd(new char[1]);
	}

	public static bool IsEOF(this BinaryReader @this)
	{
		return @this.BaseStream.IsEOF();
	}
}
