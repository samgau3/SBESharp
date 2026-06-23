namespace SBESharp.Roslyn.Ir;

/// <summary>Represents a repeating group within an SBE message.</summary>
/// <param name="Name">The name of the group.</param>
/// <param name="Id">The schema-assigned group identifier.</param>
/// <param name="DimensionType">The name of the composite type used for the group dimension header.</param>
/// <param name="BlockLength">The explicit block length in bytes, or null if auto-calculated.</param>
/// <param name="Fields">The fixed-length fields within each group entry.</param>
/// <param name="Groups">Nested repeating groups within each group entry.</param>
/// <param name="DataFields">Variable-length data fields within each group entry.</param>
/// <param name="Description">An optional human-readable description of the group.</param>
public sealed record SbeGroup(
	string Name,
	ushort Id,
	string DimensionType,
	ushort? BlockLength,
	IReadOnlyList<SbeField> Fields,
	IReadOnlyList<SbeGroup> Groups,
	IReadOnlyList<SbeData> DataFields,
	string? Description);
