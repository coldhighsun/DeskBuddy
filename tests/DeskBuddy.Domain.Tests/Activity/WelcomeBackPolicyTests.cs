namespace DeskBuddy.Domain.Tests.Activity;

/// <summary>
/// Tests for <see cref="WelcomeBackPolicy"/>.
/// </summary>
public class WelcomeBackPolicyTests
{
    /// <summary>
    /// The options under test.
    /// </summary>
    private readonly PetBehaviorOptions _options = new();

    /// <summary>
    /// The policy under test.
    /// </summary>
    private readonly WelcomeBackPolicy _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="WelcomeBackPolicyTests"/> class.
    /// </summary>
    public WelcomeBackPolicyTests()
    {
        _policy = new WelcomeBackPolicy(_options);
    }

    /// <summary>
    /// A user who is simply working is not welcomed.
    /// </summary>
    [Fact]
    public void Update_UserActive_ReturnsFalse()
    {
        // Act
        var result = _policy.Update(TimeSpan.Zero);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// After a reset a user who was away is forgotten, so coming back is not welcomed.
    /// </summary>
    [Fact]
    public void Reset_WhileAway_ForgetsTheAbsence()
    {
        // Arrange
        _policy.Update(_options.WelcomeAwayAfter + TimeSpan.FromMinutes(20));

        // Act
        _policy.Reset();
        var back = _policy.Update(TimeSpan.FromSeconds(1));

        // Assert
        back.Should().BeFalse();
    }

    /// <summary>
    /// Coming back after a long absence is welcomed, once.
    /// </summary>
    [Fact]
    public void Update_BackAfterLongAbsence_WelcomesOnce()
    {
        // Arrange
        _policy.Update(_options.WelcomeAwayAfter);
        _policy.Update(_options.WelcomeAwayAfter + TimeSpan.FromMinutes(20));

        // Act
        var back = _policy.Update(TimeSpan.FromSeconds(1));
        var later = _policy.Update(TimeSpan.FromSeconds(2));

        // Assert
        back.Should().BeTrue();
        later.Should().BeFalse();
    }

    /// <summary>
    /// A short break is not worth a welcome.
    /// </summary>
    [Fact]
    public void Update_BackAfterShortBreak_IsNotWelcomed()
    {
        // Arrange
        _policy.Update(_options.WelcomeAwayAfter - TimeSpan.FromSeconds(1));

        // Act
        var result = _policy.Update(TimeSpan.FromSeconds(1));

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// A second long absence is welcomed again.
    /// </summary>
    [Fact]
    public void Update_SecondAbsence_WelcomesAgain()
    {
        // Arrange
        _policy.Update(_options.WelcomeAwayAfter);
        _policy.Update(TimeSpan.Zero);
        _policy.Update(_options.WelcomeAwayAfter);

        // Act
        var result = _policy.Update(TimeSpan.Zero);

        // Assert
        result.Should().BeTrue();
    }
}
