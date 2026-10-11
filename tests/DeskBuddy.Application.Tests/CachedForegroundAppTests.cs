using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Application.Tests;

/// <summary>
/// Tests for <see cref="CachedForegroundApp"/> and <see cref="ThrottledProcessList"/>.
/// </summary>
public class CachedForegroundAppTests
{
    /// <summary>
    /// The fake time source.
    /// </summary>
    private readonly FakeTimeProvider _time = new();

    /// <summary>
    /// Asking again within the lifetime does not query again, and the answer is the same.
    /// </summary>
    [Fact]
    public void GetForegroundProcessName_WithinLifetime_QueriesOnce()
    {
        // Arrange
        var inner = new FakeForegroundApp { ProcessName = "Code" };
        var cached = new CachedForegroundApp(inner, _time, TimeSpan.FromMilliseconds(500));

        // Act
        var first = cached.GetForegroundProcessName();
        inner.ProcessName = "chrome";
        _time.Advance(TimeSpan.FromMilliseconds(100));
        var second = cached.GetForegroundProcessName();

        // Assert
        first.Should().Be("Code");
        second.Should().Be("Code");
        inner.QueryCount.Should().Be(1);
    }

    /// <summary>
    /// After the lifetime the application is looked up again.
    /// </summary>
    [Fact]
    public void GetForegroundProcessName_AfterLifetime_QueriesAgain()
    {
        // Arrange
        var inner = new FakeForegroundApp { ProcessName = "Code" };
        var cached = new CachedForegroundApp(inner, _time, TimeSpan.FromMilliseconds(500));
        cached.GetForegroundProcessName();
        inner.ProcessName = "chrome";

        // Act
        _time.Advance(TimeSpan.FromSeconds(1));
        var name = cached.GetForegroundProcessName();

        // Assert
        name.Should().Be("chrome");
        inner.QueryCount.Should().Be(2);
    }

    /// <summary>
    /// The process list is only walked once per interval.
    /// </summary>
    [Fact]
    public void GetNames_WithinInterval_QueriesOnce()
    {
        // Arrange
        var inner = new FakeProcessList { Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "git" } };
        var throttled = new ThrottledProcessList(inner, _time, TimeSpan.FromSeconds(2));

        // Act
        var first = throttled.GetNames();
        _time.Advance(TimeSpan.FromSeconds(1));
        var second = throttled.GetNames();
        _time.Advance(TimeSpan.FromSeconds(2));
        var third = throttled.GetNames();

        // Assert
        first.Should().Contain("git");
        second.Should().BeSameAs(first);
        third.Should().Contain("git");
        inner.QueryCount.Should().Be(2);
    }
}
