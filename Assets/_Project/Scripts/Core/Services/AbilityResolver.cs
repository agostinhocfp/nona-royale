// Assets/_Project/Scripts/Core/Services/AbilityResolver.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Runs an ability: checks it is legal, pays for it, then walks its effect
    /// list in order.
    /// </summary>
    /// <remarks>
    /// <b>There is no branch on which ability is being used.</b> Every ability is
    /// data (see <see cref="AlphaRoster"/>), and this resolver knows only the
    /// five effect kinds. Adding operators four through nine should not touch
    /// this file — if it has to, the design has introduced a genuinely new
    /// mechanic and that is worth an amendment to <c>COMBAT_SYSTEMS.md</c>
    /// rather than an <c>if</c>.
    ///
    /// <b>Order within the effect list is a rule, not a detail.</b> Miracle
    /// Pull's execute check sits first so the threshold reads health at cast
    /// time; Velvet Rope pulls before it damages so the damage lands after
    /// repositioning.
    /// </remarks>
    public sealed class AbilityResolver
    {
        private readonly PathMap _map;
        private readonly ITurnClock _clock;
        private readonly EnergyLedger _energy;
        private readonly StatusRegistry _statuses;
        private readonly TargetingRules _targeting;
        private readonly DamagePipeline _damage;

        /// <summary>operator id → ability id → the owner-turn on which it becomes usable again.</summary>
        private readonly Dictionary<int, Dictionary<int, int>> _readyOn =
            new Dictionary<int, Dictionary<int, int>>();

        public AbilityResolver(
            PathMap map,
            ITurnClock clock,
            EnergyLedger energy,
            StatusRegistry statuses,
            TargetingRules targeting,
            DamagePipeline damage)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _energy = energy ?? throw new ArgumentNullException(nameof(energy));
            _statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
        }

        /// <summary>Whether an ability is off cooldown for this operator.</summary>
        public bool IsReady(OperatorState caster, AbilityDefinition ability)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));
            if (ability == null) throw new ArgumentNullException(nameof(ability));

            if (!_readyOn.TryGetValue(caster.Id, out var byAbility)) return true;
            if (!byAbility.TryGetValue(ability.Id, out int readyTurn)) return true;

            return _clock.TurnIndexOf(caster.Owner) >= readyTurn;
        }

        /// <summary>Clears every cooldown for an operator. Called on neutralize (§1.2).</summary>
        public void ResetCooldowns(OperatorState op)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            _readyOn.Remove(op.Id);
        }

        /// <summary>
        /// Attempts to use an ability. A refusal is an ordinary answer with a
        /// reason attached — players try things they cannot afford, and the view
        /// has to say why.
        /// </summary>
        public AbilityResolution Use(
            OperatorState caster,
            AbilityDefinition ability,
            OperatorState primaryTarget,
            PlayerState casterPlayer,
            IReadOnlyList<OperatorState> allOperators)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));
            if (ability == null) throw new ArgumentNullException(nameof(ability));
            if (casterPlayer == null) throw new ArgumentNullException(nameof(casterPlayer));
            if (allOperators == null) throw new ArgumentNullException(nameof(allOperators));

            if (_statuses.IsStunned(caster))
                return AbilityResolution.Refused(AbilityRefusal.CasterStunned);

            if (!_targeting.IsInPlay(caster))
                return AbilityResolution.Refused(AbilityRefusal.CasterOutOfPlay);

            if (!IsReady(caster, ability))
                return AbilityResolution.Refused(AbilityRefusal.OnCooldown);

            if (ability.RequiresTarget)
            {
                if (primaryTarget == null)
                    return AbilityResolution.Refused(AbilityRefusal.NoTarget);

                var verdict = _targeting.CanSingleTarget(caster, primaryTarget, ability.Range);
                if (!verdict.IsLegal)
                    return AbilityResolution.Refused(AbilityRefusal.IllegalTarget, verdict.Verdict);
            }

            // Energy last, so a refusal names the problem the player can fix and
            // an illegal attempt never costs anything.
            if (!_energy.CanAfford(casterPlayer, ability.EnergyCost))
                return AbilityResolution.Refused(AbilityRefusal.InsufficientEnergy);

            _energy.Spend(casterPlayer, ability.EnergyCost);
            PutOnCooldown(caster, ability);

            // An ability with both a hostile and a friendly mode picks its mode
            // once, from who was targeted — not per effect recipient. All-In
            // Mauling's self-damage belongs to the hostile cast; on an ally the
            // Bouncer must pay nothing.
            EffectAudience castMode = CastMode(caster, primaryTarget);

            var outcomes = new List<EffectOutcome>();
            foreach (var effect in ability.Effects)
            {
                if (!AudienceAllows(effect.Audience, castMode)) continue;
                RunEffect(effect, caster, primaryTarget, allOperators, outcomes);
            }

            return AbilityResolution.Resolved(outcomes);
        }

        // ── Effects ──────────────────────────────────────────────────────

        private void RunEffect(
            AbilityEffect effect,
            OperatorState caster,
            OperatorState primaryTarget,
            IReadOnlyList<OperatorState> allOperators,
            List<EffectOutcome> outcomes)
        {
            foreach (var recipient in Recipients(effect, caster, primaryTarget, allOperators))
            {
                switch (effect.Kind)
                {
                    case EffectKind.Damage:
                        outcomes.Add(ApplyDamage(effect, caster, recipient));
                        break;

                    case EffectKind.Heal:
                        recipient.Heal(effect.Amount);
                        outcomes.Add(EffectOutcome.Healed(recipient, effect.Amount));
                        break;

                    case EffectKind.ApplyStatus:
                        _statuses.Apply(recipient, effect.Status, effect.Duration,
                            effect.Magnitude, effect.Stacks, caster.Id);
                        outcomes.Add(EffectOutcome.StatusApplied(recipient, effect.Status, effect.Duration));
                        break;

                    case EffectKind.PullToCaster:
                        int placed = PlaceAdjacentToCaster(caster, recipient);
                        outcomes.Add(EffectOutcome.Pulled(recipient, placed));
                        break;

                    case EffectKind.Execute:
                        outcomes.Add(RunExecute(effect, caster, recipient));
                        break;
                }
            }
        }

        private EffectOutcome ApplyDamage(AbilityEffect effect, OperatorState caster, OperatorState recipient)
        {
            int amount = effect.Amount;

            if (effect.BonusIfBleeding > 0 && _statuses.IsBleeding(recipient))
                amount += effect.BonusIfBleeding;

            // Damage aimed at the caster is self-inflicted and goes straight to
            // health, around every mitigation layer (§2.3).
            DamageResult result = ReferenceEquals(recipient, caster)
                ? _damage.ApplyToSelf(caster, amount)
                : _damage.Apply(recipient, new DamageInstance(amount, effect.DamageType, caster.Id, "ability"));

            return EffectOutcome.Damaged(recipient, result);
        }

        private EffectOutcome RunExecute(AbilityEffect effect, OperatorState caster, OperatorState recipient)
        {
            // Integer comparison, evaluated before any damage lands. No
            // fractional health support is needed anywhere in the core.
            bool belowThreshold =
                recipient.Health * effect.ExecuteDenominator < recipient.MaxHealth * effect.ExecuteNumerator;

            if (!belowThreshold)
            {
                var dealt = _damage.Apply(recipient,
                    new DamageInstance(effect.Amount, effect.DamageType, caster.Id, "ability"));
                return EffectOutcome.Damaged(recipient, dealt);
            }

            recipient.SetHealth(0);
            return EffectOutcome.Executed(recipient);
        }

        /// <summary>
        /// Places a pulled operator on the last track cell between it and the
        /// caster — adjacent, on the side it came from (§7.4).
        /// </summary>
        /// <remarks>
        /// Placement, never movement: it collides with nothing and triggers
        /// nothing. Landing it on the cell <i>beside</i> the caster rather than
        /// on the caster's own cell also guarantees a pull never leaves two
        /// enemies sharing a contested cell as a side effect.
        ///
        /// A pull that would carry an operator back past its own start cell
        /// clamps there. Its path does not extend behind its start, and the start
        /// is safe, so the clamp is both the only legal placement and a harmless
        /// one. <b>This case is not covered by COMBAT_SYSTEMS §7.4 and is owed a
        /// doc amendment.</b>
        /// </remarks>
        private int PlaceAdjacentToCaster(OperatorState caster, OperatorState target)
        {
            int circuit = _map.Profile.CircuitLength;
            int casterCell = _map.CellAt(caster.Owner, caster.Progress).Index;
            int targetCell = _map.CellAt(target.Owner, target.Progress).Index;

            // Signed shortest way round from the caster to the target, so the
            // destination sits on the side the target actually came from.
            int forward = ((targetCell - casterCell) % circuit + circuit) % circuit;
            int step = forward <= circuit / 2 ? 1 : -1;
            int destinationCell = ((casterCell + step) % circuit + circuit) % circuit;

            // Convert the cell back into the target's own progress, as a signed
            // shift from where it stands, so the pull never silently costs or
            // grants a whole lap.
            int rawShift = ((destinationCell - targetCell) % circuit + circuit) % circuit;
            int shift = rawShift <= circuit / 2 ? rawShift : rawShift - circuit;

            int progress = Math.Max(0, target.Progress + shift);
            target.MoveTo(progress);
            return progress;
        }

        private IEnumerable<OperatorState> Recipients(
            AbilityEffect effect,
            OperatorState caster,
            OperatorState primaryTarget,
            IReadOnlyList<OperatorState> allOperators)
        {
            switch (effect.Scope)
            {
                case EffectScope.Caster:
                    return new[] { caster };

                case EffectScope.PrimaryTarget:
                    return primaryTarget == null
                        ? (IEnumerable<OperatorState>)Array.Empty<OperatorState>()
                        : new[] { primaryTarget };

                case EffectScope.EnemiesAroundCaster:
                    return _targeting.EnemiesInArea(
                        _targeting.CellOf(caster), effect.Radius, caster.Owner, allOperators);

                case EffectScope.EnemiesAroundPrimaryTarget:
                    if (primaryTarget == null) return Array.Empty<OperatorState>();
                    return _targeting.EnemiesInArea(
                        _targeting.CellOf(primaryTarget), effect.Radius, caster.Owner,
                        allOperators, exclude: primaryTarget);

                default:
                    return Array.Empty<OperatorState>();
            }
        }

        /// <summary>
        /// Which mode this cast is in, decided by who was targeted. Area
        /// abilities have no primary target and run every effect they list —
        /// their scopes already restrict them to enemies.
        /// </summary>
        private static EffectAudience CastMode(OperatorState caster, OperatorState primaryTarget)
        {
            if (primaryTarget == null) return EffectAudience.Any;

            return primaryTarget.Owner == caster.Owner
                ? EffectAudience.AllyOnly
                : EffectAudience.EnemyOnly;
        }

        private static bool AudienceAllows(EffectAudience audience, EffectAudience castMode)
        {
            if (audience == EffectAudience.Any) return true;
            if (castMode == EffectAudience.Any) return true;

            return audience == castMode;
        }

        private void PutOnCooldown(OperatorState caster, AbilityDefinition ability)
        {
            if (ability.CooldownTurns == 0) return;

            if (!_readyOn.TryGetValue(caster.Id, out var byAbility))
            {
                byAbility = new Dictionary<int, int>();
                _readyOn[caster.Id] = byAbility;
            }

            // "Cooldown 2" means unusable for the caster's next two turns, so it
            // comes back on the turn after those. Stored as an absolute index for
            // the same reason status timers are: nothing to decrement, nothing to
            // get off by one.
            byAbility[ability.Id] = _clock.TurnIndexOf(caster.Owner) + ability.CooldownTurns + 1;
        }
    }
}