using System.Buffers;
using System.Runtime.CompilerServices;

namespace SBESharp;

/// <summary>
/// A forward-only cursor for encoding an SBE message. Owns the write position so callers
/// never compute byte offsets by hand, and targets either a caller-supplied fixed
/// <see cref="Memory{T}"/> (zero allocation) or a growable <see cref="IBufferWriter{T}"/>.
/// </summary>
/// <remarks>
/// SBE encoding is strictly forward-only — group dimension headers carry a constant
/// <c>blockLength</c> and a caller-supplied <c>numInGroup</c>, and variable-length data
/// carries a length prefix — so no value is ever back-patched. This lets a single advancing
/// cursor serve both backing modes.
/// <para>
/// Generated encoders reserve a region with <see cref="GetSpan(int)"/>, write into it, then
/// commit it with <see cref="Advance(int)"/>. Only one region is outstanding at a time, which
/// keeps the <see cref="IBufferWriter{T}"/> contract satisfied.
/// </para>
/// <para>
/// The fixed backing is <see cref="Memory{T}"/> rather than <see cref="Span{T}"/> so that the
/// spans handed to fluent encoders remain valid across cursor moves — this is what allows the
/// zero-allocation encoders to be built up entry by entry. Array and pooled buffers convert
/// implicitly; whole-message encoding into <c>stackalloc</c> memory is not supported here (use
/// the offset-based encoder overloads for that).
/// </para>
/// </remarks>
public struct SbeMessageWriter
{
	private readonly IBufferWriter<byte>? _output;
	private readonly Memory<byte> _memory;
	private int _committed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SbeMessageWriter"/> struct over a fixed
	/// caller-supplied buffer. No allocation occurs; the buffer must be large enough for the
	/// whole encoded message.
	/// </summary>
	/// <param name="buffer">The destination buffer to write the encoded message into.</param>
	public SbeMessageWriter(Memory<byte> buffer)
	{
		this._output = null;
		this._memory = buffer;
		this._committed = 0;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SbeMessageWriter"/> struct over a growable
	/// <see cref="IBufferWriter{T}"/>. The buffer grows on demand; the caller does not size it.
	/// </summary>
	/// <param name="output">The output buffer writer to encode into.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/> is <see langword="null"/>.</exception>
	public SbeMessageWriter(IBufferWriter<byte> output)
	{
		ArgumentNullException.ThrowIfNull(output);
		this._output = output;
		this._memory = default;
		this._committed = 0;
	}

	/// <summary>Gets the total number of bytes committed to the output so far.</summary>
	public readonly int BytesWritten => this._committed;

	/// <summary>
	/// Reserves a writable region of exactly <paramref name="sizeHint"/> bytes at the current
	/// position. The region must be committed with <see cref="Advance(int)"/> before the next call.
	/// </summary>
	/// <param name="sizeHint">The number of bytes to reserve.</param>
	/// <returns>A span of exactly <paramref name="sizeHint"/> bytes to write into.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="sizeHint"/> is negative, or when a fixed buffer has fewer than
	/// <paramref name="sizeHint"/> bytes remaining.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Span<byte> GetSpan(int sizeHint)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
		if (this._output is null)
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(sizeHint, this._memory.Length - this._committed, nameof(sizeHint));
			return this._memory.Span.Slice(this._committed, sizeHint);
		}

		return this._output.GetSpan(sizeHint).Slice(0, sizeHint);
	}

	/// <summary>
	/// Commits <paramref name="count"/> bytes previously reserved by <see cref="GetSpan(int)"/> and
	/// advances the cursor.
	/// </summary>
	/// <param name="count">The number of bytes to commit.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Advance(int count)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(count);
		this._output?.Advance(count);
		this._committed += count;
	}

	/// <summary>
	/// Writes the standard 8-byte <see cref="SbeMessageHeader"/> at the current position and
	/// advances past it.
	/// </summary>
	/// <param name="blockLength">The fixed-block length of the message body, excluding this header.</param>
	/// <param name="templateId">The message template identifier.</param>
	/// <param name="schemaId">The schema identifier that contains the template.</param>
	/// <param name="version">The schema version. Defaults to <c>0</c>.</param>
	public void WriteHeader(ushort blockLength, ushort templateId, ushort schemaId, ushort version = 0)
	{
		var span = this.GetSpan(SbeMessageHeader.EncodedLength);
		new SbeMessageHeader
		{
			BlockLength = blockLength,
			TemplateId = templateId,
			SchemaId = schemaId,
			Version = version,
		}.Write(span);
		this.Advance(SbeMessageHeader.EncodedLength);
	}
}
