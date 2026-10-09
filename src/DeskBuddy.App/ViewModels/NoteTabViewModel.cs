using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DeskBuddy.App.Resources;
using DeskBuddy.Application;

namespace DeskBuddy.App.ViewModels;

/// <summary>
/// Backs the note tab. Typing is handed to <see cref="NoteService"/>, which saves shortly after the user stops.
/// </summary>
public sealed partial class NoteTabViewModel : ObservableObject
{
    /// <summary>
    /// The service that owns and saves the note.
    /// </summary>
    private readonly NoteService _service;

    /// <summary>
    /// The UI thread's context, used to bring background save failures back to the UI.
    /// </summary>
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;

    /// <summary>
    /// The note text shown in the box.
    /// </summary>
    [ObservableProperty]
    private string _text;

    /// <summary>
    /// A problem to show to the user, or empty if there is none.
    /// </summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoteTabViewModel"/> class and shows the current note.
    /// </summary>
    /// <param name="service">
    /// The service that owns and saves the note.
    /// </param>
    public NoteTabViewModel(NoteService service)
    {
        _service = service;
        _text = service.Text;
        _service.SaveFailed += OnSaveFailed;

        if (service.LoadFailure is { } failure)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, Strings.NoteLoadFailed, failure.Message);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the note can be edited (it cannot if loading failed at startup).
    /// </summary>
    public bool IsEditable => _service.LoadFailure is null;

    /// <summary>
    /// Passes an edit on to the service and clears any earlier save error.
    /// </summary>
    /// <param name="value">
    /// The new text.
    /// </param>
    partial void OnTextChanged(string value)
    {
        if (!IsEditable)
        {
            return;
        }

        _service.SetText(value);
        ErrorMessage = string.Empty;
    }

    /// <summary>
    /// Shows a failed save to the user. Safe to call from any thread.
    /// </summary>
    /// <param name="exception">
    /// Why the save failed.
    /// </param>
    public void ReportSaveFailure(Exception exception)
    {
        var message = string.Format(CultureInfo.CurrentCulture, Strings.NoteSaveFailed, exception.Message);
        if (_uiContext is null)
        {
            ErrorMessage = message;
            return;
        }

        _uiContext.Post(_ => ErrorMessage = message, null);
    }

    /// <summary>
    /// Shows a failed background save.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="exception">
    /// Why the save failed.
    /// </param>
    private void OnSaveFailed(object? sender, Exception exception)
    {
        ReportSaveFailure(exception);
    }
}
