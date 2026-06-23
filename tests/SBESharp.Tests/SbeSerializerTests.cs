using System.Runtime.InteropServices;

namespace SBESharp.Tests;

/// <summary>
/// Tests for <see cref="SbeSerializer"/> covering round-trips, all overloads,
/// buffer-size guards, and explicit-layout structs.
/// </summary>
public sealed class SbeSerializerTests
{
	[Fact]
	public void Read_FromByteArray_ReturnsExpectedValue()
	{
		// Value=42 (int LE): 2A 00 00 00 | Timestamp=100 (long LE): 64 00 00 00 00 00 00 00
		byte[] bytes = [0x2A, 0x00, 0x00, 0x00, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

		SimpleMessage msg = SbeSerializer.Read<SimpleMessage>(bytes);

		Assert.Equal(42, msg.Value);
		Assert.Equal(100L, msg.Timestamp);
	}

	[Fact]
	public void Read_FromReadOnlyMemory_ReturnsExpectedValue()
	{
		ReadOnlyMemory<byte> memory = new byte[] { 0x2A, 0x00, 0x00, 0x00, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

		SimpleMessage msg = SbeSerializer.Read<SimpleMessage>(memory);

		Assert.Equal(42, msg.Value);
		Assert.Equal(100L, msg.Timestamp);
	}

	[Fact]
	public void Read_FromReadOnlySpan_ReturnsExpectedValue()
	{
		ReadOnlySpan<byte> span = new byte[] { 0x2A, 0x00, 0x00, 0x00, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

		SimpleMessage msg = SbeSerializer.Read<SimpleMessage>(span);

		Assert.Equal(42, msg.Value);
		Assert.Equal(100L, msg.Timestamp);
	}

	[Fact]
	public void Read_BufferTooSmall_ThrowsArgumentOutOfRangeException()
	{
		byte[] tooSmall = new byte[4]; // SimpleMessage requires 12 bytes

		Assert.Throws<ArgumentOutOfRangeException>(() => SbeSerializer.Read<SimpleMessage>(tooSmall));
	}

	[Fact]
	public void Serialize_IntoSpan_WritesExpectedBytes()
	{
		SimpleMessage msg = new SimpleMessage { Value = 1, Timestamp = 1L };
		byte[] buffer = new byte[12];

		SbeSerializer.Serialize(in msg, buffer);

		// Value=1 LE, Timestamp=1 LE
		Assert.Equal([0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00], buffer);
	}

	[Fact]
	public void Serialize_SpanTooSmall_ThrowsArgumentOutOfRangeException()
	{
		SimpleMessage msg = new SimpleMessage { Value = 1, Timestamp = 1L };
		byte[] tooSmall = new byte[4]; // SimpleMessage requires 12 bytes

		Assert.Throws<ArgumentOutOfRangeException>(() => SbeSerializer.Serialize(in msg, tooSmall));
	}

	[Fact]
	public void Serialize_ToNewByteArray_ReturnsAllZeroesForDefaultStruct()
	{
		SimpleMessage msg = new SimpleMessage { Value = 0, Timestamp = 0L };

		byte[] result = SbeSerializer.Serialize(in msg);

		Assert.Equal(12, result.Length);
		Assert.All(result, b => Assert.Equal(0, b));
	}

	[Fact]
	public void SerializeRead_RoundTrip_PreservesAllFields()
	{
		TelemetryMessage original = new TelemetryMessage
		{
			DeviceId = 123456789L,
			CaptureTime = 1700000000000000000L,
			MinReading = 4999.75,
			MaxReading = 5000.25,
			SampleCount = 100,
			ErrorCount = 200,
		};

		byte[] encoded = SbeSerializer.Serialize(in original);
		TelemetryMessage decoded = SbeSerializer.Read<TelemetryMessage>(encoded);

		Assert.Equal(original.DeviceId, decoded.DeviceId);
		Assert.Equal(original.CaptureTime, decoded.CaptureTime);
		Assert.Equal(original.MinReading, decoded.MinReading);
		Assert.Equal(original.MaxReading, decoded.MaxReading);
		Assert.Equal(original.SampleCount, decoded.SampleCount);
		Assert.Equal(original.ErrorCount, decoded.ErrorCount);
	}

	[Fact]
	public void SizeOf_ReturnsExpectedByteSize()
	{
		Assert.Equal(12, SbeSerializer.SizeOf<SimpleMessage>());
		Assert.Equal(SbeMessageHeader.EncodedLength, SbeSerializer.SizeOf<SbeMessageHeader>());
	}

	[Fact]
	public void Read_ExplicitLayoutStruct_ReadsFieldsFromCorrectOffsets()
	{
		// MsgType=7 (ushort LE): 07 00 | Sequence=42 (int LE): 2A 00 00 00 | Timestamp=999 (long LE)
		byte[] bytes = [0x07, 0x00, 0x2A, 0x00, 0x00, 0x00, 0xE7, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

		ExplicitLayoutMessage msg = SbeSerializer.Read<ExplicitLayoutMessage>(bytes);

		Assert.Equal(7, msg.MsgType);
		Assert.Equal(42, msg.Sequence);
		Assert.Equal(999L, msg.Timestamp);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	private struct SimpleMessage
	{
		public int Value;
		public long Timestamp;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	private struct TelemetryMessage
	{
		public long DeviceId;
		public long CaptureTime;
		public double MinReading;
		public double MaxReading;
		public int SampleCount;
		public int ErrorCount;
	}

	[StructLayout(LayoutKind.Explicit, Pack = 1)]
	private struct ExplicitLayoutMessage
	{
		[FieldOffset(0)]
		public ushort MsgType;

		[FieldOffset(2)]
		public int Sequence;

		[FieldOffset(6)]
		public long Timestamp;
	}
}
