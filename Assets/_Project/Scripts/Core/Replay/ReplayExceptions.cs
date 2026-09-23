// Assets/_Project/Scripts/Core/Replay/ReplayExceptions.cs
using System;
using NonaRoyale.Core.Commands;

namespace NonaRoyale.Core.Replay
{
    /// <summary>
    /// The text is not a replay file: a malformed line, an unknown command, a
    /// sequence number out of order. Carries the 1-based line of the text.
    /// </summary>
    public sealed class ReplayFormatException : Exception
    {
        public ReplayFormatException(int line, string message)
            : base($"replay line {line}: {message}")
        {
            Line = line;
        }

        public int Line { get; }
    }

    /// <summary>
    /// A well-formed replay this build refuses to play: a newer envelope
    /// format, or rules that no longer match the ones it was recorded under.
    /// Raised before the first command runs.
    /// </summary>
    public sealed class ReplayIncompatibleException : Exception
    {
        public ReplayIncompatibleException(string message) : base(message) { }
    }

    /// <summary>
    /// The replay stopped matching the match it was rebuilding: a recorded
    /// command was refused, came from the wrong seat, or the squads fielded
    /// differ. <see cref="Sequence"/> is the command's <c>n</c>, or 0 when the
    /// match diverged before the first command.
    /// </summary>
    /// <remarks>
    /// Thrown, never returned as a partial result: a replay that quietly
    /// stopped halfway would look like a match that ended early.
    /// </remarks>
    public sealed class ReplayDesyncException : Exception
    {
        public ReplayDesyncException(int sequence, ICommand command, string reason)
            : base(sequence == 0
                ? $"replay desynced before the first command: {reason}"
                : $"replay desynced at command {sequence} ({Describe(command)}): {reason}")
        {
            Sequence = sequence;
            Command = command;
            Reason = reason;
        }

        public int Sequence { get; }
        public ICommand Command { get; }
        public string Reason { get; }

        private static string Describe(ICommand command) =>
            command == null ? "no command" : CommandCodec.Describe(command);
    }
}