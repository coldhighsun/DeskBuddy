using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using DeskBuddy.App.Windows;

namespace DeskBuddy.App;

/// <summary>
/// The part of the tray icon service that builds the context menu.
/// </summary>
public sealed partial class TrayIconService
{
    /// <summary>
    /// Wraps a picture for a menu entry in a frame that is drawn in the accent color for the entry in use and invisible
    /// for the others, so every picture takes the same room.
    /// </summary>
    /// <param name="picture">The picture.</param>
    /// <param name="width">The width of the picture.</param>
    /// <param name="height">The height of the picture.</param>
    /// <param name="isCurrent">Whether the entry is the one in use.</param>
    /// <returns>The framed picture.</returns>
    private static Border Framed(ImageSource? picture, double width, double height, bool isCurrent)
    {
        var frame = new Border
        {
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent,
            CornerRadius = new CornerRadius(6),
            Child = new System.Windows.Controls.Image { Source = picture, Width = width, Height = height },
        };
        if (isCurrent)
        {
            frame.SetResourceReference(Border.BorderBrushProperty, "AppPrimary");
        }

        return frame;
    }

    /// <summary>
    /// Moves the submenu of an item so that its first entry is level with the item. The theme opens it higher than that,
    /// because the popup has a margin, a border and padding above its entries; the offset is worked out from the
    /// submenu's own template, so nothing depends on how big the theme makes them. A template without the parts the
    /// offset is worked out from is left as it is.
    /// </summary>
    /// <param name="item">The item that has the submenu.</param>
    /// <param name="maxHeight">Gives the most height the submenu may take before it scrolls, asked each time it loads; <see langword="null"/> for no limit.</param>
    private static void AlignSubmenu(MenuItem item, Func<double>? maxHeight = null)
    {
        item.Loaded += (_, _) =>
        {
            if (item.Template.FindName("PART_Popup", item) is Popup popup && item.Template.FindName("SubmenuBorder", item) is Border border)
            {
                popup.VerticalOffset = -(border.Margin.Top + border.BorderThickness.Top + border.Padding.Top);
            }

            // Asked each time the menu opens, so a change of resolution, scaling or taskbar since it was built is respected.
            if (maxHeight is not null && item.Template.FindName("SubmenuScroll", item) is ScrollViewer scroll)
            {
                scroll.MaxHeight = maxHeight();
            }
        };
    }

    /// <summary>
    /// Builds the menu again if what it shows changed, and it is not open.
    /// </summary>
    private void RebuildIfNeeded()
    {
        if (_rebuildPolicy.ShouldRebuild(CurrentState(), _icon.ContextMenu!.IsOpen))
        {
            Refresh();
        }
    }

    /// <summary>
    /// Gets what the menu has to show now.
    /// </summary>
    /// <returns>The state of the menu.</returns>
    private TrayMenuState CurrentState()
    {
        var settings = _settings.Current;
        return new TrayMenuState(settings.PetName,
            settings.Size,
            _brain.Visibility.IsManuallyHidden,
            _pets.Version,
            _updates is { CanInstall: true, Available: { } found } ? found.Version.ToString() : null,
            Strings.Current.Name);
    }

    /// <summary>
    /// Builds the submenu that lists the pets as a grid of their pictures, so it grows wider before it grows too tall; the
    /// picture of the pet in use has a frame in the accent color. The entries have no text, only a tooltip, except for a
    /// pet that has no picture to show: it is listed by its name on a row of its own, checked if it is the pet in use.
    /// Below the grid, on rows of their own, are entries that change to a random pet and that open the panel's list of pets.
    /// The submenu scrolls if the grid is still too tall for the screen.
    /// </summary>
    /// <param name="state">What the menu has to show.</param>
    /// <returns>The submenu item.</returns>
    private MenuItem BuildPetsMenu(TrayMenuState state)
    {
        var menu = new MenuItem { Header = Strings.TraySwitchPet, Icon = Glyph("") };
        var choices = TrayMenuModel.PetChoices(_pets.Entries, state.PetName);
        var gridWidth = TrayMenuModel.GridColumns(choices.Count) * PetGridCellWidth;
        var wrap = new FrameworkElementFactory(typeof(WrapPanel));
        wrap.SetValue(FrameworkElement.WidthProperty, gridWidth);
        menu.ItemsPanel = new ItemsPanelTemplate(wrap);

        foreach (var choice in choices)
        {
            var item = new MenuItem { ToolTip = choice.DisplayName };
            AutomationProperties.SetName(item, choice.DisplayName);
            if (choice.Picture is null)
            {
                item.Header = choice.DisplayName;
                item.IsChecked = choice.IsCurrent;
                item.Width = gridWidth;
            }
            else
            {
                item.Icon = Framed(choice.Picture, PetPictureEdge, PetPictureEdge, choice.IsCurrent);
                item.Padding = new Thickness(PetGridCellPadding);
                item.Width = PetGridCellWidth;
            }

            item.Click += async (_, _) => await ChangeSettingsAsync(settings => settings with { PetName = choice.FolderName });
            menu.Items.Add(item);
        }

        menu.IsEnabled = menu.Items.Count > 0;
        if (menu.IsEnabled)
        {
            menu.Items.Add(new Separator { Width = gridWidth });

            var random = new MenuItem { Header = Strings.TraySwitchRandom, Icon = Glyph(""), Width = gridWidth, IsEnabled = TrayMenuModel.HasAnotherPet(_pets.Entries, state.PetName) };
            random.Click += async (_, _) => await SwitchToRandomPetAsync();
            menu.Items.Add(random);

            var all = new MenuItem { Header = Strings.TrayAllPets, Icon = Glyph(""), Width = gridWidth };
            all.Click += (_, _) => _openPanel(PanelTab.Pets);
            menu.Items.Add(all);
        }

        AlignSubmenu(menu, static () => SystemParameters.WorkArea.Height * PetGridMaxHeightShare);
        return menu;
    }

    /// <summary>
    /// Changes to a pet chosen at random from the installed ones, other than the pet in use; does nothing if there is none.
    /// It is the end of an <see langword="async"/> event handler, so it does not let any exception go.
    /// </summary>
    /// <returns>A task that completes when the change has been made.</returns>
    private async Task SwitchToRandomPetAsync()
    {
        if (TrayMenuModel.PickRandom(_pets.Entries, _settings.Current.PetName, Random.Shared) is { } pick)
        {
            await ChangeSettingsAsync(settings => settings with { PetName = pick });
        }
    }

    /// <summary>
    /// Builds the submenu that changes the pet size. Each entry shows the pet in use at the size the entry gives it on
    /// screen; the picture of the size in use has a frame in the accent color. The entries have no text, only a tooltip.
    /// </summary>
    /// <param name="state">What the menu has to show.</param>
    /// <returns>The submenu item.</returns>
    private MenuItem BuildSizeMenu(TrayMenuState state)
    {
        var menu = new MenuItem { Header = Strings.TrayPetSize, Icon = Glyph("") };
        foreach (var choice in PetSizeChoices.For(_pets.Entries, state.PetName, state.Size))
        {
            var label = choice.Label;
            var item = new MenuItem { ToolTip = label, IsChecked = choice.Picture is null && choice.IsCurrent };
            AutomationProperties.SetName(item, label);
            if (choice.Picture is null)
            {
                item.Header = label;
            }
            else
            {
                item.Icon = Framed(choice.Picture, choice.Width, choice.Height, choice.IsCurrent);
            }

            item.Click += async (_, _) => await ChangeSettingsAsync(settings => settings with { Size = choice.Size });
            menu.Items.Add(item);
        }

        AlignSubmenu(menu);
        return menu;
    }

    /// <summary>
    /// Saves a settings change made from the menu; a failure to save is reported as a notice. It is the end of an
    /// <see langword="async"/> event handler, so it does not let any exception go.
    /// </summary>
    /// <param name="change">Produces the new settings from the current ones.</param>
    /// <returns>A task that completes when the change has been saved.</returns>
    private async Task ChangeSettingsAsync(Func<PetSettings, PetSettings> change)
    {
        try
        {
            await _settings.UpdateAsync(change);
        }
        catch (InvalidOperationException)
        {
            // Raised only after a failed pet load; the panel shows why, so there is nothing to add.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The choice made in the tray menu could not be saved.");
            ShowErrorNotice(Strings.AppName, string.Format(CultureInfo.CurrentCulture, Strings.SettingsSaveFailed, ErrorText.For(ex)));
        }
    }

    /// <summary>
    /// Builds the context menu from whether the user hid the pet, the pet in use and its size.
    /// </summary>
    private void Refresh()
    {
        var state = CurrentState();
        var items = _icon.ContextMenu!.Items;
        items.Clear();

        var openSettings = new MenuItem { Header = Strings.TraySettings, Icon = Glyph("") };
        openSettings.Click += (_, _) => _openPanel(null);
        items.Add(openSettings);

        items.Add(BuildPetsMenu(state));
        items.Add(BuildSizeMenu(state));

        items.Add(new Separator());

        var toggle = new MenuItem { Header = state.PetHidden ? Strings.TrayShowPet : Strings.TrayHidePet, Icon = Glyph("") };
        toggle.Click += (_, _) => ToggleVisibility();
        items.Add(toggle);

        if (state.UpdateVersion is { } version)
        {
            var install = new MenuItem { Header = string.Format(CultureInfo.CurrentCulture, Strings.TrayUpdateTo, version), Icon = Glyph("") };
            install.Click += (_, _) => _ = RunUpdateAsync(InstallUpdateAsync);
            items.Add(install);
        }

        var exit = new MenuItem { Header = Strings.TrayExit, Icon = Glyph("") };
        exit.Click += (_, _) => _onExit();
        items.Add(exit);

        _rebuildPolicy.MarkBuilt(state);
    }

    /// <summary>
    /// Makes the icon of a menu item from a glyph of the Segoe Fluent Icons font (Segoe MDL2 Assets before Windows 11).
    /// </summary>
    /// <param name="glyph">The character of the glyph.</param>
    /// <returns>A text block showing the glyph, in the color of the menu text.</returns>
    private static TextBlock Glyph(string glyph)
    {
        return new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }
}
