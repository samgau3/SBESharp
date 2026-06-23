---
paths:
  - "src/SBESharp/**/*.cs"
  - "src/SBESharp.Generator/Emission/**/*.cs"
---

# Performance Rules

## Scope

These rules apply to two locations:
- `src/SBESharp/` — the core library runs at application runtime; every byte counts.
- `src/SBESharp.Generator/Emission/` — the emitter defines the shape of generated code. Any pattern emitted here becomes consumer runtime code and must be zero-allocation.

`src/SBESharp.Generator/Parsing/` and `src/SBESharp.Generator/Ir/` run at compile time only — they may allocate freely.

## Performance (Hot-path rules)

Hot path is the decoder and encoder: `SbeSerializer`, generated `Deserialize()`, and generated encoders.
Make these as fast as possible and keep heap allocation as low as possible.

- **Zero allocation in steady state as much as possible.** No `new` on reference types, no closures, no string interpolation or boxing
- **Prefer `Span<T>` and `Memory<T>`** over array, collections and IEnumerable<T>
- **Prefer value types** `struct` over `class`, `ReadOnlySpan<char>` over `string`
- **Use `ArrayPool<T>` and buffer reuse** - no ad-hoc `new byte[]` in hot paths.
- **No virtual dispatch** - prefer static dispatch, sealed classes generic specialization.

## Design

- **Composition over inheritance.**
- **Functional style:** pure functions, immutability, pattern matching, expression-bodies members.
- **Explicit over implicit.** no hidden allocation, implicit conversions, or surprising side effects.


## Policy

- Benchmark before and after any change that touches the hot path
- All performance claims must be backed by reproducible BenchmarkDotNet benchmarks
