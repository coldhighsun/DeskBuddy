using AwesomeAssertions;
using DeskBuddy.Domain.Messages;

namespace DeskBuddy.Domain.Tests.Messages;

/// <summary>
/// Tests for <see cref="RemarkCode"/>.
/// </summary>
public class RemarkCodeTests
{
    /// <summary>
    /// What is packed comes out again.
    /// </summary>
    [Theory]
    [InlineData(PetState.Welcome, 0)]
    [InlineData(PetState.Meal, 2)]
    [InlineData(PetState.Chime, 17)]
    [InlineData(PetState.Festive, 4)]
    public void Decode_EncodedCode_ReturnsStateAndVariant(PetState state, int variant)
    {
        // Act
        var (decodedState, decodedVariant) = RemarkCode.Decode(RemarkCode.Encode(state, variant));

        // Assert
        decodedState.Should().Be(state);
        decodedVariant.Should().Be(variant);
    }

    /// <summary>
    /// A remark without a variant is just the state, as before variants existed.
    /// </summary>
    [Fact]
    public void Encode_NoVariant_IsTheStateNumber()
    {
        // Act
        var code = RemarkCode.Encode(PetState.LowBattery);

        // Assert
        code.Should().Be((int)PetState.LowBattery);
    }
}
