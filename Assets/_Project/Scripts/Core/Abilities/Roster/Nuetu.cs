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
        /// Seven since the roster-wide +1 of 2026-09-16 (COMBAT_SYSTEMS §1.1),
        /// still level with the common figure.
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
        ///
        /// Raised by 1 again on 2026-09-25 (designer, to shorten matches; COMBAT_SYSTEMS §1.1): now 8.
        ///
        /// <b>Nine since 2026-10-01, and crossing Luka's heavy line is the whole
        /// reason for it (designer).</b> <see cref="Luka.HeavyAbove"/> is 8, so
        /// at 9 he joins the Bouncer, Sanity and Revú as a <i>heavy</i> target:
        /// Blind Spot's strike and follow-up each pay their +1 into him, and a
        /// Vendetta blow that crits against him triples instead of doubling
        /// (§2.4). <b>That is the point, not a side effect</b> — the brief was to
        /// make him worse against Luka specifically, and the heavy line is the
        /// only switch in the game that does it.
        ///
        /// <b>He is a tank by intent</b> (designer, stated 2026-10-01), whatever
        /// the archetype label on §10.7 says. So the heavy list still means what
        /// it was written to mean; he is simply the fourth name on it rather than
        /// an operator who wandered onto it. The trade is deliberate and
        /// two-sided: a point of health against every source of damage in the
        /// game, paid for with a worse matchup against one duelist.
        /// </remarks>
        public const int MaxHealth = 9;

        /// <summary>
        /// The band's floor, shared with Bouncer, Mimi and Kian. He closes slowly
        /// and then does not want to leave.
        /// </summary>
        public const double Speed = 1.0;

        /// <summary>
        /// Turns of Burdened (§5.16) that Bio-Link Rage leaves on its target
        /// (2026-09-21, designer).
        /// </summary>
        /// <remarks>
        /// <b>He is the answer to mobility, and this is the whole of it.</b>
        /// Burdened is haste's mirror and cancels against it cell for cell, so a
        /// hasted operator that he has hold of is moving at its base speed and
        /// nothing more — Kurbyn's entire passive, Syla's payout and anyone
        /// standing in Lethe's Catalyst. It is never a stun: the floor is one
        /// cell (§5.16), so a Burdened operator always moves.
        ///
        /// <b>Two turns against a two-turn cooldown</b>, so keeping one target
        /// slowed down is a choice he re-makes every turn at 3 energy rather
        /// than a state he sets once. Spreading it over two targets means
        /// neither is held.
        ///
        /// <b>Why here and not on Killzone.</b> The zone already carries the
        /// stun, and one detonation every four turns is a moment, not pressure.
        /// The counter to something that arrives whenever it likes has to be
        /// repeatable, and Bio-Link Rage is the repeatable half of his kit.
        ///
        /// <b>It is Sanity's identity, shared</b> (§5.16, §10.8), exactly as
        /// Slow is shared. His is permanent and his own; Nuetu's is a timed
        /// status he puts on somebody else.
        /// </remarks>
        public const int BioLinkBurdenTurns = 2;

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
                "Tears into an enemy close by and leaves them moving shorter for a while, and you heal a little from it. You heal more while your Killzone is still live.",
            energyCost: 3, cooldownTurns: 2, range: 2,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 3, DamageType.Normal,
                    EffectAudience.EnemyOnly),
                AbilityEffect.Status_(EffectScope.PrimaryTarget, StatusKind.Burdened,
                    duration: BioLinkBurdenTurns, audience: EffectAudience.EnemyOnly),
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
        /// <b>Cooldown 3 against duration 2 — 50% uptime (designer,
        /// 2026-10-01; was cooldown 4, 40%).</b> The old note here said the
        /// 40/50 split was deliberate, because the support should hold the
        /// better shield and the bruiser should have to choose the turn. At
        /// cooldown 3 the two are level: Trauma Plate is duration 2 on cooldown
        /// 3 as well, so Nuetu now keeps a plate up half the time exactly as
        /// Javi does, and the thing that still separates them is whose plate it
        /// can be — Javi picks, Nuetu is stuck with himself. That is the whole
        /// remaining distinction, and it is thinner than it was.
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
                "Wraps you in a shield that soaks up damage until it is used up or wears off. Atomic hits go straight through it.",
            energyCost: 3, cooldownTurns: 3, range: 0,
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
        /// would hold a victim inside itself until it expired — energy spent to
        /// remove an operator from the game for three rounds, with no answer on
        /// the roster. Neural Purge cleanses the stun, but a re-stun a round
        /// later beats a cleanse every time. The grenade crushes once; what
        /// lingers only grinds.
        ///
        /// <b>2 then 1 then 1 (designer, 2026-10-01, in two steps: 1/1/1 → 2/2/2
        /// in the morning's pass, then the linger back to 1 the same day).</b>
        /// Six guaranteed damage is what the original note here rejected, on the
        /// grounds that it would kill Mimi, Syla, Javi, Kurbyn and Kian outright
        /// on top of two turns taken — a deletion rather than an ultimate. The
        /// health rises of 2026-09-16 and 2026-09-25 had made that objection
        /// expire on paper, but the measured version brought a different problem:
        /// at 2 a tick the ability was cast three times a match and put a fifth
        /// of September's pacing work back on the board (§12).
        ///
        /// <b>So the shape is back to what it was: a crush, then a grind.</b> The
        /// detonation keeps the doubled hit — it is the half that lands on
        /// everyone and the half the stun guarantees — and the ticks go back to
        /// being an incentive to leave rather than a reason to be dead. Four
        /// guaranteed damage against a common 8, with the third tick usually
        /// catching nobody, so the realistic payload is 3.
        ///
        /// <b>The stun cut to 1 spends the third tick, and that was predicted
        /// here before it happened.</b> The stun is the only reason a victim is
        /// standing in the zone for the later ticks; at duration 1 they walk out
        /// before the third, which then usually catches nobody. Halving the stun
        /// and halving the linger push the same way, which is the thing to
        /// remember if this now reads as weak: the two were never independent,
        /// and the dial that undoes both at once is the stun.
        ///
        /// <b>Cost 6, cooldown 3, range 3 (designer, 2026-10-01; was 9, 4 and
        /// 2).</b> The ultimate is no longer the roster's most expensive cast
        /// nor its longest cooldown — it is Sadist's price on half Sadist's
        /// timer, and range 3 means he no longer has to stand in the fight to
        /// place it, which the old note called "the real price" of the ability.
        /// Three of the four things that limited it moved at once, so if this
        /// overshoots, the cooldown is the dial to put back first: it is the one
        /// that governs how often the board has a Killzone on it at all.
        ///
        /// <b>Damage is per target, not divided.</b> The deliberate opposite of
        /// Drone Strike, which splits: a beam of fixed energy is worst against a
        /// crowd, and ground that grinds is best against one. Two cell abilities
        /// that behaved alike would not have been worth two.
        ///
        /// <b>Normal, so it can be answered.</b> A plate absorbs it, an evasion
        /// charge negates one tick, and a squad that scatters eats less of it.
        ///
        /// </remarks>
        public static AbilityDefinition Killzone { get; } = new AbilityDefinition(
            id: 703, name: "Killzone",
            description:
                "Marks an area near you and heals you a little. When your next turn begins it goes off: every enemy inside is hurt and stunned, and whoever is still inside keeps getting hurt for a while. Enemies have one turn to leave before it goes off.",
            // Cooldown 6 → 4 on 2026-09-21, then 4 → 3 with the cost 9 → 6 and
            // the range 2 → 3 on 2026-10-01 (designer, both). The first cut was
            // because the trap was almost never on the board at the moment a
            // fast operator chose to close, which is the only moment it answers.
            energyCost: 6, cooldownTurns: 3, range: 3,
            targeting: AbilityTargeting.Cell,
            effects: new[]
            {
                AbilityEffect.DeployZone(
                    detonationDamage: 2, lingerDamage: 1, lingerTicks: 2, radius: 2,
                    damageType: DamageType.Normal,
                    detonationStatus: StatusKind.Stun, statusDuration: 1),
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