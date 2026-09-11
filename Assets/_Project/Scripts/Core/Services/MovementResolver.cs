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
        /// <c>floor(dice × speed)</c> (COMBAT_SYSTEMS §6).
        /// </summary>
        /// <remarks>
        /// Floored rather than rounded so a half-step multiplier never gifts a
        /// cell. At 1.5×, a roll of 7 moves 10, not 11 — and the player can
        /// verify it by halving in their head, which is the readability
        /// constraint that fixed the speed band in the first place.
        /// </remarks>
        public int CellsFor(int diceValue, double effectiveSpeed)
        {
            if (diceValue < 0)
                throw new ArgumentOutOfRangeException(nameof(diceValue));
            if (effectiveSpeed <= 0)
                throw new ArgumentOutOfRangeException(nameof(effectiveSpeed));

            return (int)Math.Floor(diceValue * effectiveSpeed);
        }

        /// <summary>
        /// What this roll offers a player who has operators waiting in the yard.
        /// Reports possibilities only — deployment is optional, so the choice
        /// belongs to the command layer (COMBAT_SYSTEMS §1.3).
        /// </summary>
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