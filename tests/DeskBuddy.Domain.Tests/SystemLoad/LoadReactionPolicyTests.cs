using DeskBuddy.Domain.SystemLoad;

namespace DeskBuddy.Domain.Tests.SystemLoad;

/// <summary>
/// Tests for <see cref="LoadReactionPolicy"/>. Options: high at CPU 85 / memory 90 for 10 s, recovered at CPU 60 /
/// memory 80 for 10 s.
/// </summary>
public class LoadReactionPolicyTests
{
    /// <summary>
    /// Duration used for both the high and recover requirement.
    /// </summary>
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A calm reading, below every recover threshold.
    /// </summary>
    private static readonly SystemLoadSnapshot Calm = new(10, 40);

    /// <summary>
    /// A reading with CPU above the high threshold.
    /// </summary>
    private static readonly SystemLoadSnapshot HighCpu = new(95, 40);

    /// <summary>
    /// A reading with memory above the high threshold.
    /// </summary>
    private static readonly SystemLoadSnapshot HighMemory = new(10, 95);

    /// <summary>
    /// A reading inside the hysteresis band: neither high nor recovered.
    /// </summary>
    private static readonly SystemLoadSnapshot Middling = new(72, 85);

    /// <summary>
    /// The fake clock shared with the policy under test.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The policy under test.
    /// </summary>
    private readonly LoadReactionPolicy _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoadReactionPolicyTests"/> class.
    /// </summary>
    public LoadReactionPolicyTests()
    {
        var options = new SystemLoadOptions { HighDuration = Window, RecoverDuration = Window };
        _policy = new LoadReactionPolicy(options, _clock);
    }

    /// <summary>
    /// Feeds the same reading once per second for the given number of seconds.
    /// </summary>
    /// <param name="snapshot">
    /// The reading to feed.
    /// </param>
    /// <param name="seconds">
    /// How many one-second steps to run.
    /// </param>
    private void Feed(SystemLoadSnapshot snapshot, int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            _policy.Update(snapshot);
            _clock.Advance(TimeSpan.FromSeconds(1));
        }
    }

    /// <summary>
    /// A new policy does not consider the system strained.
    /// </summary>
    [Fact]
    public void IsStrained_Initially_IsFalse()
    {
        // Assert
        _policy.IsStrained.Should().BeFalse();
    }

    /// <summary>
    /// A calm system is never strained.
    /// </summary>
    [Fact]
    public void Update_CalmSystem_StaysNotStrained()
    {
        // Act
        Feed(Calm, 60);

        // Assert
        _policy.Update(Calm).Should().BeFalse();
    }

    /// <summary>
    /// A brief spike shorter than the required duration is ignored.
    /// </summary>
    [Fact]
    public void Update_ShortSpike_IsIgnored()
    {
        // Act
        Feed(HighCpu, 5);
        Feed(Calm, 5);

        // Assert
        _policy.IsStrained.Should().BeFalse();
    }

    /// <summary>
    /// Load that stays high for the required duration marks the system as strained.
    /// </summary>
    [Fact]
    public void Update_SustainedHighCpu_BecomesStrained()
    {
        // Act
        Feed(HighCpu, 11);

        // Assert
        _policy.IsStrained.Should().BeTrue();
    }

    /// <summary>
    /// The strained flag is not set until the full duration has elapsed.
    /// </summary>
    [Fact]
    public void Update_HighJustUnderDuration_NotYetStrained()
    {
        // Act
        Feed(HighCpu, 10);

        // Assert
        _policy.IsStrained.Should().BeFalse();
    }

    /// <summary>
    /// High memory alone is enough to strain the system.
    /// </summary>
    [Fact]
    public void Update_SustainedHighMemory_BecomesStrained()
    {
        // Act
        Feed(HighMemory, 11);

        // Assert
        _policy.IsStrained.Should().BeTrue();
    }

    /// <summary>
    /// A dip below the high threshold restarts the high timer, so intermittent spikes do not add up.
    /// </summary>
    [Fact]
    public void Update_InterruptedHighLoad_RestartsTimer()
    {
        // Act
        Feed(HighCpu, 6);
        Feed(Calm, 1);
        Feed(HighCpu, 6);

        // Assert
        _policy.IsStrained.Should().BeFalse();
    }

    /// <summary>
    /// Readings inside the hysteresis band do not clear the strained flag.
    /// </summary>
    [Fact]
    public void Update_StrainedAndLoadInBand_StaysStrained()
    {
        // Arrange
        Feed(HighCpu, 11);

        // Act
        Feed(Middling, 60);

        // Assert
        _policy.IsStrained.Should().BeTrue();
    }

    /// <summary>
    /// Sustained low load clears the strained flag.
    /// </summary>
    [Fact]
    public void Update_StrainedAndSustainedCalm_Recovers()
    {
        // Arrange
        Feed(HighCpu, 11);

        // Act
        Feed(Calm, 11);

        // Assert
        _policy.IsStrained.Should().BeFalse();
    }

    /// <summary>
    /// Recovery needs both CPU and memory to be low.
    /// </summary>
    [Fact]
    public void Update_StrainedAndMemoryStillHigh_DoesNotRecover()
    {
        // Arrange
        Feed(HighCpu, 11);

        // Act
        Feed(new SystemLoadSnapshot(10, 85), 60);

        // Assert
        _policy.IsStrained.Should().BeTrue();
    }

    /// <summary>
    /// A short calm moment during a strained period does not clear the flag.
    /// </summary>
    [Fact]
    public void Update_StrainedAndBriefCalm_StaysStrained()
    {
        // Arrange
        Feed(HighCpu, 11);

        // Act
        Feed(Calm, 5);
        Feed(HighCpu, 1);
        Feed(Calm, 5);

        // Assert
        _policy.IsStrained.Should().BeTrue();
    }

    /// <summary>
    /// After recovering, the system can become strained again.
    /// </summary>
    [Fact]
    public void Update_RecoveredThenHighAgain_BecomesStrainedAgain()
    {
        // Arrange
        Feed(HighCpu, 11);
        Feed(Calm, 11);

        // Act
        Feed(HighCpu, 11);

        // Assert
        _policy.IsStrained.Should().BeTrue();
    }
}
