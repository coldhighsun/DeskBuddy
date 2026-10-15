using System.IO;
using DeskBuddy.App.Rendering;

namespace DeskBuddy.App.Tests;

/// <summary>
/// Tests for <see cref="PetLoadFailure"/>.
/// </summary>
public class PetLoadFailureTests
{
    /// <summary>
    /// The failures that come with a pet that cannot be used are recognized.
    /// </summary>
    [Fact]
    public void Is_PetFailure_IsTrue()
    {
        // Arrange
        Exception[] failures =
        [
            new InvalidDataException(),
            new FileNotFoundException(),
            new UnauthorizedAccessException(),
            new NotSupportedException(),
            new FormatException(),
        ];

        // Act and assert
        failures.Should().OnlyContain(failure => PetLoadFailure.Is(failure));
    }

    /// <summary>
    /// Other errors are not taken for a broken pet, so they are not hidden.
    /// </summary>
    [Fact]
    public void Is_OtherError_IsFalse()
    {
        // Arrange
        Exception[] errors =
        [
            new InvalidOperationException(),
            new ArgumentOutOfRangeException(),
            new OperationCanceledException(),
            new NullReferenceException(),
        ];

        // Act and assert
        errors.Should().OnlyContain(error => !PetLoadFailure.Is(error));
    }
}
