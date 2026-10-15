using DeskBuddy.Domain.Power;

namespace DeskBuddy.Application.Tests;

/// <summary>
/// Power status stub with a settable value that counts how often it is asked. By default the computer has no battery.
/// </summary>
internal sealed class FakePowerStatusProvider : IPowerStatusProvider
{
    /// <summary>
    /// Gets or sets the status to report.
    /// </summary>
    public PowerStatus Status { get; set; }

    /// <summary>
    /// Gets how many times the status was read.
    /// </summary>
    public int QueryCount { get; private set; }

    /// <inheritdoc />
    public PowerStatus GetStatus()
    {
        QueryCount++;
        return Status;
    }
}
