namespace SBESharp.Roslyn.Ir;

/// <summary>Represents a reference to a named type defined elsewhere in the schema.</summary>
/// <param name="Name">The local name of this reference within the composite.</param>
/// <param name="ReferencedType">The name of the type being referenced.</param>
/// <param name="Description">An optional human-readable description of the reference.</param>
public sealed record SbeRefType(
	string Name,
	string ReferencedType,
	string? Description) : SbeTypeDefinition(Name, Description);
