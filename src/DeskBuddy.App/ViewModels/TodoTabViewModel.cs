using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskBuddy.App.Resources;
using DeskBuddy.Application;
using DeskBuddy.Domain.Todos;

namespace DeskBuddy.App.ViewModels;

/// <summary>
/// One row of the to-do list as the view sees it.
/// </summary>
/// <param name="Id">
/// The entry's identifier.
/// </param>
/// <param name="Title">
/// What needs doing.
/// </param>
/// <param name="IsDone">
/// Whether the entry is completed.
/// </param>
public sealed record TodoRowViewModel(Guid Id, string Title, bool IsDone);

/// <summary>
/// Backs the to-do tab. Failures to save are shown in <see cref="ErrorMessage"/> instead of being thrown into the UI.
/// </summary>
public sealed partial class TodoTabViewModel : ObservableObject
{
    /// <summary>
    /// The service that owns and saves the list.
    /// </summary>
    private readonly TodoService _service;

    /// <summary>
    /// The text of the box for a new to-do.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    private string _newTitle = string.Empty;

    /// <summary>
    /// A problem to show to the user, or empty if there is none.
    /// </summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Whether the list currently has no entries, which shows the hint instead.
    /// </summary>
    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>
    /// Whether any entry is completed, which enables the clear button.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearCompletedCommand))]
    private bool _hasCompleted;

    /// <summary>
    /// Initializes a new instance of the <see cref="TodoTabViewModel"/> class and shows the current list.
    /// </summary>
    /// <param name="service">
    /// The service that owns and saves the list.
    /// </param>
    public TodoTabViewModel(TodoService service)
    {
        _service = service;
        _service.Changed += (_, _) => Reload();

        if (service.LoadFailure is { } failure)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, Strings.TodoLoadFailed, failure.Message);
        }

        Reload();
    }

    /// <summary>
    /// Gets the rows to display, in display order.
    /// </summary>
    public ObservableCollection<TodoRowViewModel> Rows { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the list can be changed (it cannot if loading failed at startup).
    /// </summary>
    public bool IsEditable => _service.LoadFailure is null;

    /// <summary>
    /// Adds the typed to-do.
    /// </summary>
    /// <returns>
    /// A task that completes when the entry has been added and saved.
    /// </returns>
    [RelayCommand(CanExecute = nameof(CanAdd))]
    private Task AddAsync()
    {
        var title = NewTitle;
        NewTitle = string.Empty;
        return RunAsync(() => _service.AddAsync(title));
    }

    /// <summary>
    /// Completes or reopens an entry.
    /// </summary>
    /// <param name="row">
    /// The row that was clicked.
    /// </param>
    /// <returns>
    /// A task that completes when the change has been saved.
    /// </returns>
    [RelayCommand]
    private Task ToggleAsync(TodoRowViewModel row)
    {
        return RunAsync(() => _service.ToggleAsync(row.Id));
    }

    /// <summary>
    /// Deletes an entry.
    /// </summary>
    /// <param name="row">
    /// The row to delete.
    /// </param>
    /// <returns>
    /// A task that completes when the change has been saved.
    /// </returns>
    [RelayCommand]
    private Task RemoveAsync(TodoRowViewModel row)
    {
        return RunAsync(() => _service.RemoveAsync(row.Id));
    }

    /// <summary>
    /// Deletes all completed entries.
    /// </summary>
    /// <returns>
    /// A task that completes when the change has been saved.
    /// </returns>
    [RelayCommand(CanExecute = nameof(HasCompleted))]
    private Task ClearCompletedAsync()
    {
        return RunAsync(() => _service.ClearCompletedAsync());
    }

    /// <summary>
    /// Tells whether the typed text can be added.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the list is editable and the box holds something other than whitespace.
    /// </returns>
    private bool CanAdd()
    {
        return IsEditable && !string.IsNullOrWhiteSpace(NewTitle);
    }

    /// <summary>
    /// Runs a change and shows a storage failure to the user. The list is reloaded afterwards so the view always
    /// matches the service, even when a save failed.
    /// </summary>
    /// <param name="change">
    /// The change to run.
    /// </param>
    /// <returns>
    /// A task that completes when the change has finished.
    /// </returns>
    private async Task RunAsync(Func<Task> change)
    {
        try
        {
            await change();
            ErrorMessage = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, Strings.TodoSaveFailed, ex.Message);
        }

        Reload();
    }

    /// <summary>
    /// Rebuilds the rows from the service.
    /// </summary>
    private void Reload()
    {
        Rows.Clear();
        foreach (var item in _service.Items)
        {
            Rows.Add(new TodoRowViewModel(item.Id, item.Title, item.IsDone));
        }

        IsEmpty = Rows.Count == 0;
        HasCompleted = Rows.Any(row => row.IsDone);
    }
}
