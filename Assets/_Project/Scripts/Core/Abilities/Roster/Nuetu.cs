// Assets/_Project/Scripts/Core/Abilities/Roster/Nuetu.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Operator #7 — a sustain bruiser. The first operator who heals himself by
    /// hurting somebody, and the second with a shield. Content, not logic.
    /// </summary>
    /// <remarks>
    /// <b>He needed no new engine capability, which is the point.</b> Mimi forced
    /// one new effect kind, Javi another, Kian five. Nuetu is built entirely from
    /// what already exists — a damage effect, a heal aimed at the caster, and a
    /// shield status. That is what <c>AbilityResolver</c>'s contract predicted
    /// operators would look like once the vocabulary was wide enough, and it is
    /// the first time it has come true.
    ///
    /// <b>The self-heal is the All-In Mauling pattern.</b> It is declared
    /// <see cref="EffectAudience.EnemyOnly"/> despite landing on an ally — the
    /// caster. Audience picks which <i>cast mode</i> an effect belongs to, not
    /// who receives it, so aiming Bio-Link Rage at a friend refuses for
    /// <c>WrongSide</c> and costs nothing rather than healing him for free.
    ///
    /// <b>Known limitation: the heal is not lifesteal.</b> The two effects are
    /// independent, so the heal fires even when the damage is evaded or absorbed.
    /// Healing in proportion to damage <i>dealt</i> would need a new mechanic —
    /// an effect that reads another effect's result — and at 1 point the
    /// distinction is not worth one.
    ///
    /// <b>Complete, and he brought one new system with him.</b> Killzone needed
    /// lingering cell-anchored effects — ADR-0006's registry fires once and
    /// deletes — and a rider that reads the board from inside another ability.
    /// Both are ADR-0007. Everything else in his kit was already expressible.
    ///
    /// <b>He is a seventh archetype</b>, on a taxonomy that still names four.
    /// </remarks>
    public static class Nuetu
    {
        /// <summary>
        /// Six, walked 9 → 7 → 6. Nine tied Bouncer, and a second operator at the
        /// tank's health with a shield on top is not a bruiser but a better tank.
        /// </summary>
        /// <remarks>
        /// <b>Health stopped being what prices him.</b> At 6 he is level with
        /// Syla, Javi, Kurbyn and Kian, and only Mimi is lower — so nothing about
        /// his health distinguishes him at all. With Ablative Plating up he is
        /// effectively 8 against Normal damage, which is above that group rather
        /// than below it. What actually costs him is reach: range 2 on every
        /// ability, no repositioning and no escape, at the band's floor.
        ///
        /// <b>Speed was the original price and it was withdrawn.</b> The sketch
        /// put him at 0.75, outside the 1.0–1.5 band (ADR-0002 Amendment 4) and
        /// below the floor that keeps a match finishing — the match ends when the
        /// <i>last</i> operator gets home. It would also have floored a single die
        /// of 1 to zero cells, leaving him unable to take half a split roll.
        ///
        /// <b>This and Killzone's detonation both moved in the same pass, on an
        /// operator with no measurements at all.</b> <c>Bouncer.cs</c> carries the
        /// same warning from the 2026-09-12 cut: axes moved together are not
        /// separable afterwards. If he reads weak, put the detonation back first —
        /// it is the one whose effect is easiest to see at the table.
        /// </remarks>
        public const int MaxHealth = 6;

        /// <summary>
        /// The band's floor, shared with Bouncer, Mimi and Kian. He closes slowly
        /// and then does not want to leave.
        /// </summary>
        public const double Speed = 1.0;

        /// <summary>
        /// Nanofilament blades unpick the target's tissue faster than it can
        /// register the wound, and what comes off is fed straight back into him.
        /// </summary>
        /// <remarks>
        /// <b>Damage 3, walked back from a sketched 6.</b> At 6 for 3 energy it
        /// killed Mimi, Syla, Javi, Kurbyn and Kian outright from full health for
        /// one turn's income — five of seven operators dead to one button, at
        /// four times Velvet Rope's damage per energy. It also described a
        /// different operator: "deconstruct and heal" is sustain, and a one-shot
        /// with a heal stapled on is an execute wearing that sentence.
        ///
        /// <b>Cooldown 2 is what makes it an engine.</b> Against the 3.5-per-turn
        /// drip (§3.1) a 3-cost ability is limited by nothing else, so the
        /// cooldown is the whole regulator — and at 2 he can run it most turns,
        /// which is the point. Three damage and one health back, repeatedly,
        /// beats six damage once.
        ///
        /// <b>Normal, not Atomic.</b> Atomic is deliberately concentrated in
        /// Bouncer and Kurbyn (§2.2), and a repeatable Atomic hit would make
        /// Javi's plate and Kurbyn's charge worthless against the operator who
        /// attacks most often.
        ///
        /// <b>Range 2 is the whole cost of the kit.</b> He has no reach, no
        /// repositioning and no escape, so every point of this has to be bought
        /// by standing next to somebody at 1.0 speed.
        ///
        /// <b>The self-heal doubles while one of his Killzones is live</b>
        /// (ADR-0007) — the first number in the game that another ability
        /// changes. It reads whether a zone exists anywhere rather than whether
        /// he is standing in one; the positional version is a one-word change in
        /// <c>AbilityResolver</c> and is the stronger design, because it would
        /// give him a reason to walk into his own grenade.
        /// </remarks>
        public static AbilityDefinition BioLinkRage { get; } = new AbilityDefinition(
            id: 701, name: "Bio-Link Rage",
            description:
                "Unpicks an enemy's tissue faster than they can feel it, and feeds what comes off straight back into you.",
            energyCost: 3, cooldownTurns: 2, range: 2,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 3, DamageType.Normal,
                    EffectAudience.EnemyOnly),
                AbilityEffect.Heal(EffectScope.Caster, 1, EffectAudience.EnemyOnly,
                    bonusInOwnZone: 1)
            });

        /// <summary>
        /// A cloud of microscopic drones orbits his armour, meeting incoming
        /// impacts and coming apart instead of him.
        /// </summary>
        /// <remarks>
        /// <b>Cut from a 3-point pool over 4 turns, which was strictly better
        /// than Trauma Plate on cost, size and duration.</b> Javi's file opens
        /// with "he is the reason the shield layer matters"; a second shield
        /// three operators later that beats his on every axis takes that away.
        /// At pool 2 it protects exactly as much as a plate does, and the
        /// difference between the two operators is what they can do with it.
        ///
        /// <b>Cooldown 4 against duration 2 — 40% uptime, where Javi gets
        /// 50%.</b> That ordering is deliberate: the support should hold the
        /// better shield, and the bruiser should have to choose the turn.
        ///
        /// <b>Cost 3 rather than Trauma Plate's 4, because self-only is
        /// worse.</b> Javi can put a plate on whoever is about to be hit; Nuetu
        /// can only ever protect the operator already standing in the fight.
        /// He pays less for the smaller option.
        ///
        /// <b>Neural Purge strips it.</b> A cleanse is indiscriminate (§5.8), so
        /// Javi can take this off him — which is the rock-paper-scissors the
        /// cleanse exists for, and the reason a second shield source is
        /// survivable rather than redundant.
        /// </remarks>
        public static AbilityDefinition AblativePlating { get; } = new AbilityDefinition(
            id: 702, name: "Ablative Plating",
            description:
                "Wraps you in a drone swarm that meets incoming fire and comes apart instead of you.",
            energyCost: 3, cooldownTurns: 4, range: 0,
            targeting: AbilityTargeting.None,
            effects: new[]
            {
                AbilityEffect.Status_(
                    EffectScope.Caster, StatusKind.Shield, duration: 2,
                    EffectAudience.Any, magnitude: 2)
            });

        /// <summary>
        /// A grenade blankets a stretch of track. It goes off a round later,
        /// crushing whatever is standing in it, and the ground stays hostile
        /// afterwards.
        /// </summary>
        /// <remarks>
        /// <b>It detonates at his next upkeep, then bills twice more</b> — three
        /// resolutions on three of his own turns (ADR-0007). The round of warning
        /// is the counterplay, the same bargain Drone Strike makes: he is betting
        /// on where people will be, and they can see the bet.
        ///
        /// <b>Only the detonation stuns, and that is not a tuning choice.</b>
        /// Stun blocks movement (§5.1), so a zone that stunned on every tick
        /// would hold a victim inside itself until it expired — nine energy to
        /// remove an operator from the game for three rounds, with no answer on
        /// the roster. Neural Purge cleanses the stun for 6, but a re-stun a
        /// round later beats a cleanse every time. The grenade crushes once; what
        /// lingers only grinds.
        ///
        /// <b>1 then 1 then 1, walked back from 2 every tick and then again from
        /// a detonation of 2.</b> Six guaranteed damage would have killed Mimi,
        /// Syla, Javi, Kurbyn and Kian outright on top of two turns taken, which
        /// is a deletion rather than an ultimate.
        ///
        /// <b>At three total, the stun is carrying the ability and the damage is
        /// a rider.</b> Nine energy buys Miracle Pull, which either executes
        /// outright or deals 3 Atomic plus 2 splash, immediately, with no round of
        /// warning. This deals 3 Normal over three rounds, telegraphed, to
        /// whoever stays — so it is priced almost entirely on taking two turns
        /// away from everyone caught. Whether that is worth 9 is exactly what the
        /// harness has never been asked.
        ///
        /// <b>The stun and the damage are not independent dials.</b> The stun is
        /// the only reason a victim is still standing there for the second and
        /// third ticks; at stun 1 they walk out before the third, which then
        /// usually catches nobody and the third tick becomes dead weight. Cutting
        /// the stun cuts the damage twice. Reasoned, unmeasured.
        ///
        /// <b>Damage is per target, not divided.</b> The deliberate opposite of
        /// Drone Strike, which splits: a beam of fixed energy is worst against a
        /// crowd, and ground that grinds is best against one. Two cell abilities
        /// that behaved alike would not have been worth two.
        ///
        /// <b>Normal, so it can be answered.</b> A plate absorbs it, an evasion
        /// charge negates one tick, and a squad that scatters eats less of it.
        ///
        /// <b>Cost 9 and cooldown 6 — once every seven turns.</b> The longest
        /// cooldown in the game by a wide margin, which is what lets the payload
        /// be this large. Range 2 means he has to be standing in the fight to
        /// place it, and at 1.0 speed with no escape, that is the real price.
        /// </remarks>
        public static AbilityDefinition Killzone { get; } = new AbilityDefinition(
            id: 703, name: "Killzone",
            description:
                "Blankets a stretch of track. It goes off a round later and crushes whatever is standing there, and the ground stays hostile afterwards.",
            energyCost: 9, cooldownTurns: 6, range: 2,
            targeting: AbilityTargeting.Cell,
            effects: new[]
            {
                AbilityEffect.DeployZone(
                    detonationDamage: 1, lingerDamage: 1, lingerTicks: 2, radius: 1,
                    damageType: DamageType.Normal,
                    detonationStatus: StatusKind.Stun, statusDuration: 2),
                AbilityEffect.Heal(EffectScope.Caster, 1, EffectAudience.Any)
            });

        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { BioLinkRage, AblativePlating, Killzone };

        /// <summary>
        /// His uniform shape, for drafting. No aura, no passive, nothing beyond
        /// range 2 — the shortest reach on the roster.
        /// </summary>
        public static OperatorDefinition Definition { get; } = new OperatorDefinition(
            name: "Nuetu",
            maxHealth: MaxHealth,
            baseSpeed: Speed,
            abilities: All);
    }
}