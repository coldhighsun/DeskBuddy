namespace DeskBuddy.Domain.Messages;

/// <summary>
/// Packs the state a <see cref="PetMessageKind.Remark"/> belongs to, and which wording of it, into the single number
/// a <see cref="PetMessage"/> carries.
/// </summary>
public static class RemarkCode
{
    /// <summary>
    /// The factor that separates the variant from the state in the code.
    /// </summary>
    private const int VariantFactor = 100;

    /// <summary>
    /// Packs a state and a variant.
    /// </summary>
    /// <param name="state">
    /// The state the remark belongs to.
    /// </param>
    /// <param name="variant">
    /// Which wording of it; 0 for the only or the default one.
    /// </param>
    /// <returns>
    /// The code.
    /// </returns>
    public static int Encode(PetState state, int variant = 0)
    {
        return (variant * VariantFactor) + (int)state;
    }

    /// <summary>
    /// Unpacks a code.
    /// </summary>
    /// <param name="code">
    /// The code made by <see cref="Encode"/>.
    /// </param>
    /// <returns>
    /// The state and the variant.
    /// </returns>
    public static (PetState State, int Variant) Decode(int code)
    {
        return ((PetState)(code % VariantFactor), code / VariantFactor);
    }
}
