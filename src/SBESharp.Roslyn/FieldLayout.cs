using SBESharp.Roslyn.Ir;

namespace SBESharp.Roslyn;

/// <summary>Computes wire-layout byte offsets for SBE field lists.</summary>
public static class FieldLayout
{
	/// <summary>
	/// Returns a per-field byte-offset array, respecting explicit <c>offset</c> attributes
	/// and falling back to sequential packing. Constant fields get offset -1 (not on wire).
	/// </summary>
	/// <param name="fields">The ordered list of SBE fields.</param>
	/// <returns>An array of byte offsets, one per field.</returns>
	public static int[] ComputeOffsets(IReadOnlyList<SbeField> fields)
	{
		ArgumentNullException.ThrowIfNull(fields);

		var offsets = new int[fields.Count];
		var cursor = 0;

		for (var i = 0; i < fields.Count; i++)
		{
			var field = fields[i];

			if (field.Presence == Presence.Constant)
			{
				offsets[i] = -1;
				continue;
			}

			var size = ComputeFieldWireSize(field);

			if (field.Offset.HasValue)
			{
				offsets[i] = field.Offset.Value;
				cursor = field.Offset.Value + size;
			}
			else
			{
				offsets[i] = cursor;
				cursor += size;
			}
		}

		return offsets;
	}

	/// <summary>Computes the wire size of a single field in bytes.</summary>
	/// <param name="field">The field to compute the wire size for.</param>
	/// <returns>The wire size in bytes.</returns>
	public static int ComputeFieldWireSize(SbeField field)
	{
		ArgumentNullException.ThrowIfNull(field);
		if (field.Presence == Presence.Constant)
		{
			return 0;
		}

		if (field.IsComposite)
		{
			return field.WireSize;
		}

		return SbePrimitiveMap.SizeOf(field.EncodingPrimitive) * (int)field.ArrayLength;
	}
}
