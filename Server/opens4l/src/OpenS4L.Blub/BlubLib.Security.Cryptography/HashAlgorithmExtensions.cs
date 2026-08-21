using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OpenS4L.Blub.Security.Cryptography;

public static class HashAlgorithmExtensions
{
	public static byte[] GetBytes(this HashAlgorithm @this, Stream inputStream)
	{
		return @this.ComputeHash(inputStream);
	}

	public static byte[] GetBytes(this HashAlgorithm @this, byte[] data, int offset, int count)
	{
		return @this.ComputeHash(data, offset, count);
	}

	public static byte[] GetBytes(this HashAlgorithm @this, byte[] data)
	{
		return @this.GetBytes(data, 0, data.Length);
	}

	public static byte[] GetBytes(this HashAlgorithm @this, string data, Encoding encoding = null)
	{
		encoding = encoding ?? Encoding.Default;
		return @this.GetBytes(encoding.GetBytes(data));
	}

	public static string GetString(this HashAlgorithm @this, Stream inputStream)
	{
		return BitConverter.ToString(@this.ComputeHash(inputStream)).Replace("-", "").ToLowerInvariant();
	}

	public static string GetString(this HashAlgorithm @this, byte[] data, int offset, int count)
	{
		return BitConverter.ToString(@this.ComputeHash(data, offset, count)).Replace("-", "").ToLowerInvariant();
	}

	public static string GetString(this HashAlgorithm @this, byte[] data)
	{
		return @this.GetString(data, 0, data.Length);
	}

	public static string GetString(this HashAlgorithm @this, string data, Encoding encoding = null)
	{
		encoding = encoding ?? Encoding.Default;
		return @this.GetString(encoding.GetBytes(data));
	}

	public static ushort GetUInt16(this HashAlgorithm @this, Stream inputStream)
	{
		return BitConverter.ToUInt16(@this.ComputeHash(inputStream), 0);
	}

	public static ushort GetUInt16(this HashAlgorithm @this, byte[] data, int offset, int count)
	{
		return BitConverter.ToUInt16(@this.ComputeHash(data, offset, count), 0);
	}

	public static ushort GetUInt16(this HashAlgorithm @this, byte[] data)
	{
		return @this.GetUInt16(data, 0, data.Length);
	}

	public static ushort GetUInt16(this HashAlgorithm @this, string data, Encoding encoding = null)
	{
		encoding = encoding ?? Encoding.Default;
		return @this.GetUInt16(encoding.GetBytes(data));
	}

	public static uint GetUInt32(this HashAlgorithm @this, Stream inputStream)
	{
		return BitConverter.ToUInt32(@this.ComputeHash(inputStream), 0);
	}

	public static uint GetUInt32(this HashAlgorithm @this, byte[] data, int offset, int count)
	{
		return BitConverter.ToUInt32(@this.ComputeHash(data, offset, count), 0);
	}

	public static uint GetUInt32(this HashAlgorithm @this, byte[] data)
	{
		return @this.GetUInt32(data, 0, data.Length);
	}

	public static uint GetUInt32(this HashAlgorithm @this, string data, Encoding encoding = null)
	{
		encoding = encoding ?? Encoding.Default;
		return @this.GetUInt32(encoding.GetBytes(data));
	}

	public static ulong GetUInt64(this HashAlgorithm @this, Stream inputStream)
	{
		return BitConverter.ToUInt64(@this.ComputeHash(inputStream), 0);
	}

	public static ulong GetUInt64(this HashAlgorithm @this, byte[] data, int offset, int count)
	{
		return BitConverter.ToUInt64(@this.ComputeHash(data, offset, count), 0);
	}

	public static ulong GetUInt64(this HashAlgorithm @this, byte[] data)
	{
		return @this.GetUInt64(data, 0, data.Length);
	}

	public static ulong GetUInt64(this HashAlgorithm @this, string data, Encoding encoding = null)
	{
		encoding = encoding ?? Encoding.Default;
		return @this.GetUInt64(encoding.GetBytes(data));
	}
}
