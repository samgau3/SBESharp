# SBE Specification Compliance

All code, schemas, and tests must conform to the [FIX Simple Binary Encoding specification](https://github.com/FIXTradingCommunity/fix-simple-binary-encoding). Never invent non-standard features or accept schemas that violate these rules.

## Message Structure Ordering (strict)

Within a message root or a group entry, elements must appear in this order — no mixing:

1. **Fixed-length fields** (`<field>`)
2. **Repeating groups** (`<group>`)
3. **Variable-length data** (`<data>`)

A fixed field after a group, or a group after varData, is invalid.

## Repeating Groups

- Each group has a **dimension header** (composite with `blockLength` + `numInGroup`).
- `blockLength` covers only the fixed fields in the group entry — not nested groups or varData.
- **Nested groups are valid**: a group entry can itself contain fields → groups → varData, recursively.
- **VarData inside groups is valid**: `<data>` elements may appear after all fields and nested groups in a group entry. This makes each entry variable-length.

## Variable-Length Data (`<data>`)

- Encoded as a length prefix followed by raw bytes.
- The encoding type is a composite (e.g., `varStringEncoding`) with a `length` field and a `varData` field.
- VarData fields must come after all fixed fields and groups.

## Constant Fields (`presence="constant"`)

- Occupy **zero bytes on the wire** — not encoded in the message.
- Value is specified in the schema XML (element text or `valueRef` attribute for enums).
- Do **not** contribute to `blockLength`.
- Decoders return the constant from the schema; encoders write nothing.

## Block Length

- `blockLength` = size in bytes of the **fixed-field block only**.
- Excludes repeating groups and variable-length data.
- Present in the message header (root block) and each group header (per-entry fixed fields).
- Decoders use it to skip to groups/varData and for forward compatibility.

## Primitive Types

| Type | Size | Null sentinel |
|------|------|---------------|
| `char` | 1 | 0 |
| `int8` | 1 | -128 |
| `int16` | 2 | -32768 |
| `int32` | 4 | -2147483648 |
| `int64` | 8 | -9223372036854775808 |
| `uint8` | 1 | 255 |
| `uint16` | 2 | 65535 |
| `uint32` | 4 | 4294967295 |
| `uint64` | 8 | 18446744073709551615 |
| `float` | 4 | NaN |
| `double` | 8 | NaN |

## Fixed-Length Arrays

- Declared via `length="N"` on a `<type>` (e.g., `<type name="VehicleCode" primitiveType="char" length="6"/>`).
- Occupy `N × primitiveSize` bytes in the fixed block.
- Not variable-length — they are part of `blockLength`.

## Enums

- Encoding type must be `char`, `uint8`, `uint16`, `uint32`, or `uint64` (unsigned integers or char only — **no signed types**).
- Each `<validValue>` maps a symbolic name to a wire value.

## Sets (Bitsets)

- Encoding type must be `uint8`, `uint16`, `uint32`, or `uint64`.
- Each `<choice>` maps a name to a bit position.

## Composite Types

- May contain `<type>`, `<enum>`, `<set>`, `<ref>`, and `<composite>` elements.
- `<ref>` references a named type defined elsewhere in the schema.
- Used for message headers, group dimension headers, decimal encodings, and domain-specific aggregates.

## Byte Order

- Declared at the schema level (`byteOrder="littleEndian"` or `byteOrder="bigEndian"`).
- Applies to all multi-byte fields uniformly.

## Test Schemas

- Every `.sbe.xml` in `TestSchemas/` must be a valid SBE schema per this spec.
- Do not create schemas with non-standard features or orderings, even for "negative" testing.
- When adding edge-case schemas, verify they are spec-compliant first.
