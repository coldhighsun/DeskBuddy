using DeskBuddy.App.Interop;

namespace DeskBuddy.App.Tests.Interop;

/// <summary>
/// Tests for <see cref="Win32Timing"/>. The hook is shared by the whole process, so what the tests record is picked out by
/// the name of a call that only they make.
/// </summary>
public sealed class Win32TimingTests : IDisposable
{
    /// <summary>
    /// The name of the call the tests time.
    /// </summary>
    private const string CallName = "Win32TimingTests.Call";

    /// <summary>
    /// What was reported for <see cref="CallName"/>.
    /// </summary>
    private readonly List<TimeSpan> _reported = [];

    /// <summary>
    /// What the hook was before the test, put back afterwards.
    /// </summary>
    private readonly Action<string, TimeSpan>? _previous = Win32Timing.Timed;

    /// <summary>
    /// Puts the hook back.
    /// </summary>
    public void Dispose()
    {
        Win32Timing.Timed = _previous;
    }

    /// <summary>
    /// A call that was timed is reported by name with the time it took.
    /// </summary>
    [Fact]
    public void Stop_HookSet_ReportsTheCallAndHowLongItTook()
    {
        // Arrange
        Win32Timing.Timed = (call, took) =>
        {
            if (call == CallName)
            {
                lock (_reported)
                {
                    _reported.Add(took);
                }
            }
        };

        // Act
        var startedAt = Win32Timing.Start();
        Thread.Sleep(20);
        Win32Timing.Stop(CallName, startedAt);

        // Assert
        _reported.Should().ContainSingle().Which.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(15));
    }

    /// <summary>
    /// Without a hook there is nothing to report to and nothing goes wrong.
    /// </summary>
    [Fact]
    public void Stop_NoHook_DoesNothing()
    {
        // Arrange
        Win32Timing.Timed = null;

        // Act
        var act = () => Win32Timing.Stop(CallName, Win32Timing.Start());

        // Assert
        act.Should().NotThrow();
    }
}
