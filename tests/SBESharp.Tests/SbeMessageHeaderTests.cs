namespace SBESharp.Tests;

/// <summary>
/// Tests for <see cref="SbeMessageHeader"/> covering wire decoding, round-trip encoding,
/// and boundary validation.
/// </summary>
public sealed class SbeMessageHeaderTests
{
	[Fact]
	public void Read_ParsesAllFields()
	{
		// blockLength=56 (0x38 0x00 LE), templateId=1, schemaId=42 (0x2A), version=3
		byte[] bytes = [0x38, 0x00, 0x01, 0x00, 0x2A, 0x00, 0x03, 0x00];

		SbeMessageHeader header = SbeMessageHeader.Read(bytes);

		Assert.Equal(56, header.BlockLength);
		Assert.Equal(1, header.TemplateId);
		Assert.Equal(42, header.SchemaId);
		Assert.Equal(3, header.Version);
	}

	[Fact]
	public void Write_ProducesLittleEndianBytes()
	{
		SbeMessageHeader header = new SbeMessageHeader
		{
			BlockLength = 56,
			TemplateId = 1,
			SchemaId = 42,
			Version = 3,
		};
		byte[] destination = new byte[SbeMessageHeader.EncodedLength];

		header.Write(destination);

		Assert.Equal([0x38, 0x00, 0x01, 0x00, 0x2A, 0x00, 0x03, 0x00], destination);
	}

	[Fact]
	public void ReadWrite_RoundTrip_PreservesAllFields()
	{
		SbeMessageHeader original = new SbeMessageHeader
		{
			BlockLength = 512,
			TemplateId = 99,
			SchemaId = 7,
			Version = 2,
		};
		byte[] buffer = new byte[SbeMessageHeader.EncodedLength];
		original.Write(buffer);

		SbeMessageHeader decoded = SbeMessageHeader.Read(buffer);

		Assert.Equal(original.BlockLength, decoded.BlockLength);
		Assert.Equal(original.TemplateId, decoded.TemplateId);
		Assert.Equal(original.SchemaId, decoded.SchemaId);
		Assert.Equal(original.Version, decoded.Version);
	}

	[Fact]
	public void Read_BufferTooSmall_ThrowsArgumentOutOfRangeException()
	{
		byte[] tooSmall = new byte[SbeMessageHeader.EncodedLength - 1];

		Assert.Throws<ArgumentOutOfRangeException>(() => SbeMessageHeader.Read(tooSmall));
	}

	[Fact]
	public void Write_BufferTooSmall_ThrowsArgumentOutOfRangeException()
	{
		SbeMessageHeader header = default;
		byte[] tooSmall = new byte[SbeMessageHeader.EncodedLength - 1];

		Assert.Throws<ArgumentOutOfRangeException>(() => header.Write(tooSmall));
	}

	[Fact]
	public void EncodedLength_IsEightBytes()
	{
		Assert.Equal(8, SbeMessageHeader.EncodedLength);
	}
}
