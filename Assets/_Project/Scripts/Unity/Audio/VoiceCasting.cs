// Assets/_Project/Scripts/Unity/Audio/VoiceCasting.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>
    /// Which operator a line belongs to, read from what the engine reported
    /// (AUDIO.md increment AU2).
    /// </summary>
    /// <remarks>
    /// <b>Reading, not deciding.</b> The engine says who was knocked out and
    /// which seat it counts for. Which of that seat's operators takes the
    /// credit on screen is a presentation choice, and it is made from the
    /// batch's own actor: the caster of an accepted ability, otherwise the
    /// operator that moved. A knockout with no such actor (a lingering zone at
    /// upkeep, a bleed) has no kill line.
    ///
    /// <b>Plain C#</b> over core types, so it is tested without Unity.
    /// </remarks>
    public static class VoiceCasting
    {
        /// <summary>The operator that walked forward in this batch, or null.</summary>
        public static OperatorState Mover(IReadOnlyList<IGameEvent> events)
        {
            if (events == null) return null;

            foreach (var e in events)
                if (e is OperatorMoved moved && moved.AttemptedTo > moved.From)
                    return moved.Operator;

            return null;
        }

        /// <summary>
        /// The operator who takes the kill line for <paramref name="down"/>, or
        /// null when the batch has no actor on the credited seat.
        /// </summary>
        public static OperatorState Killer(
            IReadOnlyList<IGameEvent> events, OperatorState castBy, OperatorNeutralized down)
        {
            if (down == null || !down.CreditedTo.HasValue) return null;

            var seat = down.CreditedTo.Value;

            if (castBy != null)
                return castBy.Owner == seat && !ReferenceEquals(castBy, down.Operator) ? castBy : null;

            if (events == null) return null;

            foreach (var e in events)
            {
                OperatorState actor = null;
                if (e is OperatorMoved moved && moved.AttemptedTo > moved.From) actor = moved.Operator;
                else if (e is CollisionResolved collision) actor = collision.Mover;

                if (actor != null && actor.Owner == seat && !ReferenceEquals(actor, down.Operator))
                    return actor;
            }

            return null;
        }

        /// <summary>
        /// The winner's voice: the last of its operators to reach home in this
        /// batch, otherwise the first of <paramref name="squad"/> on the
        /// winning seat.
        /// </summary>
        public static OperatorState Victor(
            IReadOnlyList<IGameEvent> events, PlayerColor winner, IEnumerable<OperatorState> squad)
        {
            OperatorState home = null;

            if (events != null)
                foreach (var e in events)
                    if (e is OperatorReachedHome reached && reached.Operator.Owner == winner)
                        home = reached.Operator;

            if (home != null) return home;

            if (squad != null)
                foreach (var op in squad)
                    if (op != null && op.Owner == winner)
                        return op;

            return null;
        }
    }
}