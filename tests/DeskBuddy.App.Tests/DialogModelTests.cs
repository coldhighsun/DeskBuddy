namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="DialogModel"/>.
/// </summary>
public sealed class DialogModelTests
{
    /// <summary>
    /// A dialog with a Cancel button treats closing it as cancelling, so nothing happens by accident.
    /// </summary>
    [Fact]
    public void CloseChoice_WithCancelButton_IsCancel()
    {
        // Arrange
        var buttons = DialogModel.SaveDiscardCancelButtons();

        // Act
        var choice = DialogModel.CloseChoice(buttons);

        // Assert
        choice.Should().Be(DialogChoice.Cancel);
    }

    /// <summary>
    /// A yes-or-no question treats closing it as No.
    /// </summary>
    [Fact]
    public void CloseChoice_YesNo_IsNo()
    {
        // Arrange
        var buttons = DialogModel.YesNoButtons();

        // Act
        var choice = DialogModel.CloseChoice(buttons);

        // Assert
        choice.Should().Be(DialogChoice.No);
    }

    /// <summary>
    /// A notice with a single button treats closing it as OK.
    /// </summary>
    [Fact]
    public void CloseChoice_OkOnly_IsOk()
    {
        // Arrange
        var buttons = DialogModel.OkButtons();

        // Act
        var choice = DialogModel.CloseChoice(buttons);

        // Assert
        choice.Should().Be(DialogChoice.Ok);
    }

    /// <summary>
    /// Every set of buttons has exactly one primary button, so Enter always has one clear meaning.
    /// </summary>
    [Fact]
    public void Buttons_EverySet_HaveExactlyOnePrimary()
    {
        // Arrange
        var sets = new[] { DialogModel.OkButtons(), DialogModel.YesNoButtons(), DialogModel.SaveDiscardCancelButtons() };

        // Act
        var primaryCounts = sets.Select(set => set.Count(button => button.IsPrimary)).ToList();

        // Assert
        primaryCounts.Should().OnlyContain(count => count == 1);
    }

    /// <summary>
    /// Each kind of dialog has its own icon, and every icon is a single glyph.
    /// </summary>
    [Fact]
    public void Glyph_EveryKind_IsDistinctSingleCharacter()
    {
        // Arrange
        var kinds = Enum.GetValues<DialogKind>();

        // Act
        var glyphs = kinds.Select(DialogModel.Glyph).ToList();

        // Assert
        glyphs.Should().OnlyContain(glyph => glyph.Length == 1);
        glyphs.Should().OnlyHaveUniqueItems();
    }
}
