using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using DeskBuddy.App.ViewModels;
using DeskBuddy.Application;

namespace DeskBuddy.App.Windows;

/// <summary>
/// The panel window with the pomodoro, to-do and note tabs. Closing it only hides it, so it keeps its state and
/// opens instantly next time.
/// </summary>
public partial class PanelWindow : Window
{
    /// <summary>
    /// How often the pomodoro countdown is refreshed while the panel is visible.
    /// </summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The view models behind the tabs.
    /// </summary>
    private readonly PanelViewModel _viewModel;

    /// <summary>
    /// The service that saves the note, flushed whenever the panel is hidden.
    /// </summary>
    private readonly NoteService _noteService;

    /// <summary>
    /// Refreshes the pomodoro countdown. Runs only while the panel is visible, so a hidden panel costs no CPU.
    /// </summary>
    private readonly DispatcherTimer _refreshTimer = new() { Interval = RefreshInterval };

    /// <summary>
    /// Whether the application is exiting, in which case closing really closes the window.
    /// </summary>
    private bool _isExiting;

    /// <summary>
    /// Initializes a new instance of the <see cref="PanelWindow"/> class.
    /// </summary>
    /// <param name="viewModel">
    /// The view models behind the tabs.
    /// </param>
    /// <param name="noteService">
    /// The service that saves the note.
    /// </param>
    public PanelWindow(PanelViewModel viewModel, NoteService noteService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _noteService = noteService;
        DataContext = viewModel;

        _refreshTimer.Tick += (_, _) => _viewModel.Pomodoro.Refresh();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    /// <summary>
    /// Brings the panel to the front, restoring it if it was hidden or minimized.
    /// </summary>
    public void ShowPanel()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
    }

    /// <summary>
    /// Tells the window the application is exiting, so closing it no longer just hides it.
    /// </summary>
    public void PrepareToExit()
    {
        _isExiting = true;
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    /// <summary>
    /// Runs the countdown refresh only while visible, and saves the note whenever the panel is hidden.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private async void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            _viewModel.Pomodoro.Refresh();
            _refreshTimer.Start();
            return;
        }

        _refreshTimer.Stop();
        try
        {
            await _noteService.FlushAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _viewModel.Note.ReportSaveFailure(ex);
        }
    }
}
