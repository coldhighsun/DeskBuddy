using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using DeskBuddy.Domain.Messages;

namespace DeskBuddy.App.Windows;

/// <summary>
/// Speech bubble that appears above the pet to show a message and its action buttons. Never takes keyboard focus, so
/// it does not interrupt whatever the user is typing.
/// </summary>
public partial class BubbleWindow : Window
{
    /// <summary>
    /// How much room, in device-independent pixels, to leave between the bubble's bottom edge and the top of the pet's
    /// sprite.
    /// </summary>
    private const double SpriteGap = 2;

    /// <summary>
    /// Smallest distance between the tail and the bubble's rounded corners.
    /// </summary>
    private const double TailEdgeMargin = 14;

    /// <summary>
    /// Width of the tail, matching its path data.
    /// </summary>
    private const double TailWidth = 14;

    /// <summary>
    /// Initializes a new instance of the <see cref="BubbleWindow"/> class.
    /// </summary>
    public BubbleWindow()
    {
        InitializeComponent();

        SourceInitialized += OnSourceInitialized;
    }

    /// <summary>
    /// Raised when the user clicks one of the bubble's buttons.
    /// </summary>
    public event EventHandler<MessageAction>? ActionInvoked;

    /// <summary>
    /// Replaces the bubble's text and buttons.
    /// </summary>
    /// <param name="content">
    /// What to show.
    /// </param>
    public void SetContent(BubbleContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        MessageText.Text = content.Text;
        ButtonPanel.Children.Clear();
        foreach (var (action, label) in content.Buttons)
        {
            var button = new Button
            {
                Content = label,
                Margin = new Thickness(0, 8, 6, 0),
                Padding = new Thickness(10, 3, 10, 3),
                Focusable = false,
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "BubbleButtonStyle");
            button.Click += (_, _) => ActionInvoked?.Invoke(this, action);
            ButtonPanel.Children.Add(button);
        }
    }

    /// <summary>
    /// Puts the bubble above the pet's sprite, horizontally centered on it but kept inside the monitor's work area,
    /// with the tail still pointing at the pet.
    /// </summary>
    /// <param name="pet">
    /// The pet window to anchor to.
    /// </param>
    /// <param name="spriteTop">
    /// Where the sprite starts inside the pet window, as a fraction of the window height.
    /// </param>
    public void PositionAbove(Window pet, double spriteTop)
    {
        ArgumentNullException.ThrowIfNull(pet);

        // A window that has not been shown yet has no ActualWidth/Height, so measure the content directly.
        var root = (FrameworkElement)Content;
        root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = root.DesiredSize;

        // Everything is worked out in device pixels of the pet's monitor, where the bubble is going to be, and not in
        // this window's own scale, which is still that of the monitor it was last shown on.
        var petScale = VisualTreeHelper.GetDpi(pet).DpiScaleX;
        var petBounds = MonitorHelper.GetWindowBoundsPixels(pet);
        var placement = BubblePlacement.Compute(
            petBounds,
            (int)Math.Ceiling(size.Width * petScale),
            (int)Math.Ceiling(size.Height * petScale),
            BubblePlacement.TailOverlap(
                petBounds.Height,
                spriteTop,
                (int)Math.Round(SpriteGap * petScale, MidpointRounding.AwayFromZero)),
            MonitorHelper.GetWorkAreaPixels(pet));
        WindowMover.MoveToPixels(this, placement.Left, placement.Top);

        var tailX = ((placement.PetCenterX - placement.Left) / petScale) - (TailWidth / 2);
        var maxTailX = Math.Max(TailEdgeMargin, size.Width - TailEdgeMargin - TailWidth);
        Tail.Margin = new Thickness(Math.Clamp(tailX, TailEdgeMargin, maxTailX), -1, 0, 0);
    }

    /// <summary>
    /// Configures the native window once its handle exists, the same way as the pet window.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)!;
        source.CompositionTarget.BackgroundColor = Colors.Transparent;

        WindowStyleHelper.MakeToolWindowNoActivate(this);
        WindowStyleHelper.RemoveDwmDecorations(this);
    }
}
