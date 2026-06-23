namespace SBESharp.Roslyn.Ir;

/// <summary>Represents a fixed-length field within an SBE message or group.</summary>
/// <param name="Name">The name of the field.</param>
/// <param name="Id">The schema-assigned field identifier.</param>
/// <param name="Type">The name of the encoding type used for this field.</param>
/// <param name="EncodingPrimitive">The resolved underlying wire primitive (e.g. <see cref="SbePrimitive.U8"/> for an enum with uint8 encoding).</param>
/// <param name="IsEnumOrSet">Whether the field's declared C# type should use the type name (true for enums/sets) rather than a primitive keyword.</param>
/// <param name="Offset">The explicit byte offset within the block, or null if auto-calculated.</param>
/// <param name="Presence">The presence semantics of the field (required, optional, or constant).</param>
/// <param name="Description">An optional human-readable description of the field.</param>
/// <param name="ValueRef">The constant value reference for constant enum fields (e.g. "Model.C"), or null.</param>
/// <param name="ConstantValue">The literal constant value for constant primitive fields, or null.</param>
/// <param name="ArrayLength">The number of elements for fixed-length array fields; 1 for scalar fields.</param>
/// <param name="IsComposite">Whether the field's type is a composite (opaque byte region).</param>
/// <param name="WireSize">The total wire size in bytes when overriding the default primitive size (e.g. composites).</param>
/// <param name="CompositeDefinition">The composite type definition when the field is a composite, or null.</param>
public sealed record SbeField(
	string Name,
	ushort Id,
	string Type,
	SbePrimitive EncodingPrimitive,
	bool IsEnumOrSet,
	int? Offset,
	Presence Presence,
	string? Description,
	string? ValueRef = null,
	string? ConstantValue = null,
	uint ArrayLength = 1,
	bool IsComposite = false,
	int WireSize = 0,
	SbeComposite? CompositeDefinition = null);
