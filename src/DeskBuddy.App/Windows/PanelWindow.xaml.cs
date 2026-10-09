using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.ViewModels;
using DeskBuddy.Application;

namespace DeskBuddy.App.Windows;

/// <summary>
/// The panel window with the pomodoro, to-do, note and settings tabs. Closing it only hides it, so it keeps its state and
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
    /// <param name="tab">
    /// The tab to show; <see langword="null"/> keeps the tab that was showing.
    /// </param>
    public void ShowPanel(PanelTab? tab = null)
    {
        if (tab is { } requested)
        {
            Tabs.SelectedIndex = (int)requested;
        }

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
            _viewModel.Settings.RefreshExternalState();
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

    /// <summary>
    /// Applies a number typed into a settings box when Enter is pressed, instead of waiting for the box to lose focus.
    /// </summary>
    /// <param name="sender">
    /// The text box.
    /// </param>
    /// <param name="e">
    /// Key event data.
    /// </param>
    private void OnSettingsTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox box)
        {
            BindingOperations.GetBindingExpression(box, TextBox.TextProperty)?.UpdateSource();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Records the shortcut the user presses in the boss key box. Escape leaves the box without changing anything.
    /// </summary>
    /// <param name="sender">
    /// The text box.
    /// </param>
    /// <param name="e">
    /// Key event data.
    /// </param>
    private async void OnBossKeyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        if (e.Key == Key.Escape && sender is TextBox box)
        {
            box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            return;
        }

        switch (HotkeyCapture.TryCreate(e.Key, e.SystemKey, Keyboard.Modifiers, out var gesture))
        {
            case HotkeyCaptureResult.Accepted:
                await _viewModel.Settings.ApplyBossKeyAsync(gesture);
                break;
            case HotkeyCaptureResult.Rejected:
                _viewModel.Settings.ReportBossKeyRejected();
                break;
        }
    }
}
