using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace OpenS4L.Blub.IO;

public static class StreamExtensions
{
	public static BinaryReader ToBinaryReader(this Stream @this, Encoding encoding, bool leaveOpen)
	{
		return new BinaryReader(@this, encoding, leaveOpen);
	}

	public static BinaryReader ToBinaryReader(this Stream @this, bool leaveOpen)
	{
		return new BinaryReader(@this, Encoding.UTF8, leaveOpen);
	}

	public static BinaryWriter ToBinaryWriter(this Stream @this, Encoding encoding, bool leaveOpen)
	{
		return new BinaryWriter(@this, encoding, leaveOpen);
	}

	public static BinaryWriter ToBinaryWriter(this Stream @this, bool leaveOpen)
	{
		return new BinaryWriter(@this, Encoding.UTF8, leaveOpen);
	}

	public static byte[] ReadToEnd(this Stream @this)
	{
		using MemoryStream memoryStream = new MemoryStream();
		@this.CopyTo(memoryStream);
		return memoryStream.ToArray();
	}

	public static async Task<byte[]> ReadToEndAsync(this Stream @this)
	{
		using MemoryStream ms = new MemoryStream();
		await @this.CopyToAsync(ms).ConfigureAwait(continueOnCapturedContext: false);
		return ms.ToArray();
	}

	public static void Serialize(this Stream @this, IManualSerializer value)
	{
		value.Serialize(@this);
	}

	public static void Serialize<T>(this Stream @this, IEnumerable<T> values) where T : IManualSerializer
	{
		foreach (T value in values)
		{
			@this.Serialize(value);
		}
	}

	public static T Deserialize<T>(this Stream @this) where T : IManualSerializer, new()
	{
		T result = new T();
		result.Deserialize(@this);
		return result;
	}

	public static T[] DeserializeArray<T>(this Stream @this, int count) where T : IManualSerializer, new()
	{
		return @this.DeserializeArray<T>((uint)count);
	}

	public static T[] DeserializeArray<T>(this Stream @this, uint count) where T : IManualSerializer, new()
	{
		T[] array = new T[count];
		for (int i = 0; i < count; i++)
		{
			array[i] = @this.Deserialize<T>();
		}
		return array;
	}

	public static bool IsEOF(this Stream @this)
	{
		return @this.Position == @this.Length;
	}

	public static byte[] CompressGZip(this Stream @this)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using GZipStream gZipStream = new GZipStream(@this, CompressionMode.Compress, leaveOpen: true);
		gZipStream.CopyTo(memoryStream);
		gZipStream.Flush();
		return memoryStream.ToArray();
	}

	public static void CompressGZip(this Stream @this, Stream output)
	{
		using GZipStream gZipStream = new GZipStream(@this, CompressionMode.Compress, leaveOpen: true);
		gZipStream.CopyTo(output);
		gZipStream.Flush();
	}

	public static byte[] CompressDeflate(this Stream @this)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using DeflateStream deflateStream = new DeflateStream(@this, CompressionMode.Compress, leaveOpen: true);
		deflateStream.CopyTo(memoryStream);
		deflateStream.Flush();
		return memoryStream.ToArray();
	}

	public static void CompressDeflate(this Stream @this, Stream output)
	{
		using DeflateStream deflateStream = new DeflateStream(@this, CompressionMode.Compress, leaveOpen: true);
		deflateStream.CopyTo(output);
		deflateStream.Flush();
	}

	public static byte[] DecompressGZip(this Stream @this)
	{
		using GZipStream gZipStream = new GZipStream(@this, CompressionMode.Decompress, leaveOpen: true);
		return gZipStream.ReadToEnd();
	}

	public static void DecompressGZip(this Stream @this, Stream output)
	{
		using GZipStream gZipStream = new GZipStream(@this, CompressionMode.Decompress, leaveOpen: true);
		gZipStream.CopyTo(output);
	}

	public static byte[] DecompressDeflate(this Stream @this)
	{
		using DeflateStream deflateStream = new DeflateStream(@this, CompressionMode.Decompress, leaveOpen: true);
		return deflateStream.ReadToEnd();
	}

	public static void DecompressDeflate(this Stream @this, Stream output)
	{
		using DeflateStream deflateStream = new DeflateStream(@this, CompressionMode.Decompress, leaveOpen: true);
		deflateStream.CopyTo(output);
	}
}
