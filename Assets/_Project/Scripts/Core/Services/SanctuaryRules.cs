// Assets/_Project/Scripts/Core/Services/SanctuaryRules.cs
using System;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// What a cell protects its occupant from, as opposed to what may be aimed
    /// at it (COMBAT_SYSTEMS §4.4, third amendment, 2026-09-21).
    /// </summary>
    /// <remarks>
    /// <b>Two rules, one answer each.</b>
    /// <list type="bullet">
    /// <item><b>Safe ground is damage-proof.</b> An operator on any safe cell —
    /// every start cell and each home column's mouth — takes no damage, from
    /// any source and of any type.</item>
    /// <item><b>Its own spawn cell is also control-proof.</b> An operator on its
    /// own colour's start cell cannot be slowed or stunned either. Another
    /// seat's start cell is safe ground but not a spawn cell, so a visitor
    /// there is damage-proof and still slowable.</item>
    /// </list>
    ///
    /// <b>This is neither reachability nor mitigation, and the difference is the
    /// whole point.</b> <see cref="TargetingRules"/> decides what may be
    /// <i>aimed</i>; a blast is not aimed at anyone and still arrives. The
    /// pipeline's mitigation layers are what Atomic pierces, and bleed is
    /// Atomic. A shelter that was either of those would leak — an area would
    /// reach it, or a bleed tick would. So the pipeline asks this first, above
    /// everything, and nothing is exempt: not Atomic, not Equilibrium, not an
    /// execute below its threshold.
    ///
    /// <b>What it does not cover.</b> Self-inflicted damage (§2.3) is a price
    /// the caster chose, not damage received, and a shelter that waived it would
    /// make All-In Mauling free on a start cell. Forced movement is not damage:
    /// a push can still shove an operator off a safe cell, which is the
    /// counterplay a damage-proof shelter needs. Statuses other than slow and
    /// stun still land anywhere — a mark on a sheltered operator stays, and
    /// simply bills nothing until it steps off.
    ///
    /// <b>The spawn cell shelters from damage in its own right</b>, not only
    /// because every start cell is currently safe. Both rules are stated here
    /// separately so a board profile whose start cells stopped being safe would
    /// still keep a deploying operator's protection.
    ///
    /// Pure and stateless: a function of the board and where the operator
    /// stands. One instance is built per match and shared by the pipeline, the
    /// status registry and the engine, so there is one answer to ask.
    /// </remarks>
    public sealed class SanctuaryRules
    {
        private readonly PathMap _map;

        public SanctuaryRules(PathMap map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }

        /// <summary>
        /// Whether <paramref name="op"/> takes no damage where it stands: on a
        /// safe cell, or on its own spawn cell.
        /// </summary>
        public bool Shelters(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            // The yard and HOME are off the board, and PathMap.IsSafe already
            // answers false for them; no deployed position is skipped here.
            if (op.Progress < 0) return false;

            return _map.IsSafe(_map.CellAt(op.Owner, op.Progress)) || IsOnOwnSpawnCell(op);
        }

        /// <summary>
        /// Whether <paramref name="op"/> is standing on its own colour's start
        /// cell — where it deploys (§1.3).
        /// </summary>
        /// <remarks>
        /// Compared as a cell, not as progress zero. The two agree on every
        /// board today, since an operator turns into its home column before it
        /// could lap back round to its own start; the cell is the rule, and it
        /// also covers an operator placed back onto its start by a pull clamp
        /// (§7.4).
        /// </remarks>
        public bool IsOnOwnSpawnCell(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            if (!_map.IsOnOuterTrack(op.Progress)) return false;

            return _map.CellAt(op.Owner, op.Progress).Index == _map.StartTrackIndex(op.Owner);
        }

        /// <summary>
        /// Whether <paramref name="kind"/> is refused on <paramref name="op"/>
        /// where it stands: slow and stun, on its own spawn cell.
        /// </summary>
        /// <remarks>
        /// Only a new application is refused. An operator already slowed that
        /// is placed back onto its spawn cell keeps the slow it arrived with —
        /// the rule stops the cell being camped, it is not a cleanse (§5.8 is
        /// the cleanse).
        /// </remarks>
        public bool Resists(OperatorState op, StatusKind kind)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            return IsDisabling(kind) && IsOnOwnSpawnCell(op);
        }

        /// <summary>The statuses a spawn cell refuses: the two that take away a turn's movement.</summary>
        public static bool IsDisabling(StatusKind kind) =>
            kind == StatusKind.Slow || kind == StatusKind.Stun;
    }
}
