using SBESharp.Roslyn.Ir;

namespace SBESharp.Generator;

/// <summary>Shared pure-function helpers used across all emission phases.</summary>
public static class EmitHelpers
{
	/// <summary>Builds a BinaryPrimitives read expression for the given C# type and byte order.</summary>
	/// <param name="csType">The C# primitive type name (e.g. "int", "ushort").</param>
	/// <param name="order">The byte order of the schema.</param>
	/// <param name="spanExpr">The span expression to read from.</param>
	/// <returns>A C# expression string that reads a value from the span.</returns>
	public static string BuildReadExpression(string csType, ByteOrder order, string spanExpr)
	{
		var endian = order == ByteOrder.BigEndian ? "BigEndian" : "LittleEndian";

		return csType switch
		{
			"byte" => $"{spanExpr}[0]",
			"sbyte" => $"(sbyte){spanExpr}[0]",
			"ushort" => $"BinaryPrimitives.ReadUInt16{endian}({spanExpr})",
			"short" => $"BinaryPrimitives.ReadInt16{endian}({spanExpr})",
			"uint" => $"BinaryPrimitives.ReadUInt32{endian}({spanExpr})",
			"int" => $"BinaryPrimitives.ReadInt32{endian}({spanExpr})",
			"ulong" => $"BinaryPrimitives.ReadUInt64{endian}({spanExpr})",
			"long" => $"BinaryPrimitives.ReadInt64{endian}({spanExpr})",
			"float" => $"BinaryPrimitives.ReadSingle{endian}({spanExpr})",
			"double" => $"BinaryPrimitives.ReadDouble{endian}({spanExpr})",
			_ => $"{spanExpr}[0]",
		};
	}

	/// <summary>Builds a BinaryPrimitives write expression for the given C# type and byte order.</summary>
	/// <param name="csType">The C# primitive type name (e.g. "int", "ushort").</param>
	/// <param name="order">The byte order of the schema.</param>
	/// <param name="spanExpr">The span expression to write to.</param>
	/// <param name="valueExpr">The value expression to write.</param>
	/// <returns>A C# expression string that writes a value to the span.</returns>
	public static string BuildWriteExpression(string csType, ByteOrder order, string spanExpr, string valueExpr)
	{
		var endian = order == ByteOrder.BigEndian ? "BigEndian" : "LittleEndian";

		return csType switch
		{
			"byte" => $"{spanExpr}[0] = {valueExpr}",
			"sbyte" => $"{spanExpr}[0] = (byte){valueExpr}",
			"ushort" => $"BinaryPrimitives.WriteUInt16{endian}({spanExpr}, {valueExpr})",
			"short" => $"BinaryPrimitives.WriteInt16{endian}({spanExpr}, {valueExpr})",
			"uint" => $"BinaryPrimitives.WriteUInt32{endian}({spanExpr}, {valueExpr})",
			"int" => $"BinaryPrimitives.WriteInt32{endian}({spanExpr}, {valueExpr})",
			"ulong" => $"BinaryPrimitives.WriteUInt64{endian}({spanExpr}, {valueExpr})",
			"long" => $"BinaryPrimitives.WriteInt64{endian}({spanExpr}, {valueExpr})",
			"float" => $"BinaryPrimitives.WriteSingle{endian}({spanExpr}, {valueExpr})",
			"double" => $"BinaryPrimitives.WriteDouble{endian}({spanExpr}, {valueExpr})",
			_ => $"{spanExpr}[0] = (byte){valueExpr}",
		};
	}

	/// <summary>Produces a unique entry type name scoped to the message, e.g. CarFuelFiguresEntry.</summary>
	/// <param name="messageName">The parent message name.</param>
	/// <param name="groupName">The repeating group name.</param>
	/// <returns>The combined entry type name.</returns>
	public static string GroupEntryTypeName(string messageName, string groupName)
		=> $"{messageName}{CapitalizeFirstChar(groupName)}Entry";

	/// <summary>Capitalizes the first character of a string.</summary>
	/// <param name="name">The string to capitalize.</param>
	/// <returns>The string with its first character uppercased.</returns>
	public static string CapitalizeFirstChar(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return name;
		}

		if (char.IsUpper(name[0]))
		{
			return name; // already capitalized
		}

		return string.Create(name.Length, name, (chars, src) =>
		{
			src.AsSpan().CopyTo(chars);
			chars[0] = char.ToUpperInvariant(chars[0]);
		});
	}
}
