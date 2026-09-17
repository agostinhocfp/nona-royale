// Assets/_Project/Scripts/Core/Services/DamagePipeline.cs
using System;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// The single place health is ever reduced. Abilities, collisions and bleed
    /// ticks all come through here, in one fixed order (COMBAT_SYSTEMS §2.1).
    /// </summary>
    /// <remarks>
    /// <b>Why one choke point.</b> ADR-0004 exists because the old codebase had
    /// the energy formula written twice, and the two copies disagreed. Damage is
    /// the same hazard with more surfaces: collision, six abilities, and bleed
    /// would be five or six chances to forget that Atomic pierces shields.
    /// One method, one order, one set of tests.
    ///
    /// <b>This one mutates, and that is deliberate.</b> Movement computes and
    /// lets the caller commit, because a collision must inspect a landing before
    /// anyone moves. Damage has no such downstream decision — nothing needs the
    /// health it would have had. What collision needs is the *answer*, which
    /// <see cref="DamageResult.TargetSurvived"/> carries.
    ///
    /// <b>What it does not do.</b> Reaching zero health is reported as
    /// <see cref="DamageOutcome.Neutralized"/>; the consequences of neutralizing
    /// — yard, full heal, statuses cleared, progress lost (§1.2) — belong to the
    /// caller that owns the whole game state.
    ///
    /// <b>Every result carries its cause.</b> The label comes straight off the
    /// incoming <see cref="DamageInstance"/> and no rule reads it — it exists so
    /// the view can say what happened. Upkeep damage is why: bleed and mark ticks
    /// land in a phase where nothing else moves, so without a cause an operator
    /// simply loses health and vanishes.
    ///
    /// <b>The two mitigation layers stop the instance differently.</b> Evasion
    /// is terminal — it negates and returns. The shield subtracts, so a shielded
    /// instance usually still reaches step 4, just smaller. That is why
    /// <see cref="DamageOutcome.Absorbed"/> now means "the pool ate all of it",
    /// not "a shield was present".
    /// </remarks>
    public sealed class DamagePipeline
    {
        /// <summary>The cause recorded for damage an operator inflicts on itself (§2.3).</summary>
        private const string SelfCause = "self";

        private readonly IDamageMitigation _mitigation;
        private readonly IRandom _random;
        private readonly CombatConfig _config;

        public DamagePipeline(IDamageMitigation mitigation, IRandom random, CombatConfig config = null)
        {
            _mitigation = mitigation ?? throw new ArgumentNullException(nameof(mitigation));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _config = config ?? CombatConfig.Default;
        }

        /// <summary>
        /// Applies one damage instance. Targeting legality is settled before
        /// this point — damage against an illegal target never arrives here.
        /// </summary>
        public DamageResult Apply(OperatorState target, DamageInstance damage)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            // 0. Equilibrium (§5.17, 2026-09-17). Rescales a cast's hit by the
            // ability's cost before anything else looks at it, every damage
            // type included: it is a price on the caster's choice, not armour,
            // so Atomic's "ignores mitigation" does not reach it. Only hits that
            // carry a cast cost are touched.
            if (damage.CastCost.HasValue && _mitigation.ScalesCastDamage(target))
            {
                damage = new DamageInstance(
                    _config.EquilibriumScale(damage.CastCost.Value, damage.Amount),
                    damage.Type, damage.SourceOperatorId, damage.SourceName, damage.CastCost);
            }

            // Atomic ignores every mitigation layer (§2.2). Stated once, as an
            // explicit gate rather than as something each layer remembers to
            // check: a subtraction is far easier to apply universally by
            // accident than the old pair of early returns was. Tech is
            // mitigable exactly as Normal is; it only adds step 1b.
            bool mitigable = damage.Type != DamageType.Atomic;

            // 1b. Tech ward — Tech only, terminal, consumes nothing (§5.12).
            // Before evasion so a blocked hit never spends the round's charge,
            // and reported as Absorbed because "blocked" is what a player sees
            // (BLOCK). The whole amount is recorded as mitigated.
            //
            // Tech amplifiers, when the first one exists, belong above this
            // line: the ward blocks the amplified hit, not the base one.
            if (damage.Type == DamageType.Tech && _mitigation.BlocksTech(target))
            {
                return new DamageResult(
                    DamageOutcome.Absorbed, 0, target.Health, target.Id,
                    damage.SourceName, damage.Amount);
            }

            // 2. Evasion — Normal and Tech, one charge per round, terminal.
            if (mitigable && _mitigation.TryEvade(target, _random))
            {
                return new DamageResult(
                    DamageOutcome.Evaded, 0, target.Health, target.Id,
                    damage.SourceName, damage.Amount);
            }

            // 3. Shield — Normal and Tech. Subtracts rather than stops (§5.6).
            int mitigated = mitigable ? _mitigation.AbsorbFrom(target, damage.Amount) : 0;

            // Clamped rather than trusted. An implementation returning more than
            // it was offered would make `incoming` negative, and step 4 would
            // quietly *heal* the target — a mitigation bug presenting as a
            // healing bug, at the one place in the game health is written.
            if (mitigated < 0) mitigated = 0;
            if (mitigated > damage.Amount) mitigated = damage.Amount;

            int incoming = damage.Amount - mitigated;

            // `Absorbed` survives for the zero case only. "Reduced to nothing"
            // and "shrugged off" are the same event to a player, and
            // GameEngine.EmitDamage plus FeedbackLayer already draw BLOCK off
            // this outcome — so preserving it means the view needs no change.
            // The `mitigated > 0` guard keeps a 0-damage instance from
            // reporting as absorbed when no shield was involved at all.
            if (mitigated > 0 && incoming == 0)
            {
                return new DamageResult(
                    DamageOutcome.Absorbed, 0, target.Health, target.Id,
                    damage.SourceName, mitigated);
            }

            // 4. Apply.
            int before = target.Health;
            target.SetHealth(before - incoming);
            int applied = before - target.Health;

            // 5. Neutralize check.
            return new DamageResult(
                target.Health <= 0 ? DamageOutcome.Neutralized : DamageOutcome.Dealt,
                applied,
                target.Health,
                target.Id,
                damage.SourceName,
                mitigated);
        }

        /// <summary>
        /// Self-inflicted damage — Bouncer's All-In Mauling. Goes <b>straight to
        /// health</b>, bypassing the pipeline entirely (COMBAT_SYSTEMS §2.3).
        /// </summary>
        /// <remarks>
        /// It cannot be evaded or shielded, and it can neutralize its own
        /// caster. Exposed as its own method rather than as a flag on
        /// <see cref="Apply"/> so the bypass is visible at every call site
        /// instead of hiding behind a boolean argument.
        ///
        /// There is no <see cref="DamageInstance"/> here to take a cause from —
        /// that is the whole point of the bypass — so the label is supplied
        /// directly, and nothing is ever mitigated.
        /// </remarks>
        public DamageResult ApplyToSelf(OperatorState caster, int amount)
        {
            if (caster == null) throw new ArgumentNullException(nameof(caster));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

            int before = caster.Health;
            caster.SetHealth(before - amount);

            return new DamageResult(
                caster.Health <= 0 ? DamageOutcome.Neutralized : DamageOutcome.Dealt,
                before - caster.Health,
                caster.Health,
                caster.Id,
                SelfCause,
                amountMitigated: 0);
        }
    }
}