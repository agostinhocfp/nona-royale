// Assets/_Project/Scripts/Core/Bots/IBot.cs
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Draft;
using NonaRoyale.Core.Events;

namespace NonaRoyale.Core.Bots
{
    /// <summary>
    /// A CPU seat, one decision at a time (BOTS.md decision 1).
    /// </summary>
    /// <remarks>
    /// <b>Step-wise, so it can be paced.</b> <see cref="Next"/> returns one
    /// command for the seat whose turn it is. The caller sends it, then hands
    /// the result back through <see cref="Observe"/>, so a refused command is
    /// never proposed again in the same turn.
    /// </remarks>
    public interface IBot
    {
        BotPersonality Personality { get; }

        /// <summary>
        /// The next command for the current seat, or null when the match is
        /// over. Always returns something the turn can end with: when nothing
        /// else is worth doing, <see cref="EndTurnCommand"/>.
        /// </summary>
        ICommand Next(MatchFactory.Match match);

        /// <summary>What the engine said about the last command.</summary>
        void Observe(ICommand command, IReadOnlyList<IGameEvent> events);

        /// <summary>The operator this seat drafts next, or null if it cannot pick now.</summary>
        OperatorDefinition PickDraft(DraftState draft, PlayerColor seat);
    }
}