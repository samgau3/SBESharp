---
paths:
  - "src/**/*.cs"
  - "src/**/*.csproj"
---

# Developer Experience Rules

This is an open-source NuGet package. Every decision must account for the developer consuming it, not just the internals.

## Error Messages

- **Error messages are part of the API** — when the generator fails (bad schema, missing element, unsupported type), the `FormatException` message is what the developer sees at build time. Make it actionable: name the element, name the attribute, say what was expected.
- As the generator evolves, prefer Roslyn `Diagnostic` over raw exceptions where possible, so errors surface as proper build warnings/errors with file and line context.

## Schema Parsing — Fail Fast

- **Never silently ignore malformed input.** A schema with a missing required element, an unrecognized type, or an invalid value must throw `FormatException` at the point of the error — not return `null` and let code generation fail later with a confusing message.
- No silent skipping. No nullable returns to "handle later". No `TryParse → null → filter downstream` patterns.

## Public API & Backward Compatibility

- **Public API surface is a contract** — think carefully before adding or changing public types, members, or generated code shape. Consumers depend on generated type names, method signatures, and struct layouts.
- **Backward compatibility is a hard constraint** — once a public type, member, or generated code shape is released, changing or removing it is a breaking change. Prefer additive changes. When a breaking change is unavoidable, it requires a major version bump and a clear migration guide.

## Generated Code Quality

- **Generated code must be readable** — developers will inspect the output in `obj/`. It should look like code a senior engineer would write: clear names, no magic, no redundant casts or suppressions.
