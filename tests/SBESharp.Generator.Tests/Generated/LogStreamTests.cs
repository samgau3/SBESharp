using System.Text;
using SBESharp.TestSchemas.LogStream;

namespace SBESharp.Tests.Generated;

/// <summary>
/// End-to-end tests for generated types from <c>logstream.sbe.xml</c>.
/// Exercises variable-length data inside repeating groups, nested groups with
/// varData, and message-level varData.
/// </summary>
public sealed class LogStreamTests
{
	// groupSizeEncoding: uint16 blockLength + uint8 numInGroup = 3 bytes
	private const int GroupHeaderSize = 3;

	// varDataEncoding: uint8 length prefix = 1 byte
	private const int VarDataLengthSize = 1;

	[Fact]
	public void MessageLevelNotes_RoundTrip()
	{
		// Arrange
		var name = Encoding.UTF8.GetBytes("Sensor");
		var value = Encoding.UTF8.GetBytes("Hello");

		var totalSize = SbeMessageHeader.EncodedLength
			+ MessageLevelNotes.SbeBlockLength
			+ VarDataLengthSize + name.Length
			+ VarDataLengthSize + value.Length;

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		MessageLevelNotesEncoder.Encode(buffer, offset: bodyOffset)
			.SetId(42L)
			.SetVersion(7U);

		var varOffset = bodyOffset + MessageLevelNotes.SbeBlockLength;
		varOffset += MessageLevelNotesEncoder.WriteName(buffer, varOffset, name);
		MessageLevelNotesEncoder.WriteValue(buffer, varOffset, value);

		// Act
		var msg = SbeSerializer.Deserialize<MessageLevelNotes>(buffer);

		// Assert
		Assert.Equal(42L, msg.Id);
		Assert.Equal(7U, msg.Version);
		Assert.Equal("Sensor", Encoding.UTF8.GetString(msg.Name));
		Assert.Equal("Hello", Encoding.UTF8.GetString(msg.Value));
	}

	[Fact]
	public void RecordWithNote_OneEntry_RoundTrip()
	{
		// Arrange
		var note = Encoding.UTF8.GetBytes("abc");
		var entryBlockLength = 16; // as declared in schema

		// header + fixedBlock(16) + groupHeader(3) + entry(16 fixed + 1+3 varData)
		var totalSize = SbeMessageHeader.EncodedLength
			+ RecordWithNote.SbeBlockLength
			+ GroupHeaderSize
			+ entryBlockLength
			+ VarDataLengthSize + note.Length;

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		RecordWithNoteEncoder.Encode(buffer, offset: bodyOffset)
			.SetStreamId(99U);

		var groupOffset = bodyOffset + RecordWithNote.SbeBlockLength;
		var enc = RecordWithNoteRecordsGroupEncoder.Open(buffer, groupOffset, count: 1);
		enc.SetTimestamp(100L).SetThreadId(200L);
		var varWriteOffset = enc.CurrentEntryEnd;
		var varBytesWritten = RecordWithNoteRecordsGroupEncoder.WriteNote(buffer, varWriteOffset, note);
		enc.NextEntry(varBytesWritten);

		// Act
		var msg = SbeSerializer.Deserialize<RecordWithNote>(buffer);

		// Assert — fixed fields
		Assert.Equal(99U, msg.StreamId);

		// Assert — group via flyweight decoder
		ReadOnlySpan<byte> body = ((ReadOnlySpan<byte>)buffer).Slice(bodyOffset);
		var decoder = new RecordWithNote.RecordWithNoteRecordsDecoder(
			body.Slice(RecordWithNote.SbeBlockLength));
		Assert.Equal(1, decoder.Count);
		Assert.True(decoder.MoveNext());
		Assert.Equal(100L, decoder.Timestamp);
		Assert.Equal(200L, decoder.ThreadId);
		Assert.Equal("abc", Encoding.UTF8.GetString(decoder.GetNote()));
		Assert.False(decoder.MoveNext());
	}

	[Fact]
	public void RecordWithNote_MultipleEntries_RoundTrip()
	{
		// Arrange
		var note1 = Encoding.UTF8.GetBytes("X");
		var note2 = Encoding.UTF8.GetBytes("YZ");
		var entryBlockLength = 16;

		var totalSize = SbeMessageHeader.EncodedLength
			+ RecordWithNote.SbeBlockLength
			+ GroupHeaderSize
			+ entryBlockLength + VarDataLengthSize + note1.Length
			+ entryBlockLength + VarDataLengthSize + note2.Length;

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		RecordWithNoteEncoder.Encode(buffer, offset: bodyOffset)
			.SetStreamId(1U);

		var groupOffset = bodyOffset + RecordWithNote.SbeBlockLength;
		var enc = RecordWithNoteRecordsGroupEncoder.Open(buffer, groupOffset, count: 2);

		// Entry 0
		enc.SetTimestamp(10L).SetThreadId(20L);
		var varWriteOffset = enc.CurrentEntryEnd;
		var written = RecordWithNoteRecordsGroupEncoder.WriteNote(buffer, varWriteOffset, note1);
		enc.NextEntry(written);

		// Entry 1
		enc.SetTimestamp(30L).SetThreadId(40L);
		varWriteOffset = enc.CurrentEntryEnd;
		written = RecordWithNoteRecordsGroupEncoder.WriteNote(buffer, varWriteOffset, note2);
		enc.NextEntry(written);

		// Act
		var msg = SbeSerializer.Deserialize<RecordWithNote>(buffer);

		// Assert
		Assert.Equal(1U, msg.StreamId);

		ReadOnlySpan<byte> body = ((ReadOnlySpan<byte>)buffer).Slice(bodyOffset);
		var decoder = new RecordWithNote.RecordWithNoteRecordsDecoder(
			body.Slice(RecordWithNote.SbeBlockLength));
		Assert.Equal(2, decoder.Count);

		Assert.True(decoder.MoveNext());
		Assert.Equal(10L, decoder.Timestamp);
		Assert.Equal(20L, decoder.ThreadId);
		Assert.Equal("X", Encoding.UTF8.GetString(decoder.GetNote()));

		Assert.True(decoder.MoveNext());
		Assert.Equal(30L, decoder.Timestamp);
		Assert.Equal(40L, decoder.ThreadId);
		Assert.Equal("YZ", Encoding.UTF8.GetString(decoder.GetNote()));

		Assert.False(decoder.MoveNext());
	}

	[Fact]
	public void RecordWithAttachments_OneEntry_RoundTrip()
	{
		// Arrange
		var note = Encoding.UTF8.GetBytes("AB");
		var attachment = Encoding.UTF8.GetBytes("CDE");
		var entryBlockLength = 16;

		var totalSize = SbeMessageHeader.EncodedLength
			+ RecordWithAttachments.SbeBlockLength
			+ GroupHeaderSize
			+ entryBlockLength
			+ VarDataLengthSize + note.Length
			+ VarDataLengthSize + attachment.Length;

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		RecordWithAttachmentsEncoder.Encode(buffer, offset: bodyOffset)
			.SetStreamId(5U);

		var groupOffset = bodyOffset + RecordWithAttachments.SbeBlockLength;
		var enc = RecordWithAttachmentsRecordsGroupEncoder.Open(buffer, groupOffset, count: 1);
		enc.SetTimestamp(111L).SetThreadId(222L);

		var varWriteOffset = enc.CurrentEntryEnd;
		varWriteOffset += RecordWithAttachmentsRecordsGroupEncoder.WriteNote(buffer, varWriteOffset, note);
		RecordWithAttachmentsRecordsGroupEncoder.WriteAttachment(buffer, varWriteOffset, attachment);
		enc.NextEntry((VarDataLengthSize + note.Length) + (VarDataLengthSize + attachment.Length));

		// Act
		var msg = SbeSerializer.Deserialize<RecordWithAttachments>(buffer);

		// Assert
		Assert.Equal(5U, msg.StreamId);

		ReadOnlySpan<byte> body = ((ReadOnlySpan<byte>)buffer).Slice(bodyOffset);
		var decoder = new RecordWithAttachments.RecordWithAttachmentsRecordsDecoder(
			body.Slice(RecordWithAttachments.SbeBlockLength));
		Assert.Equal(1, decoder.Count);
		Assert.True(decoder.MoveNext());
		Assert.Equal(111L, decoder.Timestamp);
		Assert.Equal(222L, decoder.ThreadId);
		Assert.Equal("AB", Encoding.UTF8.GetString(decoder.GetNote()));
		Assert.Equal("CDE", Encoding.UTF8.GetString(decoder.GetAttachment()));
		Assert.False(decoder.MoveNext());
	}

	[Fact]
	public void AnnotationsOnly_TwoEntries_RoundTrip()
	{
		// Arrange — entries have zero fixed fields, only 2 varData each
		var e0Key = Encoding.UTF8.GetBytes("A");
		var e0Value = Encoding.UTF8.GetBytes("BB");
		var e1Key = Encoding.UTF8.GetBytes("CCC");
		var e1Value = Encoding.UTF8.GetBytes("DDDD");

		var totalSize = SbeMessageHeader.EncodedLength
			+ AnnotationsOnly.SbeBlockLength
			+ GroupHeaderSize
			+ VarDataLengthSize + e0Key.Length + VarDataLengthSize + e0Value.Length
			+ VarDataLengthSize + e1Key.Length + VarDataLengthSize + e1Value.Length;

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		AnnotationsOnlyEncoder.Encode(buffer, offset: bodyOffset)
			.SetStreamId(77U);

		var groupOffset = bodyOffset + AnnotationsOnly.SbeBlockLength;
		var enc = AnnotationsOnlyAnnotationsGroupEncoder.Open(buffer, groupOffset, count: 2);

		// Entry 0
		var varWriteOffset = enc.CurrentEntryEnd;
		varWriteOffset += AnnotationsOnlyAnnotationsGroupEncoder.WriteKey(buffer, varWriteOffset, e0Key);
		AnnotationsOnlyAnnotationsGroupEncoder.WriteValue(buffer, varWriteOffset, e0Value);
		enc.NextEntry((VarDataLengthSize + e0Key.Length) + (VarDataLengthSize + e0Value.Length));

		// Entry 1
		varWriteOffset = enc.CurrentEntryEnd;
		varWriteOffset += AnnotationsOnlyAnnotationsGroupEncoder.WriteKey(buffer, varWriteOffset, e1Key);
		AnnotationsOnlyAnnotationsGroupEncoder.WriteValue(buffer, varWriteOffset, e1Value);
		enc.NextEntry((VarDataLengthSize + e1Key.Length) + (VarDataLengthSize + e1Value.Length));

		// Act
		var msg = SbeSerializer.Deserialize<AnnotationsOnly>(buffer);

		// Assert
		Assert.Equal(77U, msg.StreamId);

		ReadOnlySpan<byte> body = ((ReadOnlySpan<byte>)buffer).Slice(bodyOffset);
		var decoder = new AnnotationsOnly.AnnotationsOnlyAnnotationsDecoder(
			body.Slice(AnnotationsOnly.SbeBlockLength));
		Assert.Equal(2, decoder.Count);

		Assert.True(decoder.MoveNext());
		Assert.Equal("A", Encoding.UTF8.GetString(decoder.GetKey()));
		Assert.Equal("BB", Encoding.UTF8.GetString(decoder.GetValue()));

		Assert.True(decoder.MoveNext());
		Assert.Equal("CCC", Encoding.UTF8.GetString(decoder.GetKey()));
		Assert.Equal("DDDD", Encoding.UTF8.GetString(decoder.GetValue()));

		Assert.False(decoder.MoveNext());
	}

	[Fact]
	public void Deserialize_MissingGroupHeader_ThrowsArgumentOutOfRangeException()
	{
		// Arrange — fixed block complete but the buffer ends before the Records group header
		byte[] buffer = new byte[SbeMessageHeader.EncodedLength + RecordWithNote.SbeBlockLength];

		RecordWithNoteEncoder.Encode(buffer, offset: SbeMessageHeader.EncodedLength)
			.SetStreamId(1U);

		// Act / Assert — the flyweight decoder fails fast on the truncated group header
		Assert.Throws<ArgumentOutOfRangeException>(() => SbeSerializer.Deserialize<RecordWithNote>(buffer));
	}

	[Fact]
	public void Deserialize_TruncatedGroupEntry_ThrowsArgumentOutOfRangeException()
	{
		// Arrange — group header claims 1 entry of 16 bytes but only 4 entry bytes follow
		var totalSize = SbeMessageHeader.EncodedLength
			+ RecordWithNote.SbeBlockLength
			+ GroupHeaderSize
			+ 4;

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		RecordWithNoteEncoder.Encode(buffer, offset: bodyOffset)
			.SetStreamId(2U);

		var groupOffset = bodyOffset + RecordWithNote.SbeBlockLength;
		System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(groupOffset), 16);
		buffer[groupOffset + 2] = 1;

		// Act / Assert — skipping the entry reads past the truncated buffer deterministically
		Assert.Throws<ArgumentOutOfRangeException>(() => SbeSerializer.Deserialize<RecordWithNote>(buffer));
	}

	[Fact]
	public void NestedRecords_RoundTrip()
	{
		// Arrange
		// Outer entry: Timestamp(8) fixed
		//   Nested entries (2): Duration(8) + varData each
		//   Outer varData
		var spanNote0 = Encoding.UTF8.GetBytes("N0");
		var spanNote1 = Encoding.UTF8.GetBytes("N1!");
		var outerNote = Encoding.UTF8.GetBytes("outer");
		var nestedEntryBlockLength = 8;
		var outerEntryBlockLength = 8;

		var nestedTotalSize = GroupHeaderSize
			+ nestedEntryBlockLength + VarDataLengthSize + spanNote0.Length
			+ nestedEntryBlockLength + VarDataLengthSize + spanNote1.Length;

		var totalSize = SbeMessageHeader.EncodedLength
			+ NestedRecords.SbeBlockLength
			+ GroupHeaderSize // outer group header
			+ outerEntryBlockLength // outer entry fixed
			+ nestedTotalSize // nested group
			+ VarDataLengthSize + outerNote.Length; // outer varData

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		NestedRecordsEncoder.Encode(buffer, offset: bodyOffset)
			.SetStreamId(42U);

		// Outer group: 1 entry
		var groupOffset = bodyOffset + NestedRecords.SbeBlockLength;
		var outerEnc = NestedRecordsRecordsGroupEncoder.Open(buffer, groupOffset, count: 1);
		outerEnc.SetTimestamp(999L);

		// Nested group: 2 entries
		var nestedEnc = outerEnc.OpenSpans(2);
		nestedEnc.SetDuration(100L);
		var nestedVarOffset = nestedEnc.CurrentEntryEnd;
		var nestedVarWritten = NestedRecordsSpansGroupEncoder.WriteSpanNote(
			buffer, nestedVarOffset, spanNote0);
		nestedEnc.NextEntry(nestedVarWritten);

		nestedEnc.SetDuration(200L);
		nestedVarOffset = nestedEnc.CurrentEntryEnd;
		nestedVarWritten = NestedRecordsSpansGroupEncoder.WriteSpanNote(
			buffer, nestedVarOffset, spanNote1);
		nestedEnc.NextEntry(nestedVarWritten);

		// Outer varData — positioned after nested group
		var outerVarOffset = groupOffset + GroupHeaderSize + outerEntryBlockLength + nestedEnc.TotalSize;
		var outerVarWritten = NestedRecordsRecordsGroupEncoder.WriteNote(
			buffer, outerVarOffset, outerNote);
		outerEnc.NextEntry(nestedEnc.TotalSize + outerVarWritten);

		// Act
		var msg = SbeSerializer.Deserialize<NestedRecords>(buffer);

		// Assert — fixed fields
		Assert.Equal(42U, msg.StreamId);

		// Assert — outer group via flyweight decoder
		ReadOnlySpan<byte> body = ((ReadOnlySpan<byte>)buffer).Slice(bodyOffset);
		var outerDec = new NestedRecords.NestedRecordsRecordsDecoder(
			body.Slice(NestedRecords.SbeBlockLength));
		Assert.Equal(1, outerDec.Count);
		Assert.True(outerDec.MoveNext());
		Assert.Equal(999L, outerDec.Timestamp);

		// Assert — nested group
		var nestedDec = outerDec.GetSpans();
		Assert.Equal(2, nestedDec.Count);

		Assert.True(nestedDec.MoveNext());
		Assert.Equal(100L, nestedDec.Duration);
		Assert.Equal("N0", Encoding.UTF8.GetString(nestedDec.GetSpanNote()));

		Assert.True(nestedDec.MoveNext());
		Assert.Equal(200L, nestedDec.Duration);
		Assert.Equal("N1!", Encoding.UTF8.GetString(nestedDec.GetSpanNote()));

		Assert.False(nestedDec.MoveNext());

		// Assert — outer varData
		Assert.Equal("outer", Encoding.UTF8.GetString(outerDec.GetNote()));
	}
}
