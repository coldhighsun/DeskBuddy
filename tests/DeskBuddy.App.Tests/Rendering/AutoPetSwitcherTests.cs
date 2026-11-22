using DeskBuddy.App.Rendering;
using DeskBuddy.Domain.Pets;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.App.Tests.Rendering;

/// <summary>
/// Tests for <see cref="AutoPetSwitcher"/>; the pets are made up on the spot, with none named in the code under test.
/// </summary>
public class AutoPetSwitcherTests
{
    /// <summary>
    /// The interval the switcher waits between changes.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>
    /// The fake clock shared with the policy.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The settings the switcher changes.
    /// </summary>
    private readonly SettingsService _settings = new(new FakeSettingsStore());

    /// <summary>
    /// The installed pets.
    /// </summary>
    private readonly FakePetCatalog _catalog = new("one", "two", "three");

    /// <summary>
    /// The loader that knows which pets can be shown.
    /// </summary>
    private readonly FakePetLoader _loader = new();

    /// <summary>
    /// Whether the pet is free to be changed.
    /// </summary>
    private bool _free = true;

    /// <summary>
    /// The switcher under test.
    /// </summary>
    private readonly AutoPetSwitcher _switcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoPetSwitcherTests"/> class with changing switched on.
    /// </summary>
    public AutoPetSwitcherTests()
    {
        _settings.UpdateAsync(s => s with { PetName = "one" }).GetAwaiter().GetResult();
        _loader.Available.UnionWith(["one", "two", "three"]);
        var policy = new AutoSwitchPolicy(_clock) { IsEnabled = true, Interval = Interval };
        _switcher = new AutoPetSwitcher(policy, _settings, _catalog, _loader, () => _free, new Random(4), NullLogger<AutoPetSwitcher>.Instance);
    }

    /// <summary>
    /// Nothing happens before the interval is over; then the pet is changed to another one.
    /// </summary>
    [Fact]
    public async Task OnTickAsync_IntervalOver_ChangesToAnotherPet()
    {
        // Arrange
        await _switcher.OnTickAsync();
        _clock.Advance(Interval - TimeSpan.FromSeconds(1));
        await _switcher.OnTickAsync();
        var early = _settings.Current.PetName;

        // Act
        _clock.Advance(TimeSpan.FromSeconds(1));
        await _switcher.OnTickAsync();

        // Assert
        early.Should().Be("one");
        _settings.Current.PetName.Should().BeOneOf("two", "three");
    }

    /// <summary>
    /// A pet that cannot be loaded is never chosen; the next one is.
    /// </summary>
    [Fact]
    public async Task OnTickAsync_OnePetBroken_ChoosesTheOther()
    {
        // Arrange
        _loader.Available.Remove("two");
        await _switcher.OnTickAsync();
        _clock.Advance(Interval);

        // Act
        await _switcher.OnTickAsync();

        // Assert
        _settings.Current.PetName.Should().Be("three");
    }

    /// <summary>
    /// With no other pet that can be shown the pet stays, and the next try is an interval away, not the next tick.
    /// </summary>
    [Fact]
    public async Task OnTickAsync_NoOtherPetCanBeShown_StaysAndWaitsAnotherInterval()
    {
        // Arrange
        _loader.Available.Remove("two");
        _loader.Available.Remove("three");
        await _switcher.OnTickAsync();
        _clock.Advance(Interval);
        await _switcher.OnTickAsync();
        var asked = _loader.Requested.Count;

        // Act
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _switcher.OnTickAsync();

        // Assert
        _settings.Current.PetName.Should().Be("one");
        _loader.Requested.Count.Should().Be(asked);
    }

    /// <summary>
    /// A change that comes due while the pet is busy is made as soon as it is free.
    /// </summary>
    [Fact]
    public async Task OnTickAsync_DueWhileBusy_ChangesWhenFree()
    {
        // Arrange
        await _switcher.OnTickAsync();
        _clock.Advance(Interval);
        _free = false;
        await _switcher.OnTickAsync();
        var whileBusy = _settings.Current.PetName;

        // Act
        _free = true;
        await _switcher.OnTickAsync();

        // Assert
        whileBusy.Should().Be("one");
        _settings.Current.PetName.Should().NotBe("one");
    }

    /// <summary>
    /// A pet chosen by hand is not taken away soon after: the wait starts afresh.
    /// </summary>
    [Fact]
    public async Task OnTickAsync_PetChangedByHand_WaitsAFullIntervalAgain()
    {
        // Arrange
        await _switcher.OnTickAsync();
        _clock.Advance(Interval - TimeSpan.FromSeconds(10));
        await _settings.UpdateAsync(s => s with { PetName = "two" }, TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromSeconds(20));

        // Act
        await _switcher.OnTickAsync();

        // Assert
        _settings.Current.PetName.Should().Be("two");
    }

    /// <summary>
    /// After <see cref="AutoPetSwitcher.Stop"/> a change by hand no longer reaches the policy and nothing breaks.
    /// </summary>
    [Fact]
    public async Task Stop_ThenPetChanged_DoesNotThrow()
    {
        // Arrange
        _switcher.Stop();

        // Act
        var act = () => _settings.UpdateAsync(s => s with { PetName = "two" }, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
