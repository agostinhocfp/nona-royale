// Assets/_Project/Scripts/Core/Services/DamagePipeline.cs
using System;
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
    /// </remarks>
    public sealed class DamagePipeline
    {
        private readonly IDamageMitigation _mitigation;
        private readonly IRandom _random;

        public DamagePipeline(IDamageMitigation mitigation, IRandom random)
        {
            _mitigation = mitigation ?? throw new ArgumentNullException(nameof(mitigation));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// Applies one damage instance. Targeting legality is settled before
        /// this point — damage against an illegal target never arrives here.
        /// </summary>
        public DamageResult Apply(OperatorState target, DamageInstance damage)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            // 2. Evasion — Normal only, one charge per round.
            if (damage.Type == DamageType.Normal && _mitigation.TryEvade(target, _random))
                return new DamageResult(DamageOutcome.Evaded, 0, target.Health, target.Id);

            // 3. Shield — Normal only, absorbs the whole instance.
            if (damage.Type == DamageType.Normal && _mitigation.TryAbsorb(target))
                return new DamageResult(DamageOutcome.Absorbed, 0, target.Health, target.Id);

            // 4. Apply.
            int before = target.Health;
            target.SetHealth(before - damage.Amount);
            int applied = before - target.Health;

            // 5. Neutralize check.
            return new DamageResult(
                target.Health <= 0 ? DamageOutcome.Neutralized : DamageOutcome.Dealt,
                applied,
                target.Health,
                target.Id);
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
                caster.Id);
        }
    }
}