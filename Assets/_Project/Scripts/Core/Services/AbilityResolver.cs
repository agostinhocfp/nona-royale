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
    /// six effect kinds. Adding operators four through nine should not touch
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

            // Legality that depends on where an effect would *put* someone, not
            // on whether the target can be aimed at. Checked here so a refusal
            // still costs nothing, which is the invariant every other refusal
            // upholds.
            if (!SwapWouldBeLegal(ability, caster, primaryTarget))
            {
                return AbilityResolution.Refused(
                    AbilityRefusal.IllegalTarget, TargetingVerdict.SwapWouldLeaveTheTrack);
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

                    case EffectKind.SwapWithCaster:
                        SwapWithCaster(caster, recipient, outcomes);
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

        /// <summary>
        /// Exchanges the caster's and the target's board cells. Placement, never
        /// movement: it collides with nothing and triggers nothing (§7.4).
        /// </summary>
        /// <remarks>
        /// Legality was settled before payment by <see cref="TrySwapProgress"/>,
        /// so this cannot fail. Emitting one outcome per operator is deliberate —
        /// two pieces move, and a view told about only one of them would draw a
        /// board that is wrong.
        /// </remarks>
        private void SwapWithCaster(OperatorState caster, OperatorState target, List<EffectOutcome> outcomes)
        {
            int casterProgress, targetProgress;

            if (!TrySwapProgress(caster, target, out casterProgress, out targetProgress))
                return;

            caster.MoveTo(casterProgress);
            target.MoveTo(targetProgress);

            outcomes.Add(EffectOutcome.Pulled(caster, casterProgress));
            outcomes.Add(EffectOutcome.Pulled(target, targetProgress));
        }

        /// <summary>
        /// Whether every swap in this ability could legally resolve, evaluated
        /// before the ability is paid for.
        /// </summary>
        /// <remarks>
        /// Abilities without a swap always pass, so this costs a loop over a
        /// two- or three-item list and nothing else.
        /// </remarks>
        private bool SwapWouldBeLegal(
            AbilityDefinition ability, OperatorState caster, OperatorState primaryTarget)
        {
            EffectAudience castMode = CastMode(caster, primaryTarget);

            foreach (var effect in ability.Effects)
            {
                if (effect.Kind != EffectKind.SwapWithCaster) continue;
                if (!AudienceAllows(effect.Audience, castMode)) continue;
                if (primaryTarget == null) return false;

                int ignoredA, ignoredB;
                if (!TrySwapProgress(caster, primaryTarget, out ignoredA, out ignoredB)) return false;
            }

            return true;
        }

        /// <summary>
        /// Where a swap would leave each operator, or <c>false</c> if it would
        /// carry either of them off its own track.
        /// </summary>
        /// <remarks>
        /// <b>A swap exchanges cells, not progress.</b> Each colour starts at a
        /// different cell, so two operators can sit four cells apart while their
        /// progress values differ by forty. Converting the cell exchange into a
        /// signed shift on each operator's own progress is what stops a swap
        /// silently granting or costing a lap — the same conversion
        /// <see cref="PlaceAdjacentToCaster"/> performs for a pull.
        ///
        /// <b>It is refused rather than clamped, and both ends are checked.</b>
        /// Clamping the way a pull does would not produce a swap at all: an
        /// operator whose new progress went negative would land on its own start
        /// cell instead of the caster's, which is a total progress wipe for the
        /// price of a cheap ability — a harsher punish than neutralizing it.
        /// Forwards is worse still: a caster near the end of the circuit would be
        /// placed inside its own home column, skipping the rest of the loop.
        /// Refusing keeps the ability honest in both directions, and a player can
        /// read the board and see why.
        ///
        /// The target's shift is the exact negation of the caster's rather than a
        /// second shortest-way-round calculation, because at exactly half the
        /// circuit the two calculations agree on direction and would push both
        /// operators the same way.
        /// </remarks>
        private bool TrySwapProgress(
            OperatorState caster, OperatorState target,
            out int casterProgress, out int targetProgress)
        {
            casterProgress = caster.Progress;
            targetProgress = target.Progress;

            if (caster.IsInYard || target.IsInYard) return false;

            int circuit = _map.Profile.CircuitLength;
            int track = _map.Profile.TrackLength;

            int casterCell = _map.CellAt(caster.Owner, caster.Progress).Index;
            int targetCell = _map.CellAt(target.Owner, target.Progress).Index;

            int raw = ((targetCell - casterCell) % circuit + circuit) % circuit;
            int shift = raw <= circuit / 2 ? raw : raw - circuit;

            casterProgress = caster.Progress + shift;
            targetProgress = target.Progress - shift;

            // Both must land on the shared circuit. Below zero is behind an
            // operator's own start, where its path does not exist; at or above
            // the track length is inside its private home column, which no
            // ability may reach into or out of (§4.3).
            if (casterProgress < 0 || casterProgress >= track) return false;
            if (targetProgress < 0 || targetProgress >= track) return false;

            return true;
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

                // Same window, target included. The only difference is whether
                // the primary target was already hit separately by this ability.
                case EffectScope.EnemiesAroundPrimaryTargetInclusive:
                    if (primaryTarget == null) return Array.Empty<OperatorState>();
                    return _targeting.EnemiesInArea(
                        _targeting.CellOf(primaryTarget), effect.Radius, caster.Owner, allOperators);

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