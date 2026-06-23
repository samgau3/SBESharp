# SBESharp Benchmarks

Standalone [BenchmarkDotNet](https://benchmarkdotnet.org/) project comparing **SBESharp** against the
**Real Logic** reference SBE C# codec (`Org.SbeTool`) on the canonical "Car" message.

This project is intentionally **not** part of `SBESharp.slnx` and is **not** built or published by CI.
It is a developer tool you run on demand.

## Running

```bash
# Full run (most accurate, slowest)
dotnet run --project benchmarks/SBESharp.Benchmarks -c Release

# Quick run
dotnet run --project benchmarks/SBESharp.Benchmarks -c Release -- --job short

# Filter
dotnet run --project benchmarks/SBESharp.Benchmarks -c Release -- --filter '*Decode*'
```

Results are written to `BenchmarkDotNet.Artifacts/results/` (including a GitHub-flavored Markdown table).

## Fairness

Both libraries encode/decode the **same** Car schema, so the wire layout is byte-for-byte identical.
`CarBenchmarks.Setup` asserts this byte-parity before any measurement runs — if the two encoders ever
disagree, the benchmark throws rather than reporting a misleading comparison. Each side reuses its
buffers and flyweights so neither is unfairly charged for allocation.

## The competitor baseline

`Vendor/RefSbe/*.g.cs` are authentic Real Logic generated stubs (see `Vendor/RefSbe/NOTICE.md` for
provenance and license). They were produced once with the official Java tool. **Java is only needed to
regenerate them — not to build or run the benchmarks.** To regenerate (e.g. to bump the schema):

```bash
# Requires a JDK on PATH. The jar ships inside the sbe-tool NuGet package.
java -Dsbe.output.dir=generated \
     -Dsbe.generate.ir=false \
     -Dsbe.target.language="uk.co.real_logic.sbe.generation.csharp.CSharp" \
     -jar <sbe-tool>/tools/sbe-tool-all.jar \
     car-ref.sbe.xml
```

The competitor schema uses `package="RefSbe.Car"` so its generated namespace does not collide with
SBESharp's own generated `SBESharp.TestSchemas.Car`.
