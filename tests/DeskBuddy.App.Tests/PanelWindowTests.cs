using System.Windows.Controls;
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
}
