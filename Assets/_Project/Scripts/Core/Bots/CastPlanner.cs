// Assets/_Project/Scripts/Core/Bots/CastPlanner.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;

namespace NonaRoyale.Core.Bots
{
    /// <summary>One cast the current seat could make, with the bot's opinion of it.</summary>
    public readonly struct ScoredCast
    {
        public ScoredCast(OperatorState caster, AbilityDefinition ability, OperatorState target, CellRef? cell,
            double offence, double defence, double score)
        {
            Caster = caster;
            Ability = ability;
            Target = target;
            Cell = cell;
            Offence = offence;
            Defence = defence;
            Score = score;
        }

        public OperatorState Caster { get; }
        public AbilityDefinition Ability { get; }
        public OperatorState Target { get; }
        public CellRef? Cell { get; }

        /// <summary>Weighted value against enemies, before the energy cost.</summary>
        public double Offence { get; }

        /// <summary>Weighted value for the caster's own side, before the energy cost.</summary>
        public double Defence { get; }

        /// <summary>Net value: offence plus defence, less the energy cost.</summary>
        public double Score { get; }

        public ICommand ToCommand() =>
            new UseAbilityCommand(Caster.Id, Ability.Id, Target?.Id, Cell);
    }

    /// <summary>
    /// Every cast the current seat could make right now, scored (BOTS.md
    /// decisions 1 and 2).
    /// </summary>
    /// <remarks>
    /// <b>Only what the engine allows is considered.</b> Casters and abilities
    /// come through <c>CheckAbility</c>; operator targets through
    /// <c>LegalTargetsFor</c>; cells through <c>LegalCellsFor</c>. Never the
    /// caster itself: self-targeting is legal by §10's mode rule, and a bot
    /// that used it would find the Velvet Rope hole at once (see the sim's
    /// <c>Casting</c> notes).
    ///
    /// <b>The value of a cast is estimated from its effect list</b>, effect by
    /// effect, in the cast mode the target implies, the same way the resolver
    /// filters effects. The estimate knows what each effect is for, not
    /// exactly how it resolves; the engine resolves it.
    /// </remarks>
    public static class CastPlanner
    {
        /// <summary>Every legal cast, best first.</summary>
        public static List<ScoredCast> Rank(BotBoard board, BotWeights weights, IRandom random)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (weights == null) throw new ArgumentNullException(nameof(weights));

            var engine = board.Engine;
            var seat = engine.CurrentPlayer;
            var ranked = new List<ScoredCast>();
            if (seat == null || engine.Phase != TurnPhase.Action) return ranked;

            foreach (var caster in seat.Operators)
            {
                foreach (var ability in board.AbilitiesOf(caster))
                {
                    if (engine.CheckAbility(caster, ability) != AbilityAvailability.Ready) continue;

                    switch (ability.Targeting)
                    {
                        case AbilityTargeting.Operator:
                            foreach (var target in engine.LegalTargetsFor(caster, ability))
                            {
                                if (target == null || target.Id == caster.Id) continue;
                                ranked.Add(Score(board, weights, seat, caster, ability, target, null, random));
                            }
                            break;

                        case AbilityTargeting.None:
                            ranked.Add(Score(board, weights, seat, caster, ability, null, null, random));
                            break;

                        case AbilityTargeting.Cell:
                            int radius = CellRadius(ability);
                            foreach (var cell in engine.LegalCellsFor(caster, ability))
                            {
                                // A painted cell nobody stands near is a guess; skip the empty ones.
                                if (board.EnemiesNear(seat.Color, cell, radius).Count == 0) continue;
                                ranked.Add(Score(board, weights, seat, caster, ability, null, cell, random));
                            }
                            break;
                    }
                }
            }

            ranked.Sort((a, b) => b.Score.CompareTo(a.Score));
            return ranked;
        }

        /// <summary>The most expensive ability the seat could ever cast now, ignoring energy: the Banker's savings target.</summary>
        public static int SavingsTarget(BotBoard board)
        {
            var seat = board.Engine.CurrentPlayer;
            if (seat == null) return 0;

            int best = 0;
            foreach (var op in seat.Operators)
            {
                if (!board.OnLoop(op)) continue;
                foreach (var ability in board.AbilitiesOf(op))
                    if (ability.EnergyCost > best && ability.EnergyCost <= board.Engine.EnergyCap)
                        best = ability.EnergyCost;
            }

            return best;
        }

        public static ScoredCast Score(
            BotBoard board, BotWeights weights, PlayerState seat,
            OperatorState caster, AbilityDefinition ability, OperatorState target, CellRef? cell,
            IRandom random)
        {
            var mode = target == null
                ? EffectAudience.Any
                : target.Owner == caster.Owner ? EffectAudience.AllyOnly : EffectAudience.EnemyOnly;

            double offence = 0.0;
            double defence = 0.0;

            foreach (var effect in ability.Effects)
            {
                if (!Applies(effect.Audience, mode)) continue;
                Value(board, weights, caster, ability, target, cell, effect, ref offence, ref defence);
            }

            double offenceScore = offence * weights.Offence;
            double defenceScore = defence * weights.Defence;

            // Energy about to overflow the cap is burned anyway, so spending it costs less.
            double cost = ability.EnergyCost * weights.EnergyCost;
            if (seat.Energy + board.Config.EnergyHorizon > board.Engine.EnergyCap) cost *= 0.4;

            double jitter = random == null ? 0.0 : random.NextDouble() * board.Config.Jitter;
            double score = offenceScore + defenceScore - cost + jitter;

            return new ScoredCast(caster, ability, target, cell, offenceScore, defenceScore, score);
        }

        // ── Effect values ────────────────────────────────────────────────

        private static void Value(
            BotBoard board, BotWeights w, OperatorState caster, AbilityDefinition ability,
            OperatorState target, CellRef? cell,
            AbilityEffect effect, ref double offence, ref double defence)
        {
            var own = caster.Owner;

            switch (effect.Kind)
            {
                case EffectKind.Damage:
                    foreach (var r in Recipients(board, caster, target, effect))
                    {
                        int amount = effect.Amount;
                        if (effect.BonusIfBleeding > 0 && board.Has(r, StatusKind.Bleed)) amount += effect.BonusIfBleeding;

                        double hit = board.ExpectedHit(r, amount, effect.DamageType);
                        if (r.Owner == own) defence -= SelfHarm(w, r, hit);
                        else
                        {
                            offence += Hit(board, w, r, hit);

                            // Lifesteal (Vendetta): the caster heals what the hit
                            // removes, up to what it is missing. Each blow is
                            // valued alone, so a nearly-full caster is slightly
                            // over-credited across several blows.
                            if (effect.Lifesteal)
                            {
                                int missing = caster.MaxHealth - caster.Health;
                                double drained = Math.Min(Math.Min(hit, r.Health), missing);
                                if (drained > 0.0) defence += drained * w.Heal;
                            }
                        }
                    }
                    break;

                case EffectKind.Heal:
                    foreach (var r in Recipients(board, caster, target, effect))
                    {
                        if (r.Owner != own) continue;
                        int missing = r.MaxHealth - r.Health;
                        if (missing <= 0) continue;

                        double urgency = 1.0 + Danger(board, r) / Math.Max(1, r.Health);
                        defence += Math.Min(effect.Amount + effect.BonusInOwnZone, missing) * w.Heal * urgency;
                    }
                    break;

                case EffectKind.ApplyStatus:
                    foreach (var r in Recipients(board, caster, target, effect))
                    {
                        if (r.Owner == own) defence += Buff(board, w, r, effect);
                        else offence += Debuff(board, r, effect);
                    }
                    break;

                case EffectKind.Execute:
                    if (target == null || target.Owner == own) break;
                    bool below = target.Health * effect.ExecuteDenominator < target.MaxHealth * effect.ExecuteNumerator;
                    offence += below
                        ? Kill(board, w, target)
                        : Hit(board, w, target, board.ExpectedHit(target, effect.Amount, effect.DamageType));
                    break;

                case EffectKind.PullToCaster:
                    // Dragging an enemy next to the caster sets up a landing; a small plus.
                    if (target != null && target.Owner != own) offence += 0.5;
                    break;

                case EffectKind.SwapWithCaster:
                    offence += SwapValue(board, w, caster, target);
                    break;

                case EffectKind.RemoveStatuses:
                    if (target != null && target.Owner == own)
                        defence += HarmfulWorth(board, target) * w.Cleanse;
                    break;

                case EffectKind.PushFromCaster:
                    foreach (var r in Recipients(board, caster, target, effect))
                    {
                        if (r.Owner == own || !board.OnLoop(r)) continue;
                        double before = HitFromThisCast(board, caster, ability, target, r);
                        offence += PushValue(board, w, caster, r, effect.Amount, before, board.ReachableTrackCells());
                    }
                    break;

                case EffectKind.PaintCell:
                    if (cell.HasValue)
                    {
                        var caught = board.EnemiesNear(own, cell.Value, effect.Radius);
                        if (caught.Count == 0) break;
                        int share = effect.Amount / caught.Count;
                        // A beacon is on show for a round, and anyone can step off it.
                        foreach (var r in caught)
                            offence += Hit(board, w, r, board.ExpectedHit(r, share, effect.DamageType))
                                       * w.DelayedDiscount * w.BeaconDiscount;
                    }
                    break;

                case EffectKind.DeployZone:
                    if (cell.HasValue)
                    {
                        var caught = board.EnemiesNear(own, cell.Value, effect.Radius);

                        // A crowd zone bills each victim once per other victim
                        // (Eris' Exploit), so a lone target is worth nothing.
                        int crowd = effect.ScalesWithCrowd ? caught.Count - 1 : 1;
                        int total = crowd * (effect.Amount + (int)Math.Round(effect.Magnitude * effect.Stacks));
                        double status = effect.CarriesStatus ? StatusWorth(effect.Status) : 0.0;

                        foreach (var r in caught)
                        {
                            double value = Hit(board, w, r, board.ExpectedHit(r, total, effect.DamageType)) + status;
                            offence += value * w.DelayedDiscount;
                        }
                    }
                    break;

                case EffectKind.AttachCharge:
                    if (target == null || target.Owner == own || !board.OnLoop(target)) break;
                    offence += Hit(board, w, target,
                        board.ExpectedHit(target, effect.Amount + effect.Stacks, effect.DamageType)) * w.DelayedDiscount;
                    foreach (var r in board.EnemiesNear(own, board.CellOf(target), effect.Radius))
                    {
                        if (r.Id == target.Id) continue;
                        offence += Hit(board, w, r, board.ExpectedHit(r, effect.Amount, effect.DamageType)) * w.DelayedDiscount;
                    }
                    break;

                case EffectKind.FollowUp:
                    if (target == null || target.Owner == own) break;
                    int follow = effect.Amount + (effect.CountsAsHeavy(target.MaxHealth) ? effect.HeavyBonus : 0);
                    offence += Hit(board, w, target, board.ExpectedHit(target, follow, effect.DamageType)) * w.DelayedDiscount;
                    break;

                case EffectKind.DashToTarget:
                    // The dash itself is positional; its path damage is small and its
                    // real payload is the effects that follow it in the list.
                    break;
            }
        }

        /// <summary>
        /// What pushing one enemy is worth (the designer's read of Kian,
        /// BOTS.md log): cells it loses (a forward push is a gift and scores
        /// negative), being moved off a safe cell, and above all a die in hand
        /// that can land on its new cell and finish it.
        /// </summary>
        /// <param name="damageFirst">Damage this cast lands on the enemy before the push.</param>
        /// <param name="reachable">Track cells the seat can land on with the dice in hand.</param>
        public static double PushValue(
            BotBoard board, BotWeights w, OperatorState caster, OperatorState enemy, int distance,
            double damageFirst, ICollection<int> reachable)
        {
            if (damageFirst >= enemy.Health) return 0.0;   // already counted as a kill

            var map = board.Map;
            int to = board.PredictPush(caster, enemy, distance);
            var fromCell = board.CellOf(enemy);
            var toCell = board.CellAt(enemy.Owner, to);

            double value = (enemy.Progress - to) * w.PushProgress;

            bool exposed = !map.IsSafe(toCell);
            if (map.IsSafe(fromCell) && exposed) value += w.PushExposure;

            if (exposed && reachable != null && reachable.Contains(toCell.Index))
            {
                double remaining = enemy.Health - damageFirst;
                double hit = board.ExpectedHit(enemy, board.Combat.CollisionDamage, DamageType.Normal);

                value += w.PushSetup * (hit >= remaining
                    ? w.Kill + remaining * w.CollisionDamage + Math.Max(0, to) * w.KillProgress
                    : hit * w.CollisionDamage);
            }

            return value;
        }

        /// <summary>Expected damage the cast's own damage effects land on one recipient.</summary>
        private static double HitFromThisCast(
            BotBoard board, OperatorState caster, AbilityDefinition ability, OperatorState target, OperatorState recipient)
        {
            double total = 0.0;
            foreach (var effect in ability.Effects)
            {
                if (effect.Kind != EffectKind.Damage || effect.Scope == EffectScope.Caster) continue;
                foreach (var r in Recipients(board, caster, target, effect))
                    if (r.Id == recipient.Id) total += board.ExpectedHit(r, effect.Amount, effect.DamageType);
            }
            return total;
        }

        /// <summary>Who an effect lands on, by its scope, as far as the bot can tell.</summary>
        private static List<OperatorState> Recipients(
            BotBoard board, OperatorState caster, OperatorState target, AbilityEffect effect)
        {
            var own = caster.Owner;
            var list = new List<OperatorState>();

            switch (effect.Scope)
            {
                case EffectScope.PrimaryTarget:
                    if (target != null) list.Add(target);
                    break;

                case EffectScope.Caster:
                    list.Add(caster);
                    break;

                case EffectScope.EnemiesAroundCaster:
                    if (board.OnLoop(caster)) list.AddRange(board.EnemiesNear(own, board.CellOf(caster), effect.Radius));
                    break;

                case EffectScope.EnemiesAroundPrimaryTarget:
                case EffectScope.EnemiesAroundPrimaryTargetInclusive:
                    if (target == null || !board.OnLoop(target)) break;
                    bool inclusive = effect.Scope == EffectScope.EnemiesAroundPrimaryTargetInclusive;
                    foreach (var r in board.EnemiesNear(own, board.CellOf(target), effect.Radius))
                        if (inclusive || r.Id != target.Id) list.Add(r);
                    break;

                case EffectScope.AlliesAroundPrimaryTarget:
                    if (target != null && board.OnLoop(target))
                        list.AddRange(board.AlliesNear(own, board.CellOf(target), effect.Radius));
                    break;

                case EffectScope.EnemiesInLineFromCaster:
                    if (board.OnLoop(caster)) list.AddRange(board.EnemiesAhead(own, board.CellOf(caster), effect.Radius));
                    break;
            }

            return list;
        }

        private static double Hit(BotBoard board, BotWeights w, OperatorState enemy, double damage)
        {
            if (damage <= 0.0) return 0.0;
            if (damage >= enemy.Health) return Kill(board, w, enemy);
            return damage * w.Damage;
        }

        private static double Kill(BotBoard board, BotWeights w, OperatorState enemy) =>
            enemy.Health * w.Damage + w.Kill + Math.Max(0, enemy.Progress) * w.KillProgress;

        private static double SelfHarm(BotWeights w, OperatorState self, double damage)
        {
            if (damage <= 0.0) return 0.0;
            // Killing yourself costs the whole journey so far.
            if (damage >= self.Health) return 50.0 + Math.Max(0, self.Progress) * w.KillProgress;
            return damage * w.SelfHarm;
        }

        private static double Danger(BotBoard board, OperatorState op) =>
            board.OnLoop(op) ? board.Threat(op.Owner, board.CellOf(op)) : 0.0;

        private static double Buff(BotBoard board, BotWeights w, OperatorState ally, AbilityEffect effect)
        {
            if (effect.Status != StatusKind.Hastened && board.Has(ally, effect.Status)) return 0.0;

            double danger = Danger(board, ally) * board.Fragility(ally);

            switch (effect.Status)
            {
                case StatusKind.Shield:
                    double pool = effect.Magnitude > 0 ? effect.Magnitude : board.Combat.ShieldPoolDefault;
                    return (Math.Min(pool, danger) + 0.3) * w.Protect;
                case StatusKind.TechWard: return (danger * 0.35 + 0.2) * w.Protect;
                case StatusKind.Stealth: return (danger * 0.5 + 0.3) * w.Protect;
                case StatusKind.Evasion: return (danger * 0.3 + 0.2) * w.Protect;
                case StatusKind.Hastened: return 1.0;

                // Stunning a friend is a price, not a buff: Nano Cell pays for
                // its bubble with the ally's next turn. Costed at what the bot
                // thinks stunning an enemy is worth.
                case StatusKind.Stun: return -StatusWorth(StatusKind.Stun);

                default: return 0.0;
            }
        }

        private static double Debuff(BotBoard board, OperatorState enemy, AbilityEffect effect)
        {
            // A second stun or slow adds nothing; bleed stacks.
            if (effect.Status != StatusKind.Bleed && board.Has(enemy, effect.Status)) return 0.0;
            return StatusWorth(effect.Status) * (effect.Status == StatusKind.Bleed ? Math.Max(1, effect.Stacks) : 1);
        }

        /// <summary>
        /// What a cleanse is worth on this ally: the statuses it would strip, by
        /// how much each hurts, less the shield it would strip with them.
        /// </summary>
        /// <remarks>
        /// <b>The shield counts against it (2026-09-17).</b> A cleanse is
        /// indiscriminate (§5.8). Without this, a Nano Cell's stun read as a
        /// harm to wash out and the bot popped a bubble its own side had just
        /// paid for, in front of the enemy it was protecting from. With it, a
        /// bubble is popped only when the ally is in no danger — which is when
        /// freeing it to move is right.
        /// </remarks>
        private static double HarmfulWorth(BotBoard board, OperatorState ally)
        {
            double worth = 0.0;
            if (board.Has(ally, StatusKind.Stun)) worth += StatusWorth(StatusKind.Stun);
            if (board.Has(ally, StatusKind.Slow)) worth += StatusWorth(StatusKind.Slow);
            if (board.Has(ally, StatusKind.Bleed)) worth += StatusWorth(StatusKind.Bleed);
            if (board.Has(ally, StatusKind.Mark)) worth += StatusWorth(StatusKind.Mark);
            if (board.Has(ally, StatusKind.Hunted)) worth += StatusWorth(StatusKind.Hunted);
            if (board.Has(ally, StatusKind.ZeroDayCharge)) worth += 2.0;
            if (board.Has(ally, StatusKind.Shield))
                worth -= Math.Min(board.ShieldPool(ally), Danger(board, ally));
            return worth * board.Fragility(ally);
        }

        private static double StatusWorth(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Stun: return 2.5;
                case StatusKind.Slow: return 1.0;
                case StatusKind.Bleed: return 1.2;
                case StatusKind.Mark: return 3.0;
                case StatusKind.Hunted: return 1.0;
                default: return 0.0;
            }
        }

        /// <summary>
        /// A swap trades cells. Against an enemy ahead of the caster on the loop,
        /// the caster gains that gap and the enemy loses it.
        /// </summary>
        private static double SwapValue(BotBoard board, BotWeights w, OperatorState caster, OperatorState target)
        {
            if (target == null || target.Owner == caster.Owner) return 0.0;
            if (!board.OnLoop(caster) || !board.OnLoop(target)) return 0.0;

            int gap = board.ForwardGap(board.CellOf(caster), board.CellOf(target));
            if (gap <= 0 || gap > board.Circuit / 2) return 0.0;

            return gap * w.Progress * 1.5;
        }

        private static int CellRadius(AbilityDefinition ability)
        {
            int radius = 0;
            foreach (var effect in ability.Effects)
                if (effect.Radius > radius) radius = effect.Radius;
            return radius;
        }

        /// <summary>The resolver's cast-mode filter (AbilityResolver.AudienceAllows), restated for scoring only.</summary>
        private static bool Applies(EffectAudience audience, EffectAudience mode) =>
            audience == EffectAudience.Any || mode == EffectAudience.Any || audience == mode;
    }
}