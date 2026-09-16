// Assets/_Project/Scripts/Core/Bots/DraftPicker.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Draft;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Bots
{
    /// <summary>
    /// A CPU seat's draft pick (BOTS.md decision 5): the operator its
    /// personality values most, nudged toward a squad with both sustain and
    /// burst, with a little randomness.
    /// </summary>
    /// <remarks>
    /// Picks only from <c>Available(seat)</c> and only what <c>CanPick</c>
    /// accepts. The randomness comes from the bot's own stream, never the
    /// draft's or the match's.
    /// </remarks>
    public static class DraftPicker
    {
        /// <summary>Largest single-ability damage at which an operator counts as burst.</summary>
        public const int BurstThreshold = 3;

        public static OperatorDefinition Choose(
            DraftState draft, PlayerColor seat, BotWeights weights, IRandom random, BotConfig config = null)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            config = config ?? BotConfig.Default;

            if (draft.CanPickAny(seat) != DraftRefusal.None) return null;

            bool hasSustain = false;
            bool hasBurst = false;
            foreach (var picked in draft.PicksOf(seat))
            {
                hasSustain |= HasSustain(picked);
                hasBurst |= Burst(picked) >= BurstThreshold;
            }

            OperatorDefinition best = null;
            double bestScore = double.MinValue;

            foreach (var candidate in draft.Available(seat))
            {
                if (draft.CanPick(seat, candidate) != DraftRefusal.None) continue;

                double score = Value(candidate, weights);
                if (!hasSustain && HasSustain(candidate)) score += weights.DraftComposition;
                if (!hasBurst && Burst(candidate) >= BurstThreshold) score += weights.DraftComposition;
                if (random != null) score += random.NextDouble() * config.DraftJitter;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>What an operator is worth to a personality, before the squad is considered.</summary>
        public static double Value(OperatorDefinition op, BotWeights weights)
        {
            int offence = 0;
            int control = 0;

            foreach (var ability in op.Abilities)
            {
                offence += BotBoard.DamageAgainstOne(ability);

                foreach (var effect in ability.Effects)
                {
                    if (effect.Kind == EffectKind.ApplyStatus &&
                        (effect.Status == StatusKind.Stun || effect.Status == StatusKind.Slow))
                        control++;

                    if (effect.Kind == EffectKind.DeployZone && effect.Status == StatusKind.Stun)
                        control++;
                }
            }

            double speed = op.BaseSpeed + op.PassiveMagnitude;

            return offence * weights.DraftOffence
                   + Burst(op) * weights.DraftBurst
                   + (HasSustain(op) ? weights.DraftSustain : 0.0)
                   + speed * weights.DraftSpeed
                   + op.MaxHealth * weights.DraftHealth
                   + control * weights.DraftControl;
        }

        /// <summary>The operator's largest single-ability damage.</summary>
        public static int Burst(OperatorDefinition op)
        {
            int best = 0;
            foreach (var ability in op.Abilities)
                best = Math.Max(best, BotBoard.DamageAgainstOne(ability));
            return best;
        }

        /// <summary>Whether the operator can heal, shield or cleanse.</summary>
        public static bool HasSustain(OperatorDefinition op)
        {
            foreach (var ability in op.Abilities)
            {
                foreach (var effect in ability.Effects)
                {
                    if (effect.Kind == EffectKind.Heal && effect.Scope != EffectScope.Caster) return true;
                    if (effect.Kind == EffectKind.RemoveStatuses) return true;
                    if (effect.Kind == EffectKind.ApplyStatus && effect.Status == StatusKind.Shield) return true;
                }
            }

            return false;
        }
    }
}