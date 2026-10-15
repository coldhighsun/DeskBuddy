namespace DeskBuddy.Domain.Tests.Activity;

/// <summary>
/// Tests for <see cref="GitSyncPolicy"/>.
/// </summary>
public class GitSyncPolicyTests
{
    /// <summary>
    /// The options under test.
    /// </summary>
    private readonly PetBehaviorOptions _options = new();

    /// <summary>
    /// The fake clock shared by the policy under test.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The policy under test.
    /// </summary>
    private readonly GitSyncPolicy _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitSyncPolicyTests"/> class.
    /// </summary>
    public GitSyncPolicyTests()
    {
        _policy = new GitSyncPolicy(_options, _clock);
    }

    /// <summary>
    /// Without a git helper running nothing is reported.
    /// </summary>
    [Fact]
    public void Update_NoHelper_ReportsNothing()
    {
        // Act
        var result = _policy.Update(false);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// After a reset a helper that is already running counts as a new start when the readings resume.
    /// </summary>
    [Fact]
    public void Reset_HelperAlreadyRunning_IsNotRememberedAsRunning()
    {
        // Arrange
        _policy.Update(true);
        _clock.Advance(_options.GitCooldown + TimeSpan.FromSeconds(1));

        // Act
        _policy.Reset();
        var afterReset = _policy.Update(true);

        // Assert
        afterReset.Should().BeTrue();
    }

    /// <summary>
    /// A git helper appearing is reported once, not on every update while it runs.
    /// </summary>
    [Fact]
    public void Update_HelperAppears_ReportsOnlyTheStart()
    {
        // Act
        var first = _policy.Update(true);
        var second = _policy.Update(true);

        // Assert
        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    /// <summary>
    /// A new burst of git traffic right after the last one is held back by the cooldown.
    /// </summary>
    [Fact]
    public void Update_AnotherStartWithinCooldown_IsIgnored()
    {
        // Arrange
        _policy.Update(true);
        _policy.Update(false);
        _clock.Advance(_options.GitCooldown - TimeSpan.FromSeconds(1));

        // Act
        var result = _policy.Update(true);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Once the cooldown has passed, the next burst is reported.
    /// </summary>
    [Fact]
    public void Update_AnotherStartAfterCooldown_IsReported()
    {
        // Arrange
        _policy.Update(true);
        _policy.Update(false);
        _clock.Advance(_options.GitCooldown);

        // Act
        var result = _policy.Update(true);

        // Assert
        result.Should().BeTrue();
    }
}
