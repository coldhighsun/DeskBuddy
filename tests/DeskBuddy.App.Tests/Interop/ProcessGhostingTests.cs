using DeskBuddy.App.Interop;

namespace DeskBuddy.App.Tests.Interop;

/// <summary>
/// Tests for <see cref="ProcessGhosting"/>. The call to Windows is replaced, so the test process keeps its ghosting.
/// </summary>
public sealed class ProcessGhostingTests
{
    /// <summary>
    /// Ghosting is turned off by running the call once.
    /// </summary>
    [Fact]
    public void TryDisable_CallSucceeds_ReturnsTrueAndCallsOnce()
    {
        // Arrange
        var calls = 0;

        // Act
        var result = ProcessGhosting.TryDisable(NullLogger.Instance, () => calls++);

        // Assert
        result.Should().BeTrue();
        calls.Should().Be(1);
    }

    /// <summary>
    /// A Windows without the call is a logged failure, not an exception.
    /// </summary>
    [Fact]
    public void TryDisable_EntryPointMissing_ReturnsFalse()
    {
        // Arrange
        void Missing()
        {
            throw new EntryPointNotFoundException();
        }

        // Act
        var result = ProcessGhosting.TryDisable(NullLogger.Instance, Missing);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// An error that is not about the call being missing is not swallowed.
    /// </summary>
    [Fact]
    public void TryDisable_OtherError_IsNotCaught()
    {
        // Arrange
        void Broken()
        {
            throw new InvalidOperationException();
        }

        // Act
        var act = () => ProcessGhosting.TryDisable(NullLogger.Instance, Broken);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
