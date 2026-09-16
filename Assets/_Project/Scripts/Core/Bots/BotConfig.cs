// Assets/_Project/Scripts/Core/Bots/BotConfig.cs
using System;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Bots
{
    /// <summary>
    /// What a CPU seat values, in one currency: <b>points</b>, where one cell
    /// of progress is worth <see cref="Progress"/> (1 by default). Every
    /// preference a bot has is a number here, not a literal in its code
    /// (BOTS.md, rules).
    /// </summary>
    /// <remarks>
    /// <b>These are preferences, never rules.</b> What is legal always comes
    /// from the engine. A weight that is wrong makes a bot play badly; it can
    /// never make it play illegally.
    /// </remarks>
    public sealed class BotWeights
    {
        // ── Movement ────────────────────────────────────────────────────

        /// <summary>Per cell advanced.</summary>
        public double Progress { get; set; } = 1.0;

        /// <summary>Deploying an operator from the yard.</summary>
        public double Deploy { get; set; } = 9.0;

        /// <summary>Stepping off the loop into the home column, where nothing can reach it.</summary>
        public double HomeEntry { get; set; } = 6.0;

        /// <summary>Reaching HOME.</summary>
        public double ReachHome { get; set; } = 10.0;

        /// <summary>Per point of expected damage avoided (or walked into) by where a piece ends.</summary>
        public double Danger { get; set; } = 1.2;

        /// <summary>Per point of expected collision damage dealt by a landing.</summary>
        public double CollisionDamage { get; set; } = 1.5;

        /// <summary>A landing that fails to clear the cell and bounces back.</summary>
        public double Bounce { get; set; } = 2.0;

        // ── Combat values, shared by landings and casts ─────────────────

        /// <summary>Per point of expected damage dealt by an ability.</summary>
        public double Damage { get; set; } = 1.6;

        /// <summary>Neutralizing an enemy operator.</summary>
        public double Kill { get; set; } = 7.0;

        /// <summary>Per cell of progress an enemy loses when neutralized.</summary>
        public double KillProgress { get; set; } = 0.2;

        /// <summary>Per point of health restored to a wounded ally.</summary>
        public double Heal { get; set; } = 1.3;

        /// <summary>Per point of expected danger a protective status covers.</summary>
        public double Protect { get; set; } = 1.0;

        /// <summary>Multiplier on the worth of the harmful statuses a cleanse strips.</summary>
        public double Cleanse { get; set; } = 1.2;

        /// <summary>Per point of damage an operator deals to itself.</summary>
        public double SelfHarm { get; set; } = 1.5;

        /// <summary>Multiplier on delayed effects (beacons, zones, charges, follow-ups): targets may move first.</summary>
        public double DelayedDiscount { get; set; } = 0.6;

        /// <summary>A further multiplier for beacons, which sit on show for a round and are easy to step off.</summary>
        public double BeaconDiscount { get; set; } = 0.6;

        /// <summary>Per cell a push moves an enemy backwards (negative when it carries them forward).</summary>
        public double PushProgress { get; set; } = 0.6;

        /// <summary>Pushing an enemy off a safe cell onto one that can be landed on.</summary>
        public double PushExposure { get; set; } = 2.0;

        /// <summary>
        /// How much of a follow-up landing a push is credited with, when a die
        /// in hand reaches the enemy's new cell. The landing is scored in full
        /// by the move that follows; this is what makes the push worth casting first.
        /// </summary>
        public double PushSetup { get; set; } = 0.8;

        // ── Casting ─────────────────────────────────────────────────────

        /// <summary>Scales everything a cast does to enemies.</summary>
        public double Offence { get; set; } = 1.0;

        /// <summary>Scales everything a cast does for its own side.</summary>
        public double Defence { get; set; } = 1.0;

        /// <summary>Per energy spent, when the pool is not about to overflow.</summary>
        public double EnergyCost { get; set; } = 0.45;

        /// <summary>The least net score a cast needs.</summary>
        public double CastThreshold { get; set; } = 1.0;

        /// <summary>
        /// Banker behaviour: hold energy for the squad's most expensive ready
        /// ability, and spend below it only on a play worth
        /// <see cref="ReserveOverride"/> or more.
        /// </summary>
        public bool SaveForBest { get; set; }

        /// <summary>The net score that justifies breaking the reserve (a kill, or a real rescue).</summary>
        public double ReserveOverride { get; set; } = 7.0;

        // ── Drafting ────────────────────────────────────────────────────

        /// <summary>Per point of damage across an operator's abilities.</summary>
        public double DraftOffence { get; set; } = 0.6;

        /// <summary>Per point of the operator's largest single-ability damage.</summary>
        public double DraftBurst { get; set; } = 1.0;

        /// <summary>Having a heal, shield or cleanse.</summary>
        public double DraftSustain { get; set; } = 1.5;

        /// <summary>Per unit of speed multiplier.</summary>
        public double DraftSpeed { get; set; } = 2.0;

        /// <summary>Per point of maximum health.</summary>
        public double DraftHealth { get; set; } = 0.3;

        /// <summary>Per stun or slow the operator can apply.</summary>
        public double DraftControl { get; set; } = 0.6;

        /// <summary>Bonus for filling a gap: the squad's first sustain, or its first burst.</summary>
        public double DraftComposition { get; set; } = 2.0;

        public BotWeights Clone() => (BotWeights)MemberwiseClone();

        /// <summary>The preset for a personality.</summary>
        public static BotWeights For(BotPersonality personality)
        {
            var w = new BotWeights();

            switch (personality)
            {
                case BotPersonality.Brawler:
                    w.Deploy = 9.0;
                    w.Danger = 0.8;
                    w.CollisionDamage = 2.4;
                    w.Kill = 9.0;
                    w.Offence = 1.3;
                    w.Defence = 0.8;
                    w.EnergyCost = 0.35;
                    w.CastThreshold = 0.8;
                    w.DraftOffence = 0.9;
                    w.DraftBurst = 1.5;
                    w.DraftSustain = 0.8;
                    w.DraftSpeed = 1.0;
                    w.DraftControl = 0.5;
                    break;

                case BotPersonality.Runner:
                    // Offence is lower, not zero: a Runner still takes the kill
                    // in front of it (the designer's condition, BOTS.md §2).
                    w.Progress = 1.3;
                    w.Deploy = 12.0;
                    w.HomeEntry = 8.0;
                    w.ReachHome = 14.0;
                    w.Danger = 1.6;
                    w.CollisionDamage = 1.0;
                    w.Offence = 0.65;
                    w.Defence = 1.3;
                    w.CastThreshold = 2.0;
                    w.DraftOffence = 0.4;
                    w.DraftBurst = 0.6;
                    w.DraftSustain = 1.6;
                    w.DraftSpeed = 4.0;
                    w.DraftControl = 0.6;
                    break;

                case BotPersonality.Banker:
                    // Defence is lower, not zero: a Banker still saves an ally
                    // who is about to fall, if the rescue is worth the reserve.
                    w.Danger = 1.2;
                    w.Offence = 1.15;
                    w.Defence = 0.75;
                    w.EnergyCost = 0.25;
                    w.CastThreshold = 1.6;
                    w.SaveForBest = true;
                    w.ReserveOverride = 4.0;
                    w.DraftOffence = 0.6;
                    w.DraftBurst = 1.3;
                    w.DraftSustain = 1.4;
                    w.DraftSpeed = 1.5;
                    w.DraftControl = 0.9;
                    break;
            }

            return w;
        }
    }

    /// <summary>Table-wide bot settings (BOTS.md).</summary>
    public sealed class BotConfig
    {
        /// <summary>XORed into the match seed for the bots' own stream. Not the draft's salt.</summary>
        public const int DefaultSeedSalt = 0x3C6EF372;

        public BotConfig(
            int maxActionsPerTurn = 40,
            double jitter = 0.35,
            double draftJitter = 1.2,
            double energyHorizon = 3,
            int nearCollisionReach = 6,
            int farCollisionReach = 12,
            double nearCollisionOdds = 0.3,
            double farCollisionOdds = 0.1,
            int seedSalt = DefaultSeedSalt)
        {
            if (maxActionsPerTurn < 4) throw new ArgumentOutOfRangeException(nameof(maxActionsPerTurn));
            if (jitter < 0.0) throw new ArgumentOutOfRangeException(nameof(jitter));
            if (draftJitter < 0.0) throw new ArgumentOutOfRangeException(nameof(draftJitter));
            if (nearCollisionReach < 1 || farCollisionReach < nearCollisionReach)
                throw new ArgumentOutOfRangeException(nameof(farCollisionReach));

            MaxActionsPerTurn = maxActionsPerTurn;
            Jitter = jitter;
            DraftJitter = draftJitter;
            EnergyHorizon = energyHorizon;
            NearCollisionReach = nearCollisionReach;
            FarCollisionReach = farCollisionReach;
            NearCollisionOdds = nearCollisionOdds;
            FarCollisionOdds = farCollisionOdds;
            SeedSalt = seedSalt;
        }

        /// <summary>
        /// Commands one turn may take before the bot ends it regardless. A
        /// guard against a stall, never a pace: a real turn uses a handful.
        /// </summary>
        public int MaxActionsPerTurn { get; }

        /// <summary>Random points added to each option, so equal choices do not always break the same way.</summary>
        public double Jitter { get; }

        /// <summary>Random points added to each draft pick.</summary>
        public double DraftJitter { get; }

        /// <summary>
        /// Energy an enemy is assumed to gain before its next cast, when
        /// judging whether it could afford to hit a cell.
        /// </summary>
        public double EnergyHorizon { get; }

        /// <summary>Cells behind a landing from which an enemy is a likely collision threat.</summary>
        public int NearCollisionReach { get; }

        /// <summary>Cells behind a landing from which an enemy is a possible collision threat.</summary>
        public int FarCollisionReach { get; }

        /// <summary>Rough chance an enemy within the near reach lands on the cell next turn.</summary>
        public double NearCollisionOdds { get; }

        /// <summary>Rough chance an enemy within the far reach lands on the cell next turn.</summary>
        public double FarCollisionOdds { get; }

        public int SeedSalt { get; }

        /// <summary>The bots' RNG for a match seed. Never the match's own stream (BOTS.md decision 4).</summary>
        public IRandom RandomFor(int matchSeed) => new SeededRandom(matchSeed ^ SeedSalt);

        /// <summary>
        /// The bots' RNG for the draft before that match. A stream of its own,
        /// so how many picks the CPUs made cannot change how they play.
        /// </summary>
        public IRandom DraftRandomFor(int matchSeed) => new SeededRandom(~(matchSeed ^ SeedSalt));

        public static BotConfig Default => new BotConfig();
    }
}