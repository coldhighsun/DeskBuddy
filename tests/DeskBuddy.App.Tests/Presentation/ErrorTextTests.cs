using System.IO;
using DeskBuddy.App.Resources;

namespace DeskBuddy.App.Tests.Presentation;

/// <summary>
/// Tests for <see cref="ErrorText"/>.
/// </summary>
public sealed class ErrorTextTests
{
    /// <summary>
    /// Each kind of error gets a reason in the language of the interface instead of its English message.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="expectedKey">The name of the string expected.</param>
    [Theory]
    [MemberData(nameof(Errors))]
    public void For_Error_GivesLocalizedReason(Exception error, string expectedKey)
    {
        // Arrange
        var expected = typeof(Strings).GetProperty(expectedKey)!.GetValue(null);

        // Act
        var reason = ErrorText.For(error);

        // Assert
        reason.Should().Be((string)expected!);
        reason.Should().NotBe(error.Message);
    }

    /// <summary>
    /// Gets the errors to try and the string each should give.
    /// </summary>
    /// <returns>Pairs of error and string name.</returns>
    public static TheoryData<Exception, string> Errors()
    {
        return new TheoryData<Exception, string>
        {
            { new InvalidDataException("manifest"), nameof(Strings.ErrorInvalidData) },
            { new FileNotFoundException("atlas"), nameof(Strings.ErrorNotFound) },
            { new DirectoryNotFoundException("pets"), nameof(Strings.ErrorNotFound) },
            { new UnauthorizedAccessException("denied"), nameof(Strings.ErrorAccessDenied) },
            { new IOException("locked"), nameof(Strings.ErrorFileInUse) },
            { new NotSupportedException("codec"), nameof(Strings.ErrorImage) },
            { new FormatException("png"), nameof(Strings.ErrorImage) },
            { new InvalidOperationException("other"), nameof(Strings.ErrorGeneric) },
        };
    }
}
