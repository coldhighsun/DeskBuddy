using DeskBuddy.Domain.Pets;

namespace DeskBuddy.Domain.Tests.Pets;

/// <summary>
/// Tests for <see cref="AutoSwitchPolicy"/>.
/// </summary>
public class AutoSwitchPolicyTests
{
    /// <summary>
    /// The interval the policy under test uses.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>
    /// The fake clock shared with the policy.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The policy under test, enabled.
    /// </summary>
    private readonly AutoSwitchPolicy _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoSwitchPolicyTests"/> class.
    /// </summary>
    public AutoSwitchPolicyTests()
    {
        _policy = new AutoSwitchPolicy(_clock) { IsEnabled = true, Interval = Interval };
    }

    /// <summary>
    /// A policy that is not enabled never asks for a change, however long it waits.
    /// </summary>
    [Fact]
    public void Update_Disabled_NeverDue()
    {
        // Arrange
        var policy = new AutoSwitchPolicy(_clock) { Interval = Interval };

        // Act
        policy.Update(canSwitch: true);
        _clock.Advance(TimeSpan.FromDays(1));

        // Assert
        policy.Update(canSwitch: true).Should().BeFalse();
    }

    /// <summary>
    /// The first change comes after a whole interval, not the moment the policy is switched on.
    /// </summary>
    [Fact]
    public void Update_FirstAsk_WaitsAFullInterval()
    {
        // Act
        var atStart = _policy.Update(canSwitch: true);
        _clock.Advance(Interval - TimeSpan.FromSeconds(1));
        var justBefore = _policy.Update(canSwitch: true);
        _clock.Advance(TimeSpan.FromSeconds(1));
        var atInterval = _policy.Update(canSwitch: true);

        // Assert
        atStart.Should().BeFalse();
        justBefore.Should().BeFalse();
        atInterval.Should().BeTrue();
    }

    /// <summary>
    /// After a change the next one is a whole interval away.
    /// </summary>
    [Fact]
    public void Update_AfterAChange_WaitsAnotherInterval()
    {
        // Arrange
        _policy.Update(canSwitch: true);
        _clock.Advance(Interval);
        _policy.Update(canSwitch: true).Should().BeTrue();

        // Act
        _clock.Advance(Interval - TimeSpan.FromSeconds(1));
        var early = _policy.Update(canSwitch: true);
        _clock.Advance(TimeSpan.FromSeconds(1));
        var due = _policy.Update(canSwitch: true);

        // Assert
        early.Should().BeFalse();
        due.Should().BeTrue();
    }

    /// <summary>
    /// A change that comes due at a bad moment is made as soon as the moment is good.
    /// </summary>
    [Fact]
    public void Update_DueWhileBusy_IsMadeWhenFree()
    {
        // Arrange
        _policy.Update(canSwitch: true);
        _clock.Advance(Interval + TimeSpan.FromMinutes(5));

        // Act
        var busy = _policy.Update(canSwitch: false);
        var free = _policy.Update(canSwitch: true);

        // Assert
        busy.Should().BeFalse();
        free.Should().BeTrue();
    }

    /// <summary>
    /// Switching the policy off and on again starts the wait afresh.
    /// </summary>
    [Fact]
    public void Update_ReEnabled_StartsAfresh()
    {
        // Arrange
        _policy.Update(canSwitch: true);
        _clock.Advance(Interval - TimeSpan.FromMinutes(1));
        _policy.IsEnabled = false;
        _policy.Update(canSwitch: true);
        _clock.Advance(TimeSpan.FromMinutes(30));
        _policy.IsEnabled = true;

        // Act
        var onSwitchingOn = _policy.Update(canSwitch: true);
        _clock.Advance(Interval - TimeSpan.FromSeconds(1));
        var early = _policy.Update(canSwitch: true);

        // Assert
        onSwitchingOn.Should().BeFalse();
        early.Should().BeFalse();
    }

    /// <summary>
    /// Choosing another interval starts the wait afresh, so a long wait is not cut short or kept.
    /// </summary>
    [Fact]
    public void Interval_Changed_StartsAfresh()
    {
        // Arrange
        _policy.Update(canSwitch: true);
        _clock.Advance(TimeSpan.FromMinutes(50));
        _policy.Interval = TimeSpan.FromMinutes(30);

        // Act
        var restarted = _policy.Update(canSwitch: true);
        _clock.Advance(TimeSpan.FromMinutes(30));
        var due = _policy.Update(canSwitch: true);

        // Assert
        restarted.Should().BeFalse();
        due.Should().BeTrue();
    }

    /// <summary>
    /// A change that could not be made is tried again after a whole interval, not at every question.
    /// </summary>
    [Fact]
    public void Postpone_Due_WaitsAnotherInterval()
    {
        // Arrange
        _policy.Update(canSwitch: true);
        _clock.Advance(Interval);
        _policy.Postpone();

        // Act
        var right = _policy.Update(canSwitch: true);
        _clock.Advance(Interval);
        var later = _policy.Update(canSwitch: true);

        // Assert
        right.Should().BeFalse();
        later.Should().BeTrue();
    }

    /// <summary>
    /// Only standing and walking are free moments; every other state is left alone.
    /// </summary>
    [Fact]
    public void CanSwitchIn_EveryState_OnlyIdleAndWalk()
    {
        // Act
        var free = Enum.GetValues<PetState>().Where(AutoSwitchPolicy.CanSwitchIn);

        // Assert
        free.Should().BeEquivalentTo([PetState.Idle, PetState.Walk]);
    }

    /// <summary>
    /// The pet to change to is never the one on screen, however the names are spelled.
    /// </summary>
    [Fact]
    public void PickNext_Names_NeverReturnsTheCurrentOne()
    {
        // Arrange
        var random = new Random(5);
        string[] names = ["alpha", "Beta", "gamma"];

        for (var i = 0; i < 50; i++)
        {
            // Act
            var next = AutoSwitchPolicy.PickNext(names, "BETA", random);

            // Assert
            next.Should().NotBeNull().And.NotBe("Beta");
        }
    }

    /// <summary>
    /// Every other pet gets its turn.
    /// </summary>
    [Fact]
    public void PickNext_ManyTimes_ReachesEveryOtherPet()
    {
        // Arrange
        var random = new Random(8);
        string[] names = ["a", "b", "c", "d"];

        // Act
        var picked = Enumerable.Range(0, 200).Select(_ => AutoSwitchPolicy.PickNext(names, "a", random)).ToHashSet();

        // Assert
        picked.Should().BeEquivalentTo(new HashSet<string?> { "b", "c", "d" });
    }

    /// <summary>
    /// With no other pet there is nothing to change to.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void PickNext_NoOtherPet_ReturnsNull(int count)
    {
        // Arrange
        var names = Enumerable.Range(0, count).Select(_ => "only").ToList();

        // Act
        var next = AutoSwitchPolicy.PickNext(names, "only", new Random(1));

        // Assert
        next.Should().BeNull();
    }
}
