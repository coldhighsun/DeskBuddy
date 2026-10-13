using AwesomeAssertions;
using DeskBuddy.Domain.Sedentary;
using Microsoft.Extensions.Time.Testing;

namespace DeskBuddy.Domain.Tests.Sedentary;

/// <summary>
/// Tests for <see cref="SedentaryReminderPolicy"/>. Options: remind after 45 min of work, 5 min away counts as a
/// break, snooze for 5 min.
/// </summary>
public class SedentaryReminderPolicyTests
{
    /// <summary>
    /// Time away that counts as a break in the tests.
    /// </summary>
    private static readonly TimeSpan RestThreshold = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Snooze length in the tests.
    /// </summary>
    private static readonly TimeSpan Snooze = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The fake clock shared with the policy under test.
    /// </summary>
    private readonly FakeTimeProvider _clock = new();

    /// <summary>
    /// The policy under test.
    /// </summary>
    private readonly SedentaryReminderPolicy _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="SedentaryReminderPolicyTests"/> class.
    /// </summary>
    public SedentaryReminderPolicyTests()
    {
        _policy = new SedentaryReminderPolicy(new SedentaryOptions(), _clock);
    }

    /// <summary>
    /// Simulates an active user for the given time, polling once per minute, and reports whether a reminder fired.
    /// </summary>
    /// <param name="duration">
    /// How long the user keeps working.
    /// </param>
    /// <returns>
    /// The number of reminders that fired during the period.
    /// </returns>
    private int Work(TimeSpan duration)
    {
        var reminders = 0;
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += TimeSpan.FromMinutes(1))
        {
            if (_policy.Update(TimeSpan.Zero))
            {
                reminders++;
            }

            _clock.Advance(TimeSpan.FromMinutes(1));
        }

        return reminders;
    }

    /// <summary>
    /// No reminder is due while the work streak is still young.
    /// </summary>
    [Fact]
    public void Update_BeforeThreshold_NoReminder()
    {
        // Act
        var reminders = Work(TimeSpan.FromMinutes(44));

        // Assert
        reminders.Should().Be(0);
    }

    /// <summary>
    /// A reminder fires once the user has worked for the threshold.
    /// </summary>
    [Fact]
    public void Update_ThresholdReached_RemindsOnce()
    {
        // Act: the poll at minute 45 is the 46th call.
        var reminders = Work(TimeSpan.FromMinutes(46));

        // Assert
        reminders.Should().Be(1);
    }

    /// <summary>
    /// Continuous work produces a new reminder every threshold, not one per poll.
    /// </summary>
    [Fact]
    public void Update_ContinuousWork_RemindsEveryThreshold()
    {
        // Act: 91 polls at minutes 0..90 -> reminders at 45 and 90.
        var reminders = Work(TimeSpan.FromMinutes(91));

        // Assert
        reminders.Should().Be(2);
    }

    /// <summary>
    /// A real break (away at least the rest threshold) starts the streak over.
    /// </summary>
    [Fact]
    public void Update_LongBreak_ResetsStreak()
    {
        // Arrange
        Work(TimeSpan.FromMinutes(40));
        _policy.Update(RestThreshold);
        _clock.Advance(RestThreshold);

        // Act: another 44 minutes of work after the break is still short of the threshold.
        var reminders = Work(TimeSpan.FromMinutes(44));

        // Assert
        reminders.Should().Be(0);
    }

    /// <summary>
    /// Stepping away for less than the rest threshold does not count as a break.
    /// </summary>
    [Fact]
    public void Update_ShortAbsence_DoesNotResetStreak()
    {
        // Arrange
        Work(TimeSpan.FromMinutes(40));
        _policy.Update(RestThreshold - TimeSpan.FromSeconds(1));

        // Act
        var reminders = Work(TimeSpan.FromMinutes(6));

        // Assert
        reminders.Should().Be(1);
    }

    /// <summary>
    /// Snoozing brings the next reminder forward to the snooze length instead of the full threshold.
    /// </summary>
    [Fact]
    public void Snooze_AfterReminder_RemindsAgainAfterSnoozeDuration()
    {
        // Arrange
        Work(TimeSpan.FromMinutes(46));
        _policy.Snooze();

        // Act
        var beforeSnoozeEnds = Work(Snooze - TimeSpan.FromMinutes(1));
        var afterSnoozeEnds = Work(TimeSpan.FromMinutes(2));

        // Assert
        beforeSnoozeEnds.Should().Be(0);
        afterSnoozeEnds.Should().Be(1);
    }

    /// <summary>
    /// Without snoozing, the next reminder after one fires is a full threshold later.
    /// </summary>
    [Fact]
    public void Update_NoSnooze_NextReminderIsFullThresholdLater()
    {
        // Arrange
        Work(TimeSpan.FromMinutes(46));

        // Act
        var reminders = Work(TimeSpan.FromMinutes(40));

        // Assert
        reminders.Should().Be(0);
    }

    /// <summary>
    /// Snoozing while the user is resting does nothing and does not start a streak.
    /// </summary>
    [Fact]
    public void Snooze_WhileResting_IsIgnored()
    {
        // Act
        _policy.Snooze();

        // Assert
        _policy.StreakDuration.Should().Be(TimeSpan.Zero);
    }

    /// <summary>
    /// The streak duration reports how long the user has been working.
    /// </summary>
    [Fact]
    public void StreakDuration_WhileWorking_ReportsElapsedTime()
    {
        // Arrange
        Work(TimeSpan.FromMinutes(30));

        // Assert
        _policy.StreakDuration.Should().Be(TimeSpan.FromMinutes(30));
    }

    /// <summary>
    /// The streak duration is zero while the user is resting.
    /// </summary>
    [Fact]
    public void StreakDuration_AfterBreak_IsZero()
    {
        // Arrange
        Work(TimeSpan.FromMinutes(30));
        _policy.Update(RestThreshold);

        // Assert
        _policy.StreakDuration.Should().Be(TimeSpan.Zero);
    }

    /// <summary>
    /// The reminder is not due again right after the first poll of a fresh streak.
    /// </summary>
    [Fact]
    public void Update_FirstPollOfStreak_DoesNotRemind()
    {
        // Act
        var due = _policy.Update(TimeSpan.Zero);

        // Assert
        due.Should().BeFalse();
    }
}
