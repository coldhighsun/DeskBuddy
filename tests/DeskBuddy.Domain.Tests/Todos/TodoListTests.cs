using AwesomeAssertions;
using DeskBuddy.Domain.Todos;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Domain.Tests.Todos;

/// <summary>
/// Tests for <see cref="TodoList"/>.
/// </summary>
public class TodoListTests
{
    /// <summary>
    /// The fake clock shared with the list under test.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The list under test.
    /// </summary>
    private readonly TodoList _list;

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoListTests"/> class.
    /// </summary>
    public TodoListTests()
    {
        _list = new TodoList(_clock);
    }

    /// <summary>
    /// Adding creates an open entry stamped with the current time.
    /// </summary>
    [Fact]
    public void Add_ValidTitle_CreatesOpenItem()
    {
        // Act
        var item = _list.Add("Write report");

        // Assert
        item.Should().NotBeNull();
        item!.Title.Should().Be("Write report");
        item.IsDone.Should().BeFalse();
        item.CompletedAt.Should().BeNull();
        item.CreatedAt.Should().Be(_clock.GetUtcNow());
        _list.Items.Should().ContainSingle().Which.Should().Be(item);
    }

    /// <summary>
    /// Surrounding whitespace is trimmed from the title.
    /// </summary>
    [Fact]
    public void Add_TitleWithSurroundingWhitespace_TrimsIt()
    {
        // Act
        var item = _list.Add("  buy milk \t");

        // Assert
        item!.Title.Should().Be("buy milk");
    }

    /// <summary>
    /// Empty or whitespace-only titles are rejected without adding anything.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Add_BlankTitle_ReturnsNullAndAddsNothing(string? title)
    {
        // Act
        var item = _list.Add(title);

        // Assert
        item.Should().BeNull();
        _list.Items.Should().BeEmpty();
    }

    /// <summary>
    /// A title of exactly the maximum length is accepted.
    /// </summary>
    [Fact]
    public void Add_TitleAtMaxLength_IsAccepted()
    {
        // Act
        var item = _list.Add(new string('x', TodoList.MaxTitleLength));

        // Assert
        item.Should().NotBeNull();
    }

    /// <summary>
    /// A title longer than the maximum is rejected.
    /// </summary>
    [Fact]
    public void Add_TitleTooLong_Throws()
    {
        // Act
        var act = () => _list.Add(new string('x', TodoList.MaxTitleLength + 1));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Completing an entry marks it done and records when.
    /// </summary>
    [Fact]
    public void Toggle_OpenItem_CompletesIt()
    {
        // Arrange
        var item = _list.Add("task")!;
        _clock.Advance(TimeSpan.FromMinutes(5));

        // Act
        var found = _list.Toggle(item.Id);

        // Assert
        found.Should().BeTrue();
        var updated = _list.Items.Single();
        updated.IsDone.Should().BeTrue();
        updated.CompletedAt.Should().Be(_clock.GetUtcNow());
        updated.CreatedAt.Should().Be(item.CreatedAt);
    }

    /// <summary>
    /// Toggling a completed entry reopens it and clears the completion time.
    /// </summary>
    [Fact]
    public void Toggle_DoneItem_ReopensIt()
    {
        // Arrange
        var item = _list.Add("task")!;
        _list.Toggle(item.Id);

        // Act
        _list.Toggle(item.Id);

        // Assert
        var updated = _list.Items.Single();
        updated.IsDone.Should().BeFalse();
        updated.CompletedAt.Should().BeNull();
    }

    /// <summary>
    /// Toggling an unknown entry reports that it was not found.
    /// </summary>
    [Fact]
    public void Toggle_UnknownId_ReturnsFalse()
    {
        // Act
        var found = _list.Toggle(Guid.NewGuid());

        // Assert
        found.Should().BeFalse();
    }

    /// <summary>
    /// Removing deletes only the chosen entry.
    /// </summary>
    [Fact]
    public void Remove_ExistingItem_DeletesOnlyThatItem()
    {
        // Arrange
        var first = _list.Add("first")!;
        var second = _list.Add("second")!;

        // Act
        var removed = _list.Remove(first.Id);

        // Assert
        removed.Should().BeTrue();
        _list.Items.Should().ContainSingle().Which.Id.Should().Be(second.Id);
    }

    /// <summary>
    /// Removing an unknown entry reports that nothing was removed.
    /// </summary>
    [Fact]
    public void Remove_UnknownId_ReturnsFalse()
    {
        // Act
        var removed = _list.Remove(Guid.NewGuid());

        // Assert
        removed.Should().BeFalse();
    }

    /// <summary>
    /// Clearing completed entries removes them and keeps the open ones.
    /// </summary>
    [Fact]
    public void ClearCompleted_MixedItems_RemovesOnlyDone()
    {
        // Arrange
        var open = _list.Add("open")!;
        var done1 = _list.Add("done1")!;
        var done2 = _list.Add("done2")!;
        _list.Toggle(done1.Id);
        _list.Toggle(done2.Id);

        // Act
        var count = _list.ClearCompleted();

        // Assert
        count.Should().Be(2);
        _list.Items.Should().ContainSingle().Which.Id.Should().Be(open.Id);
    }

    /// <summary>
    /// Open entries are listed first, oldest first.
    /// </summary>
    [Fact]
    public void Items_OpenEntries_OrderedOldestFirst()
    {
        // Arrange
        _list.Add("a");
        _clock.Advance(TimeSpan.FromMinutes(1));
        _list.Add("b");
        _clock.Advance(TimeSpan.FromMinutes(1));
        _list.Add("c");

        // Assert
        _list.Items.Select(item => item.Title).Should().Equal("a", "b", "c");
    }

    /// <summary>
    /// Completed entries come after open ones, most recently completed first.
    /// </summary>
    [Fact]
    public void Items_WithCompleted_OpenFirstThenDoneNewestFirst()
    {
        // Arrange
        var a = _list.Add("a")!;
        _clock.Advance(TimeSpan.FromMinutes(1));
        var b = _list.Add("b")!;
        _clock.Advance(TimeSpan.FromMinutes(1));
        _list.Add("c");
        _clock.Advance(TimeSpan.FromMinutes(1));
        _list.Toggle(b.Id);
        _clock.Advance(TimeSpan.FromMinutes(1));
        _list.Toggle(a.Id);

        // Assert: open "c", then "a" (completed last), then "b".
        _list.Items.Select(item => item.Title).Should().Equal("c", "a", "b");
    }

    /// <summary>
    /// A list built from existing entries exposes them.
    /// </summary>
    [Fact]
    public void Constructor_WithExistingItems_ContainsThem()
    {
        // Arrange
        var existing = new TodoItem(Guid.NewGuid(), "loaded", IsDone: false, _clock.GetUtcNow());

        // Act
        var list = new TodoList(_clock, [existing]);

        // Assert
        list.Items.Should().ContainSingle().Which.Should().Be(existing);
    }
}
