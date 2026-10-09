using AwesomeAssertions;
using DeskBuddy.Domain.Visibility;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Domain.Tests.Visibility;

/// <summary>
/// Tests for <see cref="PetVisibility"/>.
/// </summary>
public class PetVisibilityTests
{
    /// <summary>
    /// The restore delay used by the tests.
    /// </summary>
    private static readonly TimeSpan RestoreDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The fake clock shared with the object under test.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The object under test.
    /// </summary>
    private readonly PetVisibility _visibility;

    /// <summary>
    /// Initializes a new instance of the <see cref="PetVisibilityTests"/> class.
    /// </summary>
    public PetVisibilityTests()
    {
        _visibility = new PetVisibility(_clock, RestoreDelay);
    }

    /// <summary>
    /// A new pet is visible.
    /// </summary>
    [Fact]
    public void IsVisible_Initially_IsTrue()
    {
        // Assert
        _visibility.IsVisible.Should().BeTrue();
    }

    /// <summary>
    /// Hiding at the user's request hides the pet and reports the change; repeating it reports nothing.
    /// </summary>
    [Fact]
    public void SetManuallyHidden_Hide_ChangesVisibilityOnce()
    {
        // Act
        var first = _visibility.SetManuallyHidden(true);
        var second = _visibility.SetManuallyHidden(true);

        // Assert
        first.Should().BeTrue();
        second.Should().BeFalse();
        _visibility.IsVisible.Should().BeFalse();
    }

    /// <summary>
    /// Toggling hides, then shows again.
    /// </summary>
    [Fact]
    public void ToggleManuallyHidden_Twice_RestoresVisibility()
    {
        // Act
        _visibility.ToggleManuallyHidden();
        var hidden = _visibility.IsManuallyHidden;
        _visibility.ToggleManuallyHidden();

        // Assert
        hidden.Should().BeTrue();
        _visibility.IsVisible.Should().BeTrue();
    }

    /// <summary>
    /// A full-screen application hides the pet immediately.
    /// </summary>
    [Fact]
    public void UpdateFullscreen_Active_HidesImmediately()
    {
        // Act
        var changed = _visibility.UpdateFullscreen(fullscreenActive: true);

        // Assert
        changed.Should().BeTrue();
        _visibility.IsAutoHidden.Should().BeTrue();
        _visibility.IsVisible.Should().BeFalse();
    }

    /// <summary>
    /// The pet does not return the moment the full-screen application goes, only after the restore delay.
    /// </summary>
    [Fact]
    public void UpdateFullscreen_GoneBriefly_StaysHiddenUntilDelayPasses()
    {
        // Arrange
        _visibility.UpdateFullscreen(true);

        // Act
        var atOnce = _visibility.UpdateFullscreen(false);
        _clock.Advance(RestoreDelay - TimeSpan.FromMilliseconds(1));
        var almost = _visibility.UpdateFullscreen(false);
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        var restored = _visibility.UpdateFullscreen(false);

        // Assert
        atOnce.Should().BeFalse();
        almost.Should().BeFalse();
        restored.Should().BeTrue();
        _visibility.IsVisible.Should().BeTrue();
    }

    /// <summary>
    /// If the full-screen application returns during the restore delay the delay starts over, so the pet does not flicker.
    /// </summary>
    [Fact]
    public void UpdateFullscreen_ReturnsDuringDelay_RestartsDelay()
    {
        // Arrange
        _visibility.UpdateFullscreen(true);
        _visibility.UpdateFullscreen(false);
        _clock.Advance(TimeSpan.FromSeconds(1.5));
        _visibility.UpdateFullscreen(true);
        _visibility.UpdateFullscreen(false);

        // Act
        _clock.Advance(TimeSpan.FromSeconds(1.5));
        _visibility.UpdateFullscreen(false);

        // Assert
        _visibility.IsVisible.Should().BeFalse();
    }

    /// <summary>
    /// A pet the user hid stays hidden when the full-screen application goes away.
    /// </summary>
    [Fact]
    public void UpdateFullscreen_Ends_DoesNotShowManuallyHiddenPet()
    {
        // Arrange
        _visibility.SetManuallyHidden(true);
        _visibility.UpdateFullscreen(true);

        // Act
        _visibility.UpdateFullscreen(false);
        _clock.Advance(RestoreDelay);
        var changed = _visibility.UpdateFullscreen(false);

        // Assert
        changed.Should().BeFalse();
        _visibility.IsAutoHidden.Should().BeFalse();
        _visibility.IsVisible.Should().BeFalse();
    }

    /// <summary>
    /// Showing a pet while a full-screen application is in front does not make it visible.
    /// </summary>
    [Fact]
    public void SetManuallyHidden_ShowWhileFullscreen_StaysHidden()
    {
        // Arrange
        _visibility.SetManuallyHidden(true);
        _visibility.UpdateFullscreen(true);

        // Act
        var changed = _visibility.SetManuallyHidden(false);

        // Assert
        changed.Should().BeFalse();
        _visibility.IsVisible.Should().BeFalse();
    }

    /// <summary>
    /// With auto-hide switched off a full-screen application does not hide the pet.
    /// </summary>
    [Fact]
    public void UpdateFullscreen_AutoHideDisabled_NeverHides()
    {
        // Arrange
        _visibility.AutoHideEnabled = false;

        // Act
        var changed = _visibility.UpdateFullscreen(true);

        // Assert
        changed.Should().BeFalse();
        _visibility.IsVisible.Should().BeTrue();
    }

    /// <summary>
    /// Switching auto-hide off while the pet is auto-hidden brings it back at the next update, without the delay.
    /// </summary>
    [Fact]
    public void UpdateFullscreen_AutoHideDisabledWhileHidden_RestoresWithoutDelay()
    {
        // Arrange
        _visibility.UpdateFullscreen(true);
        _visibility.AutoHideEnabled = false;

        // Act
        var changed = _visibility.UpdateFullscreen(true);

        // Assert
        changed.Should().BeTrue();
        _visibility.IsVisible.Should().BeTrue();
    }
}
