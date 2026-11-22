using DeskBuddy.Domain.Settings;

namespace DeskBuddy.Domain.Tests.Settings;

/// <summary>
/// Tests for <see cref="PetSwitchEffects"/>.
/// </summary>
public sealed class PetSwitchEffectsTests
{
    /// <summary>
    /// A random choice is always one of the effects that stand for themselves, never "random", and over many
    /// picks every one of them comes up.
    /// </summary>
    [Fact]
    public void Resolve_Random_PicksOnlyConcreteEffectsAndAllOfThem()
    {
        // Arrange
        var random = new Random(1);

        // Act
        var picked = Enumerable.Range(0, 200).Select(_ => PetSwitchEffects.Resolve(PetSwitchEffect.Random, random)).ToList();

        // Assert
        picked.Should().NotContain(PetSwitchEffect.Random);
        picked.Distinct().Should().BeEquivalentTo([PetSwitchEffect.Ufo, PetSwitchEffect.Claw, PetSwitchEffect.Smoke, PetSwitchEffect.Beam]);
    }

    /// <summary>
    /// An effect the user chose is played as it is.
    /// </summary>
    /// <param name="effect">The chosen effect.</param>
    [Theory]
    [InlineData(PetSwitchEffect.Ufo)]
    [InlineData(PetSwitchEffect.Claw)]
    [InlineData(PetSwitchEffect.Smoke)]
    [InlineData(PetSwitchEffect.Beam)]
    public void Resolve_Chosen_IsUnchanged(PetSwitchEffect effect)
    {
        // Act
        var resolved = PetSwitchEffects.Resolve(effect, new Random(1));

        // Assert
        resolved.Should().Be(effect);
    }
}
