using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using OpenS4L.Blub.IO;

namespace OpenS4L.Blub;

public static class ByteArrayExtensions
{
	public static BinaryReader ToBinaryReader(this byte[] @this)
	{
		return new BinaryReader(new MemoryStream(@this));
	}

	public static BinaryWriter ToBinaryWriter(this byte[] @this)
	{
		return new BinaryWriter(new MemoryStream(@this));
	}

	public static string ToHexString(this IEnumerable<byte> @this)
	{
		return @this.ToHexString(" ");
	}

	public static string ToHexString(this IEnumerable<byte> @this, string separator)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (byte item in @this)
		{
			stringBuilder.Append(item.ToString("X2"));
			stringBuilder.Append(separator);
		}
		return stringBuilder.ToString();
	}

	public static string ToFormattedSize(this byte[] @this)
	{
		return Utilities.ToFormattedSize(@this.Length);
	}

	public static byte[] CompressGZip(this byte[] @this)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using GZipStream gZipStream = new GZipStream(memoryStream, CompressionMode.Compress);
		gZipStream.Write(@this, 0, @this.Length);
		gZipStream.Flush();
		return memoryStream.ToArray();
	}

	public static void CompressGZip(this byte[] @this, Stream output)
	{
		using GZipStream gZipStream = new GZipStream(output, CompressionMode.Compress, leaveOpen: true);
		gZipStream.Write(@this, 0, @this.Length);
		gZipStream.Flush();
	}

	public static byte[] CompressDeflate(this byte[] @this)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using DeflateStream deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress);
		deflateStream.Write(@this, 0, @this.Length);
		deflateStream.Flush();
		return memoryStream.ToArray();
	}

	public static void CompressDeflate(this byte[] @this, Stream output)
	{
		using DeflateStream deflateStream = new DeflateStream(output, CompressionMode.Compress, leaveOpen: true);
		deflateStream.Write(@this, 0, @this.Length);
		deflateStream.Flush();
	}

	public static byte[] DecompressGZip(this byte[] @this)
	{
		using MemoryStream stream = new MemoryStream(@this);
		using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress);
		return gZipStream.ReadToEnd();
	}

	public static void DecompressGZip(this byte[] @this, Stream output)
	{
		using MemoryStream stream = new MemoryStream(@this);
		using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress);
		gZipStream.CopyTo(output);
	}

	public static byte[] DecompressDeflate(this byte[] @this)
	{
		using MemoryStream stream = new MemoryStream(@this);
		using DeflateStream deflateStream = new DeflateStream(stream, CompressionMode.Decompress);
		return deflateStream.ReadToEnd();
	}

	public static void DecompressDeflate(this byte[] @this, Stream output)
	{
		using MemoryStream stream = new MemoryStream(@this);
		using DeflateStream deflateStream = new DeflateStream(stream, CompressionMode.Decompress);
		deflateStream.CopyTo(output);
	}

	public static byte[] FastClone(this byte[] @this)
	{
		byte[] array = new byte[@this.Length];
		Array.Copy(@this, array, @this.Length);
		return array;
	}
}
