namespace SBESharp.Roslyn.Ir;

/// <summary>Represents the top-level SBE schema parsed from an XML definition.</summary>
/// <param name="Package">The package namespace declared in the schema.</param>
/// <param name="Id">The numeric schema identifier.</param>
/// <param name="Version">The schema version number.</param>
/// <param name="ByteOrder">The byte order used for all encoded fields in the schema.</param>
/// <param name="Types">All type definitions (primitives, enums, sets, composites) declared in the schema.</param>
/// <param name="Messages">The messages defined in the schema.</param>
/// <param name="Description">An optional human-readable description of the schema.</param>
/// <param name="SemanticVersion">An optional semantic version string for the schema.</param>
/// <param name="HeaderType">The name of the composite type used as the message header, or null for the default.</param>
public sealed record SbeSchema(
	string Package,
	ushort Id,
	ushort Version,
	ByteOrder ByteOrder,
	IReadOnlyList<SbeTypeDefinition> Types,
	IReadOnlyList<SbeMessage> Messages,
	string? Description,
	string? SemanticVersion,
	string? HeaderType);
