namespace DeskBuddy.Application;

/// <summary>
/// What the tray icon and the start-up code need of the pet's visibility, for a caller that lives on another thread than
/// the pet: the requests are handed over to the pet's thread, the answer may be read from any thread, and the change is
/// reported on the caller's own thread.
/// </summary>
public interface IPetVisibilityControl : IPetVisibilityToggle
{
    /// <summary>
    /// Raised, on the thread of the caller that made the control, when the pet was shown or hidden.
    /// </summary>
    event EventHandler? VisibilityChanged;

    /// <summary>
    /// Gets a value indicating whether the user has hidden the pet.
    /// </summary>
    bool IsManuallyHidden { get; }

    /// <summary>
    /// Hides or shows the pet at the user's request.
    /// </summary>
    /// <param name="hidden"><see langword="true"/> to hide the pet.</param>
    void SetManuallyHidden(bool hidden);

    /// <summary>
    /// Puts the pet away while the session is locked and brings it back once it is unlocked.
    /// </summary>
    /// <param name="locked"><see langword="true"/> when the session has been locked.</param>
    void SetSessionLocked(bool locked);
}
