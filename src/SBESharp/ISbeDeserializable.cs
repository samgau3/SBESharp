namespace SBESharp;

/// <summary>Defines a type that can deserialize itself from a raw SBE wire buffer.</summary>
/// <typeparam name="T">The type that implements this interface.</typeparam>
public interface ISbeDeserializable<T>
	where T : ISbeDeserializable<T>, allows ref struct
{
	/// <summary>
	/// Deserializes an instance of <typeparamref name="T"/> from <paramref name="buffer"/>,
	/// automatically skipping the leading <see cref="SbeMessageHeader"/>.
	/// </summary>
	/// <param name="buffer">The raw SBE wire buffer beginning with an 8-byte message header.</param>
	/// <returns>The fully populated message instance.</returns>
	static abstract T Deserialize(ReadOnlySpan<byte> buffer);
}
