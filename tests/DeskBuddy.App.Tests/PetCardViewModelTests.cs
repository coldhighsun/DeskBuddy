using DeskBuddy.App.ViewModels;
using DeskBuddy.Domain.Pets;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="PetCardViewModel"/>.
/// </summary>
public class PetCardViewModelTests
{
    /// <summary>
    /// The file paths the card asked the picture supplier for, in order.
    /// </summary>
    private readonly List<string> _requestedFrames = [];

    /// <summary>
    /// A new card starts its preview on the first state, with that state's name and first picture.
    /// </summary>
    [Fact]
    public void Constructor_LoadedPet_ShowsIdleFirst()
    {
        // Act
        var card = CreateCard(CreatePet(PetState.Walk));

        // Assert
        card.IsAvailable.Should().BeTrue();
        card.CurrentState.Should().Be(PetState.Idle);
        card.CurrentFramePath.Should().Be("idle_0.png");
        card.StateCaption.Should().NotBeNullOrEmpty().And.NotStartWith("!");
        _requestedFrames.Should().Equal("idle_0.png");
    }

    /// <summary>
    /// The card shows the name the pet gives itself, and remembers the folder it was loaded from.
    /// </summary>
    [Fact]
    public void Constructor_LoadedPet_KeepsFolderAndDisplayNameApart()
    {
        // Act
        var card = new PetCardViewModel("tom-folder", CreatePet(), _ => null);

        // Assert
        card.FolderName.Should().Be("tom-folder");
        card.DisplayName.Should().Be("test");
    }

    /// <summary>
    /// The animation of the state being previewed plays on as time passes.
    /// </summary>
    [Fact]
    public void Tick_FrameDurationElapsed_ShowsNextPicture()
    {
        // Arrange
        var card = CreateCard(CreatePet());

        // Act
        card.Tick(TimeSpan.FromMilliseconds(500));

        // Assert
        card.CurrentFramePath.Should().Be("idle_1.png");
    }

    /// <summary>
    /// After the state has been shown for a while the preview goes on to the next one.
    /// </summary>
    [Fact]
    public void Tick_StateDurationElapsed_MovesToNextState()
    {
        // Arrange
        var card = CreateCard(CreatePet(PetState.Walk, PetState.Sleep));

        // Act
        card.Tick(PetCardViewModel.StateDuration);

        // Assert
        card.CurrentState.Should().Be(PetState.Walk);
        card.CurrentFramePath.Should().Be("Walk_0.png");
    }

    /// <summary>
    /// After the last state the preview starts over.
    /// </summary>
    [Fact]
    public void Tick_AfterLastState_WrapsToFirst()
    {
        // Arrange
        var card = CreateCard(CreatePet(PetState.Walk));

        // Act
        card.Tick(PetCardViewModel.StateDuration);
        card.Tick(PetCardViewModel.StateDuration);

        // Assert
        card.CurrentState.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// A pet with a single state keeps showing it.
    /// </summary>
    [Fact]
    public void Tick_OnlyOneState_StaysOnIt()
    {
        // Arrange
        var card = CreateCard(CreatePet());

        // Act
        card.Tick(PetCardViewModel.StateDuration);

        // Assert
        card.CurrentState.Should().Be(PetState.Idle);
    }

    /// <summary>
    /// Restarting goes back to the first state.
    /// </summary>
    [Fact]
    public void Restart_OnLaterState_GoesBackToFirst()
    {
        // Arrange
        var card = CreateCard(CreatePet(PetState.Walk));
        card.Tick(PetCardViewModel.StateDuration);

        // Act
        card.Restart();

        // Assert
        card.CurrentState.Should().Be(PetState.Idle);
        card.CurrentFramePath.Should().Be("idle_0.png");
    }

    /// <summary>
    /// A pet that cannot be loaded gets a card that explains why and does not move.
    /// </summary>
    [Fact]
    public void Constructor_PetCannotBeLoaded_ExplainsAndStaysStill()
    {
        // Act
        var card = new PetCardViewModel("broken", "manifest is missing");
        card.Tick(PetCardViewModel.StateDuration);

        // Assert
        card.IsAvailable.Should().BeFalse();
        card.DisplayName.Should().Be("broken");
        card.LoadError.Should().Contain("manifest is missing");
        card.CurrentFramePath.Should().BeNull();
        card.CurrentFrame.Should().BeNull();
        card.Tags.Should().BeEmpty();
    }

    /// <summary>
    /// The tags show what the pet can do and dim what it cannot.
    /// </summary>
    [Fact]
    public void Tags_PetWithClimbing_MarksOnlyWhatItCanDo()
    {
        // Act
        var card = CreateCard(CreatePet(PetState.Climb, PetState.Hang));

        // Assert
        card.Tags.Select(tag => tag.IsSupported).Should().Equal(false, true, false, false);
        card.Tags.Select(tag => tag.Text).Should().OnlyContain(text => !text.StartsWith('!'));
    }

    /// <summary>
    /// Creates a card whose pictures are looked up by file path and not decoded.
    /// </summary>
    /// <param name="pet">
    /// The pet.
    /// </param>
    /// <returns>
    /// The card.
    /// </returns>
    private PetCardViewModel CreateCard(Pet pet)
    {
        return new PetCardViewModel(
            "folder",
            pet,
            path =>
            {
                _requestedFrames.Add(path);
                return null;
            });
    }

    /// <summary>
    /// Creates a pet whose Idle animation has two pictures at 2 fps (500 ms each) and whose other states have two
    /// pictures too.
    /// </summary>
    /// <param name="states">
    /// The states to define besides Idle.
    /// </param>
    /// <returns>
    /// The pet.
    /// </returns>
    private static Pet CreatePet(params PetState[] states)
    {
        var animations = new Dictionary<PetState, PetAnimation> { [PetState.Idle] = new(2, ["idle_0.png", "idle_1.png"]) };
        foreach (var state in states)
        {
            animations[state] = new PetAnimation(2, [$"{state}_0.png", $"{state}_1.png"]);
        }

        return new Pet("test", 64, 64, animations);
    }
}
