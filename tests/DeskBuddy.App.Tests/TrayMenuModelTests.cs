namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="TrayMenuModel"/> and <see cref="TrayMenuRebuildPolicy"/>.
/// </summary>
public class TrayMenuModelTests
{
    /// <summary>
    /// The pets the tests choose from.
    /// </summary>
    private static readonly InstalledPet[] Pets =
    [
        new("cat", "Cat", null, 64, 64),
        new("robot", "Robot", null, 128, 160),
    ];

    /// <summary>
    /// Only the pet in use is marked as current, and the order of the pets is kept.
    /// </summary>
    [Fact]
    public void PetChoices_PetInUse_MarksOnlyThatPet()
    {
        // Act
        var choices = TrayMenuModel.PetChoices(Pets, "robot");

        // Assert
        choices.Select(choice => choice.FolderName).Should().Equal("cat", "robot");
        choices.Select(choice => choice.IsCurrent).Should().Equal(false, true);
        choices.Select(choice => choice.DisplayName).Should().Equal("Cat", "Robot");
    }

    /// <summary>
    /// When the pet in use is not among the pets, none is marked.
    /// </summary>
    [Fact]
    public void PetChoices_PetInUseMissing_MarksNone()
    {
        // Act
        var choices = TrayMenuModel.PetChoices(Pets, "dog");

        // Assert
        choices.Should().OnlyContain(choice => !choice.IsCurrent);
    }

    /// <summary>
    /// The folder name is compared exactly, as settings store it.
    /// </summary>
    [Fact]
    public void PetChoices_DifferentCase_IsNotTheSamePet()
    {
        // Act
        var choices = TrayMenuModel.PetChoices(Pets, "CAT");

        // Assert
        choices.Should().OnlyContain(choice => !choice.IsCurrent);
    }

    /// <summary>
    /// The first time the menu is asked about, it has to be built.
    /// </summary>
    [Fact]
    public void ShouldRebuild_NeverBuilt_IsTrue()
    {
        // Arrange
        var policy = new TrayMenuRebuildPolicy();

        // Act
        var result = policy.ShouldRebuild(new TrayMenuState("robot", PetSize.Medium, false, 0), menuOpen: false);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// An update becoming installable (or no longer being so) changes the menu, so it is built again.
    /// </summary>
    [Fact]
    public void ShouldRebuild_UpdateVersionChanged_IsTrue()
    {
        // Arrange
        var policy = new TrayMenuRebuildPolicy();
        policy.MarkBuilt(new TrayMenuState("robot", PetSize.Medium, false, 0));

        // Act
        var result = policy.ShouldRebuild(new TrayMenuState("robot", PetSize.Medium, false, 0, "1.2.3"), menuOpen: false);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// A state the menu was built for does not make it be built again.
    /// </summary>
    [Fact]
    public void ShouldRebuild_SameStateAsBuilt_IsFalse()
    {
        // Arrange
        var policy = new TrayMenuRebuildPolicy();
        policy.MarkBuilt(new TrayMenuState("robot", PetSize.Medium, false, 0));

        // Act
        var result = policy.ShouldRebuild(new TrayMenuState("robot", PetSize.Medium, false, 0), menuOpen: false);

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Each thing the menu shows makes it be built again when it changes.
    /// </summary>
    [Theory]
    [InlineData("cat", PetSize.Medium, false, 0)]
    [InlineData("robot", PetSize.Large, false, 0)]
    [InlineData("robot", PetSize.Medium, true, 0)]
    [InlineData("robot", PetSize.Medium, false, 1)]
    public void ShouldRebuild_AnythingChanged_IsTrue(string pet, PetSize size, bool hidden, int version)
    {
        // Arrange
        var policy = new TrayMenuRebuildPolicy();
        policy.MarkBuilt(new TrayMenuState("robot", PetSize.Medium, false, 0));

        // Act
        var result = policy.ShouldRebuild(new TrayMenuState(pet, size, hidden, version), menuOpen: false);

        // Assert
        result.Should().BeTrue();
    }

    /// <summary>
    /// While the menu is open it is not built again, however much has changed; the change waits for it to close.
    /// </summary>
    [Fact]
    public void ShouldRebuild_MenuOpen_IsFalseUntilItCloses()
    {
        // Arrange
        var policy = new TrayMenuRebuildPolicy();
        policy.MarkBuilt(new TrayMenuState("robot", PetSize.Medium, false, 0));
        var changed = new TrayMenuState("cat", PetSize.Large, true, 3);

        // Act
        var whileOpen = policy.ShouldRebuild(changed, menuOpen: true);
        var afterClose = policy.ShouldRebuild(changed, menuOpen: false);

        // Assert
        whileOpen.Should().BeFalse();
        afterClose.Should().BeTrue();
    }

    /// <summary>
    /// Once the menu was built for a changed state it is left alone again.
    /// </summary>
    [Fact]
    public void ShouldRebuild_AfterRebuild_IsFalse()
    {
        // Arrange
        var policy = new TrayMenuRebuildPolicy();
        var changed = new TrayMenuState("cat", PetSize.Large, true, 3);
        policy.MarkBuilt(new TrayMenuState("robot", PetSize.Medium, false, 0));
        policy.MarkBuilt(changed);

        // Act
        var result = policy.ShouldRebuild(changed, menuOpen: false);

        // Assert
        result.Should().BeFalse();
    }
}
