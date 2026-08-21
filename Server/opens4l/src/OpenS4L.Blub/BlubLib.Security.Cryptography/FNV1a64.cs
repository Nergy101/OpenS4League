using System;
using System.Security.Cryptography;

namespace OpenS4L.Blub.Security.Cryptography;

public sealed class FNV1a64 : HashAlgorithm
{
	private const ulong OffsetBasis = 14695981039346656037uL;

	private const ulong Prime = 1099511628211uL;

	private ulong _hash = 14695981039346656037uL;

	public override void Initialize()
	{
		_hash = 14695981039346656037uL;
	}

	protected override void HashCore(byte[] array, int ibStart, int cbSize)
	{
		foreach (byte b in array)
		{
			_hash ^= b;
			_hash *= 1099511628211uL;
		}
	}

	protected override byte[] HashFinal()
	{
		return BitConverter.GetBytes(_hash);
	}
}
