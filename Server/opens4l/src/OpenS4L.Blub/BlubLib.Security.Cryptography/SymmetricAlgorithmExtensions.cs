using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using OpenS4L.Blub.IO;

namespace OpenS4L.Blub.Security.Cryptography;

public static class SymmetricAlgorithmExtensions
{
	public static byte[] Encrypt(this SymmetricAlgorithm @this, byte[] buffer)
	{
		using ICryptoTransform transform = @this.CreateEncryptor();
		using MemoryStream memoryStream = new MemoryStream();
		using CryptoStream cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write);
		cryptoStream.Write(buffer, 0, buffer.Length);
		cryptoStream.Flush();
		return memoryStream.ToArray();
	}

	public static byte[] Encrypt(this SymmetricAlgorithm @this, Stream stream)
	{
		using ICryptoTransform transform = @this.CreateEncryptor();
		using MemoryStream memoryStream = new MemoryStream();
		using CryptoStream cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write);
		stream.CopyTo(cryptoStream);
		cryptoStream.Flush();
		return memoryStream.ToArray();
	}

	public static async Task<byte[]> EncryptAsync(this SymmetricAlgorithm @this, byte[] buffer)
	{
		using ICryptoTransform encryptor = @this.CreateEncryptor();
		using MemoryStream ms = new MemoryStream();
		using CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
		await cs.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(continueOnCapturedContext: false);
		await cs.FlushAsync().ConfigureAwait(continueOnCapturedContext: false);
		return ms.ToArray();
	}

	public static async Task<byte[]> EncryptAsync(this SymmetricAlgorithm @this, Stream stream)
	{
		using ICryptoTransform encryptor = @this.CreateEncryptor();
		using MemoryStream ms = new MemoryStream();
		using CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
		await stream.CopyToAsync(cs).ConfigureAwait(continueOnCapturedContext: false);
		await cs.FlushAsync().ConfigureAwait(continueOnCapturedContext: false);
		return ms.ToArray();
	}

	public static byte[] Decrypt(this SymmetricAlgorithm @this, byte[] buffer)
	{
		using ICryptoTransform transform = @this.CreateDecryptor();
		using MemoryStream stream = new MemoryStream(buffer);
		using CryptoStream cryptoStream = new CryptoStream(stream, transform, CryptoStreamMode.Read);
		return cryptoStream.ReadToEnd();
	}

	public static byte[] Decrypt(this SymmetricAlgorithm @this, Stream stream)
	{
		using ICryptoTransform transform = @this.CreateDecryptor();
		using CryptoStream cryptoStream = new CryptoStream(stream, transform, CryptoStreamMode.Read);
		return cryptoStream.ReadToEnd();
	}

	public static async Task<byte[]> DecryptAsync(this SymmetricAlgorithm @this, byte[] buffer)
	{
		using ICryptoTransform decryptor = @this.CreateDecryptor();
		using MemoryStream ms = new MemoryStream(buffer);
		using CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
		return await cs.ReadToEndAsync().ConfigureAwait(continueOnCapturedContext: false);
	}

	public static async Task<byte[]> DecryptAsync(this SymmetricAlgorithm @this, Stream stream)
	{
		using ICryptoTransform decryptor = @this.CreateDecryptor();
		using CryptoStream cs = new CryptoStream(stream, decryptor, CryptoStreamMode.Read);
		return await cs.ReadToEndAsync().ConfigureAwait(continueOnCapturedContext: false);
	}
}
