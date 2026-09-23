// Assets/_Project/Scripts/Core/Replay/ReplayEntry.cs
using System;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;

namespace NonaRoyale.Core.Replay
{
    /// <summary>One accepted command, in the order the match accepted it. One line of a replay file.</summary>
    public sealed class ReplayEntry
    {
        public ReplayEntry(int sequence, PlayerColor seat, ICommand command)
        {
            if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence), "Sequence numbers start at 1.");

            Sequence = sequence;
            Seat = seat;
            Command = command ?? throw new ArgumentNullException(nameof(command));
        }

        /// <summary>The 1-based position among accepted commands: <c>n</c> in the file, and what a desync reports.</summary>
        public int Sequence { get; }

        /// <summary>
        /// Whose turn it was. Commands carry no seat, since the engine acts for
        /// the current player, so this is redundant on purpose: a replay that
        /// drifts shows up as the wrong seat before it shows up as a refusal.
        /// </summary>
        public PlayerColor Seat { get; }

        public ICommand Command { get; }

        public override string ToString() => $"{Sequence} {Seat} {CommandCodec.Describe(Command)}";
    }
}