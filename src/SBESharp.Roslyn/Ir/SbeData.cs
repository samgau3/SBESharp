namespace SBESharp.Roslyn.Ir;

/// <summary>Represents a variable-length data field in an SBE message or group.</summary>
/// <param name="Name">The name of the data field.</param>
/// <param name="Id">The schema-assigned field identifier.</param>
/// <param name="Type">The name of the encoding type used for this data field.</param>
/// <param name="Description">An optional human-readable description of the data field.</param>
public sealed record SbeData(
	string Name,
	ushort Id,
	string Type,
	string? Description);
