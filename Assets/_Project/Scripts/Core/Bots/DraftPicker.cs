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
    /// A CPU seat's draft pick (BOTS.md decisions 5 and 12): a weighted
    /// lottery over the operators it may take, leaning toward what its
    /// personality values, toward its signature operators, and toward a squad
    /// with both sustain and burst. The Wildcard instead picks uniformly at
    /// random (decision 11, <see cref="BotWeights.DraftAtRandom"/>).
    /// </summary>
    /// <remarks>
    /// <b>A lottery, not the best score plus jitter</b> (2026-10-02). The
    /// old pick took the top score after a 0–1.2 jitter, but the roster's
    /// values spread from about 7 to 20, so the jitter never closed a gap:
    /// every style drafted Nuetu, and the Brawler and Banker almost always
    /// Revú and Sanity too. Each candidate's value is now scaled to 0–1
    /// across what is left, so the spread is the same whatever the roster
    /// numbers become, and turned into a weight; the best is about
    /// e^<see cref="BotWeights.DraftValueSharpness"/> times as likely as the
    /// worst, not certain. With no random stream the heaviest weight wins,
    /// so tests stay deterministic.
    /// </remarks>
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

            if (weights.DraftAtRandom) return AtRandom(draft, seat, random);

            bool hasSustain = false;
            bool hasBurst = false;
            foreach (var picked in draft.PicksOf(seat))
            {
                hasSustain |= HasSustain(picked);
                hasBurst |= Burst(picked) >= BurstThreshold;
            }

            var candidates = new List<OperatorDefinition>();
            var values = new List<double>();
            double lowest = double.MaxValue;
            double highest = double.MinValue;

            foreach (var candidate in draft.Available(seat))
            {
                if (draft.CanPick(seat, candidate) != DraftRefusal.None) continue;

                double value = Value(candidate, weights);
                candidates.Add(candidate);
                values.Add(value);
                lowest = Math.Min(lowest, value);
                highest = Math.Max(highest, value);
            }

            if (candidates.Count == 0) return null;

            var lots = new double[candidates.Count];
            double total = 0.0;
            int heaviest = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                double share = highest > lowest ? (values[i] - lowest) / (highest - lowest) : 1.0;

                double lot = Math.Exp(weights.DraftValueSharpness * share);
                if (IsSignature(candidate, weights)) lot *= weights.DraftSignatureWeight;
                if (!hasSustain && HasSustain(candidate)) lot *= 1.0 + weights.DraftComposition;
                if (!hasBurst && Burst(candidate) >= BurstThreshold) lot *= 1.0 + weights.DraftComposition;

                lots[i] = lot;
                total += lot;
                if (lot > lots[heaviest]) heaviest = i;
            }

            if (random == null || !(total > 0.0)) return candidates[heaviest];

            double ticket = random.NextDouble() * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                ticket -= lots[i];
                if (ticket < 0.0) return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        /// <summary>Whether <paramref name="op"/> is one of the personality's signature operators (BOTS.md decision 12).</summary>
        public static bool IsSignature(OperatorDefinition op, BotWeights weights)
        {
            if (op == null || weights?.DraftSignatures == null) return false;

            foreach (var name in weights.DraftSignatures)
                if (string.Equals(name, op.Name, StringComparison.Ordinal)) return true;

            return false;
        }

        /// <summary>
        /// Any operator the seat may take, each equally likely, drawn from the
        /// bot's own stream. With no stream, the first pickable one, so a
        /// caller without randomness still gets a legal pick.
        /// </summary>
        private static OperatorDefinition AtRandom(DraftState draft, PlayerColor seat, IRandom random)
        {
            var pickable = new List<OperatorDefinition>();
            foreach (var candidate in draft.Available(seat))
                if (draft.CanPick(seat, candidate) == DraftRefusal.None) pickable.Add(candidate);

            if (pickable.Count == 0) return null;
            return random != null ? pickable[random.NextInt(0, pickable.Count)] : pickable[0];
        }

        /// <summary>What an operator is worth to a personality, before the squad is considered.</summary>
        public static double Value(OperatorDefinition op, BotWeights weights)
        {
            int offence = 0;
            int control = 0;
            int tempo = 0;

            foreach (var ability in op.Abilities)
            {
                offence += BotBoard.DamageAgainstOne(ability);

                foreach (var effect in ability.Effects)
                {
                    // A stun aimed at allies is Nano Cell's price, not control.
                    if (effect.Kind == EffectKind.ApplyStatus &&
                        effect.Audience != EffectAudience.AllyOnly &&
                        (effect.Status == StatusKind.Stun || effect.Status == StatusKind.Slow))
                        control++;

                    if (effect.Kind == EffectKind.DeployZone && effect.CarriesStatus &&
                        effect.Status == StatusKind.Stun)
                        control++;

                    // A table takes a run away from whoever walks into it, which is
                    // the same thing a stun buys (§7.7).
                    if (effect.Kind == EffectKind.SetTable) control++;

                    // Dice are tempo, and tempo is what the draft calls speed
                    // (§6.8). Counted once per ability rather than by pips: which
                    // operator ends up spending them is not a draft-time fact.
                    if (effect.Kind == EffectKind.DealDice) tempo++;
                }
            }

            double speed = EffectiveSpeed(op);

            // The House Edge is a pool nobody else has, and a kit of pure tempo
            // would otherwise draft as the worst operator in the game (2026-09-18).
            if (op.Passive == StatusKind.HouseEdge) tempo++;

            return offence * weights.DraftOffence
                   + Burst(op) * weights.DraftBurst
                   + (HasSustain(op) ? weights.DraftSustain : 0.0)
                   + speed * weights.DraftSpeed
                   + op.MaxHealth * weights.DraftHealth
                   + control * weights.DraftControl
                   + tempo * weights.DraftTempo;
        }

        /// <summary>
        /// Speed as a multiplier, with a haste or burden passive folded in at
        /// its average worth: ±1.58 cells on a mean roll of 7, about ±0.23.
        /// </summary>
        /// <remarks>
        /// Without this a burdened Sanity would draft as a 1.0 operator and a
        /// hastened Lethe as a plain one (2026-09-17).
        /// </remarks>
        public static double EffectiveSpeed(OperatorDefinition op)
        {
            const double RollAdjustment = 57.0 / 36.0 / 7.0;   // P(≤6)·1 + P(>6)·2, per mean pip total

            double speed = op.BaseSpeed + op.PassiveMagnitude + op.Passive2Magnitude;
            if (op.Passive == StatusKind.Hastened || op.Passive2 == StatusKind.Hastened) speed += RollAdjustment;
            if (op.Passive == StatusKind.Burdened || op.Passive2 == StatusKind.Burdened) speed -= RollAdjustment;
            return speed;
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