using SBESharp.Roslyn;
using SBESharp.Roslyn.Ir;

namespace SBESharp.Generator;

/// <summary>Phase 4b: Group encoder ref struct emission.</summary>
public sealed partial class SourceFormatter
{
	private void EmitGroupEncoder(string messageName, SbeGroup group, int[] offsets)
	{
		var encoderName = $"{messageName}{EmitHelpers.CapitalizeFirstChar(group.Name)}GroupEncoder";
		var blockLength = ComputeGroupBlockLength(group);
		var dim = this.ResolveGroupDimension(group);
		var writeBlockLength = EmitHelpers.BuildWriteExpression(dim.BlockLengthCsType, this._schema.ByteOrder, "buffer.Slice(offset)", $"({dim.BlockLengthCsType}){blockLength}");
		var writeNumInGroup = EmitHelpers.BuildWriteExpression(dim.NumInGroupCsType, this._schema.ByteOrder, $"buffer.Slice(offset + {dim.BlockLengthSize})", $"({dim.NumInGroupCsType})count");

		this._writer.WriteLine($$"""
			/// <summary>Zero-allocation encoder for the {{group.Name}} repeating group.</summary>
			public ref struct {{encoderName}}
			{
				private readonly Span<byte> _buffer;
				private readonly int _headerOffset;
				private readonly int _count;
				private int _index;

				/// <summary>Writes the group header and returns an encoder for the entries.</summary>
				public static {{encoderName}} Open(Span<byte> buffer, int offset, int count)
				{
					{{writeBlockLength}};
					{{writeNumInGroup}};
					return new {{encoderName}}(buffer, offset, count);
				}

				private {{encoderName}}(Span<byte> buffer, int headerOffset, int count)
				{
					_buffer = buffer;
					_headerOffset = headerOffset;
					_count = count;
					_index = 0;
				}

				/// <summary>Moves to the next entry slot.</summary>
				public {{encoderName}} NextEntry()
				{
					_index++;
					return this;
				}

				/// <summary>Gets the total number of bytes consumed by this group (header + all entries).</summary>
				public int TotalSize => {{dim.HeaderSize}} + (_count * {{blockLength}});

				private int EntryOffset => _headerOffset + {{dim.HeaderSize}} + _index * {{blockLength}};
			""");
		this._writer.Indentation++;

		for (var i = 0; i < group.Fields.Count; i++)
		{
			var field = group.Fields[i];

			if (field.Presence == Presence.Constant)
			{
				continue;
			}

			var csType = GetCSharpType(field);
			var underlying = GetSbeType(field);
			var valueExpr = field.IsEnumOrSet ? $"({underlying})value" : "value";
			var writeExpr = EmitHelpers.BuildWriteExpression(underlying, this._schema.ByteOrder, $"_buffer.Slice(EntryOffset + {offsets[i]})", valueExpr);

			this._writer.WriteLine($$"""

				/// <summary>Sets the <c>{{field.Name}}</c> field on the current entry.</summary>
				public {{encoderName}} Set{{EmitHelpers.CapitalizeFirstChar(field.Name)}}({{csType}} value)
				{
					{{writeExpr}};
					return this;
				}
				""");
		}

		this._writer.Indentation--;
		this._writer.WriteLine('}');
		this._writer.WriteLine();
	}
}
