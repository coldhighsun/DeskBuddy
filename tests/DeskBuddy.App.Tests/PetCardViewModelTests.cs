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
        card.CurrentFrameId.Should().Be("idle_0.png");
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
        card.CurrentFrameId.Should().Be("idle_1.png");
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
        card.CurrentState.Should().BeOneOf(PetState.Walk, PetState.Sleep);
        card.CurrentFrameId.Should().Be($"{card.CurrentState}_0.png");
    }

    /// <summary>
    /// Every state is shown once in each round, in a random order that starts with Idle, and a new round does not start with
    /// the state that ended the one before.
    /// </summary>
    [Fact]
    public void Tick_ManyRounds_ShowsEveryStateOncePerRoundAndNeverTheSameTwiceInARow()
    {
        // Arrange
        var states = new[] { PetState.Walk, PetState.Sleep, PetState.Happy, PetState.Alert, PetState.Tired };
        var card = CreateCard(CreatePet(states), new Random(7));
        var shown = new List<PetState> { card.CurrentState!.Value };

        // Act
        for (var step = 0; step < ((states.Length + 1) * 20) - 1; step++)
        {
            card.Tick(PetCardViewModel.StateDuration);
            shown.Add(card.CurrentState!.Value);
        }

        // Assert
        var rounds = shown.Chunk(states.Length + 1).ToList();
        rounds[0][0].Should().Be(PetState.Idle);
        rounds.Should().OnlyContain(round => round.Distinct().Count() == states.Length + 1);
        shown.Zip(shown.Skip(1)).Should().OnlyContain(pair => pair.First != pair.Second);
        rounds.Skip(1).Select(round => string.Join(',', round)).Distinct().Count().Should().BeGreaterThan(1);
    }

    /// <summary>
    /// The order is not the same every time: restarting starts from Idle but goes on in another order.
    /// </summary>
    [Fact]
    public void Restart_Repeatedly_StartsWithIdleAndVariesTheRest()
    {
        // Arrange
        var card = CreateCard(
            CreatePet(PetState.Walk, PetState.Sleep, PetState.Happy, PetState.Alert, PetState.Tired, PetState.Dizzy),
            new Random(3));
        var seconds = new HashSet<PetState?>();

        // Act
        for (var run = 0; run < 20; run++)
        {
            card.Restart();
            card.CurrentState.Should().Be(PetState.Idle);
            card.Tick(PetCardViewModel.StateDuration);
            seconds.Add(card.CurrentState);
        }

        // Assert
        seconds.Should().HaveCountGreaterThan(1);
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
        card.CurrentFrameId.Should().Be("idle_0.png");
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
        card.CurrentFrameId.Should().BeNull();
        card.CurrentFrame.Should().BeNull();
        card.Tags.Should().BeEmpty();
        card.Details.Should().BeNull();
        card.HasPicture.Should().BeFalse();
    }

    /// <summary>
    /// A card knows whether the first picture of its preview could be read, so the tab can load it again if not.
    /// </summary>
    /// <param name="pictureAvailable">
    /// Whether the pictures can be read.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HasPicture_FirstPicture_TellsWhetherItCouldBeRead(bool pictureAvailable)
    {
        // Arrange
        var picture = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[4], 4);
        picture.Freeze();

        // Act
        var card = new PetCardViewModel("folder", CreatePet(), _ => pictureAvailable ? picture : null);

        // Assert
        card.HasPicture.Should().Be(pictureAvailable);
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
    /// The tooltip of the card starts with the name and the size line, then lists every tag on a line of its own, checked if
    /// the pet has the ability and crossed if not.
    /// </summary>
    [Fact]
    public void Details_PetWithClimbing_ListsNameInfoAndEveryTagWithItsMark()
    {
        // Act
        var card = CreateCard(CreatePet(PetState.Climb, PetState.Hang));

        // Assert
        var lines = card.Details!.Split(Environment.NewLine);
        lines.Should().HaveCount(2 + card.Tags.Count);
        lines[0].Should().Be(card.DisplayName);
        lines[1].Should().Be(card.InfoText);
        lines.Skip(2).Select(line => line[0]).Should().Equal('✗', '✓', '✗', '✗');
        lines.Skip(2).Select(line => line[2..]).Should().Equal(card.Tags.Select(tag => tag.Text));
    }

    /// <summary>
    /// Creates a card whose pictures are looked up by file path and not decoded.
    /// </summary>
    /// <param name="pet">
    /// The pet.
    /// </param>
    /// <param name="random">
    /// The source of randomness for the order of the states, or <see langword="null"/> for the shared one.
    /// </param>
    /// <returns>
    /// The card.
    /// </returns>
    private PetCardViewModel CreateCard(Pet pet, Random? random = null)
    {
        return new PetCardViewModel(
            "folder",
            pet,
            path =>
            {
                _requestedFrames.Add(path);
                return null;
            },
            random);
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
