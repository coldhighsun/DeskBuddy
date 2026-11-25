using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DeskBuddy.App.Presentation;

namespace DeskBuddy.App.Windows;

/// <summary>
/// What a switch animation draws on and moves: the canvas over the pet, the pet's measures in it, a stand-in picture of the
/// pet that can fly out of the pet window, and the pet's own content for the animations that act on that directly. Also
/// holds the small helpers the animations are built from.
/// </summary>
internal sealed class EffectStage
{
    /// <summary>
    /// How much longer than an animation lasts the wait for its end may take before it is given up on, in milliseconds, so
    /// that an animation that never finishes (its window was closed) cannot hold a pet switch up.
    /// </summary>
    private const int Patience = 300;

    /// <summary>
    /// The pace of every switch animation: the durations the effects ask for are multiplied by this, so a value above one
    /// makes all of them slower.
    /// </summary>
    internal const double Pace = 1.7;

    /// <summary>
    /// Starts fast and slows down towards the end.
    /// </summary>
    public static readonly IEasingFunction EaseOut = Freeze(new CubicEase { EasingMode = EasingMode.EaseOut });

    /// <summary>
    /// Starts slowly and speeds up towards the end.
    /// </summary>
    public static readonly IEasingFunction EaseIn = Freeze(new CubicEase { EasingMode = EasingMode.EaseIn });

    /// <summary>
    /// Goes a little past the end and comes back.
    /// </summary>
    public static readonly IEasingFunction Overshoot = Freeze(new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 });

    /// <summary>
    /// The window the animation is drawn in.
    /// </summary>
    private readonly EffectWindow _window;

    /// <summary>
    /// The pet window the animation is for.
    /// </summary>
    private readonly Window _petWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="EffectStage"/> class.
    /// </summary>
    /// <param name="window">The window the animation is drawn in.</param>
    /// <param name="petWindow">The pet window the animation is for.</param>
    /// <param name="pet">The pet's own content, the element that is faded and clipped when the pet itself is animated.</param>
    /// <param name="layout">The first layout.</param>
    /// <param name="frame">The picture the stand-in shows.</param>
    /// <param name="mirrored">Whether the pet's picture is shown mirrored left to right.</param>
    public EffectStage(EffectWindow window, Window petWindow, UIElement pet, EffectLayout layout, ImageSource? frame, bool mirrored)
    {
        _window = window;
        _petWindow = petWindow;
        Pet = pet;
        Layout = layout;

        CloneFlip = new ScaleTransform(1, 1);
        CloneScale = new ScaleTransform(1, 1);
        CloneRotate = new RotateTransform(0);
        CloneShift = new TranslateTransform(0, 0);
        Clone = new Image
        {
            Stretch = Stretch.Fill,
            Opacity = 0,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup { Children = { CloneFlip, CloneScale, CloneRotate, CloneShift } },
        };
        RenderOptions.SetBitmapScalingMode(Clone, BitmapScalingMode.HighQuality);
        window.Canvas.Children.Add(Clone);
        Relayout(layout, frame, mirrored);
    }

    /// <summary>
    /// Gets the layout of the window and the pet in it.
    /// </summary>
    public EffectLayout Layout { get; private set; }

    /// <summary>
    /// Gets the canvas the animation is drawn onto.
    /// </summary>
    public Canvas Canvas => _window.Canvas;

    /// <summary>
    /// Gets the pet's own content, which animations may fade or clip; it belongs to the pet window.
    /// </summary>
    public UIElement Pet { get; }

    /// <summary>
    /// Gets the stand-in picture of the pet, at the place of the pet and invisible at first.
    /// </summary>
    public Image Clone { get; }

    /// <summary>
    /// Gets the transform that mirrors the stand-in as the pet is.
    /// </summary>
    public ScaleTransform CloneFlip { get; }

    /// <summary>
    /// Gets the transform that scales the stand-in about its middle.
    /// </summary>
    public ScaleTransform CloneScale { get; }

    /// <summary>
    /// Gets the transform that turns the stand-in about its middle.
    /// </summary>
    public RotateTransform CloneRotate { get; }

    /// <summary>
    /// Gets the transform that moves the stand-in away from the place of the pet.
    /// </summary>
    public TranslateTransform CloneShift { get; }

    /// <summary>
    /// Puts the window and the stand-in at a new layout, with a new picture, as when the pet has been replaced by one of
    /// another size.
    /// </summary>
    /// <param name="layout">The new layout.</param>
    /// <param name="frame">The picture the stand-in shows.</param>
    /// <param name="mirrored">Whether the pet's picture is shown mirrored left to right.</param>
    public void Relayout(EffectLayout layout, ImageSource? frame, bool mirrored)
    {
        Layout = layout;
        _window.Place(layout, _petWindow);
        Clone.Source = frame;
        Clone.Width = layout.PetWidth;
        Clone.Height = layout.PetHeight;
        Canvas.SetLeft(Clone, layout.PetLeft);
        Canvas.SetTop(Clone, layout.PetTop);
        CloneFlip.ScaleX = mirrored ? -1 : 1;
    }

    /// <summary>
    /// Removes everything the animation drew, except the stand-in, which is put back to rest and hidden.
    /// </summary>
    public void Clear()
    {
        Canvas.Children.Clear();
        Canvas.Children.Add(Clone);
        ResetClone();
    }

    /// <summary>
    /// Puts the stand-in back to its size, place and angle, and hides it.
    /// </summary>
    public void ResetClone()
    {
        Clone.BeginAnimation(UIElement.OpacityProperty, null);
        Clone.Opacity = 0;
        foreach (var (transform, property, value) in new (Animatable, DependencyProperty, double)[]
        {
            (CloneScale, ScaleTransform.ScaleXProperty, 1),
            (CloneScale, ScaleTransform.ScaleYProperty, 1),
            (CloneRotate, RotateTransform.AngleProperty, 0),
            (CloneShift, TranslateTransform.XProperty, 0),
            (CloneShift, TranslateTransform.YProperty, 0),
        })
        {
            transform.BeginAnimation(property, null);
            transform.SetValue(property, value);
        }
    }

    /// <summary>
    /// Animates a number and waits for it to arrive. The value stays where it arrives.
    /// </summary>
    /// <param name="target">What is animated.</param>
    /// <param name="property">The property that is animated.</param>
    /// <param name="to">The value it goes to.</param>
    /// <param name="milliseconds">How long it takes.</param>
    /// <param name="easing">How it speeds up and slows down, or <see langword="null"/> for none.</param>
    /// <param name="from">The value it starts from, or <see langword="null"/> for the one it has.</param>
    /// <returns>A task that completes when the value has arrived.</returns>
    public Task AnimateAsync(
        IAnimatable target,
        DependencyProperty property,
        double to,
        int milliseconds,
        IEasingFunction? easing = null,
        double? from = null)
    {
        var duration = Duration(milliseconds, Motion.AnimationsEnabled);
        if (duration == TimeSpan.Zero)
        {
            ((DependencyObject)target).SetValue(property, to);
            return Task.CompletedTask;
        }

        var done = new TaskCompletionSource();
        var animation = new DoubleAnimation { To = to, Duration = duration, EasingFunction = easing };
        if (from is { } start)
        {
            animation.From = start;
        }

        animation.Completed += (_, _) => done.TrySetResult();
        target.BeginAnimation(property, animation);
        return Wait(done.Task, duration);
    }

    /// <summary>
    /// Animates a rectangle and waits for it to arrive. The rectangle stays where it arrives.
    /// </summary>
    /// <param name="target">The geometry whose rectangle is animated.</param>
    /// <param name="from">The rectangle it starts from.</param>
    /// <param name="to">The rectangle it goes to.</param>
    /// <param name="milliseconds">How long it takes.</param>
    /// <returns>A task that completes when the rectangle has arrived.</returns>
    public Task AnimateRectAsync(RectangleGeometry target, Rect from, Rect to, int milliseconds)
    {
        var duration = Duration(milliseconds, Motion.AnimationsEnabled);
        if (duration == TimeSpan.Zero)
        {
            target.Rect = to;
            return Task.CompletedTask;
        }

        var done = new TaskCompletionSource();
        var animation = new RectAnimation(from, to, duration);
        animation.Completed += (_, _) => done.TrySetResult();
        target.BeginAnimation(RectangleGeometry.RectProperty, animation);
        return Wait(done.Task, duration);
    }

    /// <summary>
    /// Waits a while, as a pause in an animation.
    /// </summary>
    /// <param name="milliseconds">How long to wait.</param>
    /// <returns>A task that completes after the wait; at once when motion is not allowed.</returns>
    public static Task PauseAsync(int milliseconds)
    {
        var duration = Duration(milliseconds, Motion.AnimationsEnabled);
        return duration == TimeSpan.Zero ? Task.CompletedTask : Task.Delay(duration);
    }

    /// <summary>
    /// Works out how long a step of an animation lasts: the time the effect asked for, slowed by <see cref="Pace"/>, or none
    /// at all when motion is not allowed.
    /// </summary>
    /// <param name="milliseconds">How long the effect asked for.</param>
    /// <param name="animationsEnabled">Whether motion is allowed.</param>
    /// <returns>The time the step takes.</returns>
    internal static TimeSpan Duration(int milliseconds, bool animationsEnabled)
    {
        return Motion.Resolve(TimeSpan.FromMilliseconds(milliseconds * Pace), animationsEnabled);
    }

    /// <summary>
    /// Animates the same number on a transform that scales on both axes.
    /// </summary>
    /// <param name="target">The transform.</param>
    /// <param name="to">The scale it goes to.</param>
    /// <param name="milliseconds">How long it takes.</param>
    /// <param name="easing">How it speeds up and slows down, or <see langword="null"/> for none.</param>
    /// <returns>A task that completes when the scale has arrived.</returns>
    public Task ScaleAsync(ScaleTransform target, double to, int milliseconds, IEasingFunction? easing = null)
    {
        return Task.WhenAll(
            AnimateAsync(target, ScaleTransform.ScaleXProperty, to, milliseconds, easing),
            AnimateAsync(target, ScaleTransform.ScaleYProperty, to, milliseconds, easing));
    }

    /// <summary>
    /// Freezes an easing function, so it can be shared.
    /// </summary>
    /// <param name="easing">The easing function.</param>
    /// <typeparam name="T">The kind of easing function.</typeparam>
    /// <returns>The same easing function, frozen.</returns>
    private static T Freeze<T>(T easing)
        where T : EasingFunctionBase
    {
        easing.Freeze();
        return easing;
    }

    /// <summary>
    /// Waits for an animation to end, but not much longer than it should take.
    /// </summary>
    /// <param name="finished">Completes when the animation ends.</param>
    /// <param name="duration">How long the animation lasts.</param>
    /// <returns>A task that completes when the animation has ended or was given up on.</returns>
    private static async Task Wait(Task finished, TimeSpan duration)
    {
        try
        {
            await finished.WaitAsync(duration + TimeSpan.FromMilliseconds(Patience));
        }
        catch (TimeoutException)
        {
            // Given up on: the animation will not finish, as when its window has been closed.
        }
    }
}
