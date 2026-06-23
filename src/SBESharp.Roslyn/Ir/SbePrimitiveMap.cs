namespace SBESharp.Roslyn.Ir;

/// <summary>Maps between SBE primitive wire-type names, <see cref="SbePrimitive"/> values, C# keywords, and byte sizes.</summary>
public static class SbePrimitiveMap
{
	/// <summary>Attempts to parse a well-known SBE primitive type name into a <see cref="SbePrimitive"/> value.</summary>
	/// <param name="name">The SBE primitive type name to parse (e.g. "uint32", "char").</param>
	/// <param name="primitive">When this method returns, contains the parsed primitive if successful.</param>
	/// <returns><see langword="true"/> if the type name was recognized; otherwise, <see langword="false"/>.</returns>
	public static bool TryParse(string name, out SbePrimitive primitive)
	{
		switch (name)
		{
			case "uint8": primitive = SbePrimitive.U8; return true;
			case "int8": primitive = SbePrimitive.I8; return true;
			case "char": primitive = SbePrimitive.Ascii; return true;
			case "uint16": primitive = SbePrimitive.U16; return true;
			case "int16": primitive = SbePrimitive.I16; return true;
			case "uint32": primitive = SbePrimitive.U32; return true;
			case "int32": primitive = SbePrimitive.I32; return true;
			case "uint64": primitive = SbePrimitive.U64; return true;
			case "int64": primitive = SbePrimitive.I64; return true;
			case "float": primitive = SbePrimitive.F32; return true;
			case "double": primitive = SbePrimitive.F64; return true;
			default: primitive = default; return false;
		}
	}

	/// <summary>Returns the C# keyword for the given <see cref="SbePrimitive"/>.</summary>
	/// <param name="primitive">The SBE primitive type.</param>
	/// <returns>The C# keyword representing the primitive type.</returns>
	public static string ToCSharp(SbePrimitive primitive) => primitive switch
	{
		SbePrimitive.U8 or SbePrimitive.Ascii => "byte",
		SbePrimitive.I8 => "sbyte",
		SbePrimitive.U16 => "ushort",
		SbePrimitive.I16 => "short",
		SbePrimitive.U32 => "uint",
		SbePrimitive.I32 => "int",
		SbePrimitive.U64 => "ulong",
		SbePrimitive.I64 => "long",
		SbePrimitive.F32 => "float",
		SbePrimitive.F64 => "double",
		_ => "byte",
	};

	/// <summary>Returns the wire size in bytes of the given <see cref="SbePrimitive"/>.</summary>
	/// <param name="primitive">The SBE primitive type.</param>
	/// <returns>The wire size in bytes.</returns>
	public static int SizeOf(SbePrimitive primitive) => primitive switch
	{
		SbePrimitive.U8 or SbePrimitive.I8 or SbePrimitive.Ascii => 1,
		SbePrimitive.U16 or SbePrimitive.I16 => 2,
		SbePrimitive.U32 or SbePrimitive.I32 or SbePrimitive.F32 => 4,
		SbePrimitive.U64 or SbePrimitive.I64 or SbePrimitive.F64 => 8,
		_ => 1,
	};
}
