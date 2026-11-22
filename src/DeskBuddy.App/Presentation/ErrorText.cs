using System.IO;

namespace DeskBuddy.App.Presentation;

/// <summary>
/// Turns an error into a short reason in the language of the interface. Exception messages come from .NET, Windows or
/// DeskBuddy's own lower layers in English and are meant for the log, so they are never shown to the user; callers log
/// the exception and show this instead. Errors of the updater are covered by <see cref="UpdateErrorText"/>.
/// </summary>
internal static class ErrorText
{
    /// <summary>
    /// Gets the reason to show for an error.
    /// </summary>
    /// <param name="error">What went wrong.</param>
    /// <returns>The localized reason.</returns>
    public static string For(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error switch
        {
            // InvalidDataException derives from SystemException, not IOException, but is matched first all the same.
            InvalidDataException => Strings.ErrorInvalidData,
            FileNotFoundException or DirectoryNotFoundException => Strings.ErrorNotFound,
            UnauthorizedAccessException => Strings.ErrorAccessDenied,
            IOException => Strings.ErrorFileInUse,
            NotSupportedException or FormatException => Strings.ErrorImage,
            _ => Strings.ErrorGeneric,
        };
    }
}
