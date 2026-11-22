using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using DeskBuddy.App.Presentation;

namespace DeskBuddy.App.Windows;

/// <summary>
/// Click-through window drawn over the pet window while another pet is switched to: the saucer, the claw, the smoke or the
/// beam of light are drawn in code onto <see cref="Canvas"/>. It is placed in front of the pet window.
/// </summary>
internal sealed partial class EffectWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EffectWindow"/> class.
    /// </summary>
    public EffectWindow()
    {
        InitializeComponent();

        SourceInitialized += OnSourceInitialized;

        // The window is placed in device pixels before it is first shown, which needs its handle.
        _ = new WindowInteropHelper(this).EnsureHandle();
    }

    /// <summary>
    /// Gets the canvas the animation is drawn onto; its origin is the top-left corner of the window.
    /// </summary>
    public Canvas Canvas => Root;

    /// <summary>
    /// Puts the window where a layout says, shows it if it is not shown, and keeps it in front of the pet window.
    /// </summary>
    /// <param name="layout">Where the window goes.</param>
    /// <param name="pet">The pet window the animation is for.</param>
    public void Place(EffectLayout layout, Window pet)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(pet);

        // Sizes are in device-independent units at the scale of the monitor the window is on, so bring it onto the pet's
        // monitor first; the size then comes out right.
        WindowMover.MoveToPixelsSettled(this, layout.X, layout.Y);
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        Width = layout.WidthPixels / scale;
        Height = layout.HeightPixels / scale;
        WindowMover.MoveToPixels(this, layout.X, layout.Y);

        if (!IsVisible)
        {
            Show();
            WindowMover.MoveToPixels(this, layout.X, layout.Y);
        }

        WindowMover.PlaceBehind(pet, this);
    }

    /// <summary>
    /// Configures the native window once its handle exists, the same way as the pet window, and makes it click-through.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">Event data.</param>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)!;
        source.CompositionTarget.BackgroundColor = Colors.Transparent;
        source.AddHook(WindowStyleHelper.ClickThroughHook);

        WindowStyleHelper.MakeToolWindowNoActivate(this);
        WindowStyleHelper.RemoveDwmDecorations(this);
    }
}
