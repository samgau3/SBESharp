using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SBESharp.Roslyn;
using SBESharp.Roslyn.Parsing;

namespace SBESharp.Generator;

/// <summary>
/// Roslyn incremental source generator that transforms <c>*.sbe.xml</c> schema files
/// listed as <c>&lt;AdditionalFiles /&gt;</c> into zero-allocation C# encoder/decoder types.
/// </summary>
[Generator]
public sealed class SbeSchemaGenerator : IIncrementalGenerator
{
	private static readonly DiagnosticDescriptor ParseError = new DiagnosticDescriptor(
		id: "SBE001",
		title: "SBE schema parse error",
		messageFormat: "Failed to parse SBE schema '{0}': {1}",
		category: "SBESharp.Generator",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		description: "The SBE XML schema could not be parsed. Verify that it is well-formed and conforms to the SBE specification.");

	/// <inheritdoc/>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Select extracts file metadata + text content. The resulting tuple has value equality
		// (strings compare by content), so Roslyn's incremental cache skips re-emission when
		// the schema file hasn't actually changed.
		var pipeline = context.AdditionalTextsProvider
			.Where(static file => file.Path.EndsWith(".sbe.xml", StringComparison.OrdinalIgnoreCase))
			.Select(static (file, ct) => (
				HintName: SbePathHelper.DeriveHintName(file.Path),
				FilePath: file.Path,
				Text: file.GetText(ct)?.ToString()))
			.Where(static t => t.Text is not null);

		context.RegisterSourceOutput(pipeline, static (spc, input) =>
		{
			try
			{
				var schema = SbeXmlParser.Parse(input.Text!, spc.CancellationToken);
				spc.AddSource(input.HintName, SourceFormatter.GenerateSourceFiles(schema));
			}
			catch (Exception ex) when (ex is FormatException or InvalidOperationException or XmlException)
			{
				spc.ReportDiagnostic(Diagnostic.Create(
					ParseError,
					Location.Create(
						input.FilePath,
						TextSpan.FromBounds(0, 0),
						new LinePositionSpan(LinePosition.Zero, LinePosition.Zero)),
					SbePathHelper.DeriveDisplayName(input.FilePath),
					ex.Message));
			}
		});
	}
}
