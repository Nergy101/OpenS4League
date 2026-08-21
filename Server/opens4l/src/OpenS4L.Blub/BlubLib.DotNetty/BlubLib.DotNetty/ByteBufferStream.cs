using System;
using System.IO;
using DotNetty.Buffers;
using DotNetty.Common.Utilities;

namespace OpenS4L.Blub.DotNetty;

public abstract class ByteBufferStream : Stream
{
	private readonly bool _releaseBuffer;

	private bool _disposed;

	public IByteBuffer Buffer { get; }

	public override bool CanRead => !_disposed;

	public override bool CanSeek => !_disposed;

	public override bool CanWrite => !_disposed;

	public override long Length => Buffer.Capacity;

	protected ByteBufferStream(IByteBuffer bytebuffer, bool releaseBuffer)
	{
		if (bytebuffer == null)
		{
			throw new ArgumentNullException("bytebuffer");
		}
		Buffer = bytebuffer;
		_releaseBuffer = releaseBuffer;
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
			Position += (int)offset;
			break;
		case SeekOrigin.End:
			Position = Length + (int)offset;
			break;
		default:
			throw new ArgumentException("origin");
		}
		return Position;
	}

	public override void SetLength(long value)
	{
		ThrowIfDisposed();
		if (value < 0 || value > int.MaxValue)
		{
			throw new ArgumentOutOfRangeException("value");
		}
		int num = (int)value;
		Buffer.AdjustCapacity(num);
		if (Position > Length)
		{
			Position = num;
		}
	}

	public override int Read(byte[] array, int offset, int count)
	{
		ThrowIfDisposed();
		if (!CanRead)
		{
			throw new InvalidOperationException("Stream is write only");
		}
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
		count = Math.Min(Buffer.ReadableBytes, count);
		if (count <= 0)
		{
			return 0;
		}
		Buffer.ReadBytes(array, offset, count);
		return count;
	}

	public override void Write(byte[] array, int offset, int count)
	{
		ThrowIfDisposed();
		if (!CanWrite)
		{
			throw new InvalidOperationException("Stream is read only");
		}
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
		long num = Position + count;
		if (num > Length)
		{
			Buffer.AdjustCapacity((int)num);
		}
		Buffer.WriteBytes(array, offset, count);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && !_disposed)
		{
			_disposed = true;
			if (_releaseBuffer)
			{
				Buffer.SafeRelease();
			}
		}
		base.Dispose(disposing);
	}

	protected void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(GetType().FullName);
		}
	}
}
