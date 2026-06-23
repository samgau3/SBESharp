---
paths:
  - "tests/**/*.cs"
---

# Testing Rules

## Frameworks & Conventions

- xUnit for all tests — no MSTest, no NUnit
- One test class per production class
- Test method names follow the pattern: `MethodName_Scenario_ExpectedResult`
- Never use `// ── Section ───` comment separators inside test classes — they add noise with no value.
- Add Arrange, Act and Assert comments in the test body to define the 3 steps.

## Assertions

- **xUnit2013**: Use `Assert.Empty(collection)` not `Assert.Equal(0, collection.Count)`
- Use `Assert.Equal`, `Assert.True`, `Assert.Throws` — avoid `Assert.IsType` when possible
- One logical assertion per test; use multiple `Assert` calls only when they verify the same outcome

## Project Structure

- `tests/SBESharp.Tests/` — tests for the core library only; no generator references
- `tests/SBESharp.Generator.Tests/` — tests for parser, emitter, and generated code round-trips
- `TestSchemas/` — real `.sbe.xml` files used as `AdditionalFiles`; do not add synthetic schemas unless testing a specific edge case
- `Generated/` — round-trip tests against generator output; tests here encode then decode and assert field values

## Generator Test Project Setup

- Two `ProjectReference` entries for `SBESharp.Generator`: one normal (for unit-testing APIs), one as `OutputItemType="Analyzer"` (for running the generator on `TestSchemas`)
- `EmitCompilerGeneratedFiles=true` — generated files are visible on disk at `obj/Debug/net10.0/generated/...`

## Generated Round-Trip Tests (MANDATORY)

- Every repeating group in a test schema **must** have a round-trip test that encodes entries with distinct, non-zero field values and asserts every field after deserialization
- When a message has **multiple groups**, at least one test must encode **all groups** with real data and assert every field in every group — this is the only way to catch sequential-offset bugs where the second group is parsed from the wrong position
- Buffer sizes in tests must match the actual wire layout: `header + fixedBlock + (groupHeaderSize + count * entrySize) per group` — never hardcode magic numbers without a comment explaining the calculation
- Group header sizes depend on the schema's `dimensionType` composite — do not assume 3 bytes

## StyleCop in Tests

- Test helper methods go at the bottom of the class (SA1202: public before private)
- No `_` discard variables in `foreach` — use a real name (SA1312)
