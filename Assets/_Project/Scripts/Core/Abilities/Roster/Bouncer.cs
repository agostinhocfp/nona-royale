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
    ///
    /// <b>Cut on all three axes after human play.</b> Health, reach and damage
    /// all came down at once (2026-09-12). Any one of them alone would have been
    /// measurable; together they are not separable, so if he now reads as weak,
    /// the reach is the first thing to put back — it is the only one of the
    /// three that also governs what his aura can catch.
    /// </remarks>
    public static class Bouncer
    {
        /// <summary>
        /// Nine, not twelve. At 12 he absorbed four collisions and shrugged off
        /// the six-damage sequence every other operator dies to; at 9 he takes
        /// three collisions and dies to two full exchanges. Still the only
        /// operator above 6, which is what a tank is.
        /// </summary>
        public const int MaxHealth = 9;

        /// <summary>
        /// ADR-0002 Amendment 4. He briefly sat at 1.5 for a pacing reason — a
        /// slow tank taxes every match, because the match ends when the
        /// <i>last</i> operator gets home — and opening deployments paid that
        /// cost elsewhere. A tank that moves like everyone else is not a tank.
        /// </summary>
        public const double Speed = 1.0;

        /// <summary>Enemies within this many steps of Bouncer are slowed.</summary>
        /// <remarks>
        /// Equal to Velvet Rope's reach, which is the relationship worth keeping:
        /// everything he can rope is already slowed, and everything he ropes
        /// stays slowed once it arrives. The two were briefly out of step when
        /// the rope went to 4 and back. Reach and aura are one kit (§10.1) — if
        /// one moves, move the other.
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
        /// <b>Back to range 3.</b> It went to 4 as compensation for his speed
        /// and proved to be too much of it — a slow operator with the longest
        /// reach in the game is not slow in any way that costs him. At 3 he
        /// matches Syla, and only Mimi's Translocation reaches further.
        ///
        /// <b>This is also the anti-Evasion tool getting shorter</b>, which
        /// matters more than the number suggests: it is the only reliable route
        /// through Evasive Protocol that is not a two-ability sequence.
        /// </remarks>
        public static AbilityDefinition VelvetRope { get; } = new AbilityDefinition(
            id: 101, name: "Velvet Rope",
            description:
                "Drags an enemy to your side, and nothing they carry will stop it. On an ally, repositions them unharmed.",
            energyCost: 6, cooldownTurns: 2, range: 3,
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
        /// <b>The one-turn kill is gone, deliberately.</b> Rope into Mauling was
        /// 3 Atomic plus 3, and six damage killed either 6-health operator from
        /// full for the price of a banked pool. It is now 3 plus 2, which leaves
        /// them at 1 — a setup rather than an execution, and something the
        /// victim's owner gets a turn to answer.
        ///
        /// <b>The self-damage finally costs something.</b> At 1 against 12
        /// health it took twelve casts to matter, which made it flavour text. At
        /// 2 against 9 it is four, and a Bouncer already wounded has to decide
        /// whether he can afford the exchange.
        ///
        /// The zero cooldown is what is left of the combo. Against the energy
        /// drip alone it is close to inert (§3.1); at the cap it still buys
        /// back-to-back turns, and it is still what allows this to fire twice in
        /// one — for 4 self-damage, which is now most of what he can pay.
        /// </remarks>
        public static AbilityDefinition AllInMauling { get; } = new AbilityDefinition(
            id: 102, name: "All-In Mauling",
            description:
                "A brutal exchange at close quarters that costs you blood as well. On an ally, a rough grapple that patches them up instead.",
            energyCost: 6, cooldownTurns: 0, range: 2,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 2, DamageType.Normal, EffectAudience.EnemyOnly),
                AbilityEffect.Damage(EffectScope.Caster, 2, DamageType.Normal, EffectAudience.EnemyOnly),
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