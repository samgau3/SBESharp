using System.Xml.Linq;
using SBESharp.Roslyn.Ir;

namespace SBESharp.Roslyn.Parsing;

/// <summary>Helper methods for parsing XML attributes and elements.</summary>
public static partial class SbeXmlParser
{
	private static string RequireAttribute(XAttribute? attr) =>
		attr?.Value is { Length: > 0 } val
			? val
			: throw new FormatException(attr is null
				? "Required attribute is missing."
				: $"<{attr.Parent?.Name.LocalName ?? "?"}> has empty required attribute '{attr.Name.LocalName}'.");

	private static Presence ParsePresence(string? value) =>
		value switch
		{
			SbeXmlNames.AttributeValues.PresenceOptional => Presence.Optional,
			SbeXmlNames.AttributeValues.PresenceConstant => Presence.Constant,
			_ => Presence.Required,
		};

	private static ByteOrder ParseByteOrder(string? value) =>
		value == SbeXmlNames.AttributeValues.BigEndian
			? ByteOrder.BigEndian
			: ByteOrder.LittleEndian;

	private static ushort ParseUInt16(string? value, ushort defaultValue = 0) =>
		ushort.TryParse(value, out var v) ? v : defaultValue;

	private static uint ParseUInt32(string? value, uint defaultValue = 0u) =>
		uint.TryParse(value, out var v) ? v : defaultValue;

	private static int? TryParseInt32(string? value) =>
		int.TryParse(value, out var v) ? v : null;

	private static ushort? TryParseUInt16(string? value) =>
		ushort.TryParse(value, out var v) ? v : null;

	private static SbePrimitive ParseSbePrimitive(XAttribute? attr, string defaultValue = "uint8")
	{
		var raw = attr?.Value ?? defaultValue;

		return raw switch
		{
			"uint8" => SbePrimitive.U8,
			"int8" => SbePrimitive.I8,
			"char" => SbePrimitive.Ascii,
			"uint16" => SbePrimitive.U16,
			"int16" => SbePrimitive.I16,
			"uint32" => SbePrimitive.U32,
			"int32" => SbePrimitive.I32,
			"uint64" => SbePrimitive.U64,
			"int64" => SbePrimitive.I64,
			"float" => SbePrimitive.F32,
			"double" => SbePrimitive.F64,
			_ => throw new FormatException($"<{attr?.Parent?.Name.LocalName ?? "?"}> attribute '{attr?.Name.LocalName ?? "?"}' has unknown primitive type '{raw}'."),
		};
	}

	private static XElement GetChild(XElement parent, string localName) =>
		GetChildren(parent, localName).FirstOrDefault()
			?? throw new FormatException($"<{parent.Name.LocalName}> is missing required child element <{localName}>.");

	private static IEnumerable<XElement> GetChildren(XElement parent, string localName) =>
		parent.Elements().Where(e => e.Name.LocalName == localName);
}
