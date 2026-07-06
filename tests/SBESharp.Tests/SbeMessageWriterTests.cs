using System.Buffers;

namespace SBESharp.Tests;

/// <summary>
/// Tests for <see cref="SbeMessageWriter"/> covering both backing modes (fixed span and
/// <see cref="IBufferWriter{T}"/>), cursor advancement, header emission, and bounds validation.
/// </summary>
public sealed class SbeMessageWriterTests
{
	[Fact]
	public void GetSpanAdvance_FixedBuffer_WritesContiguouslyAndTracksBytesWritten()
	{
		// Arrange
		byte[] buffer = new byte[8];
		var writer = new SbeMessageWriter(buffer);

		// Act
		writer.GetSpan(3)[0] = 0xAA;
		writer.Advance(3);
		writer.GetSpan(2)[0] = 0xBB;
		writer.Advance(2);

		// Assert
		Assert.Equal(0xAA, buffer[0]);
		Assert.Equal(0xBB, buffer[3]);
		Assert.Equal(5, writer.BytesWritten);
	}

	[Fact]
	public void WriteHeader_FixedBuffer_EmitsReadableHeaderAndAdvances()
	{
		// Arrange
		byte[] buffer = new byte[SbeMessageHeader.EncodedLength];
		var writer = new SbeMessageWriter(buffer);

		// Act
		writer.WriteHeader(blockLength: 56, templateId: 1, schemaId: 42, version: 3);

		// Assert
		var header = SbeMessageHeader.Read(buffer);
		Assert.Equal(56, header.BlockLength);
		Assert.Equal(1, header.TemplateId);
		Assert.Equal(42, header.SchemaId);
		Assert.Equal(3, header.Version);
		Assert.Equal(SbeMessageHeader.EncodedLength, writer.BytesWritten);
	}

	[Fact]
	public void GetSpan_FixedBufferTooSmall_Throws()
	{
		// Act & Assert
		Assert.Throws<ArgumentOutOfRangeException>(static () =>
		{
			var writer = new SbeMessageWriter(new byte[4]);
			writer.GetSpan(5);
		});
	}

	[Fact]
	public void GetSpan_NegativeSize_Throws()
	{
		// Act & Assert
		Assert.Throws<ArgumentOutOfRangeException>(static () =>
		{
			var writer = new SbeMessageWriter(new byte[4]);
			writer.GetSpan(-1);
		});
	}

	[Fact]
	public void BufferWriterMode_WritesToOutputAndTracksBytesWritten()
	{
		// Arrange
		var output = new ArrayBufferWriter<byte>();
		var writer = new SbeMessageWriter(output);

		// Act
		writer.WriteHeader(blockLength: 12, templateId: 7, schemaId: 99);
		writer.GetSpan(2)[0] = 0xCD;
		writer.Advance(2);

		// Assert
		var written = output.WrittenSpan;
		Assert.Equal(SbeMessageHeader.EncodedLength + 2, writer.BytesWritten);
		Assert.Equal(written.Length, writer.BytesWritten);
		var header = SbeMessageHeader.Read(written);
		Assert.Equal(7, header.TemplateId);
		Assert.Equal(0xCD, written[SbeMessageHeader.EncodedLength]);
	}

	[Fact]
	public void BufferWriterMode_NullOutput_Throws()
	{
		// Act & Assert
		Assert.Throws<ArgumentNullException>(() => new SbeMessageWriter((IBufferWriter<byte>)null!));
	}

	[Fact]
	public void BothModes_ProduceIdenticalBytesForSameSequence()
	{
		// Arrange
		byte[] fixedBuffer = new byte[SbeMessageHeader.EncodedLength + 5];
		var output = new ArrayBufferWriter<byte>();

		// Act
		WriteSequence(new SbeMessageWriter(fixedBuffer));
		WriteSequence(new SbeMessageWriter(output));

		// Assert
		Assert.True(fixedBuffer.AsSpan().SequenceEqual(output.WrittenSpan));
	}

	[Fact]
	public void FixedBuffer_EncodingLoop_AllocatesNothing()
	{
		// Arrange — a reused buffer; encoding must not allocate on the heap
		byte[] buffer = new byte[64];

		// Warm up the JIT so first-call compilation is not counted
		EncodeInto(buffer);

		// Act — measure heap allocation across many encodes into the same buffer
		var before = GC.GetAllocatedBytesForCurrentThread();
		for (var i = 0; i < 1000; i++)
		{
			EncodeInto(buffer);
		}

		var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

		// Assert
		Assert.Equal(0, allocated);
	}

	private static int EncodeInto(byte[] buffer)
	{
		var writer = new SbeMessageWriter(buffer);
		writer.WriteHeader(blockLength: 12, templateId: 1, schemaId: 42);
		var block = writer.GetSpan(12);
		block[0] = 0x01;
		block[11] = 0x0C;
		writer.Advance(12);
		return writer.BytesWritten;
	}

	private static void WriteSequence(SbeMessageWriter writer)
	{
		writer.WriteHeader(blockLength: 5, templateId: 3, schemaId: 8, version: 1);
		var block = writer.GetSpan(5);
		block[0] = 0x01;
		block[4] = 0x05;
		writer.Advance(5);
	}
}
