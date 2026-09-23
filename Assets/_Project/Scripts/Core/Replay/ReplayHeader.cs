// Assets/_Project/Scripts/Core/Replay/ReplayHeader.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;

namespace NonaRoyale.Core.Replay
{
    /// <summary>
    /// Line 1 of a replay: which game was set up, under which rules, and who
    /// ended up fielding whom.
    /// </summary>
    /// <remarks>
    /// <b>Two version fields that move independently.</b>
    /// <see cref="Format"/> is the envelope, the shape of the file, and changes
    /// only when this code changes what a line looks like.
    /// <see cref="RulesHash"/> is the content guard, and changes whenever a
    /// config dial or an operator does (<see cref="RulesFingerprint"/>).
    ///
    /// <b>The fielded squads are recorded even when the recipe draws them.</b>
    /// A Random or Alpha recipe does not name its squads, so without this line
    /// a file could not say who played without being replayed. It is also a
    /// check: a replay whose rebuilt squads differ from these has diverged
    /// before the first command, and says so.
    /// </remarks>
    public sealed class ReplayHeader
    {
        /// <summary>The envelope version this build writes and the newest it reads.</summary>
        public const int CurrentFormat = 1;

        private static readonly IReadOnlyDictionary<PlayerColor, string> NoLabels =
            new Dictionary<PlayerColor, string>();

        public ReplayHeader(
            int format,
            string rulesHash,
            string createdUtc,
            MatchRecipe recipe,
            IReadOnlyDictionary<PlayerColor, IReadOnlyList<string>> fielded,
            IReadOnlyDictionary<PlayerColor, string> seatLabels = null)
        {
            if (format < 1) throw new ArgumentOutOfRangeException(nameof(format));
            if (string.IsNullOrEmpty(rulesHash)) throw new ArgumentException("A replay needs a rules hash.", nameof(rulesHash));

            Format = format;
            RulesHash = rulesHash;
            CreatedUtc = createdUtc ?? string.Empty;
            Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
            Fielded = fielded ?? throw new ArgumentNullException(nameof(fielded));
            SeatLabels = seatLabels ?? NoLabels;

            foreach (var seat in recipe.Seats)
            {
                if (!fielded.ContainsKey(seat))
                    throw new ArgumentException($"{seat} is seated but fields no squad.", nameof(fielded));
            }
        }

        public int Format { get; }
        public string RulesHash { get; }

        /// <summary>
        /// When the match was recorded, ISO 8601 in UTC. Supplied by the
        /// caller: the core never reads the clock, so a test's file is the same
        /// text every run.
        /// </summary>
        public string CreatedUtc { get; }

        public MatchRecipe Recipe { get; }

        /// <summary>Each seat's operators by name, in squad order.</summary>
        public IReadOnlyDictionary<PlayerColor, IReadOnlyList<string>> Fielded { get; }

        /// <summary>
        /// Free text per seat for whoever reads the file ("CPU · BRAWLER").
        /// Display only; the replay player never reads it.
        /// </summary>
        public IReadOnlyDictionary<PlayerColor, string> SeatLabels { get; }
    }
}