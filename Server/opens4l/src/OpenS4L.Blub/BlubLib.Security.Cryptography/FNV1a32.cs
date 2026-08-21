using System;
using System.Security.Cryptography;

namespace OpenS4L.Blub.Security.Cryptography;

public sealed class FNV1a32 : HashAlgorithm
{
	private const uint OffsetBasis = 2166136261u;

	private const uint Prime = 16777619u;

	private uint _hash = 2166136261u;

	public override void Initialize()
	{
		_hash = 2166136261u;
	}

	protected override void HashCore(byte[] array, int ibStart, int cbSize)
	{
		foreach (byte b in array)
		{
			_hash ^= b;
			_hash *= 16777619u;
		}
	}

	protected override byte[] HashFinal()
	{
		return BitConverter.GetBytes(_hash);
	}
}
