namespace SBESharp.Roslyn.Ir;

/// <summary>Specifies the byte order used for encoding multi-byte fields in an SBE schema.</summary>
public enum ByteOrder
{
	/// <summary>Least-significant byte first (Intel / x86 native order).</summary>
	LittleEndian,

	/// <summary>Most-significant byte first (network byte order).</summary>
	BigEndian,
}
