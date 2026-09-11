using System.Buffers;
using System.Text;
using SBESharp.TestSchemas.Car;
using SBESharp.TestSchemas.Telemetry;

namespace SBESharp.AotSmokeTest;

/// <summary>
/// Publishes with NativeAOT and round-trips real messages through the generated codecs.
/// The unit tests prove the codecs are correct on CoreCLR; this proves the same code survives
/// ahead-of-time compilation — no reflection, no runtime code generation, no trimmed-away types —
/// which is what the package's "NativeAOT- and trim-safe" claim rests on.
/// Exits non-zero if any check fails, so CI can gate on it.
/// </summary>
internal static class Program
{
	private static int Main()
	{
		Console.WriteLine("SBESharp NativeAOT smoke test");
		Console.WriteLine();

		var failures = 0;

		Console.WriteLine("Car — writer API, composite, fixed arrays, nested group, in-group varData");
		failures += RunCarRoundTrip();
		Console.WriteLine();

		Console.WriteLine("DiagnosticsMessage — SbeGroupView<T> over simple repeating groups");
		failures += RunGroupViewRoundTrip();
		Console.WriteLine();

		Console.WriteLine("ManifestMessage — growable IBufferWriter<byte> backing");
		failures += RunBufferWriterRoundTrip();
		Console.WriteLine();

		if (failures == 0)
		{
			Console.WriteLine("All NativeAOT smoke checks passed.");
			return 0;
		}

		Console.WriteLine($"{failures} NativeAOT smoke check(s) FAILED.");
		return 1;
	}

	/// <summary>Reports one check and returns its contribution to the failure count.</summary>
	private static int Check(string what, bool passed)
	{
		Console.WriteLine($"  {(passed ? "pass" : "FAIL")}  {what}");
		return passed ? 0 : 1;
	}

	/// <summary>
	/// Encodes and decodes the canonical Car message: the full feature matrix in one message —
	/// a nested composite, fixed-length arrays, an enum, a bitset, a schema constant, two
	/// repeating groups (one with in-entry varData, one with a nested group), and three
	/// message-level varData fields.
	/// </summary>
	private static int RunCarRoundTrip()
	{
		// car.sbe.xml: groupSizeEncoding is uint16 blockLength + uint16 numInGroup;
		// varStringEncoding carries a uint32 length prefix.
		const int groupHeaderSize = 4;
		const int varDataPrefixSize = 4;
		const int fuelEntrySize = 6; // speed:uint16 + mpg:float
		const int perfEntrySize = 1; // octaneRating:uint8
		const int accelEntrySize = 6; // mph:uint16 + seconds:float

		ReadOnlySpan<byte> highway = "Highway"u8;
		ReadOnlySpan<byte> city = "City"u8;
		ReadOnlySpan<byte> manufacturer = "Honda"u8;
		ReadOnlySpan<byte> model = "Civic"u8;
		ReadOnlySpan<byte> activationCode = "XYZ"u8;

		// Size the buffer to the exact wire layout, so an encoder that writes the wrong number
		// of bytes either overruns the fixed backing or leaves BytesWritten short.
		var expectedLength = SbeMessageHeader.EncodedLength
			+ Car.SbeBlockLength
			+ groupHeaderSize
			+ fuelEntrySize + varDataPrefixSize + highway.Length
			+ fuelEntrySize + varDataPrefixSize + city.Length
			+ groupHeaderSize
			+ perfEntrySize
			+ groupHeaderSize + (2 * accelEntrySize)
			+ varDataPrefixSize + manufacturer.Length
			+ varDataPrefixSize + model.Length
			+ varDataPrefixSize + activationCode.Length;

		var buffer = new byte[expectedLength];
		var writer = new SbeMessageWriter(buffer);

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

		ReadOnlySpan<uint> someNumbers = [1, 2, 3, 4, 5];

		// Encode through the static-abstract dispatch path (ISbeMessageEncoder<TSelf>), which is
		// the construct most likely to break under AOT if generic sharing went wrong.
		SbeSerializer.Encode<CarEncoder>(ref writer)
			.SetSerialNumber(12345UL)
			.SetModelYear(2024)
			.SetAvailable(BooleanType.T)
			.SetCode(Model.B)
			.SetSomeNumbers(someNumbers)
			.SetVehicleCode("AB123C"u8)
			.SetExtras(OptionalExtras.sunRoof | OptionalExtras.cruiseControl)
			.SetEngine(engine);

		var fuel = CarFuelFiguresGroupEncoder.Open(ref writer, count: 2);
		fuel.SetSpeed(100).SetMpg(35.5f);
		CarFuelFiguresGroupEncoder.WriteUsageDescription(ref writer, highway);
		fuel.NextEntry(ref writer).SetSpeed(50).SetMpg(25.0f);
		CarFuelFiguresGroupEncoder.WriteUsageDescription(ref writer, city);

		var perf = CarPerformanceFiguresGroupEncoder.Open(ref writer, count: 1);
		perf.SetOctaneRating(95);
		var accel = perf.OpenAcceleration(ref writer, count: 2);
		accel.SetMph(30).SetSeconds(4.5f);
		accel.NextEntry().SetMph(60).SetSeconds(7.2f);

		CarEncoder.WriteManufacturer(ref writer, manufacturer);
		CarEncoder.WriteModel(ref writer, model);
		CarEncoder.WriteActivationCode(ref writer, activationCode);

		var failures = Check($"encoded exactly {expectedLength} bytes", writer.BytesWritten == expectedLength);

		var msg = SbeSerializer.Deserialize<Car>(buffer);

		failures += Check("serialNumber round-trips", msg.SerialNumber == 12345UL);
		failures += Check("modelYear round-trips", msg.ModelYear == 2024);
		failures += Check("enum field round-trips", msg.Code == Model.B);
		var extrasOk = msg.Extras.HasFlag(OptionalExtras.sunRoof)
			&& msg.Extras.HasFlag(OptionalExtras.cruiseControl)
			&& !msg.Extras.HasFlag(OptionalExtras.sportsPack);
		failures += Check("bitset field round-trips", extrasOk);
		failures += Check("fixed-length uint array round-trips", msg.SomeNumbers.Length == 5 && msg.SomeNumbers[4] == 5U);
		failures += Check("fixed-length char array round-trips", msg.VehicleCode.SequenceEqual("AB123C"u8));
		failures += Check("schema constant reads from the schema", Car.DiscountedModel == Model.C && Engine.MaxRpm == 9000);

		var engineOk = msg.Engine.Capacity == 2000
			&& msg.Engine.NumCylinders == 6
			&& msg.Engine.Efficiency == 95
			&& msg.Engine.ManufacturerCode[0] == (byte)'H';
		failures += Check("composite sub-fields round-trip", engineOk);

		var boosterOk = msg.Engine.Booster.BoostType == BoostType.TURBO && msg.Engine.Booster.HorsePower == 200;
		failures += Check("nested composite round-trips", boosterOk);

		var varDataOk = msg.Manufacturer.SequenceEqual(manufacturer)
			&& msg.Model.SequenceEqual(model)
			&& msg.ActivationCode.SequenceEqual(activationCode);
		failures += Check("message-level varData round-trips", varDataOk);

		// Groups decode through the flyweight decoders, walking the same buffer the encoders wrote.
		ReadOnlySpan<byte> body = buffer.AsSpan(SbeMessageHeader.EncodedLength);
		var fuelDecoder = new Car.CarFuelFiguresDecoder(body.Slice(Car.SbeBlockLength));
		var fuelOk = fuelDecoder.Count == 2
			&& fuelDecoder.MoveNext()
			&& fuelDecoder.Speed == 100
			&& fuelDecoder.Mpg == 35.5f
			&& fuelDecoder.GetUsageDescription().SequenceEqual(highway)
			&& fuelDecoder.MoveNext()
			&& fuelDecoder.Speed == 50
			&& fuelDecoder.GetUsageDescription().SequenceEqual(city)
			&& !fuelDecoder.MoveNext();
		failures += Check("group with in-entry varData round-trips", fuelOk);

		var fuelGroupSize = groupHeaderSize
			+ fuelEntrySize + varDataPrefixSize + highway.Length
			+ fuelEntrySize + varDataPrefixSize + city.Length;
		var perfDecoder = new Car.CarPerformanceFiguresDecoder(body.Slice(Car.SbeBlockLength + fuelGroupSize));
		var perfOk = perfDecoder.Count == 1 && perfDecoder.MoveNext() && perfDecoder.OctaneRating == 95;
		if (perfOk)
		{
			var accelDecoder = perfDecoder.GetAcceleration();
			perfOk = accelDecoder.Count == 2
				&& accelDecoder.MoveNext()
				&& accelDecoder.Mph == 30
				&& accelDecoder.Seconds == 4.5f
				&& accelDecoder.MoveNext()
				&& accelDecoder.Mph == 60
				&& accelDecoder.Seconds == 7.2f
				&& !accelDecoder.MoveNext();
		}

		failures += Check("nested group round-trips", perfOk);

		return failures;
	}

	/// <summary>
	/// Round-trips a message whose groups decode to <see cref="SbeGroupView{T}"/>. Each view is a
	/// generic instantiation over a generated entry struct, read back with
	/// <c>MemoryMarshal.Read&lt;T&gt;</c> — the path that depends on AOT having generated code for
	/// every value-type instantiation rather than falling back to shared generics.
	/// </summary>
	private static int RunGroupViewRoundTrip()
	{
		const int groupHeaderSize = 3; // telemetry groupSizeEncoding: uint16 blockLength + uint8 numInGroup
		var expectedLength = SbeMessageHeader.EncodedLength
			+ DiagnosticsMessage.SbeBlockLength
			+ groupHeaderSize + (2 * 4) // Ids: int32
			+ groupHeaderSize + (1 * 16) // Samples: int64 + double
			+ groupHeaderSize + (3 * 10); // Stats: uint32 + uint32 + uint16

		var buffer = new byte[expectedLength];
		var writer = new SbeMessageWriter(buffer);

		DiagnosticsMessageEncoder.Encode(ref writer)
			.SetSequenceNumber(999L)
			.SetCount(6)
			.SetUrgency(Urgency.High);

		var ids = DiagnosticsMessageIdsGroupEncoder.Open(ref writer, count: 2);
		ids.SetId(100);
		ids.NextEntry().SetId(200);

		DiagnosticsMessageSamplesGroupEncoder.Open(ref writer, count: 1)
			.SetTimestamp(1700000000000000L)
			.SetValue(99.99);

		var stats = DiagnosticsMessageStatsGroupEncoder.Open(ref writer, count: 3);
		stats.SetMetric(1).SetReading(1000).SetSource(10);
		stats.NextEntry().SetMetric(2).SetReading(2000).SetSource(20);
		stats.NextEntry().SetMetric(3).SetReading(3000).SetSource(30);

		var failures = Check($"encoded exactly {expectedLength} bytes", writer.BytesWritten == expectedLength);

		var msg = SbeSerializer.Deserialize<DiagnosticsMessage>(buffer);

		var fixedOk = msg.SequenceNumber == 999L && msg.Count == 6 && msg.Urgency == Urgency.High;
		failures += Check("fixed fields round-trip", fixedOk);
		failures += Check("int32 group view round-trips", msg.Ids.Length == 2 && msg.Ids[0].Id == 100 && msg.Ids[1].Id == 200);
		var samplesOk = msg.Samples.Length == 1
			&& msg.Samples[0].Timestamp == 1700000000000000L
			&& msg.Samples[0].Value == 99.99;
		failures += Check("wide-entry group view round-trips", samplesOk);

		// foreach over a group view goes through the ref struct enumerator — no boxing, and
		// nothing for the AOT compiler to devirtualize away incorrectly.
		var statsSum = 0U;
		var statsCount = 0;
		foreach (var stat in msg.Stats)
		{
			statsSum += stat.Reading;
			statsCount++;
		}

		failures += Check("foreach over a group view sees every entry", statsCount == 3 && statsSum == 6000U);

		return failures;
	}

	/// <summary>
	/// Round-trips through the growable <see cref="IBufferWriter{T}"/> backing, where the encoder
	/// never sees a pre-sized buffer, and decodes straight back out of the written span.
	/// </summary>
	private static int RunBufferWriterRoundTrip()
	{
		ReadOnlySpan<byte> payload = "payload-bytes"u8;
		ReadOnlySpan<byte> metadata = "meta"u8;
		ReadOnlySpan<byte> traceId = "trace-0001"u8;

		var output = new ArrayBufferWriter<byte>();
		var writer = new SbeMessageWriter(output);

		SbeSerializer.Encode<ManifestMessageEncoder>(ref writer)
			.SetRequestId(4242L)
			.SetVersion(3);

		ManifestMessageEncoder.WritePayload(ref writer, payload);
		ManifestMessageEncoder.WriteMetadata(ref writer, metadata);
		ManifestMessageEncoder.WriteTraceId(ref writer, traceId);

		// telemetry.sbe.xml's varStringEncoding carries a uint16 length prefix (car.sbe.xml uses uint32).
		const int varDataPrefixSize = 2;
		var expectedLength = SbeMessageHeader.EncodedLength
			+ ManifestMessage.SbeBlockLength
			+ (3 * varDataPrefixSize) + payload.Length + metadata.Length + traceId.Length;

		var lengthOk = output.WrittenCount == expectedLength && writer.BytesWritten == expectedLength;
		var failures = Check($"grew to exactly {expectedLength} bytes", lengthOk);

		var msg = SbeSerializer.Deserialize<ManifestMessage>(output.WrittenSpan);

		failures += Check("fixed fields round-trip", msg.RequestId == 4242L && msg.Version == 3);
		var manifestVarDataOk = msg.Payload.SequenceEqual(payload)
			&& msg.Metadata.SequenceEqual(metadata)
			&& msg.TraceId.SequenceEqual(traceId);
		failures += Check("all three varData fields round-trip", manifestVarDataOk);
		failures += Check("decoded text matches", Encoding.UTF8.GetString(msg.Payload) == "payload-bytes");

		return failures;
	}
}
