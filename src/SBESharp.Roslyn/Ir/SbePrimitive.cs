namespace SBESharp.Roslyn.Ir;

/// <summary>The closed set of SBE primitive wire types.</summary>
public enum SbePrimitive
{
	/// <summary>Unsigned 8-bit integer.</summary>
	U8,

	/// <summary>Signed 8-bit integer.</summary>
	I8,

	/// <summary>Single byte character (maps to <c>byte</c> in C#).</summary>
	Ascii,

	/// <summary>Unsigned 16-bit integer.</summary>
	U16,

	/// <summary>Signed 16-bit integer.</summary>
	I16,

	/// <summary>Unsigned 32-bit integer.</summary>
	U32,

	/// <summary>Signed 32-bit integer.</summary>
	I32,

	/// <summary>Unsigned 64-bit integer.</summary>
	U64,

	/// <summary>Signed 64-bit integer.</summary>
	I64,

	/// <summary>32-bit IEEE 754 floating-point.</summary>
	F32,

	/// <summary>64-bit IEEE 754 floating-point.</summary>
	F64,
}
