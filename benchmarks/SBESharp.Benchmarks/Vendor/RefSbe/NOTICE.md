# Vendored Real Logic SBE generated code

The `*.g.cs` files in this directory are **generated code from the Real Logic Simple Binary
Encoding tool** (the reference SBE implementation), used solely as the comparison baseline for
the benchmarks in this repository. They are not part of the SBESharp library and are not shipped
in the `SBESharp` NuGet package.

- **Source project:** https://github.com/real-logic/simple-binary-encoding
- **Generator:** `sbe-tool-all.jar` from the `sbe-tool` NuGet package, version **1.23.1.1**
- **License:** Apache-2.0 (Copyright Bill Segall, MarketFactory Inc, Adaptive Consulting)
- **Runtime dependency:** `Org.SbeTool.Sbe.Dll` (the `sbe-tool` NuGet package's `SBE.dll`)

## How these were generated

From the Car schema (with `package="RefSbe.Car"` so the namespace does not collide with
SBESharp's own generated `SBESharp.TestSchemas.Car`):

```bash
java -Dsbe.output.dir=generated \
     -Dsbe.generate.ir=false \
     -Dsbe.target.language="uk.co.real_logic.sbe.generation.csharp.CSharp" \
     -jar sbe-tool-all.jar \
     car-ref.sbe.xml
```

Java is required only to regenerate these files; it is not needed to build or run the
benchmarks, and the Java tool is never invoked by the build or by CI.
