using System;
using System.Security.Cryptography;

namespace OpenS4L.Blub.Security.Cryptography;

public sealed class RC4 : SymmetricAlgorithm
{
	private class RC4ManagedTransform : ICryptoTransform, IDisposable
	{
		private readonly byte[] _key;

		private readonly int _keyLen;

		private readonly byte[] _permutation;

		private byte _index1;

		private byte _index2;

		private bool _disposed;

		public bool CanReuseTransform => true;

		public bool CanTransformMultipleBlocks => true;

		public int InputBlockSize => 1;

		public int OutputBlockSize => 1;

		public RC4ManagedTransform(byte[] key)
		{
			_key = key.FastClone();
			_keyLen = key.Length;
			_permutation = new byte[256];
			_disposed = false;
			Init();
		}

		public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}
			if (inputBuffer == null || outputBuffer == null)
			{
				throw new ArgumentNullException();
			}
			if (inputOffset < 0 || outputOffset < 0 || inputOffset + inputCount > inputBuffer.Length || outputOffset + inputCount > outputBuffer.Length)
			{
				throw new ArgumentOutOfRangeException();
			}
			int num = inputOffset + inputCount;
			while (inputOffset < num)
			{
				_index1 = (byte)((_index1 + 1) % 256);
				_index2 = (byte)((_index2 + _permutation[_index1]) % 256);
				byte b = _permutation[_index1];
				_permutation[_index1] = _permutation[_index2];
				_permutation[_index2] = b;
				byte b2 = (byte)((_permutation[_index1] + _permutation[_index2]) % 256);
				outputBuffer[outputOffset] = (byte)(inputBuffer[inputOffset] ^ _permutation[b2]);
				inputOffset++;
				outputOffset++;
			}
			return inputCount;
		}

		public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}
			byte[] array = new byte[inputCount];
			TransformBlock(inputBuffer, inputOffset, inputCount, array, 0);
			Init();
			return array;
		}

		public void Dispose()
		{
			_disposed = true;
		}

		private void Init()
		{
			for (int i = 0; i < 256; i++)
			{
				_permutation[i] = (byte)i;
			}
			_index1 = 0;
			_index2 = 0;
			int num = 0;
			for (int j = 0; j < 256; j++)
			{
				num = (num + _permutation[j] + _key[j % _keyLen]) % 256;
				byte b = _permutation[j];
				_permutation[j] = _permutation[num];
				_permutation[num] = b;
			}
		}
	}

	private RandomNumberGenerator _rng = RandomNumberGenerator.Create();

	public override int BlockSize
	{
		get
		{
			return 8;
		}
		set
		{
			throw new NotSupportedException();
		}
	}

	public override int FeedbackSize
	{
		get
		{
			return 0;
		}
		set
		{
			throw new NotSupportedException();
		}
	}

	public override byte[] IV
	{
		get
		{
			return Array.Empty<byte>();
		}
		set
		{
			throw new NotSupportedException();
		}
	}

	public override KeySizes[] LegalBlockSizes { get; }

	public override KeySizes[] LegalKeySizes { get; }

	public override CipherMode Mode
	{
		get
		{
			return CipherMode.ECB;
		}
		set
		{
			if (value != CipherMode.ECB)
			{
				throw new NotSupportedException("RC4 only supports OFB");
			}
		}
	}

	public override PaddingMode Padding
	{
		get
		{
			return PaddingMode.None;
		}
		set
		{
			throw new NotSupportedException();
		}
	}

	public RC4()
	{
		KeySizeValue = 128;
		LegalBlockSizes = new KeySizes[1]
		{
			new KeySizes(8, 8, 0)
		};
		LegalKeySizes = new KeySizes[1]
		{
			new KeySizes(8, 2048, 8)
		};
	}

	public override void GenerateIV()
	{
	}

	public override void GenerateKey()
	{
		if (_rng == null)
		{
			throw new ObjectDisposedException(GetType().FullName);
		}
		byte[] array = new byte[KeySize / 8];
		_rng.GetBytes(array);
		Key = array;
	}

	public override ICryptoTransform CreateDecryptor(byte[] rgbKey, byte[] rgbIV)
	{
		if (rgbKey == null)
		{
			throw new ArgumentNullException("rgbKey");
		}
		if (rgbKey.Length == 0 || rgbKey.Length > 256)
		{
			throw new CryptographicException("Invalid Key");
		}
		if (rgbIV != null && rgbIV.Length > 1)
		{
			throw new CryptographicException("Invalid Initialization Vector");
		}
		return new RC4ManagedTransform(rgbKey);
	}

	public override ICryptoTransform CreateEncryptor(byte[] rgbKey, byte[] rgbIV)
	{
		return CreateDecryptor(rgbKey, rgbIV);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && _rng != null)
		{
			_rng.Dispose();
			_rng = null;
		}
		base.Dispose(disposing);
	}
}
