namespace DeskBuddy.App.Tests.Presentation.Tray;

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
    /// The grid has four columns up to sixteen pets, five up to twenty-five and six beyond, and never fewer than four, even
    /// with no pet at all.
    /// </summary>
    /// <param name="count">How many pets there are.</param>
    /// <param name="columns">The columns expected.</param>
    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 4)]
    [InlineData(11, 4)]
    [InlineData(16, 4)]
    [InlineData(17, 5)]
    [InlineData(25, 5)]
    [InlineData(26, 6)]
    [InlineData(200, 6)]
    public void GridColumns_ByNumberOfPets_GrowsWiderBeforeItGrowsTaller(int count, int columns)
    {
        // Act
        var result = TrayMenuModel.GridColumns(count);

        // Assert
        result.Should().Be(columns);
    }

    /// <summary>
    /// A random choice is never the pet in use, whichever way the dice fall.
    /// </summary>
    [Fact]
    public void PickRandom_SeveralPets_NeverPicksThePetInUse()
    {
        // Arrange
        InstalledPet[] pets = [.. Enumerable.Range(0, 8).Select(i => new InstalledPet($"pet{i}", $"Pet {i}", null, 64, 64))];
        var random = new Random(1234);

        // Act
        var picks = Enumerable.Range(0, 200).Select(_ => TrayMenuModel.PickRandom(pets, "pet3", random)).ToList();

        // Assert
        picks.Should().NotContain("pet3").And.NotContainNulls();
        picks.Distinct().Should().HaveCountGreaterThan(1);
    }

    /// <summary>
    /// With no other pet to change to there is nothing to pick.
    /// </summary>
    /// <param name="current">The pet in use.</param>
    [Theory]
    [InlineData("cat")]
    [InlineData("CAT")]
    public void PickRandom_OnlyThePetInUse_ReturnsNull(string current)
    {
        // Arrange
        InstalledPet[] pets = [new("cat", "Cat", null, 64, 64)];

        // Act
        var result = TrayMenuModel.PickRandom(pets, current, new Random(1));

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// There is another pet to change to whenever any pet is not the one in use, even if the pet in use is not listed at all.
    /// </summary>
    /// <param name="current">The pet in use.</param>
    /// <param name="expected">Whether another pet is expected.</param>
    [Theory]
    [InlineData("cat", true)]
    [InlineData("dog", true)]
    [InlineData("robot", true)]
    public void HasAnotherPet_PetsOtherThanTheOneInUse_IsTrue(string current, bool expected)
    {
        // Act
        var result = TrayMenuModel.HasAnotherPet(Pets, current);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// With only the pet in use listed, or none at all, there is no other pet.
    /// </summary>
    [Fact]
    public void HasAnotherPet_OnlyThePetInUseOrNone_IsFalse()
    {
        // Arrange
        InstalledPet[] only = [new("cat", "Cat", null, 64, 64)];

        // Act
        var withOne = TrayMenuModel.HasAnotherPet(only, "CAT");
        var withNone = TrayMenuModel.HasAnotherPet([], "cat");

        // Assert
        withOne.Should().BeFalse();
        withNone.Should().BeFalse();
    }

    /// <summary>
    /// With no pets at all there is nothing to pick either.
    /// </summary>
    [Fact]
    public void PickRandom_NoPets_ReturnsNull()
    {
        // Act
        var result = TrayMenuModel.PickRandom([], "cat", new Random(1));

        // Assert
        result.Should().BeNull();
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
    /// A change of the interface language changes the text of the menu, so it is built again.
    /// </summary>
    [Fact]
    public void ShouldRebuild_LanguageChanged_IsTrue()
    {
        // Arrange
        var policy = new TrayMenuRebuildPolicy();
        policy.MarkBuilt(new TrayMenuState("robot", PetSize.Medium, false, 0, Language: "zh-CN"));

        // Act
        var result = policy.ShouldRebuild(new TrayMenuState("robot", PetSize.Medium, false, 0, Language: "en"), menuOpen: false);

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

    /// <summary>
    /// A pet the user has switched off is left out of the grid of pets to change to.
    /// </summary>
    [Fact]
    public void PetChoices_HiddenPets_AreLeftOut()
    {
        // Arrange
        InstalledPet[] pets = [.. new[] { "cat", "dog", "fox" }.Select(name => new InstalledPet(name, name, null, 64, 64))];

        // Act
        var choices = TrayMenuModel.PetChoices(pets, "cat", ["DOG"]);

        // Assert
        choices.Select(choice => choice.FolderName).Should().Equal("cat", "fox");
    }

    /// <summary>
    /// The pet in use stays in the grid, even when it is on the list of pets that are switched off, so the menu shows what
    /// is on the screen.
    /// </summary>
    [Fact]
    public void PetChoices_PetInUseIsHidden_StaysInTheGrid()
    {
        // Arrange
        InstalledPet[] pets = [.. new[] { "cat", "dog" }.Select(name => new InstalledPet(name, name, null, 64, 64))];

        // Act
        var choices = TrayMenuModel.PetChoices(pets, "cat", ["cat", "dog"]);

        // Assert
        choices.Select(choice => choice.FolderName).Should().Equal("cat");
        choices.Single().IsCurrent.Should().BeTrue();
    }

    /// <summary>
    /// A random pick never lands on a pet that is switched off.
    /// </summary>
    [Fact]
    public void PickRandom_HiddenPets_AreNeverPicked()
    {
        // Arrange
        InstalledPet[] pets = [.. Enumerable.Range(0, 8).Select(i => new InstalledPet($"pet{i}", $"Pet {i}", null, 64, 64))];
        var random = new Random(3);
        string[] hidden = ["pet1", "pet2", "pet4", "pet5", "pet6"];

        // Act
        var picks = Enumerable.Range(0, 200).Select(_ => TrayMenuModel.PickRandom(pets, "pet0", random, hidden)).ToList();

        // Assert
        picks.Should().OnlyContain(pick => pick == "pet3" || pick == "pet7");
        picks.Distinct().Should().BeEquivalentTo(["pet3", "pet7"]);
    }

    /// <summary>
    /// When every other pet is switched off there is nothing to pick and no other pet to change to.
    /// </summary>
    [Fact]
    public void PickRandomAndHasAnotherPet_AllOthersHidden_NothingToChangeTo()
    {
        // Arrange
        InstalledPet[] pets = [.. new[] { "cat", "dog", "fox" }.Select(name => new InstalledPet(name, name, null, 64, 64))];
        string[] hidden = ["dog", "fox"];

        // Act
        var pick = TrayMenuModel.PickRandom(pets, "cat", new Random(1), hidden);
        var hasAnother = TrayMenuModel.HasAnotherPet(pets, "cat", hidden);

        // Assert
        pick.Should().BeNull();
        hasAnother.Should().BeFalse();
    }

    /// <summary>
    /// Switching a pet on or off makes the menu build again, as the pets it lists have changed.
    /// </summary>
    [Fact]
    public void ShouldRebuild_HiddenPetsChanged_IsTrue()
    {
        // Arrange
        var policy = new TrayMenuRebuildPolicy();
        policy.MarkBuilt(new TrayMenuState("cat", PetSize.Medium, false, 1, HiddenPets: "dog"));

        // Act
        var result = policy.ShouldRebuild(new TrayMenuState("cat", PetSize.Medium, false, 1, HiddenPets: "dog|fox"), menuOpen: false);

        // Assert
        result.Should().BeTrue();
    }
}
