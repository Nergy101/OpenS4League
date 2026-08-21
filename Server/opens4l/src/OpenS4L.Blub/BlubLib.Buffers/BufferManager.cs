using System;
using System.Collections.Concurrent;

namespace OpenS4L.Blub.Buffers;

public class BufferManager
{
	private readonly ConcurrentStack<Buffer> _bufferPool = new ConcurrentStack<Buffer>();

	public static BufferManager Default { get; set; } = new BufferManager(1024, 1024, canGrow: true);

	public bool CanGrow { get; }

	public int BufferSize { get; }

	public int BufferCount { get; }

	public int AvailableBuffers => _bufferPool.Count;

	public BufferManager(int bufferSize, int bufferCount, bool canGrow)
	{
		if (bufferSize < 0)
		{
			throw new ArgumentOutOfRangeException("bufferSize");
		}
		if (bufferCount < 0)
		{
			throw new ArgumentOutOfRangeException("bufferCount");
		}
		BufferSize = bufferSize;
		BufferCount = bufferCount;
		CanGrow = true;
		AllocateNewBuffers();
		CanGrow = canGrow;
	}

	public Buffer Rent()
	{
		if (_bufferPool.TryPop(out var result))
		{
			result.IsUnused = false;
			return result;
		}
		if (CanGrow)
		{
			lock (_bufferPool)
			{
				if (_bufferPool.TryPop(out result))
				{
					result.IsUnused = false;
					return result;
				}
				AllocateNewBuffers();
			}
			while (!_bufferPool.TryPop(out result))
			{
			}
			result.IsUnused = false;
			return result;
		}
		throw new OutOfMemoryException("No buffers available and BufferManager is not allowed to allocate new memory. Set canGrow to true or increase buffer count");
	}

	public void Return(Buffer buffer)
	{
		if (buffer.BufferManager != this)
		{
			throw new ArgumentException("The buffer is not owned by this BufferManager", "buffer");
		}
		_bufferPool.Push(buffer);
		buffer.IsUnused = true;
	}

	private void AllocateNewBuffers()
	{
		if (!CanGrow)
		{
			throw new OutOfMemoryException("BufferManager is not allowed to allocate new memory. Set canGrow to true or increase buffer count");
		}
		byte[] array = new byte[BufferSize * BufferCount];
		for (int i = 0; i < BufferCount; i++)
		{
			int offset = i * BufferSize;
			_bufferPool.Push(new Buffer(this, array, offset, BufferSize));
		}
	}
}
