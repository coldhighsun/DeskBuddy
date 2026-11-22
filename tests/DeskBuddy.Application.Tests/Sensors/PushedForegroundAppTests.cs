namespace DeskBuddy.Application.Tests.Sensors;

/// <summary>
/// Tests for <see cref="PushedForegroundApp"/>.
/// </summary>
public class PushedForegroundAppTests
{
    /// <summary>
    /// The detector that answers while nothing was pushed.
    /// </summary>
    private readonly FakeForegroundApp _fallback = new() { ProcessName = "polled" };

    /// <summary>
    /// Before the first push the question goes to the fallback.
    /// </summary>
    [Fact]
    public void GetForegroundProcessName_BeforeFirstPush_AsksFallback()
    {
        // Arrange
        var app = new PushedForegroundApp(_fallback);

        // Act
        var name = app.GetForegroundProcessName();

        // Assert
        name.Should().Be("polled");
        _fallback.QueryCount.Should().Be(1);
    }

    /// <summary>
    /// After a push the pushed program is the answer and the fallback is no longer asked.
    /// </summary>
    [Fact]
    public void GetForegroundProcessName_AfterPush_AnswersPushedAndSkipsFallback()
    {
        // Arrange
        var app = new PushedForegroundApp(_fallback);
        app.Push("Code");

        // Act
        var name = app.GetForegroundProcessName();

        // Assert
        name.Should().Be("Code");
        _fallback.QueryCount.Should().Be(0);
    }

    /// <summary>
    /// Every push replaces the one before, so the answer is always the program that came to the front last.
    /// </summary>
    [Fact]
    public void GetForegroundProcessName_AfterSeveralPushes_AnswersTheLast()
    {
        // Arrange
        var app = new PushedForegroundApp(_fallback);
        app.Push("Code");

        // Act
        app.Push("chrome");

        // Assert
        app.GetForegroundProcessName().Should().Be("chrome");
    }

    /// <summary>
    /// Every push announces itself, after the new program is known, even when the program is the same one again.
    /// </summary>
    [Fact]
    public void Push_RaisesChangedAfterStoring()
    {
        // Arrange
        var app = new PushedForegroundApp(_fallback);
        var seen = new List<string?>();
        app.Changed += (_, _) => seen.Add(app.GetForegroundProcessName());

        // Act
        app.Push("Code");
        app.Push("Code");

        // Assert
        seen.Should().Equal("Code", "Code");
    }

    /// <summary>
    /// A push of nothing (no window in front, or a program that cannot be read) is an answer too, not a reason to poll.
    /// </summary>
    [Fact]
    public void GetForegroundProcessName_AfterPushOfNull_AnswersNullWithoutFallback()
    {
        // Arrange
        var app = new PushedForegroundApp(_fallback);
        app.Push("Code");
        app.Push(null);

        // Act
        var name = app.GetForegroundProcessName();

        // Assert
        name.Should().BeNull();
        _fallback.QueryCount.Should().Be(0);
    }
}
