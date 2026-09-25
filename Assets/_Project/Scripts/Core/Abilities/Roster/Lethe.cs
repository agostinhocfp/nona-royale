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
    /// <b>The kit is about where everyone stands, and since 2026-09-24 she
    /// decides it rather than waiting for it.</b> Catalyst pays allies near her
    /// or trailing her, Nano Cell shelters one of them, and Eris' Exploit drags
    /// the enemy together before it turns them on each other.
    ///
    /// <b>Reworked after measurement</b> (<c>LETHE_ANALYSIS.md</c>): as built,
    /// the board almost never made the crowds and huddles her kit needed, and
    /// Nano Cell's stun cost her side more than the bubble saved.
    /// </remarks>
    public static class Lethe
    {
        /// <summary>
        /// Seven: the roster's common health since the +1 of 2026-09-16, level
        /// with Syla, Javi, Kurbyn, Kian, Nuetu and Luka.
        ///
        /// Raised by 1 again on 2026-09-25 (designer, to shorten matches; COMBAT_SYSTEMS §1.1): now 8.
        /// </summary>
        public const int MaxHealth = 8;

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
        /// Cells behind her, along the way everyone travels, that Catalyst also
        /// reaches: the slipstream (designer, 2026-09-24).
        /// </summary>
        /// <remarks>
        /// A race strings a squad out behind its runner; beside her they stood
        /// within two cells on only 27% of her turns. The trail meets them where
        /// they are.
        /// </remarks>
        public const int CatalystTrail = 6;

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
                description: "Stay close to her, or follow in her wake, and the night runs faster. Allies near her or behind her move as if they were already late.",
                trail: CatalystTrail);

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
        /// enemy acted. Duration 2 covers exactly one round of enemy turns.
        /// </remarks>
        public const int NanoCellDurationTurns = 2;

        /// <summary>
        /// A bubble on an ally: it heals 2 on the way in, and nothing that can
        /// be mitigated gets through for a round.
        /// </summary>
        /// <remarks>
        /// <b>No new mechanics.</b> A heal, and a shield with a pool nothing can
        /// empty. Normal and Tech are absorbed, a collision included (§5.6);
        /// Atomic ignores every mitigation layer (§2.2), so bleed, marks, Velvet
        /// Rope, Miracle Pull and Vendetta all go straight through.
        ///
        /// <b>No stun since 2026-09-24</b> (designer). The stun cost the ally one
        /// or two moves, and in a race that price was larger than the shelter:
        /// 88% of bubbles absorbed nothing, a heal-only Nano Cell beat the real
        /// one, and removing the stun moved her from 23% to 27% of wins
        /// (<c>LETHE_ANALYSIS.md</c>). The price is now the energy alone; 5 is
        /// the first dial if she reads strong.
        ///
        /// <b>It blocks the road.</b> A collision's damage is Normal, so the
        /// bubble eats it, the target survives, and the mover bounces (§7.2).
        ///
        /// <b>Neural Purge strips it</b>: a cleanse is indiscriminate (§5.8).
        ///
        /// <b>Cost 4, cooldown 4, range 4</b> (designer, 2026-09-17). It may be
        /// cast on herself.
        /// </remarks>
        public static AbilityDefinition NanoCell { get; } = new AbilityDefinition(
            id: 1001, name: "Nano Cell",
            description:
                "Wraps an ally in a lattice of nanites that knits its wounds and turns ordinary harm aside until it dissolves.",
            energyCost: 4, cooldownTurns: 4, range: 4,
            // Opts in to self-cast (§10, 2026-09-17).
            allowsSelfTarget: true,
            effects: new[]
            {
                AbilityEffect.Heal(EffectScope.PrimaryTarget, 2, EffectAudience.AllyOnly),
                AbilityEffect.Status_(
                    EffectScope.PrimaryTarget, StatusKind.Shield, duration: NanoCellDurationTurns,
                    EffectAudience.AllyOnly, magnitude: NanoCellPool)
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

        /// <summary>How far from the cell Eris' Exploit reaches to drag enemies in.</summary>
        public const int ErisDrawRadius = 4;

        /// <summary>How many cells each of them is dragged toward it, at most.</summary>
        public const int ErisDrawCells = 2;

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
        /// <b>It makes its own crowd</b> (designer, 2026-09-24). Before the zone
        /// strikes, every enemy within <see cref="ErisDrawRadius"/> of the cell
        /// is dragged up to <see cref="ErisDrawCells"/> cells toward it —
        /// placement, so nothing collides (§7.4). Without it, three enemies were
        /// in her reach on 1% of her turns and the quadratic bill was a
        /// formality (<c>LETHE_ANALYSIS.md</c>). The drag also pulls pieces off
        /// safe cells and into allied zones, which is the point.
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
                "Sows discord in a patch of track: it draws the enemy in around it, then every enemy inside turns on every other one at once, and again next round on whoever stayed.",
            energyCost: 4, cooldownTurns: 3, range: 3,
            targeting: AbilityTargeting.Cell,
            effects: new[]
            {
                // The draw first, so the zone strikes the crowd it made.
                AbilityEffect.DrawToCell(ErisDrawRadius, ErisDrawCells),
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