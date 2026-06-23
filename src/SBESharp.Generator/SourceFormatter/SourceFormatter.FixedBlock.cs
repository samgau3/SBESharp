using SBESharp.Roslyn.Ir;

namespace SBESharp.Generator;

/// <summary>Phase 2a: FixedBlock struct emission for little-endian schemas.</summary>
public sealed partial class SourceFormatter
{
	private void EmitFixedBlock(SbeMessage message, int[] offsets)
	{
		this._writer.WriteLine("[StructLayout(LayoutKind.Explicit, Pack = 1)]");
		this._writer.WriteLine($"internal struct {message.Name}FixedBlock");
		this._writer.WriteLine('{');
		this._writer.Indentation++;

		for (var i = 0; i < message.Fields.Count; i++)
		{
			var field = message.Fields[i];

			if (field.Presence == Presence.Constant)
			{
				continue;
			}

			this._writer.WriteLine($"[FieldOffset({offsets[i]})]");

			if (field.IsComposite)
			{
				this._writer.WriteLine($"public {field.CompositeDefinition!.Name} {EmitHelpers.CapitalizeFirstChar(field.Name)};");
			}
			else if (field.ArrayLength > 1)
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
