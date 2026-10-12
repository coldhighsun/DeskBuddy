namespace DeskBuddy.Domain;

/// <summary>
/// Visual and behavioral states the pet can be in.
/// </summary>
public enum PetState
{
    /// <summary>
    /// Default resting state.
    /// </summary>
    Idle,

    /// <summary>
    /// The pet is walking.
    /// </summary>
    Walk,

    /// <summary>
    /// The pet is asleep, typically because the user is away.
    /// </summary>
    Sleep,

    /// <summary>
    /// The pet is being dragged by the user.
    /// </summary>
    Drag,

    /// <summary>
    /// The pet is tired because the system is under heavy load.
    /// </summary>
    Tired,

    /// <summary>
    /// The pet is drawing attention to a reminder.
    /// </summary>
    Alert,

    /// <summary>
    /// The pet is quietly accompanying a focus (pomodoro work) session.
    /// </summary>
    Focus,

    /// <summary>
    /// The pet is happy, for example after being clicked.
    /// </summary>
    Happy,

    /// <summary>
    /// The pet is sitting at a computer, keeping the user company while they develop.
    /// </summary>
    Coding,

    /// <summary>
    /// A build is running; the pet shows a loading indicator.
    /// </summary>
    Building,

    /// <summary>
    /// A build has just finished; the pet cheers (or wipes its brow, as it cannot tell whether it succeeded).
    /// </summary>
    BuildDone,

    /// <summary>
    /// Code is being sent to or fetched from a remote git repository; the pet ships a parcel.
    /// </summary>
    GitPush,

    /// <summary>
    /// The user has been developing for hours late at night; the pet keeps them company with coffee and heavy eyes.
    /// </summary>
    NightCoding,

    /// <summary>
    /// The user looked something up in a browser right after developing; the pet reads along with a magnifier.
    /// </summary>
    Reading,

    /// <summary>
    /// The user is back after a long absence; the pet waves hello.
    /// </summary>
    Welcome,

    /// <summary>
    /// A meeting application is in front; the pet puts on a headset and keeps quiet.
    /// </summary>
    Meeting,

    /// <summary>
    /// The battery is low and the computer is not plugged in; the pet shows an empty battery.
    /// </summary>
    LowBattery,

    /// <summary>
    /// The power cable has just been plugged in; the pet charges up happily.
    /// </summary>
    Charging,

    /// <summary>
    /// It is mealtime; the pet holds up a bowl and chopsticks.
    /// </summary>
    Meal,

    /// <summary>
    /// The working day is over; the pet checks its watch.
    /// </summary>
    ClockOut,

    /// <summary>
    /// A full hour has just begun; the pet strikes a little bell.
    /// </summary>
    Chime,

    /// <summary>
    /// It is a festival; the pet puts on a party hat.
    /// </summary>
    Festive,

    /// <summary>
    /// The mouse was shaken wildly; the pet is dizzy, with stars circling its head.
    /// </summary>
    Dizzy,

    /// <summary>
    /// The pet was dropped at the left or right edge of the screen and hangs on to it.
    /// </summary>
    Hang,

    /// <summary>
    /// The pet was dropped from a high place and is falling, holding an umbrella or a parachute.
    /// </summary>
    Fall,
}
