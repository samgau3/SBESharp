using System.Text;
using SBESharp.TestSchemas.Telemetry;

namespace SBESharp.Tests.Generated;

/// <summary>
/// End-to-end tests for generated types from <c>telemetry.sbe.xml</c>.
/// Exercises minimal messages, all primitive types, multiple groups with
/// different entry sizes, uint16 enums/sets, uint32 group counts, optional
/// fields with null sentinels, constant fields inside group entries, and
/// two groups followed by message-level varData.
/// </summary>
public sealed class TelemetryTests
{
	// groupSizeEncoding: uint16 blockLength + uint8 numInGroup = 3 bytes
	private const int GroupHeaderSize = 3;

	// groupSizeEncoding32: uint16 blockLength + uint32 numInGroup = 6 bytes
	private const int Group32HeaderSize = 6;

	// varStringEncoding: uint16 length prefix = 2 bytes
	private const int VarDataLengthSize = 2;

	[Fact]
	public void PingMessage_RoundTrip()
	{
		// Arrange
		byte[] buffer = new byte[SbeMessageHeader.EncodedLength + PingMessage.SbeBlockLength];

		PingMessageEncoder.Encode(buffer, offset: SbeMessageHeader.EncodedLength)
			.SetTag(42);

		// Act
		var msg = SbeSerializer.Deserialize<PingMessage>(buffer);

		// Assert
		Assert.Equal(42, msg.Tag);
	}

	[Fact]
	public void PingMessage_WriterRoundTrip_FixedBuffer()
	{
		// Arrange
		byte[] buffer = new byte[SbeMessageHeader.EncodedLength + PingMessage.SbeBlockLength];
		var writer = new SbeMessageWriter(buffer);

		// Act
		PingMessageEncoder.Encode(ref writer).SetTag(42);
		var msg = SbeSerializer.Deserialize<PingMessage>(buffer);

		// Assert
		Assert.Equal(42, msg.Tag);
		Assert.Equal(SbeMessageHeader.EncodedLength + PingMessage.SbeBlockLength, writer.BytesWritten);
	}

	[Fact]
	public void PingMessage_WriterRoundTrip_GrowableBuffer()
	{
		// Arrange
		var output = new System.Buffers.ArrayBufferWriter<byte>();
		var writer = new SbeMessageWriter(output);

		// Act
		SbeSerializer.Encode<PingMessageEncoder>(ref writer).SetTag(42);
		var msg = SbeSerializer.Deserialize<PingMessage>(output.WrittenSpan);

		// Assert
		Assert.Equal(42, msg.Tag);
		Assert.Equal(SbeMessageHeader.EncodedLength + PingMessage.SbeBlockLength, output.WrittenCount);
	}

	[Fact]
	public void PingMessage_Constants_MatchSchema()
	{
		// Arrange / Act / Assert
		Assert.Equal(1, PingMessage.TemplateId);
		Assert.Equal(103, PingMessage.SchemaId);
		Assert.Equal(1, PingMessage.SbeBlockLength);
	}

	[Fact]
	public void SensorReadingsMessage_RoundTrip()
	{
		// Arrange
		byte[] buffer = new byte[SbeMessageHeader.EncodedLength + SensorReadingsMessage.SbeBlockLength];

		SensorReadingsMessageEncoder.Encode(buffer, offset: SbeMessageHeader.EncodedLength)
			.SetFieldU8(255)
			.SetFieldU16(65535)
			.SetFieldU32(4294967295U)
			.SetFieldU64(18446744073709551615UL)
			.SetFieldI8(-128)
			.SetFieldI16(-32768)
			.SetFieldI32(-2147483648)
			.SetFieldI64(-9223372036854775808L)
			.SetFieldFloat(3.14f)
			.SetFieldDouble(2.718281828459045);

		// Act
		var msg = SbeSerializer.Deserialize<SensorReadingsMessage>(buffer);

		// Assert
		Assert.Equal(255, msg.FieldU8);
		Assert.Equal(65535, msg.FieldU16);
		Assert.Equal(4294967295U, msg.FieldU32);
		Assert.Equal(18446744073709551615UL, msg.FieldU64);
		Assert.Equal(-128, msg.FieldI8);
		Assert.Equal(-32768, msg.FieldI16);
		Assert.Equal(-2147483648, msg.FieldI32);
		Assert.Equal(-9223372036854775808L, msg.FieldI64);
		Assert.Equal(3.14f, msg.FieldFloat);
		Assert.Equal(2.718281828459045, msg.FieldDouble);
	}

	[Fact]
	public void SensorReadingsMessage_Constants_MatchSchema()
	{
		// Arrange / Act / Assert
		Assert.Equal(2, SensorReadingsMessage.TemplateId);
		Assert.Equal(42, SensorReadingsMessage.SbeBlockLength);
	}

	[Fact]
	public void DiagnosticsMessage_ThreeGroups_AllFieldsCorrect()
	{
		// Arrange
		// Ids: 2 entries × 4 bytes = 8
		// Samples: 1 entry × 16 bytes = 16
		// Stats: 3 entries × 10 bytes = 30
		var idsCount = 2;
		var samplesCount = 1;
		var statsCount = 3;
		var idsEntrySize = 4;
		var samplesEntrySize = 16;
		var statsEntrySize = 10;

		var totalSize = SbeMessageHeader.EncodedLength
			+ DiagnosticsMessage.SbeBlockLength
			+ GroupHeaderSize + (idsCount * idsEntrySize)
			+ GroupHeaderSize + (samplesCount * samplesEntrySize)
			+ GroupHeaderSize + (statsCount * statsEntrySize);

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		DiagnosticsMessageEncoder.Encode(buffer, offset: bodyOffset)
			.SetSequenceNumber(999L)
			.SetCount(6)
			.SetUrgency(Urgency.High);

		var idsOffset = bodyOffset + DiagnosticsMessage.SbeBlockLength;
		var idsEnc = DiagnosticsMessageIdsGroupEncoder.Open(buffer, idsOffset, count: idsCount);
		idsEnc.SetId(100);
		idsEnc.NextEntry().SetId(200);

		var samplesOffset = idsOffset + GroupHeaderSize + (idsCount * idsEntrySize);
		var samplesEnc = DiagnosticsMessageSamplesGroupEncoder.Open(buffer, samplesOffset, count: samplesCount);
		samplesEnc.SetTimestamp(1700000000000000L).SetValue(99.99);

		var statsOffset = samplesOffset + GroupHeaderSize + (samplesCount * samplesEntrySize);
		var statsEnc = DiagnosticsMessageStatsGroupEncoder.Open(buffer, statsOffset, count: statsCount);
		statsEnc.SetMetric(1).SetReading(1000).SetSource(10);
		statsEnc.NextEntry().SetMetric(2).SetReading(2000).SetSource(20);
		statsEnc.NextEntry().SetMetric(3).SetReading(3000).SetSource(30);

		// Act
		var msg = SbeSerializer.Deserialize<DiagnosticsMessage>(buffer);

		// Assert — fixed fields
		Assert.Equal(999L, msg.SequenceNumber);
		Assert.Equal(6U, msg.Count);
		Assert.Equal(Urgency.High, msg.Urgency);

		// Assert — Ids group
		Assert.Equal(2, msg.Ids.Length);
		Assert.Equal(100, msg.Ids[0].Id);
		Assert.Equal(200, msg.Ids[1].Id);

		// Assert — Samples group
		Assert.Equal(1, msg.Samples.Length);
		Assert.Equal(1700000000000000L, msg.Samples[0].Timestamp);
		Assert.Equal(99.99, msg.Samples[0].Value);

		// Assert — Stats group
		Assert.Equal(3, msg.Stats.Length);
		Assert.Equal(1U, msg.Stats[0].Metric);
		Assert.Equal(1000U, msg.Stats[0].Reading);
		Assert.Equal(10, msg.Stats[0].Source);
		Assert.Equal(2U, msg.Stats[1].Metric);
		Assert.Equal(2000U, msg.Stats[1].Reading);
		Assert.Equal(20, msg.Stats[1].Source);
		Assert.Equal(3U, msg.Stats[2].Metric);
		Assert.Equal(3000U, msg.Stats[2].Reading);
		Assert.Equal(30, msg.Stats[2].Source);
	}

	[Fact]
	public void DiagnosticsMessage_WriterPath_ThreeGroups_AllFieldsCorrect()
	{
		// Arrange — no manual offset math; the writer owns the cursor
		var expectedSize = SbeMessageHeader.EncodedLength
			+ DiagnosticsMessage.SbeBlockLength
			+ GroupHeaderSize + (2 * 4)
			+ GroupHeaderSize + (1 * 16)
			+ GroupHeaderSize + (3 * 10);
		byte[] buffer = new byte[expectedSize];
		var writer = new SbeMessageWriter(buffer);

		// Act — encode
		DiagnosticsMessageEncoder.Encode(ref writer)
			.SetSequenceNumber(999L).SetCount(6).SetUrgency(Urgency.High);

		var ids = DiagnosticsMessageIdsGroupEncoder.Open(ref writer, count: 2);
		ids.SetId(100);
		ids.NextEntry().SetId(200);

		DiagnosticsMessageSamplesGroupEncoder.Open(ref writer, count: 1)
			.SetTimestamp(1700000000000000L).SetValue(99.99);

		var stats = DiagnosticsMessageStatsGroupEncoder.Open(ref writer, count: 3);
		stats.SetMetric(1).SetReading(1000).SetSource(10);
		stats.NextEntry().SetMetric(2).SetReading(2000).SetSource(20);
		stats.NextEntry().SetMetric(3).SetReading(3000).SetSource(30);

		var msg = SbeSerializer.Deserialize<DiagnosticsMessage>(buffer);

		// Assert — the cursor consumed exactly the wire layout
		Assert.Equal(expectedSize, writer.BytesWritten);

		// Assert — fixed fields
		Assert.Equal(999L, msg.SequenceNumber);
		Assert.Equal(6U, msg.Count);
		Assert.Equal(Urgency.High, msg.Urgency);

		// Assert — Ids group
		Assert.Equal(2, msg.Ids.Length);
		Assert.Equal(100, msg.Ids[0].Id);
		Assert.Equal(200, msg.Ids[1].Id);

		// Assert — Samples group
		Assert.Equal(1, msg.Samples.Length);
		Assert.Equal(1700000000000000L, msg.Samples[0].Timestamp);
		Assert.Equal(99.99, msg.Samples[0].Value);

		// Assert — Stats group
		Assert.Equal(3, msg.Stats.Length);
		Assert.Equal(1U, msg.Stats[0].Metric);
		Assert.Equal(1000U, msg.Stats[0].Reading);
		Assert.Equal(10, msg.Stats[0].Source);
		Assert.Equal(3U, msg.Stats[2].Metric);
		Assert.Equal(3000U, msg.Stats[2].Reading);
		Assert.Equal(30, msg.Stats[2].Source);
	}

	[Fact]
	public void DiagnosticsMessage_WriterPath_GrowableBuffer_ResizeSafeAcrossGroups()
	{
		// Arrange — a growable writer that starts empty; group Opens may trigger internal resizes
		var output = new System.Buffers.ArrayBufferWriter<byte>();
		var writer = new SbeMessageWriter(output);

		// Act — encode all three groups
		DiagnosticsMessageEncoder.Encode(ref writer)
			.SetSequenceNumber(999L).SetCount(6).SetUrgency(Urgency.High);
		DiagnosticsMessageIdsGroupEncoder.Open(ref writer, count: 2)
			.SetId(100).NextEntry().SetId(200);
		DiagnosticsMessageSamplesGroupEncoder.Open(ref writer, count: 1)
			.SetTimestamp(1700000000000000L).SetValue(99.99);
		DiagnosticsMessageStatsGroupEncoder.Open(ref writer, count: 1)
			.SetMetric(7).SetReading(7000).SetSource(70);

		var msg = SbeSerializer.Deserialize<DiagnosticsMessage>(output.WrittenSpan);

		// Assert — fixed fields survived any resize
		Assert.Equal(999L, msg.SequenceNumber);
		Assert.Equal(Urgency.High, msg.Urgency);

		// Assert — every group decoded from the correct position
		Assert.Equal(100, msg.Ids[0].Id);
		Assert.Equal(200, msg.Ids[1].Id);
		Assert.Equal(99.99, msg.Samples[0].Value);
		Assert.Equal(7U, msg.Stats[0].Metric);
		Assert.Equal(70, msg.Stats[0].Source);
	}

	[Fact]
	public void DiagnosticsMessage_AllGroupsEmpty_Deserializes()
	{
		// Arrange
		var totalSize = SbeMessageHeader.EncodedLength
			+ DiagnosticsMessage.SbeBlockLength
			+ GroupHeaderSize
			+ GroupHeaderSize
			+ GroupHeaderSize;

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		DiagnosticsMessageEncoder.Encode(buffer, offset: bodyOffset)
			.SetSequenceNumber(1L)
			.SetCount(0)
			.SetUrgency(Urgency.Low);

		var idsOffset = bodyOffset + DiagnosticsMessage.SbeBlockLength;
		DiagnosticsMessageIdsGroupEncoder.Open(buffer, idsOffset, count: 0);

		var samplesOffset = idsOffset + GroupHeaderSize;
		DiagnosticsMessageSamplesGroupEncoder.Open(buffer, samplesOffset, count: 0);

		var statsOffset = samplesOffset + GroupHeaderSize;
		DiagnosticsMessageStatsGroupEncoder.Open(buffer, statsOffset, count: 0);

		// Act
		var msg = SbeSerializer.Deserialize<DiagnosticsMessage>(buffer);

		// Assert
		Assert.Equal(1L, msg.SequenceNumber);
		Assert.Equal(0, msg.Ids.Length);
		Assert.Equal(0, msg.Samples.Length);
		Assert.Equal(0, msg.Stats.Length);
	}

	[Fact]
	public void StatusMessage_RoundTrip_Uint16EnumAndSet()
	{
		// Arrange
		byte[] buffer = new byte[SbeMessageHeader.EncodedLength + StatusMessage.SbeBlockLength];

		StatusMessageEncoder.Encode(buffer, offset: SbeMessageHeader.EncodedLength)
			.SetUnit(Unit.Volt)
			.SetCaps(Capabilities.Streaming | Capabilities.Recovery)
			.SetPriority(Urgency.Critical)
			.SetReading(12345.67)
			.SetCaptureTime(1700000000000000L);

		// Act
		var msg = SbeSerializer.Deserialize<StatusMessage>(buffer);

		// Assert
		Assert.Equal(Unit.Volt, msg.Unit);
		Assert.True(msg.Caps.HasFlag(Capabilities.Streaming));
		Assert.True(msg.Caps.HasFlag(Capabilities.Recovery));
		Assert.False(msg.Caps.HasFlag(Capabilities.Compression));
		Assert.Equal(Urgency.Critical, msg.Priority);
		Assert.Equal(12345.67, msg.Reading);
		Assert.Equal(1700000000000000L, msg.CaptureTime);
	}

	[Fact]
	public void EventLogMessage_FixedFieldsAndGroup_RoundTrip()
	{
		// Arrange — group + 2 varData fields with uint16 length prefix
		var sampleCount = 2;
		var sampleEntrySize = 12; // double(8) + int32(4)
		var totalSize = SbeMessageHeader.EncodedLength
			+ EventLogMessage.SbeBlockLength
			+ GroupHeaderSize + (sampleCount * sampleEntrySize)
			+ VarDataLengthSize + VarDataLengthSize; // DeviceName + FirmwareVersion empty

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		EventLogMessageEncoder.Encode(buffer, offset: bodyOffset)
			.SetDeviceId(77777L)
			.SetEventCount(500);

		var groupOffset = bodyOffset + EventLogMessage.SbeBlockLength;
		var enc = EventLogMessageSamplesGroupEncoder.Open(buffer, groupOffset, count: sampleCount);
		enc.SetValue(100.50).SetCount(200);
		enc.NextEntry().SetValue(101.25).SetCount(300);

		// Act
		var msg = SbeSerializer.Deserialize<EventLogMessage>(buffer);

		// Assert
		Assert.Equal(77777L, msg.DeviceId);
		Assert.Equal(500, msg.EventCount);
		Assert.Equal(2, msg.Samples.Length);
		Assert.Equal(100.50, msg.Samples[0].Value);
		Assert.Equal(200, msg.Samples[0].Count);
		Assert.Equal(101.25, msg.Samples[1].Value);
		Assert.Equal(300, msg.Samples[1].Count);
	}

	[Fact]
	public void SubscriptionsMessage_GroupEntriesWithEnumsAndSets()
	{
		// Arrange
		var subCount = 2;
		var subEntrySize = 9; // uint32(4) + Urgency(1) + Unit(2) + Capabilities(2)
		var totalSize = SbeMessageHeader.EncodedLength
			+ SubscriptionsMessage.SbeBlockLength
			+ GroupHeaderSize + (subCount * subEntrySize);

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		SubscriptionsMessageEncoder.Encode(buffer, offset: bodyOffset)
			.SetSessionId(42UL);

		var groupOffset = bodyOffset + SubscriptionsMessage.SbeBlockLength;
		var enc = SubscriptionsMessageSubscriptionsGroupEncoder.Open(buffer, groupOffset, count: subCount);
		enc.SetChannelId(1).SetPriority(Urgency.Normal).SetUnit(Unit.Celsius).SetFeatures(Capabilities.Streaming | Capabilities.Snapshots);
		enc.NextEntry().SetChannelId(2).SetPriority(Urgency.High).SetUnit(Unit.Kelvin).SetFeatures(Capabilities.Incremental);

		// Act
		var msg = SbeSerializer.Deserialize<SubscriptionsMessage>(buffer);

		// Assert
		Assert.Equal(42UL, msg.SessionId);
		Assert.Equal(2, msg.Subscriptions.Length);

		Assert.Equal(1U, msg.Subscriptions[0].ChannelId);
		Assert.Equal(Urgency.Normal, msg.Subscriptions[0].Priority);
		Assert.Equal(Unit.Celsius, msg.Subscriptions[0].Unit);
		Assert.True(msg.Subscriptions[0].Features.HasFlag(Capabilities.Streaming));
		Assert.True(msg.Subscriptions[0].Features.HasFlag(Capabilities.Snapshots));

		Assert.Equal(2U, msg.Subscriptions[1].ChannelId);
		Assert.Equal(Urgency.High, msg.Subscriptions[1].Priority);
		Assert.Equal(Unit.Kelvin, msg.Subscriptions[1].Unit);
		Assert.True(msg.Subscriptions[1].Features.HasFlag(Capabilities.Incremental));
		Assert.False(msg.Subscriptions[1].Features.HasFlag(Capabilities.Streaming));
	}

	[Fact]
	public void BatchMessage_Uint32NumInGroup_RoundTrip()
	{
		// Arrange
		var itemCount = 3;
		var itemEntrySize = 12; // int64(8) + int32(4)
		var totalSize = SbeMessageHeader.EncodedLength
			+ BatchMessage.SbeBlockLength
			+ Group32HeaderSize + (itemCount * itemEntrySize);

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		BatchMessageEncoder.Encode(buffer, offset: bodyOffset)
			.SetBatchId(12345L);

		var groupOffset = bodyOffset + BatchMessage.SbeBlockLength;
		var enc = BatchMessageItemsGroupEncoder.Open(buffer, groupOffset, count: itemCount);
		enc.SetItemId(1L).SetPayload(100);
		enc.NextEntry().SetItemId(2L).SetPayload(200);
		enc.NextEntry().SetItemId(3L).SetPayload(300);

		// Act
		var msg = SbeSerializer.Deserialize<BatchMessage>(buffer);

		// Assert
		Assert.Equal(12345L, msg.BatchId);
		Assert.Equal(3, msg.Items.Length);
		Assert.Equal(1L, msg.Items[0].ItemId);
		Assert.Equal(100, msg.Items[0].Payload);
		Assert.Equal(2L, msg.Items[1].ItemId);
		Assert.Equal(200, msg.Items[1].Payload);
		Assert.Equal(3L, msg.Items[2].ItemId);
		Assert.Equal(300, msg.Items[2].Payload);
	}

	[Fact]
	public void OptionalReadingsMessage_RealValues_RoundTrip()
	{
		// Arrange
		byte[] buffer = new byte[SbeMessageHeader.EncodedLength + OptionalReadingsMessage.SbeBlockLength];

		OptionalReadingsMessageEncoder.Encode(buffer, offset: SbeMessageHeader.EncodedLength)
			.SetRecordId(555L)
			.SetSampleCount(1234)
			.SetTemperature(21.5)
			.SetRetryCount(3);

		// Act
		var msg = SbeSerializer.Deserialize<OptionalReadingsMessage>(buffer);

		// Assert
		Assert.Equal(555L, msg.RecordId);
		Assert.Equal(1234, msg.SampleCount);
		Assert.Equal(21.5, msg.Temperature);
		Assert.Equal(3, msg.RetryCount);
	}

	[Fact]
	public void OptionalReadingsMessage_NullSentinels_RoundTrip()
	{
		// Arrange — SBE null sentinels: int32 → INT32 min, double → NaN, uint8 → 255.
		// SBESharp passes sentinel values through unchanged; interpreting them as
		// "null" is the caller's responsibility.
		byte[] buffer = new byte[SbeMessageHeader.EncodedLength + OptionalReadingsMessage.SbeBlockLength];

		OptionalReadingsMessageEncoder.Encode(buffer, offset: SbeMessageHeader.EncodedLength)
			.SetRecordId(556L)
			.SetSampleCount(int.MinValue)
			.SetTemperature(double.NaN)
			.SetRetryCount(255);

		// Act
		var msg = SbeSerializer.Deserialize<OptionalReadingsMessage>(buffer);

		// Assert
		Assert.Equal(556L, msg.RecordId);
		Assert.Equal(int.MinValue, msg.SampleCount);
		Assert.True(double.IsNaN(msg.Temperature));
		Assert.Equal(255, msg.RetryCount);
	}

	[Fact]
	public void CalibrationMessage_ConstantInGroupEntry_ReturnsSchemaValue()
	{
		// Arrange / Act / Assert — constants are not encoded on the wire; the
		// generated entry type exposes the schema value as a static property.
		Assert.Equal(CalibrationMode.Factory, CalibrationMessageChannelsEntry.Mode);
	}

	[Fact]
	public void CalibrationMessage_RoundTrip_ConstantExcludedFromEntryStride()
	{
		// Arrange — entry stride is int32(4) + double(8) = 12 bytes; the constant
		// Mode field contributes zero bytes to the wire format.
		var channelCount = 2;
		var channelEntrySize = 12;
		var totalSize = SbeMessageHeader.EncodedLength
			+ CalibrationMessage.SbeBlockLength
			+ GroupHeaderSize + (channelCount * channelEntrySize);

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		CalibrationMessageEncoder.Encode(buffer, offset: bodyOffset)
			.SetDeviceId(7777UL);

		var groupOffset = bodyOffset + CalibrationMessage.SbeBlockLength;
		var enc = CalibrationMessageChannelsGroupEncoder.Open(buffer, groupOffset, count: channelCount);
		enc.SetChannelId(1).SetOffset(0.25);
		enc.NextEntry().SetChannelId(2).SetOffset(-1.5);

		// Act
		var msg = SbeSerializer.Deserialize<CalibrationMessage>(buffer);

		// Assert
		Assert.Equal(7777UL, msg.DeviceId);
		Assert.Equal(2, msg.Channels.Length);
		Assert.Equal(channelEntrySize, msg.Channels.Stride);
		Assert.Equal(1, msg.Channels[0].ChannelId);
		Assert.Equal(0.25, msg.Channels[0].Offset);
		Assert.Equal(2, msg.Channels[1].ChannelId);
		Assert.Equal(-1.5, msg.Channels[1].Offset);
	}

	[Fact]
	public void SnapshotMessage_TwoGroupsThenVarData_RoundTrip()
	{
		// Arrange
		var deviceName = Encoding.UTF8.GetBytes("thermostat-01");
		var sensorCount = 2;
		var sensorEntrySize = 16; // int64(8) + double(8)
		var actuatorCount = 2;
		var actuatorEntrySize = 9; // int64(8) + uint8(1)

		var totalSize = SbeMessageHeader.EncodedLength
			+ SnapshotMessage.SbeBlockLength
			+ GroupHeaderSize + (sensorCount * sensorEntrySize)
			+ GroupHeaderSize + (actuatorCount * actuatorEntrySize)
			+ VarDataLengthSize + deviceName.Length;

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		SnapshotMessageEncoder.Encode(buffer, offset: bodyOffset)
			.SetCaptureTime(1700000000UL)
			.SetUpdateId(88UL);

		var sensorsOffset = bodyOffset + SnapshotMessage.SbeBlockLength;
		var sensorsEnc = SnapshotMessageSensorsGroupEncoder.Open(buffer, sensorsOffset, count: sensorCount);
		sensorsEnc.SetSensorId(11L).SetValue(21.5);
		sensorsEnc.NextEntry().SetSensorId(12L).SetValue(-3.75);

		var actuatorsOffset = sensorsOffset + GroupHeaderSize + (sensorCount * sensorEntrySize);
		var actuatorsEnc = SnapshotMessageActuatorsGroupEncoder.Open(buffer, actuatorsOffset, count: actuatorCount);
		actuatorsEnc.SetActuatorId(21L).SetState(1);
		actuatorsEnc.NextEntry().SetActuatorId(22L).SetState(0);

		var varOffset = actuatorsOffset + GroupHeaderSize + (actuatorCount * actuatorEntrySize);
		SnapshotMessageEncoder.WriteDeviceName(buffer, varOffset, deviceName);

		// Act
		var msg = SbeSerializer.Deserialize<SnapshotMessage>(buffer);

		// Assert — fixed fields
		Assert.Equal(1700000000UL, msg.CaptureTime);
		Assert.Equal(88UL, msg.UpdateId);

		// Assert — Sensors group
		Assert.Equal(2, msg.Sensors.Length);
		Assert.Equal(11L, msg.Sensors[0].SensorId);
		Assert.Equal(21.5, msg.Sensors[0].Value);
		Assert.Equal(12L, msg.Sensors[1].SensorId);
		Assert.Equal(-3.75, msg.Sensors[1].Value);

		// Assert — Actuators group (parsed from the offset after Sensors)
		Assert.Equal(2, msg.Actuators.Length);
		Assert.Equal(21L, msg.Actuators[0].ActuatorId);
		Assert.Equal(1, msg.Actuators[0].State);
		Assert.Equal(22L, msg.Actuators[1].ActuatorId);
		Assert.Equal(0, msg.Actuators[1].State);

		// Assert — varData positioned after both groups
		Assert.Equal("thermostat-01", Encoding.UTF8.GetString(msg.DeviceName));
	}

	[Fact]
	public void SnapshotMessage_WriterPath_GroupsThenVarData_RoundTrips()
	{
		// Arrange — fixed fields, two groups, then message-level varData, all via the writer
		var deviceName = Encoding.UTF8.GetBytes("thermostat-01");
		var output = new System.Buffers.ArrayBufferWriter<byte>();
		var writer = new SbeMessageWriter(output);

		// Act — encode
		SnapshotMessageEncoder.Encode(ref writer)
			.SetCaptureTime(1700000000UL).SetUpdateId(88UL);

		var sensors = SnapshotMessageSensorsGroupEncoder.Open(ref writer, count: 2);
		sensors.SetSensorId(11L).SetValue(21.5);
		sensors.NextEntry().SetSensorId(12L).SetValue(-3.75);

		var actuators = SnapshotMessageActuatorsGroupEncoder.Open(ref writer, count: 2);
		actuators.SetActuatorId(21L).SetState(1);
		actuators.NextEntry().SetActuatorId(22L).SetState(0);

		int varBytes = SnapshotMessageEncoder.WriteDeviceName(ref writer, deviceName);

		var msg = SbeSerializer.Deserialize<SnapshotMessage>(output.WrittenSpan);

		// Assert — varData reported its own byte count and the cursor consumed everything
		Assert.Equal(VarDataLengthSize + deviceName.Length, varBytes);
		Assert.Equal(output.WrittenCount, writer.BytesWritten);

		// Assert — fixed fields
		Assert.Equal(1700000000UL, msg.CaptureTime);
		Assert.Equal(88UL, msg.UpdateId);

		// Assert — both groups decoded from the correct positions
		Assert.Equal(11L, msg.Sensors[0].SensorId);
		Assert.Equal(-3.75, msg.Sensors[1].Value);
		Assert.Equal(21L, msg.Actuators[0].ActuatorId);
		Assert.Equal(0, msg.Actuators[1].State);

		// Assert — varData positioned after both groups
		Assert.Equal("thermostat-01", Encoding.UTF8.GetString(msg.DeviceName));
	}

	[Fact]
	public void Deserialize_BufferShorterThanMessageHeader_ThrowsArgumentOutOfRangeException()
	{
		// Arrange — 4 bytes cannot hold the 8-byte message header
		byte[] buffer = new byte[4];

		// Act / Assert
		Assert.Throws<ArgumentOutOfRangeException>(() => SbeSerializer.Deserialize<PingMessage>(buffer));
	}

	[Fact]
	public void Deserialize_TruncatedFixedBlock_ThrowsArgumentOutOfRangeException()
	{
		// Arrange — header present but only 10 of the 42 fixed-block bytes follow
		byte[] buffer = new byte[SbeMessageHeader.EncodedLength + 10];

		// Act / Assert
		Assert.Throws<ArgumentOutOfRangeException>(() => SbeSerializer.Deserialize<SensorReadingsMessage>(buffer));
	}

	[Fact]
	public void Deserialize_MissingGroupHeader_ThrowsArgumentOutOfRangeException()
	{
		// Arrange — fixed block complete but the buffer ends before the Ids group header
		byte[] buffer = new byte[SbeMessageHeader.EncodedLength + DiagnosticsMessage.SbeBlockLength];

		DiagnosticsMessageEncoder.Encode(buffer, offset: SbeMessageHeader.EncodedLength)
			.SetSequenceNumber(1L)
			.SetCount(0)
			.SetUrgency(Urgency.Low);

		// Act / Assert
		Assert.Throws<ArgumentOutOfRangeException>(() => SbeSerializer.Deserialize<DiagnosticsMessage>(buffer));
	}

	[Fact]
	public void Deserialize_GroupCountExceedsBuffer_ThrowsArgumentOutOfRangeException()
	{
		// Arrange — encode a valid message with 2 Ids entries (4 bytes each), then
		// truncate the buffer so only the group header and the first entry remain
		var idsCount = 2;
		var idsEntrySize = 4;
		var fullSize = SbeMessageHeader.EncodedLength
			+ DiagnosticsMessage.SbeBlockLength
			+ GroupHeaderSize + (idsCount * idsEntrySize)
			+ GroupHeaderSize
			+ GroupHeaderSize;

		byte[] fullBuffer = new byte[fullSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		DiagnosticsMessageEncoder.Encode(fullBuffer, offset: bodyOffset)
			.SetSequenceNumber(2L)
			.SetCount(2)
			.SetUrgency(Urgency.Normal);

		var idsOffset = bodyOffset + DiagnosticsMessage.SbeBlockLength;
		var idsEnc = DiagnosticsMessageIdsGroupEncoder.Open(fullBuffer, idsOffset, count: idsCount);
		idsEnc.SetId(100);
		idsEnc.NextEntry().SetId(200);

		var truncatedLength = idsOffset + GroupHeaderSize + idsEntrySize;
		byte[] truncated = fullBuffer[..truncatedLength];

		// Act / Assert — the header claims 2 entries but only 1 is present
		Assert.Throws<ArgumentOutOfRangeException>(() => SbeSerializer.Deserialize<DiagnosticsMessage>(truncated));
	}

	[Fact]
	public void Deserialize_VarDataLengthExceedsBuffer_ThrowsArgumentOutOfRangeException()
	{
		// Arrange — empty groups, then a DeviceName length prefix claiming 13 bytes
		// with no payload bytes behind it
		var totalSize = SbeMessageHeader.EncodedLength
			+ SnapshotMessage.SbeBlockLength
			+ GroupHeaderSize
			+ GroupHeaderSize
			+ VarDataLengthSize;

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		SnapshotMessageEncoder.Encode(buffer, offset: bodyOffset)
			.SetCaptureTime(1UL)
			.SetUpdateId(2UL);

		var sensorsOffset = bodyOffset + SnapshotMessage.SbeBlockLength;
		SnapshotMessageSensorsGroupEncoder.Open(buffer, sensorsOffset, count: 0);

		var actuatorsOffset = sensorsOffset + GroupHeaderSize;
		SnapshotMessageActuatorsGroupEncoder.Open(buffer, actuatorsOffset, count: 0);

		var varOffset = actuatorsOffset + GroupHeaderSize;
		System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(varOffset), 13);

		// Act / Assert
		Assert.Throws<ArgumentOutOfRangeException>(() => SbeSerializer.Deserialize<SnapshotMessage>(buffer));
	}

	[Fact]
	public void SnapshotMessage_EmptyGroups_VarDataStillDecodes()
	{
		// Arrange
		var deviceName = Encoding.UTF8.GetBytes("hub");

		var totalSize = SbeMessageHeader.EncodedLength
			+ SnapshotMessage.SbeBlockLength
			+ GroupHeaderSize
			+ GroupHeaderSize
			+ VarDataLengthSize + deviceName.Length;

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		SnapshotMessageEncoder.Encode(buffer, offset: bodyOffset)
			.SetCaptureTime(1UL)
			.SetUpdateId(2UL);

		var sensorsOffset = bodyOffset + SnapshotMessage.SbeBlockLength;
		SnapshotMessageSensorsGroupEncoder.Open(buffer, sensorsOffset, count: 0);

		var actuatorsOffset = sensorsOffset + GroupHeaderSize;
		SnapshotMessageActuatorsGroupEncoder.Open(buffer, actuatorsOffset, count: 0);

		var varOffset = actuatorsOffset + GroupHeaderSize;
		SnapshotMessageEncoder.WriteDeviceName(buffer, varOffset, deviceName);

		// Act
		var msg = SbeSerializer.Deserialize<SnapshotMessage>(buffer);

		// Assert
		Assert.Equal(0, msg.Sensors.Length);
		Assert.Equal(0, msg.Actuators.Length);
		Assert.Equal("hub", Encoding.UTF8.GetString(msg.DeviceName));
	}
}
