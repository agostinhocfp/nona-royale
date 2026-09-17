// Assets/_Project/Scripts/Core/Services/MovementResolver.cs
using System;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Turns dice into cells: how far an operator moves, what it costs to
    /// deploy, where a move ends, and where a bounced mover lands.
    /// </summary>
    /// <remarks>
    /// <b>Computes, never mutates.</b> Every method returns a value the caller
    /// applies. This matters most for collision: the mover's landing has to be
    /// evaluated <i>before</i> anyone decides whether it keeps the cell or
    /// bounces back a step (COMBAT_SYSTEMS §7.2). A resolver that had already
    /// written the new position would force that into an undo, and undo paths
    /// are where the subtle bugs live.
    ///
    /// It also knows nothing about occupancy. Whether an enemy is standing on
    /// the destination is <c>CollisionResolver</c>'s question; this type only
    /// says where the destination is.
    ///
    /// <b>It knows nothing about how many dice a move spends either.</b>
    /// <see cref="CellsFor"/> takes a pip count, so a pooled two-die move and a
    /// single-die move are the same call with different arguments. Splitting a
    /// roll (§6) therefore needed no change here at all.
    /// </remarks>
    public sealed class MovementResolver
    {
        private readonly PathMap _map;
        private readonly GameConfig _config;

        public MovementResolver(PathMap map, GameConfig config)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// The die face a deploy consumes (§1.3). Exposed so <c>GameEngine</c>
        /// can ask which of the dice it is holding could be spent on a deploy
        /// without reaching for <c>GameConfig</c>, which it does not own.
        /// </summary>
        public int DeployFace => _config.DeployRequirement;

        /// <summary>Straight eligible turns without <see cref="DeployFace"/> before the pity deploy fires. Zero disables.</summary>
        public int PityDeployAfterTurns => _config.PityDeployAfterTurns;

        /// <summary>
        /// Effective speed after auras and slows, floored at
        /// <see cref="GameConfig.MinSpeedMultiplier"/>.
        /// </summary>
        /// <remarks>
        /// The floor exists so stacked slows cannot reach zero. A zero
        /// multiplier is a stun, and stun is a separate mechanic that an
        /// opponent should have to apply on purpose (COMBAT_SYSTEMS §5.2).
        /// </remarks>
        public double EffectiveSpeed(double baseMultiplier, double modifier = 0.0) =>
            Math.Max(_config.MinSpeedMultiplier, baseMultiplier + modifier);

        /// <summary>
        /// Cells moved for a dice value at a given speed:
        /// <c>max(1, floor(dice × speed))</c> at 1.0× and above, but half
        /// cells round UP below 1.0× (COMBAT_SYSTEMS §6, amended 2026-09-15).
        /// </summary>
        /// <remarks>
        /// Floored rather than rounded at 1.0× and above, so a half-step
        /// multiplier never gifts a cell. At 1.5×, a roll of 7 moves 10, not
        /// 11 — and the player can verify it by halving in their head, which
        /// is the readability constraint that fixed the speed band in the
        /// first place.
        ///
        /// <b>Below 1.0× the half cell rounds up (2026-09-15).</b> Sanity was
        /// the first operator who lived under 1.0× permanently (his crawl is a
        /// Burdened passive since 2026-09-17, so today only slows reach here),
        /// and the floor taxed him twice: once by the multiplier, then again on every odd
        /// die — a 5 always moved 2, never 3. For a fast operator the floored
        /// half is a rounding tax on a long move; for the slowest operator
        /// ever fielded it is half of everything he has. So below 1.0× the
        /// half rounds up: a 5 moves 3, a 7 moves 4. The rule is general —
        /// anyone slowed to 0.5× gets the same grace — but 1.5× is untouched:
        /// the "never gifts a cell" argument above still holds at or above
        /// 1.0×, where the floor is the affordable tax.
        ///
        /// <b>The rounding is per move, not per roll</b>, which is what gives
        /// splitting its price. Two dice pooled lose at most one half-cell;
        /// spent separately they can lose one each. The two losses coincide
        /// exactly when both dice are odd — 9 rolls in 36 — and at whole-number
        /// speeds they never happen at all.
        ///
        /// <b>A spent die always moves at least one cell (2026-09-14).</b>
        /// Before the clamp, exactly one case computed zero: a die of 1 under
        /// any slow that takes effective speed below 1.0. Rolling a die and
        /// watching the piece not move reads as the game breaking, not as a
        /// slow biting — so slows may shrink a move but never erase one.
        /// Standing still remains stun's job alone, applied on purpose (§5.2).
        /// The clamp is a rule, not a dial: "a move moves" is in the same
        /// family as "an area includes its origin", and
        /// <see cref="GameConfig.MinSpeedMultiplier"/> stays the tuning knob
        /// for how hard slows bite everywhere else. A dice value of zero still
        /// moves zero — no die was spent, so there is nothing to guarantee.
        /// </remarks>
        public int CellsFor(int diceValue, double effectiveSpeed)
        {
            if (diceValue < 0)
                throw new ArgumentOutOfRangeException(nameof(diceValue));
            if (effectiveSpeed <= 0)
                throw new ArgumentOutOfRangeException(nameof(effectiveSpeed));

            if (diceValue == 0) return 0;

            double exact = diceValue * effectiveSpeed;
            int cells = effectiveSpeed < 1.0
                ? (int)Math.Round(exact, MidpointRounding.AwayFromZero)
                : (int)Math.Floor(exact);

            return Math.Max(1, cells);
        }

        /// <summary>
        /// What this roll offers a player who has operators waiting in the yard.
        /// Reports possibilities only — deployment is optional, so the choice
        /// belongs to the command layer (COMBAT_SYSTEMS §1.3).
        /// </summary>
        /// <remarks>
        /// <b><c>GameEngine</c> no longer calls this.</b> Once the engine began
        /// tracking unspent dice individually (§6), it could answer "may I
        /// deploy" by looking for an unspent <see cref="DeployFace"/> and
        /// "how many" by deploying one per die, which made
        /// <c>DeployOption.TotalIfDeclined</c> and
        /// <c>MovementAfterDeploying</c> redundant. Kept because it is still the
        /// honest way for a caller outside the engine to ask what a roll offers.
        /// </remarks>
        public DeployOption GetDeployOption(DiceRoll roll, int operatorsInYard)
        {
            if (operatorsInYard < 0)
                throw new ArgumentOutOfRangeException(nameof(operatorsInYard));

            int sixes = roll.CountOf(_config.DeployRequirement);

            if (operatorsInYard == 0 || sixes == 0)
                return DeployOption.Unavailable(roll.Total);

            return new DeployOption(
                maxOperators: Math.Min(sixes, operatorsInYard),
                deployFace: _config.DeployRequirement,
                roll: roll);
        }

        /// <summary>
        /// Where an operator ends up after travelling <paramref name="cells"/>.
        /// </summary>
        /// <remarks>
        /// Overshooting HOME finishes rather than bouncing: home entry is
        /// automatic in the MVP and needs no exact roll (ADR-0003).
        /// </remarks>
        public MoveResult ResolveMove(OperatorState op, int cells)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            if (cells < 0)
                throw new ArgumentOutOfRangeException(nameof(cells),
                    "Movement is forward only; pulls and pushes are placement, not movement (COMBAT_SYSTEMS §7.4).");
            if (op.IsInYard)
                throw new InvalidOperationException(
                    $"{op.Name} is in the yard and must be deployed before it can move.");

            int journey = _map.Profile.Journey;
            int from = op.Progress;
            int raw = from + cells;
            int to = Math.Min(raw, journey);

            return new MoveResult(
                from: from,
                to: to,
                destination: _map.CellAt(op.Owner, to),
                finished: to >= journey,
                enteredHomeColumn: _map.IsInHomeColumn(to) && !_map.IsInHomeColumn(from),
                overshot: raw > journey);
        }

        /// <summary>Progress of an operator deployed onto its start cell.</summary>
        public int DeployProgress => 0;

        /// <summary>
        /// Where a bounced mover lands: one step back along its own path
        /// (COMBAT_SYSTEMS §7.2).
        /// </summary>
        /// <remarks>
        /// This is <b>placement, not movement</b> — it triggers nothing. No
        /// second collision, no special space, no home entry.
        ///
        /// The destination always exists. A collision can only happen on a
        /// non-safe cell, and the only cell an operator can occupy immediately
        /// after deploying is its start cell, which is safe — so a mover at
        /// progress 0 can never be the one bouncing.
        /// </remarks>
        public int BounceBackProgress(int progress)
        {
            if (progress <= 0)
                throw new InvalidOperationException(
                    "Nothing can bounce back from the start cell: it is safe, so it is never contested.");

            return progress - 1;
        }
    }
}