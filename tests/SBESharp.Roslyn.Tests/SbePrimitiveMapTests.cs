using SBESharp.Roslyn.Ir;

namespace SBESharp.Roslyn.Tests;

public sealed class SbePrimitiveMapTests
{
	[Theory]
	[InlineData("uint8", true, SbePrimitive.U8)]
	[InlineData("int8", true, SbePrimitive.I8)]
	[InlineData("char", true, SbePrimitive.Ascii)]
	[InlineData("uint16", true, SbePrimitive.U16)]
	[InlineData("int16", true, SbePrimitive.I16)]
	[InlineData("uint32", true, SbePrimitive.U32)]
	[InlineData("int32", true, SbePrimitive.I32)]
	[InlineData("uint64", true, SbePrimitive.U64)]
	[InlineData("int64", true, SbePrimitive.I64)]
	[InlineData("float", true, SbePrimitive.F32)]
	[InlineData("double", true, SbePrimitive.F64)]
	[InlineData("unknown", false, default(SbePrimitive))]
	public void TryParse_ReturnsExpected(string input, bool expectedResult, SbePrimitive expectedPrimitive)
	{
		// Act
		var result = SbePrimitiveMap.TryParse(input, out var primitive);

		// Assert
		Assert.Equal(expectedResult, result);
		Assert.Equal(expectedPrimitive, primitive);
	}

	[Theory]
	[InlineData(SbePrimitive.U8, "byte")]
	[InlineData(SbePrimitive.I8, "sbyte")]
	[InlineData(SbePrimitive.Ascii, "byte")]
	[InlineData(SbePrimitive.U16, "ushort")]
	[InlineData(SbePrimitive.I16, "short")]
	[InlineData(SbePrimitive.U32, "uint")]
	[InlineData(SbePrimitive.I32, "int")]
	[InlineData(SbePrimitive.U64, "ulong")]
	[InlineData(SbePrimitive.I64, "long")]
	[InlineData(SbePrimitive.F32, "float")]
	[InlineData(SbePrimitive.F64, "double")]
	public void ToCSharp_ReturnsExpectedKeyword(SbePrimitive primitive, string expected)
	{
		// Act
		var result = SbePrimitiveMap.ToCSharp(primitive);

		// Assert
		Assert.Equal(expected, result);
	}

	[Theory]
	[InlineData(SbePrimitive.U8, 1)]
	[InlineData(SbePrimitive.I8, 1)]
	[InlineData(SbePrimitive.Ascii, 1)]
	[InlineData(SbePrimitive.U16, 2)]
	[InlineData(SbePrimitive.I16, 2)]
	[InlineData(SbePrimitive.U32, 4)]
	[InlineData(SbePrimitive.I32, 4)]
	[InlineData(SbePrimitive.F32, 4)]
	[InlineData(SbePrimitive.U64, 8)]
	[InlineData(SbePrimitive.I64, 8)]
	[InlineData(SbePrimitive.F64, 8)]
	public void SizeOf_ReturnsExpectedSize(SbePrimitive primitive, int expected)
	{
		// Act
		var result = SbePrimitiveMap.SizeOf(primitive);

		// Assert
		Assert.Equal(expected, result);
	}
}
