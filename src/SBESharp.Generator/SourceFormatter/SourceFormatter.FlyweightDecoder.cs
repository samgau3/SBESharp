using SBESharp.Roslyn;
using SBESharp.Roslyn.Ir;

namespace SBESharp.Generator;

/// <summary>Phase 7: Flyweight decoder emission for complex groups (nested groups and/or varData).</summary>
public sealed partial class SourceFormatter
{
	private void EmitFlyweightDecoder(string messageName, SbeGroup group)
	{
		var decoderName = $"{messageName}{EmitHelpers.CapitalizeFirstChar(group.Name)}Decoder";
		var dim = this.ResolveGroupDimension(group);
		var blockLength = ComputeGroupBlockLength(group);
		var groupOffsets = FieldLayout.ComputeOffsets(group.Fields);

		this._writer.WriteDocComment($"Flyweight decoder for the {group.Name} complex repeating group.");
		this._writer.WriteLine($"public ref struct {decoderName}");
		this._writer.WriteLine('{');
		this._writer.Indentation++;

		this._writer.WriteLine("private readonly ReadOnlySpan<byte> _data;");
		this._writer.WriteLine("private int _offset;");
		this._writer.WriteLine("private readonly int _count;");
		this._writer.WriteLine("private readonly int _entryBlockLength;");
		this._writer.WriteLine("private int _index;");
		this._writer.WriteLine();

		var rawBlockLengthExpr = EmitHelpers.BuildReadExpression(dim.BlockLengthCsType, this._schema.ByteOrder, "data");
		var rawNumInGroupExpr = EmitHelpers.BuildReadExpression(dim.NumInGroupCsType, this._schema.ByteOrder, $"data.Slice({dim.BlockLengthSize})");

		this._writer.WriteLine($$"""
			internal {{decoderName}}(ReadOnlySpan<byte> data)
			{
				ArgumentOutOfRangeException.ThrowIfLessThan(data.Length, {{dim.HeaderSize}}, nameof(data));
				_data = data;
				_entryBlockLength = {{rawBlockLengthExpr}};
				_count = (int)({{rawNumInGroupExpr}});
				_offset = {{dim.HeaderSize}};
				_index = -1;
			}
			""");
		this._writer.WriteLine();

		this._writer.WriteLine("/// <summary>Gets the number of entries in this group.</summary>");
		this._writer.WriteLine("public int Count => _count;");
		this._writer.WriteLine();

		this._writer.WriteLine($$"""
			/// <summary>Advances to the next entry. Must be called before accessing fields.</summary>
			public bool MoveNext()
			{
				if (_index >= 0)
				{
					AdvancePastCurrentEntry();
				}
				_index++;
				return _index < _count;
			}
			""");
		this._writer.WriteLine();

		for (var i = 0; i < group.Fields.Count; i++)
		{
			var field = group.Fields[i];

			if (field.Presence == Presence.Constant)
			{
				this.EmitConstantProperty(field);
				continue;
			}

			var capName = EmitHelpers.CapitalizeFirstChar(field.Name);
			var csType = GetCSharpType(field);
			var underlying = GetSbeType(field);
			var readExpr = EmitHelpers.BuildReadExpression(underlying, this._schema.ByteOrder, $"_data.Slice(_offset + {groupOffsets[i]})");

			if (field.IsEnumOrSet)
			{
				readExpr = $"({csType}){readExpr}";
			}

			this._writer.WriteDocComment(field.Description);
			this._writer.WriteLine($"public {csType} {capName} => {readExpr};");
			this._writer.WriteLine();
		}

		foreach (var nested in group.Groups)
		{
			var nestedDecoderName = $"{messageName}{EmitHelpers.CapitalizeFirstChar(nested.Name)}Decoder";
			var capName = EmitHelpers.CapitalizeFirstChar(nested.Name);
			this._writer.WriteDocComment($"Returns a decoder for the nested {nested.Name} group within the current entry.");
			this._writer.WriteLine($"public {nestedDecoderName} Get{capName}() => new {nestedDecoderName}(_data.Slice(_offset + _entryBlockLength));");
			this._writer.WriteLine();
		}

		this.EmitFlyweightVarDataAccessors(group);

		this._writer.WriteLine("private void AdvancePastCurrentEntry()");
		this._writer.WriteLine('{');
		this._writer.Indentation++;
		this._writer.WriteLine("_offset += _entryBlockLength;");

		foreach (var nested in group.Groups)
		{
			var nestedDecoderName = $"{messageName}{EmitHelpers.CapitalizeFirstChar(nested.Name)}Decoder";
			this._writer.WriteLine('{');
			this._writer.Indentation++;
			this._writer.WriteLine($"var dec = new {nestedDecoderName}(_data.Slice(_offset));");
			this._writer.WriteLine("while (dec.MoveNext()) { }");
			this._writer.WriteLine("_offset += dec.BytesConsumed;");
			this._writer.Indentation--;
			this._writer.WriteLine('}');
		}

		foreach (var data in group.DataFields)
		{
			var enc = this.ResolveVarDataEncoding(data);
			var readLen = EmitHelpers.BuildReadExpression(enc.LengthCsType, this._schema.ByteOrder, "_data.Slice(_offset)");
			this._writer.WriteLine('{');
			this._writer.Indentation++;
			this._writer.WriteLine($"int len = (int)({readLen});");
			this._writer.WriteLine($"_offset += {enc.LengthSize} + len;");
			this._writer.Indentation--;
			this._writer.WriteLine('}');
		}

		this._writer.Indentation--;
		this._writer.WriteLine('}');
		this._writer.WriteLine();

		this._writer.WriteLine("/// <summary>Gets the total bytes consumed by this group (header + all entries).</summary>");
		this._writer.WriteLine("public int BytesConsumed => _offset;");

		this._writer.Indentation--;
		this._writer.WriteLine('}');
		this._writer.WriteLine();

		this.EmitSkipHelper(messageName, group, decoderName);

		foreach (var nested in group.Groups)
		{
			this.EmitFlyweightDecoder(messageName, nested);
		}
	}

	private void EmitFlyweightVarDataAccessors(SbeGroup group)
	{
		if (group.DataFields.Count == 0)
		{
			return;
		}

		for (var dataIdx = 0; dataIdx < group.DataFields.Count; dataIdx++)
		{
			var data = group.DataFields[dataIdx];
			var capName = EmitHelpers.CapitalizeFirstChar(data.Name);
			var enc = this.ResolveVarDataEncoding(data);

			this._writer.WriteDocComment(data.Description);
			this._writer.WriteLine($"public ReadOnlySpan<byte> Get{capName}()");
			this._writer.WriteLine('{');
			this._writer.Indentation++;

			this._writer.WriteLine("int pos = _offset + _entryBlockLength;");

			foreach (var nested in group.Groups)
			{
				this._writer.WriteLine('{');
				this._writer.Indentation++;

				if (IsSimpleGroup(nested))
				{
					var nestedDim = this.ResolveGroupDimension(nested);
					var readBl = EmitHelpers.BuildReadExpression(nestedDim.BlockLengthCsType, this._schema.ByteOrder, "_data.Slice(pos)");
					var readNig = EmitHelpers.BuildReadExpression(nestedDim.NumInGroupCsType, this._schema.ByteOrder, $"_data.Slice(pos + {nestedDim.BlockLengthSize})");
					this._writer.WriteLine($"int nestedBl = {readBl};");
					this._writer.WriteLine($"int nestedCount = (int)({readNig});");
					this._writer.WriteLine($"pos += {nestedDim.HeaderSize} + (nestedCount * nestedBl);");
				}
				else
				{
					var nestedDim = this.ResolveGroupDimension(nested);
					var readBl = EmitHelpers.BuildReadExpression(nestedDim.BlockLengthCsType, this._schema.ByteOrder, "_data.Slice(pos)");
					var readNig = EmitHelpers.BuildReadExpression(nestedDim.NumInGroupCsType, this._schema.ByteOrder, $"_data.Slice(pos + {nestedDim.BlockLengthSize})");
					this._writer.WriteLine($"int nestedBl = {readBl};");
					this._writer.WriteLine($"int nestedCount = (int)({readNig});");
					this._writer.WriteLine($"pos += {nestedDim.HeaderSize};");
					this._writer.WriteLine("for (int ni = 0; ni < nestedCount; ni++)");
					this._writer.WriteLine('{');
					this._writer.Indentation++;
					this._writer.WriteLine("pos += nestedBl;");

					foreach (var nn in nested.Groups)
					{
						this.EmitSkipNestedGroupInline(nn);
					}

					foreach (var nd in nested.DataFields)
					{
						var nestedEnc = this.ResolveVarDataEncoding(nd);
						var readNLen = EmitHelpers.BuildReadExpression(nestedEnc.LengthCsType, this._schema.ByteOrder, "_data.Slice(pos)");
						this._writer.WriteLine($"pos += {nestedEnc.LengthSize} + (int)({readNLen});");
					}

					this._writer.Indentation--;
					this._writer.WriteLine('}');
				}

				this._writer.Indentation--;
				this._writer.WriteLine('}');
			}

			for (var priorIdx = 0; priorIdx < dataIdx; priorIdx++)
			{
				var priorData = group.DataFields[priorIdx];
				var dEnc = this.ResolveVarDataEncoding(priorData);
				var readDLen = EmitHelpers.BuildReadExpression(dEnc.LengthCsType, this._schema.ByteOrder, "_data.Slice(pos)");
				this._writer.WriteLine($"pos += {dEnc.LengthSize} + (int)({readDLen});");
			}

			var readTargetLen = EmitHelpers.BuildReadExpression(enc.LengthCsType, this._schema.ByteOrder, "_data.Slice(pos)");
			this._writer.WriteLine($"int dataLen = (int)({readTargetLen});");
			this._writer.WriteLine($"return _data.Slice(pos + {enc.LengthSize}, dataLen);");
			this._writer.Indentation--;
			this._writer.WriteLine('}');
			this._writer.WriteLine();
		}
	}

	private void EmitSkipNestedGroupInline(SbeGroup nested)
	{
		var nestedDim = this.ResolveGroupDimension(nested);
		var readBl = EmitHelpers.BuildReadExpression(nestedDim.BlockLengthCsType, this._schema.ByteOrder, "_data.Slice(pos)");
		var readNig = EmitHelpers.BuildReadExpression(nestedDim.NumInGroupCsType, this._schema.ByteOrder, $"_data.Slice(pos + {nestedDim.BlockLengthSize})");

		this._writer.WriteLine('{');
		this._writer.Indentation++;
		this._writer.WriteLine($"int nnBl = {readBl};");
		this._writer.WriteLine($"int nnCount = (int)({readNig});");
		this._writer.WriteLine($"pos += {nestedDim.HeaderSize} + (nnCount * nnBl);");
		this._writer.Indentation--;
		this._writer.WriteLine('}');
	}

	private void EmitSkipHelper(string messageName, SbeGroup group, string decoderName)
	{
		var capName = EmitHelpers.CapitalizeFirstChar(group.Name);

		this._writer.WriteLine($"private static int Skip{capName}Group(ReadOnlySpan<byte> data)");
		this._writer.WriteLine('{');
		this._writer.Indentation++;
		this._writer.WriteLine($"var dec = new {decoderName}(data);");
		this._writer.WriteLine("while (dec.MoveNext()) { }");
		this._writer.WriteLine("return dec.BytesConsumed;");
		this._writer.Indentation--;
		this._writer.WriteLine('}');
		this._writer.WriteLine();
	}
}
