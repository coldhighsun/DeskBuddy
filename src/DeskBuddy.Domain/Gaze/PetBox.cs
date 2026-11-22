namespace DeskBuddy.Domain.Gaze;

/// <summary>
/// Where the pet is on the screen, for working out where to look.
/// </summary>
/// <param name="CenterX">The horizontal centre, in pixels.</param>
/// <param name="CenterY">The vertical centre, in pixels.</param>
/// <param name="Size">The width, in pixels.</param>
public readonly record struct PetBox(double CenterX, double CenterY, double Size);
