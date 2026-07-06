# Changelog

All notable changes to SBESharp are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0-preview.1]

### Added

- **Writer-based encoding API** — a new `SbeMessageWriter` cursor owns the write position so
  callers no longer compute byte offsets or pre-size buffers by hand. It targets either a fixed
  `Memory<byte>` (zero allocation) or a growable `IBufferWriter<byte>`, and exposes `BytesWritten`
  as the exact encoded length.
- `SbeSerializer.Encode<TEncoder>(ref SbeMessageWriter)` — the encode-side counterpart to
  `Deserialize<T>`, and the `ISbeMessageEncoder<TSelf>` interface it dispatches through.
- Writer-based overloads on every generated encoder, covering the full feature matrix:
  - `{Message}Encoder.Encode(ref SbeMessageWriter)` writes the framing header and fixed block.
  - `{Group}GroupEncoder.Open(ref SbeMessageWriter, count)` for simple, nested, and
    variable-length repeating groups, with `NextEntry(ref SbeMessageWriter)` on complex groups.
  - `Write{Data}(ref SbeMessageWriter, value)` for message-level and in-group variable-length data.

All additions are backward compatible: the existing offset-based encoder overloads
(`Encode(Span<byte>, offset)`, `Open(Span<byte>, offset, count)`, `Write…(Span<byte>, offset, value)`)
remain for manual buffer layout and `stackalloc` scenarios.

### Performance

- The writer path is zero-allocation on the fixed-buffer backing (verified by unit test and by the
  `[MemoryDiagnoser]` Car benchmark) and encodes the canonical Car message faster than the Real Logic
  reference codec. The offset-based encoders remain the lowest-latency path.

## [0.1.0-preview.1]

### Added

- Initial release: zero-allocation, source-generated SBE for .NET 10+. Compile-time code generation
  from SBE XML schemas, NativeAOT- and trim-safe. Decoders, offset-based encoders, repeating groups
  (including nested and variable-length data), enums, sets, composites, constant fields, and
  little- and big-endian byte order.
