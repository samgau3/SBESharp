namespace SBESharp.Roslyn.Ir;

/// <summary>Represents a top-level SBE message defined in a schema.</summary>
/// <param name="Name">The name of the message.</param>
/// <param name="Id">The schema-assigned message template identifier.</param>
/// <param name="BlockLength">The fixed block length of the message root in bytes.</param>
/// <param name="Description">An optional human-readable description of the message.</param>
/// <param name="Fields">The fixed-length fields in the message root block.</param>
/// <param name="Groups">The repeating groups following the root block.</param>
/// <param name="DataFields">The variable-length data fields following all groups.</param>
public sealed record SbeMessage(
	string Name,
	ushort Id,
	ushort BlockLength,
	string? Description,
	IReadOnlyList<SbeField> Fields,
	IReadOnlyList<SbeGroup> Groups,
	IReadOnlyList<SbeData> DataFields);
