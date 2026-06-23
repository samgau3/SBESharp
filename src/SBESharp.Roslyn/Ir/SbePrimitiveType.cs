namespace SBESharp.Roslyn.Ir;

/// <summary>Represents a primitive (simple) type definition in an SBE schema.</summary>
/// <param name="Name">The name of the type as declared in the schema.</param>
/// <param name="PrimitiveType">The underlying SBE primitive kind (e.g. uint32, char).</param>
/// <param name="Presence">The presence semantics of the type (required, optional, or constant).</param>
/// <param name="Length">The number of elements when the type represents a fixed-length array; 1 for scalar types.</param>
/// <param name="Description">An optional human-readable description of the type.</param>
/// <param name="NullValue">The wire value that represents null for optional fields, or null if not specified.</param>
/// <param name="MinValue">The minimum valid value, or null if not specified.</param>
/// <param name="MaxValue">The maximum valid value, or null if not specified.</param>
/// <param name="ConstantValue">The constant value for constant-presence fields, or null if not constant.</param>
public sealed record SbePrimitiveType(
	string Name,
	SbePrimitive PrimitiveType,
	Presence Presence,
	uint Length,
	string? Description,
	string? NullValue,
	string? MinValue,
	string? MaxValue,
	string? ConstantValue) : SbeTypeDefinition(Name, Description);
