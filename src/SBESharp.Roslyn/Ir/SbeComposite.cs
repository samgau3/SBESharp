namespace SBESharp.Roslyn.Ir;

/// <summary>Represents an SBE composite type composed of multiple named sub-fields.</summary>
/// <param name="Name">The name of the composite type as declared in the schema.</param>
/// <param name="Fields">The ordered list of sub-field type definitions within this composite.</param>
/// <param name="Description">An optional human-readable description of the composite type.</param>
public sealed record SbeComposite(
	string Name,
	IReadOnlyList<SbeTypeDefinition> Fields,
	string? Description) : SbeTypeDefinition(Name, Description);
