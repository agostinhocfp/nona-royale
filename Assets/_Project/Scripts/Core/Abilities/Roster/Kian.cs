// Assets/_Project/Scripts/Core/Abilities/Roster/Kian.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Operator #6 — Artillery. The first operator whose whole kit is area
    /// damage, and the first with no single-target ability at all. Content, not
    /// logic.
    /// </summary>
    /// <remarks>
    /// <b>He is a sixth archetype.</b> The base four — tank, assassin, brawler,
    /// support — are filled, "Brawler" already sits outside them as a fifth, and
    /// this is a sixth. The archetype taxonomy in the design docs now describes
    /// a smaller game than the one that exists, and should be rewritten or
    /// dropped rather than stretched.
    ///
    /// <b>Priced on being reached, not on reach.</b> Bouncer pays in speed,
    /// Mimi and Javi pay in health for their range. Kian pays in both directions
    /// at once: at 6 health he dies to two collisions, and at 1.0 he is the
    /// slowest thing on the board with no escape tool in his kit. Anyone who
    /// closes on him has him. That is the entire cost of a kit that otherwise
    /// never needs to be near anything.
    ///
    /// <b>Complete.</b> Drone Strike brought cell targeting and the deferred
    /// cell-effect registry with it, adopted as a system rather than as this
    /// ability's machinery (ADR-0006) on the expectation that mines, zones and
    /// timed hazards follow. He is the only operator whose third ability
    /// required new engine capability rather than new content.
    ///
    /// <b>Every number here is reasoned and none is measured.</b> He entered the
    /// pool after the §12 baseline was withdrawn, and adding an operator shifts
    /// the RNG stream besides, so nothing on this sheet can be compared to
    /// anything recorded before it.
    /// </remarks>
    public static class Kian
    {
        /// <summary>
        /// Six, down from a sketched seven. At 7 he was second-toughest on the
        /// roster <i>and</i> longest-ranged, which left him paying for nothing.
        /// At 6 he sits with Syla and Javi, and unlike either of them he cannot
        /// run.
        ///
        /// Seven since the roster-wide +1 of 2026-09-16 (COMBAT_SYSTEMS §1.1).
        /// The sketched 7 is not back: everyone moved, so he still sits with
        /// Syla and Javi.
        ///
        /// Raised by 1 again on 2026-09-25 (designer, to shorten matches; COMBAT_SYSTEMS §1.1): now 8.
        /// </summary>
        public const int MaxHealth = 8;

        /// <summary>
        /// The floor of the 1.0–1.5 band (ADR-0002 Amendment 4), shared with
        /// Bouncer and Mimi. For an operator with no repositioning tool this is
        /// the whole of his vulnerability.
        /// </summary>
        public const double Speed = 1.0;

        /// <summary>
        /// Synchronised micro-graviton emitters fire along the track ahead of
        /// him, lifting everything caught in the beam off its feet.
        /// </summary>
        /// <remarks>
        /// <b>The roster's first directional effect.</b> Every other area is
        /// symmetric — "within N" both ways — which makes a self-centred blast
        /// something you position for rather than aim. A line points somewhere,
        /// and choosing where is a decision no other area ability asks for.
        /// Fired forward along his own direction of travel; a bidirectional
        /// version would be Dargin Pulse with a longer reach and no new
        /// decision in it.
        ///
        /// <b>Damage 1 — it is a control tool, not a damage ability.</b> The
        /// stun is what is being bought. Compare Dargin Pulse at 6 energy for 2
        /// area damage and a stun over 2N+1 cells; this covers 4 cells on one
        /// side for less, and the trade is that they all have to be in front of
        /// him. The line was 6 cells until the 2026-09-15 balance pass
        /// (<c>245a60b</c>).
        ///
        /// <b>Duration 1 is already "their next turn".</b> A status applied
        /// outside its target's turn takes hold on that target's <i>next</i> one,
        /// so duration 1 costs each victim a whole action phase. Duration 2
        /// would cost two, which for a multi-target stun is a different ability.
        ///
        /// <b>Cost walked 3 → 4.</b> At 3 it sat at the cheapest price in the
        /// game — Nanite Infusion's — for something that can lock down two or
        /// three operators at once, where Kurbyn's stun reaches one. 4 is still
        /// cheap for what it does, and it is one of the three abilities that
        /// ended the 3/6/9 cost tier (abolished 2026-09-13, <c>Roster</c>).
        /// Costs are now argued against peers rather than rounded to a rung.
        ///
        /// <b>Designer buff, 2026-09-17: cost 3, 2 Tech.</b> Kian was last in
        /// the bots sweep (19%). Back to the cheapest rung, with twice the
        /// damage, and Tech. That is a designer call against §2.2's rule of
        /// thumb (Tech is a guided or remote device); the emitters are read as
        /// a device he fires. A warded Luka now takes none of it, and a Revú
        /// takes it doubled (§5.17, cost 3).
        /// </remarks>
        public static AbilityDefinition InversionMatrix { get; } = new AbilityDefinition(
            id: 601, name: "Inversion Matrix",
            description:
                "Fires a line of graviton emitters down the track ahead of you, lifting every enemy in their path off the ground.",
            energyCost: 3, cooldownTurns: 3, range: 4,
            targeting: AbilityTargeting.None,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.EnemiesInLineFromCaster, 2, DamageType.Tech,
                    EffectAudience.EnemyOnly, radius: 4),
                AbilityEffect.Status_(EffectScope.EnemiesInLineFromCaster, StatusKind.Stun,
                    duration: 1, radius: 4)
            });

        /// <summary>Sonic Disrupter's reach each way, for damage, slow and push. 2 until 2026-09-17.</summary>
        public const int SonicDisrupterRadius = 3;

        /// <summary>
        /// A hyper-compressed charge vents all at once, and everything standing
        /// near him is thrown clear of it.
        /// </summary>
        /// <remarks>
        /// <b>The effect order is a rule.</b> Damage, then slow, then push.
        /// Recipients are recomputed for every effect in the list, so pushing
        /// first would carry enemies from within radius 2 to as far as 4 and the
        /// slow would then find nobody. Anyone reordering this list breaks the
        /// ability silently.
        ///
        /// <b>Push away from the caster, and some enemies gain ground.</b> An
        /// enemy behind him is thrown backwards and loses progress; one ahead of
        /// him is thrown toward its own home. Considered and accepted: the
        /// operator is meant to be punishing and swingy, and reading which side
        /// of him to stand on is a real thing for an opponent to get right. It
        /// is the first ability in the game that can help the player it is aimed
        /// at, and that is the axis to revisit first if he reads badly at the
        /// table.
        ///
        /// <b>The forward clamp is not a balance dial.</b> A push stops at the
        /// last outer-track cell and never carries anyone across a home entry.
        /// Without it this finishes an opponent's lap for them.
        ///
        /// <b>An enemy sharing his cell is thrown backwards.</b> That is only
        /// reachable on a safe cell, where no collision resolves — which is
        /// exactly the free parking §4.4 worries about, and exactly what this
        /// ability should break up.
        ///
        /// <b>Cost 4, argued against its peers.</b> Dargin Pulse is 6 for 2
        /// area damage and a stun; Ace Shards 6 for 3 and a bleed. This is 2, a
        /// slow and a shove for less than either, which is defensible only
        /// because a slow is the weakest of the three statuses and the shove
        /// cuts both ways. Under the abolished 3/6/9 tier it would have had to
        /// be 3 or 6, and neither was the right number.
        ///
        /// <b>Designer buff, 2026-09-17: cost 3, radius 3, Tech.</b> Kian was
        /// last in the bots sweep. The vent now reaches three cells each way
        /// (seven in all) for damage, slow and push alike, and its 2 damage is
        /// Tech — a designer call against §2.2's self-centred-is-Normal rule
        /// of thumb. A warded Luka shrugs off the damage but is still slowed
        /// and shoved; a Revú takes the 2 doubled (§5.17, cost 3). The push
        /// distance stays 2, so an enemy at the edge ends up five away.
        /// </remarks>
        public static AbilityDefinition SonicDisrupter { get; } = new AbilityDefinition(
            id: 602, name: "Sonic Disrupter",
            description:
                "Vents a compressed charge in every direction, hurling nearby enemies clear of you and leaving them struggling to recover.",
            energyCost: 3, cooldownTurns: 3, range: SonicDisrupterRadius,
            targeting: AbilityTargeting.None,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.EnemiesAroundCaster, 2, DamageType.Tech,
                    EffectAudience.EnemyOnly, radius: SonicDisrupterRadius),
                AbilityEffect.Status_(EffectScope.EnemiesAroundCaster, StatusKind.Slow,
                    duration: 1, radius: SonicDisrupterRadius),
                AbilityEffect.Push(EffectScope.EnemiesAroundCaster, distance: 2,
                    EffectAudience.EnemyOnly, radius: SonicDisrupterRadius)
            });

        /// <summary>
        /// A beacon paints a square of the track. One round later a particle beam
        /// comes down on it, and on the ground either side.
        /// </summary>
        /// <remarks>
        /// <b>The delay is the mechanic, not a cost.</b> Every other ability in
        /// the game resolves the instant it is paid for, which makes positioning
        /// something a player reacts to. This makes it something both players
        /// commit to a round ahead: he bets on where somebody will be, and they
        /// decide whether moving off it is worth what moving costs them.
        ///
        /// <b>It is at its best against one target and its worst against a
        /// crowd.</b> Four on a lone operator takes a 6-health operator to 2 and
        /// Mimi to 1 — inside collision range, but nobody dies from full. Split
        /// two ways it is 2 each, three ways 1 each (the split floors), both less
        /// than a collision. So its counterplay is to <i>bunch up</i> — which
        /// works against everything else on the board, since Ace Shards, Dargin
        /// Pulse and Cryo-Pulse all punish standing together. That tension is the
        /// reason it earns a place rather than being a second area attack.
        ///
        /// <b>Radius 1, three cells.</b> At radius 0 it is one square out of 52
        /// painted a full round ahead, which is a bet thin enough that nobody
        /// would take it; the splash gives the prediction a margin without making
        /// it forgiving.
        ///
        /// <b>Unlimited range, which is his only free axis.</b> He pays in
        /// fragility and speed, not distance — 6 health at 1.0 with no escape
        /// tool. It also means the beacon is his contribution to a fight he is
        /// nowhere near, which is what an artillery operator should be doing.
        ///
        /// <b>Tech, so the plate and the charge still blunt it</b> (2026-09-15).
        /// A drone is a remote-operated device, which is §2.2's Tech rule, and
        /// a warded Luka takes none of the beam (§5.12). Atomic is deliberately
        /// concentrated, and a 4-damage strike that ignored every defence would
        /// make Javi pointless against him.
        ///
        /// <b>A warded Luka still counts toward the split.</b> The beam divides
        /// among everyone caught before any hit reaches the pipeline, so his
        /// share is blocked rather than passed on. An ally standing under the
        /// beam with him takes half of what it would alone.
        ///
        /// <b>Beam walked 6 → 4 on 2026-09-15</b> (<c>245a60b</c>). At 6 a
        /// correct guess killed four of the roster's operators from full; at 4 it
        /// kills nobody from full and sets up the kill instead.
        ///
        /// <b>Cost 6, cooldown 2 — and the cooldown is doing the limiting.</b>
        /// It paints on one turn, fires on the next, and can be painted again the
        /// turn after that: one idle turn between strikes. With range gone as a
        /// price this is the only dial left on him, which is where to look first
        /// if he reads as oppressive.
        ///
        /// <b>It survives his death and still credits him</b> (ADR-0006). A
        /// deployed device is not its operator, and letting a kill refund six
        /// spent energy would make the ability worse than it reads.
        ///
        /// <b>Cost 4 since 2026-09-17 (designer; was 6).</b> Part of the Kian
        /// buff. The cooldown of 2 still does the limiting, and at 4 a paint
        /// every other turn fits the drip without banking. It was already his
        /// most-cast ability and the most-cast in the game.
        /// </remarks>
        public static AbilityDefinition DroneStrike { get; } = new AbilityDefinition(
            id: 603, name: "Drone Strike",
            description:
                "Paints a square anywhere on the board. A beam comes down on it next round, splitting its force between everyone caught underneath.",
            energyCost: 4, cooldownTurns: 2,
            range: AbilityDefinition.UnlimitedRange,
            targeting: AbilityTargeting.Cell,
            effects: new[]
            {
                AbilityEffect.PaintCell(totalDamage: 4, radius: 1, DamageType.Tech)
            });

        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { InversionMatrix, SonicDisrupter, DroneStrike };

        /// <summary>
        /// His uniform shape, for drafting. No aura, no passive, and no
        /// single-target ability — the only operator on the roster who cannot
        /// pick one enemy and hit it.
        /// </summary>
        public static OperatorDefinition Definition { get; } = new OperatorDefinition(
            name: "Kian",
            maxHealth: MaxHealth,
            baseSpeed: Speed,
            abilities: All);
    }
}