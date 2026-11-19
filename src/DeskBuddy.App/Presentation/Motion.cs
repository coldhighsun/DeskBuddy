using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// The small entrance animations of the windows. All of them are skipped when Windows has animations turned off, so they
/// never get in the way of users who asked for no motion.
/// </summary>
internal static class Motion
{
    /// <summary>
    /// How long a content change (a tab, a dialog) takes to fade in.
    /// </summary>
    public static readonly TimeSpan Short = TimeSpan.FromMilliseconds(160);

    /// <summary>
    /// How long a pop-up (the speech bubble) takes to appear.
    /// </summary>
    public static readonly TimeSpan Pop = TimeSpan.FromMilliseconds(180);

    /// <summary>
    /// How far, in device-independent pixels, content slides up while it fades in.
    /// </summary>
    private const double SlideDistance = 8;

    /// <summary>
    /// The size a pop-up starts from, as a fraction of its final size.
    /// </summary>
    private const double PopStartScale = 0.9;

    /// <summary>
    /// Gets whether Windows allows animations in the client area of windows.
    /// </summary>
    public static bool AnimationsEnabled => SystemParameters.ClientAreaAnimation;

    /// <summary>
    /// Works out how long an animation should last.
    /// </summary>
    /// <param name="duration">
    /// The duration the animation has when motion is allowed.
    /// </param>
    /// <param name="animationsEnabled">
    /// Whether motion is allowed.
    /// </param>
    /// <returns>
    /// The duration itself, or <see cref="TimeSpan.Zero"/> when motion is not allowed.
    /// </returns>
    public static TimeSpan Resolve(TimeSpan duration, bool animationsEnabled)
    {
        return animationsEnabled ? duration : TimeSpan.Zero;
    }

    /// <summary>
    /// Fades an element in while it slides up a little.
    /// </summary>
    /// <param name="element">
    /// The element that has just appeared.
    /// </param>
    public static void FadeIn(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var duration = Resolve(Short, AnimationsEnabled);
        if (duration == TimeSpan.Zero)
        {
            return;
        }

        var slide = new TranslateTransform(0, SlideDistance);
        element.RenderTransform = slide;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var fade = new DoubleAnimation(0, 1, duration) { EasingFunction = easing };
        fade.Completed += (_, _) => Settle(element);
        element.BeginAnimation(UIElement.OpacityProperty, fade);
        slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(SlideDistance, 0, duration) { EasingFunction = easing });
    }

    /// <summary>
    /// Makes an element grow and fade in from its transform origin, like a speech bubble popping up. The element should
    /// set <see cref="UIElement.RenderTransformOrigin"/> to the point it grows from.
    /// </summary>
    /// <param name="element">
    /// The element that has just appeared.
    /// </param>
    public static void PopIn(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var duration = Resolve(Pop, AnimationsEnabled);
        if (duration == TimeSpan.Zero)
        {
            return;
        }

        var scale = new ScaleTransform(PopStartScale, PopStartScale);
        element.RenderTransform = scale;
        var easing = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };
        var fade = new DoubleAnimation(0, 1, duration);
        fade.Completed += (_, _) => Settle(element);
        element.BeginAnimation(UIElement.OpacityProperty, fade);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(PopStartScale, 1, duration) { EasingFunction = easing });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(PopStartScale, 1, duration) { EasingFunction = easing });
    }

    /// <summary>
    /// Takes the animation state off an element once its entrance has finished. A finished animation that is held keeps
    /// the element drawn as an animated one, which costs its text the sharp ClearType rendering for as long as it is shown.
    /// </summary>
    /// <param name="element">
    /// The element whose entrance has finished.
    /// </param>
    private static void Settle(UIElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = 1;
        element.RenderTransform = Transform.Identity;
    }
}
