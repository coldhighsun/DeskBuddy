namespace DeskBuddy.Application;

/// <summary>
/// What a global shortcut needs of the pet: to hide or show it. Kept apart from <see cref="PetBrain"/> so that a caller on
/// another thread than the pet's can be given an implementation that hands the request over to it.
/// </summary>
public interface IPetVisibilityToggle
{
    /// <summary>
    /// Hides the pet if it is shown by the user's choice, and shows it if the user had hidden it.
    /// </summary>
    void ToggleManuallyHidden();
}
