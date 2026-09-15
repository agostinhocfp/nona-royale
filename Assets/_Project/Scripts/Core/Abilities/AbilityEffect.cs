// Assets/_Project/Scripts/Core/Abilities/AbilityEffect.cs
using System;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// One thing an ability does. An ability is an ordered list of these, and
    /// order matters — Miracle Pull's execute check has to run before its damage
    /// so the threshold reads health at cast time.
    /// </summary>
    /// <remarks>
    /// A flat struct with factory methods rather than a class hierarchy: the
    /// whole point of going data-driven is that operators four through nine are
    /// content, and content is easier to write, diff and eventually load from a
    /// ScriptableObject when it is plain data.
    /// </remarks>
    public readonly struct AbilityEffect
    {
        private AbilityEffect(
            EffectKind kind, EffectScope scope, EffectAudience audience,
            int amount, DamageType damageType, int radius,
            StatusKind status, int duration, int stacks, double magnitude,
            int bonusIfBleeding, int executeNumerator, int executeDenominator,
            int bonusInOwnZone = 0,
            double critChance = 0.0, int critMultiplier = 1, int heavyCritMultiplier = 1,
            int heavyAboveMaxHealth = 0, int heavyBonus = 0)
        {
            Kind = kind;
            Scope = scope;
            Audience = audience;
            Amount = amount;
            DamageType = damageType;
            Radius = radius;
            Status = status;
            Duration = duration;
            Stacks = stacks;
            Magnitude = magnitude;
            BonusIfBleeding = bonusIfBleeding;
            ExecuteNumerator = executeNumerator;
            ExecuteDenominator = executeDenominator;
            BonusInOwnZone = bonusInOwnZone;
            CritChance = critChance;
            CritMultiplier = critMultiplier;
            HeavyCritMultiplier = heavyCritMultiplier;
            HeavyAboveMaxHealth = heavyAboveMaxHealth;
            HeavyBonus = heavyBonus;
        }

        public EffectKind Kind { get; }
        public EffectScope Scope { get; }
        public EffectAudience Audience { get; }

        /// <summary>
        /// Damage or healing amount, and the distance in cells for
        /// <see cref="EffectKind.PushFromCaster"/>.
        /// </summary>
        public int Amount { get; }

        public DamageType DamageType { get; }

        /// <summary>
        /// Radius for the area scopes, in track steps. "Within N" covers 2N+1
        /// cells. For <see cref="EffectScope.EnemiesInLineFromCaster"/> it is
        /// the line's length ahead of the caster, which covers N cells rather
        /// than 2N+1 — the scope is one-directional and excludes the origin.
        /// </summary>
        public int Radius { get; }

        public StatusKind Status { get; }
        public int Duration { get; }
        public int Stacks { get; }
        public double Magnitude { get; }

        /// <summary>Extra damage when the target already carries a bleed stack. Syla's From the Hip.</summary>
        public int BonusIfBleeding { get; }

        /// <summary>
        /// Extra healing while the caster's side has a lingering zone in play
        /// (ADR-0007). Killzone's rider on Bio-Link Rage.
        /// </summary>
        /// <remarks>
        /// <b>The first number in the game that one ability changes on
        /// another.</b> <see cref="BonusIfBleeding"/> is the nearest precedent
        /// and it is not the same thing: that reads a status on the effect's own
        /// recipient, where this reads the board. It is a field rather than a
        /// second conditional effect because nothing exists that could express
        /// "this effect, but only sometimes".
        /// </remarks>
        public int BonusInOwnZone { get; }

        /// <summary>Execute threshold as a fraction — 1/2 for "below 50%".</summary>
        public int ExecuteNumerator { get; }
        public int ExecuteDenominator { get; }

        /// <summary>
        /// Chance, per recipient, that a damage effect lands as a critical hit
        /// (§2.4). Zero — the default — never rolls, so no existing effect
        /// consumes a random number it did not consume before.
        /// </summary>
        public double CritChance { get; }

        /// <summary>What a critical hit multiplies the damage by.</summary>
        public int CritMultiplier { get; }

        /// <summary>
        /// What a critical hit multiplies the damage by against a heavy target
        /// (see <see cref="HeavyAboveMaxHealth"/>). Vendetta's triple.
        /// </summary>
        public int HeavyCritMultiplier { get; }

        /// <summary>
        /// A target whose <b>maximum</b> health is above this counts as heavy.
        /// Zero means no target is ever heavy. Read by the critical multiplier
        /// and by <see cref="HeavyBonus"/>.
        /// </summary>
        /// <remarks>
        /// Maximum, not current: "heavy" is who an operator is, not how hurt it
        /// is, so a wounded Bouncer is still heavy and the answer never changes
        /// during a match.
        /// </remarks>
        public int HeavyAboveMaxHealth { get; }

        /// <summary>
        /// Extra damage a follow-up strike deals to a heavy target (§6.5).
        /// L's "+2 instead of +1".
        /// </summary>
        public int HeavyBonus { get; }

        /// <summary>Whether an operator with this maximum health counts as heavy for this effect.</summary>
        public bool CountsAsHeavy(int maxHealth) =>
            HeavyAboveMaxHealth > 0 && maxHealth > HeavyAboveMaxHealth;

        /// <summary>A copy with a different radius. For balance sweeps only.</summary>
        public AbilityEffect WithRadius(int radius) =>
            new AbilityEffect(Kind, Scope, Audience, Amount, DamageType, radius,
                Status, Duration, Stacks, Magnitude, BonusIfBleeding,
                ExecuteNumerator, ExecuteDenominator, BonusInOwnZone,
                CritChance, CritMultiplier, HeavyCritMultiplier, HeavyAboveMaxHealth, HeavyBonus);

        /// <summary>
        /// A copy of a damage effect that can land as a critical hit: on a roll
        /// under <paramref name="chance"/> its damage is multiplied by
        /// <paramref name="multiplier"/>, or by <paramref name="heavyMultiplier"/>
        /// against a target whose maximum health is above
        /// <paramref name="heavyAboveMaxHealth"/> (§2.4). Vendetta.
        /// </summary>
        /// <remarks>
        /// A copy method rather than more parameters on <see cref="Damage"/>:
        /// four optional arguments on the most-used factory would make every
        /// existing call site harder to read for one ability's sake.
        ///
        /// The roll is per recipient and per effect, so three blows are three
        /// independent rolls.
        /// </remarks>
        public AbilityEffect WithCritical(
            double chance, int multiplier, int heavyMultiplier = 0, int heavyAboveMaxHealth = 0)
        {
            if (Kind != EffectKind.Damage)
                throw new InvalidOperationException("Only a damage effect can land a critical hit.");
            if (chance <= 0.0 || chance > 1.0) throw new ArgumentOutOfRangeException(nameof(chance));
            if (multiplier < 1) throw new ArgumentOutOfRangeException(nameof(multiplier));
            if (heavyAboveMaxHealth < 0) throw new ArgumentOutOfRangeException(nameof(heavyAboveMaxHealth));

            // No heavy rule stated: a heavy target crits like anyone else.
            if (heavyMultiplier == 0) heavyMultiplier = multiplier;
            if (heavyMultiplier < 1) throw new ArgumentOutOfRangeException(nameof(heavyMultiplier));

            return new AbilityEffect(Kind, Scope, Audience, Amount, DamageType, Radius,
                Status, Duration, Stacks, Magnitude, BonusIfBleeding,
                ExecuteNumerator, ExecuteDenominator, BonusInOwnZone,
                chance, multiplier, heavyMultiplier, heavyAboveMaxHealth, HeavyBonus);
        }

        public static AbilityEffect Damage(
            EffectScope scope, int amount, DamageType type,
            EffectAudience audience = EffectAudience.EnemyOnly,
            int radius = 0, int bonusIfBleeding = 0)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

            return new AbilityEffect(EffectKind.Damage, scope, audience, amount, type, radius,
                default, 0, 0, 0, bonusIfBleeding, 0, 0);
        }

        /// <summary>
        /// Restore health, capped at maximum.
        /// </summary>
        /// <remarks>
        /// <paramref name="radius"/> is only read by the area scopes. A heal
        /// that reaches an area is still audience-scoped by the <i>cast mode</i>,
        /// not by who receives it — Nanite Infusion's splash heal is declared
        /// <see cref="EffectAudience.EnemyOnly"/> because it belongs to the
        /// hostile cast, even though every operator it touches is friendly.
        /// </remarks>
        public static AbilityEffect Heal(
            EffectScope scope, int amount,
            EffectAudience audience = EffectAudience.AllyOnly,
            int radius = 0,
            int bonusInOwnZone = 0)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (bonusInOwnZone < 0) throw new ArgumentOutOfRangeException(nameof(bonusInOwnZone));

            return new AbilityEffect(EffectKind.Heal, scope, audience, amount, default, radius,
                default, 0, 0, 0, 0, 0, 0, bonusInOwnZone);
        }

        /// <summary>
        /// Strip every applied status from the target. Passives survive.
        /// </summary>
        public static AbilityEffect Cleanse(EffectAudience audience = EffectAudience.AllyOnly) =>
            new AbilityEffect(EffectKind.RemoveStatuses, EffectScope.PrimaryTarget, audience,
                0, default, 0, default, 0, 0, 0, 0, 0, 0);

        public static AbilityEffect Status_(
            EffectScope scope, StatusKind status, int duration,
            EffectAudience audience = EffectAudience.EnemyOnly,
            int radius = 0, int stacks = 1, double magnitude = 0.0)
        {
            if (duration < 1) throw new ArgumentOutOfRangeException(nameof(duration));

            return new AbilityEffect(EffectKind.ApplyStatus, scope, audience, 0, default, radius,
                status, duration, stacks, magnitude, 0, 0, 0);
        }

        public static AbilityEffect Pull(EffectAudience audience = EffectAudience.Any) =>
            new AbilityEffect(EffectKind.PullToCaster, EffectScope.PrimaryTarget, audience,
                0, default, 0, default, 0, 0, 0, 0, 0, 0);

        /// <summary>
        /// Push everyone in scope <paramref name="distance"/> cells away from
        /// the caster along the loop. Placement, not movement (§7.4).
        /// </summary>
        /// <remarks>
        /// <b>Takes a scope, unlike <see cref="Pull"/> and <see cref="Swap"/>.</b>
        /// Those two act on a chosen target and can hardcode
        /// <see cref="EffectScope.PrimaryTarget"/>; a shockwave is an area and
        /// pushes whoever is caught in it.
        ///
        /// An operator standing on the caster's own cell has no direction to be
        /// pushed in — the shortest arc between them is zero. It is pushed
        /// <i>backwards</i>, by decision rather than by accident: sharing a cell
        /// with Kian is only possible on a safe cell, which is exactly the free
        /// parking this ability exists to break up.
        /// </remarks>
        public static AbilityEffect Push(
            EffectScope scope, int distance,
            EffectAudience audience = EffectAudience.EnemyOnly,
            int radius = 0)
        {
            if (distance < 1)
                throw new ArgumentOutOfRangeException(nameof(distance),
                    "A push of zero is not a push; omit the effect instead.");

            return new AbilityEffect(EffectKind.PushFromCaster, scope, audience, distance,
                default, radius, default, 0, 0, 0, 0, 0, 0);
        }

        /// <summary>
        /// Paints the targeted cell. It fires at the caster's next upkeep,
        /// dealing <paramref name="totalDamage"/> split between every enemy
        /// within <paramref name="radius"/> of it (ADR-0006).
        /// </summary>
        /// <remarks>
        /// <b>It has no scope</b>, unlike every other factory here. Scopes
        /// resolve to operators, and this effect has no recipients when it is
        /// cast — only a place. <see cref="EffectScope.PrimaryTarget"/> is
        /// recorded so the field is never garbage, and the resolver ignores it.
        ///
        /// The split is what makes it a single-target bet rather than an area
        /// attack: the whole beam on one operator is a heavy hit, and the same
        /// beam divided three ways is less than a collision each. The split
        /// floors. The numbers live on the ability (<c>Kian.DroneStrike</c>).
        /// </remarks>
        public static AbilityEffect PaintCell(
            int totalDamage, int radius, DamageType damageType,
            EffectAudience audience = EffectAudience.Any)
        {
            if (totalDamage < 0) throw new ArgumentOutOfRangeException(nameof(totalDamage));
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));

            return new AbilityEffect(EffectKind.PaintCell, EffectScope.PrimaryTarget, audience,
                totalDamage, damageType, radius, default, 0, 0, 0, 0, 0, 0);
        }

        /// <summary>
        /// Deploys a lingering zone on the targeted cell. It detonates at the
        /// caster's next upkeep for <paramref name="detonationDamage"/> and a
        /// status, then bills <paramref name="lingerDamage"/> for
        /// <paramref name="lingerTicks"/> further turns of the caster's
        /// (ADR-0007).
        /// </summary>
        /// <remarks>
        /// <b>Damage is per target, not split.</b> A beacon is one beam of fixed
        /// energy divided among whoever it catches; a zone is ground that grinds
        /// each of them in full. That opposition is the whole reason the roster
        /// can carry two cell abilities.
        ///
        /// <b>The status lands on the detonation only.</b> Stun blocks movement,
        /// so re-applying it each tick would trap an operator inside the zone
        /// until it expired. See <see cref="EffectKind.DeployZone"/>.
        ///
        /// Scope is recorded as <see cref="EffectScope.PrimaryTarget"/> and
        /// ignored, exactly as <see cref="PaintCell"/> does — this effect names a
        /// place, and scopes resolve to operators.
        /// </remarks>
        public static AbilityEffect DeployZone(
            int detonationDamage, int lingerDamage, int lingerTicks, int radius,
            DamageType damageType, StatusKind detonationStatus, int statusDuration,
            EffectAudience audience = EffectAudience.Any)
        {
            if (detonationDamage < 0) throw new ArgumentOutOfRangeException(nameof(detonationDamage));
            if (lingerDamage < 0) throw new ArgumentOutOfRangeException(nameof(lingerDamage));
            if (lingerTicks < 0) throw new ArgumentOutOfRangeException(nameof(lingerTicks));
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            if (statusDuration < 1) throw new ArgumentOutOfRangeException(nameof(statusDuration));

            // Amount carries the detonation, Magnitude the lingering tick, Stacks
            // the number of those ticks. Reused fields rather than three more on
            // a struct every factory already has to fill — the same trade as the
            // shield pool living in a status entry's magnitude.
            return new AbilityEffect(EffectKind.DeployZone, EffectScope.PrimaryTarget, audience,
                detonationDamage, damageType, radius, detonationStatus, statusDuration,
                lingerTicks, lingerDamage, 0, 0, 0);
        }

        /// <summary>
        /// Caster and target exchange board cells. Placement, not movement — it
        /// collides with nothing and triggers nothing (§7.4).
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="EffectAudience.Any"/>: Mimi's Translocation
        /// swaps with a friend or a foe, and the two modes are the same effect
        /// rather than two branches of one ability.
        ///
        /// A swap can be refused after targeting has already passed — see
        /// <see cref="EffectKind.SwapWithCaster"/>.
        /// </remarks>
        public static AbilityEffect Swap(EffectAudience audience = EffectAudience.Any) =>
            new AbilityEffect(EffectKind.SwapWithCaster, EffectScope.PrimaryTarget, audience,
                0, default, 0, default, 0, 0, 0, 0, 0, 0);

        /// <summary>
        /// Neutralize if health is below <paramref name="numerator"/>/<paramref name="denominator"/>
        /// of maximum at cast time; otherwise deal <paramref name="fallbackAmount"/> instead.
        /// </summary>
        public static AbilityEffect Execute(
            int numerator, int denominator, int fallbackAmount, DamageType fallbackType)
        {
            if (denominator < 1) throw new ArgumentOutOfRangeException(nameof(denominator));

            return new AbilityEffect(EffectKind.Execute, EffectScope.PrimaryTarget, EffectAudience.EnemyOnly,
                fallbackAmount, fallbackType, 0, default, 0, 0, 0, 0, numerator, denominator);
        }

        /// <summary>
        /// Attaches a charge to the target operator. It follows the target and
        /// detonates at the caster's next upkeep on the target's current cell:
        /// <paramref name="splashDamage"/> to every enemy within
        /// <paramref name="radius"/>, plus <paramref name="primaryBonus"/> more
        /// for the marked target, and the status on everyone caught (§6.4).
        /// </summary>
        /// <remarks>
        /// <b>It has no recipients at cast time beyond the mark itself</b>, like
        /// <see cref="PaintCell"/> — but it names an operator rather than a
        /// place, so it keeps <see cref="EffectScope.PrimaryTarget"/> and means
        /// it. The attachment is telegraphed two ways: an event when it lands,
        /// and a <see cref="StatusKind.ZeroDayCharge"/> marker on the target,
        /// which is what a cleanse strips to cancel the detonation (§5.10).
        ///
        /// Reused fields, exactly as <see cref="DeployZone"/> packs its payload:
        /// <c>Amount</c> is the splash, <c>Stacks</c> the primary bonus,
        /// <c>Status</c>/<c>Duration</c> the status everyone caught receives.
        /// </remarks>
        public static AbilityEffect AttachCharge(
            int splashDamage, int primaryBonus, int radius,
            DamageType damageType, StatusKind detonationStatus, int statusDuration,
            EffectAudience audience = EffectAudience.EnemyOnly)
        {
            if (splashDamage < 0) throw new ArgumentOutOfRangeException(nameof(splashDamage));
            if (primaryBonus < 0) throw new ArgumentOutOfRangeException(nameof(primaryBonus));
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            if (statusDuration < 1) throw new ArgumentOutOfRangeException(nameof(statusDuration));

            return new AbilityEffect(EffectKind.AttachCharge, EffectScope.PrimaryTarget, audience,
                splashDamage, damageType, radius, detonationStatus, statusDuration,
                primaryBonus, 0, 0, 0, 0);
        }

        /// <summary>
        /// The caster dashes along the track to the target, dealing
        /// <paramref name="pathDamage"/> to every enemy standing on the cells it
        /// traverses, and is placed one step past the target — or one short of
        /// it when that cell is occupied. Placement, not movement (§7.4, §7.6).
        /// </summary>
        /// <remarks>
        /// <b>Defaults to <see cref="EffectAudience.Any"/></b>: the dash is the
        /// mobility half of the ability and runs in both cast modes. Whatever
        /// the target itself suffers belongs in separate enemy-audience effects
        /// on the ability, which the cast-mode system filters — an ally anchor
        /// is a pure reposition with the path damage still applying.
        /// </remarks>
        public static AbilityEffect Dash(
            int pathDamage, DamageType damageType = DamageType.Normal,
            EffectAudience audience = EffectAudience.Any)
        {
            if (pathDamage < 0) throw new ArgumentOutOfRangeException(nameof(pathDamage));

            return new AbilityEffect(EffectKind.DashToTarget, EffectScope.PrimaryTarget, audience,
                pathDamage, damageType, 0, default, 0, 0, 0, 0, 0, 0);
        }

        /// <summary>
        /// Marks the target for a follow-up strike at the caster's next
        /// upkeep: <paramref name="damage"/>, plus <paramref name="heavyBonus"/>
        /// if the target's maximum health is above
        /// <paramref name="heavyAboveMaxHealth"/> — but only if the caster is
        /// then within <paramref name="withinRange"/> of it (§6.5). Luka's L.
        /// </summary>
        /// <remarks>
        /// Telegraphed the way a Zero-Day charge is: an event at cast time and
        /// a <see cref="StatusKind.Hunted"/> marker on the target, which a
        /// cleanse strips to cancel the strike (§5.13).
        ///
        /// <c>Amount</c> carries the damage and <c>Radius</c> the proximity
        /// requirement — a radius around the target in which the caster must
        /// stand, which is exactly what the field already means.
        /// </remarks>
        public static AbilityEffect FollowUp(
            int damage, int heavyBonus, int heavyAboveMaxHealth, int withinRange,
            DamageType damageType, EffectAudience audience = EffectAudience.EnemyOnly)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (heavyBonus < 0) throw new ArgumentOutOfRangeException(nameof(heavyBonus));
            if (heavyAboveMaxHealth < 0) throw new ArgumentOutOfRangeException(nameof(heavyAboveMaxHealth));
            if (withinRange < 0) throw new ArgumentOutOfRangeException(nameof(withinRange));

            return new AbilityEffect(EffectKind.FollowUp, EffectScope.PrimaryTarget, audience,
                damage, damageType, withinRange, default, 0, 0, 0, 0, 0, 0,
                heavyAboveMaxHealth: heavyAboveMaxHealth, heavyBonus: heavyBonus);
        }
    }
}