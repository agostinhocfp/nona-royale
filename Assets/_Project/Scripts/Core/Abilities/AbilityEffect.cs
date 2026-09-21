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
            int heavyAboveMaxHealth = 0, int heavyBonus = 0,
            bool scalesWithCrowd = false,
            bool lifesteal = false,
            bool strikesOnCast = false,
            int minimumDamage = 0)
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
            ScalesWithCrowd = scalesWithCrowd;
            Lifesteal = lifesteal;
            StrikesOnCast = strikesOnCast;
            MinimumDamage = minimumDamage;
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
        /// Blind Spot's "+2 instead of +1".
        /// </summary>
        public int HeavyBonus { get; }

        /// <summary>
        /// For a zone: each victim takes the payload once for every <i>other</i>
        /// victim caught with it, rather than once (ADR-0007 Amendment 1).
        /// Lethe's Eris' Exploit.
        /// </summary>
        public bool ScalesWithCrowd { get; }

        /// <summary>
        /// Whether <see cref="Status"/> means anything. A status always lasts at
        /// least one turn, so a zero <see cref="Duration"/> is "no status" —
        /// the convention every non-status effect already followed, since
        /// <see cref="Status"/> defaults to <see cref="StatusKind.Stun"/>.
        /// Read this before reading <see cref="Status"/> on anything but
        /// <see cref="EffectKind.ApplyStatus"/>.
        /// </summary>
        public bool CarriesStatus => Duration > 0;

        /// <summary>
        /// For a damage effect: the caster heals the health the hit actually
        /// removed (§2.5). Luka's Vendetta.
        /// </summary>
        public bool Lifesteal { get; }

        /// <summary>
        /// A zone that lands its first hit at cast time instead of waiting for
        /// its owner's next upkeep (COMBAT_SYSTEMS ADR-0007 Amendment 2, designer 2026-09-18).
        /// The remaining ticks still resolve on the owner's clock.
        /// </summary>
        /// <remarks>
        /// Only the instant hit carries the cast's cost, so only it meets
        /// Revú's Equilibrium (§5.17) — a deferred tick never has a cost to
        /// read.
        /// </remarks>
        public bool StrikesOnCast { get; }

        /// <summary>
        /// A floor under a computed damage figure, for effects whose amount is
        /// read off the board rather than declared (today only
        /// <see cref="EffectKind.MissingEnergyDamage"/>). Zero means no floor.
        /// </summary>
        /// <remarks>
        /// It floors the <b>primary</b> figure only, and the splash is still
        /// divided from that floored figure, so a topped-up seat's neighbours
        /// are billed the minimum's share rather than nothing — the ability
        /// stays a punishment for an empty pool and merely stops being a blank.
        /// Applied before mitigation, so a shield or Equilibrium still reduces
        /// it: this is a floor on what the cast <i>computes</i>, never a
        /// guarantee of what lands.
        /// </remarks>
        public int MinimumDamage { get; }

        /// <summary>Whether an operator with this maximum health counts as heavy for this effect.</summary>
        public bool CountsAsHeavy(int maxHealth) =>
            HeavyAboveMaxHealth > 0 && maxHealth > HeavyAboveMaxHealth;

        /// <summary>A copy with a different radius. For balance sweeps only.</summary>
        public AbilityEffect WithRadius(int radius) =>
            new AbilityEffect(Kind, Scope, Audience, Amount, DamageType, radius,
                Status, Duration, Stacks, Magnitude, BonusIfBleeding,
                ExecuteNumerator, ExecuteDenominator, BonusInOwnZone,
                CritChance, CritMultiplier, HeavyCritMultiplier, HeavyAboveMaxHealth, HeavyBonus,
                ScalesWithCrowd, Lifesteal, StrikesOnCast);

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
                chance, multiplier, heavyMultiplier, heavyAboveMaxHealth, HeavyBonus,
                ScalesWithCrowd, Lifesteal, StrikesOnCast);
        }

        /// <summary>
        /// A copy of a damage effect whose caster heals the health the hit
        /// actually removed (§2.5). Vendetta.
        /// </summary>
        /// <remarks>
        /// <b>What the hit removed, not what it was worth.</b> Overkill heals
        /// nothing, a plate's share heals nothing, an evaded or absorbed hit
        /// heals nothing, and the heal stops at the caster's maximum. A copy
        /// method, like <see cref="WithCritical"/>, so the crit and the drain
        /// compose in either order.
        /// </remarks>
        public AbilityEffect WithLifesteal()
        {
            if (Kind != EffectKind.Damage)
                throw new InvalidOperationException("Only a damage effect can steal life.");
            if (Scope == EffectScope.Caster)
                throw new InvalidOperationException("Damage aimed at the caster cannot heal the caster.");

            return new AbilityEffect(Kind, Scope, Audience, Amount, DamageType, Radius,
                Status, Duration, Stacks, Magnitude, BonusIfBleeding,
                ExecuteNumerator, ExecuteDenominator, BonusInOwnZone,
                CritChance, CritMultiplier, HeavyCritMultiplier, HeavyAboveMaxHealth, HeavyBonus,
                ScalesWithCrowd, lifesteal: true);
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
        /// A zone whose damage grows with the crowd it catches: every victim
        /// takes <paramref name="perOtherVictim"/> for each other victim inside,
        /// at the caster's next upkeep and for <paramref name="lingerTicks"/>
        /// upkeeps after (ADR-0007 Amendment 1).
        /// </summary>
        /// <remarks>
        /// <b>Quadratic where the other two are not.</b> A beacon divides a
        /// fixed payload (best against one), a zone bills each victim in full
        /// (linear in the crowd), and this bills each victim for the rest of
        /// the crowd — N victims take N(N−1) between them per tick. With one
        /// victim it does nothing at all, and that is the design, not a bug.
        ///
        /// <b>One hit per victim, not one per neighbour.</b> The victim takes a
        /// single instance of (N−1), so an evasion charge or a plate meets it
        /// once, the same way it meets a Killzone tick.
        ///
        /// No status: <see cref="CarriesStatus"/> is false. Packed like
        /// <see cref="DeployZone"/>: Amount is the first tick's per-neighbour
        /// payload, Magnitude the lingering one, Stacks the lingering ticks.
        /// </remarks>
        public static AbilityEffect CrowdZone(
            int perOtherVictim, int lingerTicks, int radius, DamageType damageType,
            EffectAudience audience = EffectAudience.Any,
            bool strikesOnCast = false)
        {
            if (perOtherVictim < 1) throw new ArgumentOutOfRangeException(nameof(perOtherVictim));
            if (lingerTicks < 0) throw new ArgumentOutOfRangeException(nameof(lingerTicks));
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));

            return new AbilityEffect(EffectKind.DeployZone, EffectScope.PrimaryTarget, audience,
                perOtherVictim, damageType, radius, default, 0,
                lingerTicks, perOtherVictim, 0, 0, 0,
                scalesWithCrowd: true, strikesOnCast: strikesOnCast);
        }

        /// <summary>
        /// Removes energy from the primary target's seat (§3.3). Destroyed,
        /// not transferred. Revú's Leech Round.
        /// </summary>
        public static AbilityEffect DrainEnergy(int amount, EffectAudience audience = EffectAudience.EnemyOnly)
        {
            if (amount < 1) throw new ArgumentOutOfRangeException(nameof(amount));

            return new AbilityEffect(EffectKind.DrainEnergy, EffectScope.PrimaryTarget, audience,
                amount, default, 0, default, 0, 0, 0, 0, 0, 0);
        }

        /// <summary>
        /// One damage to the primary target for every <paramref name="energyPerDamage"/>
        /// its seat is missing from the cap, and that figure divided by
        /// <paramref name="splashDivisor"/> to enemies within
        /// <paramref name="splashRadius"/> of it (§3.3). Revú's Sadist.
        /// </summary>
        /// <remarks>
        /// Packed into the shared fields: Amount is the energy per damage
        /// point, Radius the splash radius, Stacks the splash divisor.
        /// </remarks>
        public static AbilityEffect MissingEnergyDamage(
            int energyPerDamage, int splashRadius, int splashDivisor, DamageType damageType,
            EffectAudience audience = EffectAudience.EnemyOnly, int minimumDamage = 0)
        {
            if (energyPerDamage < 1) throw new ArgumentOutOfRangeException(nameof(energyPerDamage));
            if (splashRadius < 0) throw new ArgumentOutOfRangeException(nameof(splashRadius));
            if (splashDivisor < 1) throw new ArgumentOutOfRangeException(nameof(splashDivisor));
            if (minimumDamage < 0) throw new ArgumentOutOfRangeException(nameof(minimumDamage));

            return new AbilityEffect(EffectKind.MissingEnergyDamage, EffectScope.PrimaryTarget, audience,
                energyPerDamage, damageType, splashRadius, default, 0, splashDivisor, 0, 0, 0, 0,
                minimumDamage: minimumDamage);
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
        /// Projects a self-anchored field onto the caster: at each of her
        /// owner-upkeeps for <paramref name="durationTurns"/> of her turns,
        /// every enemy within <paramref name="radius"/> of her current cell
        /// takes <paramref name="tickDamage"/> (ADR-0007 Amendment 2). Mimi's Cryo Field.
        /// </summary>
        /// <remarks>
        /// <b>Scoped to the caster, and the recipient is real.</b> Unlike
        /// <see cref="PaintCell"/> and <see cref="DeployZone"/> this effect has
        /// someone in hand at cast time — the operator the field is projected
        /// onto — so it runs through the ordinary recipient path rather than
        /// the no-recipient routing those two need.
        ///
        /// The field follows her: each tick is measured from where she stands
        /// at that upkeep, not from where she cast it. The
        /// <see cref="StatusKind.CryoField"/> marker is the telegraph and the
        /// counterplay — a cleanse or her own neutralize ends the field by
        /// stripping it (§5.14).
        ///
        /// Reused fields, exactly as <see cref="DeployZone"/> packs its
        /// payload: <c>Amount</c> is the per-tick damage, <c>Duration</c> the
        /// marker's span. Note the registry counts a self-applied status's cast
        /// turn as its first (§5), so a field designed to tick at her next two
        /// upkeeps is duration 3.
        /// </remarks>
        public static AbilityEffect Field(
            int tickDamage, int radius, int durationTurns,
            DamageType damageType, EffectAudience audience = EffectAudience.Any)
        {
            if (tickDamage < 0) throw new ArgumentOutOfRangeException(nameof(tickDamage));
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            if (durationTurns < 1) throw new ArgumentOutOfRangeException(nameof(durationTurns));

            return new AbilityEffect(EffectKind.ProjectField, EffectScope.Caster, audience,
                tickDamage, damageType, radius, default, durationTurns, 0, 0, 0, 0, 0);
        }

        /// <summary>
        /// Marks the target for a follow-up strike at the caster's next
        /// upkeep: <paramref name="damage"/>, plus <paramref name="heavyBonus"/>
        /// if the target's maximum health is above
        /// <paramref name="heavyAboveMaxHealth"/> — but only if the caster is
        /// then within <paramref name="withinRange"/> of it (§6.5). Luka's Blind Spot.
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

        /// <summary>
        /// Sets a watch on the target: if it moves by dice before the caster's
        /// owner's next upkeep, it takes <paramref name="damage"/>, once, and
        /// the watch is spent; if it never moves, the watch lapses (§6.7).
        /// Placement never trips it (§7.4). Kurbyn's Predator's Read.
        /// </summary>
        /// <remarks>
        /// Telegraphed the way a Zero-Day charge is: an event at cast time and
        /// a <see cref="StatusKind.Watched"/> marker on the target, which a
        /// cleanse strips to cancel the watch (§5.15).
        ///
        /// <c>Amount</c> carries the damage — the whole payload. Unlike
        /// <see cref="FollowUp"/> there is no reach to settle: the condition
        /// reads only the target's own conduct, never the caster's position,
        /// which is also why a watch outlives its caster exactly as a charge
        /// does (ADR-0006).
        /// </remarks>
        /// <summary>
        /// Deals a table on the targeted cell: the first enemy dice move that
        /// crosses or ends on it stops there and takes
        /// <paramref name="stopDamage"/>, once per enemy operator, for
        /// <paramref name="lifetimeTurns"/> of the caster's turns (§7.7).
        /// Fortuna's The Table.
        /// </summary>
        /// <remarks>
        /// <b>It names a place</b>, so it carries no real scope and is routed past
        /// <c>RunEffect</c> exactly as <see cref="PaintCell"/> and
        /// <see cref="DeployZone"/> are. <c>Amount</c> is the bill,
        /// <c>Stacks</c> the lifetime.
        /// </remarks>
        public static AbilityEffect SetTable(
            int stopDamage, int lifetimeTurns, EffectAudience audience = EffectAudience.Any)
        {
            if (stopDamage < 0) throw new ArgumentOutOfRangeException(nameof(stopDamage));
            if (lifetimeTurns < 1) throw new ArgumentOutOfRangeException(nameof(lifetimeTurns));

            return new AbilityEffect(EffectKind.SetTable, EffectScope.PrimaryTarget, audience,
                stopDamage, default, 0, default, 0, lifetimeTurns, 0, 0, 0, 0);
        }

        /// <summary>
        /// Changes the dice the caster's seat is holding: re-rolls
        /// <paramref name="dice"/> unspent dice, or sets that many to
        /// <paramref name="face"/> when a face is given (§6.8). Fortuna's Deal
        /// Again and Boxcars.
        /// </summary>
        /// <remarks>
        /// <b>No recipients and no place</b> — the subject is the roll, so it is
        /// routed past <c>RunEffect</c> the way <see cref="PaintCell"/> is, and
        /// the engine applies what it declares.
        ///
        /// Packed into the shared fields: <c>Amount</c> is how many dice,
        /// <c>Stacks</c> the face they are set to, zero meaning a re-roll.
        /// Audience is <see cref="EffectAudience.Any"/> because a roll has no
        /// side; the cast mode never scopes it away.
        /// </remarks>
        public static AbilityEffect DealDice(int dice, int face = 0)
        {
            if (dice < 1) throw new ArgumentOutOfRangeException(nameof(dice));
            if (face < 0) throw new ArgumentOutOfRangeException(nameof(face));

            return new AbilityEffect(EffectKind.DealDice, EffectScope.Caster, EffectAudience.Any,
                dice, default, 0, default, 0, face, 0, 0, 0, 0);
        }

        public static AbilityEffect Watch(
            int damage, DamageType damageType,
            EffectAudience audience = EffectAudience.EnemyOnly)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));

            return new AbilityEffect(EffectKind.Watch, EffectScope.PrimaryTarget, audience,
                damage, damageType, 0, default, 0, 0, 0, 0, 0, 0);
        }
    }
}