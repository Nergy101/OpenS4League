using DotNetty.Buffers;

namespace OpenS4L.Blub.DotNetty;

public class WriteOnlyByteBufferStream : ByteBufferStream
{
	public override bool CanRead => false;

	public override long Position
	{
		get
		{
			ThrowIfDisposed();
			return base.Buffer.WriterIndex;
		}
		set
		{
			ThrowIfDisposed();
			base.Buffer.SetWriterIndex((int)value);
		}
	}

	public WriteOnlyByteBufferStream(IByteBuffer bytebuffer, bool releaseBuffer)
		: base(bytebuffer, releaseBuffer)
	{
	}
}
