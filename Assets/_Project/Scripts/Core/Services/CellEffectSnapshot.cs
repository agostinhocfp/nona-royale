// Assets/_Project/Scripts/Core/Services/CellEffectSnapshot.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// One pending cell effect as the board should show it: where it is, whose
    /// it is, what kind it is, and every cell it will strike.
    /// </summary>
    /// <remarks>
    /// <b>The covered cells come from the core</b> (PRESENTATION §1). The area
    /// is measured along the track, and the view drawing it with its own
    /// arithmetic would be a second copy of the rule that decides who gets
    /// caught. <see cref="Covered"/> is built from the same
    /// <c>TargetingRules.IsInArea</c> test the resolution uses, so the drawn
    /// area and the struck area cannot disagree.
    ///
    /// <b>The owner is included</b> because two seats can hold effects on the
    /// same cell (ADR-0006), and a player needs to know whose bet it is before
    /// deciding whether to step off it.
    ///
    /// Nothing about damage is exposed. A player sees where and when a device
    /// will strike, not what it will do to whom. That is the "never show an
    /// outcome before it is committed" line in PRESENTATION §2.
    /// </remarks>
    public readonly struct CellEffectSnapshot
    {
        public CellEffectSnapshot(
            CellRef cell, PlayerColor owner, bool isZone, bool hasDetonated,
            IReadOnlyList<CellRef> covered)
        {
            Cell = cell;
            Owner = owner;
            IsZone = isZone;
            HasDetonated = hasDetonated;
            Covered = covered ?? Array.Empty<CellRef>();
        }

        /// <summary>The anchored cell, at the centre of the area.</summary>
        public CellRef Cell { get; }

        /// <summary>The seat that placed it.</summary>
        public PlayerColor Owner { get; }

        /// <summary>A lingering zone (ADR-0007) rather than a one-shot beacon (ADR-0006).</summary>
        public bool IsZone { get; }

        /// <summary>
        /// True once a zone has gone off and is only lingering. Always false for
        /// a beacon, which is removed when it fires.
        /// </summary>
        public bool HasDetonated { get; }

        /// <summary>Every outer-track cell the effect will strike, the anchor included.</summary>
        public IReadOnlyList<CellRef> Covered { get; }
    }
}
