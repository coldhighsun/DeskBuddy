using System.Runtime.InteropServices;

namespace DeskBuddy.App.Interop;

/// <summary>
/// Reads whether Windows is in a high-contrast theme straight from the system. WPF's own <c>SystemParameters</c> caches the
/// value and may not have refreshed it yet when a settings-changed notification arrives.
/// </summary>
internal static partial class HighContrastReader
{
    /// <summary>
    /// <c>SPI_GETHIGHCONTRAST</c>: asks for the high-contrast settings.
    /// </summary>
    private const uint SpiGetHighContrast = 0x0042;

    /// <summary>
    /// <c>HCF_HIGHCONTRASTON</c>: set in the flags while a high-contrast theme is on.
    /// </summary>
    private const uint HighContrastOn = 0x00000001;

    /// <summary>
    /// Asks the system whether a high-contrast theme is on.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> while a high-contrast theme is on; <see langword="false"/> when it is off or the system
    /// cannot say.
    /// </returns>
    public static bool IsOn()
    {
        return TryRead(out var on) && on;
    }

    /// <summary>
    /// Asks the system whether a high-contrast theme is on, and tells whether the system could answer.
    /// </summary>
    /// <param name="on">
    /// Whether a high-contrast theme is on; <see langword="false"/> when the call failed.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the system answered; <see langword="false"/> when the call failed, which would
    /// otherwise look the same as high contrast being off.
    /// </returns>
    public static bool TryRead(out bool on)
    {
        var info = new HighContrast { Size = (uint)Marshal.SizeOf<HighContrast>() };
        var answered = SystemParametersInfo(SpiGetHighContrast, info.Size, ref info, 0);
        on = answered && (info.Flags & HighContrastOn) != 0;
        return answered;
    }

    /// <summary>
    /// Reads or changes a system-wide parameter.
    /// </summary>
    /// <param name="action">
    /// The parameter to read.
    /// </param>
    /// <param name="parameter">
    /// The size of the structure.
    /// </param>
    /// <param name="info">
    /// The structure that receives the settings.
    /// </param>
    /// <param name="flags">
    /// Not used when reading.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on success.
    /// </returns>
    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint parameter, ref HighContrast info, uint flags);

    /// <summary>
    /// The <c>HIGHCONTRASTW</c> structure; the name of the theme is not needed, so the pointer is left unused.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct HighContrast
    {
        /// <summary>
        /// The size of the structure in bytes.
        /// </summary>
        public uint Size;

        /// <summary>
        /// The <c>HCF_*</c> flags.
        /// </summary>
        public uint Flags;

        /// <summary>
        /// Pointer to the name of the default theme; unused.
        /// </summary>
        public nint DefaultScheme;
    }
}
