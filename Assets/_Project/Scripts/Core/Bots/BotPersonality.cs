// Assets/_Project/Scripts/Core/Bots/BotPersonality.cs
namespace NonaRoyale.Core.Bots
{
    /// <summary>
    /// How a CPU seat plays (BOTS.md decisions 2 and 11). The three styles are
    /// meant to be challenging, each leaning one way without ignoring the
    /// others; the Wildcard is the default and leans no way at all.
    /// </summary>
    /// <remarks>
    /// The Wildcard comes last, so the three styles keep their values: tests
    /// and the sim hand out <c>(BotPersonality)(i % 3)</c> to get one of each.
    /// </remarks>
    public enum BotPersonality
    {
        /// <summary>Spends freely, prefers kills and finishing blows, and lands on enemies when it can.</summary>
        Brawler = 0,

        /// <summary>Races and deploys eagerly. Casts defensively first, offensively at a lower rate.</summary>
        Runner = 1,

        /// <summary>Saves for its best ability and the best target. Spends on defence when it must, at a lower rate.</summary>
        Banker = 2,

        /// <summary>
        /// The default (BOTS.md decision 11). Drafts completely at random, and
        /// plays the match on the neutral base weights, between the three styles.
        /// </summary>
        Wildcard = 3
    }
}