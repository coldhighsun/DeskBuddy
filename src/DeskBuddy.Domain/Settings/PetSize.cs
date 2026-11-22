namespace DeskBuddy.Domain.Settings;

/// <summary>
/// How large the pet is shown on the screen.
/// </summary>
public enum PetSize
{
    /// <summary>
    /// A small pet, three quarters of the reference size.
    /// </summary>
    Small,

    /// <summary>
    /// A medium pet, the reference size.
    /// </summary>
    Medium,

    /// <summary>
    /// A large pet, one and a half times the reference size.
    /// </summary>
    Large,
}

/// <summary>
/// Conversions for <see cref="PetSize"/>.
/// </summary>
public static class PetSizeExtensions
{
    /// <summary>
    /// Gets the edge length in device independent pixels that pets are designed for, which is what the
    /// <see cref="PetSize.Medium"/> size shows.
    /// </summary>
    public const double ReferenceEdge = 64;

    /// <summary>
    /// Gets the factor by which a size scales the frame size declared by a pet.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <returns>0.75, 1 or 1.5.</returns>
    public static double ToScale(this PetSize size)
    {
        return size switch
        {
            PetSize.Small => 48 / ReferenceEdge,
            PetSize.Medium => 1,
            _ => 96 / ReferenceEdge,
        };
    }

    /// <summary>
    /// Scales one edge of a pet frame.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <param name="frameEdge">The edge length declared by the pet.</param>
    /// <returns>The scaled edge length, rounded and at least 1.</returns>
    public static double Scale(this PetSize size, int frameEdge)
    {
        return Math.Max(1, Math.Round(frameEdge * size.ToScale()));
    }
}
