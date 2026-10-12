using AwesomeAssertions;
using DeskBuddy.App.Rendering;
using DeskBuddy.Application;
using DeskBuddy.Domain.Settings;
using DeskBuddy.Domain.Skins;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="SkinSwitcher"/>.
/// </summary>
public class SkinSwitcherTests
{
    /// <summary>
    /// The skins that were put on screen, in order.
    /// </summary>
    private readonly List<string> _applied = [];

    /// <summary>
    /// The reasons reported for failed switches.
    /// </summary>
    private readonly List<string> _failures = [];

    /// <summary>
    /// The loader the switcher uses.
    /// </summary>
    private readonly FakeSkinLoader _loader = new();

    /// <summary>
    /// The settings the switcher follows.
    /// </summary>
    private readonly SettingsService _settings;

    /// <summary>
    /// The storage behind the settings.
    /// </summary>
    private readonly FakeSettingsStore _store = new();

    /// <summary>
    /// The switcher under test.
    /// </summary>
    private readonly SkinSwitcher _switcher;

    /// <summary>
    /// What the apply step does besides recording; may throw to simulate undecodable images.
    /// </summary>
    private Action<Skin> _applyBehavior = _ => { };

    /// <summary>
    /// Initializes a new instance of the <see cref="SkinSwitcherTests"/> class.
    /// </summary>
    public SkinSwitcherTests()
    {
        _settings = new SettingsService(_store);
        _loader.Available.Add(PetSettings.DefaultSkinName);
        _loader.Available.Add("cat");
        _switcher = new SkinSwitcher(
            _settings,
            _loader,
            "skins",
            PetSettings.DefaultSkinName,
            skin =>
            {
                _applyBehavior(skin);
                _applied.Add(skin.Name);
            },
            NullLogger<SkinSwitcher>.Instance);
        _switcher.SwitchFailed += (_, reason) => _failures.Add(reason);
    }

    /// <summary>
    /// After disposal the switcher no longer follows the settings.
    /// </summary>
    [Fact]
    public async Task Dispose_ThenSettingsChanged_DoesNothing()
    {
        // Arrange
        _switcher.Dispose();

        // Act
        await _settings.UpdateAsync(s => s with { SkinName = "cat" });
        await Task.Delay(50);

        // Assert
        _applied.Should().BeEmpty();
    }

    /// <summary>
    /// Changing the skin in the settings switches the skin on screen.
    /// </summary>
    [Fact]
    public async Task SettingsChanged_SkinName_SwitchesSkin()
    {
        // Act
        await _settings.UpdateAsync(s => s with { SkinName = "cat" });
        await EventuallyAsync(() => _applied.Count > 0);

        // Assert
        _applied.Should().Equal("cat");
    }

    /// <summary>
    /// After a failure the previous skin can still be switched away from and back to normally.
    /// </summary>
    [Fact]
    public async Task SwitchAsync_AfterFailure_NextSwitchWorks()
    {
        // Arrange
        await _switcher.SwitchAsync("broken");

        // Act
        await _switcher.SwitchAsync("cat");

        // Assert
        _applied.Should().Equal("cat");
    }

    /// <summary>
    /// A skin whose images cannot be decoded is treated the same as one that cannot be loaded.
    /// </summary>
    [Fact]
    public async Task SwitchAsync_ApplyFails_KeepsOldSkinAndReports()
    {
        // Arrange
        _applyBehavior = _ => throw new NotSupportedException("bad image");

        // Act
        await _switcher.SwitchAsync("cat");

        // Assert
        _failures.Should().Equal("bad image");
        _applied.Should().BeEmpty();
    }

    /// <summary>
    /// A skin that cannot be loaded leaves the old one, reports why, and puts the setting back.
    /// </summary>
    [Fact]
    public async Task SwitchAsync_LoadFails_KeepsOldSkinReportsAndRestoresSetting()
    {
        // Arrange
        await _settings.UpdateAsync(s => s with { SkinName = "broken" });
        await EventuallyAsync(() => _failures.Count > 0);

        // Assert
        _applied.Should().BeEmpty();
        _failures.Should().ContainSingle();
        await EventuallyAsync(() => _settings.Current.SkinName == PetSettings.DefaultSkinName);
        _settings.Current.SkinName.Should().Be(PetSettings.DefaultSkinName);
    }

    /// <summary>
    /// A different skin is loaded from its folder and put on screen.
    /// </summary>
    [Fact]
    public async Task SwitchAsync_OtherSkin_LoadsAndApplies()
    {
        // Act
        await _switcher.SwitchAsync("cat");

        // Assert
        _loader.Requested.Should().Equal("cat");
        _applied.Should().Equal("cat");
        _failures.Should().BeEmpty();
    }

    /// <summary>
    /// When two switches overlap, the later request wins even if the earlier one finishes loading last.
    /// </summary>
    [Fact]
    public async Task SwitchAsync_OverlappingRequests_LastOneWins()
    {
        // Arrange
        _loader.Available.Add("dog");
        var gate = new TaskCompletionSource();
        _loader.Gates["cat"] = gate;
        var slow = _switcher.SwitchAsync("cat");

        // Act
        await _switcher.SwitchAsync("dog");
        gate.SetResult();
        await slow;

        // Assert
        _applied.Should().Equal("dog");
    }

    /// <summary>
    /// Asking for the skin that is already on screen does nothing.
    /// </summary>
    [Fact]
    public async Task SwitchAsync_SameSkin_DoesNothing()
    {
        // Act
        await _switcher.SwitchAsync(PetSettings.DefaultSkinName);

        // Assert
        _loader.Requested.Should().BeEmpty();
        _applied.Should().BeEmpty();
    }

    /// <summary>
    /// Waits, briefly, for something that happens after a hand-off to another continuation.
    /// </summary>
    /// <param name="condition">
    /// The condition to wait for.
    /// </param>
    /// <returns>
    /// A task that completes when the condition holds, or after a timeout.
    /// </returns>
    private static async Task EventuallyAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }
}