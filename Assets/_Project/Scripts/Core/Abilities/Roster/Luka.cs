// Assets/_Project/Scripts/Core/Abilities/Roster/Luka.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Operator #9 — the duelist. Closes instantly, punishes a target that
    /// stays near him, and finishes with the roster's only random damage
    /// swing. Content, not logic — but content that arrived with three
    /// amendments (COMBAT_SYSTEMS §2.2, §2.4, §6.5, 2026-09-15).
    /// </summary>
    /// <remarks>
    /// <b>He needed three new engine capabilities.</b> The Tech damage type
    /// (§2.2), which Hermes' Ring exists to block; the follow-up strike
    /// (<see cref="EffectKind.FollowUp"/>, §6.5), the conditional sibling of
    /// Zero-Day's charge; and critical hits (§2.4), the first damage roll in
    /// the game. The teleport is Sanity's dash landing reused with no path
    /// damage, not a new mechanic.
    ///
    /// <b>"Heavy" means maximum health above <see cref="HeavyAbove"/></b>, a
    /// fact about who the target is rather than how hurt it is. Today that is
    /// the Bouncer and Sanity, both at 10. Both of his damage riders read it.
    ///
    /// <b>Numbers are the designer's, as dropped, and unmeasured.</b> The
    /// remarks on each ability record what they look like against their
    /// peers; nothing here has been swept. Adding him also shifts the draft's
    /// dice stream, so no figure taken before him compares with one after.
    /// </remarks>
    public static class Luka
    {
        /// <summary>Seven, the roster's common figure since the +1 of 2026-09-16.</summary>
        public const int MaxHealth = 7;

        /// <summary>1.0, the band's floor (ADR-0002 Amendment 4). His mobility is the teleport.</summary>
        public const double Speed = 1.0;

        /// <summary>
        /// A target whose maximum health is above this is heavy, and both of
        /// his damage riders hit it harder.
        /// </summary>
        /// <remarks>
        /// 7 since the roster-wide +1 of 2026-09-16. Left at 6 it would have
        /// made every 7-health operator heavy, which is most of the roster.
        /// </remarks>
        public const int HeavyAbove = 7;

        /// <summary>How close he must still be to Blind Spot's target at his next upkeep.</summary>
        public const int FollowUpReach = 2;

        /// <summary>
        /// Luka's ring wipes him out of every lens in the room; he reappears
        /// beside the target and strikes it. If it is still within reach when
        /// his next turn begins, he strikes it again.
        /// </summary>
        /// <remarks>
        /// <b>Named 2026-09-15</b> (designer); dropped as "L". The device is
        /// Hermes' Ring turned outward (OPERATORS.md): nobody sees him cross
        /// the floor, which is why it plays as a teleport and hits nobody on
        /// the way. He stays in the target's blind spot, which is the
        /// follow-up.
        ///
        /// <b>The teleport is Collision's landing with no rake</b> (§7.6):
        /// placement one cell past the target, or one short when that cell is
        /// taken. It collides with nothing and passes through nobody.
        ///
        /// <b>Two now, and one or two later if the target stays.</b> The
        /// follow-up is Zero-Day's pattern turned into a duel: telegraphed by
        /// the <see cref="StatusKind.Hunted"/> marker, cancelled by a cleanse
        /// (§5.13), and — unlike a charge — escapable by moving more than
        /// <see cref="FollowUpReach"/> away from him before his next turn.
        /// Since the teleport leaves him adjacent, the target has to spend its
        /// own move to get clear, which is what the rider is really buying.
        ///
        /// <b>Cost 5, one above Zero-Day, because it does more to one
        /// target</b> (designer, 2026-09-15; dropped at 4). Same cooldown; two
        /// on the target now where Zero-Day deals two a round later, up to two
        /// more if it stays, and up to four cells of free mobility. Zero-Day
        /// pays for its certainty with a cleanse and a blast that reaches
        /// others; this pays with the escape and the extra point. Normal
        /// damage, both hits — no type was specified.
        /// </remarks>
        public static AbilityDefinition BlindSpot { get; } = new AbilityDefinition(
            id: 901, name: "Blind Spot",
            description:
                "Luka's ring wipes him from every lens in the room. He reappears beside the target and strikes it, and if it is still close when his next turn begins, he strikes it again, harder if it is a heavy target.",
            energyCost: 5, cooldownTurns: 3, range: 3,
            effects: new[]
            {
                AbilityEffect.Dash(pathDamage: 0, audience: EffectAudience.EnemyOnly),
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 2, DamageType.Normal,
                    EffectAudience.EnemyOnly),
                AbilityEffect.FollowUp(
                    damage: 1, heavyBonus: 1, heavyAboveMaxHealth: HeavyAbove,
                    withinRange: FollowUpReach, damageType: DamageType.Normal)
            });

        /// <summary>
        /// The ring turned inward: it jams anything guided or remote-operated
        /// that is aimed at Luka, for a while.
        /// </summary>
        /// <remarks>
        /// <b>Dropped as a passive; built as a self-cast.</b> A passive with a
        /// cost, a duration and a cooldown is an ability the player triggers,
        /// so it is one — targeting none, like Ablative Plating. Duration 3 on a
        /// self-buff means this turn and his next two, so it covers the
        /// opponents' turns across two full rounds (§5).
        ///
        /// <b>A full block, not a pool.</b> Tech is otherwise Normal (§2.2);
        /// the ward stops it before evasion or a shield is consulted, so it
        /// never spends either (§5.12). Normal and Atomic pass straight
        /// through.
        ///
        /// <b>Three Tech sources, one per operator</b> (2026-09-15): Mimi's
        /// Cryo-Pulse, Sanity's Zero-Day and Kian's Drone Strike, the
        /// roster's guided and remote-operated devices (§2.2). Sanity and Kian
        /// keep Normal damage elsewhere in their kits, so against them the
        /// ring is a counter, not an immunity. Mimi's only built damage is
        /// Cryo-Pulse, so a warded Luka is immune to her until Cryo Field
        /// lands. Cost 3, cooldown 4 is the Ablative Plating price.
        /// Cleansable like any applied status.
        /// </remarks>
        public static AbilityDefinition HermesRing { get; } = new AbilityDefinition(
            id: 902, name: "Hermes' Ring",
            description:
                "Luka turns his ring inward, jamming all tech damage aimed at him for a while.",
            energyCost: 3, cooldownTurns: 4, range: 0,
            targeting: AbilityTargeting.None,
            effects: new[]
            {
                AbilityEffect.Status_(
                    EffectScope.Caster, StatusKind.TechWard, duration: 3,
                    EffectAudience.Any)
            });

        /// <summary>
        /// Enraged and utterly focused, Luka hits the target with a relentless
        /// flurry of atomic blows, any of which can land as a crushing
        /// critical.
        /// </summary>
        /// <remarks>
        /// <b>Three blows of 1 Atomic, each rolling a 10% critical</b> (§2.4):
        /// double damage, triple against a heavy target. Three independent
        /// rolls, so a blow can be 1, 2 or 3.
        ///
        /// <b>Cost 6 (designer, 2026-09-15; dropped at 9).</b> Expected 3.3
        /// damage, 3.6 against a heavy target; a crit lands on at least one
        /// blow about 27% of the time, and the ceiling is 6, or 9 against a
        /// heavy target. At 9 it lost to Miracle Pull at the same price. At 6
        /// it sits with Velvet Rope, the other single-target Atomic cast:
        /// 3 certain damage and a pull against 3.3 expected and a swing.
        ///
        /// <b>A blow that finds its target already down is not thrown.</b> The
        /// resolver stops striking a recipient that an earlier effect of the
        /// same cast brought to zero, so a kill on the first blow is one kill
        /// and one bounty, not three.
        ///
        /// <b>Atomic is no longer concentrated in two operators</b> (§2.2):
        /// he is the third source, and the only one whose Atomic is a single
        /// target.
        /// </remarks>
        public static AbilityDefinition Vendetta { get; } = new AbilityDefinition(
            id: 903, name: "Vendetta",
            description:
                "Luka becomes enraged with unrelenting focus, assaulting the target with a flurry of atomic blows. Any blow can land a critical hit, and heavy targets suffer worse.",
            energyCost: 6, cooldownTurns: 3, range: 3,
            effects: new[] { VendettaBlow(), VendettaBlow(), VendettaBlow() });

        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { BlindSpot, HermesRing, Vendetta };

        /// <summary>His uniform shape, for drafting. No aura, no passive.</summary>
        public static OperatorDefinition Definition { get; } = new OperatorDefinition(
            name: "Luka",
            maxHealth: MaxHealth,
            baseSpeed: Speed,
            abilities: All);

        /// <summary>
        /// One of Vendetta's blows. A method rather than a shared field so the
        /// static initialiser order of this class can never hand an array a
        /// default (empty) effect.
        /// </summary>
        private static AbilityEffect VendettaBlow() =>
            AbilityEffect.Damage(EffectScope.PrimaryTarget, 1, DamageType.Atomic, EffectAudience.EnemyOnly)
                .WithCritical(chance: 0.1, multiplier: 2, heavyMultiplier: 3, heavyAboveMaxHealth: HeavyAbove);
    }
}
