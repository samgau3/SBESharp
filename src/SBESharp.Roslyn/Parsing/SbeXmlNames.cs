namespace SBESharp.Roslyn.Parsing;

/// <summary>String constants for SBE XML schema element and attribute names.</summary>
public static class SbeXmlNames
{
	/// <summary>Local names of XML elements in an SBE schema document.</summary>
	public static class Elements
	{
		/// <summary>The "types" element name.</summary>
		public const string Types = "types";

		/// <summary>The "message" element name.</summary>
		public const string Message = "message";

		/// <summary>The "field" element name.</summary>
		public const string Field = "field";

		/// <summary>The "group" element name.</summary>
		public const string Group = "group";

		/// <summary>The "data" element name.</summary>
		public const string Data = "data";

		/// <summary>The "type" element name.</summary>
		public const string Type = "type";

		/// <summary>The "composite" element name.</summary>
		public const string Composite = "composite";

		/// <summary>The "enum" element name.</summary>
		public const string Enum = "enum";

		/// <summary>The "set" element name.</summary>
		public const string Set = "set";

		/// <summary>The "validValue" element name.</summary>
		public const string ValidValue = "validValue";

		/// <summary>The "choice" element name.</summary>
		public const string Choice = "choice";

		/// <summary>The "ref" element name.</summary>
		public const string Ref = "ref";
	}

	/// <summary>Attribute names shared across multiple element types in an SBE schema document.</summary>
	public static class Attributes
	{
		/// <summary>The "package" attribute name.</summary>
		public const string Package = "package";

		/// <summary>The "id" attribute name.</summary>
		public const string Id = "id";

		/// <summary>The "version" attribute name.</summary>
		public const string Version = "version";

		/// <summary>The "description" attribute name.</summary>
		public const string Description = "description";

		/// <summary>The "byteOrder" attribute name.</summary>
		public const string ByteOrder = "byteOrder";

		/// <summary>The "name" attribute name.</summary>
		public const string Name = "name";

		/// <summary>The "blockLength" attribute name.</summary>
		public const string BlockLength = "blockLength";

		/// <summary>The "type" attribute name.</summary>
		public const string Type = "type";

		/// <summary>The "offset" attribute name.</summary>
		public const string Offset = "offset";

		/// <summary>The "presence" attribute name.</summary>
		public const string Presence = "presence";

		/// <summary>The "dimensionType" attribute name.</summary>
		public const string DimensionType = "dimensionType";

		/// <summary>The "primitiveType" attribute name.</summary>
		public const string PrimitiveType = "primitiveType";

		/// <summary>The "length" attribute name.</summary>
		public const string Length = "length";

		/// <summary>The "nullValue" attribute name.</summary>
		public const string NullValue = "nullValue";

		/// <summary>The "minValue" attribute name.</summary>
		public const string MinValue = "minValue";

		/// <summary>The "maxValue" attribute name.</summary>
		public const string MaxValue = "maxValue";

		/// <summary>The "encodingType" attribute name.</summary>
		public const string EncodingType = "encodingType";

		/// <summary>The "semanticVersion" attribute name.</summary>
		public const string SemanticVersion = "semanticVersion";

		/// <summary>The "headerType" attribute name.</summary>
		public const string HeaderType = "headerType";

		/// <summary>The "valueRef" attribute name.</summary>
		public const string ValueRef = "valueRef";
	}

	/// <summary>Well-known attribute values and schema-level defaults.</summary>
	public static class AttributeValues
	{
		/// <summary>The "bigEndian" byte-order value.</summary>
		public const string BigEndian = "bigEndian";

		/// <summary>The "littleEndian" byte-order value.</summary>
		public const string LittleEndian = "littleEndian";

		/// <summary>The "optional" presence value.</summary>
		public const string PresenceOptional = "optional";

		/// <summary>The "constant" presence value.</summary>
		public const string PresenceConstant = "constant";

		/// <summary>The default dimension type name used for repeating groups.</summary>
		public const string DefaultDimensionType = "groupSizeEncoding";

		/// <summary>The default encoding type for variable-length data fields.</summary>
		public const string DefaultEncodingType = "uint8";
	}
}
