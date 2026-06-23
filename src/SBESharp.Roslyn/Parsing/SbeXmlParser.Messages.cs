using System.Xml.Linq;
using SBESharp.Roslyn.Ir;

namespace SBESharp.Roslyn.Parsing;

/// <summary>Message and group parsing methods.</summary>
public static partial class SbeXmlParser
{
	private static List<SbeMessage> ParseMessages(
		XElement root,
		IReadOnlyList<SbeTypeDefinition> types,
		CancellationToken cancellationToken) =>
		[.. GetChildren(root, SbeXmlNames.Elements.Message).Select(el =>
		{
			cancellationToken.ThrowIfCancellationRequested();
			return ParseMessage(el, types);
		})];

	private static SbeMessage ParseMessage(XElement el, IReadOnlyList<SbeTypeDefinition> types)
	{
		var (fields, groups, data) = ParseMessageBody(el, types);

		return new SbeMessage(
			Name: RequireAttribute(el.Attribute(SbeXmlNames.Attributes.Name)),
			Id: ParseUInt16(el.Attribute(SbeXmlNames.Attributes.Id)?.Value),
			BlockLength: ParseUInt16(el.Attribute(SbeXmlNames.Attributes.BlockLength)?.Value),
			Description: el.Attribute(SbeXmlNames.Attributes.Description)?.Value,
			Fields: fields,
			Groups: groups,
			DataFields: data);
	}

	private static (List<SbeField> Fields, List<SbeGroup> Groups, List<SbeData> DataFields)
		ParseMessageBody(XElement el, IReadOnlyList<SbeTypeDefinition> types) => (
			Fields: [.. GetChildren(el, SbeXmlNames.Elements.Field).Select(f => ParseField(f, types))],
			Groups: [.. GetChildren(el, SbeXmlNames.Elements.Group).Select(g => ParseGroup(g, types))],
			DataFields: [.. GetChildren(el, SbeXmlNames.Elements.Data).Select(ParseData)]);

	private static SbeField ParseField(XElement el, IReadOnlyList<SbeTypeDefinition> types)
	{
		var typeName = RequireAttribute(el.Attribute(SbeXmlNames.Attributes.Type));
		var presence = ParsePresence(el.Attribute(SbeXmlNames.Attributes.Presence)?.Value);
		var valueRef = el.Attribute(SbeXmlNames.Attributes.ValueRef)?.Value;
		var resolved = ResolveFieldType(typeName, types);

		return new SbeField(
			Name: RequireAttribute(el.Attribute(SbeXmlNames.Attributes.Name)),
			Id: ParseUInt16(el.Attribute(SbeXmlNames.Attributes.Id)?.Value),
			Type: typeName,
			EncodingPrimitive: resolved.EncodingPrimitive,
			IsEnumOrSet: resolved.IsEnumOrSet,
			Offset: TryParseInt32(el.Attribute(SbeXmlNames.Attributes.Offset)?.Value),
			Presence: presence,
			Description: el.Attribute(SbeXmlNames.Attributes.Description)?.Value,
			ValueRef: valueRef,
			ConstantValue: resolved.ConstantValue,
			ArrayLength: resolved.ArrayLength,
			IsComposite: resolved.IsComposite,
			WireSize: resolved.WireSize,
			CompositeDefinition: resolved.CompositeDefinition);
	}

	private static SbeGroup ParseGroup(XElement el, IReadOnlyList<SbeTypeDefinition> types)
	{
		var (fields, groups, data) = ParseMessageBody(el, types);

		return new SbeGroup(
			Name: RequireAttribute(el.Attribute(SbeXmlNames.Attributes.Name)),
			Id: ParseUInt16(el.Attribute(SbeXmlNames.Attributes.Id)?.Value),
			DimensionType: el.Attribute(SbeXmlNames.Attributes.DimensionType)?.Value ?? SbeXmlNames.AttributeValues.DefaultDimensionType,
			BlockLength: TryParseUInt16(el.Attribute(SbeXmlNames.Attributes.BlockLength)?.Value),
			Fields: fields,
			Groups: groups,
			DataFields: data,
			Description: el.Attribute(SbeXmlNames.Attributes.Description)?.Value);
	}

	private static SbeData ParseData(XElement el) =>
		new SbeData(
			Name: RequireAttribute(el.Attribute(SbeXmlNames.Attributes.Name)),
			Id: ParseUInt16(el.Attribute(SbeXmlNames.Attributes.Id)?.Value),
			Type: RequireAttribute(el.Attribute(SbeXmlNames.Attributes.Type)),
			Description: el.Attribute(SbeXmlNames.Attributes.Description)?.Value);

	private static ResolvedFieldType ResolveFieldType(
		string typeName,
		IReadOnlyList<SbeTypeDefinition> types)
	{
		if (SbePrimitiveMap.TryParse(typeName, out var primitive))
		{
			return new ResolvedFieldType(primitive, false);
		}

		var typeDef = types.FirstOrDefault(t => string.Equals(t.Name, typeName, StringComparison.Ordinal));

		return typeDef switch
		{
			SbePrimitiveType alias => new ResolvedFieldType(
				alias.PrimitiveType,
				false,
				ConstantValue: alias.Presence == Presence.Constant ? alias.ConstantValue : null,
				ArrayLength: alias.Length),
			SbeEnumDefinition e => new ResolvedFieldType(e.EncodingType, true),
			SbeSet s => new ResolvedFieldType(s.EncodingType, true),
			SbeComposite composite => new ResolvedFieldType(
				SbePrimitive.U8,
				false,
				IsComposite: true,
				WireSize: ComputeCompositeWireSize(composite, types),
				CompositeDefinition: composite),
			_ => new ResolvedFieldType(SbePrimitive.U8, false),
		};
	}

	private static int ComputeCompositeWireSize(SbeComposite composite, IReadOnlyList<SbeTypeDefinition> types)
	{
		var size = 0;
		foreach (var field in composite.Fields)
		{
			size += field switch
			{
				SbePrimitiveType pt when pt.Presence == Presence.Constant => 0,
				SbePrimitiveType pt => SbePrimitiveMap.SizeOf(pt.PrimitiveType) * (int)pt.Length,
				SbeEnumDefinition e => SbePrimitiveMap.SizeOf(e.EncodingType),
				SbeSet s => SbePrimitiveMap.SizeOf(s.EncodingType),
				SbeComposite nested => ComputeCompositeWireSize(nested, types),
				SbeRefType refType => ComputeRefWireSize(refType, types),
				_ => 0,
			};
		}

		return size;
	}

	private static int ComputeRefWireSize(SbeRefType refType, IReadOnlyList<SbeTypeDefinition> types)
	{
		var referenced = types.FirstOrDefault(t => string.Equals(t.Name, refType.ReferencedType, StringComparison.Ordinal));
		return referenced switch
		{
			SbePrimitiveType pt when pt.Presence == Presence.Constant => 0,
			SbePrimitiveType pt => SbePrimitiveMap.SizeOf(pt.PrimitiveType) * (int)pt.Length,
			SbeEnumDefinition e => SbePrimitiveMap.SizeOf(e.EncodingType),
			SbeSet s => SbePrimitiveMap.SizeOf(s.EncodingType),
			SbeComposite composite => ComputeCompositeWireSize(composite, types),
			_ => 0,
		};
	}

	private readonly record struct ResolvedFieldType(
		SbePrimitive EncodingPrimitive,
		bool IsEnumOrSet,
		string? ConstantValue = null,
		uint ArrayLength = 1,
		bool IsComposite = false,
		int WireSize = 0,
		SbeComposite? CompositeDefinition = null);
}
