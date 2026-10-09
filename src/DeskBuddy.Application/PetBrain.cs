using DeskBuddy.Application.Abstractions;
using DeskBuddy.Domain;
using DeskBuddy.Domain.Behavior;

namespace DeskBuddy.Application;

/// <summary>
/// Gathers sensor input and user interactions and drives the pet's behavior state machine. This is the single place
/// the UI talks to for "what should the pet be doing"; further sensors (system load, reminders) plug in here.
/// </summary>
/// <param name="idleDetector">
/// Reports how long the user has been inactive.
/// </param>
/// <param name="stateMachine">
/// The behavior state machine to drive.
/// </param>
public sealed class PetBrain(IIdleDetector idleDetector, PetBehaviorStateMachine stateMachine)
{
    /// <summary>
    /// Gets the current state.
    /// </summary>
    public PetState State => stateMachine.State;

    /// <summary>
    /// Gets the direction of the current or most recent walk: <c>-1</c> for left, <c>1</c> for right.
    /// </summary>
    public int WalkDirection => stateMachine.WalkDirection;

    /// <summary>
    /// Reads the sensors and advances the state machine. Call periodically.
    /// </summary>
    /// <returns>
    /// The state after the update.
    /// </returns>
    public PetState Tick()
    {
        return stateMachine.Update(idleDetector.GetIdleTime());
    }

    /// <summary>
    /// Notifies the brain that the user started dragging the pet.
    /// </summary>
    public void OnDragStarted()
    {
        stateMachine.OnDragStarted();
    }

    /// <summary>
    /// Notifies the brain that the user dropped the pet.
    /// </summary>
    public void OnDragEnded()
    {
        stateMachine.OnDragEnded();
    }

    /// <summary>
    /// Notifies the brain that the user clicked the pet.
    /// </summary>
    public void OnClicked()
    {
        stateMachine.OnClicked();
    }
}
