using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenS4L.Blub.Buffers;

public class Buffer : IList<byte>, ICollection<byte>, IEnumerable<byte>, IEnumerable, IReadOnlyList<byte>, IReadOnlyCollection<byte>, IDisposable
{
	public byte[] Array { get; }

	public int Offset { get; }

	public int Count { get; }

	public BufferManager BufferManager { get; }

	internal bool IsUnused { get; set; }

	byte IReadOnlyList<byte>.this[int index] => this[index];

	public byte this[int index]
	{
		get
		{
			if (index < 0 || index >= Count)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return Array[Offset + index];
		}
		set
		{
			if (index < 0 || index >= Count)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			Array[Offset + index] = value;
		}
	}

	bool ICollection<byte>.IsReadOnly => false;

	internal Buffer(BufferManager bufferManager, byte[] array, int offset, int count)
	{
		if (bufferManager == null)
		{
			throw new ArgumentNullException("bufferManager");
		}
		if (array == null)
		{
			throw new ArgumentNullException("array");
		}
		if (offset < 0)
		{
			throw new ArgumentOutOfRangeException("count");
		}
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException("count");
		}
		if (array.Length - offset < count)
		{
			throw new ArgumentOutOfRangeException();
		}
		BufferManager = bufferManager;
		Array = array;
		Offset = offset;
		Count = count;
	}

	public void Dispose()
	{
		if (!IsUnused)
		{
			BufferManager.Return(this);
		}
	}

	int IList<byte>.IndexOf(byte item)
	{
		int num = System.Array.IndexOf(Array, item, Offset, Count);
		if (num < 0)
		{
			return -1;
		}
		return num - Offset;
	}

	void IList<byte>.Insert(int index, byte item)
	{
		throw new NotSupportedException();
	}

	void IList<byte>.RemoveAt(int index)
	{
		throw new NotSupportedException();
	}

	void ICollection<byte>.Add(byte item)
	{
		throw new NotSupportedException();
	}

	void ICollection<byte>.Clear()
	{
		throw new NotSupportedException();
	}

	bool ICollection<byte>.Contains(byte item)
	{
		return System.Array.IndexOf(Array, item, Offset, Count) >= 0;
	}

	public void CopyTo(byte[] array, int arrayIndex)
	{
		System.Array.Copy(Array, Offset, array, arrayIndex, Count);
	}

	bool ICollection<byte>.Remove(byte item)
	{
		throw new NotSupportedException();
	}

	public IEnumerator<byte> GetEnumerator()
	{
		int i = Offset;
		while (i < Offset + Count)
		{
			yield return Array[i];
			int num = i + 1;
			i = num;
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
