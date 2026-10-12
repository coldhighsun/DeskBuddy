namespace DeskBuddy.Domain;

/// <summary>
/// Questions about a <see cref="PetState"/> that several parts of the pet ask.
/// </summary>
public static class PetStateExtensions
{
    /// <summary>
    /// Tells whether the pet crawls along the edges of the screen in this state: climbing up or down a side, or hanging
    /// from the top.
    /// </summary>
    /// <param name="state">
    /// The state.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for <see cref="PetState.Climb"/>, <see cref="PetState.ClimbDown"/> and
    /// <see cref="PetState.Ceiling"/>.
    /// </returns>
    public static bool IsCrawl(this PetState state)
    {
        return state is PetState.Climb or PetState.ClimbDown or PetState.Ceiling;
    }

    /// <summary>
    /// Tells whether the pet falls in this state: before its umbrella or parachute opens, or floating down with it.
    /// </summary>
    /// <param name="state">
    /// The state.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for <see cref="PetState.Fall"/> and <see cref="PetState.Glide"/>.
    /// </returns>
    public static bool IsFalling(this PetState state)
    {
        return state is PetState.Fall or PetState.Glide;
    }

    /// <summary>
    /// Tells whether the pet, changing from one state to another, lets go of the edge of the screen it crawls along and
    /// falls, if it is high enough above the ground. Being picked up (<see cref="PetState.Drag"/>) is not letting go, the
    /// drop decides; and a fall that has begun needs no second one.
    /// </summary>
    /// <param name="previous">
    /// The state the pet was in.
    /// </param>
    /// <param name="next">
    /// The state it changes to.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the pet crawled and now is in another state than crawling, dragging or falling.
    /// </returns>
    public static bool EndsCrawlInFall(this PetState previous, PetState next)
    {
        return previous.IsCrawl() && !next.IsCrawl() && next is not PetState.Drag && !next.IsFalling();
    }
}
