using SBESharp.Roslyn.Ir;

namespace SBESharp.Generator;

/// <summary>Phase 1b: Blittable value struct emission for composite-typed fields.</summary>
public sealed partial class SourceFormatter
{
	private enum CompositeMemberKind
	{
		Scalar,
		Array,
		Named,
		NestedComposite,
		Constant,
	}

	private static string CompositeInlineArrayName(CompositeMember member)
		=> $"InlineArray{EmitHelpers.CapitalizeFirstChar(SbePrimitiveMap.ToCSharp(member.Primitive))}{member.ArrayLength}";

	private static CompositeMember ResolvePrimitiveMember(string name, SbePrimitiveType primitive, string? description)
	{
		var csType = SbePrimitiveMap.ToCSharp(primitive.PrimitiveType);

		if (primitive.Presence == Presence.Constant)
		{
			var isAsciiString = primitive.ConstantValue is { Length: > 1 } && primitive.PrimitiveType == SbePrimitive.Ascii;
			return new CompositeMember(name, 0, 0, CompositeMemberKind.Constant, csType, primitive.PrimitiveType, 1, isAsciiString, primitive.ConstantValue, description);
		}

		if (primitive.Length > 1)
		{
			var size = SbePrimitiveMap.SizeOf(primitive.PrimitiveType) * (int)primitive.Length;
			return new CompositeMember(name, 0, size, CompositeMemberKind.Array, csType, primitive.PrimitiveType, primitive.Length, false, null, description);
		}

		return new CompositeMember(name, 0, SbePrimitiveMap.SizeOf(primitive.PrimitiveType), CompositeMemberKind.Scalar, csType, primitive.PrimitiveType, 1, false, null, description);
	}

	private static void CollectComposites(
		IReadOnlyList<SbeField> fields,
		IReadOnlyList<SbeGroup> groups,
		Action<SbeComposite> add)
	{
		foreach (var field in fields)
		{
			if (field.IsComposite && field.CompositeDefinition is { } definition)
			{
				add(definition);
			}
		}

		foreach (var group in groups)
		{
			CollectComposites(group.Fields, group.Groups, add);
		}
	}

	/// <summary>
	/// Emits a blittable value struct for every composite used as a message or group field,
	/// plus every composite reachable from one via <c>&lt;ref&gt;</c>. Dependencies are emitted
	/// before dependents so nested composites (e.g. Booster within Engine) compile cleanly.
	/// </summary>
	private void EmitCompositeStructs()
	{
		foreach (var composite in this._fieldComposites)
		{
			this.EmitCompositeInlineArrays(composite);
			this.EmitCompositeStruct(composite);
		}
	}

	private void EmitCompositeInlineArrays(SbeComposite composite)
	{
		foreach (var member in this.ResolveCompositeMembers(composite))
		{
			if (member.Kind != CompositeMemberKind.Array)
			{
				continue;
			}

			var arrayName = CompositeInlineArrayName(member);
			if (!this._compositeInlineArrays.Add(arrayName))
			{
				continue;
			}

			// Public (not internal) because it is the type of a public field on a public struct.
			this._writer.WriteLine($"[InlineArray({member.ArrayLength})]");
			this._writer.WriteLine($"public struct {arrayName}");
			this._writer.WriteLine('{');
			this._writer.Indentation++;
			this._writer.WriteLine($"private {SbePrimitiveMap.ToCSharp(member.Primitive)} _element;");
			this._writer.Indentation--;
			this._writer.WriteLine('}');
			this._writer.WriteLine();
		}
	}

	private void EmitCompositeStruct(SbeComposite composite)
	{
		var members = this.ResolveCompositeMembers(composite);

		this._writer.WriteDocComment(composite.Description ?? $"SBE composite type {composite.Name}.");
		this._writer.WriteLine("[StructLayout(LayoutKind.Explicit, Pack = 1)]");
		this._writer.WriteLine($"public struct {composite.Name}");
		this._writer.WriteLine('{');
		this._writer.Indentation++;

		// Constant members occupy zero wire bytes — expose as static properties.
		foreach (var member in members)
		{
			if (member.Kind == CompositeMemberKind.Constant)
			{
				this.EmitCompositeConstant(member);
			}
		}

		// Encoded members as explicitly-positioned instance fields.
		foreach (var member in members)
		{
			if (member.Kind == CompositeMemberKind.Constant)
			{
				continue;
			}

			this._writer.WriteDocComment(member.Description);
			this._writer.WriteLine($"[FieldOffset({member.Offset})]");
			var typeName = member.Kind == CompositeMemberKind.Array
				? CompositeInlineArrayName(member)
				: member.CsType;
			this._writer.WriteLine($"public {typeName} {EmitHelpers.CapitalizeFirstChar(member.Name)};");
		}

		// Big-endian schemas cannot use MemoryMarshal on the explicit-layout struct,
		// so emit per-member wire codecs alongside the fields.
		if (!this.IsLittleEndian)
		{
			this.EmitCompositeBigEndianCodecs(composite.Name, members);
		}

		this._writer.Indentation--;
		this._writer.WriteLine('}');
		this._writer.WriteLine();
	}

	private void EmitCompositeBigEndianCodecs(string compositeName, List<CompositeMember> members)
	{
		this._writer.WriteLine();
		this._writer.WriteLine($"/// <summary>Decodes a <c>{compositeName}</c> from big-endian wire bytes.</summary>");
		this._writer.WriteLine($"public static {compositeName} DecodeBigEndian(ReadOnlySpan<byte> source)");
		this._writer.WriteLine('{');
		this._writer.Indentation++;
		this._writer.WriteLine($"var value = default({compositeName});");

		foreach (var member in members)
		{
			if (member.Kind == CompositeMemberKind.Constant)
			{
				continue;
			}

			var capName = EmitHelpers.CapitalizeFirstChar(member.Name);

			switch (member.Kind)
			{
				case CompositeMemberKind.NestedComposite:
					this._writer.WriteLine($"value.{capName} = {member.CsType}.DecodeBigEndian(source.Slice({member.Offset}));");
					break;
				case CompositeMemberKind.Array:
					this.EmitCompositeArrayDecodeLoop(member, capName);
					break;
				case CompositeMemberKind.Named:
					var namedUnderlying = SbePrimitiveMap.ToCSharp(member.Primitive);
					var namedRead = EmitHelpers.BuildReadExpression(namedUnderlying, ByteOrder.BigEndian, $"source.Slice({member.Offset})");
					this._writer.WriteLine($"value.{capName} = ({member.CsType}){namedRead};");
					break;
				default:
					var scalarRead = EmitHelpers.BuildReadExpression(member.CsType, ByteOrder.BigEndian, $"source.Slice({member.Offset})");
					this._writer.WriteLine($"value.{capName} = {scalarRead};");
					break;
			}
		}

		this._writer.WriteLine("return value;");
		this._writer.Indentation--;
		this._writer.WriteLine('}');

		this._writer.WriteLine();
		this._writer.WriteLine($"/// <summary>Encodes this <c>{compositeName}</c> to big-endian wire bytes.</summary>");
		this._writer.WriteLine("public readonly void EncodeBigEndian(Span<byte> destination)");
		this._writer.WriteLine('{');
		this._writer.Indentation++;

		foreach (var member in members)
		{
			if (member.Kind == CompositeMemberKind.Constant)
			{
				continue;
			}

			var capName = EmitHelpers.CapitalizeFirstChar(member.Name);

			switch (member.Kind)
			{
				case CompositeMemberKind.NestedComposite:
					this._writer.WriteLine($"{capName}.EncodeBigEndian(destination.Slice({member.Offset}));");
					break;
				case CompositeMemberKind.Array:
					this.EmitCompositeArrayEncodeLoop(member, capName);
					break;
				case CompositeMemberKind.Named:
					var namedUnderlying = SbePrimitiveMap.ToCSharp(member.Primitive);
					var namedWrite = EmitHelpers.BuildWriteExpression(namedUnderlying, ByteOrder.BigEndian, $"destination.Slice({member.Offset})", $"({namedUnderlying}){capName}");
					this._writer.WriteLine($"{namedWrite};");
					break;
				default:
					var scalarWrite = EmitHelpers.BuildWriteExpression(member.CsType, ByteOrder.BigEndian, $"destination.Slice({member.Offset})", capName);
					this._writer.WriteLine($"{scalarWrite};");
					break;
			}
		}

		this._writer.Indentation--;
		this._writer.WriteLine('}');
	}

	private void EmitCompositeArrayDecodeLoop(CompositeMember member, string capName)
	{
		var elemCsType = SbePrimitiveMap.ToCSharp(member.Primitive);
		var elemSize = SbePrimitiveMap.SizeOf(member.Primitive);
		var sliceExpr = elemSize == 1
			? $"source.Slice({member.Offset} + i)"
			: $"source.Slice({member.Offset} + (i * {elemSize}))";

		this._writer.WriteLine($"for (int i = 0; i < {member.ArrayLength}; i++)");
		this._writer.WriteLine('{');
		this._writer.Indentation++;
		this._writer.WriteLine($"value.{capName}[i] = {EmitHelpers.BuildReadExpression(elemCsType, ByteOrder.BigEndian, sliceExpr)};");
		this._writer.Indentation--;
		this._writer.WriteLine('}');
	}

	private void EmitCompositeArrayEncodeLoop(CompositeMember member, string capName)
	{
		var elemCsType = SbePrimitiveMap.ToCSharp(member.Primitive);
		var elemSize = SbePrimitiveMap.SizeOf(member.Primitive);
		var sliceExpr = elemSize == 1
			? $"destination.Slice({member.Offset} + i)"
			: $"destination.Slice({member.Offset} + (i * {elemSize}))";

		this._writer.WriteLine($"for (int i = 0; i < {member.ArrayLength}; i++)");
		this._writer.WriteLine('{');
		this._writer.Indentation++;
		this._writer.WriteLine($"{EmitHelpers.BuildWriteExpression(elemCsType, ByteOrder.BigEndian, sliceExpr, $"{capName}[i]")};");
		this._writer.Indentation--;
		this._writer.WriteLine('}');
	}

	private void EmitCompositeConstant(CompositeMember member)
	{
		this._writer.WriteDocComment(member.Description);

		if (member.IsAsciiStringConstant)
		{
			this._writer.WriteLine($"public static string {EmitHelpers.CapitalizeFirstChar(member.Name)} => \"{member.ConstantValue}\";");
		}
		else
		{
			this._writer.WriteLine($"public static {member.CsType} {EmitHelpers.CapitalizeFirstChar(member.Name)} => {member.ConstantValue};");
		}

		this._writer.WriteLine();
	}

	/// <summary>
	/// Collects every composite used as a field (transitively following <c>&lt;ref&gt;</c> to other
	/// composites), ordered dependencies-first and de-duplicated by name.
	/// </summary>
	private List<SbeComposite> CollectFieldComposites()
	{
		var ordered = new List<SbeComposite>();
		var seen = new HashSet<string>(StringComparer.Ordinal);

		void Add(SbeComposite composite)
		{
			if (!seen.Add(composite.Name))
			{
				return;
			}

			foreach (var element in composite.Fields)
			{
				switch (element)
				{
					case SbeComposite nested:
						Add(nested);
						break;
					case SbeRefType refType when this.FindType(refType.ReferencedType) is SbeComposite referenced:
						Add(referenced);
						break;
					default:
						break;
				}
			}

			ordered.Add(composite);
		}

		foreach (var message in this._schema.Messages)
		{
			CollectComposites(message.Fields, message.Groups, Add);
		}

		return ordered;
	}

	private List<CompositeMember> ResolveCompositeMembers(SbeComposite composite)
	{
		var members = new List<CompositeMember>(composite.Fields.Count);
		var offset = 0;

		foreach (var element in composite.Fields)
		{
			var member = this.ResolveCompositeMember(element) with { Offset = offset };
			members.Add(member);
			offset += member.Size;
		}

		return members;
	}

	private CompositeMember ResolveCompositeMember(SbeTypeDefinition element) => element switch
	{
		SbePrimitiveType primitive => ResolvePrimitiveMember(primitive.Name, primitive, element.Description),
		SbeEnumDefinition e => new CompositeMember(e.Name, 0, SbePrimitiveMap.SizeOf(e.EncodingType), CompositeMemberKind.Named, e.Name, e.EncodingType, 1, false, null, e.Description),
		SbeSet s => new CompositeMember(s.Name, 0, SbePrimitiveMap.SizeOf(s.EncodingType), CompositeMemberKind.Named, s.Name, s.EncodingType, 1, false, null, s.Description),
		SbeComposite nested => new CompositeMember(nested.Name, 0, this.ComputeCompositeSize(nested), CompositeMemberKind.NestedComposite, nested.Name, SbePrimitive.U8, 1, false, null, nested.Description),
		SbeRefType refType => this.ResolveRefMember(refType),
		_ => new CompositeMember(element.Name, 0, 0, CompositeMemberKind.Scalar, "byte", SbePrimitive.U8, 1, false, null, element.Description),
	};

	private CompositeMember ResolveRefMember(SbeRefType refType)
	{
		var referenced = this.FindType(refType.ReferencedType);
		return referenced switch
		{
			SbePrimitiveType primitive => ResolvePrimitiveMember(refType.Name, primitive, refType.Description ?? primitive.Description),
			SbeEnumDefinition e => new CompositeMember(refType.Name, 0, SbePrimitiveMap.SizeOf(e.EncodingType), CompositeMemberKind.Named, e.Name, e.EncodingType, 1, false, null, refType.Description),
			SbeSet s => new CompositeMember(refType.Name, 0, SbePrimitiveMap.SizeOf(s.EncodingType), CompositeMemberKind.Named, s.Name, s.EncodingType, 1, false, null, refType.Description),
			SbeComposite composite => new CompositeMember(refType.Name, 0, this.ComputeCompositeSize(composite), CompositeMemberKind.NestedComposite, composite.Name, SbePrimitive.U8, 1, false, null, refType.Description),
			_ => new CompositeMember(refType.Name, 0, 0, CompositeMemberKind.Scalar, "byte", SbePrimitive.U8, 1, false, null, refType.Description),
		};
	}

	private int ComputeCompositeSize(SbeComposite composite)
		=> this.ResolveCompositeMembers(composite).Sum(m => m.Size);

	private SbeTypeDefinition? FindType(string name)
		=> this._schema.Types.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));

	private readonly record struct CompositeMember(
		string Name,
		int Offset,
		int Size,
		CompositeMemberKind Kind,
		string CsType,
		SbePrimitive Primitive,
		uint ArrayLength,
		bool IsAsciiStringConstant,
		string? ConstantValue,
		string? Description);
}
