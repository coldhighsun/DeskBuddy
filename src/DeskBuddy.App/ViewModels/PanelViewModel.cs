namespace DeskBuddy.App.ViewModels;

/// <summary>
/// Groups the view models of the panel window's three tabs.
/// </summary>
/// <param name="Pomodoro">
/// The pomodoro tab.
/// </param>
/// <param name="Todo">
/// The to-do tab.
/// </param>
/// <param name="Note">
/// The note tab.
/// </param>
public sealed record PanelViewModel(PomodoroTabViewModel Pomodoro, TodoTabViewModel Todo, NoteTabViewModel Note);
