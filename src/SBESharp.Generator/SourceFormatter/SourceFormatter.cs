using SBESharp.Roslyn;
using SBESharp.Roslyn.Ir;

namespace SBESharp.Generator;

/// <summary>
/// Entry point and generation orchestrator.
/// <para>
/// Walks an <see cref="SbeSchema"/> IR and emits a complete C# compilation unit in four phases:
/// <list type="number">
/// <item><description>Phase 1 — Enums and sets (<see cref="EmitEnums"/>, <see cref="EmitSets"/>)</description></item>
/// <item><description>Phase 2a/2b — FixedBlock and group entry structs (<see cref="EmitFixedBlock"/>, <see cref="EmitGroupEntry"/>)</description></item>
/// <item><description>Phase 3 — Decoder message struct, deserialization body, and group parse helpers (<see cref="EmitMessageStruct"/>)</description></item>
/// <item><description>Phase 4a/4b — Encoder and group encoder ref structs (<see cref="EmitEncoder"/>, <see cref="EmitGroupEncoder"/>)</description></item>
/// </list>
/// Pure-function helpers live in <see cref="EmitHelpers"/>.
/// </para>
/// </summary>
public sealed partial class SourceFormatter
{
	// Default groupSizeEncoding wire layout: blockLength (uint16) + numInGroup (uint8) = 3 bytes
	private const int DefaultGroupHeaderSize = 3;

	private readonly SbeSchema _schema;
	private readonly SourceWriter _writer;
	private readonly List<SbeComposite> _fieldComposites;
	private readonly HashSet<string> _compositeInlineArrays = new(StringComparer.Ordinal);

	private SourceFormatter(SbeSchema schema)
	{
		this._schema = schema;
		this._writer = new SourceWriter(4096);
		this._fieldComposites = this.CollectFieldComposites();
	}

	private bool IsLittleEndian => this._schema.ByteOrder != ByteOrder.BigEndian;

	/// <summary>Generate a complete C# compilation unit for the given schema.</summary>
	/// <param name="schema">The parsed SBE schema IR to emit code for.</param>
	/// <returns>The generated C# source text.</returns>
	public static string GenerateSourceFiles(SbeSchema schema)
	{
		return new SourceFormatter(schema).Generate();
	}

	private static string GetCSharpType(SbeField field)
		=> field.IsEnumOrSet ? field.Type : SbePrimitiveMap.ToCSharp(field.EncodingPrimitive);

	private static string GetSbeType(SbeField field)
		=> SbePrimitiveMap.ToCSharp(field.EncodingPrimitive);

	private static int ComputeBlockLength(SbeMessage message)
	{
		if (message.BlockLength > 0)
		{
			return message.BlockLength;
		}

		return message.Fields.Sum(f => FieldLayout.ComputeFieldWireSize(f));
	}

	private static int ComputeGroupBlockLength(SbeGroup group)
	{
		if (group.BlockLength.HasValue)
		{
			return group.BlockLength.Value;
		}

		return group.Fields.Sum(f => FieldLayout.ComputeFieldWireSize(f));
	}

	private static bool IsSimpleGroup(SbeGroup group) =>
		group.Groups.Count == 0 && group.DataFields.Count == 0;

	private static void ValidateBigEndianGroupFields(string messageName, IReadOnlyList<SbeGroup> groups)
	{
		foreach (var group in groups)
		{
			foreach (var field in group.Fields)
			{
				if (field.IsComposite)
				{
					throw new FormatException(
						$"Big-endian schemas do not support composite fields inside repeating groups yet " +
						$"(message '{messageName}', group '{group.Name}', field '{field.Name}'). " +
						"Declare the composite members as individual fields instead.");
				}

				if (field.ArrayLength > 1)
				{
					throw new FormatException(
						$"Big-endian schemas do not support fixed-length array fields inside repeating groups yet " +
						$"(message '{messageName}', group '{group.Name}', field '{field.Name}'). " +
						"Declare the elements as individual fields instead.");
				}
			}

			ValidateBigEndianGroupFields(messageName, group.Groups);
		}
	}

	private static string InlineArrayTypeName(SbeField field)
	{
		var csType = SbePrimitiveMap.ToCSharp(field.EncodingPrimitive);
		return $"InlineArray{EmitHelpers.CapitalizeFirstChar(csType)}{field.ArrayLength}";
	}

	/// <summary>
	/// Resolves the group dimension header size and numInGroup primitive type
	/// from the schema's composite type definitions.
	/// </summary>
	private GroupDimensionInfo ResolveGroupDimension(SbeGroup group)
	{
		foreach (var type in this._schema.Types)
		{
			if (type is SbeComposite composite && composite.Name == group.DimensionType
				&& composite.Fields.Count >= 2
				&& composite.Fields[0] is SbePrimitiveType blockLengthField
				&& composite.Fields[1] is SbePrimitiveType numInGroupField)
			{
				var headerSize = SbePrimitiveMap.SizeOf(blockLengthField.PrimitiveType)
					+ SbePrimitiveMap.SizeOf(numInGroupField.PrimitiveType);
				var blockLengthCsType = SbePrimitiveMap.ToCSharp(blockLengthField.PrimitiveType);
				var numInGroupCsType = SbePrimitiveMap.ToCSharp(numInGroupField.PrimitiveType);
				var blockLengthSize = SbePrimitiveMap.SizeOf(blockLengthField.PrimitiveType);
				return new GroupDimensionInfo(headerSize, blockLengthCsType, blockLengthSize, numInGroupCsType);
			}
		}

		// Default: uint16 blockLength + uint8 numInGroup
		return new GroupDimensionInfo(DefaultGroupHeaderSize, "ushort", 2, "byte");
	}

	/// <summary>Resolves the varData encoding for a data field, returning the C# type name and byte size of the length prefix.</summary>
	private VarDataEncodingInfo ResolveVarDataEncoding(SbeData data)
	{
		foreach (var type in this._schema.Types)
		{
			if (type is SbeComposite composite && composite.Name == data.Type
				&& composite.Fields.Count >= 1
				&& composite.Fields[0] is SbePrimitiveType lengthField)
			{
				var lengthCsType = SbePrimitiveMap.ToCSharp(lengthField.PrimitiveType);
				var lengthSize = SbePrimitiveMap.SizeOf(lengthField.PrimitiveType);
				return new VarDataEncodingInfo(lengthCsType, lengthSize);
			}
		}

		// Default: uint32 length prefix
		return new VarDataEncodingInfo("uint", 4);
	}

	private string Generate()
	{
		this.ValidateBigEndianGroupFields();

		var ns = string.IsNullOrWhiteSpace(this._schema.Package)
			? "SBESharp.Generated"
			: this._schema.Package;

		this._writer.WriteLine($$"""
			// <auto-generated/>
			// Generated by SBESharp.Generator — do not edit manually.
			#nullable enable

			using System;
			using System.Buffers.Binary;
			using System.Runtime.CompilerServices;
			using System.Runtime.InteropServices;
			using SBESharp;

			namespace {{ns}}
			{
			""");
		this._writer.Indentation++;

		// Phase 1: Enums and sets (including those embedded in composite fields)
		this.EmitEnums();
		this.EmitSets();

		// Phase 1b: Blittable value structs for composite-typed fields
		this.EmitCompositeStructs();

		foreach (var message in this._schema.Messages)
		{
			this.EmitMessage(message);
		}

		this._writer.Indentation--;
		this._writer.WriteLine('}');

		return this._writer.ToString();
	}

	/// <summary>
	/// Big-endian group entries are decoded field by field with scalar reads only;
	/// composite and fixed-length array fields inside groups are not supported yet.
	/// Fail at generation time with an actionable message instead of emitting
	/// byte-order-incorrect code.
	/// </summary>
	private void ValidateBigEndianGroupFields()
	{
		if (this.IsLittleEndian)
		{
			return;
		}

		foreach (var message in this._schema.Messages)
		{
			ValidateBigEndianGroupFields(message.Name, message.Groups);
		}
	}

	private void EmitMessage(SbeMessage message)
	{
		var offsets = FieldLayout.ComputeOffsets(message.Fields);
		var blockLength = ComputeBlockLength(message);

		// Emit inline array types for fixed-length array fields
		this.EmitInlineArrayTypes(message);

		// Phase 2a: FixedBlock — LE-only blittable struct for MemoryMarshal.Read
		if (this.IsLittleEndian)
		{
			this.EmitFixedBlock(message, offsets);
		}

		// Phase 2b: Group entry structs — one per simple repeating group
		foreach (var group in message.Groups)
		{
			if (IsSimpleGroup(group))
			{
				var groupOffsets = FieldLayout.ComputeOffsets(group.Fields);
				this.EmitGroupEntry(message.Name, group, groupOffsets);
			}
		}

		// Phase 3: Decoder — public message struct + Deserialize + group parse helpers
		this.EmitMessageStruct(message, offsets, blockLength);

		// Phase 4a: Encoder — fluent ref struct for writing
		this.EmitEncoder(message, offsets, blockLength);

		// Phase 4b: Group encoders — one per repeating group
		foreach (var group in message.Groups)
		{
			if (IsSimpleGroup(group))
			{
				var groupOffsets = FieldLayout.ComputeOffsets(group.Fields);
				this.EmitGroupEncoder(message.Name, group, groupOffsets);
			}
			else
			{
				this.EmitFlyweightEncoder(message.Name, group);
			}
		}
	}

	private void EmitInlineArrayTypes(SbeMessage message)
	{
		var emittedArrays = new HashSet<string>();
		this.EmitInlineArraysForFields(message.Fields, emittedArrays);

		foreach (var group in message.Groups)
		{
			this.EmitInlineArraysForFields(group.Fields, emittedArrays);
		}
	}

	private void EmitInlineArraysForFields(IReadOnlyList<SbeField> fields, HashSet<string> emitted)
	{
		foreach (var field in fields)
		{
			if (field.Presence == Presence.Constant)
			{
				continue;
			}

			// Composite fields are emitted as standalone blittable structs (EmitCompositeStructs),
			// not as opaque byte arrays.
			if (field.IsComposite)
			{
				continue;
			}

			if (field.ArrayLength > 1)
			{
				var arrayName = InlineArrayTypeName(field);
				if (this._compositeInlineArrays.Contains(arrayName))
				{
					continue;
				}

				if (emitted.Add(arrayName))
				{
					this._writer.WriteLine($"[InlineArray({field.ArrayLength})]");
					this._writer.WriteLine($"internal struct {arrayName}");
					this._writer.WriteLine('{');
					this._writer.Indentation++;
					this._writer.WriteLine($"private {GetSbeType(field)} _element;");
					this._writer.Indentation--;
					this._writer.WriteLine('}');
					this._writer.WriteLine();
				}
			}
		}
	}

	private readonly record struct GroupDimensionInfo(
		int HeaderSize,
		string BlockLengthCsType,
		int BlockLengthSize,
		string NumInGroupCsType);

	private readonly record struct VarDataEncodingInfo(
		string LengthCsType,
		int LengthSize);
}
