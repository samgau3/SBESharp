namespace SBESharp.Roslyn.Ir;

/// <summary>Represents an SBE set (bitset) type definition with named bit-flag choices.</summary>
/// <param name="Name">The name of the set type as declared in the schema.</param>
/// <param name="EncodingType">The underlying primitive type used to encode the bitset on the wire.</param>
/// <param name="Choices">The named bit-flag choices that comprise this set.</param>
/// <param name="Description">An optional human-readable description of the set type.</param>
public sealed record SbeSet(
	string Name,
	SbePrimitive EncodingType,
	IReadOnlyList<SbeSet.Choice> Choices,
	string? Description) : SbeTypeDefinition(Name, Description)
{
	/// <summary>A single bit-flag choice within an SBE set type.</summary>
	/// <param name="Name">The name of the choice.</param>
	/// <param name="BitIndex">The zero-based bit position of this choice within the bitset.</param>
	/// <param name="Description">An optional human-readable description of the choice.</param>
	public sealed record Choice(string Name, uint BitIndex, string? Description);
}
