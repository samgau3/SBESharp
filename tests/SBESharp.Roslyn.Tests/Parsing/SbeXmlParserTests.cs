using SBESharp.Roslyn.Ir;
using SBESharp.Roslyn.Parsing;

namespace SBESharp.Roslyn.Tests.Parsing;

public sealed class SbeXmlParserTests
{
	[Fact]
	public void Parse_ReadsSchemaAttributes()
	{
		// Arrange
		const string xml = """
			<messageSchema
				xmlns="http://fixprotocol.io/2016/sbe"
				package="com.example"
				id="42"
				version="3"
				description="Test schema"
				byteOrder="littleEndian">
				<types/>
			</messageSchema>
			""";

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		Assert.Equal("com.example", schema.Package);
		Assert.Equal(42, schema.Id);
		Assert.Equal(3, schema.Version);
		Assert.Equal("Test schema", schema.Description);
		Assert.Equal(ByteOrder.LittleEndian, schema.ByteOrder);
	}

	[Fact]
	public void Parse_BigEndian_IsRecognised()
	{
		// Arrange
		const string xml = """
			<messageSchema xmlns="http://fixprotocol.io/2016/sbe" id="1" version="0" byteOrder="bigEndian">
				<types/>
			</messageSchema>
			""";

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		Assert.Equal(ByteOrder.BigEndian, schema.ByteOrder);
	}

	[Fact]
	public void Parse_MissingByteOrder_DefaultsToLittleEndian()
	{
		// Arrange
		const string xml = """
			<messageSchema xmlns="http://fixprotocol.io/2016/sbe" id="1" version="0">
				<types/>
			</messageSchema>
			""";

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		Assert.Equal(ByteOrder.LittleEndian, schema.ByteOrder);
	}

	[Fact]
	public void Parse_NoNamespace_IsAccepted()
	{
		// Arrange
		const string xml = """
			<messageSchema id="1" version="0" byteOrder="littleEndian">
				<types/>
				<message name="Ping" id="1" blockLength="0"/>
			</messageSchema>
			""";

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		Assert.Single(schema.Messages);
		Assert.Equal("Ping", schema.Messages[0].Name);
	}

	[Fact]
	public void Parse_SbeNamespacePrefix_TypesAndMessagesFound()
	{
		// Arrange
		// Some real-world schemas use xmlns:sbe=... prefix on the root
		// element and sbe:message elements, but leave <types> and its children unprefixed.
		const string xml = """
			<sbe:messageSchema xmlns:sbe="http://fixprotocol.io/2016/sbe" id="1" version="0">
				<types>
					<enum name="Status" encodingType="uint8">
						<validValue name="Active">0</validValue>
						<validValue name="Idle">1</validValue>
					</enum>
					<type name="int64" primitiveType="int64"/>
				</types>
				<sbe:message name="StatusReport" id="1">
					<field name="Status" id="1" type="Status"/>
				</sbe:message>
			</sbe:messageSchema>
			""";

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		Assert.Equal(2, schema.Types.Count);
		var status = Assert.IsType<SbeEnumDefinition>(schema.Types[0]);
		Assert.Equal("Status", status.Name);
		Assert.Equal(2, status.ValidValues.Count);
		var msg = Assert.Single(schema.Messages);
		Assert.Equal("StatusReport", msg.Name);
		Assert.Single(msg.Fields);
		Assert.Equal("Status", msg.Fields[0].Type);
	}

	[Fact]
	public void Parse_ForeignNamespaceAttributes_AreIgnored()
	{
		// Arrange
		// Some real-world schemas annotate fields with attributes from their own
		// XML namespaces; the parser must tolerate and ignore them.
		const string xml = """
			<messageSchema xmlns="http://fixprotocol.io/2016/sbe" xmlns:ext="urn:example:extensions" id="1" version="0">
				<types>
					<type name="int64" primitiveType="int64"/>
				</types>
				<message name="Reading" id="1" blockLength="8">
					<field name="Value" id="1" type="int64" ext:units="volts"/>
				</message>
			</messageSchema>
			""";

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var msg = Assert.Single(schema.Messages);
		var field = Assert.Single(msg.Fields);
		Assert.Equal("Value", field.Name);
		Assert.Equal("int64", field.Type);
	}

	[Fact]
	public void Parse_NullContent_ThrowsArgumentNullException()
	{
		Assert.Throws<ArgumentNullException>(() => SbeXmlParser.Parse(null!));
	}

	[Fact]
	public void Parse_MissingTypesElement_ThrowsFormatException()
	{
		// Arrange
		const string xml = """
			<messageSchema xmlns="http://fixprotocol.io/2016/sbe" id="1" version="0"/>
			""";

		// Act/Assert
		Assert.Throws<FormatException>(() => SbeXmlParser.Parse(xml));
	}

	[Fact]
	public void Parse_MultipleTypesBlocks_AllTypesParsed()
	{
		// Arrange
		const string xml = """
			<messageSchema xmlns="http://fixprotocol.io/2016/sbe" id="1" version="0">
				<types>
					<type name="Reading" primitiveType="int64"/>
				</types>
				<types>
					<type name="Count" primitiveType="int32"/>
					<enum name="Status" encodingType="uint8">
						<validValue name="Active">0</validValue>
					</enum>
				</types>
			</messageSchema>
			""";

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		Assert.Equal(3, schema.Types.Count);
		Assert.Equal("Reading", schema.Types[0].Name);
		Assert.Equal("Count", schema.Types[1].Name);
		Assert.Equal("Status", schema.Types[2].Name);
	}
}
