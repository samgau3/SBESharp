namespace SBESharp.Roslyn.Ir;

/// <summary>Represents an SBE enum type definition with a set of named valid values.</summary>
/// <param name="Name">The name of the enum type as declared in the schema.</param>
/// <param name="EncodingType">The underlying primitive type used to encode the enum on the wire.</param>
/// <param name="ValidValues">The named valid values that comprise this enum.</param>
/// <param name="Description">An optional human-readable description of the enum type.</param>
public sealed record SbeEnumDefinition(
	string Name,
	SbePrimitive EncodingType,
	IReadOnlyList<SbeEnumDefinition.ValidValue> ValidValues,
	string? Description) : SbeTypeDefinition(Name, Description)
{
	/// <summary>A single valid value within an SBE enum type.</summary>
	/// <param name="Name">The name of the valid value.</param>
	/// <param name="Value">The numeric value assigned to this entry.</param>
	/// <param name="Description">An optional human-readable description of the valid value.</param>
	public sealed record ValidValue(string Name, long Value, string? Description);
}
