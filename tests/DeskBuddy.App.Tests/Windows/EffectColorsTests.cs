using System.Windows.Media;
using DeskBuddy.App.Windows;

namespace DeskBuddy.App.Tests.Windows;

/// <summary>
/// Tests for <see cref="EffectColors"/>.
/// </summary>
public sealed class EffectColorsTests
{
    /// <summary>
    /// The rainbow starts at red, passes the colors in order and is red again after a full turn.
    /// </summary>
    [Fact]
    public void At_RunsRoundTheRainbow()
    {
        // Assert
        EffectColors.At(0, 255).Should().Be(Color.FromRgb(0xFF, 0x00, 0x00));
        EffectColors.At(1.0 / 6, 255).Should().Be(Color.FromRgb(0xFF, 0xFF, 0x00));
        EffectColors.At(2.0 / 6, 255).Should().Be(Color.FromRgb(0x00, 0xFF, 0x00));
        EffectColors.At(3.0 / 6, 255).Should().Be(Color.FromRgb(0x00, 0xFF, 0xFF));
        EffectColors.At(4.0 / 6, 255).Should().Be(Color.FromRgb(0x00, 0x00, 0xFF));
        EffectColors.At(5.0 / 6, 255).Should().Be(Color.FromRgb(0xFF, 0x00, 0xFF));
        EffectColors.At(1, 255).Should().Be(EffectColors.At(0, 255));
    }

    /// <summary>
    /// Any point of the rainbow, also one before the start or past the end, is the same as the one a turn away.
    /// </summary>
    /// <param name="phase">The point of the rainbow.</param>
    [Theory]
    [InlineData(0.1)]
    [InlineData(0.37)]
    [InlineData(0.9)]
    public void At_Wraps(double phase)
    {
        // Assert
        EffectColors.At(phase + 1, 200).Should().Be(EffectColors.At(phase, 200));
        EffectColors.At(phase - 1, 200).Should().Be(EffectColors.At(phase, 200));
    }

    /// <summary>
    /// Between two colors of the rainbow the color is a shade of both, and the opacity asked for is kept.
    /// </summary>
    [Fact]
    public void At_BetweenColors_MixesThemAndKeepsAlpha()
    {
        // Act
        var color = EffectColors.At(1.0 / 12, 0x40);

        // Assert
        color.A.Should().Be(0x40);
        color.R.Should().Be(0xFF);
        color.G.Should().BeInRange(0x7E, 0x81);
        color.B.Should().Be(0x00);
    }

    /// <summary>
    /// A gradient has the stops it was asked for, each with the opacity given, and starts each at its own point of the
    /// rainbow.
    /// </summary>
    [Fact]
    public void Gradient_HasTheStopsAskedFor()
    {
        // Act
        var brush = EffectColors.Gradient(
            new System.Windows.Point(0, 0),
            new System.Windows.Point(1, 0),
            [(0, 0, 0), (0.5, 0x80, 0.5), (1, 0xFF, 0.25)],
            TimeSpan.FromSeconds(1));

        // Assert
        brush.GradientStops.Select(stop => stop.Offset).Should().Equal(0, 0.5, 1);
        brush.GradientStops.Select(stop => stop.Color.A).Should().Equal(0, 0x80, 0xFF);
        brush.GradientStops[1].Color.Should().Be(EffectColors.At(0.5, 0x80));
    }

    /// <summary>
    /// Whitening makes a color paler without changing its opacity: none leaves it, all of it gives white.
    /// </summary>
    [Fact]
    public void At_Whiten_MakesTheColorPaler()
    {
        // Assert
        EffectColors.At(0, 0x80, 0).Should().Be(Color.FromArgb(0x80, 0xFF, 0x00, 0x00));
        EffectColors.At(0, 0x80, 1).Should().Be(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF));
        var pale = EffectColors.At(0, 0x80, 0.5);
        pale.A.Should().Be(0x80);
        pale.R.Should().Be(0xFF);
        pale.G.Should().BeInRange(0x7E, 0x81);
    }

    /// <summary>
    /// A puff of smoke has a light middle and a rim that is a little less opaque, in colors that differ from puff to puff.
    /// </summary>
    [Fact]
    public void Puff_HasMiddleAndRimAndDiffersByPhase()
    {
        // Act
        var first = EffectColors.Puff(0, TimeSpan.FromSeconds(2), 0.3);
        var second = EffectColors.Puff(0.5, TimeSpan.FromSeconds(2), 0.3);

        // Assert
        first.GradientStops.Select(stop => stop.Offset).Should().Equal(0, 1);
        first.GradientStops[0].Color.A.Should().Be(0xFF);
        first.GradientStops[1].Color.A.Should().BeLessThan(0xFF);
        first.GradientStops[0].Color.Should().NotBe(second.GradientStops[0].Color);
    }
}
