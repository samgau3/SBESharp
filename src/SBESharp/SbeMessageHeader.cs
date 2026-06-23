using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SBESharp;

/// <summary>
/// The standard SBE message framing header that precedes every message body.
/// Encoded as 8 contiguous bytes in little-endian byte order.
/// </summary>
/// <remarks>
/// Wire layout:
/// <code>
/// Offset  Length  Field
/// ------  ------  -----------
///      0       2  BlockLength
///      2       2  TemplateId
///      4       2  SchemaId
///      6       2  Version
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Explicit, Pack = 1)]
public struct SbeMessageHeader
{
	/// <summary>Total encoded size of this header in bytes.</summary>
	public const int EncodedLength = 8;

	[FieldOffset(0)]
	private ushort _blockLength;

	[FieldOffset(2)]
	private ushort _templateId;

	[FieldOffset(4)]
	private ushort _schemaId;

	[FieldOffset(6)]
	private ushort _version;

	/// <summary>
	/// Gets or sets the length of the fixed-length message body in bytes, not including this header.
	/// </summary>
	public ushort BlockLength { get => this._blockLength; set => this._blockLength = value; }

	/// <summary>Gets or sets the SBE message template identifier.</summary>
	public ushort TemplateId { get => this._templateId; set => this._templateId = value; }

	/// <summary>Gets or sets the SBE schema identifier that contains this template.</summary>
	public ushort SchemaId { get => this._schemaId; set => this._schemaId = value; }

	/// <summary>Gets or sets the version of the SBE schema.</summary>
	public ushort Version { get => this._version; set => this._version = value; }

	/// <summary>
	/// Reads an <see cref="SbeMessageHeader"/> from the first <see cref="EncodedLength"/>
	/// bytes of <paramref name="buffer"/> using little-endian decoding.
	/// </summary>
	/// <param name="buffer">The source buffer. Must be at least <see cref="EncodedLength"/> bytes.</param>
	/// <returns>The decoded header.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="buffer"/> is shorter than <see cref="EncodedLength"/>.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SbeMessageHeader Read(ReadOnlySpan<byte> buffer)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(buffer.Length, EncodedLength, nameof(buffer));
		return SbeSerializer.Read<SbeMessageHeader>(buffer);
	}

	/// <summary>
	/// Writes this header into the first <see cref="EncodedLength"/> bytes of
	/// <paramref name="destination"/> using little-endian encoding.
	/// </summary>
	/// <param name="destination">The target buffer. Must be at least <see cref="EncodedLength"/> bytes.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> is shorter than <see cref="EncodedLength"/>.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Write(Span<byte> destination)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, EncodedLength, nameof(destination));
		SbeSerializer.Serialize(in this, destination);
	}
}
