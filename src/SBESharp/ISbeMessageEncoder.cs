namespace SBESharp;

/// <summary>
/// Defines a generated message encoder that begins encoding into an <see cref="SbeMessageWriter"/>,
/// writing the framing header and returning a fluent encoder positioned at the fixed block.
/// </summary>
/// <typeparam name="TSelf">The concrete encoder type that implements this interface.</typeparam>
public interface ISbeMessageEncoder<TSelf>
	where TSelf : ISbeMessageEncoder<TSelf>, allows ref struct
{
	/// <summary>
	/// Writes the 8-byte <see cref="SbeMessageHeader"/> into <paramref name="writer"/> and returns
	/// an encoder for the message body.
	/// </summary>
	/// <param name="writer">The message writer to encode into.</param>
	/// <returns>A fluent encoder positioned at the start of the fixed block.</returns>
	static abstract TSelf Encode(ref SbeMessageWriter writer);
}
