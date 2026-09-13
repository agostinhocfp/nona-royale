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
    /// <b>Two of three abilities.</b> Drone Strike is absent: it paints a
    /// <i>cell</i> and fires a round later, which needs cell targeting and a
    /// deferred-effect registry that do not exist. Both are specified in
    /// ADR-0006 and adopted as a system rather than as this ability's
    /// machinery — more cell-targeted abilities are expected. A drafted Kian is
    /// playable but thin until it lands.
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
        /// </summary>
        public const int MaxHealth = 6;

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
        /// area damage and a stun over 2N+1 cells; this covers 6 cells on one
        /// side for less, and the trade is that they all have to be in front of
        /// him.
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
        /// </remarks>
        public static AbilityDefinition InversionMatrix { get; } = new AbilityDefinition(
            id: 601, name: "Inversion Matrix",
            description:
                "Fires a line of graviton emitters down the track ahead of you, lifting every enemy in their path off the ground.",
            energyCost: 4, cooldownTurns: 3, range: 6,
            requiresTarget: false,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.EnemiesInLineFromCaster, 1, DamageType.Normal,
                    EffectAudience.EnemyOnly, radius: 6),
                AbilityEffect.Status_(EffectScope.EnemiesInLineFromCaster, StatusKind.Stun,
                    duration: 1, radius: 6)
            });

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
        /// </remarks>
        public static AbilityDefinition SonicDisrupter { get; } = new AbilityDefinition(
            id: 602, name: "Sonic Disrupter",
            description:
                "Vents a compressed charge in every direction, hurling nearby enemies clear of you and leaving them struggling to recover.",
            energyCost: 4, cooldownTurns: 3, range: 2,
            requiresTarget: false,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.EnemiesAroundCaster, 2, DamageType.Normal,
                    EffectAudience.EnemyOnly, radius: 2),
                AbilityEffect.Status_(EffectScope.EnemiesAroundCaster, StatusKind.Slow,
                    duration: 1, radius: 2),
                AbilityEffect.Push(EffectScope.EnemiesAroundCaster, distance: 2,
                    EffectAudience.EnemyOnly, radius: 2)
            });

        // id 603 is reserved for Drone Strike, so his ids stay in cast order
        // when it lands rather than being renumbered around it.
        //
        // 6 energy, cooldown 2, unlimited range, 6 Normal damage split between
        // everyone caught. It paints a cell and fires at Kian's next upkeep —
        // one full round, so every opponent moves once before it lands. It
        // survives his neutralize and still credits him. ADR-0006 has the rest.

        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { InversionMatrix, SonicDisrupter };

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