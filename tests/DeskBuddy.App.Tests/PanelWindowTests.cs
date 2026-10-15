using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeskBuddy.App.Rendering;
using DeskBuddy.App.Resources;
using DeskBuddy.App.ViewModels;
using DeskBuddy.App.Windows;
using DeskBuddy.Infrastructure.Pets;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests that build the panel window over the pets shipped in the repository, to catch markup and binding mistakes
/// that the view models' own tests cannot see.
/// </summary>
public class PanelWindowTests
{
    /// <summary>
    /// The panel has the pets tab first, and every shipped pet gets a card.
    /// </summary>
    [Fact]
    public void Constructor_ShippedPets_ShowsPetsTabFirstWithACardPerPet()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var petsDirectory = RepositoryPaths.Pets;
            var settings = new SettingsService(new FakeSettingsStore());
            settings.InitializeAsync().GetAwaiter().GetResult();
            var loader = new FolderPetLoader(NullLogger<FolderPetLoader>.Instance);
            var switcher = new PetSwitcher(
                settings,
                loader,
                petsDirectory,
                settings.Current.PetName,
                _ => { },
                NullLogger<PetSwitcher>.Instance);
            var pets = new PetsTabViewModel(
                settings,
                new FolderPetCatalog(petsDirectory),
                loader,
                petsDirectory,
                switcher,
                new PetPreviewFrames().Get);
            var bossKey = new BossKeyController(settings, new FakeHotkeyService(), TestBrain.Create());
            var panel = new PanelViewModel(
                new SettingsTabViewModel(settings, bossKey, new FakeAutoStartService(), () => { }),
                pets);
            pets.RefreshAsync().GetAwaiter().GetResult();

            // Act
            var window = new PanelWindow(panel);

            // Assert
            var tabs = (TabControl)window.FindName("Tabs");
            tabs.Items.Cast<TabItem>().Select(tab => tab.Header).First().Should().Be(Strings.TabPets);
            tabs.Items.Count.Should().Be(Enum.GetValues<PanelTab>().Length);
            tabs.SelectedIndex.Should().Be((int)PanelTab.Pets);
            window.Close();
        });
    }

    /// <summary>
    /// The size buttons of the appearance tab show the pet in use at each size with a picture, mark the size in use, and
    /// pressing one chooses that size and moves the mark.
    /// </summary>
    [Fact]
    public void AppearanceTab_SizeButtons_ShowPetMarkSizeInUseAndChooseSize()
    {
        StaThread.Run(() =>
        {
            // Arrange
            var petsDirectory = RepositoryPaths.Pets;
            var settings = new SettingsService(new FakeSettingsStore());
            settings.InitializeAsync().GetAwaiter().GetResult();
            var loader = new FolderPetLoader(NullLogger<FolderPetLoader>.Instance);
            var previewFrames = new PetPreviewFrames();
            var installed = new InstalledPets(new FolderPetCatalog(petsDirectory), loader, petsDirectory, previewFrames.Get);
            installed.RefreshAsync().GetAwaiter().GetResult();
            var switcher = new PetSwitcher(
                settings,
                loader,
                petsDirectory,
                settings.Current.PetName,
                _ => { },
                NullLogger<PetSwitcher>.Instance);
            var pets = new PetsTabViewModel(settings, new FolderPetCatalog(petsDirectory), loader, petsDirectory, switcher, previewFrames.Get);
            var bossKey = new BossKeyController(settings, new FakeHotkeyService(), TestBrain.Create());
            var settingsTab = new SettingsTabViewModel(settings, bossKey, new FakeAutoStartService(), () => { }, installed);
            var window = new PanelWindow(new PanelViewModel(settingsTab, pets));

            // The application gives the panel the Fluent theme, whose styles are the ones that decide how the buttons are laid out.
            using var theme = new ThemeService(new ResourceDictionary(), Dispatcher.CurrentDispatcher, settings, NullLogger<ThemeService>.Instance);
            theme.Register(window);
            ((TabControl)window.FindName("Tabs")).SelectedIndex = (int)PanelTab.Appearance;
            window.Show();
            window.UpdateLayout();

            // Act
            var buttons = SizeButtons(window);
            var markedAtStart = buttons.Select(button => button.IsChecked == true).ToList();
            var picturesAtStart = buttons.Select(button => Descendants<Image>(button).Single().Width).ToList();
            var widthsAtStart = buttons.Select(button => button.ActualWidth).ToList();
            buttons[2].Command.Execute(buttons[2].CommandParameter);
            window.UpdateLayout();
            var markedAfter = SizeButtons(window).Select(button => button.IsChecked == true).ToList();

            // Assert
            buttons.Should().HaveCount(3);
            markedAtStart.Should().Equal(false, true, false);
            picturesAtStart.Should().OnlyContain(width => width > 0);
            picturesAtStart.Should().BeInAscendingOrder();

            // Each button is as wide as its own picture needs, not as wide as the biggest one.
            widthsAtStart.Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
            widthsAtStart.Sum().Should().BeLessThan(picturesAtStart.Sum() + (3 * 40));
            settingsTab.SelectedSize.Should().Be(PetSize.Large);
            markedAfter.Should().Equal(false, false, true);
            window.Close();
        });
    }

    /// <summary>
    /// Gets the buttons the size list of the appearance tab shows.
    /// </summary>
    /// <param name="window">
    /// The panel window, with the appearance tab shown.
    /// </param>
    /// <returns>
    /// The buttons, in the order of the sizes.
    /// </returns>
    private static List<RadioButton> SizeButtons(Window window)
    {
        var list = Descendants<ItemsControl>(window).Single(control => AutomationProperties.GetAutomationId(control) == "PetSizeList");
        return Descendants<RadioButton>(list);
    }

    /// <summary>
    /// Finds the elements of a type below an element in the visual tree, in the order they are shown.
    /// </summary>
    /// <typeparam name="T">
    /// The type of element to find.
    /// </typeparam>
    /// <param name="root">
    /// The element to search below.
    /// </param>
    /// <returns>
    /// The elements found.
    /// </returns>
    private static List<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        var found = new List<T>();
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                found.Add(match);
            }

            found.AddRange(Descendants<T>(child));
        }

        return found;
    }
}
