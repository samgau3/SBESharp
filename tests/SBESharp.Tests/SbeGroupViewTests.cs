using System.Runtime.InteropServices;

namespace SBESharp.Tests;

public sealed class SbeGroupViewTests
{
	private const int EntryStride = 12;

	[Fact]
	public void Length_ReturnsValuePassedToConstructor()
	{
		// Arrange
		var buffer = new byte[EntryStride * 3];

		// Act
		var view = MakeView(buffer, count: 3, stride: EntryStride);

		// Assert
		Assert.Equal(3, view.Length);
	}

	[Fact]
	public void Stride_ReturnsValuePassedToConstructor()
	{
		// Arrange
		var buffer = new byte[EntryStride * 2];

		// Act
		var view = MakeView(buffer, count: 2, stride: EntryStride);

		// Assert
		Assert.Equal(EntryStride, view.Stride);
	}

	[Fact]
	public void Indexer_SingleEntry_ReadsCorrectly()
	{
		// Arrange
		var buffer = BuildBuffer([new TestEntry { Id = 42, Value = 3.14 }]);

		// Act
		var view = MakeView(buffer, count: 1, stride: EntryStride);

		// Assert
		Assert.Equal(42, view[0].Id);
		Assert.Equal(3.14, view[0].Value);
	}

	[Fact]
	public void Indexer_MultipleEntries_EachIndexReturnsItsOwnEntry()
	{
		// Arrange
		var entries = new TestEntry[]
		{
			new TestEntry { Id = 1, Value = 1.1 },
			new TestEntry { Id = 2, Value = 2.2 },
			new TestEntry { Id = 3, Value = 3.3 },
		};
		var buffer = BuildBuffer(entries);

		// Act
		var view = MakeView(buffer, count: 3, stride: EntryStride);

		// Assert
		Assert.Equal(1, view[0].Id);
		Assert.Equal(1.1, view[0].Value);
		Assert.Equal(2, view[1].Id);
		Assert.Equal(2.2, view[1].Value);
		Assert.Equal(3, view[2].Id);
		Assert.Equal(3.3, view[2].Value);
	}

	[Fact]
	public void Indexer_RepeatedAccessSameIndex_ReturnsSameValue()
	{
		// Arrange
		var buffer = BuildBuffer([new TestEntry { Id = 99, Value = 7.5 }]);
		var view = MakeView(buffer, count: 1, stride: EntryStride);

		// Act / Assert
		Assert.Equal(view[0].Id, view[0].Id);
		Assert.Equal(view[0].Value, view[0].Value);
	}

	[Fact]
	public void Indexer_StrideExceedsSizeOfT_ReadsCorrectFieldsIgnoresPadding()
	{
		// Arrange: stride=20, sizeof(TestEntry)=12, 8 bytes of padding per entry
		const int paddedStride = 20;
		var entries = new TestEntry[]
		{
			new TestEntry { Id = 10, Value = 1.0 },
			new TestEntry { Id = 20, Value = 2.0 },
		};
		var buffer = BuildBuffer(entries, paddedStride);

		// Act
		var view = MakeView(buffer, count: 2, stride: paddedStride);

		// Assert
		Assert.Equal(10, view[0].Id);
		Assert.Equal(1.0, view[0].Value);
		Assert.Equal(20, view[1].Id);
		Assert.Equal(2.0, view[1].Value);
	}

	[Fact]
	public void Indexer_IndexEqualsLength_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var buffer = BuildBuffer([new TestEntry { Id = 1, Value = 0 }]);
		var view = MakeView(buffer, count: 1, stride: EntryStride);

		// Act
		var threw = false;
		try
		{
			_ = view[1];
		}
		catch (ArgumentOutOfRangeException)
		{
			threw = true;
		}

		// Assert
		Assert.True(threw);
	}

	[Fact]
	public void Indexer_IndexExceedsLength_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var buffer = BuildBuffer([new TestEntry { Id = 1, Value = 0 }]);
		var view = MakeView(buffer, count: 1, stride: EntryStride);

		// Act
		var threw = false;
		try
		{
			_ = view[99];
		}
		catch (ArgumentOutOfRangeException)
		{
			threw = true;
		}

		// Assert
		Assert.True(threw);
	}

	[Fact]
	public void Indexer_NegativeIndex_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var buffer = BuildBuffer([new TestEntry { Id = 1, Value = 0 }]);
		var view = MakeView(buffer, count: 1, stride: EntryStride);

		// Act
		var threw = false;
		try
		{
			_ = view[-1];
		}
		catch (ArgumentOutOfRangeException)
		{
			threw = true;
		}

		// Assert
		Assert.True(threw);
	}

	[Fact]
	public void Indexer_EmptyView_AnyIndexThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var buffer = new byte[EntryStride];
		var view = MakeView(buffer, count: 0, stride: EntryStride);

		// Act
		var threw = false;
		try
		{
			_ = view[0];
		}
		catch (ArgumentOutOfRangeException)
		{
			threw = true;
		}

		// Assert
		Assert.True(threw);
	}

	[Fact]
	public void Foreach_IteratesAllEntriesInOrder()
	{
		// Arrange
		var entries = new TestEntry[]
		{
			new TestEntry { Id = 10, Value = 1.0 },
			new TestEntry { Id = 20, Value = 2.0 },
			new TestEntry { Id = 30, Value = 3.0 },
		};
		var buffer = BuildBuffer(entries);
		var view = MakeView(buffer, count: 3, stride: EntryStride);

		// Act
		var ids = new List<int>();
		foreach (var entry in view)
		{
			ids.Add(entry.Id);
		}

		// Assert
		Assert.Equal([10, 20, 30], ids);
	}

	[Fact]
	public void Foreach_EmptyView_DoesNotIterate()
	{
		// Arrange
		var buffer = new byte[EntryStride];
		var view = MakeView(buffer, count: 0, stride: EntryStride);

		// Act
		var iterationCount = 0;
		foreach (var entry in view)
		{
			iterationCount++;
		}

		// Assert
		Assert.Equal(0, iterationCount);
	}

	[Fact]
	public void Foreach_MatchesDirectIndexerAccess()
	{
		// Arrange
		var entries = new TestEntry[]
		{
			new TestEntry { Id = 5, Value = 0.5 },
			new TestEntry { Id = 6, Value = 0.6 },
		};
		var buffer = BuildBuffer(entries);
		var view = MakeView(buffer, count: 2, stride: EntryStride);

		// Act
		var i = 0;
		foreach (var entry in view)
		{
			Assert.Equal(view[i].Id, entry.Id);
			Assert.Equal(view[i].Value, entry.Value);
			i++;
		}

		// Assert
		Assert.Equal(2, i);
	}

	[Fact]
	public void GetEnumerator_CalledTwice_ProducesTwoIndependentIterations()
	{
		// Arrange
		var entries = new TestEntry[]
		{
			new TestEntry { Id = 1, Value = 0 },
			new TestEntry { Id = 2, Value = 0 },
		};
		var buffer = BuildBuffer(entries);
		var view = MakeView(buffer, count: 2, stride: EntryStride);

		// Act
		var firstPassIds = new List<int>();
		foreach (var entry in view)
		{
			firstPassIds.Add(entry.Id);
		}

		var secondPassIds = new List<int>();
		foreach (var entry in view)
		{
			secondPassIds.Add(entry.Id);
		}

		// Assert
		Assert.Equal(firstPassIds, secondPassIds);
	}

	[Fact]
	public void Enumerator_MoveNext_ReturnsFalseImmediatelyOnEmptyView()
	{
		// Arrange
		var buffer = new byte[EntryStride];
		var view = MakeView(buffer, count: 0, stride: EntryStride);

		// Act
		var enumerator = view.GetEnumerator();

		// Assert
		Assert.False(enumerator.MoveNext());
	}

	[Fact]
	public void Enumerator_MoveNext_ReturnsFalseAfterLastEntry()
	{
		// Arrange
		var buffer = BuildBuffer([new TestEntry { Id = 1, Value = 0 }]);
		var view = MakeView(buffer, count: 1, stride: EntryStride);
		var enumerator = view.GetEnumerator();

		// Act
		var firstMove = enumerator.MoveNext();
		var secondMove = enumerator.MoveNext();

		// Assert
		Assert.True(firstMove);
		Assert.False(secondMove);
	}

	[Fact]
	public void Default_LengthIsZero()
	{
		// Arrange / Act
		SbeGroupView<TestEntry> view = default;

		// Assert
		Assert.Equal(0, view.Length);
	}

	[Fact]
	public void Default_StrideIsZero()
	{
		// Arrange / Act
		SbeGroupView<TestEntry> view = default;

		// Assert
		Assert.Equal(0, view.Stride);
	}

	[Fact]
	public void Default_Foreach_DoesNotIterate()
	{
		// Arrange
		SbeGroupView<TestEntry> view = default;

		// Act
		var iterationCount = 0;
		foreach (var entry in view)
		{
			iterationCount++;
		}

		// Assert
		Assert.Equal(0, iterationCount);
	}

	[Fact]
	public void Default_IndexerAccess_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		SbeGroupView<TestEntry> view = default;

		// Act
		var threw = false;
		try
		{
			_ = view[0];
		}
		catch (ArgumentOutOfRangeException)
		{
			threw = true;
		}

		// Assert
		Assert.True(threw);
	}

	private static byte[] BuildBuffer(TestEntry[] entries, int stride = EntryStride)
	{
		var buffer = new byte[entries.Length * stride];
		for (var i = 0; i < entries.Length; i++)
		{
			MemoryMarshal.Write(buffer.AsSpan(i * stride), in entries[i]);
		}

		return buffer;
	}

	private static SbeGroupView<TestEntry> MakeView(byte[] buffer, int count, int stride)
		=> new SbeGroupView<TestEntry>(ref MemoryMarshal.GetReference(buffer.AsSpan()), stride, count);

	[StructLayout(LayoutKind.Explicit, Pack = 1)]
	private struct TestEntry
	{
		[FieldOffset(0)]
		public int Id;

		[FieldOffset(4)]
		public double Value;
	}
}
