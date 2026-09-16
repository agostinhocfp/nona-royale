// Assets/_Project/Scripts/Core/Draft/DraftMode.cs
namespace NonaRoyale.Core.Draft
{
    /// <summary>
    /// How the seats take their picks (DRAFT.md decision 1).
    /// </summary>
    public enum DraftMode
    {
        /// <summary>
        /// The default. One shared clock for the whole draft; any seat picks at
        /// any time, in any order. When the clock runs out, every empty slot is
        /// filled at random.
        /// </summary>
        AllPick = 0,

        /// <summary>
        /// Turn order that reverses each round: R, B, G, V, V, G, B, R, and so
        /// on. Each pick has its own clock; when it runs out, the pick is made
        /// at random for that seat.
        /// </summary>
        Snake = 1
    }
}