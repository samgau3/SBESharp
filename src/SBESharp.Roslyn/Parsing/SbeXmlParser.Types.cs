using System.Xml.Linq;
using SBESharp.Roslyn.Ir;

namespace SBESharp.Roslyn.Parsing;

/// <summary>Type definition parsing methods.</summary>
public static partial class SbeXmlParser
{
	private static List<SbeTypeDefinition> ParseTypes(XElement typesElement, CancellationToken cancellationToken) =>
		[.. typesElement.Elements().Select(el =>
		{
			cancellationToken.ThrowIfCancellationRequested();
			return ParseTypeElement(el);
		})];

	private static SbeTypeDefinition ParseTypeElement(XElement el) =>
		el.Name.LocalName switch
		{
			SbeXmlNames.Elements.Type => ParsePrimitiveType(el),
			SbeXmlNames.Elements.Composite => ParseComposite(el),
			SbeXmlNames.Elements.Enum => ParseEnum(el),
			SbeXmlNames.Elements.Set => ParseSet(el),
			SbeXmlNames.Elements.Ref => ParseRef(el),
			_ => throw new FormatException($"Unknown type element <{el.Name.LocalName}>."),
		};

	private static SbeRefType ParseRef(XElement el) =>
		new SbeRefType(
			Name: RequireAttribute(el.Attribute(SbeXmlNames.Attributes.Name)),
			ReferencedType: RequireAttribute(el.Attribute(SbeXmlNames.Attributes.Type)),
			Description: el.Attribute(SbeXmlNames.Attributes.Description)?.Value);

	private static SbePrimitiveType ParsePrimitiveType(XElement el) =>
		new SbePrimitiveType(
			Name: RequireAttribute(el.Attribute(SbeXmlNames.Attributes.Name)),
			PrimitiveType: ParseSbePrimitive(el.Attribute(SbeXmlNames.Attributes.PrimitiveType)),
			Presence: ParsePresence(el.Attribute(SbeXmlNames.Attributes.Presence)?.Value),
			Length: ParseUInt32(el.Attribute(SbeXmlNames.Attributes.Length)?.Value, 1u),
			Description: el.Attribute(SbeXmlNames.Attributes.Description)?.Value,
			NullValue: el.Attribute(SbeXmlNames.Attributes.NullValue)?.Value,
			MinValue: el.Attribute(SbeXmlNames.Attributes.MinValue)?.Value,
			MaxValue: el.Attribute(SbeXmlNames.Attributes.MaxValue)?.Value,
			ConstantValue: el.Value.Length > 0 ? el.Value.Trim() : null);

	private static SbeComposite ParseComposite(XElement el) =>
		new SbeComposite(
			Name: RequireAttribute(el.Attribute(SbeXmlNames.Attributes.Name)),
			Fields: [.. el.Elements().Select(ParseTypeElement)],
			Description: el.Attribute(SbeXmlNames.Attributes.Description)?.Value);

	private static SbeEnumDefinition ParseEnum(XElement el) =>
		new SbeEnumDefinition(
			Name: RequireAttribute(el.Attribute(SbeXmlNames.Attributes.Name)),
			EncodingType: ParseSbePrimitive(el.Attribute(SbeXmlNames.Attributes.EncodingType), SbeXmlNames.AttributeValues.DefaultEncodingType),
			ValidValues: [.. GetChildren(el, SbeXmlNames.Elements.ValidValue).Select(ParseEnumValue)],
			Description: el.Attribute(SbeXmlNames.Attributes.Description)?.Value);

	private static SbeSet ParseSet(XElement el) =>
		new SbeSet(
			Name: RequireAttribute(el.Attribute(SbeXmlNames.Attributes.Name)),
			EncodingType: ParseSbePrimitive(el.Attribute(SbeXmlNames.Attributes.EncodingType), SbeXmlNames.AttributeValues.DefaultEncodingType),
			Choices: [.. GetChildren(el, SbeXmlNames.Elements.Choice).Select(ParseSetChoice)],
			Description: el.Attribute(SbeXmlNames.Attributes.Description)?.Value);

	private static SbeEnumDefinition.ValidValue ParseEnumValue(XElement el)
	{
		var name = RequireAttribute(el.Attribute(SbeXmlNames.Attributes.Name));
		var rawValue = el.Value.Trim();
		var description = el.Attribute(SbeXmlNames.Attributes.Description)?.Value;

		if (long.TryParse(rawValue, out var numericValue))
		{
			return new SbeEnumDefinition.ValidValue(name, numericValue, description);
		}

		// Char-encoded enum: single character maps to its byte value
		if (rawValue.Length == 1)
		{
			return new SbeEnumDefinition.ValidValue(name, rawValue[0], description);
		}

		throw new FormatException($"<validValue name='{name}'> has non-numeric value '{rawValue}'.");
	}

	private static SbeSet.Choice ParseSetChoice(XElement el) =>
		uint.TryParse(el.Value, out var bitIndex)
			? new SbeSet.Choice(
				Name: RequireAttribute(el.Attribute(SbeXmlNames.Attributes.Name)),
				BitIndex: bitIndex,
				Description: el.Attribute(SbeXmlNames.Attributes.Description)?.Value)
			: throw new FormatException($"<choice name='{el.Attribute(SbeXmlNames.Attributes.Name)?.Value}'> has non-numeric bit index '{el.Value}'.");
}
