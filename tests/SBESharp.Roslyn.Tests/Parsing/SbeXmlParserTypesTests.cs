using SBESharp.Roslyn.Ir;
using SBESharp.Roslyn.Parsing;

namespace SBESharp.Roslyn.Tests.Parsing;

public sealed class SbeXmlParserTypesTests
{
	[Fact]
	public void Parse_PrimitiveType_IsRead()
	{
		// Arrange
		var xml = Schema("""<type name="Reading" primitiveType="int64" description="Fixed-point reading"/>""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var pt = Assert.IsType<SbePrimitiveType>(Assert.Single(schema.Types));
		Assert.Equal("Reading", pt.Name);
		Assert.Equal(SbePrimitive.I64, pt.PrimitiveType);
		Assert.Equal("Fixed-point reading", pt.Description);
		Assert.Equal(Presence.Required, pt.Presence);
		Assert.Equal(1u, pt.Length);
	}

	[Fact]
	public void Parse_PrimitiveType_WithOptionalPresence_IsRead()
	{
		// Arrange
		var xml = Schema("""<type name="Tag" primitiveType="uint8" presence="optional" nullValue="255"/>""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var pt = Assert.IsType<SbePrimitiveType>(Assert.Single(schema.Types));
		Assert.Equal(Presence.Optional, pt.Presence);
		Assert.Equal("255", pt.NullValue);
	}

	[Fact]
	public void Parse_PrimitiveType_WithConstantPresence_IsRead()
	{
		// Arrange
		var xml = Schema("""<type name="Version" primitiveType="uint8" presence="constant">3</type>""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var pt = Assert.IsType<SbePrimitiveType>(Assert.Single(schema.Types));
		Assert.Equal(Presence.Constant, pt.Presence);
		Assert.Equal("3", pt.ConstantValue);
	}

	[Fact]
	public void Parse_PrimitiveType_WithLength_IsRead()
	{
		// Arrange
		var xml = Schema("""<type name="VehicleCode" primitiveType="char" length="8"/>""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var pt = Assert.IsType<SbePrimitiveType>(Assert.Single(schema.Types));
		Assert.Equal(8u, pt.Length);
	}

	[Fact]
	public void Parse_PrimitiveType_WithMinMaxValue_IsRead()
	{
		// Arrange
		var xml = Schema("""<type name="Count" primitiveType="int32" minValue="0" maxValue="1000000"/>""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var pt = Assert.IsType<SbePrimitiveType>(Assert.Single(schema.Types));
		Assert.Equal("0", pt.MinValue);
		Assert.Equal("1000000", pt.MaxValue);
	}

	[Fact]
	public void Parse_Enum_WithValidValues()
	{
		// Arrange
		var xml = Schema("""
			<enum name="Status" encodingType="uint8">
				<validValue name="Active">0</validValue>
				<validValue name="Idle">1</validValue>
			</enum>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var en = Assert.IsType<SbeEnumDefinition>(Assert.Single(schema.Types));
		Assert.Equal("Status", en.Name);
		Assert.Equal(SbePrimitive.U8, en.EncodingType);
		Assert.Equal(2, en.ValidValues.Count);
		Assert.Equal("Active", en.ValidValues[0].Name);
		Assert.Equal(0L, en.ValidValues[0].Value);
		Assert.Equal("Idle", en.ValidValues[1].Name);
		Assert.Equal(1L, en.ValidValues[1].Value);
	}

	[Fact]
	public void Parse_Enum_MissingEncodingType_DefaultsToUint8()
	{
		// Arrange
		var xml = Schema("""
			<enum name="Status">
				<validValue name="Active">0</validValue>
			</enum>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var en = Assert.IsType<SbeEnumDefinition>(Assert.Single(schema.Types));
		Assert.Equal(SbePrimitive.U8, en.EncodingType);
	}

	[Fact]
	public void Parse_Enum_NonNumericValue_ThrowsFormatException()
	{
		// Arrange
		var xml = Schema("""
			<enum name="Status" encodingType="uint8">
				<validValue name="Active">ACTIVE</validValue>
			</enum>
			""");

		// Act/Assert
		Assert.Throws<FormatException>(() => SbeXmlParser.Parse(xml));
	}

	[Fact]
	public void Parse_Enum_CharEncodedValues_MappedToByteValues()
	{
		// Arrange
		var xml = Schema("""
			<enum name="Model" encodingType="char">
				<validValue name="A">A</validValue>
				<validValue name="B">B</validValue>
				<validValue name="C">C</validValue>
			</enum>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var en = Assert.IsType<SbeEnumDefinition>(Assert.Single(schema.Types));
		Assert.Equal("Model", en.Name);
		Assert.Equal(SbePrimitive.Ascii, en.EncodingType);
		Assert.Equal(3, en.ValidValues.Count);
		Assert.Equal(65L, en.ValidValues[0].Value);
		Assert.Equal(66L, en.ValidValues[1].Value);
		Assert.Equal(67L, en.ValidValues[2].Value);
	}

	[Fact]
	public void Parse_Composite_WithRefElement_IsRead()
	{
		// Arrange
		var xml = Schema("""
			<type name="Percentage" primitiveType="int8"/>
			<composite name="Engine">
				<type name="capacity" primitiveType="uint16"/>
				<ref name="efficiency" type="Percentage"/>
			</composite>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		Assert.Equal(2, schema.Types.Count);
		var comp = Assert.IsType<SbeComposite>(schema.Types[1]);
		Assert.Equal(2, comp.Fields.Count);
		Assert.IsType<SbePrimitiveType>(comp.Fields[0]);
		var refField = Assert.IsType<SbeRefType>(comp.Fields[1]);
		Assert.Equal("efficiency", refField.Name);
		Assert.Equal("Percentage", refField.ReferencedType);
	}

	[Fact]
	public void Parse_Set_WithChoices()
	{
		// Arrange
		var xml = Schema("""
			<set name="Flags" encodingType="uint8">
				<choice name="IsEnabled">0</choice>
				<choice name="IsPrimary">1</choice>
			</set>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var set = Assert.IsType<SbeSet>(Assert.Single(schema.Types));
		Assert.Equal("Flags", set.Name);
		Assert.Equal(2, set.Choices.Count);
		Assert.Equal("IsEnabled", set.Choices[0].Name);
		Assert.Equal(0u, set.Choices[0].BitIndex);
		Assert.Equal("IsPrimary", set.Choices[1].Name);
		Assert.Equal(1u, set.Choices[1].BitIndex);
	}

	[Fact]
	public void Parse_Set_MissingEncodingType_DefaultsToUint8()
	{
		// Arrange
		var xml = Schema("""
			<set name="Flags">
				<choice name="IsEnabled">0</choice>
			</set>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var set = Assert.IsType<SbeSet>(Assert.Single(schema.Types));
		Assert.Equal(SbePrimitive.U8, set.EncodingType);
	}

	[Fact]
	public void Parse_Set_NegativeBitIndex_ThrowsFormatException()
	{
		// Arrange
		var xml = Schema("""
			<set name="Flags" encodingType="uint8">
				<choice name="IsEnabled">-1</choice>
			</set>
			""");

		// Act/Assert
		Assert.Throws<FormatException>(() => SbeXmlParser.Parse(xml));
	}

	[Fact]
	public void Parse_Set_NonNumericBitIndex_ThrowsFormatException()
	{
		// Arrange
		var xml = Schema("""
			<set name="Flags" encodingType="uint8">
				<choice name="IsEnabled">x</choice>
			</set>
			""");

		// Act/Assert
		Assert.Throws<FormatException>(() => SbeXmlParser.Parse(xml));
	}

	[Fact]
	public void Parse_Composite_WithNestedPrimitives()
	{
		// Arrange
		var xml = Schema("""
			<composite name="groupSizeEncoding">
				<type name="blockLength" primitiveType="uint16"/>
				<type name="numInGroup" primitiveType="uint8"/>
			</composite>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var comp = Assert.IsType<SbeComposite>(Assert.Single(schema.Types));
		Assert.Equal("groupSizeEncoding", comp.Name);
		Assert.Equal(2, comp.Fields.Count);
		Assert.IsType<SbePrimitiveType>(comp.Fields[0]);
		Assert.IsType<SbePrimitiveType>(comp.Fields[1]);
	}

	[Fact]
	public void Parse_Composite_WithNestedComposite_IsRead()
	{
		// Arrange
		var xml = Schema("""
			<composite name="outer">
				<type name="id" primitiveType="uint32"/>
				<composite name="inner">
					<type name="major" primitiveType="uint8"/>
					<type name="minor" primitiveType="uint8"/>
				</composite>
			</composite>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var outer = Assert.IsType<SbeComposite>(Assert.Single(schema.Types));
		Assert.Equal(2, outer.Fields.Count);
		var inner = Assert.IsType<SbeComposite>(outer.Fields[1]);
		Assert.Equal("inner", inner.Name);
		Assert.Equal(2, inner.Fields.Count);
	}

	[Fact]
	public void Parse_Composite_WithNestedEnum_IsRead()
	{
		// Arrange
		var xml = Schema("""
			<composite name="header">
				<type name="blockLength" primitiveType="uint16"/>
				<enum name="status" encodingType="uint8">
					<validValue name="Active">0</validValue>
				</enum>
			</composite>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var comp = Assert.IsType<SbeComposite>(Assert.Single(schema.Types));
		Assert.Equal(2, comp.Fields.Count);
		Assert.IsType<SbeEnumDefinition>(comp.Fields[1]);
	}

	[Fact]
	public void Parse_UnknownTypeElement_ThrowsFormatException()
	{
		// Arrange
		var xml = Schema("""<unknown name="X"/>""");

		// Act/Assert
		Assert.Throws<FormatException>(() => SbeXmlParser.Parse(xml));
	}

	private static string Schema(string typeXml) =>
		$$"""
		<messageSchema xmlns="http://fixprotocol.io/2016/sbe" id="1" version="0">
			<types>
				{{typeXml}}
			</types>
		</messageSchema>
		""";
}
