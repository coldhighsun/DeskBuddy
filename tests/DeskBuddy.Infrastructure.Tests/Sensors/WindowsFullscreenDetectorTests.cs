using DeskBuddy.Application;
using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain.Settings;
using DeskBuddy.Infrastructure.Sensors;

namespace DeskBuddy.Infrastructure.Tests.Sensors;

/// <summary>
/// Tests for <see cref="WindowsFullscreenDetector"/>.
/// </summary>
public class WindowsFullscreenDetectorTests
{
    /// <summary>
    /// States that mean the user must not be disturbed.
    /// </summary>
    /// <param name="state">
    /// A <c>QUERY_USER_NOTIFICATION_STATE</c> value.
    /// </param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void IsSuppressing_DoNotDisturbStates_ReturnsTrue(int state)
    {
        // Act
        var result = WindowsFullscreenDetector.IsSuppressing(state);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// States where notices are fine, plus values the shell never reports.
    /// </summary>
    /// <param name="state">
    /// A <c>QUERY_USER_NOTIFICATION_STATE</c> value or an out-of-range value.
    /// </param>
    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(-1)]
    public void IsSuppressing_OtherStates_ReturnsFalse(int state)
    {
        // Act
        var result = WindowsFullscreenDetector.IsSuppressing(state);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Builds a detector over the default settings.
    /// </summary>
    /// <param name="settings">
    /// The settings to run with.
    /// </param>
    /// <returns>
    /// The detector.
    /// </returns>
    private static WindowsFullscreenDetector CreateDetector(PetSettings settings)
    {
        var service = new SettingsService(new FixedStore(settings));
        service.InitializeAsync().GetAwaiter().GetResult();
        return new WindowsFullscreenDetector(service);
    }

    /// <summary>
    /// The shell states that mean a full-screen or presenting application are Fullscreen.
    /// </summary>
    /// <param name="state">
    /// A <c>QUERY_USER_NOTIFICATION_STATE</c> value.
    /// </param>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Classify_FullscreenShellStates_ReturnsFullscreen(int state)
    {
        // Act
        var level = WindowsFullscreenDetector.Classify(state, null, false, PetSettings.Default);

        // Assert
        level.Should().Be(DoNotDisturbLevel.Fullscreen);
    }

    /// <summary>
    /// Locked screen and Focus Assist hold messages back without hiding the pet.
    /// </summary>
    /// <param name="state">
    /// A <c>QUERY_USER_NOTIFICATION_STATE</c> value.
    /// </param>
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void Classify_QuietShellStates_ReturnsQuiet(int state)
    {
        // Act
        var level = WindowsFullscreenDetector.Classify(state, null, false, PetSettings.Default);

        // Assert
        level.Should().Be(DoNotDisturbLevel.Quiet);
    }

    /// <summary>
    /// With nothing special going on the level is None.
    /// </summary>
    [Fact]
    public void Classify_AcceptingNotifications_ReturnsNone()
    {
        // Act
        var level = WindowsFullscreenDetector.Classify(5, "notepad", false, PetSettings.Default);

        // Assert
        level.Should().Be(DoNotDisturbLevel.None);
    }

    /// <summary>
    /// A blacklisted foreground process counts as full screen, whatever the case of its name.
    /// </summary>
    [Fact]
    public void Classify_BlacklistedProcessInFront_ReturnsFullscreen()
    {
        // Arrange
        var settings = PetSettings.Default with { BlacklistedProcesses = ["Zoom", "wemeetapp"] };

        // Act
        var level = WindowsFullscreenDetector.Classify(5, "zoom", false, settings);

        // Assert
        level.Should().Be(DoNotDisturbLevel.Fullscreen);
    }

    /// <summary>
    /// A process that is not on the list does not count.
    /// </summary>
    [Fact]
    public void Classify_OtherProcessInFront_ReturnsNone()
    {
        // Arrange
        var settings = PetSettings.Default with { BlacklistedProcesses = ["Zoom"] };

        // Act
        var level = WindowsFullscreenDetector.Classify(5, "zoomit", false, settings);

        // Assert
        level.Should().Be(DoNotDisturbLevel.None);
    }

    /// <summary>
    /// A window covering a monitor counts only when the option is on.
    /// </summary>
    /// <param name="detectCovered">
    /// Whether the option is on.
    /// </param>
    /// <param name="expected">
    /// The expected level.
    /// </param>
    [Theory]
    [InlineData(true, DoNotDisturbLevel.Fullscreen)]
    [InlineData(false, DoNotDisturbLevel.None)]
    public void Classify_WindowCoversMonitor_DependsOnOption(bool detectCovered, DoNotDisturbLevel expected)
    {
        // Arrange
        var settings = PetSettings.Default with { DetectCoveredMonitor = detectCovered };

        // Act
        var level = WindowsFullscreenDetector.Classify(5, "game", true, settings);

        // Assert
        level.Should().Be(expected);
    }

    /// <summary>
    /// The real shell and window calls succeed without throwing, with every option on.
    /// </summary>
    [Fact]
    public void GetLevel_RealApi_DoesNotThrow()
    {
        // Arrange
        var detector = CreateDetector(PetSettings.Default with
        {
            DetectCoveredMonitor = true,
            BlacklistedProcesses = ["definitely-not-running"],
        });

        // Act
        var act = () => detector.GetLevel();

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    /// Settings storage that returns fixed settings.
    /// </summary>
    /// <param name="settings">
    /// The settings to return.
    /// </param>
    private sealed class FixedStore(PetSettings settings) : ISettingsStore
    {
        /// <inheritdoc />
        public Task<PetSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(settings);
        }

        /// <inheritdoc />
        public Task SaveAsync(PetSettings value, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
