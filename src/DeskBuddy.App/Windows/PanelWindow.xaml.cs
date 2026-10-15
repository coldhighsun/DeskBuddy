using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DeskBuddy.App.ViewModels;

namespace DeskBuddy.App.Windows;

/// <summary>
/// The panel window with the settings tabs. Closing it only hides it, so it keeps its state and
/// opens instantly next time.
/// </summary>
public partial class PanelWindow
{
    /// <summary>
    /// How often the pet previews are redrawn; the fastest animations run at 8 pictures a second.
    /// </summary>
    private static readonly TimeSpan PreviewInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// The view models behind the tabs.
    /// </summary>
    private readonly PanelViewModel _viewModel;

    /// <summary>
    /// Whether the application is exiting, in which case closing really closes the window.
    /// </summary>
    private bool _isExiting;

    /// <summary>
    /// Moves the pet previews on while the pets tab is showing.
    /// </summary>
    private readonly DispatcherTimer _previewTimer = new(DispatcherPriority.Render)
    {
        Interval = PreviewInterval,
    };

    /// <summary>
    /// Measures the real time between two ticks of <see cref="_previewTimer"/>, as timer intervals are not exact.
    /// </summary>
    private readonly Stopwatch _previewWatch = new();

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
        Tabs.SelectionChanged += OnTabsSelectionChanged;
        _previewTimer.Tick += OnPreviewTick;
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
            if (_viewModel.Settings.HasUnsavedChanges)
            {
                ReportFaults(AskBeforeHidingAsync());
            }
            else
            {
                Hide();
            }
        }

        base.OnClosing(e);
    }

    /// <summary>
    /// Asks what to do with unsaved changes when the panel is closed: save them, drop them or keep the panel open. The
    /// panel stays open if saving is not possible, so the user can see what is wrong.
    /// </summary>
    /// <returns>
    /// A task that completes when the question is answered and acted on.
    /// </returns>
    private async Task AskBeforeHidingAsync()
    {
        if (!_viewModel.Settings.CanSave)
        {
            // Some entry cannot be saved, so there is nothing to offer but to drop the changes or to keep fixing them.
            var discard = MessageBox.Show(
                this,
                Strings.UnsavedInvalidCloseMessage,
                Strings.AppName,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (discard == MessageBoxResult.Yes)
            {
                _viewModel.Settings.Revert();
                Hide();
            }

            return;
        }

        var answer = MessageBox.Show(
            this,
            Strings.UnsavedCloseMessage,
            Strings.AppName,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        switch (answer)
        {
            case MessageBoxResult.Yes:
                if (await _viewModel.Settings.TrySaveAsync())
                {
                    Hide();
                }

                break;

            case MessageBoxResult.No:
                _viewModel.Settings.Revert();
                Hide();
                break;
        }
    }

    /// <summary>
    /// Hands an error of work started without waiting for it to the dispatcher, so it reaches the handler for unhandled
    /// errors instead of vanishing with the unobserved task.
    /// </summary>
    /// <param name="work">
    /// The work that was started.
    /// </param>
    private void ReportFaults(Task work)
    {
        _ = work.ContinueWith(
            task => Dispatcher.BeginInvoke(() => ExceptionDispatchInfo.Capture(task.Exception!.GetBaseException()).Throw()),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
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
                _viewModel.Settings.SetBossKey(gesture);
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

        UpdatePreviews();
    }

    /// <summary>
    /// Picks the pet whose card was clicked, so its preview starts moving. The use button handles its own click, so
    /// pressing it does not get here.
    /// </summary>
    /// <param name="sender">
    /// The card.
    /// </param>
    /// <param name="e">
    /// Mouse event data.
    /// </param>
    private void OnPetCardMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PetCardViewModel card })
        {
            _viewModel.Pets.SelectCard(card);
        }
    }

    /// <summary>
    /// Starts or stops the pet previews when another tab is chosen.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Selection events of the lists inside the tabs bubble up to the tab control; only its own matter here.
        if (ReferenceEquals(e.OriginalSource, Tabs))
        {
            SettingsBar.Visibility = Tabs.SelectedIndex == (int)PanelTab.Pets ? Visibility.Collapsed : Visibility.Visible;
            UpdatePreviews();
        }
    }

    /// <summary>
    /// Runs the pet previews only while the pets tab is in view, so the panel costs nothing when it is hidden or on
    /// another tab, and looks for new pets each time the tab comes into view.
    /// </summary>
    private void UpdatePreviews()
    {
        var shouldRun = IsVisible && Tabs.SelectedIndex == (int)PanelTab.Pets;
        if (shouldRun == _previewTimer.IsEnabled)
        {
            return;
        }

        if (!shouldRun)
        {
            _previewTimer.Stop();
            _previewWatch.Reset();
            return;
        }

        _viewModel.Pets.RestartPreviews();
        _previewWatch.Restart();
        _previewTimer.Start();
        _ = RefreshPetsAsync();
    }

    /// <summary>
    /// Looks for pets again.
    /// </summary>
    /// <returns>
    /// A task that completes when the list is up to date.
    /// </returns>
    private async Task RefreshPetsAsync()
    {
        await _viewModel.Pets.RefreshAsync();
    }

    /// <summary>
    /// Moves the pet previews on by the time that really passed.
    /// </summary>
    /// <param name="sender">
    /// The event source.
    /// </param>
    /// <param name="e">
    /// Event data.
    /// </param>
    private void OnPreviewTick(object? sender, EventArgs e)
    {
        var delta = _previewWatch.Elapsed;
        _previewWatch.Restart();
        _viewModel.Pets.Tick(delta);
    }
}