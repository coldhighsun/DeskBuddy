using DeskBuddy.Domain.Chatter;
using DeskBuddy.Domain.Messages;
using DeskBuddy.Domain.Schedule;
using DeskBuddy.Domain.Power;
using DeskBuddy.Domain.Sedentary;
using DeskBuddy.Domain.SystemLoad;
using DeskBuddy.Domain.Visibility;

namespace DeskBuddy.Application.Tests;

/// <content>
/// Situations the pet acts out: coding, meetings, chat, battery, lunch and the like.
/// </content>
public partial class PetBrainTests
{
    /// <summary>
    /// A development tool in front while the user is active makes the pet sit down to code.
    /// </summary>
    [Fact]
    public void Tick_DevelopmentToolInFront_PetCodes()
    {
        // Arrange
        _foreground.ProcessName = "Code";

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().Be(PetState.Coding);
    }

    /// <summary>
    /// An application that is not a development tool does not make the pet code.
    /// </summary>
    [Fact]
    public void Tick_OtherApplicationInFront_PetDoesNotCode()
    {
        // Arrange
        _foreground.ProcessName = "chrome";

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().NotBe(PetState.Coding);
    }

    /// <summary>
    /// A meeting application in front makes the pet keep company in a headset.
    /// </summary>
    [Fact]
    public void Tick_MeetingApplicationInFront_PetIsInMeeting()
    {
        // Arrange
        _foreground.ProcessName = "Zoom";

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().Be(PetState.Meeting);
    }

    /// <summary>
    /// The pet keeps quiet during a meeting, however long it waits.
    /// </summary>
    [Fact]
    public void Tick_Meeting_NoChatter()
    {
        // Arrange
        _foreground.ProcessName = "Zoom";

        // Act
        var message = RunUntilChatter(60);

        // Assert
        message.Should().BeNull();
    }

    /// <summary>
    /// A chat application in front puts the pet into chatting.
    /// </summary>
    [Fact]
    public void Tick_ChatApplicationInFront_PetIsChatting()
    {
        // Arrange
        _foreground.ProcessName = "Weixin";

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().Be(PetState.Chatting);
    }

    /// <summary>
    /// The pet keeps quiet while the user chats, however long it waits.
    /// </summary>
    [Fact]
    public void Tick_Chatting_NoChatter()
    {
        // Arrange
        _foreground.ProcessName = "Weixin";

        // Act
        var message = RunUntilChatter(60);

        // Assert
        message.Should().BeNull();
    }

    /// <summary>
    /// A meeting outranks a running build: the pet keeps still and quiet rather than showing the loading ring.
    /// </summary>
    [Fact]
    public void Tick_MeetingWhileBuilding_MeetingWins()
    {
        // Arrange
        _foreground.ProcessName = "Zoom";
        _processes.Names = new HashSet<string>(["msbuild"], StringComparer.OrdinalIgnoreCase);
        _load.Snapshot = new SystemLoadSnapshot(95, 40);
        PetState state = default;

        // Act
        for (var second = 0; second < 8; second++)
        {
            state = _brain.Tick();
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        // Assert
        state.Should().Be(PetState.Meeting);
    }

    /// <summary>
    /// The user coming back after a long absence is welcomed: the pet wakes up waving and says so.
    /// </summary>
    [Fact]
    public void Tick_BackAfterLongAbsence_PetWelcomesTheUser()
    {
        // Arrange
        _idle.IdleTime = TimeSpan.FromMinutes(45);
        _brain.Tick().Should().Be(PetState.Sleep);
        _clock.Advance(TimeSpan.FromSeconds(1));
        _idle.IdleTime = TimeSpan.FromSeconds(1);

        // Act
        var state = _brain.Tick();
        _clock.Advance(TimeSpan.FromSeconds(1));
        _brain.Tick();

        // Assert
        state.Should().Be(PetState.Welcome);
        _brain.CurrentMessage.Should().NotBeNull();
        _brain.CurrentMessage!.Kind.Should().Be(PetMessageKind.Remark);
        _brain.CurrentMessage.Count.Should().Be((int)PetState.Welcome);
    }

    /// <summary>
    /// A low battery makes the pet show an empty battery, and plugging in makes it charge up.
    /// </summary>
    [Fact]
    public void Tick_BatteryLowThenPluggedIn_PetShowsLowBatteryThenCharges()
    {
        // Arrange
        _power.Status = new PowerStatus(true, 10, false);
        var low = _brain.Tick();
        _clock.Advance(TimeSpan.FromSeconds(1));
        _power.Status = new PowerStatus(true, 10, true);

        // Act
        var charging = _brain.Tick();

        // Assert
        low.Should().Be(PetState.LowBattery);
        charging.Should().Be(PetState.Charging);
    }

    /// <summary>
    /// A state the pet is made to act out is shown for its duration.
    /// </summary>
    [Fact]
    public void ActOut_State_PetShowsItThenCarriesOn()
    {
        // Arrange
        _brain.ActOut(PetState.Focus, TimeSpan.FromSeconds(3));

        // Act
        var during = _brain.Tick();
        _clock.Advance(TimeSpan.FromSeconds(3));
        var after = _brain.Tick();

        // Assert
        during.Should().Be(PetState.Focus);
        after.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// A remark appears in the speech bubble, and unlike a reminder it does not make the pet call for attention.
    /// </summary>
    [Fact]
    public void Say_PetOnScreen_ShowsRemarkWithoutAlerting()
    {
        // Arrange
        _brain.Tick();

        // Act
        var queued = _brain.Say(PetState.Coding);
        var state = _brain.Tick();

        // Assert
        queued.Should().BeTrue();
        _brain.CurrentMessage.Should().NotBeNull();
        _brain.CurrentMessage!.Kind.Should().Be(PetMessageKind.Remark);
        _brain.CurrentMessage.Count.Should().Be((int)PetState.Coding);
        state.Should().NotBe(PetState.Alert);
    }

    /// <summary>
    /// A remark is dropped, not saved for later, while the user must not be disturbed.
    /// </summary>
    [Fact]
    public void Say_DoNotDisturb_IsDropped()
    {
        // Arrange
        _fullscreen.DoNotDisturb = true;
        _brain.Tick();

        // Act
        var queued = _brain.Say(PetState.Coding);
        _fullscreen.DoNotDisturb = false;
        _brain.Tick();

        // Assert
        queued.Should().BeFalse();
        _brain.CurrentMessage.Should().BeNull();
    }

    /// <summary>
    /// At lunch time the pet acts out a meal and says the remark of lunch, not of dinner.
    /// </summary>
    [Fact]
    public void Tick_LunchTime_ActsOutMealWithLunchRemark()
    {
        // Arrange
        UseNoonOnAWeekday();

        // Act
        var state = _brain.Tick();

        // Assert
        state.Should().Be(PetState.Meal);
        _brain.CurrentMessage!.Kind.Should().Be(PetMessageKind.Remark);
        _brain.CurrentMessage.Count.Should().Be(RemarkCode.Encode(PetState.Meal, ScheduleClock.Lunch));
    }

    /// <summary>
    /// A pet that is being dragged does not use up the occasion; it comes once the pet is put down.
    /// </summary>
    [Fact]
    public void Tick_LunchTimeWhileDragging_MealComesAfterTheDrop()
    {
        // Arrange
        UseNoonOnAWeekday();
        _brain.OnDragStarted();

        // Act
        var whileDragging = _brain.Tick();
        _brain.OnDragEnded();
        var afterDrop = _brain.Tick();

        // Assert
        whileDragging.Should().Be(PetState.Drag);
        afterDrop.Should().Be(PetState.Meal);
    }

    /// <summary>
    /// A pet that is falling does not use up the occasion either; it comes once the pet has landed.
    /// </summary>
    [Fact]
    public void Tick_LunchTimeWhileFalling_MealComesAfterTheLanding()
    {
        // Arrange
        UseNoonOnAWeekday();
        _brain.OnFallStarted();

        // Act
        var whileFalling = _brain.Tick();
        _brain.OnFallEnded();
        var afterLanding = _brain.Tick();

        // Assert
        whileFalling.Should().Be(PetState.Fall);
        afterLanding.Should().Be(PetState.Meal);
    }

    /// <summary>
    /// A pet gliding down under its umbrella is not disturbed either: the occasion comes once the pet has landed.
    /// </summary>
    [Fact]
    public void Tick_LunchTimeWhileGliding_MealComesAfterTheLanding()
    {
        // Arrange
        UseNoonOnAWeekday();
        _brain.OnFallStarted();
        _brain.OnFallOpened();

        // Act
        var whileGliding = _brain.Tick();
        _brain.OnFallEnded();
        var afterLanding = _brain.Tick();

        // Assert
        whileGliding.Should().Be(PetState.Glide);
        afterLanding.Should().Be(PetState.Meal);
    }
}
