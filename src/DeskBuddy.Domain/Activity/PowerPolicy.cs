using DeskBuddy.Domain.Power;

namespace DeskBuddy.Domain.Activity;

/// <summary>
/// What a call to <see cref="PowerPolicy.Update"/> found.
/// </summary>
/// <param name="IsLow">
/// Whether the battery is low and the computer is running on it.
/// </param>
/// <param name="JustPluggedIn">
/// Whether the power cable was plugged in since the previous update.
/// </param>
public readonly record struct PowerChange(bool IsLow, bool JustPluggedIn);

/// <summary>
/// Watches the power supply: a low battery on battery power, and the cable being plugged in. A battery that has been
/// low stays low until it has recovered a little, so a charge hovering around the limit does not flicker.
/// </summary>
/// <param name="options">
/// The charge limits.
/// </param>
public sealed class PowerPolicy(PetBehaviorOptions options)
{
    /// <summary>
    /// Whether there has been a previous reading to compare with.
    /// </summary>
    private bool _hasPrevious;

    /// <summary>
    /// Whether the computer was plugged in at the previous reading.
    /// </summary>
    private bool _wasPluggedIn;

    /// <summary>
    /// Whether the battery currently counts as low.
    /// </summary>
    private bool _isLow;

    /// <summary>
    /// Forgets the earlier readings, for when they stop coming (the scenarios were switched off), so that a cable
    /// plugged in meanwhile does not count as plugged in just now.
    /// </summary>
    public void Reset()
    {
        _hasPrevious = false;
        _isLow = false;
    }

    /// <summary>
    /// Takes in the latest reading.
    /// </summary>
    /// <param name="status">
    /// How the computer is powered.
    /// </param>
    /// <returns>
    /// Whether the battery is low and whether the cable was just plugged in. A computer without a battery, or with an
    /// unknown charge, never reports anything.
    /// </returns>
    public PowerChange Update(PowerStatus status)
    {
        if (!status.HasBattery || status.BatteryPercent is not { } percent)
        {
            _hasPrevious = false;
            _isLow = false;
            return default;
        }

        var justPluggedIn = _hasPrevious && status.IsPluggedIn && !_wasPluggedIn;
        _hasPrevious = true;
        _wasPluggedIn = status.IsPluggedIn;

        if (status.IsPluggedIn)
        {
            _isLow = false;
        }
        else if (_isLow)
        {
            _isLow = percent < options.LowBatteryRecoverPercent;
        }
        else
        {
            _isLow = percent <= options.LowBatteryPercent;
        }

        return new PowerChange(_isLow, justPluggedIn);
    }
}
