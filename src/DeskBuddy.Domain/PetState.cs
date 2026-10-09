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
}
