using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace DeskBuddy.App.Interop;

/// <summary>
/// Gives a borderless window the operating system's own rounded corners. The window must not use
/// <c>AllowsTransparency</c>: DWM then clips the whole window and its content to the rounded shape, instead of the
/// application drawing a rounded border whose anti-aliased edge leaves a seam.
/// </summary>
internal static class WindowCornerHelper
{
    /// <summary>
    /// The first Windows build (Windows 11) whose DWM supports the corner preference.
    /// </summary>
    private const int MinimumBuild = 22000;

    /// <summary>
    /// Attached property that gives a window the native rounded corners. <c>BorderlessWindowStyle</c> in
    /// <c>Themes/AppTheme.xaml</c> sets it, so a window gets the corners by using that style.
    /// </summary>
    public static readonly DependencyProperty UseNativeCornersProperty = DependencyProperty.RegisterAttached(
        "UseNativeCorners",
        typeof(bool),
        typeof(WindowCornerHelper),
        new PropertyMetadata(false, OnUseNativeCornersChanged));

    /// <summary>
    /// Gets a value indicating whether the running Windows version rounds the corners of a borderless window and draws
    /// its border and shadow itself.
    /// </summary>
    public static bool IsSupportedByThisOs => IsSupported(Environment.OSVersion.Version.Build);

    /// <summary>
    /// Tells whether a Windows build supports corners drawn by DWM.
    /// </summary>
    /// <param name="osBuild">The Windows build number.</param>
    /// <returns><see langword="true"/> from Windows 11 (build 22000) onwards.</returns>
    public static bool IsSupported(int osBuild)
    {
        return osBuild >= MinimumBuild;
    }

    /// <summary>
    /// Gets <see cref="UseNativeCornersProperty"/>.
    /// </summary>
    /// <param name="element">The element, normally a window.</param>
    /// <returns>Whether the element gets the native rounded corners.</returns>
    public static bool GetUseNativeCorners(DependencyObject element)
    {
        return (bool)element.GetValue(UseNativeCornersProperty);
    }

    /// <summary>
    /// Sets <see cref="UseNativeCornersProperty"/>.
    /// </summary>
    /// <param name="element">The element, normally a window.</param>
    /// <param name="value">Whether the element gets the native rounded corners.</param>
    public static void SetUseNativeCorners(DependencyObject element, bool value)
    {
        element.SetValue(UseNativeCornersProperty, value);
    }

    /// <summary>
    /// Asks DWM to round the corners of a window once its handle exists. A window without a caption style is not rounded
    /// by Windows 11 on its own, so the preference is set explicitly; DWM then draws the border and the shadow too.
    /// Windows 10 does not know the attribute, so the window draws a 1 px outline of its own instead.
    /// </summary>
    /// <param name="window">The window to round.</param>
    /// <param name="nativeCornersSupported">Whether this Windows version supports the corner attribute.</param>
    private static void Apply(Window window, bool nativeCornersSupported)
    {
        if (!nativeCornersSupported)
        {
            window.BorderThickness = new Thickness(1);
            window.SetResourceReference(Control.BorderBrushProperty, "AppDivider");
            return;
        }

        var helper = new WindowInteropHelper(window);
        if (helper.Handle != 0)
        {
            WindowStyleHelper.SetCornerPreference(helper.Handle, WindowStyleHelper.DwmwcpRound);
            return;
        }

        // There is no handle yet: set the corners when there is one, unless the property was switched off meanwhile.
        window.SourceInitialized += (_, _) =>
        {
            if (GetUseNativeCorners(window))
            {
                WindowStyleHelper.SetCornerPreference(helper.Handle, WindowStyleHelper.DwmwcpRound);
            }
        };
    }

    /// <summary>
    /// Takes the corners <see cref="Apply"/> gave a window away again.
    /// </summary>
    /// <param name="window">The window to take the corners from.</param>
    /// <param name="nativeCornersSupported">Whether this Windows version supports the corner attribute.</param>
    private static void Remove(Window window, bool nativeCornersSupported)
    {
        if (!nativeCornersSupported)
        {
            window.ClearValue(Control.BorderThicknessProperty);
            window.ClearValue(Control.BorderBrushProperty);
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle != 0)
        {
            WindowStyleHelper.SetCornerPreference(handle, WindowStyleHelper.DwmwcpDefault);
        }
    }

    /// <summary>
    /// Gives a window the corners when the property is switched on, and takes them away when it is switched off.
    /// </summary>
    /// <param name="element">The element the property changed on.</param>
    /// <param name="e">The change.</param>
    private static void OnUseNativeCornersChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Window window)
        {
            return;
        }

        if (e.NewValue is true)
        {
            Apply(window, IsSupportedByThisOs);
        }
        else
        {
            Remove(window, IsSupportedByThisOs);
        }
    }
}
