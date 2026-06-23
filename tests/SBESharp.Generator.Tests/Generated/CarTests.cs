using System.Text;
using SBESharp.TestSchemas.Car;

namespace SBESharp.Tests.Generated;

/// <summary>
/// End-to-end tests for generated types from <c>car.sbe.xml</c>.
/// Exercises composites, constants, fixed-length arrays, nested groups,
/// and variable-length data at both message and group levels.
/// </summary>
public sealed class CarTests
{
	// car.sbe.xml groupSizeEncoding: uint16 blockLength + uint16 numInGroup = 4 bytes
	private const int GroupHeaderSize = 4;

	// varStringEncoding: uint32 length prefix = 4 bytes
	private const int VarDataLengthSize = 4;

	[Fact]
	public void Car_Constants_MatchSchema()
	{
		// Arrange / Act / Assert
		Assert.Equal(1, Car.TemplateId);
		Assert.Equal(102, Car.SchemaId);
		Assert.Equal(49, Car.SbeBlockLength);
	}

	[Fact]
	public void Car_DiscountedModel_ReturnsConstant()
	{
		// Arrange / Act / Assert
		Assert.Equal(Model.C, Car.DiscountedModel);
	}

	[Fact]
	public void Model_CharEnum_HasExpectedValues()
	{
		// Arrange / Act / Assert
		Assert.Equal(65, (byte)Model.A);
		Assert.Equal(66, (byte)Model.B);
		Assert.Equal(67, (byte)Model.C);
	}

	[Fact]
	public void OptionalExtras_HasExpectedBitPositions()
	{
		// Arrange / Act / Assert
		Assert.Equal(0b001, (byte)OptionalExtras.sunRoof);
		Assert.Equal(0b010, (byte)OptionalExtras.sportsPack);
		Assert.Equal(0b100, (byte)OptionalExtras.cruiseControl);
	}

	[Fact]
	public void Car_FixedFields_RoundTrip()
	{
		// Arrange
		// Fixed block (49) + empty fuelFigures group (4) + empty perfFigures group (4)
		// + 3 empty varData (3 × 4 byte length prefix)
		var totalSize = SbeMessageHeader.EncodedLength
			+ Car.SbeBlockLength
			+ GroupHeaderSize
			+ GroupHeaderSize
			+ (3 * VarDataLengthSize);

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		var someNumbers = new uint[] { 1, 2, 3, 4, 5 };
		var vehicleCode = Encoding.ASCII.GetBytes("AB123C");

		CarEncoder.Encode(buffer, offset: bodyOffset)
			.SetSerialNumber(12345UL)
			.SetModelYear(2024)
			.SetAvailable(BooleanType.T)
			.SetCode(Model.B)
			.SetSomeNumbers(someNumbers)
			.SetVehicleCode(vehicleCode)
			.SetExtras(OptionalExtras.sunRoof | OptionalExtras.cruiseControl);

		// Write empty groups
		var fuelGroupOffset = bodyOffset + Car.SbeBlockLength;
		CarFuelFiguresGroupEncoder.Open(buffer, fuelGroupOffset, count: 0);

		var perfGroupOffset = fuelGroupOffset + GroupHeaderSize;
		CarPerformanceFiguresGroupEncoder.Open(buffer, perfGroupOffset, count: 0);

		// Write empty varData
		var varOffset = perfGroupOffset + GroupHeaderSize;
		varOffset += CarEncoder.WriteManufacturer(buffer, varOffset, []);
		varOffset += CarEncoder.WriteModel(buffer, varOffset, []);
		CarEncoder.WriteActivationCode(buffer, varOffset, []);

		// Act
		var msg = SbeSerializer.Deserialize<Car>(buffer);

		// Assert
		Assert.Equal(12345UL, msg.SerialNumber);
		Assert.Equal(2024, msg.ModelYear);
		Assert.Equal(BooleanType.T, msg.Available);
		Assert.Equal(Model.B, msg.Code);
		Assert.Equal(5, msg.SomeNumbers.Length);
		Assert.Equal(1U, msg.SomeNumbers[0]);
		Assert.Equal(5U, msg.SomeNumbers[4]);
		Assert.Equal(6, msg.VehicleCode.Length);
		Assert.Equal((byte)'A', msg.VehicleCode[0]);
		Assert.Equal((byte)'C', msg.VehicleCode[5]);
		Assert.True(msg.Extras.HasFlag(OptionalExtras.sunRoof));
		Assert.True(msg.Extras.HasFlag(OptionalExtras.cruiseControl));
		Assert.False(msg.Extras.HasFlag(OptionalExtras.sportsPack));
	}

	[Fact]
	public void Car_VarData_RoundTrip()
	{
		// Arrange
		var manufacturer = Encoding.UTF8.GetBytes("Honda");
		var model = Encoding.UTF8.GetBytes("Civic");
		var activationCode = Encoding.UTF8.GetBytes("XYZ");

		var totalSize = SbeMessageHeader.EncodedLength
			+ Car.SbeBlockLength
			+ GroupHeaderSize
			+ GroupHeaderSize
			+ VarDataLengthSize + manufacturer.Length
			+ VarDataLengthSize + model.Length
			+ VarDataLengthSize + activationCode.Length;

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		CarEncoder.Encode(buffer, offset: bodyOffset)
			.SetSerialNumber(1UL)
			.SetModelYear(2025)
			.SetAvailable(BooleanType.F)
			.SetCode(Model.A)
			.SetSomeNumbers(new uint[5])
			.SetVehicleCode(new byte[6])
			.SetExtras(OptionalExtras.None);

		// Empty groups
		var fuelGroupOffset = bodyOffset + Car.SbeBlockLength;
		CarFuelFiguresGroupEncoder.Open(buffer, fuelGroupOffset, count: 0);

		var perfGroupOffset = fuelGroupOffset + GroupHeaderSize;
		CarPerformanceFiguresGroupEncoder.Open(buffer, perfGroupOffset, count: 0);

		// Write varData
		var varOffset = perfGroupOffset + GroupHeaderSize;
		varOffset += CarEncoder.WriteManufacturer(buffer, varOffset, manufacturer);
		varOffset += CarEncoder.WriteModel(buffer, varOffset, model);
		CarEncoder.WriteActivationCode(buffer, varOffset, activationCode);

		// Act
		var msg = SbeSerializer.Deserialize<Car>(buffer);

		// Assert
		Assert.Equal("Honda", Encoding.UTF8.GetString(msg.Manufacturer));
		Assert.Equal("Civic", Encoding.UTF8.GetString(msg.Model));
		Assert.Equal("XYZ", Encoding.UTF8.GetString(msg.ActivationCode));
	}

	[Fact]
	public void Car_FuelFigures_WithUsageDescription_RoundTrip()
	{
		// Arrange
		// fuelFigures: 2 entries, blockLength = 6 (speed:uint16 + mpg:float),
		// each followed by a usageDescription varData (uint32 length prefix).
		var desc0 = Encoding.UTF8.GetBytes("Highway");
		var desc1 = Encoding.UTF8.GetBytes("City");

		var totalSize = SbeMessageHeader.EncodedLength
			+ Car.SbeBlockLength
			+ GroupHeaderSize // fuelFigures header
			+ 6 + VarDataLengthSize + desc0.Length // entry 0: fixed + varData
			+ 6 + VarDataLengthSize + desc1.Length // entry 1: fixed + varData
			+ GroupHeaderSize // performanceFigures (empty)
			+ (3 * VarDataLengthSize); // 3 empty message-level varData

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		CarEncoder.Encode(buffer, offset: bodyOffset)
			.SetSerialNumber(7UL)
			.SetModelYear(2025)
			.SetAvailable(BooleanType.T)
			.SetCode(Model.A)
			.SetSomeNumbers(new uint[5])
			.SetVehicleCode(new byte[6])
			.SetExtras(OptionalExtras.None);

		// fuelFigures: 2 entries, each with a usageDescription varData
		var fuelGroupOffset = bodyOffset + Car.SbeBlockLength;
		var fuelEnc = CarFuelFiguresGroupEncoder.Open(buffer, fuelGroupOffset, count: 2);

		fuelEnc.SetSpeed(100).SetMpg(35.5f);
		var varWriteOffset = fuelEnc.CurrentEntryEnd;
		var written = CarFuelFiguresGroupEncoder.WriteUsageDescription(buffer, varWriteOffset, desc0);
		fuelEnc.NextEntry(written);

		fuelEnc.SetSpeed(50).SetMpg(25.0f);
		varWriteOffset = fuelEnc.CurrentEntryEnd;
		written = CarFuelFiguresGroupEncoder.WriteUsageDescription(buffer, varWriteOffset, desc1);
		fuelEnc.NextEntry(written);

		// Empty performanceFigures after fuelFigures
		var perfGroupOffset = fuelGroupOffset + fuelEnc.TotalSize;
		CarPerformanceFiguresGroupEncoder.Open(buffer, perfGroupOffset, count: 0);

		// Empty message-level varData
		var varOffset = perfGroupOffset + GroupHeaderSize;
		varOffset += CarEncoder.WriteManufacturer(buffer, varOffset, []);
		varOffset += CarEncoder.WriteModel(buffer, varOffset, []);
		CarEncoder.WriteActivationCode(buffer, varOffset, []);

		// Act
		var msg = SbeSerializer.Deserialize<Car>(buffer);

		// Assert — fixed fields and message-level varData still parse past the populated group
		Assert.Equal(7UL, msg.SerialNumber);
		Assert.True(msg.Manufacturer.IsEmpty);
		Assert.True(msg.Model.IsEmpty);
		Assert.True(msg.ActivationCode.IsEmpty);

		// Assert — fuelFigures entries via flyweight decoder
		ReadOnlySpan<byte> body = ((ReadOnlySpan<byte>)buffer).Slice(bodyOffset);
		var fuelDecoder = new Car.CarFuelFiguresDecoder(body.Slice(Car.SbeBlockLength));
		Assert.Equal(2, fuelDecoder.Count);

		Assert.True(fuelDecoder.MoveNext());
		Assert.Equal(100, fuelDecoder.Speed);
		Assert.Equal(35.5f, fuelDecoder.Mpg);
		Assert.Equal("Highway", Encoding.UTF8.GetString(fuelDecoder.GetUsageDescription()));

		Assert.True(fuelDecoder.MoveNext());
		Assert.Equal(50, fuelDecoder.Speed);
		Assert.Equal(25.0f, fuelDecoder.Mpg);
		Assert.Equal("City", Encoding.UTF8.GetString(fuelDecoder.GetUsageDescription()));

		Assert.False(fuelDecoder.MoveNext());
	}

	[Fact]
	public void Car_Engine_Composite_RoundTrip()
	{
		// Arrange
		// Fixed block (49, includes the 10-byte Engine composite) + 2 empty groups + 3 empty varData.
		var totalSize = SbeMessageHeader.EncodedLength
			+ Car.SbeBlockLength
			+ GroupHeaderSize
			+ GroupHeaderSize
			+ (3 * VarDataLengthSize);

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		var engine = new Engine
		{
			Capacity = 2000,
			NumCylinders = 6,
			Efficiency = 95,
			BoosterEnabled = BooleanType.T,
			Booster = new Booster { BoostType = BoostType.TURBO, HorsePower = 200 },
		};
		engine.ManufacturerCode[0] = (byte)'H';
		engine.ManufacturerCode[1] = (byte)'O';
		engine.ManufacturerCode[2] = (byte)'N';

		CarEncoder.Encode(buffer, offset: bodyOffset)
			.SetSerialNumber(1UL)
			.SetModelYear(2025)
			.SetAvailable(BooleanType.T)
			.SetCode(Model.A)
			.SetSomeNumbers(new uint[5])
			.SetVehicleCode(new byte[6])
			.SetExtras(OptionalExtras.None)
			.SetEngine(engine);

		// Empty groups and varData so the message is well-formed
		var fuelGroupOffset = bodyOffset + Car.SbeBlockLength;
		CarFuelFiguresGroupEncoder.Open(buffer, fuelGroupOffset, count: 0);
		var perfGroupOffset = fuelGroupOffset + GroupHeaderSize;
		CarPerformanceFiguresGroupEncoder.Open(buffer, perfGroupOffset, count: 0);
		var varOffset = perfGroupOffset + GroupHeaderSize;
		varOffset += CarEncoder.WriteManufacturer(buffer, varOffset, []);
		varOffset += CarEncoder.WriteModel(buffer, varOffset, []);
		CarEncoder.WriteActivationCode(buffer, varOffset, []);

		// Act
		var msg = SbeSerializer.Deserialize<Car>(buffer);

		// Assert — composite sub-fields decode to typed values
		Assert.Equal(2000, msg.Engine.Capacity);
		Assert.Equal(6, msg.Engine.NumCylinders);
		Assert.Equal((byte)'H', msg.Engine.ManufacturerCode[0]);
		Assert.Equal((byte)'O', msg.Engine.ManufacturerCode[1]);
		Assert.Equal((byte)'N', msg.Engine.ManufacturerCode[2]);
		Assert.Equal(95, msg.Engine.Efficiency);
		Assert.Equal(BooleanType.T, msg.Engine.BoosterEnabled);

		// Assert — nested Booster composite
		Assert.Equal(BoostType.TURBO, msg.Engine.Booster.BoostType);
		Assert.Equal(200, msg.Engine.Booster.HorsePower);

		// Assert — embedded constants occupy zero wire bytes and come from the schema
		Assert.Equal(9000, Engine.MaxRpm);
		Assert.Equal("Petrol", Engine.Fuel);
	}

	[Fact]
	public void Car_PerformanceFigures_NestedAcceleration_RoundTrip()
	{
		// Arrange
		// performanceFigures: 1 entry, blockLength = 1 byte (octaneRating: uint8)
		// nested acceleration: 2 entries, blockLength = 6 bytes (mph:uint16 + seconds:float)
		var accelCount = 2;

		var totalSize = SbeMessageHeader.EncodedLength
			+ Car.SbeBlockLength
			+ GroupHeaderSize // fuelFigures (empty)
			+ GroupHeaderSize // performanceFigures header
			+ 1 // perfEntry fixed block (octaneRating)
			+ GroupHeaderSize // nested acceleration header
			+ (accelCount * 6) // accel entries (mph:2 + seconds:4)
			+ (3 * VarDataLengthSize); // 3 empty varData

		byte[] buffer = new byte[totalSize];
		var bodyOffset = SbeMessageHeader.EncodedLength;

		CarEncoder.Encode(buffer, offset: bodyOffset)
			.SetSerialNumber(1UL)
			.SetModelYear(2025)
			.SetAvailable(BooleanType.T)
			.SetCode(Model.A)
			.SetSomeNumbers(new uint[5])
			.SetVehicleCode(new byte[6])
			.SetExtras(OptionalExtras.None);

		// Empty fuelFigures
		var fuelGroupOffset = bodyOffset + Car.SbeBlockLength;
		CarFuelFiguresGroupEncoder.Open(buffer, fuelGroupOffset, count: 0);

		// performanceFigures: 1 entry
		var perfGroupOffset = fuelGroupOffset + GroupHeaderSize;
		var perfEnc = CarPerformanceFiguresGroupEncoder.Open(buffer, perfGroupOffset, count: 1);
		perfEnc.SetOctaneRating(95);

		// nested acceleration: 2 entries via helper
		var accelEnc = perfEnc.OpenAcceleration(accelCount);
		accelEnc.SetMph(30).SetSeconds(4.5f);
		accelEnc.NextEntry().SetMph(60).SetSeconds(7.2f);

		// Advance past this entry
		perfEnc.NextEntry(accelEnc.TotalSize);

		// Write empty varData after all groups
		var varOffset = perfGroupOffset + perfEnc.TotalSize;
		varOffset += CarEncoder.WriteManufacturer(buffer, varOffset, []);
		varOffset += CarEncoder.WriteModel(buffer, varOffset, []);
		CarEncoder.WriteActivationCode(buffer, varOffset, []);

		// Act — deserialize fixed fields + varData
		var msg = SbeSerializer.Deserialize<Car>(buffer);

		// Assert — fixed fields deserialize correctly
		Assert.Equal(1UL, msg.SerialNumber);
		Assert.Equal(2025, msg.ModelYear);

		// Assert — decode performanceFigures via flyweight decoder
		ReadOnlySpan<byte> body = ((ReadOnlySpan<byte>)buffer).Slice(bodyOffset);
		var decoderOffset = Car.SbeBlockLength + GroupHeaderSize; // past fuelFigures (empty)
		var perfDecoder = new Car.CarPerformanceFiguresDecoder(body.Slice(decoderOffset));
		Assert.Equal(1, perfDecoder.Count);
		Assert.True(perfDecoder.MoveNext());
		Assert.Equal(95, perfDecoder.OctaneRating);

		// Assert — nested acceleration via flyweight decoder
		var accelDecoder = perfDecoder.GetAcceleration();
		Assert.Equal(2, accelDecoder.Count);
		Assert.True(accelDecoder.MoveNext());
		Assert.Equal(30, accelDecoder.Mph);
		Assert.Equal(4.5f, accelDecoder.Seconds);
		Assert.True(accelDecoder.MoveNext());
		Assert.Equal(60, accelDecoder.Mph);
		Assert.Equal(7.2f, accelDecoder.Seconds);
		Assert.False(accelDecoder.MoveNext());
	}
}
