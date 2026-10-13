using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using DeskBuddy.App.Presentation;
using DeskBuddy.App.ViewModels;

namespace DeskBuddy.App.Windows;

/// <summary>
/// The panel window with the settings tabs. Closing it only hides it, so it keeps its state and
/// opens instantly next time.
/// </summary>
public partial class PanelWindow
{
    /// <summary>
    /// The view models behind the tabs.
    /// </summary>
    private readonly PanelViewModel _viewModel;

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
    public PanelWindow(PanelViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        IsVisibleChanged += OnIsVisibleChanged;
    }

    /// <summary>
    /// Tells the window the application is exiting, so closing it no longer just hides it.
    /// </summary>
    public void PrepareToExit()
    {
        _isExiting = true;
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

    /// <summary>
    /// Refreshes external state when the panel is shown.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            _viewModel.Settings.RefreshExternalState();
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
}