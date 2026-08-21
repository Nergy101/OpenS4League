using System;
using System.Collections.Generic;
using System.IO;

namespace OpenS4L.Blub.Buffers;

public class BufferStream : Stream
{
	private bool _disposed;

	private List<Buffer> _buffers;

	private int _position;

	private int _length;

	public BufferManager BufferManager { get; }

	public override bool CanRead => !_disposed;

	public override bool CanSeek => !_disposed;

	public override bool CanWrite => !_disposed;

	public override long Length => _length;

	public override long Position
	{
		get
		{
			ThrowIfDisposed();
			return _position;
		}
		set
		{
			ThrowIfDisposed();
			if (value < 0 || value > _length)
			{
				throw new ArgumentOutOfRangeException("value");
			}
			if (_position != value)
			{
				_position = (int)value;
			}
		}
	}

	public BufferStream(BufferManager bufferManager)
		: this(bufferManager, 0)
	{
	}

	public BufferStream(BufferManager bufferManager, int initialCapacity)
	{
		if (bufferManager == null)
		{
			throw new ArgumentNullException("bufferManager");
		}
		BufferManager = bufferManager;
		_length = 0;
		_position = 0;
		_buffers = new List<Buffer>(initialCapacity / bufferManager.BufferSize + 1);
	}

	public override void Flush()
	{
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		ThrowIfDisposed();
		if (offset > int.MaxValue || offset < int.MinValue)
		{
			throw new ArgumentOutOfRangeException("offset");
		}
		switch (origin)
		{
		case SeekOrigin.Begin:
			Position = (int)offset;
			break;
		case SeekOrigin.Current:
			Position = _position + (int)offset;
			break;
		case SeekOrigin.End:
			Position = _length + (int)offset;
			break;
		default:
			throw new ArgumentException("origin");
		}
		return _position;
	}

	public override void SetLength(long value)
	{
		ThrowIfDisposed();
		if (value < 0 || value > int.MaxValue)
		{
			throw new ArgumentOutOfRangeException("value");
		}
		int num = (int)value;
		EnsureCapacity(num);
		_length = num;
		if (_position > Length)
		{
			_position = num;
		}
	}

	public override int Read(byte[] array, int offset, int count)
	{
		ThrowIfDisposed();
		if (array == null)
		{
			throw new ArgumentNullException("array");
		}
		if (offset < 0)
		{
			throw new ArgumentOutOfRangeException("offset");
		}
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException("count");
		}
		if (array.Length - offset < count)
		{
			throw new ArgumentOutOfRangeException();
		}
		count = Math.Min(_length - _position, count);
		if (count <= 0)
		{
			return 0;
		}
		int num = 0;
		while (num < count)
		{
			Buffer currentBuffer = GetCurrentBuffer(out var offset2);
			int num2 = Math.Min(count - num, BufferManager.BufferSize - offset2);
			Array.Copy(currentBuffer.Array, currentBuffer.Offset + offset2, array, offset + num, num2);
			num += num2;
			_position += num2;
		}
		return count;
	}

	public override void Write(byte[] array, int offset, int count)
	{
		ThrowIfDisposed();
		if (array == null)
		{
			throw new ArgumentNullException("array");
		}
		if (offset < 0)
		{
			throw new ArgumentOutOfRangeException("offset");
		}
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException("count");
		}
		if (array.Length - offset < count)
		{
			throw new ArgumentOutOfRangeException();
		}
		int num = _position + count;
		if (num > _length)
		{
			EnsureCapacity(num);
			_length = num;
		}
		int num2 = 0;
		while (num2 < count)
		{
			Buffer currentBuffer = GetCurrentBuffer(out var offset2);
			int num3 = Math.Min(count - num2, BufferManager.BufferSize - offset2);
			Array.Copy(array, offset + num2, currentBuffer.Array, currentBuffer.Offset + offset2, num3);
			num2 += num3;
			_position += num3;
		}
	}

	public virtual byte[] ToArray()
	{
		ThrowIfDisposed();
		byte[] array = new byte[_length];
		int i = 0;
		int num = 0;
		int num2;
		for (; i < array.Length; i += num2)
		{
			num2 = Math.Min(BufferManager.BufferSize, array.Length - i);
			Buffer buffer = _buffers[num++];
			Array.Copy(buffer.Array, buffer.Offset, array, i, num2);
		}
		return array;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && !_disposed)
		{
			_disposed = true;
			foreach (Buffer buffer in _buffers)
			{
				buffer.Dispose();
			}
			_buffers = null;
		}
		base.Dispose(disposing);
	}

	private Buffer GetCurrentBuffer(out int offset)
	{
		int num = _position / BufferManager.BufferSize;
		offset = (_position - num * BufferManager.BufferSize) % BufferManager.BufferSize;
		return _buffers[num];
	}

	private void EnsureCapacity(int newCapacity)
	{
		int num = _buffers.Count * BufferManager.BufferSize;
		if (num < newCapacity)
		{
			int num2 = (newCapacity - num) / BufferManager.BufferSize + 1;
			for (int i = 0; i < num2; i++)
			{
				_buffers.Add(BufferManager.Rent());
			}
		}
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(GetType().FullName);
		}
	}
}
