using SBESharp.Roslyn.Ir;
using SBESharp.Roslyn.Parsing;

namespace SBESharp.Roslyn.Tests.Parsing;

public sealed class SbeXmlParserMessagesTests
{
	[Fact]
	public void Parse_Message_WithPrimitiveFields()
	{
		// Arrange
		var xml = Schema("""
			<message name="Reading" id="1" blockLength="16" description="Sensor reading">
				<field name="DeviceId" id="1" type="int64"/>
				<field name="Value" id="2" type="double"/>
			</message>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var msg = Assert.Single(schema.Messages);
		Assert.Equal("Reading", msg.Name);
		Assert.Equal(1, msg.Id);
		Assert.Equal(16, msg.BlockLength);
		Assert.Equal("Sensor reading", msg.Description);
		Assert.Equal(2, msg.Fields.Count);
		Assert.Equal("DeviceId", msg.Fields[0].Name);
		Assert.Equal("int64", msg.Fields[0].Type);
		Assert.Equal("Value", msg.Fields[1].Name);
		Assert.Equal("double", msg.Fields[1].Type);
	}

	[Fact]
	public void Parse_Message_MissingName_ThrowsFormatException()
	{
		// Arrange
		var xml = Schema("""<message id="1" blockLength="0"/>""");

		// Act/Assert
		Assert.Throws<FormatException>(() => SbeXmlParser.Parse(xml));
	}

	[Fact]
	public void Parse_Field_WithExplicitOffset()
	{
		// Arrange
		var xml = Schema("""
			<message name="M" id="1" blockLength="8">
				<field name="X" id="1" type="int32" offset="4"/>
			</message>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var field = Assert.Single(schema.Messages[0].Fields);
		Assert.Equal(4, field.Offset);
	}

	[Fact]
	public void Parse_Field_DefaultPresence_IsRequired()
	{
		// Arrange
		var xml = Schema("""
			<message name="M" id="1" blockLength="4">
				<field name="X" id="1" type="int32"/>
			</message>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var field = Assert.Single(schema.Messages[0].Fields);
		Assert.Equal(Presence.Required, field.Presence);
		Assert.Null(field.Offset);
	}

	[Fact]
	public void Parse_Field_WithOptionalPresence_IsRead()
	{
		// Arrange
		var xml = Schema("""
			<message name="M" id="1" blockLength="4">
				<field name="X" id="1" type="int32" presence="optional"/>
			</message>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var field = Assert.Single(schema.Messages[0].Fields);
		Assert.Equal(Presence.Optional, field.Presence);
	}

	[Fact]
	public void Parse_Field_MissingName_ThrowsFormatException()
	{
		// Arrange
		var xml = Schema("""
			<message name="M" id="1" blockLength="4">
				<field id="1" type="int32"/>
			</message>
			""");

		// Act/Assert
		Assert.Throws<FormatException>(() => SbeXmlParser.Parse(xml));
	}

	[Fact]
	public void Parse_Field_MissingType_ThrowsFormatException()
	{
		// Arrange
		var xml = Schema("""
			<message name="M" id="1" blockLength="4">
				<field name="X" id="1"/>
			</message>
			""");

		// Act/Assert
		Assert.Throws<FormatException>(() => SbeXmlParser.Parse(xml));
	}

	[Fact]
	public void Parse_Group_WithFields()
	{
		// Arrange
		var xml = Schema("""
			<message name="Scan" id="1" blockLength="0">
				<group name="Samples" id="2" dimensionType="groupSizeEncoding">
					<field name="Value" id="3" type="double"/>
					<field name="Count" id="4" type="int32"/>
				</group>
			</message>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var msg = Assert.Single(schema.Messages);
		var group = Assert.Single(msg.Groups);
		Assert.Equal("Samples", group.Name);
		Assert.Equal("groupSizeEncoding", group.DimensionType);
		Assert.Equal(2, group.Fields.Count);
	}

	[Fact]
	public void Parse_Group_MissingDimensionType_DefaultsToGroupSizeEncoding()
	{
		// Arrange
		var xml = Schema("""
			<message name="M" id="1" blockLength="0">
				<group name="Items" id="2">
					<field name="X" id="3" type="int32"/>
				</group>
			</message>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var group = Assert.Single(schema.Messages[0].Groups);
		Assert.Equal("groupSizeEncoding", group.DimensionType);
	}

	[Fact]
	public void Parse_Group_WithNestedGroup_IsRead()
	{
		// Arrange
		var xml = Schema("""
			<message name="M" id="1" blockLength="0">
				<group name="Devices" id="2">
					<field name="DeviceId" id="3" type="int64"/>
					<group name="Samples" id="4">
						<field name="Count" id="5" type="uint32"/>
					</group>
				</group>
			</message>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var outer = Assert.Single(schema.Messages[0].Groups);
		Assert.Equal("Devices", outer.Name);
		Assert.Single(outer.Fields);
		var inner = Assert.Single(outer.Groups);
		Assert.Equal("Samples", inner.Name);
		Assert.Single(inner.Fields);
	}

	[Fact]
	public void Parse_Data_IsCollected()
	{
		// Arrange
		var xml = Schema("""
			<message name="M" id="1" blockLength="0">
				<data name="Payload" id="1" type="varDataEncoding"/>
			</message>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var data = Assert.Single(schema.Messages[0].DataFields);
		Assert.Equal("Payload", data.Name);
		Assert.Equal("varDataEncoding", data.Type);
	}

	[Fact]
	public void Parse_Message_WithGroupsAndData_AllParsed()
	{
		// Arrange
		var xml = Schema("""
			<message name="M" id="1" blockLength="8">
				<field name="Id" id="1" type="int64"/>
				<group name="Items" id="2">
					<field name="Count" id="3" type="uint32"/>
				</group>
				<data name="Note" id="4" type="varDataEncoding"/>
			</message>
			""");

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var msg = Assert.Single(schema.Messages);
		Assert.Single(msg.Fields);
		Assert.Single(msg.Groups);
		Assert.Single(msg.DataFields);
	}

	[Fact]
	public void Parse_Field_ConstantPresence_ValueRefParsed()
	{
		// Arrange
		var xml = """
			<messageSchema xmlns="http://fixprotocol.io/2016/sbe" id="1" version="0">
				<types>
					<enum name="Model" encodingType="char">
						<validValue name="A">A</validValue>
						<validValue name="C">C</validValue>
					</enum>
				</types>
				<message name="M" id="1" blockLength="0">
					<field name="discountedModel" id="1" type="Model" presence="constant" valueRef="Model.C"/>
				</message>
			</messageSchema>
			""";

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var field = Assert.Single(schema.Messages[0].Fields);
		Assert.Equal(Presence.Constant, field.Presence);
		Assert.Equal("Model.C", field.ValueRef);
		Assert.True(field.IsEnumOrSet);
	}

	[Fact]
	public void Parse_Field_ArrayType_ArrayLengthPropagated()
	{
		// Arrange
		var xml = """
			<messageSchema xmlns="http://fixprotocol.io/2016/sbe" id="1" version="0">
				<types>
					<type name="VehicleCode" primitiveType="char" length="6"/>
				</types>
				<message name="M" id="1" blockLength="6">
					<field name="code" id="1" type="VehicleCode"/>
				</message>
			</messageSchema>
			""";

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var field = Assert.Single(schema.Messages[0].Fields);
		Assert.Equal(6u, field.ArrayLength);
		Assert.Equal(SbePrimitive.Ascii, field.EncodingPrimitive);
	}

	[Fact]
	public void Parse_Field_CompositeType_IsMarkedAsComposite()
	{
		// Arrange
		var xml = """
			<messageSchema xmlns="http://fixprotocol.io/2016/sbe" id="1" version="0">
				<types>
					<composite name="Engine">
						<type name="capacity" primitiveType="uint16"/>
						<type name="numCylinders" primitiveType="uint8"/>
					</composite>
				</types>
				<message name="M" id="1" blockLength="3">
					<field name="engine" id="1" type="Engine"/>
				</message>
			</messageSchema>
			""";

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var field = Assert.Single(schema.Messages[0].Fields);
		Assert.True(field.IsComposite);
		Assert.Equal(3, field.WireSize);
	}

	[Fact]
	public void Parse_Field_ConstantType_HasConstantValue()
	{
		// Arrange
		var xml = """
			<messageSchema xmlns="http://fixprotocol.io/2016/sbe" id="1" version="0">
				<types>
					<type name="MaxRpm" primitiveType="uint16" presence="constant">9000</type>
				</types>
				<message name="M" id="1" blockLength="0">
					<field name="maxRpm" id="1" type="MaxRpm"/>
				</message>
			</messageSchema>
			""";

		// Act
		var schema = SbeXmlParser.Parse(xml);

		// Assert
		var field = Assert.Single(schema.Messages[0].Fields);
		Assert.Equal("9000", field.ConstantValue);
	}

	private static string Schema(string messageXml) =>
		$$"""
		<messageSchema xmlns="http://fixprotocol.io/2016/sbe" id="1" version="0">
			<types/>
			{{messageXml}}
		</messageSchema>
		""";
}
