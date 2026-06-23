using System.Text;

namespace SBESharp.Roslyn;

/// <summary>
/// Indent-aware text writer for emitting C# source. Each indentation level emits one tab character.
/// Modeled after the <c>SourceWriter</c> pattern in
/// <see href="https://github.com/eiriktsarpalis/PolyType">PolyType</see>.
/// </summary>
public sealed class SourceWriter
{
	private readonly StringBuilder _sb;

	/// <summary>Initializes a new instance of the <see cref="SourceWriter"/> class.</summary>
	/// <param name="capacity">Initial buffer capacity.</param>
	public SourceWriter(int capacity = 4096)
	{
		this._sb = new StringBuilder(capacity);
	}

	/// <summary>Gets or sets the current indentation level.</summary>
	public int Indentation { get; set; }

	/// <inheritdoc/>
	public override string ToString() => this._sb.ToString();

	/// <summary>Writes an empty line.</summary>
	public void WriteLine() => this._sb.Append('\n');

	/// <summary>Writes a single character at the current indentation, followed by a newline.</summary>
	/// <param name="c">The character to write.</param>
	public void WriteLine(char c)
	{
		this.AddIndentation();
		this._sb.Append(c);
		this._sb.Append('\n');
	}

	/// <summary>
	/// Writes text at the current indentation, followed by a newline.
	/// Multi-line strings (e.g. from <c>$$"""..."""</c> raw string literals) are split
	/// and each line is indented individually. Empty lines are emitted without
	/// indentation to avoid trailing whitespace.
	/// </summary>
	/// <param name="text">The text to write.</param>
	public void WriteLine(string text)
	{
		foreach (var line in text.AsSpan().EnumerateLines())
		{
			if (line.IsEmpty)
			{
				this._sb.Append('\n');
			}
			else
			{
				this.AddIndentation();
				this._sb.Append(line);
				this._sb.Append('\n');
			}
		}
	}

	/// <summary>Writes an XML doc comment summary line if the description is non-empty.</summary>
	/// <param name="description">The description text to wrap in a summary element, or <see langword="null"/> to skip.</param>
	public void WriteDocComment(string? description)
	{
		if (!string.IsNullOrWhiteSpace(description))
		{
			this.WriteLine($"/// <summary>{description}</summary>");
		}
	}

	private void AddIndentation()
	{
		for (var i = 0; i < this.Indentation; i++)
		{
			this._sb.Append('\t');
		}
	}
}
