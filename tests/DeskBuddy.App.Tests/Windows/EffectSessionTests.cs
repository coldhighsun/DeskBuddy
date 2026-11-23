using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using DeskBuddy.App.Interop;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.Windows;

namespace DeskBuddy.App.Tests.Windows;

/// <summary>
/// Plays every pet switch animation from start to end with real windows and a made-up pet that changes size, to check that each
/// part runs through, takes the pet out of sight and brings it back.
/// </summary>
public sealed class EffectSessionTests
{
    /// <summary>
    /// How long a part of an animation may take before the test gives up, in seconds.
    /// </summary>
    private const int TimeoutSeconds = 15;

    /// <summary>
    /// Every animation of a pet switch, with a pet that arrives in a different size than the one that leaves (the harder case:
    /// the animation must not assume that the two pets are alike).
    /// </summary>
    /// <returns>The animations and the pet sizes.</returns>
    public static TheoryData<PetSwitchEffect, int, int> Cases()
    {
        var data = new TheoryData<PetSwitchEffect, int, int>();
        foreach (var effect in new[] { PetSwitchEffect.Ufo, PetSwitchEffect.Claw, PetSwitchEffect.Smoke, PetSwitchEffect.Beam })
        {
            data.Add(effect, 80, 160);
        }

        return data;
    }

    /// <summary>
    /// The pet is out of sight after the first part and in sight again after the second, and the animation leaves nothing
    /// of itself on the pet.
    /// </summary>
    /// <param name="effect">The animation to play.</param>
    /// <param name="firstSize">The size in pixels of the pet that leaves.</param>
    /// <param name="secondSize">The size in pixels of the pet that arrives.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void Play_LeaveThenEnter_HidesThePetAndBringsItBack(PetSwitchEffect effect, int firstSize, int secondSize)
    {
        StaThread.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var petWindow = new Window { Width = firstSize, Height = firstSize, ShowInTaskbar = false };
            _ = new WindowInteropHelper(petWindow).EnsureHandle();
            var work = MonitorHelper.GetWorkAreaPixels(petWindow);
            WindowMover.MoveToPixels(petWindow, work.Left + (work.Width / 2), work.Bottom - firstSize);
            var content = new Border();
            var size = firstSize;
            var session = new EffectSession(
                petWindow,
                content,
                PetSwitchEffectFactory.Create(effect)!,
                () => EffectLayout.For(new PixelRect(work.Left + (work.Width / 2), work.Bottom - size, work.Left + (work.Width / 2) + size, work.Bottom), work, 1),
                () => null,
                () => false);
            try
            {
                Complete(session.LeaveAsync());
                var hidden = content.Opacity;

                // The new pet is put in place, of another size, with its feet where the old pet's were.
                size = secondSize;
                Complete(session.EnterAsync());
                var shown = content.Opacity;
                session.Close();

                hidden.Should().Be(0);
                shown.Should().Be(1);
                content.Clip.Should().BeNull();
            }
            finally
            {
                session.Close();
                petWindow.Close();
            }
        });
    }

    /// <summary>
    /// Runs the dispatcher until a part of an animation has finished, and fails if it throws or does not finish.
    /// </summary>
    /// <param name="part">The part of the animation.</param>
    private static void Complete(Task part)
    {
        var frame = new DispatcherFrame();
        _ = part.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(TimeoutSeconds) };
        timeout.Tick += (_, _) => frame.Continue = false;
        timeout.Start();
        if (!part.IsCompleted)
        {
            Dispatcher.PushFrame(frame);
        }

        timeout.Stop();
        part.IsCompleted.Should().BeTrue("the animation must finish");
        part.GetAwaiter().GetResult();
    }
}
