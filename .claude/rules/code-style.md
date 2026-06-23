---
paths:
  - "**/*.cs"
---

# Code Style Rules

## Language & Syntax

- **C# 14**, file-scoped namespaces, nullable enabled everywhere (`<Nullable>enable</Nullable>`)
- Use `var` to infer type — never declare explicit types: `var name = "sbe"` not `string name = "sbe"`
- Tabs for indentation throughout — never spaces
- `readonly record struct` for value types where appropriate
- `ref struct` for reader/writer types that must stay stack-allocated
- Prefer `static` methods and `readonly` fields to minimize instance state
- XML doc comments on all public API surface

## StyleCop Rules (enforced, TreatWarningsAsErrors=true)

- **SA1512**: No blank line after a single-line `//` comment
- **SA1515**: Single-line `//` comment following code must be preceded by a blank line
- **SA1202**: Public members before private in the same class
- **SA1117**: All parameters on one line OR each on its own line — no mixing
- **SA1414**: Named tuple elements required
- **SA1027**: No mixed tabs and spaces — use tab-only indentation inside raw strings
- **SA1312**: Variable names must start with lowercase — avoid `_` discard in foreach; use a real name
- **SA1407**: Mixed arithmetic needs explicit parentheses: `(2 * 12)`
- **SA1309**: Disabled — private fields use `_camelCase` prefix
- **SA1134**: Attributes must be on their own line — `[FieldOffset(0)]` on a separate line from the field
- **SA1101**: Must prefix instance member access with `this.` — e.g. `this._writer.WriteLine(...)`
- **SA1649**: Disabled in `.editorconfig` — avoids awkward backtick names for generic type files

## Analyzer Rules

- **CA1051**: No public instance fields — use properties
- **CA1859**: Private methods should return concrete type (`List<T>` not `IReadOnlyList<T>`)
- **CA1711**: Type names must not end in `Enum` — use `EnumDefinition` suffix instead
- **CA1062**: Public methods need null guards (`ArgumentNullException.ThrowIfNull`) for reference-type params
- **CS1574**: Don't use `<see cref="X"/>` in generated doc comments if X is a field on a ref struct — use `<c>X</c>`

## Namespaces

- Namespaces do **not** follow folder structure — subdirectories are for file organization only
- Example: `SourceFormatter/SourceFormatter.cs` uses `namespace SBESharp.Generator;`, not `SBESharp.Generator.SourceFormatter`
- Same for tests: `SourceFormatter/SourceFormatterTests.cs` uses `namespace SBESharp.Generator.Tests;`, not `SBESharp.Generator.Tests.SourceFormatter`

## Package Management

- Versions only in `Directory.Packages.props` — never in `<PackageReference>` in `.csproj` files
- No `InternalsVisibleTo` anywhere — all generator IR/parser/emitter types are public

## Struct Layout

- Use `Pack = 1` with `[StructLayout(Explicit)]` to match SBE dense wire format
- Without `Pack = 1`, the CLR adds alignment padding (e.g. a struct with `double` gets size 16 not 12)
