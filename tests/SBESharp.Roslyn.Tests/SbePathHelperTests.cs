using SBESharp.Roslyn;

namespace SBESharp.Roslyn.Tests;

public sealed class SbePathHelperTests
{
	[Theory]
	[InlineData("/repo/schemas/telemetry.sbe.xml", "telemetry.g.cs")]
	[InlineData(@"C:\repo\schemas\sensors.sbe.xml", "sensors.g.cs")]
	[InlineData("simple.sbe.xml", "simple.g.cs")]
	[InlineData("/a/b/c/UPPER.SBE.XML", "UPPER.g.cs")]
	public void DeriveHintName_ReturnsBaseNameWithGCs(string filePath, string expected)
	{
		string result = SbePathHelper.DeriveHintName(filePath);

		Assert.Equal(expected, result);
	}

	[Fact]
	public void DeriveHintName_FileNotEndingWithSbeXml_Throws()
	{
		Assert.Throws<ArgumentException>(() => SbePathHelper.DeriveHintName("/schemas/noext"));
	}

	[Fact]
	public void DeriveHintName_MixedSeparators_UsesLast()
	{
		string result = SbePathHelper.DeriveHintName(@"/repo\schemas/file.sbe.xml");

		Assert.Equal("file.g.cs", result);
	}

	[Theory]
	[InlineData("/repo/schemas/telemetry.sbe.xml", "telemetry.sbe.xml")]
	[InlineData(@"C:\repo\schemas\sensors.sbe.xml", "sensors.sbe.xml")]
	[InlineData("simple.sbe.xml", "simple.sbe.xml")]
	public void DeriveDisplayName_ReturnsFileNameOnly(string filePath, string expected)
	{
		string result = SbePathHelper.DeriveDisplayName(filePath);

		Assert.Equal(expected, result);
	}

	[Fact]
	public void DeriveDisplayName_NoSeparator_ReturnsInputUnchanged()
	{
		string result = SbePathHelper.DeriveDisplayName("justname.sbe.xml");

		Assert.Equal("justname.sbe.xml", result);
	}
}
