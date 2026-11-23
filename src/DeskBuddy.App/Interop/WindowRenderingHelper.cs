using System.Windows;
using System.Windows.Interop;

namespace DeskBuddy.App.Interop;

/// <summary>
/// Makes an ordinary window (the panel, a dialog) draw on the CPU instead of through Direct3D. When such a window is dragged
/// between monitors of different scaling, WPF makes a new render surface at each change of scaling and the memory of the
/// old ones is not given back (measured: about 7 MB per change, without end, for the panel), while the CPU path does not
/// grow at all and even starts lower. These windows draw plain controls and a few small pictures, so nothing is lost; the
/// pet and its other windows, which are animated all the time, are not affected, as they do not use the style that sets
/// this.
/// </summary>
internal static class WindowRenderingHelper
{
    /// <summary>
    /// Attached property that makes a window draw on the CPU. <c>BorderlessWindowStyle</c> in <c>Themes/AppTheme.xaml</c>
    /// sets it, so a window gets this by using that style.
    /// </summary>
    public static readonly DependencyProperty UseSoftwareRenderingProperty = DependencyProperty.RegisterAttached(
        "UseSoftwareRendering",
        typeof(bool),
        typeof(WindowRenderingHelper),
        new PropertyMetadata(false, OnUseSoftwareRenderingChanged));

    /// <summary>
    /// Gets <see cref="UseSoftwareRenderingProperty"/>.
    /// </summary>
    /// <param name="element">The element, normally a window.</param>
    /// <returns>Whether the element draws on the CPU.</returns>
    public static bool GetUseSoftwareRendering(DependencyObject element)
    {
        return (bool)element.GetValue(UseSoftwareRenderingProperty);
    }

    /// <summary>
    /// Sets <see cref="UseSoftwareRenderingProperty"/>.
    /// </summary>
    /// <param name="element">The element, normally a window.</param>
    /// <param name="value">Whether the element draws on the CPU.</param>
    public static void SetUseSoftwareRendering(DependencyObject element, bool value)
    {
        element.SetValue(UseSoftwareRenderingProperty, value);
    }

    /// <summary>
    /// Switches the rendering of a window when the property changes: at once if the window has a handle, otherwise when it
    /// gets one.
    /// </summary>
    /// <param name="element">The element the property changed on.</param>
    /// <param name="e">The change.</param>
    private static void OnUseSoftwareRenderingChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Window window)
        {
            return;
        }

        if (new WindowInteropHelper(window).Handle != 0)
        {
            Apply(window);
            return;
        }

        window.SourceInitialized += (_, _) => Apply(window);
    }

    /// <summary>
    /// Gives a window the rendering its property says, if it has a render target yet.
    /// </summary>
    /// <param name="window">The window.</param>
    private static void Apply(Window window)
    {
        if (HwndSource.FromHwnd(new WindowInteropHelper(window).Handle) is { CompositionTarget: { } target })
        {
            target.RenderMode = GetUseSoftwareRendering(window) ? RenderMode.SoftwareOnly : RenderMode.Default;
        }
    }
}
