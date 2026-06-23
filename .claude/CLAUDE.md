# CLAUDE.md — SBE Sharp

## Commands

**Build the repository**:
```bash
dotnet build
```

**Run tests**:
```bash
dotnet test --no-build -c Release
```

**Verify code formatting**:
```bash
dotnet format --verify-no-changes --no-restore
```

**Validate generated output** (after any SourceFormatter or emission-related change):
1. Build before changes: `dotnet build -c Release`
2. Capture baseline checksums:
   ```bash
   find tests/SBESharp.Generator.Tests/obj/Release/net10.0/generated -name "*.cs" -exec md5 {} \;
   ```
3. Make changes, rebuild, and compare checksums — they must match exactly.

---

## File Structure

```
src/SBESharp/               — Core library: serialization, deserialization, and SBE message header
src/SBESharp.Roslyn/        — Shared Roslyn helper library: IR models, parsing, type resolution, SourceWriter
src/SBESharp.Generator/     — Roslyn source generator: links all .cs from SBESharp.Roslyn (PolyType pattern), emits C# code at compile time
tests/SBESharp.Tests/       — Unit tests for the core library (SbeSerializer, SbeMessageHeader)
tests/SBESharp.Roslyn.Tests/ — Unit tests for SBESharp.Roslyn (parser, path helpers)
tests/SBESharp.Generator.Tests/ — Unit tests for the generator (SourceFormatter, real-world schema round-trips)
```

---

## Project Vision

A modern, zero-allocation, source-generated **Simple Binary Encoding (SBE)** library for .NET 10+.

The goal is to be the definitive SBE implementation for the .NET ecosystem — one that feels native to modern C#, is NativeAOT compatible, and performs at the level expected in low-latency infrastructure.

---

## NuGet Package

SBESharp ships as a single NuGet package for SBE serialization/deserialization. The package bundles the core runtime library and the source generator.

## Core Constraints

- **SBE specification compliance** — all parsing, code generation, and test schemas must conform to the [FIX SBE specification](https://github.com/FIXTradingCommunity/fix-simple-binary-encoding). See `rules/sbe-spec.md` for the key rules.
- **Source-generator first** — SBE XML schemas are parsed at compile time; no runtime reflection, no runtime codegen
- **NativeAOT and trim safe** — must work in fully AOT-compiled applications
- **Zero allocation on the hot path** — see `rules/performance.md`
- **Developer experience** — see `rules/developer-experience.md`

---

## Inspiration & References

- **[Nerdbank.MessagePack](https://github.com/AArnott/Nerdbank.MessagePack)** — API design, source generator pattern, `[GenerateShape]` approach
- **[SBE Specification](https://github.com/FIXTradingCommunity/fix-simple-binary-encoding)** — wire format compliance
- **[Real Logic SBE (Java)](https://github.com/real-logic/simple-binary-encoding)** — the canonical reference implementation
- **[LMAX Disruptor](https://lmax-exchange.github.io/disruptor/)** — philosophy on low-latency design
- **BenchmarkDotNet** — all performance claims must be backed by reproducible benchmarks
