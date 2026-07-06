using SBESharp.Roslyn;
using SBESharp.Roslyn.Ir;

namespace SBESharp.Generator;

/// <summary>Phase 7b: Flyweight encoder emission for complex groups (nested groups and/or varData).</summary>
public sealed partial class SourceFormatter
{
	private void EmitFlyweightEncoder(string messageName, SbeGroup group)
	{
		var encoderName = $"{messageName}{EmitHelpers.CapitalizeFirstChar(group.Name)}GroupEncoder";
		var blockLength = ComputeGroupBlockLength(group);
		var dim = this.ResolveGroupDimension(group);
		var groupOffsets = FieldLayout.ComputeOffsets(group.Fields);
		var writeBlockLength = EmitHelpers.BuildWriteExpression(dim.BlockLengthCsType, this._schema.ByteOrder, "buffer.Slice(offset)", $"({dim.BlockLengthCsType}){blockLength}");
		var writeNumInGroup = EmitHelpers.BuildWriteExpression(dim.NumInGroupCsType, this._schema.ByteOrder, $"buffer.Slice(offset + {dim.BlockLengthSize})", $"({dim.NumInGroupCsType})count");
		var writeBlockLengthRegion = EmitHelpers.BuildWriteExpression(dim.BlockLengthCsType, this._schema.ByteOrder, "region.Slice(0)", $"({dim.BlockLengthCsType}){blockLength}");
		var writeNumInGroupRegion = EmitHelpers.BuildWriteExpression(dim.NumInGroupCsType, this._schema.ByteOrder, $"region.Slice({dim.BlockLengthSize})", $"({dim.NumInGroupCsType})count");

		this._writer.WriteDocComment($"Encoder for the {group.Name} complex repeating group.");
		this._writer.WriteLine($"public ref struct {encoderName}");
		this._writer.WriteLine('{');
		this._writer.Indentation++;

		this._writer.WriteLine("private Span<byte> _buffer;");
		this._writer.WriteLine("private int _offset;");
		this._writer.WriteLine("private readonly int _headerOffset;");
		this._writer.WriteLine();

		// Open static factory (offset-based, caller-sized buffer)
		this._writer.WriteLine($$"""
			/// <summary>Writes the group header and returns an encoder for the entries.</summary>
			public static {{encoderName}} Open(Span<byte> buffer, int offset, int count)
			{
				{{writeBlockLength}};
				{{writeNumInGroup}};
				return new {{encoderName}}(buffer, offset);
			}

			/// <summary>Reserves the group header on <paramref name="writer"/> and returns an encoder positioned at the first entry. Pass <paramref name="writer"/> to <c>Open*</c>, <c>Write*</c>, and <c>NextEntry</c> as each entry is built.</summary>
			public static {{encoderName}} Open(ref SbeMessageWriter writer, int count)
			{
				var region = writer.GetSpan({{dim.HeaderSize}});
				{{writeBlockLengthRegion}};
				{{writeNumInGroupRegion}};
				writer.Advance({{dim.HeaderSize}});
				var entry = default(Span<byte>);
				if (count > 0)
				{
					entry = writer.GetSpan({{blockLength}});
					writer.Advance({{blockLength}});
				}

				return new {{encoderName}}(entry);
			}

			private {{encoderName}}(Span<byte> buffer, int headerOffset)
			{
				_buffer = buffer;
				_headerOffset = headerOffset;
				_offset = headerOffset + {{dim.HeaderSize}};
			}

			private {{encoderName}}(Span<byte> entry)
			{
				_buffer = entry;
				_headerOffset = 0;
				_offset = 0;
			}
			""");
		this._writer.WriteLine();

		// Field setters on current entry
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
			var writeExpr = EmitHelpers.BuildWriteExpression(underlying, this._schema.ByteOrder, $"_buffer.Slice(_offset + {groupOffsets[i]})", valueExpr);

			this._writer.WriteLine($$"""
				/// <summary>Sets the <c>{{field.Name}}</c> field on the current entry.</summary>
				public {{encoderName}} Set{{EmitHelpers.CapitalizeFirstChar(field.Name)}}({{csType}} value)
				{
					{{writeExpr}};
					return this;
				}
				""");
		}

		// Nested group encoder access
		foreach (var nested in group.Groups)
		{
			var nestedEncoderName = $"{messageName}{EmitHelpers.CapitalizeFirstChar(nested.Name)}GroupEncoder";
			var capName = EmitHelpers.CapitalizeFirstChar(nested.Name);

			this._writer.WriteLine($$"""

				/// <summary>Opens a nested {{nested.Name}} group encoder at the current position past the fixed block.</summary>
				public {{nestedEncoderName}} Open{{capName}}(int count)
				{
					return {{nestedEncoderName}}.Open(_buffer, _offset + {{blockLength}}, count);
				}

				/// <summary>Opens a nested {{nested.Name}} group encoder on <paramref name="writer"/> at the current position.</summary>
				public {{nestedEncoderName}} Open{{capName}}(ref SbeMessageWriter writer, int count)
				{
					return {{nestedEncoderName}}.Open(ref writer, count);
				}
				""");
		}

		// VarData write methods
		foreach (var data in group.DataFields)
		{
			var enc = this.ResolveVarDataEncoding(data);
			var capName = EmitHelpers.CapitalizeFirstChar(data.Name);
			var writeLenExpr = EmitHelpers.BuildWriteExpression(enc.LengthCsType, this._schema.ByteOrder, "buffer.Slice(varOffset)", $"({enc.LengthCsType})value.Length");
			var writeLenExprRegion = EmitHelpers.BuildWriteExpression(enc.LengthCsType, this._schema.ByteOrder, "region.Slice(0)", $"({enc.LengthCsType})value.Length");

			this._writer.WriteLine($$"""

				/// <summary>Writes the <c>{{data.Name}}</c> variable-length data at the given offset. Returns bytes written.</summary>
				public static int Write{{capName}}(Span<byte> buffer, int varOffset, ReadOnlySpan<byte> value)
				{
					{{writeLenExpr}};
					value.CopyTo(buffer.Slice(varOffset + {{enc.LengthSize}}));
					return {{enc.LengthSize}} + value.Length;
				}

				/// <summary>Writes the <c>{{data.Name}}</c> variable-length data to <paramref name="writer"/>. Returns bytes written.</summary>
				public static int Write{{capName}}(ref SbeMessageWriter writer, ReadOnlySpan<byte> value)
				{
					int size = {{enc.LengthSize}} + value.Length;
					var region = writer.GetSpan(size);
					{{writeLenExprRegion}};
					value.CopyTo(region.Slice({{enc.LengthSize}}));
					writer.Advance(size);
					return size;
				}
				""");
		}

		// NextEntry — advances offset past current entry's fixed block + nested content
		this._writer.WriteLine($$"""

			/// <summary>Advances to the next entry. Pass the total nested size (nested groups + varData) written after the fixed block.</summary>
			public {{encoderName}} NextEntry(int nestedBytesWritten = 0)
			{
				_offset += {{blockLength}} + nestedBytesWritten;
				return this;
			}

			/// <summary>Reserves the next entry's fixed block on <paramref name="writer"/> and advances to it.</summary>
			public {{encoderName}} NextEntry(ref SbeMessageWriter writer)
			{
				_buffer = writer.GetSpan({{blockLength}});
				_offset = 0;
				writer.Advance({{blockLength}});
				return this;
			}

			/// <summary>Gets the current write position for positioning nested content.</summary>
			public int CurrentEntryEnd => _offset + {{blockLength}};

			/// <summary>Gets the total bytes consumed by this group from header start to current position.</summary>
			public int TotalSize => _offset - _headerOffset;
			""");

		this._writer.Indentation--;
		this._writer.WriteLine('}');
		this._writer.WriteLine();

		// Recursively emit encoders for nested groups
		foreach (var nested in group.Groups)
		{
			if (IsSimpleGroup(nested))
			{
				var nestedOffsets = FieldLayout.ComputeOffsets(nested.Fields);
				this.EmitGroupEncoder(messageName, nested, nestedOffsets);
			}
			else
			{
				this.EmitFlyweightEncoder(messageName, nested);
			}
		}
	}
}
