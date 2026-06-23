using System.Text;
using SBESharp.TestSchemas.BigEndian;

namespace SBESharp.Tests.Generated;

/// <summary>
/// End-to-end tests for generated types from <c>bigendian.sbe.xml</c>.
/// Exercises big-endian encoding of every multi-byte primitive, uint16
/// enums/sets, a message-level composite, a fixed-length array, a repeating
/// group with scalar fields, and varData — including byte-level assertions
/// that multi-byte values land big-endian on the wire.
/// </summary>
public sealed class BigEndianTests
{
	// groupSizeEncoding: uint16 blockLength + uint8 numInGroup = 3 bytes
	private const int GroupHeaderSize = 3;

	// varStringEncoding: uint16 length prefix = 2 bytes
	private const int VarDataLengthSize = 2;

	[Fact]
	public void WeatherReportMessage_AllFields_RoundTrip()
	{
		// Arrange
		byte[] buffer = new byte[SbeMessageHeader.EncodedLength + WeatherReportMessage.SbeBlockLength];

		WeatherReportMessageEncoder.Encode(buffer, offset: SbeMessageHeader.EncodedLength)
			.SetStationId(4011U)
			.SetObservationTime(1700000000000000000L)
			.SetTemperatureMilliC(-12345)
			.SetPressurePa(101325U)
			.SetHumidityPercent(87)
			.SetWindDirectionDeg(-90)
			.SetWindSpeedMps(7.25f)
			.SetDewPointC(-2.5)
			.SetSky(SkyCondition.Rain)
			.SetFlags(StationFlags.Online | StationFlags.SolarPowered)
			.SetPosition(new GeoPosition { Latitude = 467123456, Longitude = -710987654 });

		// Act
		var msg = SbeSerializer.Deserialize<WeatherReportMessage>(buffer);

		// Assert
		Assert.Equal(4011U, msg.StationId);
		Assert.Equal(1700000000000000000L, msg.ObservationTime);
		Assert.Equal(-12345, msg.TemperatureMilliC);
		Assert.Equal(101325U, msg.PressurePa);
		Assert.Equal(87, msg.HumidityPercent);
		Assert.Equal(-90, msg.WindDirectionDeg);
		Assert.Equal(7.25f, msg.WindSpeedMps);
		Assert.Equal(-2.5, msg.DewPointC);
		Assert.Equal(SkyCondition.Rain, msg.Sky);
		Assert.True(msg.Flags.HasFlag(StationFlags.Online));
		Assert.True(msg.Flags.HasFlag(StationFlags.SolarPowered));
		Assert.False(msg.Flags.HasFlag(StationFlags.Heated));
		Assert.Equal(467123456, msg.Position.Latitude);
		Assert.Equal(-710987654, msg.Position.Longitude);
	}

	[Fact]
	public void WeatherReportMessage_WireBytes_AreBigEndian()
	{
		// Arrange
		byte[] buffer = new byte[SbeMessageHeader.EncodedLength + WeatherReportMessage.SbeBlockLength];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		WeatherReportMessageEncoder.Encode(buffer, offset: bodyOffset)
			.SetStationId(0x01020304U)
			.SetObservationTime(0x1122334455667788L)
			.SetHumidityPercent(0xABCD)
			.SetSky(SkyCondition.Rain)
			.SetFlags(StationFlags.Online | StationFlags.SolarPowered)
			.SetPosition(new GeoPosition { Latitude = 0x0A0B0C0D, Longitude = 0x0E0F1011 });

		// Act / Assert — most significant byte first at each field offset
		Assert.Equal([0x01, 0x02, 0x03, 0x04], buffer[bodyOffset..(bodyOffset + 4)]);
		Assert.Equal(
			[0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88],
			buffer[(bodyOffset + 4)..(bodyOffset + 12)]);
		Assert.Equal([0xAB, 0xCD], buffer[(bodyOffset + 20)..(bodyOffset + 22)]);

		// SkyCondition.Rain = 3 as big-endian uint16
		Assert.Equal([0x00, 0x03], buffer[(bodyOffset + 36)..(bodyOffset + 38)]);

		// Online(bit 0) | SolarPowered(bit 2) = 0b101 = 5 as big-endian uint16
		Assert.Equal([0x00, 0x05], buffer[(bodyOffset + 38)..(bodyOffset + 40)]);

		// GeoPosition members are individually big-endian
		Assert.Equal([0x0A, 0x0B, 0x0C, 0x0D], buffer[(bodyOffset + 40)..(bodyOffset + 44)]);
		Assert.Equal([0x0E, 0x0F, 0x10, 0x11], buffer[(bodyOffset + 44)..(bodyOffset + 48)]);
	}

	[Fact]
	public void WindProfileMessage_ArrayGroupAndVarData_RoundTrip()
	{
		// Arrange
		var stationName = Encoding.UTF8.GetBytes("summit-ridge");
		var levelCount = 2;
		var levelEntrySize = 10; // int32(4) + float(4) + uint16(2)

		var totalSize = SbeMessageHeader.EncodedLength
			+ WindProfileMessage.SbeBlockLength
			+ GroupHeaderSize + (levelCount * levelEntrySize)
			+ VarDataLengthSize + stationName.Length;

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		WindProfileMessageEncoder.Encode(buffer, offset: bodyOffset)
			.SetStationId(900U)
			.SetGustSpeeds([1.5f, 2.25f, 3.75f, 4.125f]);

		var groupOffset = bodyOffset + WindProfileMessage.SbeBlockLength;
		var enc = WindProfileMessageLevelsGroupEncoder.Open(buffer, groupOffset, count: levelCount);
		enc.SetAltitudeM(100).SetSpeedMps(5.5f).SetDirectionDeg(270);
		enc.NextEntry().SetAltitudeM(500).SetSpeedMps(9.0f).SetDirectionDeg(315);

		var varOffset = groupOffset + GroupHeaderSize + (levelCount * levelEntrySize);
		WindProfileMessageEncoder.WriteStationName(buffer, varOffset, stationName);

		// Act
		var msg = SbeSerializer.Deserialize<WindProfileMessage>(buffer);

		// Assert — fixed fields and array
		Assert.Equal(900U, msg.StationId);
		Assert.Equal(4, msg.GustSpeeds.Length);
		Assert.Equal(1.5f, msg.GustSpeeds[0]);
		Assert.Equal(2.25f, msg.GustSpeeds[1]);
		Assert.Equal(3.75f, msg.GustSpeeds[2]);
		Assert.Equal(4.125f, msg.GustSpeeds[3]);

		// Assert — Levels group
		Assert.Equal(2, msg.Levels.Length);
		Assert.Equal(100, msg.Levels[0].AltitudeM);
		Assert.Equal(5.5f, msg.Levels[0].SpeedMps);
		Assert.Equal(270, msg.Levels[0].DirectionDeg);
		Assert.Equal(500, msg.Levels[1].AltitudeM);
		Assert.Equal(9.0f, msg.Levels[1].SpeedMps);
		Assert.Equal(315, msg.Levels[1].DirectionDeg);

		// Assert — varData after the group
		Assert.Equal("summit-ridge", Encoding.UTF8.GetString(msg.StationName));
	}

	[Fact]
	public void WindProfileMessage_GroupHeaderAndLengthPrefix_AreBigEndian()
	{
		// Arrange
		var stationName = Encoding.UTF8.GetBytes("ab");
		var totalSize = SbeMessageHeader.EncodedLength
			+ WindProfileMessage.SbeBlockLength
			+ GroupHeaderSize + 10
			+ VarDataLengthSize + stationName.Length;

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		WindProfileMessageEncoder.Encode(buffer, offset: bodyOffset)
			.SetStationId(1U)
			.SetGustSpeeds([0f, 0f, 0f, 0f]);

		var groupOffset = bodyOffset + WindProfileMessage.SbeBlockLength;
		var enc = WindProfileMessageLevelsGroupEncoder.Open(buffer, groupOffset, count: 1);
		enc.SetAltitudeM(0x01020304).SetSpeedMps(0f).SetDirectionDeg(1);

		var varOffset = groupOffset + GroupHeaderSize + 10;
		WindProfileMessageEncoder.WriteStationName(buffer, varOffset, stationName);

		// Act / Assert — group header: blockLength=10 big-endian, then numInGroup=1
		Assert.Equal([0x00, 0x0A, 0x01], buffer[groupOffset..(groupOffset + 3)]);

		// Entry field is big-endian
		Assert.Equal([0x01, 0x02, 0x03, 0x04], buffer[(groupOffset + 3)..(groupOffset + 7)]);

		// varData length prefix: 2 big-endian
		Assert.Equal([0x00, 0x02], buffer[varOffset..(varOffset + 2)]);
	}

	[Fact]
	public void WindProfileMessage_EmptyGroupAndEmptyVarData_RoundTrip()
	{
		// Arrange
		var totalSize = SbeMessageHeader.EncodedLength
			+ WindProfileMessage.SbeBlockLength
			+ GroupHeaderSize
			+ VarDataLengthSize;

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		WindProfileMessageEncoder.Encode(buffer, offset: bodyOffset)
			.SetStationId(2U)
			.SetGustSpeeds([1f, 2f, 3f, 4f]);

		var groupOffset = bodyOffset + WindProfileMessage.SbeBlockLength;
		WindProfileMessageLevelsGroupEncoder.Open(buffer, groupOffset, count: 0);

		var varOffset = groupOffset + GroupHeaderSize;
		WindProfileMessageEncoder.WriteStationName(buffer, varOffset, []);

		// Act
		var msg = SbeSerializer.Deserialize<WindProfileMessage>(buffer);

		// Assert
		Assert.Equal(2U, msg.StationId);
		Assert.Empty(msg.Levels);
		Assert.Equal(0, msg.StationName.Length);
	}
}
