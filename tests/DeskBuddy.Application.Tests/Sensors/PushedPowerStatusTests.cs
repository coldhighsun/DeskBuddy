using DeskBuddy.Domain.Power;

namespace DeskBuddy.Application.Tests.Sensors;

/// <summary>
/// Tests for <see cref="PushedPowerStatus"/>.
/// </summary>
public class PushedPowerStatusTests
{
    /// <summary>
    /// How long a pushed status is trusted, in the tests.
    /// </summary>
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The fake time source.
    /// </summary>
    private readonly FakeTimeProvider _time = new();

    /// <summary>
    /// The fake power supply that is read.
    /// </summary>
    private readonly FakePowerStatusProvider _reader = new();

    /// <summary>
    /// Creates the provider under test over the fake power supply.
    /// </summary>
    /// <returns>The provider.</returns>
    private PushedPowerStatus Create()
    {
        return new PushedPowerStatus(_reader, _time, MaxAge);
    }

    /// <summary>
    /// Before the first push every question is answered by a reading.
    /// </summary>
    [Fact]
    public void GetStatus_BeforeFirstPush_ReadsEveryTime()
    {
        // Arrange
        _reader.Status = new PowerStatus(true, 80, true);
        var power = Create();

        // Act
        power.GetStatus();
        _reader.Status = new PowerStatus(true, 79, false);
        var status = power.GetStatus();

        // Assert
        status.Should().Be(new PowerStatus(true, 79, false));
        _reader.QueryCount.Should().Be(2);
    }

    /// <summary>
    /// After a push the pushed status is the answer, however often it is asked, with nothing read.
    /// </summary>
    [Fact]
    public void GetStatus_AfterPush_AnswersPushedWithoutReading()
    {
        // Arrange
        var power = Create();
        power.Push(new PowerStatus(true, 15, false));

        // Act
        _time.Advance(TimeSpan.FromSeconds(30));
        var first = power.GetStatus();
        var second = power.GetStatus();

        // Assert
        first.Should().Be(new PowerStatus(true, 15, false));
        second.Should().Be(first);
        _reader.QueryCount.Should().Be(0);
    }

    /// <summary>
    /// A newer push replaces the one before.
    /// </summary>
    [Fact]
    public void GetStatus_AfterSecondPush_AnswersTheNewer()
    {
        // Arrange
        var power = Create();
        power.Push(new PowerStatus(true, 15, false));

        // Act
        power.Push(new PowerStatus(true, 16, true));

        // Assert
        power.GetStatus().Should().Be(new PowerStatus(true, 16, true));
    }

    /// <summary>
    /// A push that arrives while an old status is being read again is newer than that reading and wins over it, now and
    /// for the questions after it.
    /// </summary>
    [Fact]
    public void GetStatus_WhenPushArrivesDuringReading_KeepsThePush()
    {
        // Arrange
        var unplugged = new PowerStatus(true, 40, false);
        var plugged = new PowerStatus(true, 40, true);
        PushedPowerStatus? power = null;
        var reads = 0;
        var reader = new CallbackPower(() =>
        {
            reads++;
            power!.Push(plugged);
            return unplugged;
        });
        power = new PushedPowerStatus(reader, _time, MaxAge);
        power.Push(unplugged);
        _time.Advance(MaxAge + TimeSpan.FromSeconds(1));

        // Act
        var during = power.GetStatus();
        var after = power.GetStatus();

        // Assert
        during.Should().Be(plugged);
        after.Should().Be(plugged);
        reads.Should().Be(1);
    }

    /// <summary>
    /// A power supply whose reading runs a callback, to do something while the reading is under way.
    /// </summary>
    /// <param name="read">What a reading does and returns.</param>
    private sealed class CallbackPower(Func<PowerStatus> read) : IPowerStatusProvider
    {
        /// <inheritdoc />
        public PowerStatus GetStatus()
        {
            return read();
        }
    }

    /// <summary>
    /// A pushed status that is too old is read again, as the system does not announce every percent of charge, and the new
    /// reading is trusted for a while in turn.
    /// </summary>
    [Fact]
    public void GetStatus_WhenPushedStatusIsTooOld_ReadsAgainOnce()
    {
        // Arrange
        var power = Create();
        power.Push(new PowerStatus(true, 15, false));
        _reader.Status = new PowerStatus(true, 11, false);

        // Act
        _time.Advance(MaxAge + TimeSpan.FromSeconds(1));
        var stale = power.GetStatus();
        var after = power.GetStatus();

        // Assert
        stale.Should().Be(new PowerStatus(true, 11, false));
        after.Should().Be(stale);
        _reader.QueryCount.Should().Be(1);
    }
}
