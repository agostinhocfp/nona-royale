// Assets/_Project/Scripts/Core/Bots/BotPersonality.cs
namespace NonaRoyale.Core.Bots
{
    /// <summary>
    /// How a CPU seat plays (BOTS.md decision 2). All three are meant to be
    /// challenging; each leans one way without ignoring the others.
    /// </summary>
    public enum BotPersonality
    {
        /// <summary>Spends freely, prefers kills and finishing blows, and lands on enemies when it can.</summary>
        Brawler = 0,

        /// <summary>Races and deploys eagerly. Casts defensively first, offensively at a lower rate.</summary>
        Runner = 1,

        /// <summary>Saves for its best ability and the best target. Spends on defence when it must, at a lower rate.</summary>
        Banker = 2
    }
}