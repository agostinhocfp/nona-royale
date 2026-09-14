// Assets/_Project/Scripts/Core/Services/MoveResult.cs
using NonaRoyale.Core.Board;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// The outcome of a movement calculation, before anything is applied. The
    /// caller decides whether to commit it — which is what lets a collision
    /// inspect the landing first (COMBAT_SYSTEMS §7.2).
    /// </summary>
    public readonly struct MoveResult
    {
        public MoveResult(int from, int to, CellRef destination, bool finished, bool enteredHomeColumn, bool overshot)
        {
            From = from;
            To = to;
            Destination = destination;
            Finished = finished;
            EnteredHomeColumn = enteredHomeColumn;
            Overshot = overshot;
        }

        public int From { get; }
        public int To { get; }
        public CellRef Destination { get; }

        /// <summary>Reached HOME. The operator leaves play for the rest of the match (COMBAT_SYSTEMS §8).</summary>
        public bool Finished { get; }

        /// <summary>Crossed from the shared loop into its own column this move — it is now out of the fight (§4.3).</summary>
        public bool EnteredHomeColumn { get; }

        /// <summary>
        /// The roll carried past HOME and was clamped. Recorded because the
        /// post-MVP opt-out-of-home-entry rule will need to know, and because
        /// it is the kind of thing worth seeing in a log.
        /// </summary>
        public bool Overshot { get; }

        /// <summary>Only a move ending on the shared loop can be contested.</summary>
        public bool CanBeContested => Destination.IsOnTrack;

        public int Cells => To - From;

        public override string ToString() => $"{From} -> {To} ({Destination})";
    }
}