using System;
using System.IO;

namespace OpenS4L.Blub.IO;

public class NonClosingStream : Stream
{
	public Stream BaseStream { get; }

	public override bool CanRead => BaseStream.CanRead;

	public override bool CanSeek => BaseStream.CanSeek;

	public override bool CanWrite => BaseStream.CanWrite;

	public override long Length => BaseStream.Length;

	public override long Position
	{
		get
		{
			return BaseStream.Position;
		}
		set
		{
			BaseStream.Position = value;
		}
	}

	public NonClosingStream(Stream input)
	{
		if (input == null)
		{
			throw new ArgumentNullException("input");
		}
		BaseStream = input;
	}

	public override void Flush()
	{
		BaseStream.Flush();
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		return BaseStream.Seek(offset, origin);
	}

	public override void SetLength(long value)
	{
		BaseStream.SetLength(value);
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		return BaseStream.Read(buffer, offset, count);
	}

	public override void Write(byte[] buffer, int offset, int count)
	{
		BaseStream.Write(buffer, offset, count);
	}
}
