namespace SBESharp.Roslyn.Ir;

/// <summary>Specifies the presence semantics of a field in an SBE schema.</summary>
public enum Presence
{
	/// <summary>The field is always present and must be encoded.</summary>
	Required,

	/// <summary>The field may be absent, indicated by a null value on the wire.</summary>
	Optional,

	/// <summary>The field has a fixed value defined in the schema and is not encoded on the wire.</summary>
	Constant,
}
