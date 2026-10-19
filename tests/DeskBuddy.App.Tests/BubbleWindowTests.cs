using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using DeskBuddy.App.Interop;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Windows;
using DeskBuddy.Domain.Messages;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for where the speech bubble sits relative to the pet, with real windows.
/// </summary>
public sealed class BubbleWindowTests
{
    /// <summary>
    /// A fraction of the pet window's height at which the test sprite starts.
    /// </summary>
    private const double SpriteTop = 0.25;

    /// <summary>
    /// A message long enough to wrap to several lines in the bubble.
    /// </summary>
    private const string LongText = "You have been working for 60 minutes in a row, get up, move around a little and drink some water.";

    /// <summary>
    /// A message that fits on one line.
    /// </summary>
    private const string ShortText = "Hello!";

    /// <summary>
    /// Creates a pet stand-in window that has a handle, is never shown and sits in the middle of the screen.
    /// </summary>
    /// <returns>
    /// The window.
    /// </returns>
    private static Window CreatePet()
    {
        var pet = new Window { Width = 128, Height = 128, ShowInTaskbar = false };
        _ = new WindowInteropHelper(pet).EnsureHandle();
        var work = MonitorHelper.GetWorkAreaPixels(pet);
        WindowMover.MoveToPixels(pet, work.Left + (work.Width / 2), work.Top + (work.Height / 2));
        return pet;
    }

    /// <summary>
    /// Lets the dispatcher finish the layout and rendering the bubble has queued.
    /// </summary>
    private static void FlushDispatcher()
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
    }

    /// <summary>
    /// Runs a scenario with a pet stand-in and a new bubble on an STA thread, and closes both windows afterwards even
    /// when the scenario fails.
    /// </summary>
    /// <param name="scenario">
    /// What to do with the pet, the bubble and the pixel row where the pet's sprite starts.
    /// </param>
    private static void Run(Action<Window, BubbleWindow, int> scenario)
    {
        StaThread.Run(() =>
        {
            var pet = CreatePet();
            var bubble = new BubbleWindow();
            try
            {
                var petBounds = MonitorHelper.GetWindowBoundsPixels(pet);
                var spriteTopPixel = petBounds.Top + (int)Math.Round(petBounds.Height * SpriteTop, MidpointRounding.AwayFromZero);
                scenario(pet, bubble, spriteTopPixel);
            }
            finally
            {
                bubble.Close();
                pet.Close();
            }
        });
    }

    /// <summary>
    /// Shows the bubble above the pet with a message, and lets its layout settle.
    /// </summary>
    /// <param name="pet">
    /// The pet stand-in.
    /// </param>
    /// <param name="bubble">
    /// The bubble.
    /// </param>
    /// <param name="text">
    /// The message.
    /// </param>
    private static void ShowMessage(Window pet, BubbleWindow bubble, string text)
    {
        bubble.SetContent(new BubbleContent(text, []));
        bubble.PositionAbove(pet, SpriteTop);
        if (!bubble.IsVisible)
        {
            bubble.Show();
        }

        FlushDispatcher();
    }

    /// <summary>
    /// Checks that the bubble ends at or above the sprite, and that it got there without being pushed down against the
    /// top of the work area, which would make the check meaningless.
    /// </summary>
    /// <param name="pet">
    /// The pet stand-in.
    /// </param>
    /// <param name="bubble">
    /// The bubble.
    /// </param>
    /// <param name="spriteTopPixel">
    /// The pixel row where the sprite starts.
    /// </param>
    private static void AssertEndsAboveSprite(Window pet, BubbleWindow bubble, int spriteTopPixel)
    {
        var bubbleBounds = MonitorHelper.GetWindowBoundsPixels(bubble);
        bubbleBounds.Top.Should().BeGreaterThan(MonitorHelper.GetWorkAreaPixels(pet).Top, "the bubble must not be clamped to the top of the screen");
        bubbleBounds.Bottom.Should().BeLessThanOrEqualTo(spriteTopPixel);
    }

    /// <summary>
    /// The bubble ends just above the sprite whether its text is on one line or wraps to several, after it has been laid
    /// out.
    /// </summary>
    /// <param name="text">
    /// The message to show.
    /// </param>
    [Theory]
    [InlineData(ShortText)]
    [InlineData(LongText)]
    public void PositionAbove_AfterLayout_BubbleEndsAboveTheSprite(string text)
    {
        Run((pet, bubble, spriteTopPixel) =>
        {
            // Act
            ShowMessage(pet, bubble, text);

            // Assert
            AssertEndsAboveSprite(pet, bubble, spriteTopPixel);
        });
    }

    /// <summary>
    /// Replacing a short message with a long one while the bubble is already showing makes it taller, and still keeps
    /// its bottom above the sprite.
    /// </summary>
    [Fact]
    public void PositionAbove_ContentGrowsWhileVisible_BubbleStillEndsAboveTheSprite()
    {
        Run((pet, bubble, spriteTopPixel) =>
        {
            // Arrange
            ShowMessage(pet, bubble, ShortText);
            var heightBefore = MonitorHelper.GetWindowBoundsPixels(bubble).Height;

            // Act
            ShowMessage(pet, bubble, LongText);

            // Assert
            MonitorHelper.GetWindowBoundsPixels(bubble).Height.Should().BeGreaterThan(heightBefore, "the long message wraps to more lines");
            AssertEndsAboveSprite(pet, bubble, spriteTopPixel);
        });
    }

    /// <summary>
    /// A long message shown after a short one has been hidden again also ends above the sprite.
    /// </summary>
    [Fact]
    public void PositionAbove_LongMessageAfterHiddenShortOne_BubbleEndsAboveTheSprite()
    {
        Run((pet, bubble, spriteTopPixel) =>
        {
            // Arrange
            ShowMessage(pet, bubble, ShortText);
            var heightBefore = MonitorHelper.GetWindowBoundsPixels(bubble).Height;
            bubble.Hide();
            FlushDispatcher();

            // Act
            ShowMessage(pet, bubble, LongText);

            // Assert
            MonitorHelper.GetWindowBoundsPixels(bubble).Height.Should().BeGreaterThan(heightBefore, "the long message wraps to more lines");
            AssertEndsAboveSprite(pet, bubble, spriteTopPixel);
        });
    }

    /// <summary>
    /// A message replaced by a longer one before the bubble has ever been shown still ends above the sprite.
    /// </summary>
    [Fact]
    public void PositionAbove_ContentReplacedBeforeFirstShow_BubbleEndsAboveTheSprite()
    {
        Run((pet, bubble, spriteTopPixel) =>
        {
            // Arrange
            bubble.SetContent(new BubbleContent(ShortText, []));
            bubble.PositionAbove(pet, SpriteTop);

            // Act
            ShowMessage(pet, bubble, LongText);

            // Assert
            AssertEndsAboveSprite(pet, bubble, spriteTopPixel);
        });
    }

    /// <summary>
    /// A size change that does not come from a new message, here a bigger font, moves the bubble back above the sprite.
    /// </summary>
    [Fact]
    public void SizeChanged_FontGrowsWhileVisible_BubbleStillEndsAboveTheSprite()
    {
        Run((pet, bubble, spriteTopPixel) =>
        {
            // Arrange
            ShowMessage(pet, bubble, LongText);
            var heightBefore = MonitorHelper.GetWindowBoundsPixels(bubble).Height;
            var text = (TextBlock)bubble.FindName("MessageText");

            // Act
            text.FontSize *= 2;
            FlushDispatcher();

            // Assert
            MonitorHelper.GetWindowBoundsPixels(bubble).Height.Should().BeGreaterThan(heightBefore, "the bigger font needs more room");
            AssertEndsAboveSprite(pet, bubble, spriteTopPixel);
        });
    }
}
