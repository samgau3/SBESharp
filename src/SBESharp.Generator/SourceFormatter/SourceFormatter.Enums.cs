using SBESharp.Roslyn.Ir;

namespace SBESharp.Generator;

/// <summary>Phase 1: Enum and set type emission.</summary>
public sealed partial class SourceFormatter
{
	private void EmitEnums()
	{
		var emitted = new HashSet<string>(StringComparer.Ordinal);

		foreach (var sbeEnum in this.AllEnumDefinitions())
		{
			if (!emitted.Add(sbeEnum.Name))
			{
				continue;
			}

			this._writer.WriteDocComment(sbeEnum.Description);
			this._writer.WriteLine($"public enum {sbeEnum.Name} : {SbePrimitiveMap.ToCSharp(sbeEnum.EncodingType)}");
			this._writer.WriteLine('{');
			this._writer.Indentation++;

			foreach (var value in sbeEnum.ValidValues)
			{
				this._writer.WriteDocComment(value.Description);
				this._writer.WriteLine($"{value.Name} = {value.Value},");
			}

			this._writer.Indentation--;
			this._writer.WriteLine('}');
			this._writer.WriteLine();
		}
	}

	private void EmitSets()
	{
		var emitted = new HashSet<string>(StringComparer.Ordinal);

		foreach (var sbeSet in this.AllSetDefinitions())
		{
			if (!emitted.Add(sbeSet.Name))
			{
				continue;
			}

			this._writer.WriteDocComment(sbeSet.Description);
			this._writer.WriteLine("[Flags]");
			this._writer.WriteLine($"public enum {sbeSet.Name} : {SbePrimitiveMap.ToCSharp(sbeSet.EncodingType)}");
			this._writer.WriteLine('{');
			this._writer.Indentation++;
			this._writer.WriteLine("None = 0,");

			foreach (var choice in sbeSet.Choices)
			{
				this._writer.WriteDocComment(choice.Description);
				this._writer.WriteLine($"{choice.Name} = 1 << {choice.BitIndex},");
			}

			this._writer.Indentation--;
			this._writer.WriteLine('}');
			this._writer.WriteLine();
		}
	}

	/// <summary>Top-level enum definitions followed by enums embedded within composite-typed fields.</summary>
	private IEnumerable<SbeEnumDefinition> AllEnumDefinitions()
	{
		foreach (var typeDef in this._schema.Types)
		{
			if (typeDef is SbeEnumDefinition sbeEnum)
			{
				yield return sbeEnum;
			}
		}

		foreach (var composite in this._fieldComposites)
		{
			foreach (var element in composite.Fields)
			{
				if (element is SbeEnumDefinition embedded)
				{
					yield return embedded;
				}
			}
		}
	}

	/// <summary>Top-level set definitions followed by sets embedded within composite-typed fields.</summary>
	private IEnumerable<SbeSet> AllSetDefinitions()
	{
		foreach (var typeDef in this._schema.Types)
		{
			if (typeDef is SbeSet sbeSet)
			{
				yield return sbeSet;
			}
		}

		foreach (var composite in this._fieldComposites)
		{
			foreach (var element in composite.Fields)
			{
				if (element is SbeSet embedded)
				{
					yield return embedded;
				}
			}
		}
	}
}
