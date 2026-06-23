# SBESharp

A modern, zero-allocation, source-generated [Simple Binary Encoding (SBE)](https://github.com/FIXTradingCommunity/fix-simple-binary-encoding) library for .NET 10+.

SBESharp parses your SBE XML schemas **at compile time** with a Roslyn source generator and emits hand-tuned C# encoders and decoders — no runtime reflection, no runtime code generation, no allocations on the hot path.

- **Source-generator first** — schemas compile into your assembly; errors surface at build time
- **Zero allocation** — decoding a message with repeating groups allocates nothing; groups are lazily decoded `ref struct` views over the original buffer
- **NativeAOT and trim safe** — no reflection, fully AOT-compatible
- **Spec compliant** — follows the FIX Trading Community SBE specification: fields → groups → varData ordering, dimension headers, constants, null sentinels, composites
- **Safe by default** — truncated or malformed buffers fail fast with `ArgumentOutOfRangeException`; decoders never read past the end of your buffer

## Installation

SBESharp ships as a single NuGet package that bundles the runtime library and the source generator:

```bash
dotnet add package SBESharp
```

## Quick start

### 1. Define a schema

Add an `.sbe.xml` file to your project — for example `Schemas/sensor.sbe.xml`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<messageSchema
  xmlns="http://fixprotocol.io/2016/sbe"
  package="Acme.Telemetry"
  id="1"
  version="0"
  byteOrder="littleEndian">

  <types>
    <composite name="messageHeader">
      <type name="blockLength" primitiveType="uint16"/>
      <type name="templateId"  primitiveType="uint16"/>
      <type name="schemaId"    primitiveType="uint16"/>
      <type name="version"     primitiveType="uint16"/>
    </composite>

    <composite name="groupSizeEncoding">
      <type name="blockLength" primitiveType="uint16"/>
      <type name="numInGroup"  primitiveType="uint8"/>
    </composite>

    <composite name="varStringEncoding">
      <type name="length"  primitiveType="uint16"/>
      <type name="varData" primitiveType="uint8" length="0" characterEncoding="UTF-8"/>
    </composite>
  </types>

  <!-- Fixed block: int64 + int32 = 12 bytes -->
  <message name="SensorUpdate" id="1" blockLength="12">
    <field name="DeviceId"   id="1" type="int64"/>
    <field name="EventCount" id="2" type="int32"/>
    <!-- Entry: double + int32 = 12 bytes -->
    <group name="Samples" id="10" dimensionType="groupSizeEncoding">
      <field name="Value" id="11" type="double"/>
      <field name="Count" id="12" type="int32"/>
    </group>
    <data name="DeviceName" id="20" type="varStringEncoding"/>
  </message>

</messageSchema>
```

### 2. Register the schema with the generator

Schemas are passed to the source generator as `AdditionalFiles` in your `.csproj`:

```xml
<ItemGroup>
  <AdditionalFiles Include="Schemas\*.sbe.xml" />
</ItemGroup>
```

That's it — build the project and the generated types appear in the namespace declared by the schema's `package` attribute (here, `Acme.Telemetry`).

### 3. Encode a message

Encoding is explicit and allocation-free: you supply the buffer, the fluent encoders write into it.

```csharp
using System.Text;
using SBESharp;
using Acme.Telemetry;

byte[] deviceName = Encoding.UTF8.GetBytes("thermo-01");

// header(8) + fixed block(12) + group header(3) + 2 entries(2 × 12) + varData(2 + name)
int totalSize = SbeMessageHeader.EncodedLength
    + SensorUpdate.SbeBlockLength
    + 3 + (2 * 12)
    + 2 + deviceName.Length;

byte[] buffer = new byte[totalSize];

// 1. Framing header
new SbeMessageHeader
{
    BlockLength = SensorUpdate.SbeBlockLength,
    TemplateId = SensorUpdate.TemplateId,
    SchemaId = SensorUpdate.SchemaId,
    Version = 0,
}.Write(buffer);

// 2. Fixed fields
int bodyOffset = SbeMessageHeader.EncodedLength;
SensorUpdateEncoder.Encode(buffer, offset: bodyOffset)
    .SetDeviceId(42L)
    .SetEventCount(2);

// 3. Repeating group — Open writes the dimension header, NextEntry advances
int groupOffset = bodyOffset + SensorUpdate.SbeBlockLength;
var samples = SensorUpdateSamplesGroupEncoder.Open(buffer, groupOffset, count: 2);
samples.SetValue(21.5).SetCount(100);
samples.NextEntry().SetValue(22.0).SetCount(200);

// 4. Variable-length data — returns the number of bytes written
int varOffset = groupOffset + 3 + (2 * 12);
SensorUpdateEncoder.WriteDeviceName(buffer, varOffset, deviceName);
```

### 4. Decode a message

Decoding is a single call. The returned value is a `ref struct` view over the buffer — fields are plain values, groups decode lazily, and nothing is copied or allocated:

```csharp
using SBESharp;
using Acme.Telemetry;

var msg = SbeSerializer.Deserialize<SensorUpdate>(buffer);

long deviceId = msg.DeviceId;
int eventCount = msg.EventCount;

// SbeGroupView<T>: zero-allocation, lazily decoded, foreach-able
foreach (var sample in msg.Samples)
{
    Console.WriteLine($"value={sample.Value} count={sample.Count}");
}

// varData is a ReadOnlySpan<byte> into the original buffer
string name = Encoding.UTF8.GetString(msg.DeviceName);
```

To dispatch on message type before decoding, read the framing header first:

```csharp
var header = SbeMessageHeader.Read(buffer);
if (header.TemplateId == SensorUpdate.TemplateId)
{
    var msg = SbeSerializer.Deserialize<SensorUpdate>(buffer);
    // ...
}
```

## What gets generated

For each `<message>` in the schema, the generator emits:

| Type | Purpose |
|------|---------|
| `{Name}` | The decoder: a `public ref struct` (or plain `struct` for messages without buffer-backed fields) implementing `ISbeDeserializable<{Name}>`, with `TemplateId`, `SchemaId`, and `SbeBlockLength` constants |
| `{Name}Encoder` | Fluent, zero-allocation field setters plus static `Write{Data}` helpers for varData |
| `{Message}{Group}Entry` | Blittable entry struct for each repeating group |
| `{Message}{Group}GroupEncoder` | Writes the group dimension header and entries |
| `{Message}{Group}Decoder` | Flyweight decoder for groups that contain nested groups or varData (`MoveNext()` / accessors) |

Enums, sets (as `[Flags]` enums), and composites (as blittable structs) are generated from the `<types>` section. Constant fields (`presence="constant"`) occupy zero wire bytes and are exposed as static properties.

The generated namespace comes verbatim from the schema's `package` attribute.

To inspect the generated code, add this to your `.csproj` and look under `obj/{Configuration}/{TFM}/generated/`:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>
```

## Core API

Everything lives in the `SBESharp` namespace:

- **`SbeSerializer.Deserialize<T>(ReadOnlySpan<byte>)`** — decodes a generated message type, skipping the 8-byte framing header.
- **`SbeSerializer.Read<T>` / `Serialize<T>`** — raw blittable struct read/write with bounds checks, for headerless payloads.
- **`SbeMessageHeader`** — the standard 8-byte SBE framing header (`BlockLength`, `TemplateId`, `SchemaId`, `Version`) with `Read`/`Write`.
- **`SbeGroupView<T>`** — zero-allocation view over a repeating group; indexer and `foreach` decode entries on demand.

Malformed input fails deterministically: a buffer that is truncated mid-block, claims more group entries than it contains, or declares a varData length past its end throws `ArgumentOutOfRangeException` — decoders never read out of bounds and never silently return partial data.

## Schema feature support

| Feature | Support |
|---------|---------|
| All SBE primitive types (`char`, `int8`–`uint64`, `float`, `double`) | ✅ |
| Fixed-length arrays (`length="N"`) | ✅ exposed as `ReadOnlySpan<T>` |
| Enums (`char`/`uint8`/`uint16`/`uint32`/`uint64` encodings) | ✅ |
| Sets / bitsets | ✅ as `[Flags]` enums |
| Composites (including nested and `<ref>`) | ✅ as blittable structs |
| Repeating groups, nested groups | ✅ |
| varData inside groups | ✅ via flyweight decoders |
| Constant fields (`presence="constant"`, `valueRef`) | ✅ zero wire bytes |
| Optional fields with null sentinels | ✅ sentinel values passed through |
| `littleEndian` and `bigEndian` byte order | ✅ see below |

## Byte order

The schema-level `byteOrder` attribute is honored for all field, group-header, and varData-length encoding.

Big-endian schemas have two current limitations:

- Composite and fixed-length-array fields **inside repeating groups** are not yet supported — the generator fails at build time with an explanatory error (they work fine at message level).
- Big-endian groups and arrays decode into heap-allocated arrays rather than zero-allocation span views.

`SbeMessageHeader` encodes little-endian. Generated decoders skip the framing header without interpreting it, so this only matters if you frame big-endian messages with the helper — write the header fields manually for fully big-endian wire formats.

## Performance

The hot path — generated `Deserialize`, fluent encoders, `SbeGroupView<T>` — is designed for low-latency messaging:

- Little-endian decode is a single `MemoryMarshal.Read` of the fixed block plus offset arithmetic; groups and varData are views into the original buffer.
- No virtual dispatch, no boxing, no closures; `ref struct` types keep everything on the stack.
- Encoding writes directly into a caller-supplied buffer; the only allocating API is the convenience `SbeSerializer.Serialize<T>(in T)` overload, which is documented as such.

### Benchmarks

Encoding and decoding the canonical SBE **Car** message (fixed block with an `Engine` composite, fixed-length arrays, an enum, a bitset, and three variable-length strings) — SBESharp versus the [Real Logic](https://github.com/real-logic/simple-binary-encoding) reference C# codec (`Org.SbeTool`), the de-facto standard SBE implementation for .NET:

| Operation | SBESharp | Real Logic | Allocated |
|-----------|---------:|-----------:|----------:|
| Encode    | **13.33 ns** | 39.37 ns | **0 B** (both) |
| Decode    | **10.66 ns** | 41.82 ns | **0 B** (both) |

SBESharp is ≈**3.0× faster** to encode and ≈**3.9× faster** to decode, and — like the reference codec — allocates nothing on the hot path.

## License

[MIT](LICENSE)
