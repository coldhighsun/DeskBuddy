using System.Windows;
using System.Windows.Media;

namespace DeskBuddy.App.Windows;

/// <summary>
/// Keeps the umbrella or parachute over a pet window: opens and folds it, moves it together with the pet while the pet
/// falls, and puts it right again when the pet is moved some other way. Holds everything that coordinates the two
/// windows, so the pet window does not have to.
/// </summary>
/// <param name="pet">
/// The pet window the canopy belongs to.
/// </param>
public sealed class UmbrellaFollower(Window pet)
{
    /// <summary>
    /// The canopy window, created the first time it is needed.
    /// </summary>
    private UmbrellaWindow? _canopy;

    /// <summary>
    /// The size of the pet window in device pixels, which does not change while the pet falls.
    /// </summary>
    private readonly PixelSizeCache _petSize = new(() =>
    {
        var bounds = MonitorHelper.GetWindowBoundsPixels(pet);
        return (bounds.Width, bounds.Height);
    });

    /// <summary>
    /// Whether the pet is being moved together with the canopy, which then places the canopy itself.
    /// </summary>
    private bool _movingTogether;

    /// <summary>
    /// Gets a value indicating whether the canopy is on screen.
    /// </summary>
    public bool IsOpen => _canopy is { IsVisible: true };

    /// <summary>
    /// Opens a canopy over the pet, an umbrella or a parachute by chance.
    /// </summary>
    public void Open()
    {
        _canopy ??= new UmbrellaWindow();
        _canopy.Open(Random.Shared.Next(2) == 0 ? UmbrellaKind.Umbrella : UmbrellaKind.Parachute, pet);
    }

    /// <summary>
    /// Folds the canopy away, if it is open.
    /// </summary>
    public void Fold()
    {
        _canopy?.Fold();
    }

    /// <summary>
    /// Closes the canopy window for good, as when the pet window is closed.
    /// </summary>
    public void Close()
    {
        _canopy?.Close();
    }

    /// <summary>
    /// Moves the pet to a new place and, if the canopy is open, the canopy over it to its new place together, so the
    /// canopy does not trail behind the pet.
    /// </summary>
    /// <param name="left">
    /// The pet's new left edge, in device-independent pixels.
    /// </param>
    /// <param name="top">
    /// The pet's new top edge, in device-independent pixels.
    /// </param>
    public void MovePet(double left, double top)
    {
        var dpi = VisualTreeHelper.GetDpi(pet);
        if (!IsOpen || !WindowMover.TryGetMove(pet.Left, pet.Top, left, top, dpi.DpiScaleX, dpi.DpiScaleY, out var x, out var y))
        {
            WindowMover.MoveTo(pet, left, top);
            return;
        }

        var (width, height) = _petSize.Get();
        _movingTogether = true;
        try
        {
            _canopy!.MoveWithPet(pet, new PixelRect(x, y, x + width, y + height), dpi.DpiScaleX);
        }
        finally
        {
            _movingTogether = false;
        }
    }

    /// <summary>
    /// Keeps the canopy over the pet's head after the pet was moved by something other than <see cref="MovePet"/>.
    /// </summary>
    public void OnPetLocationChanged()
    {
        if (!_movingTogether && IsOpen)
        {
            _canopy!.PositionAbove(pet);
        }
    }

    /// <summary>
    /// Forgets the pet's size after the pet window was resized or its scale changed.
    /// </summary>
    public void OnPetResized()
    {
        _petSize.Invalidate();
    }
}
