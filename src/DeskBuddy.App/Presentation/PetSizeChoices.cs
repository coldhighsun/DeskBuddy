using System.Windows.Media;
using DeskBuddy.App.Resources;
using DeskBuddy.Domain.Settings;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// One size to choose from, shown with the pet in use drawn at the size it gets on screen.
/// </summary>
/// <param name="Size">
/// The size.
/// </param>
/// <param name="Label">
/// The name of the size in the language of the interface.
/// </param>
/// <param name="Picture">
/// The picture of the pet in use, or <see langword="null"/> if the pet in use has none.
/// </param>
/// <param name="Width">
/// The width in device-independent pixels to draw the picture in; 0 if there is no pet to measure. It is the width the
/// pet in use has at this size on screen, unless that would make the list too big, see
/// <see cref="PetSizeChoices.MaxPictureEdge"/>.
/// </param>
/// <param name="Height">
/// The height in device-independent pixels to draw the picture in; 0 if there is no pet to measure. See
/// <paramref name="Width"/>.
/// </param>
/// <param name="IsCurrent">
/// Whether this is the size in use.
/// </param>
public sealed record SizeChoice(PetSize Size, string Label, ImageSource? Picture, double Width, double Height, bool IsCurrent);

/// <summary>
/// Works out the sizes to choose from, as the tray menu and the panel show them: the pet in use at each size it can have,
/// so the user sees what they get. Kept apart from the windows so it can be tested.
/// </summary>
internal static class PetSizeChoices
{
    /// <summary>
    /// The longest edge in device-independent pixels the picture of the biggest size is drawn with. A pet may declare any
    /// frame size, and a list of pictures as big as the pet itself would no longer fit on the screen; when the pet is
    /// bigger than this, all sizes are drawn smaller by the same factor, so they keep their proportions.
    /// </summary>
    public const double MaxPictureEdge = 96;

    /// <summary>
    /// Lists the sizes, each with the size the pet in use has at it, and marks the size in use.
    /// </summary>
    /// <param name="pets">
    /// The pets that can be chosen.
    /// </param>
    /// <param name="currentPet">
    /// The folder name of the pet in use.
    /// </param>
    /// <param name="currentSize">
    /// The size in use.
    /// </param>
    /// <returns>
    /// One choice per size, from the smallest. Without a pet in use, or when it has no picture, the choices have no
    /// picture and no measure.
    /// </returns>
    public static IReadOnlyList<SizeChoice> For(IReadOnlyList<InstalledPet> pets, string currentPet, PetSize currentSize)
    {
        ArgumentNullException.ThrowIfNull(pets);

        // A pet without a picture to draw gets no measure either, as an empty box of its size would only be in the way.
        var pet = pets.FirstOrDefault(entry => string.Equals(entry.FolderName, currentPet, StringComparison.Ordinal) && entry.Picture is not null);
        var sizes = Enum.GetValues<PetSize>();
        var factor = pet is null
            ? 1
            : Math.Min(1, MaxPictureEdge / sizes.Max(size => Math.Max(size.Scale(pet.FrameWidth), size.Scale(pet.FrameHeight))));
        return
        [
            .. sizes.Select(size => new SizeChoice(
                size,
                Label(size),
                pet?.Picture,
                pet is null ? 0 : Math.Round(size.Scale(pet.FrameWidth) * factor),
                pet is null ? 0 : Math.Round(size.Scale(pet.FrameHeight) * factor),
                size == currentSize)),
        ];
    }

    /// <summary>
    /// Gets the name of a size.
    /// </summary>
    /// <param name="size">
    /// The size.
    /// </param>
    /// <returns>
    /// The name in the language of the interface.
    /// </returns>
    public static string Label(PetSize size)
    {
        return size switch
        {
            PetSize.Small => Strings.PetSizeSmall,
            PetSize.Medium => Strings.PetSizeMedium,
            _ => Strings.PetSizeLarge,
        };
    }
}
