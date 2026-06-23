using System.Runtime.CompilerServices;

namespace SBESharp;

/// <summary>
/// A zero-allocation, lazily-decoded view over a contiguous SBE repeating group region.
/// Holds a managed reference to the first entry byte, the wire stride (blockLength), and the
/// entry count. Elements are decoded on demand via the indexer — no heap allocation occurs.
/// </summary>
/// <typeparam name="T">An unmanaged entry struct with a blittable little-endian layout.</typeparam>
public ref struct SbeGroupView<T>
	where T : unmanaged
{
	private readonly ref byte _first;

	/// <summary>Initializes a new instance of the <see cref="SbeGroupView{T}"/> struct.</summary>
	/// <param name="first">Managed reference to the first byte of the first entry.</param>
	/// <param name="stride">Wire blockLength (bytes per entry) read from the group header.</param>
	/// <param name="length">Number of entries (numInGroup) read from the group header.</param>
	public SbeGroupView(ref byte first, int stride, int length)
	{
		this._first = ref first;
		this.Stride = stride;
		this.Length = length;
	}

	/// <summary>Gets the number of entries in this group.</summary>
	public int Length { get; }

	/// <summary>Gets the wire stride in bytes between consecutive entries (blockLength from the group header).</summary>
	public int Stride { get; }

	/// <summary>
	/// Returns the entry at <paramref name="index"/>, decoded on demand via <c>MemoryMarshal.Read</c>.
	/// No allocation occurs.
	/// </summary>
	/// <param name="index">Zero-based entry index.</param>
	/// <returns>The decoded entry value.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="index"/> is out of range.
	/// </exception>
	public T this[int index]
	{
		get
		{
			ArgumentOutOfRangeException.ThrowIfNegative(index);
			ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, this.Length);
			return Unsafe.ReadUnaligned<T>(ref Unsafe.Add(ref this._first, index * this.Stride));
		}
	}

	/// <summary>Returns an enumerator over the entries.</summary>
	/// <returns>A forward enumerator.</returns>
	public Enumerator GetEnumerator() => new Enumerator(this);

	/// <summary>Forward enumerator for <see cref="SbeGroupView{T}"/>.</summary>
	public ref struct Enumerator
	{
		private readonly SbeGroupView<T> _view;
		private int _index;

		internal Enumerator(SbeGroupView<T> view)
		{
			this._view = view;
			this._index = -1;
		}

		/// <summary>Gets the current entry.</summary>
		public T Current => this._view[this._index];

		/// <summary>Advances to the next entry.</summary>
		/// <returns><see langword="true"/> if another entry is available; otherwise <see langword="false"/>.</returns>
		public bool MoveNext() => ++this._index < this._view.Length;
	}
}
