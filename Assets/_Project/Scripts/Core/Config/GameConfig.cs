// Assets/_Project/Scripts/Core/Config/GameConfig.cs
using System;

namespace NonaRoyale.Core.Config
{
    /// <summary>
    /// Tunable rule values, as named data rather than numbers typed inline
    /// (CONVENTIONS: "Config, not literals"). Every figure here is a dial the
    /// design expects to turn — ADR-0002 Amendment 2 ranks them in the order
    /// they should be reached for.
    /// </summary>
    /// <remarks>
    /// Immutable, and constructed with defaults, so a test can vary one value
    /// without a mutable global that leaks between tests. There is no static
    /// singleton on purpose: <see cref="Default"/> is a fresh instance, and
    /// services take a config in their constructor (ADR-0004: no <c>.Instance</c>).
    /// </remarks>
    public sealed class GameConfig
    {
        public GameConfig(
            int deployRequirement = 6,
            int maxRollsPerTurn = 3,
            double minSpeedMultiplier = 0.5,
            double speedMultiplierMin = 1.0,
            double speedMultiplierMax = 2.5,
            int diceSides = 6,
            int dicePerRoll = 2,
            int pityDeployAfterTurns = 3)
        {
            if (diceSides < 2)
                throw new ArgumentOutOfRangeException(nameof(diceSides));
            if (dicePerRoll < 1)
                throw new ArgumentOutOfRangeException(nameof(dicePerRoll));
            if (deployRequirement < 1 || deployRequirement > diceSides)
                throw new ArgumentOutOfRangeException(nameof(deployRequirement),
                    $"Deploy requirement must be a face a d{diceSides} can show; was {deployRequirement}.");
            if (maxRollsPerTurn < 1)
                throw new ArgumentOutOfRangeException(nameof(maxRollsPerTurn));
            if (minSpeedMultiplier <= 0)
                throw new ArgumentOutOfRangeException(nameof(minSpeedMultiplier),
                    "A floor of zero would be a permanent stun, not a slow.");
            if (speedMultiplierMax < speedMultiplierMin)
                throw new ArgumentException("Speed band maximum is below its minimum.", nameof(speedMultiplierMax));
            if (pityDeployAfterTurns < 0)
                throw new ArgumentOutOfRangeException(nameof(pityDeployAfterTurns),
                    "Negative makes no sense; zero disables the mechanic.");

            DeployRequirement = deployRequirement;
            MaxRollsPerTurn = maxRollsPerTurn;
            MinSpeedMultiplier = minSpeedMultiplier;
            SpeedMultiplierMin = speedMultiplierMin;
            SpeedMultiplierMax = speedMultiplierMax;
            DiceSides = diceSides;
            DicePerRoll = dicePerRoll;
            PityDeployAfterTurns = pityDeployAfterTurns;
        }

        /// <summary>A die must show this face to deploy an operator (ADR-0003).</summary>
        public int DeployRequirement { get; }

        /// <summary>Initial roll plus doubles re-rolls. Bounds how long one turn can run.</summary>
        public int MaxRollsPerTurn { get; }

        /// <summary>
        /// Floor on effective speed after slows and auras. Not zero: a
        /// multiplier of zero is a stun by another name, and stun is a separate
        /// mechanic that should be applied deliberately (COMBAT_SYSTEMS §5.2).
        /// </summary>
        public double MinSpeedMultiplier { get; }

        /// <summary>Schema bound on an operator's base speed. The alpha roster uses 1.5 and 2.0 only.</summary>
        public double SpeedMultiplierMin { get; }

        /// <summary>
        /// Schema bound on an operator's base speed. Above 2.0 a single move
        /// stops being readable — at 2.5 a double-6 crosses 30 cells of a
        /// 48-cell loop (ADR-0002 Amendment 2).
        /// </summary>
        public double SpeedMultiplierMax { get; }

        public int DiceSides { get; }

        public int DicePerRoll { get; }

        /// <summary>Highest total the dice can show. Used to bound movement sanity checks.</summary>
        public int MaxDiceTotal => DiceSides * DicePerRoll;

        /// <summary>
        /// Bad-luck deploy protection: a turn ending as this player's Nth
        /// straight eligible turn without the deploy face deploys a random yard
        /// operator, free. Zero disables it.
        /// </summary>
        /// <remarks>
        /// <b>At 3 this is a pacing mechanic wearing a protection costume, and
        /// that is the design.</b> P(no deploy face) ≈ 0.69 per roll, so
        /// three-turn droughts are routine — most matches will see this fire.
        /// The consequence is deliberate: full squads within a handful of
        /// turns, and the combat-first priority the sims support (67/33,
        /// 2026-09-14) gets its combatants. It also bounds kill-snowballing,
        /// since it covers re-deployment after neutralize uniformly. Raise to
        /// 4 (~23% per window) if it should feel like rare mercy instead.
        /// </remarks>
        public int PityDeployAfterTurns { get; }

        /// <summary>The shipping values.</summary>
        public static GameConfig Default => new GameConfig();
    }
}