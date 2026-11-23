using System.Windows.Threading;
using DeskBuddy.App.Windows;

namespace DeskBuddy.App.Tests.Windows;

/// <summary>
/// Tests for <see cref="PetVisibilityControl"/>, which hands requests over to the pet's thread and reports the change on
/// the caller's. The pet's thread is stood in for by a queue the test runs by hand.
/// </summary>
public sealed class PetVisibilityControlTests
{
    /// <summary>
    /// A toggle is handed over to the pet's thread and not carried out on the spot.
    /// </summary>
    [Fact]
    public void ToggleManuallyHidden_Called_IsOnlyQueuedForThePetThread()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var brain = TestBrain.Create();
            var queue = new List<Action>();
            using var control = new PetVisibilityControl(queue.Add, brain, Dispatcher.CurrentDispatcher);

            // Act
            control.ToggleManuallyHidden();

            // Assert
            queue.Should().HaveCount(1);
            control.IsManuallyHidden.Should().BeFalse();
        });
    }

    /// <summary>
    /// Once the pet's thread runs the request, the pet is hidden, and the control says so from any thread.
    /// </summary>
    [Fact]
    public void ToggleManuallyHidden_QueuedWorkRuns_HidesThePet()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var brain = TestBrain.Create();
            var queue = new List<Action>();
            using var control = new PetVisibilityControl(queue.Add, brain, Dispatcher.CurrentDispatcher);
            control.ToggleManuallyHidden();

            // Act
            queue.ForEach(action => action());

            // Assert
            control.IsManuallyHidden.Should().BeTrue();
        });
    }

    /// <summary>
    /// A request to hide or show is handed over with what was asked for.
    /// </summary>
    [Fact]
    public void SetManuallyHidden_HiddenThenShown_FollowsTheRequests()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var brain = TestBrain.Create();
            var queue = new List<Action>();
            using var control = new PetVisibilityControl(queue.Add, brain, Dispatcher.CurrentDispatcher);

            // Act
            control.SetManuallyHidden(true);
            queue.ForEach(action => action());
            var hidden = control.IsManuallyHidden;
            queue.Clear();
            control.SetManuallyHidden(false);
            queue.ForEach(action => action());

            // Assert
            hidden.Should().BeTrue();
            control.IsManuallyHidden.Should().BeFalse();
        });
    }

    /// <summary>
    /// A locked session puts the pet away, and unlocking brings it back.
    /// </summary>
    [Fact]
    public void SetSessionLocked_LockedThenUnlocked_HidesAndShowsThePet()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var brain = TestBrain.Create();
            var queue = new List<Action>();
            using var control = new PetVisibilityControl(queue.Add, brain, Dispatcher.CurrentDispatcher);

            // Act
            control.SetSessionLocked(true);
            queue.ForEach(action => action());
            var whileLocked = brain.Visibility.IsVisible;
            queue.Clear();
            control.SetSessionLocked(false);
            queue.ForEach(action => action());

            // Assert
            whileLocked.Should().BeFalse();
            brain.Visibility.IsVisible.Should().BeTrue();
        });
    }

    /// <summary>
    /// The change is reported on the caller's thread, although the brain raises it on the pet's.
    /// </summary>
    [Fact]
    public void VisibilityChanged_BrainChangesOnAnotherThread_IsRaisedOnTheCallerThread()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var brain = TestBrain.Create();
            var callerThread = Environment.CurrentManagedThreadId;
            var raisedOn = new List<int>();
            using var control = new PetVisibilityControl(action => action(), brain, Dispatcher.CurrentDispatcher);
            control.VisibilityChanged += (_, _) => raisedOn.Add(Environment.CurrentManagedThreadId);

            // Act: the pet's thread hides the pet.
            var petThread = new Thread(() => brain.SetManuallyHidden(true));
            petThread.Start();
            petThread.Join();
            raisedOn.Should().BeEmpty("the report is queued for the caller's thread");
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, () => { });

            // Assert
            raisedOn.Should().Equal(callerThread);
        });
    }

    /// <summary>
    /// A disposed control reports nothing more, not even a change that was already on its way.
    /// </summary>
    [Fact]
    public void VisibilityChanged_AfterDispose_IsNotRaised()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var brain = TestBrain.Create();
            var control = new PetVisibilityControl(action => action(), brain, Dispatcher.CurrentDispatcher);
            var raised = 0;
            control.VisibilityChanged += (_, _) => raised++;
            brain.SetManuallyHidden(true);

            // Act
            control.Dispose();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, () => { });
            brain.SetManuallyHidden(false);
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, () => { });

            // Assert
            raised.Should().Be(0);
        });
    }
}
