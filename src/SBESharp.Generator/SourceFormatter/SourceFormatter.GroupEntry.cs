using SBESharp.Roslyn.Ir;

namespace SBESharp.Generator;

/// <summary>Phase 2b: Group entry struct emission.</summary>
public sealed partial class SourceFormatter
{
	private void EmitGroupEntry(string messageName, SbeGroup group, int[] offsets)
	{
		var entryTypeName = EmitHelpers.GroupEntryTypeName(messageName, group.Name);

		this._writer.WriteDocComment(group.Description ?? $"Wire-layout entry for the {group.Name} repeating group.");
		this._writer.WriteLine("[StructLayout(LayoutKind.Explicit, Pack = 1)]");
		this._writer.WriteLine($"public struct {entryTypeName}");
		this._writer.WriteLine('{');
		this._writer.Indentation++;

		foreach (var field in group.Fields)
		{
			if (field.Presence != Presence.Constant)
			{
				continue;
			}

			this.EmitConstantProperty(field);
		}

		for (var i = 0; i < group.Fields.Count; i++)
		{
			var field = group.Fields[i];

			if (field.Presence == Presence.Constant)
			{
				continue;
			}

			this._writer.WriteLine($"[FieldOffset({offsets[i]})]");

			if (field.ArrayLength > 1)
			{
				this._writer.WriteLine($"public {InlineArrayTypeName(field)} {EmitHelpers.CapitalizeFirstChar(field.Name)};");
			}
			else
			{
				this._writer.WriteLine($"public {GetCSharpType(field)} {EmitHelpers.CapitalizeFirstChar(field.Name)};");
			}
		}

		this._writer.Indentation--;
		this._writer.WriteLine('}');
		this._writer.WriteLine();
	}
}
