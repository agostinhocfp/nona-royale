// Assets/_Project/Scripts/Core/Abilities/Roster/Lethe.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Operator #10. Stats and kit, transcribed from <c>COMBAT_SYSTEMS.md</c>
    /// §10.10 (added 2026-09-17).
    /// </summary>
    /// <remarks>
    /// <b>Bouncer's shape, pointed the other way.</b> Two actives and an aura
    /// in the middle slot. His aura makes enemies slow near him; hers makes
    /// allies quick near her. Both are local: they hold only while the
    /// operators stand close, and they are gone the moment anyone steps out.
    ///
    /// <b>The kit is about where the squad stands.</b> Catalyst pays allies to
    /// stay close to her, Nano Cell locks one of them in place, and Eris'
    /// Exploit punishes the enemy for doing what Catalyst asks her own side to
    /// do. Two crowd tools on one operator, one on each side of the table.
    ///
    /// <b>Every number here is the designer's and unmeasured.</b> Adding a
    /// tenth operator shifts the draft's dice stream, so nothing measured
    /// before her compares with a figure taken after.
    /// </remarks>
    public static class Lethe
    {
        /// <summary>
        /// Seven: the roster's common health since the +1 of 2026-09-16, level
        /// with Syla, Javi, Kurbyn, Kian, Nuetu and Luka.
        /// </summary>
        public const int MaxHealth = 7;

        /// <summary>
        /// Base 1.0. Her mobility is her passive haste, not speed.
        /// </summary>
        /// <remarks>
        /// <b>Not Kurbyn's construction.</b> His passive carries a +0.5 speed
        /// delta as its magnitude. Haste stopped being speed on 2026-09-16, so
        /// a Hastened passive carries no magnitude at all — the registry skips
        /// Hastened in the speed channel. What she gets instead is a permanent
        /// +1 cell on a roll of 6 or less, +2 above, once per roll, capped at 3
        /// per turn (§5.9). About 1.6 cells a roll: less than 1.5× on a big
        /// roll, more on a small one, and always countable.
        ///
        /// <b>A passive, so a cleanse cannot take it</b> and neutralize does not
        /// clear it (§1.2).
        /// </remarks>
        public const double BaseSpeed = 1.0;

        /// <summary>Allies within this many steps of her count as Hastened.</summary>
        /// <remarks>
        /// <b>2, and deliberately local</b> (designer, 2026-09-17). Not a copy of
        /// Tagged From Above's payout: that one follows its squad anywhere for
        /// two turns; this one ends at the edge of five cells. An ally that
        /// wants the cell has to walk beside her, which is exactly where Eris'
        /// Exploit — and every other area ability — wants a squad to be.
        /// </remarks>
        public const int CatalystRadius = 2;

        /// <summary>
        /// Her passive aura. Not an ability — never used, no cost, no cooldown,
        /// no duration (§10.1, §10.3). An ally within
        /// <see cref="CatalystRadius"/> when it starts a move counts as
        /// Hastened for that move.
        /// </summary>
        /// <remarks>
        /// <b>Haste, not speed.</b> A +0.5 speed aura would put Syla and Javi at
        /// 2.0×, where a mean roll crosses a quarter of the loop, because the
        /// speed channel has no ceiling (§6.3). Haste is flat cells under a
        /// per-turn cap, so there is nothing to overflow.
        ///
        /// <b>It does not stack with other haste.</b> An ally already hastened
        /// by Tagged From Above's payout gains nothing more from standing near
        /// her; the bonus is a yes or no, paid once per roll (§5.9).
        /// </remarks>
        public static AuraDefinition Catalyst { get; } =
            new AuraDefinition("Catalyst", CatalystRadius, speedModifier: 0.0,
                side: AuraSide.Allies, grantsHaste: true,
                description: "Stand close to her and the night runs faster. Allies near her move as if they were already late.");

        /// <summary>
        /// Far larger than a round of enemy turns can deal, so the pool never
        /// runs dry inside its duration. That is what makes the bubble total
        /// immunity rather than a large plate.
        /// </summary>
        public const int NanoCellPool = 99;

        /// <summary>
        /// Two of the ally's own turns: the one it is cast in, and the next.
        /// </summary>
        /// <remarks>
        /// Cast on an ally during her own turn, a status takes hold at once
        /// (§5). Duration 1 would expire at the end of this turn, before any
        /// enemy acted. Duration 2 covers exactly one round of enemy turns,
        /// and takes the ally's next turn in exchange.
        /// </remarks>
        public const int NanoCellDurationTurns = 2;

        /// <summary>
        /// A bubble on an ally: nothing that can be mitigated gets through, and
        /// nothing inside can move or act.
        /// </summary>
        /// <remarks>
        /// <b>No new mechanics.</b> A shield with a pool nothing can empty, and
        /// a stun. Normal and Tech are absorbed, a collision included (§5.6);
        /// Atomic ignores every mitigation layer (§2.2), so bleed, marks,
        /// Velvet Rope, Miracle Pull and Vendetta all go straight through.
        ///
        /// <b>The stun is the price, and it is total.</b> The bubbled ally
        /// cannot move and cannot spend energy (§5.1). The original "cannot be
        /// stunned inside" is therefore redundant rather than contradicted: it
        /// already is. Status immunity was proposed and dropped — it would have
        /// needed an immunity system with a carve-out on day one.
        ///
        /// <b>Cast order matters.</b> The stun takes hold at once, so an ally
        /// that has not moved yet this turn loses that move as well as the
        /// next. Move first, then bubble.
        ///
        /// <b>It blocks the road.</b> A collision's damage is Normal, so the
        /// bubble eats it, the target survives, and the mover bounces (§7.2).
        ///
        /// <b>Neural Purge strips both, and the ability needs that.</b> A cleanse
        /// is indiscriminate (§5.8), so a Javi on her side can pop the bubble to
        /// free the ally early. An enemy cannot: a cleanse only reaches allies.
        /// Do not "fix" this: a blanket immunity with no answer at all would be
        /// oppressive.
        ///
        /// <b>Cost 4, cooldown 4</b> (designer, 2026-09-17). Trauma Plate's
        /// price: at 3 it blanked a 9-energy Killzone or Drone Strike on one
        /// ally for a third of the cost. The stun is the rest of the price.
        /// Range 4, as Trauma Plate. It may be cast on herself; her aura
        /// survives the stun (§10.1), so a bubbled Lethe is a stationary
        /// Catalyst that Normal and Tech damage cannot touch.
        /// </remarks>
        public static AbilityDefinition NanoCell { get; } = new AbilityDefinition(
            id: 1001, name: "Nano Cell",
            description:
                "Seals an ally inside a lattice of nanites. Ordinary harm slides off it, but nothing inside can move or act until it dissolves.",
            energyCost: 4, cooldownTurns: 4, range: 4,
            // Opts in to self-cast (§10, 2026-09-17): the self-bubble pays the
            // stun as its price, and her aura survives it (§10.1).
            allowsSelfTarget: true,
            effects: new[]
            {
                AbilityEffect.Heal(EffectScope.PrimaryTarget, 2, EffectAudience.AllyOnly),
                AbilityEffect.Status_(
                    EffectScope.PrimaryTarget, StatusKind.Shield, duration: NanoCellDurationTurns,
                    EffectAudience.AllyOnly, magnitude: NanoCellPool),
                AbilityEffect.Status_(
                    EffectScope.PrimaryTarget, StatusKind.Stun, duration: NanoCellDurationTurns,
                    EffectAudience.AllyOnly)
            });

        /// <summary>Each victim's damage per other victim, per tick.</summary>
        public const int ErisExploitPerOtherVictim = 1;

        /// <summary>Two cells each way: five cells.</summary>
        /// <remarks>
        /// <b>2, not the spec's 3</b> (designer, 2026-09-17). Five cells bound the
        /// crowd by geometry rather than by an arbitrary clamp. At radius 3
        /// (seven cells) four victims were routine on stacked safe cells, and
        /// four victims take 6 each over the two ticks — 24 damage for 6
        /// energy, against Ace Shards' 4 each at the same price.
        /// </remarks>
        public const int ErisExploitRadius = 2;

        /// <summary>
        /// Ticks after the instant one. One: two hits in all, the first at cast
        /// (ADR-0007 Amendment 2) and the second at her next upkeep.
        /// </summary>
        public const int ErisExploitLingerTicks = 1;

        /// <summary>
        /// A field on a cell that turns every enemy inside it against the
        /// others: each takes 1 for every other enemy caught with it.
        /// </summary>
        /// <remarks>
        /// <b>The anti-clustering ability, and nothing else on the board is.</b>
        /// N victims take N−1 each per tick, N(N−1) between them, and twice that
        /// over the two ticks:
        ///
        /// 1 caught → 0 · 2 → 1 each · 3 → 2 each · 4 → 3 each (per tick).
        ///
        /// That makes the roster's three cell abilities three different shapes:
        /// Drone Strike divides a fixed payload (best against one), Killzone
        /// bills each victim in full (linear), and this bills each victim for
        /// the rest of the crowd (quadratic).
        ///
        /// <b>It does nothing with one enemy inside.</b> That is its failure
        /// condition, stated here so nobody discovers it at the table: aimed at
        /// a lone operator it is 6 energy for nothing.
        ///
        /// <b>It strikes on the cast, then once more at Lethe's next upkeep</b>
        /// (designer, 2026-09-18; ADR-0007 Amendment 2). It was a plain ADR-0007 zone —
        /// nothing at cast, a tick at her next upkeep and one after that — and
        /// the enemy could simply scatter, which made a 6-energy cast worth
        /// nothing often enough that the bots cast it about once a match and
        /// she sat at the bottom of the sweep. The first hit is now
        /// undodgeable; the second still is, so a crowd that breaks up has
        /// still been controlled. Total damage against a crowd that stays put
        /// is unchanged.
        ///
        /// <b>One hit of N−1 per victim</b>, Normal, credited to Lethe: an
        /// evasion charge or a plate meets it once, and a kill pays her side
        /// the bounty, as Killzone's pays Nuetu's.
        ///
        /// <b>Cost 6, cooldown 4, range 3</b> (designer, 2026-09-17). Range 3
        /// matches Syla's area reach; Killzone's is 2 and Drone Strike's is
        /// unlimited.
        ///
        /// <b>Naming.</b> Like Hermes' Ring, and like her own name, it borrows
        /// Greek myth: Eris, who started a war by throwing one apple into a
        /// crowd. Kept as the designer wrote it.
        /// </remarks>
        public static AbilityDefinition ErisExploit { get; } = new AbilityDefinition(
            id: 1002, name: "Eris' Exploit",
            description:
                "Sows discord in a patch of track: every enemy inside turns on every other one at once, and again next round on whoever stayed.",
            energyCost: 4, cooldownTurns: 3, range: 3,
            targeting: AbilityTargeting.Cell,
            effects: new[]
            {
                AbilityEffect.CrowdZone(
                    perOtherVictim: ErisExploitPerOtherVictim,
                    lingerTicks: ErisExploitLingerTicks,
                    radius: ErisExploitRadius,
                    damageType: DamageType.Normal,
                    strikesOnCast: true)
            });

        /// <remarks>Cast order, and id order — 1001, 1002. Catalyst sits between them in the design table and is not an ability.</remarks>
        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { NanoCell, ErisExploit };

        /// <summary>
        /// Her uniform shape, for drafting: two abilities, a passive and an aura.
        /// The passive carries no magnitude — haste is not speed.
        /// </summary>
        public static OperatorDefinition Definition { get; } = new OperatorDefinition(
            name: "Lethe",
            maxHealth: MaxHealth,
            baseSpeed: BaseSpeed,
            abilities: All,
            aura: Catalyst,
            passive: StatusKind.Hastened,
            passiveDescription: "She never waits for the dice to finish. Every roll carries her a little further than anyone else.");
    }
}