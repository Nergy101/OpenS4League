using System;
using System.Collections.Generic;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;

namespace OpenS4L.Blub.DotNetty.Codecs;

public class LengthFieldBasedFrameDecoder : ByteToMessageDecoder
{
	private readonly ByteOrder byteOrder;

	private readonly int maxFrameLength;

	private readonly int lengthFieldOffset;

	private readonly int lengthFieldLength;

	private readonly int lengthFieldEndOffset;

	private readonly int lengthAdjustment;

	private readonly int initialBytesToStrip;

	private readonly bool failFast;

	private bool discardingTooLongFrame;

	private long tooLongFrameLength;

	private long bytesToDiscard;

	public LengthFieldBasedFrameDecoder(int maxFrameLength, int lengthFieldOffset, int lengthFieldLength)
		: this(maxFrameLength, lengthFieldOffset, lengthFieldLength, 0, 0)
	{
	}

	public LengthFieldBasedFrameDecoder(int maxFrameLength, int lengthFieldOffset, int lengthFieldLength, int lengthAdjustment, int initialBytesToStrip)
		: this(maxFrameLength, lengthFieldOffset, lengthFieldLength, lengthAdjustment, initialBytesToStrip, failFast: true)
	{
	}

	public LengthFieldBasedFrameDecoder(int maxFrameLength, int lengthFieldOffset, int lengthFieldLength, int lengthAdjustment, int initialBytesToStrip, bool failFast)
		: this(ByteOrder.BigEndian, maxFrameLength, lengthFieldOffset, lengthFieldLength, lengthAdjustment, initialBytesToStrip, failFast)
	{
	}

	public LengthFieldBasedFrameDecoder(ByteOrder byteOrder, int maxFrameLength, int lengthFieldOffset, int lengthFieldLength, int lengthAdjustment, int initialBytesToStrip, bool failFast)
	{
		if (maxFrameLength <= 0)
		{
			throw new ArgumentOutOfRangeException("maxFrameLength", "maxFrameLength must be a positive integer: " + maxFrameLength);
		}
		if (lengthFieldOffset < 0)
		{
			throw new ArgumentOutOfRangeException("lengthFieldOffset", "lengthFieldOffset must be a non-negative integer: " + lengthFieldOffset);
		}
		if (initialBytesToStrip < 0)
		{
			throw new ArgumentOutOfRangeException("initialBytesToStrip", "initialBytesToStrip must be a non-negative integer: " + initialBytesToStrip);
		}
		if (lengthFieldOffset > maxFrameLength - lengthFieldLength)
		{
			throw new ArgumentOutOfRangeException("maxFrameLength", "maxFrameLength (" + maxFrameLength + ") must be equal to or greater than lengthFieldOffset (" + lengthFieldOffset + ") + lengthFieldLength (" + lengthFieldLength + ").");
		}
		this.byteOrder = byteOrder;
		this.maxFrameLength = maxFrameLength;
		this.lengthFieldOffset = lengthFieldOffset;
		this.lengthFieldLength = lengthFieldLength;
		this.lengthAdjustment = lengthAdjustment;
		lengthFieldEndOffset = lengthFieldOffset + lengthFieldLength;
		this.initialBytesToStrip = initialBytesToStrip;
		this.failFast = failFast;
	}

	protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
	{
		object obj = Decode(context, input);
		if (obj != null)
		{
			output.Add(obj);
		}
	}

	protected virtual object Decode(IChannelHandlerContext context, IByteBuffer input)
	{
		if (discardingTooLongFrame)
		{
			long num = bytesToDiscard;
			int num2 = (int)Math.Min(num, input.ReadableBytes);
			input.SkipBytes(num2);
			num -= num2;
			bytesToDiscard = num;
			FailIfNecessary(firstDetectionOfTooLongFrame: false);
		}
		if (input.ReadableBytes < lengthFieldEndOffset)
		{
			return null;
		}
		int offset = input.ReaderIndex + lengthFieldOffset;
		long unadjustedFrameLength = GetUnadjustedFrameLength(input, offset, lengthFieldLength, byteOrder);
		if (unadjustedFrameLength < 0)
		{
			input.SkipBytes(lengthFieldEndOffset);
			throw new CorruptedFrameException("negative pre-adjustment length field: " + unadjustedFrameLength);
		}
		unadjustedFrameLength += lengthAdjustment + lengthFieldEndOffset;
		if (unadjustedFrameLength < lengthFieldEndOffset)
		{
			input.SkipBytes(lengthFieldEndOffset);
			throw new CorruptedFrameException("Adjusted frame length (" + unadjustedFrameLength + ") is less than lengthFieldEndOffset: " + lengthFieldEndOffset);
		}
		if (unadjustedFrameLength > maxFrameLength)
		{
			long num3 = unadjustedFrameLength - input.ReadableBytes;
			tooLongFrameLength = unadjustedFrameLength;
			if (num3 < 0)
			{
				input.SkipBytes((int)unadjustedFrameLength);
			}
			else
			{
				discardingTooLongFrame = true;
				bytesToDiscard = num3;
				input.SkipBytes(input.ReadableBytes);
			}
			FailIfNecessary(firstDetectionOfTooLongFrame: true);
			return null;
		}
		int num4 = (int)unadjustedFrameLength;
		if (input.ReadableBytes < num4)
		{
			return null;
		}
		if (initialBytesToStrip > num4)
		{
			input.SkipBytes(num4);
			throw new CorruptedFrameException("Adjusted frame length (" + unadjustedFrameLength + ") is less than initialBytesToStrip: " + initialBytesToStrip);
		}
		input.SkipBytes(initialBytesToStrip);
		int readerIndex = input.ReaderIndex;
		int num5 = num4 - initialBytesToStrip;
		IByteBuffer result = ExtractFrame(context, input, readerIndex, num5);
		input.SetReaderIndex(readerIndex + num5);
		return result;
	}

	protected virtual long GetUnadjustedFrameLength(IByteBuffer buffer, int offset, int length, ByteOrder order)
	{
		return length switch
		{
			1 => buffer.GetByte(offset), 
			2 => (order == ByteOrder.BigEndian) ? buffer.GetUnsignedShort(offset) : buffer.GetUnsignedShortLE(offset), 
			3 => (order == ByteOrder.BigEndian) ? buffer.GetUnsignedMedium(offset) : buffer.GetUnsignedMediumLE(offset), 
			4 => (order == ByteOrder.BigEndian) ? buffer.GetInt(offset) : buffer.GetIntLE(offset), 
			8 => (order == ByteOrder.BigEndian) ? buffer.GetLong(offset) : buffer.GetLongLE(offset), 
			_ => throw new DecoderException("unsupported lengthFieldLength: " + lengthFieldLength + " (expected: 1, 2, 3, 4, or 8)"), 
		};
	}

	protected virtual IByteBuffer ExtractFrame(IChannelHandlerContext context, IByteBuffer buffer, int index, int length)
	{
		IByteBuffer byteBuffer = buffer.Slice(index, length);
		byteBuffer.Retain();
		return byteBuffer;
	}

	private void FailIfNecessary(bool firstDetectionOfTooLongFrame)
	{
		if (bytesToDiscard == 0L)
		{
			long frameLength = tooLongFrameLength;
			tooLongFrameLength = 0L;
			discardingTooLongFrame = false;
			if (!failFast || (failFast & firstDetectionOfTooLongFrame))
			{
				Fail(frameLength);
			}
		}
		else if (failFast & firstDetectionOfTooLongFrame)
		{
			Fail(tooLongFrameLength);
		}
	}

	private void Fail(long frameLength)
	{
		if (frameLength > 0)
		{
			throw new TooLongFrameException("Adjusted frame length exceeds " + maxFrameLength + ": " + frameLength + " - discarded");
		}
		throw new TooLongFrameException("Adjusted frame length exceeds " + maxFrameLength + " - discarding");
	}
}
