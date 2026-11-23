namespace DeskBuddy.Application.Tests.Sensors;

/// <summary>
/// Tests for <see cref="BackgroundSensor{T}"/> and the adapters that put it behind the sensor interfaces.
/// </summary>
public class BackgroundSensorTests
{
    /// <summary>
    /// The fake time source.
    /// </summary>
    private readonly FakeTimeProvider _time = new();

    /// <summary>
    /// The readings that were started and not yet run, for the tests that decide when a reading finishes.
    /// </summary>
    private readonly Queue<Action> _pending = new();

    /// <summary>
    /// How many times the fake system was read.
    /// </summary>
    private int _reads;

    /// <summary>
    /// What the fake system answers.
    /// </summary>
    private string _answer = "first";

    /// <summary>
    /// The first call does not wait for the system and answers with the initial value.
    /// </summary>
    [Fact]
    public void GetValue_FirstCall_AnswersInitialWithoutReading()
    {
        // Arrange
        var sensor = Create(queue: true);

        // Act
        var value = sensor.GetValue();

        // Assert
        value.Should().Be("initial");
        _reads.Should().Be(0);
        _pending.Should().HaveCount(1);
    }

    /// <summary>
    /// Once the reading is done its result is what the next call answers.
    /// </summary>
    [Fact]
    public void GetValue_AfterReadingFinished_AnswersTheReading()
    {
        // Arrange
        var sensor = Create(queue: true);
        sensor.GetValue();
        _pending.Dequeue()();

        // Act
        var value = sensor.GetValue();

        // Assert
        value.Should().Be("first");
    }

    /// <summary>
    /// A reading that is still under way is waited for, not started a second time, however often it is asked.
    /// </summary>
    [Fact]
    public void GetValue_WhileReadingUnderWay_StartsNoSecondReading()
    {
        // Arrange
        var sensor = Create(queue: true);
        sensor.GetValue();

        // Act
        _time.Advance(TimeSpan.FromMinutes(1));
        var value = sensor.GetValue();
        sensor.GetValue();

        // Assert
        value.Should().Be("initial");
        _pending.Should().HaveCount(1);
    }

    /// <summary>
    /// Within the interval the last reading is reused; after it a new one starts, and the old answer is still given.
    /// </summary>
    [Fact]
    public void GetValue_AfterInterval_StartsNewReadingAndAnswersOldValue()
    {
        // Arrange
        var sensor = Create(queue: false);
        sensor.GetValue();
        _answer = "second";

        // Act
        _time.Advance(TimeSpan.FromSeconds(1));
        var within = sensor.GetValue();
        _time.Advance(TimeSpan.FromSeconds(2));
        var after = sensor.GetValue();
        var later = sensor.GetValue();

        // Assert
        within.Should().Be("first");
        after.Should().Be("first");
        later.Should().Be("second");
        _reads.Should().Be(2);
    }

    /// <summary>
    /// A reading that throws leaves the last answer in place, is reported, and lets the next reading start.
    /// </summary>
    [Fact]
    public void GetValue_WhenReadingThrows_KeepsLastValueAndReportsFailure()
    {
        // Arrange
        var failures = new List<(string Name, Exception Error)>();
        var diagnostics = new SensorDiagnostics((_, _) => { }, (name, error) => failures.Add((name, error)));
        var fail = false;
        var sensor = new BackgroundSensor<string>(
            "probe",
            () => fail ? throw new InvalidOperationException("boom") : "ok",
            "initial",
            TimeSpan.FromSeconds(2),
            _time,
            diagnostics,
            action => action());
        sensor.GetValue();
        fail = true;
        _time.Advance(TimeSpan.FromSeconds(3));
        sensor.GetValue();
        fail = false;
        _time.Advance(TimeSpan.FromSeconds(3));

        // Act
        var value = sensor.GetValue();
        var afterRecovery = sensor.GetValue();

        // Assert
        value.Should().Be("ok");
        afterRecovery.Should().Be("ok");
        failures.Should().ContainSingle().Which.Name.Should().Be("probe");
        failures[0].Error.Message.Should().Be("boom");
    }

    /// <summary>
    /// Every reading is timed under the name of the sensor.
    /// </summary>
    [Fact]
    public void GetValue_ReportsHowLongTheReadingTook()
    {
        // Arrange
        var timings = new List<string>();
        var diagnostics = new SensorDiagnostics((name, _) => timings.Add(name), (_, _) => { });
        var sensor = new BackgroundSensor<string>(
            "probe",
            () => "ok",
            "initial",
            TimeSpan.FromSeconds(2),
            _time,
            diagnostics,
            action => action());

        // Act
        sensor.GetValue();

        // Assert
        timings.Should().Equal("probe");
    }

    /// <summary>
    /// The thread pool runs the reading when no runner is given, and the answer arrives without the caller waiting.
    /// </summary>
    [Fact]
    public void GetValue_OnThreadPool_ReadsOffTheCallingThread()
    {
        // Arrange
        var readerThread = new TaskCompletionSource<int>();
        var sensor = new BackgroundSensor<string>(
            "probe",
            () =>
            {
                readerThread.TrySetResult(Environment.CurrentManagedThreadId);
                return "ok";
            },
            "initial",
            TimeSpan.FromSeconds(2),
            _time);
        var callingThread = 0;
        string? first = null;
        var reader = 0;

        // Act
        // A thread of its own that waits for the reading: a thread pool thread that returned to the pool could pick up the
        // reading itself, which would make the two ids equal without the caller ever having read.
        var caller = new Thread(() =>
        {
            callingThread = Environment.CurrentManagedThreadId;
            first = sensor.GetValue();
            reader = readerThread.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        });
        caller.Start();
        caller.Join();

        // Assert
        first.Should().Be("initial");
        reader.Should().NotBe(callingThread);
    }

    /// <summary>
    /// The adapters hand the sensor's answer on as the answer of their interface.
    /// </summary>
    [Fact]
    public void Adapters_AnswerTheLastReading()
    {
        // Arrange
        var level = new BackgroundSensor<DoNotDisturbLevel>(
            "level", () => DoNotDisturbLevel.Fullscreen, DoNotDisturbLevel.None, TimeSpan.Zero, _time, null, action => action());
        var app = new BackgroundSensor<string?>(
            "app", () => "Code", null, TimeSpan.Zero, _time, null, action => action());
        var names = new BackgroundSensor<IReadOnlySet<string>>(
            "names", () => new HashSet<string> { "git" }, new HashSet<string>(), TimeSpan.Zero, _time, null, action => action());
        var fullscreen = new BackgroundFullscreenDetector(level);
        var foreground = new BackgroundForegroundApp(app);
        var processes = new BackgroundProcessList(names);
        fullscreen.GetLevel();
        foreground.GetForegroundProcessName();
        processes.GetNames();

        // Act
        var results = (fullscreen.GetLevel(), foreground.GetForegroundProcessName(), processes.GetNames());

        // Assert
        results.Item1.Should().Be(DoNotDisturbLevel.Fullscreen);
        results.Item2.Should().Be("Code");
        results.Item3.Should().Contain("git");
    }

    /// <summary>
    /// A refresh reads at once, whatever the interval, and the next question gets the new answer.
    /// </summary>
    [Fact]
    public void Refresh_WithinInterval_ReadsAtOnce()
    {
        // Arrange
        var sensor = Create(queue: false);
        sensor.GetValue();
        _answer = "second";

        // Act
        sensor.Refresh();
        var value = sensor.GetValue();

        // Assert
        value.Should().Be("second");
        _reads.Should().Be(2);
    }

    /// <summary>
    /// A refresh asked for while a reading is under way is not started next to it, but followed by one more reading, as the
    /// one under way may have started before the reason for the refresh.
    /// </summary>
    [Fact]
    public void Refresh_WhileReadingUnderWay_ReadsOnceMoreAfterwards()
    {
        // Arrange
        var sensor = Create(queue: true);
        sensor.GetValue();

        // Act
        sensor.Refresh();
        sensor.Refresh();
        var queued = _pending.Count;
        _pending.Dequeue()();

        // Assert
        queued.Should().Be(1);
        _reads.Should().Be(2);
        _pending.Should().BeEmpty();
        sensor.GetValue().Should().Be("first");
    }

    /// <summary>
    /// The change is announced when a reading differs from the one before, not when it is the same.
    /// </summary>
    [Fact]
    public void Changed_IsRaisedOnlyWhenTheReadingDiffers()
    {
        // Arrange
        var sensor = Create(queue: false);
        var raised = 0;
        sensor.Changed += (_, _) => raised++;

        // Act
        sensor.GetValue();
        var afterFirst = raised;
        _time.Advance(TimeSpan.FromSeconds(3));
        sensor.GetValue();
        var afterSame = raised;
        _answer = "second";
        _time.Advance(TimeSpan.FromSeconds(3));
        sensor.GetValue();

        // Assert
        afterFirst.Should().Be(1);
        afterSame.Should().Be(1);
        raised.Should().Be(2);
    }

    /// <summary>
    /// A listener that throws is reported, and does not leave the sensor stuck in a reading that never ends.
    /// </summary>
    [Fact]
    public void Changed_WhenListenerThrows_SensorKeepsReading()
    {
        // Arrange
        var failures = new List<string>();
        var diagnostics = new SensorDiagnostics((_, _) => { }, (name, _) => failures.Add(name));
        var value = "a";
        var sensor = new BackgroundSensor<string>(
            "probe", () => value, "initial", TimeSpan.FromSeconds(2), _time, diagnostics, action => action());
        sensor.Changed += (_, _) => throw new InvalidOperationException("listener");
        sensor.GetValue();
        value = "b";

        // Act
        sensor.Refresh();

        // Assert
        sensor.GetValue().Should().Be("b");
        failures.Should().Equal("probe", "probe");
    }

    /// <summary>
    /// The full-screen adapter passes a refresh on to its sensor and its change notices on to its listeners.
    /// </summary>
    [Fact]
    public void BackgroundFullscreenDetector_PassesRefreshAndChangedOn()
    {
        // Arrange
        var level = DoNotDisturbLevel.None;
        var sensor = new BackgroundSensor<DoNotDisturbLevel>(
            "level", () => level, DoNotDisturbLevel.None, TimeSpan.FromMinutes(1), _time, null, action => action());
        var detector = new BackgroundFullscreenDetector(sensor);
        var raised = 0;
        detector.Changed += (_, _) => raised++;
        detector.GetLevel();
        level = DoNotDisturbLevel.Fullscreen;

        // Act
        detector.Refresh();

        // Assert
        detector.GetLevel().Should().Be(DoNotDisturbLevel.Fullscreen);
        raised.Should().Be(1);
    }

    /// <summary>
    /// Creates a sensor over the fake system.
    /// </summary>
    /// <param name="queue">Whether readings wait in <see cref="_pending"/> instead of running at once.</param>
    /// <returns>The sensor, with a two-second interval.</returns>
    private BackgroundSensor<string> Create(bool queue)
    {
        return new BackgroundSensor<string>(
            "probe",
            () =>
            {
                _reads++;
                return _answer;
            },
            "initial",
            TimeSpan.FromSeconds(2),
            _time,
            null,
            queue ? _pending.Enqueue : action => action());
    }
}
