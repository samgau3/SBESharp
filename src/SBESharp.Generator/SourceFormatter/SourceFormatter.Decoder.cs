using SBESharp.Roslyn;
using SBESharp.Roslyn.Ir;

namespace SBESharp.Generator;

/// <summary>Phase 3: Message decoder struct, deserialization body, and group parse helpers.</summary>
public sealed partial class SourceFormatter
{
	private void EmitMessageStruct(SbeMessage message, int[] offsets, int blockLength)
	{
		var hasGroups = message.Groups.Count > 0;
		var hasVarData = message.DataFields.Count > 0;
		var hasComplexGroups = message.Groups.Any(g => !IsSimpleGroup(g));

		// LE array fields are exposed as spans into the buffer, which forces a ref struct;
		// BE array fields materialize as heap arrays and do not.
		var hasArrayFields = message.Fields.Any(f => f.Presence != Presence.Constant && f.ArrayLength > 1);
		var needsRefStruct = hasVarData || hasComplexGroups || (this.IsLittleEndian && (hasGroups || hasArrayFields));
		var structKind = needsRefStruct ? "ref struct" : "struct";

		this._writer.WriteDocComment(message.Description ?? $"SBE message {message.Name}.");
		this._writer.WriteLine($"public {structKind} {message.Name} : ISbeDeserializable<{message.Name}>");
		this._writer.WriteLine('{');
		this._writer.Indentation++;

		// Public constants first (SA1202)
		this._writer.WriteLine($$"""
			/// <summary>SBE template identifier for this message.</summary>
			public const ushort TemplateId = {{message.Id}};

			/// <summary>SBE schema identifier.</summary>
			public const ushort SchemaId = {{this._schema.Id}};

			/// <summary>Wire block length declared in the schema.</summary>
			public const ushort SbeBlockLength = {{blockLength}};
			""");
		this._writer.WriteLine();

		// Constant fields as static properties
		foreach (var field in message.Fields)
		{
			if (field.Presence != Presence.Constant)
			{
				continue;
			}

			this.EmitConstantProperty(field);
		}

		// Public instance fields
		foreach (var field in message.Fields)
		{
			if (field.Presence == Presence.Constant)
			{
				continue;
			}

			this._writer.WriteDocComment(field.Description);

			if (field.IsComposite)
			{
				this._writer.WriteLine($"public {field.CompositeDefinition!.Name} {EmitHelpers.CapitalizeFirstChar(field.Name)};");
			}
			else if (field.ArrayLength > 1)
			{
				var arrayFieldType = this.IsLittleEndian
					? $"ReadOnlySpan<{GetSbeType(field)}>"
					: $"{GetSbeType(field)}[]";
				this._writer.WriteLine($"public {arrayFieldType} {EmitHelpers.CapitalizeFirstChar(field.Name)};");
			}
			else
			{
				this._writer.WriteLine($"public {GetCSharpType(field)} {EmitHelpers.CapitalizeFirstChar(field.Name)};");
			}

			this._writer.WriteLine();
		}

		// Simple group fields
		foreach (var group in message.Groups)
		{
			if (IsSimpleGroup(group))
			{
				this._writer.WriteDocComment(group.Description);
				var groupFieldType = this.IsLittleEndian
					? $"SbeGroupView<{EmitHelpers.GroupEntryTypeName(message.Name, group.Name)}>"
					: $"{EmitHelpers.GroupEntryTypeName(message.Name, group.Name)}[]";
				this._writer.WriteLine($"public {groupFieldType} {EmitHelpers.CapitalizeFirstChar(group.Name)};");
				this._writer.WriteLine();
			}
		}

		// VarData fields
		foreach (var data in message.DataFields)
		{
			this._writer.WriteDocComment(data.Description);
			this._writer.WriteLine($"public ReadOnlySpan<byte> {EmitHelpers.CapitalizeFirstChar(data.Name)};");
			this._writer.WriteLine();
		}

		// Public Deserialize (SA1202: public before private)
		this._writer.WriteLine("/// <summary>Deserializes an instance from <paramref name=\"buffer\"/>, automatically skipping the leading message header.</summary>");
		this._writer.WriteLine($"public static {message.Name} Deserialize(ReadOnlySpan<byte> buffer)");
		this._writer.WriteLine('{');
		this._writer.Indentation++;
		this._writer.WriteLine("ReadOnlySpan<byte> body = buffer.Slice(SbeMessageHeader.EncodedLength);");
		this.EmitDeserializeBody(message, offsets);
		this._writer.Indentation--;
		this._writer.WriteLine('}');
		this._writer.WriteLine();

		// Private Parse helpers (SA1202: private after public)
		foreach (var group in message.Groups)
		{
			if (IsSimpleGroup(group))
			{
				var groupOffsets = FieldLayout.ComputeOffsets(group.Fields);
				this.EmitGroupParseHelper(message.Name, group, groupOffsets);
			}
		}

		// Complex group flyweight decoders
		foreach (var group in message.Groups)
		{
			if (!IsSimpleGroup(group))
			{
				this.EmitFlyweightDecoder(message.Name, group);
			}
		}

		this._writer.Indentation--;
		this._writer.WriteLine('}');
		this._writer.WriteLine();
	}

	private void EmitConstantProperty(SbeField field)
	{
		var capName = EmitHelpers.CapitalizeFirstChar(field.Name);

		if (field.ValueRef is not null)
		{
			var csType = GetCSharpType(field);
			var resolvedValue = this.ResolveValueRef(field.ValueRef, csType);
			this._writer.WriteDocComment(field.Description);
			this._writer.WriteLine($"public static {csType} {capName} => {resolvedValue};");
			this._writer.WriteLine();
		}
		else if (field.ConstantValue is not null)
		{
			var csType = GetCSharpType(field);
			this._writer.WriteDocComment(field.Description);

			// String constants (e.g. fuel = "Petrol") — expose as string
			if (field.ConstantValue.Length > 1 && field.EncodingPrimitive == SbePrimitive.Ascii)
			{
				this._writer.WriteLine($"public static string {capName} => \"{field.ConstantValue}\";");
			}
			else
			{
				this._writer.WriteLine($"public static {csType} {capName} => {field.ConstantValue};");
			}

			this._writer.WriteLine();
		}
	}

	/// <summary>
	/// Resolves a valueRef like "Model.C" to a cast expression like "(Model)67"
	/// to avoid naming collisions with instance fields.
	/// </summary>
	private string ResolveValueRef(string valueRef, string csType)
	{
		var parts = valueRef.Split('.');
		if (parts.Length != 2)
		{
			return $"({csType}){valueRef}";
		}

		var enumName = parts[0];
		var valueName = parts[1];

		foreach (var typeDef in this._schema.Types)
		{
			if (typeDef is SbeEnumDefinition enumDef && enumDef.Name == enumName)
			{
				var validValue = enumDef.ValidValues.FirstOrDefault(
					v => string.Equals(v.Name, valueName, StringComparison.Ordinal));
				if (validValue is not null)
				{
					return $"({csType}){validValue.Value}";
				}
			}
		}

		return $"({csType}){valueRef}";
	}

	private void EmitDeserializeBody(SbeMessage message, int[] offsets)
	{
		var hasGroups = message.Groups.Count > 0;
		var hasVarData = message.DataFields.Count > 0;

		// Composites are blittable value structs (not spans), so they ride the fast MemoryMarshal.Read
		// path; only fixed-length array fields (exposed as spans into the buffer) force the manual path.
		var hasSpanFields = message.Fields.Any(f => f.Presence != Presence.Constant && f.ArrayLength > 1);

		if (this.IsLittleEndian && !hasSpanFields)
		{
			this._writer.WriteLine($"{message.Name}FixedBlock block = MemoryMarshal.Read<{message.Name}FixedBlock>(body);");
		}

		if (hasGroups)
		{
			this._writer.WriteLine("int groupOffset = SbeBlockLength;");
		}

		// Emit group parsing
		for (var g = 0; g < message.Groups.Count; g++)
		{
			var group = message.Groups[g];
			var capName = EmitHelpers.CapitalizeFirstChar(group.Name);
			var isLast = g == message.Groups.Count - 1 && !hasVarData;

			if (IsSimpleGroup(group))
			{
				var varName = $"{group.Name}Group";
				this._writer.WriteLine($"var {varName} = Parse{capName}Group(body.Slice(groupOffset), out int {group.Name}Size);");

				if (!isLast)
				{
					this._writer.WriteLine($"groupOffset += {group.Name}Size;");
				}
			}
			else
			{
				// Complex group — use flyweight decoder to skip over it
				this._writer.WriteLine($"int {group.Name}Size = Skip{capName}Group(body.Slice(groupOffset));");
				if (!isLast)
				{
					this._writer.WriteLine($"groupOffset += {group.Name}Size;");
				}
			}
		}

		// Emit varData parsing
		if (hasVarData)
		{
			if (hasGroups)
			{
				this._writer.WriteLine("int varOffset = groupOffset;");
			}
			else
			{
				this._writer.WriteLine("int varOffset = SbeBlockLength;");
			}

			foreach (var data in message.DataFields)
			{
				var enc = this.ResolveVarDataEncoding(data);
				var capName = EmitHelpers.CapitalizeFirstChar(data.Name);
				var readLen = EmitHelpers.BuildReadExpression(enc.LengthCsType, this._schema.ByteOrder, "body.Slice(varOffset)");
				this._writer.WriteLine($"int {data.Name}Len = (int)({readLen});");
				this._writer.WriteLine($"varOffset += {enc.LengthSize};");
				this._writer.WriteLine($"ReadOnlySpan<byte> {data.Name}Data = body.Slice(varOffset, {data.Name}Len);");
				this._writer.WriteLine($"varOffset += {data.Name}Len;");
			}
		}

		// Build return statement
		if (this.IsLittleEndian && !hasSpanFields)
		{
			this._writer.WriteLine($"return new {message.Name}");
			this._writer.WriteLine('{');
			this._writer.Indentation++;

			foreach (var field in message.Fields)
			{
				if (field.Presence == Presence.Constant)
				{
					continue;
				}

				this._writer.WriteLine($"{EmitHelpers.CapitalizeFirstChar(field.Name)} = block.{EmitHelpers.CapitalizeFirstChar(field.Name)},");
			}

			foreach (var group in message.Groups)
			{
				if (IsSimpleGroup(group))
				{
					this._writer.WriteLine($"{EmitHelpers.CapitalizeFirstChar(group.Name)} = {group.Name}Group,");
				}
			}

			foreach (var data in message.DataFields)
			{
				this._writer.WriteLine($"{EmitHelpers.CapitalizeFirstChar(data.Name)} = {data.Name}Data,");
			}

			this._writer.Indentation--;
			this._writer.WriteLine("};");
		}
		else
		{
			// Manual field-by-field reads (BE or span fields present)
			if (!this.IsLittleEndian)
			{
				this.EmitBigEndianArrayLocals(message, offsets);
			}

			this._writer.WriteLine($"return new {message.Name}");
			this._writer.WriteLine('{');
			this._writer.Indentation++;

			for (var i = 0; i < message.Fields.Count; i++)
			{
				var field = message.Fields[i];

				if (field.Presence == Presence.Constant)
				{
					continue;
				}

				var capName = EmitHelpers.CapitalizeFirstChar(field.Name);

				if (field.IsComposite)
				{
					// LE composites are blittable; BE composites need per-member reads.
					var compositeExpr = this.IsLittleEndian
						? $"MemoryMarshal.Read<{field.CompositeDefinition!.Name}>(body.Slice({offsets[i]}))"
						: $"{field.CompositeDefinition!.Name}.DecodeBigEndian(body.Slice({offsets[i]}))";
					this._writer.WriteLine($"{capName} = {compositeExpr},");
				}
				else if (field.ArrayLength > 1 && !this.IsLittleEndian)
				{
					this._writer.WriteLine($"{capName} = {field.Name}Array,");
				}
				else if (field.ArrayLength > 1)
				{
					var csType = GetSbeType(field);
					var byteSize = FieldLayout.ComputeFieldWireSize(field);
					this._writer.WriteLine($"{capName} = MemoryMarshal.Cast<byte, {csType}>(body.Slice({offsets[i]}, {byteSize})),");
				}
				else
				{
					var csType = GetCSharpType(field);
					var underlying = GetSbeType(field);
					var readExpr = EmitHelpers.BuildReadExpression(underlying, this._schema.ByteOrder, $"body.Slice({offsets[i]})");

					if (field.IsEnumOrSet)
					{
						readExpr = $"({csType}){readExpr}";
					}

					this._writer.WriteLine($"{capName} = {readExpr},");
				}
			}

			foreach (var group in message.Groups)
			{
				if (IsSimpleGroup(group))
				{
					this._writer.WriteLine($"{EmitHelpers.CapitalizeFirstChar(group.Name)} = {group.Name}Group,");
				}
			}

			foreach (var data in message.DataFields)
			{
				this._writer.WriteLine($"{EmitHelpers.CapitalizeFirstChar(data.Name)} = {data.Name}Data,");
			}

			this._writer.Indentation--;
			this._writer.WriteLine("};");
		}
	}

	/// <summary>
	/// Emits one local heap array per fixed-length array field, decoded element by
	/// element with big-endian reads, for assignment in the return initializer.
	/// </summary>
	private void EmitBigEndianArrayLocals(SbeMessage message, int[] offsets)
	{
		for (var i = 0; i < message.Fields.Count; i++)
		{
			var field = message.Fields[i];

			if (field.Presence == Presence.Constant || field.ArrayLength <= 1)
			{
				continue;
			}

			var elemCsType = GetSbeType(field);
			var elemSize = SbePrimitiveMap.SizeOf(field.EncodingPrimitive);
			var sliceExpr = elemSize == 1
				? $"body.Slice({offsets[i]} + i)"
				: $"body.Slice({offsets[i]} + (i * {elemSize}))";

			this._writer.WriteLine($"{elemCsType}[] {field.Name}Array = new {elemCsType}[{field.ArrayLength}];");
			this._writer.WriteLine($"for (int i = 0; i < {field.ArrayLength}; i++)");
			this._writer.WriteLine('{');
			this._writer.Indentation++;
			this._writer.WriteLine($"{field.Name}Array[i] = {EmitHelpers.BuildReadExpression(elemCsType, ByteOrder.BigEndian, sliceExpr)};");
			this._writer.Indentation--;
			this._writer.WriteLine('}');
		}
	}

	private void EmitGroupParseHelper(string messageName, SbeGroup group, int[] groupOffsets)
	{
		var entryTypeName = EmitHelpers.GroupEntryTypeName(messageName, group.Name);
		var dim = this.ResolveGroupDimension(group);
		var rawBlockLengthExpr = EmitHelpers.BuildReadExpression(dim.BlockLengthCsType, this._schema.ByteOrder, "data");
		var blockLengthReadExpr = dim.BlockLengthCsType is "int" or "ushort" or "byte" or "sbyte" or "short"
			? rawBlockLengthExpr
			: $"(int){rawBlockLengthExpr}";
		var rawNumInGroupExpr = EmitHelpers.BuildReadExpression(dim.NumInGroupCsType, this._schema.ByteOrder, $"data.Slice({dim.BlockLengthSize})");
		var numInGroupReadExpr = dim.NumInGroupCsType is "int" or "ushort" or "byte" or "sbyte" or "short"
			? rawNumInGroupExpr
			: $"(int){rawNumInGroupExpr}";

		if (this.IsLittleEndian)
		{
			this._writer.WriteLine($$"""
				private static SbeGroupView<{{entryTypeName}}> Parse{{EmitHelpers.CapitalizeFirstChar(group.Name)}}Group(ReadOnlySpan<byte> data, out int bytesConsumed)
				{
					ArgumentOutOfRangeException.ThrowIfLessThan(data.Length, {{dim.HeaderSize}}, nameof(data));
					int entrySize = {{blockLengthReadExpr}};
					int count = {{numInGroupReadExpr}};
					bytesConsumed = {{dim.HeaderSize}} + (count * entrySize);
					if (count == 0) return default;
					return new SbeGroupView<{{entryTypeName}}>(ref MemoryMarshal.GetReference(data.Slice({{dim.HeaderSize}}, count * entrySize)), entrySize, count);
				}
				""");
			this._writer.WriteLine();
		}
		else
		{
			this._writer.WriteLine($$"""
				private static {{entryTypeName}}[] Parse{{EmitHelpers.CapitalizeFirstChar(group.Name)}}Group(ReadOnlySpan<byte> data, out int bytesConsumed)
				{
					ArgumentOutOfRangeException.ThrowIfLessThan(data.Length, {{dim.HeaderSize}}, nameof(data));
					int entrySize = {{blockLengthReadExpr}};
					int count = {{numInGroupReadExpr}};
					bytesConsumed = {{dim.HeaderSize}} + (count * entrySize);
					ArgumentOutOfRangeException.ThrowIfLessThan(data.Length, bytesConsumed, nameof(data));
					if (count == 0) return [];
					{{entryTypeName}}[] entries = new {{entryTypeName}}[count];
					for (int i = 0; i < count; i++)
					{
				""");
			this._writer.Indentation += 3;

			this._writer.WriteLine($"int entryBase = {dim.HeaderSize} + (i * entrySize);");
			this._writer.WriteLine($"entries[i] = new {entryTypeName}");
			this._writer.WriteLine('{');
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
				var readExpr = EmitHelpers.BuildReadExpression(underlying, this._schema.ByteOrder, $"data.Slice(entryBase + {groupOffsets[i]})");

				if (field.IsEnumOrSet)
				{
					readExpr = $"({csType}){readExpr}";
				}

				this._writer.WriteLine($"{EmitHelpers.CapitalizeFirstChar(field.Name)} = {readExpr},");
			}

			this._writer.Indentation--;
			this._writer.WriteLine("};");
			this._writer.Indentation -= 3;

			this._writer.WriteLine($$"""
						}
						return entries;
					}
				""");
			this._writer.WriteLine();
		}
	}
}
