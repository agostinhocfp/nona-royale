// Assets/_Project/Scripts/Core/Services/AbilityResolver.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Runs an ability: checks it is legal, pays for it, then walks its effect
    /// list in order.
    /// </summary>
    /// <remarks>
    /// <b>There is no branch on which ability is being used.</b> Every ability is
    /// data (see <see cref="Roster"/>), and this resolver knows only the effect
    /// kinds. Adding operators should not touch this file — if it has to, the
    /// design has introduced a genuinely new mechanic and that is worth an
    /// amendment to <c>COMBAT_SYSTEMS.md</c> rather than an <c>if</c>.
    ///
    /// <b>Order within the effect list is a rule, not a detail.</b> Miracle
    /// Pull's execute check sits first so the threshold reads health at cast
    /// time; Velvet Rope pulls before it damages so the damage lands after
    /// repositioning; and Sonic Disrupter pushes <i>last</i>, because its
    /// recipients are recomputed per effect and a shockwave that moved everyone
    /// out of its own radius first would then fail to slow them.
    /// </remarks>
    public sealed class AbilityResolver
    {
        /// <summary>The cause recorded on ordinary ability damage.</summary>
        public const string AbilityCause = "ability";

        /// <summary>
        /// The cause recorded on a critical hit (§2.4). The view labels it; no
        /// rule reads it.
        /// </summary>
        public const string CriticalCause = "critical";

        private readonly PathMap _map;
        private readonly ITurnClock _clock;
        private readonly EnergyLedger _energy;
        private readonly StatusRegistry _statuses;
        private readonly TargetingRules _targeting;
        private readonly DamagePipeline _damage;
        private readonly DeferredCellEffects _cellEffects;
        private readonly DeferredOperatorEffects _operatorEffects;
        private readonly IRandom _random;

        /// <summary>operator id → ability id → the owner-turn on which it becomes usable again.</summary>
        private readonly Dictionary<int, Dictionary<int, int>> _readyOn =
            new Dictionary<int, Dictionary<int, int>>();

        public AbilityResolver(
            PathMap map,
            ITurnClock clock,
            EnergyLedger energy,
            StatusRegistry statuses,
            TargetingRules targeting,
            DamagePipeline damage,
            DeferredCellEffects cellEffects,
            DeferredOperatorEffects operatorEffects = null,
            IRandom random = null)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _energy = energy ?? throw new ArgumentNullException(nameof(energy));
            _statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            _targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _cellEffects = cellEffects ?? throw new ArgumentNullException(nameof(cellEffects));

            // Optional rather than required: fixtures built before Zero-Day
            // existed construct this resolver without the registry, and one of
            // them is frozen red. Only an AttachCharge effect can tell the
            // difference, and content that lists one is wired with one.
            _operatorEffects = operatorEffects;

            // Optional for the same reason, and with the same mildest failure:
            // without a random source no hit is ever critical (§2.4). Only an
            // effect with a crit chance ever reads it, so every fixture built
            // before Vendetta keeps its exact behaviour — and its dice stream.
            _random = random;
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

        /// <summary>
        /// Every operator this ability could legally be aimed at right now.
        /// Empty for an ability that takes no target.
        /// </summary>
        /// <remarks>
        /// <b>For drawing a target list that is short and true.</b> The view was
        /// offering every operator on the board — eleven buttons at four seats —
        /// and most were never legal for the cast that followed. Range, stealth
        /// and home columns are rules (§4), so the view must not decide them
        /// (<c>PRESENTATION.md</c> §1).
        ///
        /// <b>It runs exactly the target-dependent checks <see cref="Use"/>
        /// runs</b>, in the same order and through the same code: single-target
        /// legality, then whether any effect survives the cast mode, then swap
        /// placement. Anything that answers here is a cast that will be
        /// approved. What it deliberately omits is everything about the
        /// <i>caster</i> — stun, cooldown, energy — because those do not vary by
        /// target and <c>GameEngine.CheckAbility</c> already reports them.
        ///
        /// <b>The caster is included when it is a legal target of its own
        /// ability.</b> Aiming at yourself resolves as a friendly cast by §10's
        /// mode rule, which is a real question the rules have not settled — so
        /// this reports it rather than quietly deciding it. A view that does not
        /// want to offer it can filter one entry.
        ///
        /// <b>A cell-targeted ability answers empty</b>, the same as one that
        /// takes no target: there is no operator to list, and the view picks a
        /// square instead (ADR-0006).
        /// </remarks>
        public IReadOnlyList<OperatorState> LegalTargets(
            OperatorState caster,
            AbilityDefinition ability,
            IReadOnlyList<OperatorState> allOperators)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));
            if (ability == null) throw new ArgumentNullException(nameof(ability));
            if (allOperators == null) throw new ArgumentNullException(nameof(allOperators));

            var legal = new List<OperatorState>();

            if (!ability.RequiresTarget) return legal;
            if (!_targeting.IsInPlay(caster)) return legal;

            foreach (var candidate in allOperators)
            {
                if (candidate == null) continue;

                if (!_targeting.CanSingleTarget(caster, candidate, ability.Range).IsLegal) continue;
                if (!AnyEffectApplies(ability, CastMode(caster, candidate))) continue;
                if (!SwapWouldBeLegal(ability, caster, candidate)) continue;
                if (!AllyPlacementWouldBeLegal(ability, caster, candidate)) continue;

                legal.Add(candidate);
            }

            return legal;
        }

        /// <summary>
        /// Every board cell this ability could legally be aimed at right now
        /// (ADR-0006). Empty for an ability that does not take a cell.
        /// </summary>
        /// <remarks>
        /// The cell-targeted twin of <see cref="LegalTargets"/>, and it exists
        /// for the same reason: the view must not evaluate rules
        /// (PRESENTATION §1), so the picker highlights exactly what a cast
        /// would accept — range measured along the track, home columns
        /// excluded, and the camping rule (§4.4, second amendment) already
        /// applied, so a sheltered caster is offered nothing behind itself.
        /// </remarks>
        public IReadOnlyList<CellRef> LegalCells(OperatorState caster, AbilityDefinition ability)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));
            if (ability == null) throw new ArgumentNullException(nameof(ability));

            var cells = new List<CellRef>();
            if (!ability.RequiresCell) return cells;

            int circuit = _map.Profile.CircuitLength;

            for (int index = 0; index < circuit; index++)
            {
                var cell = CellRef.Track(index);
                if (_targeting.CanTargetCell(caster, cell, ability.Range).IsLegal)
                    cells.Add(cell);
            }

            return cells;
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
            IReadOnlyList<OperatorState> allOperators,
            CellRef? targetCell = null)
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

            // A cell-targeted ability is checked on the same terms and in the
            // same place as an operator-targeted one, so a refusal still costs
            // nothing (ADR-0006). Occupancy is deliberately not checked: an empty
            // cell is a legal target, and betting on one is the ability.
            if (ability.RequiresCell)
            {
                if (targetCell == null)
                    return AbilityResolution.Refused(AbilityRefusal.NoCell);

                var cellVerdict = _targeting.CanTargetCell(caster, targetCell.Value, ability.Range);
                if (!cellVerdict.IsLegal)
                    return AbilityResolution.Refused(AbilityRefusal.IllegalTarget, cellVerdict.Verdict);
            }

            // An ability with both a hostile and a friendly mode picks its mode
            // once, from who was targeted — not per effect recipient. All-In
            // Mauling's self-damage belongs to the hostile cast; on an ally the
            // Bouncer must pay nothing.
            EffectAudience castMode = CastMode(caster, primaryTarget);

            // An ability every one of whose effects is scoped away in this mode
            // would resolve, do nothing, and charge for it. A heal aimed at an
            // enemy was never a legal cast.
            if (!AnyEffectApplies(ability, castMode))
                return AbilityResolution.Refused(AbilityRefusal.IllegalTarget, TargetingVerdict.WrongSide);

            // Legality that depends on where an effect would *put* someone, not
            // on whether the target can be aimed at. Checked here so a refusal
            // still costs nothing, which is the invariant every other refusal
            // upholds.
            //
            // A push is deliberately absent from this check: it clamps rather
            // than refusing, because a self-origin area gives the player no
            // alternative target to pick instead (see EffectKind.PushFromCaster).
            if (!SwapWouldBeLegal(ability, caster, primaryTarget))
            {
                return AbilityResolution.Refused(
                    AbilityRefusal.IllegalTarget, TargetingVerdict.SwapWouldLeaveTheTrack);
            }

            // The ally half of the camping rule (§4.4, second amendment). The
            // enemy and cell halves live in TargetingRules and were checked
            // above; this one depends on what the ability contains, so it is
            // checked here, on the same terms as the swap rule — before
            // payment, so a refusal costs nothing.
            if (!AllyPlacementWouldBeLegal(ability, caster, primaryTarget))
            {
                return AbilityResolution.Refused(
                    AbilityRefusal.IllegalTarget, TargetingVerdict.AimedBehindFromSafeCell);
            }

            // Energy last, so a refusal names the problem the player can fix and
            // an illegal attempt never costs anything.
            if (!_energy.CanAfford(casterPlayer, ability.EnergyCost))
                return AbilityResolution.Refused(AbilityRefusal.InsufficientEnergy);

            _energy.Spend(casterPlayer, ability.EnergyCost);
            PutOnCooldown(caster, ability);

            var outcomes = new List<EffectOutcome>();
            foreach (var effect in ability.Effects)
            {
                if (!AudienceAllows(effect.Audience, castMode)) continue;

                // PaintCell is the one kind with no recipients at cast time, so
                // it cannot go through RunEffect — every scope resolves to a list
                // of operators, and this effect names a place instead. Routed
                // here rather than given a fake scope, which would have made it
                // silently do nothing.
                if (effect.Kind == EffectKind.PaintCell)
                {
                    RunPaintCell(effect, caster, targetCell, outcomes);
                    continue;
                }

                if (effect.Kind == EffectKind.DeployZone)
                {
                    RunDeployZone(effect, caster, targetCell, outcomes);
                    continue;
                }

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
                        // A recipient already brought to zero by an earlier
                        // effect of this same cast is not struck again. The
                        // engine yards it only after the cast resolves, so a
                        // second hit would report a second neutralize — and
                        // pay a second bounty. Vendetta is the first ability
                        // that lands more than one hit on one target.
                        if (IsAlreadyDown(recipient)) break;
                        outcomes.Add(ApplyDamage(effect, caster, recipient));
                        break;

                    case EffectKind.Heal:
                        // Killzone's rider on Bio-Link Rage: the first number in
                        // the game that one ability changes on another
                        // (ADR-0007). Read here rather than baked into the
                        // effect, because whether a zone is live is a fact about
                        // the board at cast time.
                        //
                        // HasActiveZoneFor asks only whether a zone exists.
                        // ZoneCoversOperator asks whether the caster is standing
                        // in one — the same cost to call, and a stronger design,
                        // since it would give him a reason to walk into his own
                        // grenade. Swapping them is a one-word change.
                        int healed = effect.Amount;

                        if (effect.BonusInOwnZone > 0 && _cellEffects.HasActiveZoneFor(caster.Owner))
                            healed += effect.BonusInOwnZone;

                        recipient.Heal(healed);
                        outcomes.Add(EffectOutcome.Healed(recipient, healed));
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

                    case EffectKind.PushFromCaster:
                        // Nothing shoves itself. Guarded rather than assumed:
                        // the scope is enemy-only today, and a future ability
                        // pushing an area that includes the caster would
                        // otherwise compute a direction from a zero-length arc
                        // and fling him backwards.
                        if (ReferenceEquals(recipient, caster)) break;

                        int shoved = PushAwayFromCaster(caster, recipient, effect.Amount);
                        outcomes.Add(EffectOutcome.Pushed(recipient, shoved));
                        break;

                    case EffectKind.SwapWithCaster:
                        SwapWithCaster(caster, recipient, outcomes);
                        break;

                    case EffectKind.RemoveStatuses:
                        foreach (var kind in _statuses.ClearApplied(recipient))
                            outcomes.Add(EffectOutcome.StatusRemoved(recipient, kind));
                        break;

                    case EffectKind.Execute:
                        if (IsAlreadyDown(recipient)) break;
                        outcomes.Add(RunExecute(effect, caster, recipient));
                        break;

                    case EffectKind.AttachCharge:
                        RunAttachCharge(effect, caster, recipient, outcomes);
                        break;

                    case EffectKind.DashToTarget:
                        RunDash(effect, caster, recipient, allOperators, outcomes);
                        break;

                    case EffectKind.FollowUp:
                        RunFollowUp(effect, caster, recipient, outcomes);
                        break;
                }
            }
        }

        /// <summary>
        /// Paints a cell. Nothing resolves now — the beam fires at the caster's
        /// next upkeep (ADR-0006).
        /// </summary>
        /// <remarks>
        /// <b>The null check cannot fire for a cell-targeted ability</b>, which
        /// <see cref="Use"/> has already refused without a cell. It guards the
        /// other case: an ability that is not declared cell-targeted but lists a
        /// paint effect anyway. That is a content bug, and doing nothing quietly
        /// is the mildest thing to do about it — the alternative is throwing at a
        /// player mid-turn for a mistake in a stat block.
        /// </remarks>
        private void RunPaintCell(
            AbilityEffect effect, OperatorState caster, CellRef? cell, List<EffectOutcome> outcomes)
        {
            if (cell == null) return;

            _cellEffects.Paint(
                cell.Value, caster.Owner, caster.Id,
                effect.Amount, effect.Radius, effect.DamageType);

            outcomes.Add(EffectOutcome.BeaconPlaced(caster, cell.Value, effect.Amount));
        }

        /// <summary>
        /// Deploys a lingering zone. Nothing resolves now — it detonates at the
        /// caster's next upkeep and bills again after that (ADR-0007).
        /// </summary>
        /// <remarks>
        /// Reads the payload out of the reused fields the factory packed it into:
        /// <c>Amount</c> is the detonation, <c>Magnitude</c> the lingering tick,
        /// <c>Stacks</c> how many of those. The null guard is the same one
        /// <see cref="RunPaintCell"/> carries, for the same reason.
        /// </remarks>
        private void RunDeployZone(
            AbilityEffect effect, OperatorState caster, CellRef? cell, List<EffectOutcome> outcomes)
        {
            if (cell == null) return;

            _cellEffects.Deploy(
                cell.Value, caster.Owner, caster.Id,
                detonationDamage: effect.Amount,
                lingerDamage: (int)effect.Magnitude,
                lingerTicks: effect.Stacks,
                radius: effect.Radius,
                damageType: effect.DamageType,
                detonationStatus: effect.Status,
                statusDuration: effect.Duration);

            outcomes.Add(EffectOutcome.ZoneDeployed(caster, cell.Value, effect.Amount));
        }

        /// <summary>
        /// Attaches a charge to the target. Nothing detonates now — the charge
        /// follows the target and fires at the caster's next upkeep (§6.4).
        /// </summary>
        /// <remarks>
        /// Reads the payload out of the reused fields the factory packed it
        /// into: <c>Amount</c> is the splash, <c>Stacks</c> the marked target's
        /// bonus, <c>Status</c>/<c>Duration</c> what everyone caught receives.
        ///
        /// <b>The marker is applied here, at cast time, not by the registry.</b>
        /// It is an ordinary status — the badge layer draws it through
        /// <c>ActiveStatusesOn</c> and a cleanse strips it through
        /// <c>ClearApplied</c> — so it goes through the same
        /// <see cref="StatusRegistry.Apply"/> call as every other debuff. The
        /// registry only reads it back at fire time to tell a cleansed charge
        /// from a live one.
        ///
        /// A null <see cref="DeferredOperatorEffects"/> is a wiring bug, not a
        /// content bug, but it is guarded the same way <see cref="RunPaintCell"/>
        /// guards its missing cell: the mildest failure is the charge simply
        /// never existing, and the alternative is throwing at a player mid-turn.
        /// </remarks>
        private void RunAttachCharge(
            AbilityEffect effect, OperatorState caster, OperatorState target, List<EffectOutcome> outcomes)
        {
            if (_operatorEffects == null) return;

            _statuses.Apply(target, StatusKind.ZeroDayCharge,
                DeferredOperatorEffects.MarkerDurationTurns, sourceOperatorId: caster.Id);

            _operatorEffects.Attach(
                target, caster.Owner, caster.Id,
                splashDamage: effect.Amount,
                primaryBonus: effect.Stacks,
                radius: effect.Radius,
                damageType: effect.DamageType,
                status: effect.Status,
                statusDuration: effect.Duration);

            outcomes.Add(EffectOutcome.ChargeAttached(target));
            outcomes.Add(EffectOutcome.StatusApplied(
                target, StatusKind.ZeroDayCharge, DeferredOperatorEffects.MarkerDurationTurns));
        }

        /// <summary>
        /// Sets a follow-up strike on the target. Nothing strikes now — it
        /// resolves at the caster's next upkeep, if the caster is still within
        /// the effect's radius of the target (§6.5).
        /// </summary>
        /// <remarks>
        /// <see cref="RunAttachCharge"/> with a different marker and payload:
        /// the <see cref="StatusKind.Hunted"/> marker is applied here as an
        /// ordinary status so the badge draws it and a cleanse strips it
        /// (§5.13), and the registry reads it back at resolution.
        ///
        /// <b>The heavy bonus is settled now.</b> It reads maximum health,
        /// which never changes during a match, so settling it at cast time is
        /// the same answer as settling it later — and the pending entry then
        /// carries a plain number, like every other deferred payload.
        /// </remarks>
        private void RunFollowUp(
            AbilityEffect effect, OperatorState caster, OperatorState target, List<EffectOutcome> outcomes)
        {
            if (_operatorEffects == null) return;

            _statuses.Apply(target, StatusKind.Hunted,
                DeferredOperatorEffects.MarkerDurationTurns, sourceOperatorId: caster.Id);

            _operatorEffects.SetFollowUp(
                target, caster.Owner, caster.Id,
                damage: effect.Amount,
                heavyBonus: effect.CountsAsHeavy(target.MaxHealth) ? effect.HeavyBonus : 0,
                withinRange: effect.Radius,
                damageType: effect.DamageType);

            outcomes.Add(EffectOutcome.FollowUpMarked(target));
            outcomes.Add(EffectOutcome.StatusApplied(
                target, StatusKind.Hunted, DeferredOperatorEffects.MarkerDurationTurns));
        }

        /// <summary>
        /// Dashes the caster along the track to the target, striking every enemy
        /// on the traversed cells, and places the caster one step past the
        /// target along the dash direction — one short of it, on the caster's
        /// side, when that cell is occupied (§7.6).
        /// </summary>
        /// <remarks>
        /// <b>The dash is placement, never movement.</b> It collides with
        /// nothing and triggers nothing (§7.4), which is why passing through an
        /// occupied cell contests nothing either (§7.1): the only damage the
        /// path deals is the effect's own, declared in the stat block.
        ///
        /// <b>Direction is the shortest way round, exactly as targeting measured
        /// it.</b> Range is a distance in either direction (§4.1), so a target
        /// behind the caster is dashed to against the race direction. A tie at
        /// exactly half the circuit resolves forward — the rule
        /// <see cref="SignedShortestShift"/> already fixes for swaps, reused
        /// rather than re-decided.
        ///
        /// <b>The landing clamp is §7.4's general rule applied to a new case.</b>
        /// A dash moves one operator, and placement that moves one operator
        /// clamps: behind the caster's own start cell there is no path, and past
        /// the last outer-track cell is its home column, which no ability may
        /// reach into (§4.3). The arithmetic is the same conversion every
        /// placement effect performs — cells for the geometry, a signed shift on
        /// the caster's own progress for the application.
        ///
        /// A target sharing the caster's cell has no dash direction — reachable
        /// only on a safe cell. The dash resolves forward, the same arbitrary
        /// but fixed answer <see cref="PushAwayFromCaster"/> gives its own
        /// zero-length arc.
        /// </remarks>
        private void RunDash(
            AbilityEffect effect,
            OperatorState caster,
            OperatorState target,
            IReadOnlyList<OperatorState> allOperators,
            List<EffectOutcome> outcomes)
        {
            int circuit = _map.Profile.CircuitLength;
            int track = _map.Profile.TrackLength;

            int casterCell = _map.CellAt(caster.Owner, caster.Progress).Index;
            int targetCell = _map.CellAt(target.Owner, target.Progress).Index;

            int shift = SignedShortestShift(casterCell, targetCell);
            int step = shift == 0 ? 1 : Math.Sign(shift);
            int distance = Math.Abs(shift);

            // Every enemy standing on a cell the dash traverses, the target's
            // own cell included — the caster passes through it on the way past.
            // The target itself is excluded: whatever it suffers is declared as
            // separate effects on the ability, and striking it here as well
            // would hit it twice.
            //
            // A dash with no path damage — Luka's teleport — strikes nobody on
            // the way. Skipped rather than applied as zero, which would report a
            // zero-damage hit on every enemy passed.
            for (int i = 1; effect.Amount > 0 && i <= distance; i++)
            {
                int index = ((casterCell + step * i) % circuit + circuit) % circuit;

                foreach (var op in allOperators)
                {
                    if (op == null) continue;
                    if (ReferenceEquals(op, caster) || ReferenceEquals(op, target)) continue;
                    if (op.Owner == caster.Owner) continue;
                    if (!_targeting.IsInPlay(op)) continue;
                    if (_map.CellAt(op.Owner, op.Progress).Index != index) continue;

                    outcomes.Add(ApplyDamage(effect, caster, op));
                }
            }

            // One step past the target along the dash direction; if that cell
            // is occupied — by anyone, friend or foe — fall back to one short
            // of it, on the side the caster came from. The fallback itself may
            // be occupied and that is legal: placement onto an occupied cell
            // resolves nothing (§7.4), so there is always somewhere to land.
            int landingCell = ((targetCell + step) % circuit + circuit) % circuit;

            if (IsOccupied(landingCell, caster, allOperators))
                landingCell = ((targetCell - step) % circuit + circuit) % circuit;

            int progress = caster.Progress + SignedShortestShift(casterCell, landingCell);

            if (progress < 0) progress = 0;
            if (progress > track - 1) progress = track - 1;

            caster.MoveTo(progress);
            outcomes.Add(EffectOutcome.Dashed(caster, progress));
        }

        /// <summary>Whether any operator in play stands on the given track cell.</summary>
        private bool IsOccupied(int trackIndex, OperatorState caster, IReadOnlyList<OperatorState> allOperators)
        {
            foreach (var op in allOperators)
            {
                if (op == null) continue;
                if (ReferenceEquals(op, caster)) continue;
                if (!_targeting.IsInPlay(op)) continue;
                if (_map.CellAt(op.Owner, op.Progress).Index == trackIndex) return true;
            }

            return false;
        }

        private EffectOutcome ApplyDamage(AbilityEffect effect, OperatorState caster, OperatorState recipient)
        {
            int amount = effect.Amount;

            if (effect.BonusIfBleeding > 0 && _statuses.IsBleeding(recipient))
                amount += effect.BonusIfBleeding;

            bool selfInflicted = ReferenceEquals(recipient, caster);
            bool critical = !selfInflicted && RollsCritical(effect);

            // The multiplier applies to everything the hit would have dealt,
            // bonuses included, and before mitigation: a critical is a bigger
            // hit, not a hit that ignores armour (§2.4).
            if (critical)
            {
                amount *= effect.CountsAsHeavy(recipient.MaxHealth)
                    ? effect.HeavyCritMultiplier
                    : effect.CritMultiplier;
            }

            string cause = critical ? CriticalCause : AbilityCause;

            // Damage aimed at the caster is self-inflicted and goes straight to
            // health, around every mitigation layer (§2.3).
            DamageResult result = selfInflicted
                ? _damage.ApplyToSelf(caster, amount)
                : _damage.Apply(recipient, new DamageInstance(amount, effect.DamageType, caster.Id, cause));

            return EffectOutcome.Damaged(recipient, result);
        }

        /// <summary>
        /// Rolls for a critical hit. Consumes a random number only when the
        /// effect can crit and a source is wired — no other effect touches the
        /// match's dice stream (§2.4, §9.1).
        /// </summary>
        private bool RollsCritical(AbilityEffect effect)
        {
            if (effect.CritChance <= 0.0 || _random == null) return false;
            return _random.NextDouble() < effect.CritChance;
        }

        /// <summary>
        /// An operator at zero health that the engine has not yarded yet —
        /// struck down earlier in the cast now resolving.
        /// </summary>
        private bool IsAlreadyDown(OperatorState op) =>
            op.Health <= 0 && _targeting.IsInPlay(op);

        private EffectOutcome RunExecute(AbilityEffect effect, OperatorState caster, OperatorState recipient)
        {
            // Integer comparison, evaluated before any damage lands. No
            // fractional health support is needed anywhere in the core.
            bool belowThreshold =
                recipient.Health * effect.ExecuteDenominator < recipient.MaxHealth * effect.ExecuteNumerator;

            if (!belowThreshold)
            {
                var dealt = _damage.Apply(recipient,
                    new DamageInstance(effect.Amount, effect.DamageType, caster.Id, AbilityCause));
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

            int progress = Math.Max(0, target.Progress + SignedShortestShift(targetCell, destinationCell));
            target.MoveTo(progress);
            return progress;
        }

        /// <summary>
        /// Shoves an operator <paramref name="distance"/> cells directly away
        /// from the caster along the loop. Placement, never movement (§7.4).
        /// </summary>
        /// <remarks>
        /// <b>It clamps at both ends and refuses nothing.</b> A push is the only
        /// placement effect with no chosen target, so there is no alternative
        /// the player could have picked instead — refusing would punish them for
        /// a board state they did not author. See
        /// <see cref="EffectKind.PushFromCaster"/>.
        ///
        /// <b>The forward clamp is load-bearing.</b> An enemy standing ahead of
        /// the caster is pushed toward its own home column, and without the stop
        /// at the last outer-track cell a 4-energy ability could carry an
        /// opponent across its home entry — finishing their lap for them. The
        /// backward clamp at progress 0 is the milder twin of the one
        /// <see cref="PlaceAdjacentToCaster"/> already applies.
        ///
        /// <b>Sharing the caster's cell has no direction</b>, and that is
        /// reachable: a safe cell resolves no collision, so enemies can and do
        /// stand on top of each other there. Such an operator is pushed
        /// backwards, which is the decision rather than the accident — a
        /// shockwave that left the free-parkers untouched would miss the
        /// situation the ability exists for.
        /// </remarks>
        private int PushAwayFromCaster(OperatorState caster, OperatorState target, int distance)
        {
            int track = _map.Profile.TrackLength;

            int casterCell = _map.CellAt(caster.Owner, caster.Progress).Index;
            int targetCell = _map.CellAt(target.Owner, target.Progress).Index;

            int shortest = SignedShortestShift(casterCell, targetCell);
            int step = shortest == 0 ? -1 : Math.Sign(shortest);

            int progress = target.Progress + (step * distance);

            if (progress < 0) progress = 0;
            if (progress > track - 1) progress = track - 1;

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

            outcomes.Add(EffectOutcome.Swapped(caster, casterProgress));
            outcomes.Add(EffectOutcome.Swapped(target, targetProgress));
        }

        /// <summary>
        /// Whether at least one of this ability's effects survives the cast
        /// mode's audience filter.
        /// </summary>
        /// <remarks>
        /// Cheap insurance against a whole class of silent failures: a
        /// single-mode ability aimed at the wrong side would otherwise pay its
        /// cost, take its cooldown, and produce an empty outcome list that the
        /// view has nothing to draw from.
        /// </remarks>
        private static bool AnyEffectApplies(AbilityDefinition ability, EffectAudience castMode)
        {
            foreach (var effect in ability.Effects)
                if (AudienceAllows(effect.Audience, castMode)) return true;

            return false;
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
        /// <see cref="PlaceAdjacentToCaster"/> and
        /// <see cref="PushAwayFromCaster"/> perform.
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

            int track = _map.Profile.TrackLength;

            int casterCell = _map.CellAt(caster.Owner, caster.Progress).Index;
            int targetCell = _map.CellAt(target.Owner, target.Progress).Index;

            int shift = SignedShortestShift(casterCell, targetCell);

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

        /// <summary>
        /// Whether this cast survives the ally half of the safe-cell camping
        /// rule: an ability containing a placement effect may not be aimed at
        /// an ally behind a caster standing on a safe cell (§4.4, second
        /// amendment).
        /// </summary>
        /// <remarks>
        /// What it closes is the safe-cell taxi — Translocation handing the
        /// shelter to a teammate, Velvet Rope cycling allies through the camp.
        /// Heals, plates and cleanses still pass backwards: a camper
        /// supporting the squad behind it is not the aggression the rule
        /// exists to stop. Enemy aims need no ability knowledge and are
        /// refused in <see cref="TargetingRules"/>; this half lives here,
        /// beside <see cref="SwapWouldBeLegal"/>, the existing precedent for
        /// legality that depends on what the ability contains rather than on
        /// whether the target can be aimed at.
        /// </remarks>
        private bool AllyPlacementWouldBeLegal(
            AbilityDefinition ability, OperatorState caster, OperatorState primaryTarget)
        {
            if (primaryTarget == null) return true;
            if (caster.Owner != primaryTarget.Owner) return true;
            if (!ability.ContainsPlacement) return true;

            return !_targeting.IsAimedBehindFromSafeCell(
                caster, _targeting.CellOf(primaryTarget));
        }

        /// <summary>
        /// Steps from one track cell to another, signed, by the shorter way
        /// round. Positive is forward along the loop.
        /// </summary>
        /// <remarks>
        /// Because progress moves one-for-one with cells, this doubles as the
        /// shift to apply to an operator's own progress — which is what stops
        /// every placement effect from silently granting or costing a lap. It
        /// was written out three times before it was worth a name.
        ///
        /// At exactly half the circuit it resolves forward. Arbitrary, but fixed
        /// rather than incidental: <see cref="TrySwapProgress"/> depends on both
        /// ends of a swap agreeing on one answer.
        /// </remarks>
        private int SignedShortestShift(int fromCell, int toCell)
        {
            int circuit = _map.Profile.CircuitLength;
            int raw = ((toCell - fromCell) % circuit + circuit) % circuit;

            return raw <= circuit / 2 ? raw : raw - circuit;
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

                case EffectScope.AlliesAroundPrimaryTarget:
                    if (primaryTarget == null) return Array.Empty<OperatorState>();
                    return _targeting.AlliesInArea(
                        _targeting.CellOf(primaryTarget), effect.Radius, caster.Owner, allOperators);

                // Directional, and the radius carries the line's length rather
                // than a symmetric reach.
                case EffectScope.EnemiesInLineFromCaster:
                    return _targeting.EnemiesInLineAhead(caster, effect.Radius, allOperators);

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