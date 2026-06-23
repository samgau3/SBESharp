# Contributing to SBESharp

Thanks for your interest in contributing! SBESharp is a zero-allocation,
source-generated [Simple Binary Encoding](https://github.com/FIXTradingCommunity/fix-simple-binary-encoding)
library for .NET 10+. This document explains how to get set up and what we
expect from contributions.

## Getting Started

You'll need the **.NET 10 SDK** or newer.

```bash
# Build
dotnet build

# Run tests
dotnet test --no-build -c Release

# Verify formatting
dotnet format --verify-no-changes --no-restore
```

## Project Layout

| Project | Purpose |
| --- | --- |
| `src/SBESharp/` | Core runtime: serialization, deserialization, message header |
| `src/SBESharp.Roslyn/` | IR models, schema parsing, type resolution, source writer |
| `src/SBESharp.Generator/` | Roslyn source generator that emits C# at compile time |
| `tests/` | Unit and round-trip tests for each project |

## Guidelines

- **SBE spec compliance is non-negotiable.** All parsing, code generation, and
  test schemas must conform to the FIX SBE specification. Never invent
  non-standard features or orderings.
- **Zero allocation on the hot path.** Encode/decode must not allocate. Back any
  performance claim with a reproducible BenchmarkDotNet benchmark.
- **NativeAOT- and trim-safe.** No runtime reflection or runtime codegen.
- **Style.** Tabs for indentation. StyleCop is enforced and warnings are treated
  as errors — `dotnet format` must report no changes.
- **Tests.** New features and bug fixes need test coverage. Prefer a round-trip
  test (encode → decode → assert) using a schema under `tests/.../TestSchemas/`.

## Submitting Changes

1. Fork the repo and create a branch from `main`.
2. Make your change with tests and confirm `dotnet build`, `dotnet test`, and
   `dotnet format --verify-no-changes` all pass.
3. Open a pull request describing the change and the motivation. Link any
   related issue.

## Reporting Bugs & Requesting Features

Please use the issue templates. For bugs, include the schema (or a minimal
reproduction), the expected vs. actual behavior, and your .NET SDK version.

## License

By contributing, you agree that your contributions will be licensed under the
[MIT License](LICENSE) that covers this project.
