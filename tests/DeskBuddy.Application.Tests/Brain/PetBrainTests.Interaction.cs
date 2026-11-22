using DeskBuddy.Domain.Interaction;
using DeskBuddy.Domain.Messages;

namespace DeskBuddy.Application.Tests.Brain;

/// <content>
/// Tests for the reactions to being clicked.
/// </content>
public partial class PetBrainTests
{
    /// <summary>
    /// A click the pet is asked to remark on makes it say something right after the next tick, and it is delighted.
    /// </summary>
    [Fact]
    public void OnClicked_WithRemark_PetSaysTheRemarkOfAFirstClick()
    {
        // Arrange
        _brain.Tick();

        // Act
        var tier = _brain.OnClicked(remark: true);
        _brain.Tick();

        // Assert
        tier.Should().Be(ClickTier.OneClick);
        _brain.State.Should().Be(PetState.Happy);
        _brain.CurrentMessage.Should().NotBeNull();
        _brain.CurrentMessage!.Kind.Should().Be(PetMessageKind.Remark);
        _brain.CurrentMessage.Count.Should().Be(RemarkCode.Encode(PetState.Happy, (int)ClickTier.OneClick));
    }

    /// <summary>
    /// A click the pet is not asked to remark on stays quiet.
    /// </summary>
    [Fact]
    public void OnClicked_WithoutRemark_PetSaysNothing()
    {
        // Arrange
        _brain.Tick();

        // Act
        _brain.OnClicked();
        _brain.Tick();

        // Assert
        _brain.State.Should().Be(PetState.Happy);
        _brain.CurrentMessage.Should().BeNull();
    }

    /// <summary>
    /// Clicking again soon after changes the wording, and many clicks make the pet cross.
    /// </summary>
    [Fact]
    public void OnClicked_ClickedAgainAndAgain_RemarksGrowCross()
    {
        // Arrange
        _brain.Tick();
        var counts = new List<int>();

        // Act
        for (var i = 0; i < ClickStreak.AnnoyedAt; i++)
        {
            _brain.OnClicked(remark: true);
            _brain.Tick();
            counts.Add(_brain.CurrentMessage!.Count!.Value);
            _clock.Advance(TimeSpan.FromMilliseconds(200));
        }

        // Assert
        counts.Should().Equal(
            RemarkCode.Encode(PetState.Happy, (int)ClickTier.OneClick),
            RemarkCode.Encode(PetState.Happy, (int)ClickTier.Repeated),
            RemarkCode.Encode(PetState.Happy, (int)ClickTier.Repeated),
            RemarkCode.Encode(PetState.Happy, (int)ClickTier.Annoyed));
    }

    /// <summary>
    /// A pet that cannot speak (it is hidden) does not queue a remark for later.
    /// </summary>
    [Fact]
    public void OnClicked_PetHidden_NoRemarkIsHeldBack()
    {
        // Arrange
        _brain.Tick();
        _brain.SetManuallyHidden(true);
        _brain.Tick();

        // Act
        _brain.OnClicked(remark: true);
        _brain.SetManuallyHidden(false);
        _brain.Tick();

        // Assert
        _brain.CurrentMessage.Should().BeNull();
    }
}
