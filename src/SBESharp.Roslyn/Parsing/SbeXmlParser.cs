using System.Xml.Linq;
using SBESharp.Roslyn.Ir;

namespace SBESharp.Roslyn.Parsing;

/// <summary>Parses an SBE XML schema document into the generator's typed IR.</summary>
public static partial class SbeXmlParser
{
	/// <summary>
	/// Parses the given SBE XML schema text and returns the corresponding <see cref="SbeSchema"/>.
	/// </summary>
	/// <param name="xmlContent">The raw XML text of the schema file.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A fully populated <see cref="SbeSchema"/> IR node.</returns>
	/// <exception cref="FormatException">Thrown when the XML is missing required structure.</exception>
	public static SbeSchema Parse(string xmlContent, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(xmlContent);

		var doc = XDocument.Parse(xmlContent);
		var root = doc.Root ?? throw new FormatException("SBE schema XML has no root element.");

		var typesElements = GetChildren(root, SbeXmlNames.Elements.Types).ToList();
		if (typesElements.Count == 0)
		{
			throw new FormatException($"<{root.Name.LocalName}> is missing required child element <{SbeXmlNames.Elements.Types}>.");
		}

		var types = typesElements
			.SelectMany(typesEl => ParseTypes(typesEl, cancellationToken))
			.ToList();

		return new SbeSchema(
			Package: root.Attribute(SbeXmlNames.Attributes.Package)?.Value ?? string.Empty,
			Id: ParseUInt16(root.Attribute(SbeXmlNames.Attributes.Id)?.Value),
			Version: ParseUInt16(root.Attribute(SbeXmlNames.Attributes.Version)?.Value),
			ByteOrder: ParseByteOrder(root.Attribute(SbeXmlNames.Attributes.ByteOrder)?.Value),
			Types: types,
			Messages: ParseMessages(root, types, cancellationToken),
			Description: root.Attribute(SbeXmlNames.Attributes.Description)?.Value,
			SemanticVersion: root.Attribute(SbeXmlNames.Attributes.SemanticVersion)?.Value,
			HeaderType: root.Attribute(SbeXmlNames.Attributes.HeaderType)?.Value);
	}
}
