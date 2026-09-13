// Assets/_Project/Scripts/Core/Abilities/Roster/Bouncer.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Operator #1 — Tank. Stats and kit, transcribed from
    /// <c>COMBAT_SYSTEMS.md</c> §10.1.
    /// </summary>
    /// <remarks>
    /// <b>Content, not logic.</b> Every ability is a list of effects the
    /// resolver already knows how to run; nothing here branches on which
    /// operator is casting. A new operator that needs a new <i>mechanic</i> gets
    /// an amendment to <c>COMBAT_SYSTEMS.md</c>, never a special case in a stat
    /// block (§12).
    ///
    /// <b>Priced on positioning, not energy.</b> He is the roster's slowest
    /// operator, so the real cost of his kit is the turns it takes him to be
    /// standing near anyone. That makes him the most pool-efficient operator in
    /// the squad, which is a legitimate reason to run him.
    /// </remarks>
    public static class Bouncer
    {
        public const int MaxHealth = 12;

        /// <summary>
        /// ADR-0002 Amendment 4. He briefly sat at 1.5 for a pacing reason — a
        /// slow tank taxes every match, because the match ends when the
        /// <i>last</i> operator gets home — and opening deployments paid that
        /// cost elsewhere. A tank that moves like everyone else is not a tank.
        /// </summary>
        public const double Speed = 1.0;

        /// <summary>Enemies within this many steps of Bouncer are slowed.</summary>
        /// <remarks>
        /// Widened from 2 to match Velvet Rope's previous reach, so anything he
        /// could rope before is already slowed and anything he ropes now is
        /// slowed the moment it arrives. Reach and aura are one kit (§10.1).
        /// </remarks>
        public const int IntimidatingPresenceRadius = 3;

        /// <summary>
        /// His passive aura. Not an ability — it is never used, has no cost and
        /// no duration; it is simply true while an enemy stands close enough,
        /// and is evaluated when that enemy's movement is calculated.
        /// </summary>
        public static AuraDefinition IntimidatingPresence { get; } =
            new AuraDefinition("Intimidating Presence", IntimidatingPresenceRadius, -0.5);

        /// <summary>
        /// Pull the target adjacent, damaging it if it is an enemy. Usable on an
        /// ally purely to reposition, which is why the pull is audience-Any and
        /// the damage is enemy-only.
        /// </summary>
        /// <remarks>
        /// <b>Atomic, which makes Bouncer the roster's direct counter to
        /// Evasion</b> (§2.2). That was a targeted answer to Kurbyn dominating
        /// the first human sessions, chosen over weakening Evasion itself: a
        /// counter preserves the rock-paper-scissors, a nerf flattens it.
        ///
        /// <b>Range 4</b> is the compensation for his speed, and it makes the
        /// rope a soft denial tool: pulling an operator four cells back from its
        /// home mouth is a swing the board has no other answer to. It was the
        /// longest reach in the game until Mimi's Translocation at 6.
        /// </remarks>
        public static AbilityDefinition VelvetRope { get; } = new AbilityDefinition(
            id: 101, name: "Velvet Rope", energyCost: 6, cooldownTurns: 2, range: 4,
            effects: new[]
            {
                AbilityEffect.Pull(EffectAudience.Any),
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 3, DamageType.Atomic, EffectAudience.EnemyOnly)
            });

        /// <summary>
        /// Wounds the target and the Bouncer alike; on an ally it is a playful
        /// grapple that heals instead. The self-damage is aimed at the caster,
        /// which routes it around the pipeline entirely (§2.3) — it cannot be
        /// evaded or shielded and it can neutralize him.
        /// </summary>
        /// <remarks>
        /// <b>This and Velvet Rope are a combo, not alternatives.</b> On the same
        /// operator at the same cost they look redundant — the rope has more
        /// range, is unblockable, pulls, and costs no health. The point is that
        /// you cast both: the rope pulls the target adjacent and hits for 3
        /// Atomic, this follows at range 2 for 3 more. Six damage in one turn
        /// kills either 6-health operator from full. It needs the full 12-energy
        /// bank, so it comes round about every third turn — ultimate cadence,
        /// from the one operator with no ultimate.
        ///
        /// That is also what makes the zero cooldown worth having. Against the
        /// energy drip alone it is close to inert (§3.1); at the cap it buys
        /// back-to-back turns, and it is what allows this to fire twice in one.
        /// </remarks>
        public static AbilityDefinition AllInMauling { get; } = new AbilityDefinition(
            id: 102, name: "All-In Mauling", energyCost: 6, cooldownTurns: 0, range: 2,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 3, DamageType.Normal, EffectAudience.EnemyOnly),
                AbilityEffect.Damage(EffectScope.Caster, 1, DamageType.Normal, EffectAudience.EnemyOnly),
                AbilityEffect.Heal(EffectScope.PrimaryTarget, 3, EffectAudience.AllyOnly)
            });

        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { VelvetRope, AllInMauling };

        /// <summary>His uniform shape, for drafting. Stats, kit and aura in one object.</summary>
        public static OperatorDefinition Definition { get; } = new OperatorDefinition(
            name: "Bouncer",
            maxHealth: MaxHealth,
            baseSpeed: Speed,
            abilities: All,
            aura: IntimidatingPresence);
    }
}