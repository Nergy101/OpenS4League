using DotNetty.Buffers;

namespace OpenS4L.Blub.DotNetty;

public class ReadOnlyByteBufferStream : ByteBufferStream
{
	public override bool CanWrite => false;

	public override long Position
	{
		get
		{
			ThrowIfDisposed();
			return base.Buffer.ReaderIndex;
		}
		set
		{
			ThrowIfDisposed();
			base.Buffer.SetReaderIndex((int)value);
		}
	}

	public ReadOnlyByteBufferStream(IByteBuffer bytebuffer, bool releaseBuffer)
		: base(bytebuffer, releaseBuffer)
	{
	}
}
