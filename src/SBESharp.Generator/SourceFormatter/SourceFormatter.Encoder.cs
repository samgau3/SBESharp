using SBESharp.Roslyn;
using SBESharp.Roslyn.Ir;

namespace SBESharp.Generator;

/// <summary>Phase 4a: Message encoder ref struct emission.</summary>
public sealed partial class SourceFormatter
{
	private void EmitEncoder(SbeMessage message, int[] offsets, int blockLength)
	{
		var encoderName = $"{message.Name}Encoder";

		this._writer.WriteLine($$"""
			/// <summary>Zero-allocation encoder for the {{message.Name}} message.</summary>
			public ref struct {{encoderName}}
			{
				private readonly Span<byte> _buffer;
				private readonly int _offset;

				/// <summary>Creates an encoder over <paramref name="buffer"/> at <paramref name="offset"/>.</summary>
				public static {{encoderName}} Encode(Span<byte> buffer, int offset = 0)
				{
					return new {{encoderName}}(buffer, offset);
				}

				private {{encoderName}}(Span<byte> buffer, int offset)
				{
					_buffer = buffer;
					_offset = offset;
				}
			""");
		this._writer.Indentation++;

		for (var i = 0; i < message.Fields.Count; i++)
		{
			var field = message.Fields[i];

			if (field.Presence == Presence.Constant)
			{
				continue;
			}

			if (field.IsComposite)
			{
				// Composite field: LE writes the blittable value struct in one shot;
				// BE writes per-member with big-endian byte order.
				var compositeWrite = this.IsLittleEndian
					? $"MemoryMarshal.Write(_buffer.Slice(_offset + {offsets[i]}), in value);"
					: $"value.EncodeBigEndian(_buffer.Slice(_offset + {offsets[i]}));";
				this._writer.WriteLine($$"""

					/// <summary>Sets the <c>{{field.Name}}</c> composite field.</summary>
					public {{encoderName}} Set{{EmitHelpers.CapitalizeFirstChar(field.Name)}}(in {{field.CompositeDefinition!.Name}} value)
					{
						{{compositeWrite}}
						return this;
					}
					""");
			}
			else if (field.ArrayLength > 1)
			{
				var csType = GetSbeType(field);
				var byteSize = FieldLayout.ComputeFieldWireSize(field);

				if (field.EncodingPrimitive == SbePrimitive.Ascii)
				{
					// Char array: accept ReadOnlySpan<byte>
					this._writer.WriteLine($$"""

						/// <summary>Sets the <c>{{field.Name}}</c> field.</summary>
						public {{encoderName}} Set{{EmitHelpers.CapitalizeFirstChar(field.Name)}}(ReadOnlySpan<byte> value)
						{
							Span<byte> dest = _buffer.Slice(_offset + {{offsets[i]}}, {{byteSize}});
							dest.Clear();
							value.Slice(0, Math.Min(value.Length, {{byteSize}})).CopyTo(dest);
							return this;
						}
						""");
				}
				else if (this.IsLittleEndian || SbePrimitiveMap.SizeOf(field.EncodingPrimitive) == 1)
				{
					// Numeric array: accept ReadOnlySpan<T>; a straight byte copy
					// matches the wire format on LE schemas and for 1-byte elements.
					this._writer.WriteLine($$"""

						/// <summary>Sets the <c>{{field.Name}}</c> field.</summary>
						public {{encoderName}} Set{{EmitHelpers.CapitalizeFirstChar(field.Name)}}(ReadOnlySpan<{{csType}}> value)
						{
							Span<byte> dest = _buffer.Slice(_offset + {{offsets[i]}}, {{byteSize}});
							dest.Clear();
							MemoryMarshal.AsBytes(value.Slice(0, Math.Min(value.Length, {{field.ArrayLength}}))).CopyTo(dest);
							return this;
						}
						""");
				}
				else
				{
					// Numeric array on a BE schema: write each element with big-endian byte order.
					var elemSize = SbePrimitiveMap.SizeOf(field.EncodingPrimitive);
					var elemWrite = EmitHelpers.BuildWriteExpression(csType, ByteOrder.BigEndian, $"dest.Slice(i * {elemSize})", "value[i]");
					this._writer.WriteLine($$"""

						/// <summary>Sets the <c>{{field.Name}}</c> field.</summary>
						public {{encoderName}} Set{{EmitHelpers.CapitalizeFirstChar(field.Name)}}(ReadOnlySpan<{{csType}}> value)
						{
							Span<byte> dest = _buffer.Slice(_offset + {{offsets[i]}}, {{byteSize}});
							dest.Clear();
							int count = Math.Min(value.Length, {{field.ArrayLength}});
							for (int i = 0; i < count; i++)
							{
								{{elemWrite}};
							}
							return this;
						}
						""");
				}
			}
			else
			{
				var csType = GetCSharpType(field);
				var underlying = GetSbeType(field);
				var valueExpr = field.IsEnumOrSet ? $"({underlying})value" : "value";
				var writeExpr = EmitHelpers.BuildWriteExpression(underlying, this._schema.ByteOrder, $"_buffer.Slice(_offset + {offsets[i]})", valueExpr);

				this._writer.WriteLine($$"""

					/// <summary>Sets the <c>{{field.Name}}</c> field.</summary>
					public {{encoderName}} Set{{EmitHelpers.CapitalizeFirstChar(field.Name)}}({{csType}} value)
					{
						{{writeExpr}};
						return this;
					}
					""");
			}
		}

		// VarData write methods
		if (message.DataFields.Count > 0)
		{
			this._writer.WriteLine();
			this._writer.WriteLine($"/// <summary>Gets the byte offset immediately after the fixed block (for positioning groups/varData).</summary>");
			this._writer.WriteLine($"public int BlockEnd => _offset + {ComputeBlockLength(message)};");

			foreach (var data in message.DataFields)
			{
				var enc = this.ResolveVarDataEncoding(data);
				var capName = EmitHelpers.CapitalizeFirstChar(data.Name);
				var writeLenExpr = EmitHelpers.BuildWriteExpression(enc.LengthCsType, this._schema.ByteOrder, "buffer.Slice(offset)", $"({enc.LengthCsType})value.Length");

				this._writer.WriteLine($$"""

					/// <summary>Writes the <c>{{data.Name}}</c> variable-length data field at the given offset and returns the number of bytes written.</summary>
					public static int Write{{capName}}(Span<byte> buffer, int offset, ReadOnlySpan<byte> value)
					{
						{{writeLenExpr}};
						value.CopyTo(buffer.Slice(offset + {{enc.LengthSize}}));
						return {{enc.LengthSize}} + value.Length;
					}
					""");
			}
		}

		this._writer.Indentation--;
		this._writer.WriteLine('}');
		this._writer.WriteLine();
	}
}
