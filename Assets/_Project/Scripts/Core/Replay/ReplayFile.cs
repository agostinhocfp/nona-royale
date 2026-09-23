// Assets/_Project/Scripts/Core/Replay/ReplayFile.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Core.Replay
{
    /// <summary>A replay in memory: the header and every accepted command, in order.</summary>
    public sealed class ReplayFile
    {
        public ReplayFile(ReplayHeader header, IReadOnlyList<ReplayEntry> entries, bool truncated = false)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] == null) throw new ArgumentException($"Entry {i} is null.", nameof(entries));
                if (entries[i].Sequence != i + 1)
                    throw new ArgumentException(
                        $"Entry {i} has sequence {entries[i].Sequence}; entries are numbered 1, 2, 3 without gaps.",
                        nameof(entries));
            }

            Truncated = truncated;
        }

        public ReplayHeader Header { get; }
        public IReadOnlyList<ReplayEntry> Entries { get; }

        /// <summary>
        /// Whether the text ended in half a line, as a file cut short by a crash
        /// does. The complete lines before it are all here; the partial one is
        /// dropped, and this says that it was.
        /// </summary>
        public bool Truncated { get; }
    }
}