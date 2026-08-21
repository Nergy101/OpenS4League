using System;
using System.IO;

namespace OpenS4L.Blub.IO;

public class BitStreamReader
{
	private readonly byte[] _byteArray;

	private uint _bufferLengthInBits;

	private int _byteArrayIndex;

	private byte _partialByte;

	private int _cbitsInPartialByte;

	public bool EndOfStream => _bufferLengthInBits == 0;

	public int CurrentIndex => _byteArrayIndex - 1;

	public BitStreamReader(byte[] buffer)
	{
		_byteArray = buffer;
		_bufferLengthInBits = (uint)(buffer.Length * 8);
	}

	public BitStreamReader(byte[] buffer, int startIndex)
	{
		if (startIndex < 0 || startIndex >= buffer.Length)
		{
			throw new ArgumentOutOfRangeException("startIndex");
		}
		_byteArray = buffer;
		_byteArrayIndex = startIndex;
		_bufferLengthInBits = (uint)((buffer.Length - startIndex) * 8);
	}

	public BitStreamReader(byte[] buffer, uint bufferLengthInBits)
		: this(buffer)
	{
		if (bufferLengthInBits > buffer.Length * 8)
		{
			throw new ArgumentOutOfRangeException("bufferLengthInBits");
		}
		_bufferLengthInBits = bufferLengthInBits;
	}

	public long ReadUInt64(int countOfBits)
	{
		if (countOfBits > 64 || countOfBits <= 0)
		{
			throw new ArgumentOutOfRangeException("countOfBits");
		}
		long num = 0L;
		while (countOfBits > 0)
		{
			int num2 = 8;
			if (countOfBits < 8)
			{
				num2 = countOfBits;
			}
			num <<= num2;
			byte b = ReadByte(num2);
			num |= b;
			countOfBits -= num2;
		}
		return num;
	}

	public ushort ReadUInt16(int countOfBits)
	{
		if (countOfBits > 16 || countOfBits <= 0)
		{
			throw new ArgumentOutOfRangeException("countOfBits");
		}
		ushort num = 0;
		while (countOfBits > 0)
		{
			int num2 = 8;
			if (countOfBits < 8)
			{
				num2 = countOfBits;
			}
			num = (ushort)(num << num2);
			byte b = ReadByte(num2);
			num |= b;
			countOfBits -= num2;
		}
		return num;
	}

	public uint ReadUInt16Reverse(int countOfBits)
	{
		if (countOfBits > 16 || countOfBits <= 0)
		{
			throw new ArgumentOutOfRangeException("countOfBits");
		}
		ushort num = 0;
		int num2 = 0;
		while (countOfBits > 0)
		{
			int num3 = 8;
			if (countOfBits < 8)
			{
				num3 = countOfBits;
			}
			ushort num4 = ReadByte(num3);
			num4 = (ushort)(num4 << num2 * 8);
			num |= num4;
			num2++;
			countOfBits -= num3;
		}
		return num;
	}

	public uint ReadUInt32(int countOfBits)
	{
		if (countOfBits > 32 || countOfBits <= 0)
		{
			throw new ArgumentOutOfRangeException("countOfBits");
		}
		uint num = 0u;
		while (countOfBits > 0)
		{
			int num2 = 8;
			if (countOfBits < 8)
			{
				num2 = countOfBits;
			}
			num <<= num2;
			byte b = ReadByte(num2);
			num |= b;
			countOfBits -= num2;
		}
		return num;
	}

	public uint ReadUInt32Reverse(int countOfBits)
	{
		if (countOfBits > 32 || countOfBits <= 0)
		{
			throw new ArgumentOutOfRangeException("countOfBits");
		}
		uint num = 0u;
		int num2 = 0;
		while (countOfBits > 0)
		{
			int num3 = 8;
			if (countOfBits < 8)
			{
				num3 = countOfBits;
			}
			uint num4 = ReadByte(num3);
			num4 <<= num2 * 8;
			num |= num4;
			num2++;
			countOfBits -= num3;
		}
		return num;
	}

	public bool ReadBit()
	{
		return (ReadByte(1) & 1) == 1;
	}

	public byte ReadByte(int countOfBits)
	{
		if (EndOfStream)
		{
			throw new EndOfStreamException();
		}
		if (countOfBits > 8 || countOfBits <= 0)
		{
			throw new ArgumentOutOfRangeException("countOfBits");
		}
		if (countOfBits > _bufferLengthInBits)
		{
			throw new ArgumentOutOfRangeException("countOfBits");
		}
		_bufferLengthInBits -= (uint)countOfBits;
		byte result;
		if (_cbitsInPartialByte >= countOfBits)
		{
			int num = 8 - countOfBits;
			result = (byte)(_partialByte >> num);
			_partialByte = (byte)(_partialByte << countOfBits);
			_cbitsInPartialByte -= countOfBits;
		}
		else
		{
			byte b = _byteArray[_byteArrayIndex];
			_byteArrayIndex++;
			int num2 = 8 - countOfBits;
			result = (byte)(_partialByte >> num2);
			int num3 = Math.Abs(countOfBits - _cbitsInPartialByte - 8);
			result |= (byte)(b >> num3);
			_partialByte = (byte)(b << countOfBits - _cbitsInPartialByte);
			_cbitsInPartialByte = 8 - (countOfBits - _cbitsInPartialByte);
		}
		return result;
	}
}
