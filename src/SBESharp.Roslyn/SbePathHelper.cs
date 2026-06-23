namespace SBESharp.Roslyn;

/// <summary>
/// File-path utilities used by the schema generator when deriving
/// Roslyn hint names and diagnostic display names from schema file paths.
/// Isolated here so the logic is testable without loading Roslyn assemblies.
/// </summary>
public static class SbePathHelper
{
	/// <summary>
	/// Converts a full file path like <c>/repo/schemas/telemetry.sbe.xml</c>
	/// to the Roslyn hint name <c>telemetry.g.cs</c>.
	/// </summary>
	/// <param name="filePath">The full path of the schema file.</param>
	/// <returns>The Roslyn hint name, e.g. <c>telemetry.g.cs</c>.</returns>
	public static string DeriveHintName(string filePath)
	{
		ArgumentNullException.ThrowIfNull(filePath);

		if (!filePath.EndsWith(".sbe.xml", StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("File path must end with '.sbe.xml'.", nameof(filePath));
		}

		ReadOnlySpan<char> span = filePath.AsSpan();

		int lastSep = span.LastIndexOfAny('/', '\\');
		int extLength = ".sbe.xml".Length;

		return string.Concat(span.Slice(lastSep + 1, span.Length - lastSep - 1 - extLength), ".g.cs");
	}

	/// <summary>Returns just the file name portion of <paramref name="filePath"/>.</summary>
	/// <param name="filePath">The full path of the schema file.</param>
	/// <returns>The file name without any leading directory path.</returns>
	public static string DeriveDisplayName(string filePath)
	{
		ArgumentNullException.ThrowIfNull(filePath);

		ReadOnlySpan<char> span = filePath.AsSpan();
		int lastSep = span.LastIndexOfAny('/', '\\');
		return new string(span.Slice(lastSep + 1));
	}
}
