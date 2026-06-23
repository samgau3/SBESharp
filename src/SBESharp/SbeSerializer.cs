using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("SBESharp.Tests")]

namespace SBESharp;

/// <summary>
/// Provides zero-allocation serialization and deserialization for SBE messages.
/// </summary>
/// <remarks>
/// Use <see cref="Deserialize{T}(ReadOnlySpan{byte})"/> for schema-generated message types
/// that implement <see cref="ISbeDeserializable{T}"/>. Use <see cref="Read{T}(ReadOnlySpan{byte})"/>
/// for raw blittable structs where no header skipping is needed.
/// </remarks>
public static class SbeSerializer
{
	/// <summary>
	/// Deserializes an SBE message of type <typeparamref name="T"/> from <paramref name="buffer"/>,
	/// automatically skipping the leading 8-byte message header.
	/// </summary>
	/// <typeparam name="T">A type implementing <see cref="ISbeDeserializable{T}"/>.</typeparam>
	/// <param name="buffer">The raw SBE wire buffer beginning with an 8-byte message header.</param>
	/// <returns>The fully populated message instance.</returns>
	public static T Deserialize<T>(ReadOnlySpan<byte> buffer)
		where T : ISbeDeserializable<T>, allows ref struct
		=> T.Deserialize(buffer);

	/// <summary>
	/// Reads a value of type <typeparamref name="T"/> from the beginning of <paramref name="buffer"/>.
	/// </summary>
	/// <typeparam name="T">An unmanaged struct with a blittable binary layout.</typeparam>
	/// <param name="buffer">The source buffer containing the encoded bytes.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="buffer"/> contains fewer bytes than <c>sizeof(T)</c>.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Read<T>(ReadOnlySpan<byte> buffer)
		where T : unmanaged
	{
		int size = Unsafe.SizeOf<T>();
		ArgumentOutOfRangeException.ThrowIfGreaterThan(size, buffer.Length, nameof(buffer));
		return MemoryMarshal.Read<T>(buffer);
	}

	/// <summary>
	/// Reads a value of type <typeparamref name="T"/> from <paramref name="buffer"/>.
	/// </summary>
	/// <typeparam name="T">An unmanaged struct with a blittable binary layout.</typeparam>
	/// <param name="buffer">The source byte array.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="buffer"/> contains fewer bytes than <c>sizeof(T)</c>.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Read<T>(byte[] buffer)
		where T : unmanaged
	{
		return Read<T>(new ReadOnlySpan<byte>(buffer));
	}

	/// <summary>
	/// Reads a value of type <typeparamref name="T"/> from <paramref name="buffer"/>.
	/// </summary>
	/// <typeparam name="T">An unmanaged struct with a blittable binary layout.</typeparam>
	/// <param name="buffer">The source memory region.</param>
	/// <returns>The deserialized value.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="buffer"/> contains fewer bytes than <c>sizeof(T)</c>.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T Read<T>(ReadOnlyMemory<byte> buffer)
		where T : unmanaged
	{
		return Read<T>(buffer.Span);
	}

	/// <summary>
	/// Serializes <paramref name="value"/> into <paramref name="destination"/>.
	/// This is the zero-allocation hot-path overload.
	/// </summary>
	/// <typeparam name="T">An unmanaged struct with a blittable binary layout.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <param name="destination">The destination span to write into.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="destination"/> is smaller than <c>sizeof(T)</c>.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Serialize<T>(in T value, Span<byte> destination)
		where T : unmanaged
	{
		int size = Unsafe.SizeOf<T>();
		ArgumentOutOfRangeException.ThrowIfGreaterThan(size, destination.Length, nameof(destination));
		MemoryMarshal.Write(destination, in value);
	}

	/// <summary>
	/// Serializes <paramref name="value"/> into a newly allocated byte array.
	/// </summary>
	/// <remarks>
	/// This overload allocates a new byte array on every call. For zero-allocation
	/// encoding on the hot path, use <see cref="Serialize{T}(in T, Span{byte})"/>
	/// with a caller-supplied buffer instead.
	/// </remarks>
	/// <typeparam name="T">An unmanaged struct with a blittable binary layout.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <returns>A newly allocated byte array containing the encoded bytes.</returns>
	public static byte[] Serialize<T>(in T value)
		where T : unmanaged
	{
		byte[] buffer = new byte[Unsafe.SizeOf<T>()];
		MemoryMarshal.Write(buffer, in value);
		return buffer;
	}

	/// <summary>
	/// Returns the number of bytes required to encode a value of type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">An unmanaged struct type.</typeparam>
	/// <returns>The byte size of <typeparamref name="T"/>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int SizeOf<T>()
		where T : unmanaged
	{
		return Unsafe.SizeOf<T>();
	}
}
