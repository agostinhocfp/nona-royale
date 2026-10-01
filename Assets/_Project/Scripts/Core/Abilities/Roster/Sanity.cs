// Assets/_Project/Scripts/Core/Abilities/Roster/Sanity.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Operator #8 — the engineer. The roster's immovable object: tied for the
    /// most health on the roster, the only burdened operator, and the first whose
    /// kit needed two new mechanics at once. Content, not logic — but content
    /// that arrived with an amendment (COMBAT_SYSTEMS §10.8, 2026-09-15).
    /// </summary>
    /// <remarks>
    /// <b>Burdened, not slow (designer, 2026-09-17).</b> He moves at 1.0 with
    /// a permanent Burdened passive: −1 cell on a roll of 6 or less, −2 above,
    /// once per roll (§5.16). That replaced a speed of 0.5, which sat below
    /// the 1.0–1.5 band as a recorded override; he is inside the band now.
    /// The history that follows is the override as it stood.
    ///
    /// <b>He transgressed a precedent, knowingly, recorded as a designer
    /// override rather than drift.</b> Speed 0.5 sat below the 1.0–1.5 band
    /// (ADR-0002 Amendment 4). He shipped with a second override — health 12,
    /// tying the maximum the Bouncer cut (2026-09-12) had just vacated — and
    /// the balance pass the same day (<c>46488b6</c>) took him to 9, level
    /// with the Bouncer, so only the speed override stands. COMBAT_SYSTEMS
    /// §10.8 owns the decision; the roster test that enforces the band names
    /// him as its one documented exception.
    ///
    /// <b>The slow immunity went with the 0.5.</b> On the floor, no slow and
    /// no aura could move him. At 1.0 a slow takes him to 0.5 and the burden
    /// still applies on top, so slows bite him harder than anyone: a slowed
    /// Sanity on a 7 moves 4 − 2 = 2. That is the price of the faster crawl,
    /// and it gives Bouncer, Syla, Mimi and Kian a real answer to him.
    ///
    /// <b>He needed two new engine capabilities, which is the point of the
    /// amendment trail.</b> Zero-Day is the first effect anchored to a victim
    /// rather than a cell — <c>DeferredOperatorEffects</c>, the marker status
    /// that makes it cleanse-detachable, and the telegraph event are §6.4 and
    /// §5.10. Collision is the first effect that repositions the caster without
    /// swapping — <see cref="EffectKind.DashToTarget"/> and §7.6. Short Circuit
    /// is built entirely from what already existed.
    ///
    /// <b>Costs are the balance review's outcome, argued against peers.</b>
    /// 3 / 4 / 7, approved 2026-09-15: a basic priced like From the Hip, a
    /// delayed area priced under Drone Strike because it can be cleansed away,
    /// and an ultimate priced under Miracle Pull because its damage is Normal
    /// and its target can be an ally. Reasoned, unmeasured — adding him shifts
    /// the draft's dice stream besides, so nothing here can be compared to
    /// figures taken before him.
    ///
    /// <b>Designer balance pass, 2026-09-16: 3 / 4 / 6, and more reach.</b>
    /// Zero-Day range 2 → 3. Collision cost 7 → 6, cooldown 4 → 3, range
    /// 5 → 6. Both changes push the same way: the slowest operator gets to the
    /// fight sooner and from further out. Why: the bots sweep and human games
    /// agreed he was among the weakest (19% win share) while the fast
    /// operators led, so this pass buffs him as the haste cap trims them.
    /// COMBAT_SYSTEMS §10.8 has the before/after sim.
    /// </remarks>
    public static class Sanity
    {
        /// <summary>
        /// Ten, level with the Bouncer at the top of the roster (9 until the
        /// roster-wide +1 of 2026-09-16, COMBAT_SYSTEMS §1.1).
        /// </summary>
        /// <remarks>
        /// <b>Walked 12 → 9 on 2026-09-15.</b> He shipped at twelve as a
        /// deliberate override, the figure the Bouncer was cut from because he
        /// absorbed four collisions and shrugged off the sequence that kills
        /// everyone else. The balance pass the same day withdrew it: at 9 he
        /// survives two collisions, not three, and the slowest speed ever
        /// fielded is the one override left.
        ///
        /// Raised by 1 again on 2026-09-25 (designer, to shorten matches; COMBAT_SYSTEMS §1.1): now 10.
        /// </remarks>
        public const int MaxHealth = 10;

        /// <summary>
        /// 1.0, with the crawl carried by <see cref="Definition"/>'s Burdened
        /// passive rather than by the multiplier (designer, 2026-09-17).
        /// </summary>
        /// <remarks>
        /// <b>Was 0.5, the first operator outside the band</b> (2026-09-15):
        /// 3.75 cells a roll on average, and a match-length tax the bots sweep
        /// measured as his 21% win share. The designer weighed 0.75 against
        /// haste in reverse. 0.75 breaks the half-step band a player can count
        /// in their head; the burden is a subtraction. Both average about 5.4
        /// cells a roll. Measured with his damage unchanged: 21% → 27%, and
        /// turns per seat 26.5 → 25.1 (COMBAT_SYSTEMS §10.8).
        /// </remarks>
        public const double Speed = 1.0;

        /// <summary>
        /// A charged prod, driven in at arm's length. It shorts the target's
        /// systems and leaves it frozen through its next turn.
        /// </summary>
        /// <remarks>
        /// <b>Damage 1 — the stun is what is being bought.</b> The same shape
        /// as From the Hip, which is 1 damage and a slow at range 3 for the
        /// same price: this trades all of that reach for a stun, the strongest
        /// control status in the game, at melee range on the slowest operator
        /// ever fielded. The range is the whole cost of the ability.
        ///
        /// <b>Cooldown 1 against a 3-cost is the economy's pattern, not a
        /// coincidence.</b> §3.1: a 3-cost ability is only limited at all if it
        /// carries a cooldown, and the drip makes it roughly every-turn anyway.
        /// The declared cooldown is what stops two casts in one banked turn.
        ///
        /// <b>Duration 1 is already "their next turn".</b> Applied outside the
        /// target's turn, a 1-turn stun blocks a whole action phase before it
        /// expires (§5.1).
        /// </remarks>
        public static AbilityDefinition ShortCircuit { get; } = new AbilityDefinition(
            id: 801, name: "Short Circuit",
            description:
                "Jabs an enemy right beside you for a little damage. They are stunned through their next turn, unable to move or use abilities.",
            energyCost: 3, cooldownTurns: 1, range: 1,
            effects: new[]
            {
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 1, DamageType.Normal,
                    EffectAudience.EnemyOnly),
                AbilityEffect.Status_(EffectScope.PrimaryTarget, StatusKind.Stun, duration: 1)
            });

        /// <summary>
        /// A magnetized guided grenade. It snaps onto the target and follows
        /// it, and at the engineer's next upkeep it goes off wherever the
        /// target then stands — hardest on the target itself, and slowing
        /// everyone it catches.
        /// </summary>
        /// <remarks>
        /// <b>The first operator-anchored deferred effect (§6.4).</b> A beacon
        /// is a bet on where somebody will be; this is a delayed certainty that
        /// follows them there. It pays for that certainty in counterplay rather
        /// than in damage: the attachment is telegraphed, and a cleanse strips
        /// the marker and cancels the detonation outright (§5.10) — Javi's
        /// Neural Purge answers a 4-energy ability for 6, which is the
        /// rock-paper-scissors the cleanse exists for.
        ///
        /// <b>2 to everyone in the blast, +2 for the marked target, radius 3
        /// (designer, 2026-10-01; was 1, +1 and radius 1).</b> Four damage on
        /// the primary and two on the ring, over seven cells of track. It keeps
        /// the shape it always had — the inverse of Drone Strike's split, since
        /// the beacon is strongest against a crowd that scatters its beam and
        /// the charge is strongest against the one operator it is riding — but
        /// it is now a real area attack as well, and at radius 3 it will
        /// routinely catch operators nobody aimed at, the caster's own
        /// neighbours included. A target that dies before the detonation still
        /// takes the blast with it — it goes off on the death cell, with no
        /// living recipient for the bonus.
        ///
        /// <b>This is the change that fixed him, and it is the blast that did
        /// it, not the price.</b> Measured over 4000 bot matches: at the old
        /// 1/+1 radius 1 he won 21.4% of the matches he was fielded in, last on
        /// the roster; cutting the cost to 3 and leaving the blast alone moved
        /// him to 20.6%, which is nothing, while leaving the cost at 4 and
        /// raising the blast moved him to 25.2%. He was never short of casts —
        /// Zero-Day's problem was what a cast was worth. The cost went back to
        /// 4 for what the cheap version did to pacing (§12).
        ///
        /// <b>Tech, so it can be answered three ways</b> (2026-09-15). Tech is
        /// damage from a guided or remote-operated device (§2.2), and a homing
        /// grenade is the plainest case. It is otherwise Normal: a plate
        /// absorbs it and an evasion charge can dodge it. A warded Luka takes
        /// none of the blast (§5.12), though the slow still lands, and the
        /// cleanse cancels the whole thing. Atomic would make the counterplay
        /// one-dimensional, and Atomic is deliberately concentrated.
        ///
        /// <b>Cost 4, cooldown 3.</b> It was dropped to 3 on 2026-10-01 and put
        /// back the same day: at 3 the bots cast it 2.83 times a match against
        /// 2.35 at 4, which bought 0.7 points of win share — inside noise — and
        /// cost 0.4 turns and 0.5 knockouts a seat, against a pacing budget that
        /// had just been cut from 34 turns to 24.7 (§12). The old note here
        /// priced it under "Drone Strike's 6"; Drone Strike has cost 4 for some
        /// time, so the two now sit level and the cleanse is what separates
        /// them — this one can be answered, that one cannot. The cooldown is the
        /// real limiter either way (§3.1).
        ///
        /// <b>Cost 3 carries no Equilibrium penalty, which is why it looked
        /// cheaper than it was.</b> Equilibrium doubles a hit from a cast
        /// costing 3 or less (§5.17), so 3 should have meant a doubled blast
        /// against Revú. It does not: <c>DamageInstance.CastCost</c> is null for
        /// anything that resolves later, and a charge detonation is a device,
        /// not a cast. The whole cheap-cast tax the rest of the roster pays
        /// never reaches this ability.
        ///
        /// <b>Range 4 (2 until 2026-09-16, then 3; designer, 2026-10-01).</b> At
        /// 2 the slowest operator on the roster had to stand inside the fight to
        /// throw it, the same price Nuetu pays for his whole kit. At 4 he throws
        /// it from a cell further back than From the Hip or Blind Spot reach,
        /// which on an operator at speed 1.0× is the difference between setting
        /// the charge and being in range of the answer.
        /// </remarks>
        public static AbilityDefinition ZeroDay { get; } = new AbilityDefinition(
            id: 802, name: "Zero-Day",
            description:
                "Sticks a charge on an enemy. When your next turn begins it goes off wherever they have moved, hitting them hardest, catching every enemy nearby, and slowing them all. A cleanse removes it before it blows.",
            energyCost: 4, cooldownTurns: 3, range: 4,
            effects: new[]
            {
                AbilityEffect.AttachCharge(
                    splashDamage: 2, primaryBonus: 2, radius: 3,
                    damageType: DamageType.Tech,
                    detonationStatus: StatusKind.Slow, statusDuration: 1)
            });

        /// <summary>
        /// The engineer anchors himself to a target — enemy or ally — and
        /// launches himself along the track at it, raking every enemy he passes
        /// and landing a cell behind it. An enemy anchor is struck and stunned;
        /// an ally is a pure mobility anchor.
        /// </summary>
        /// <remarks>
        /// <b>The first effect that repositions the caster without swapping
        /// (§7.6).</b> The dash is placement, not movement: it collides with
        /// nothing, triggers nothing, and passes through occupants without
        /// contesting them. The only damage the path deals is the dash's own —
        /// and the target's hit and stun are declared as ordinary
        /// enemy-audience effects, so the cast-mode system filters them out
        /// for an ally anchor instead of the dash branching on it.
        ///
        /// <b>It is the mobility his burden denies him.</b> A roll of 7 moves
        /// him five cells; this moves him up to seven — six to the target
        /// and one past it — in either direction, for 6 energy once every three
        /// of his turns. (Dated: the range is 5 since 2026-09-20, and
        /// Translocation's is 9 since 2026-09-24, so the tie is gone; Drone
        /// Strike's is unlimited, but it aims at a cell.) Dashing
        /// backwards to an ally behind him is the escape the rest of the kit
        /// refuses to give him, and it is also why the ability carries the
        /// camping rule: a sheltered engineer may not dash to an ally behind
        /// himself (§4.4, second amendment).
        ///
        /// <b>Cost 6, under Miracle Pull's 9.</b> Three Normal and a stun on
        /// the anchor plus a rake along the path is less than a possible
        /// execute, and the dash cuts both ways — it delivers the slowest
        /// operator in the game to exactly where the fight is, which is
        /// sometimes where he wanted to be and sometimes not.
        ///
        /// <b>No longer priced as an ultimate (2026-09-16).</b> It shipped at
        /// 7 energy, cooldown 4, range 5. At 6 / 3 it has the same price and
        /// cooldown as Ace Shards, Cryo-Pulse, Vendetta and Neural Purge, so it
        /// sits in the roster's mid tier. The cooldown still keeps it an event
        /// rather than a commute, but a more frequent one.
        /// </remarks>
        public static AbilityDefinition Collision { get; } = new AbilityDefinition(
            id: 803, name: "Collision",
            description:
                "Charge down the track to the target, clipping every enemy you pass, and land just beyond it. On an enemy you hit hard and stun them for their next turn; on an ally it is just the ride.",
            energyCost: 6, cooldownTurns: 3, range: 5,
            effects: new[]
            {
                AbilityEffect.Dash(pathDamage: 1),
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 3, DamageType.Normal,
                    EffectAudience.EnemyOnly),
                AbilityEffect.Status_(EffectScope.PrimaryTarget, StatusKind.Stun, duration: 1,
                    EffectAudience.EnemyOnly)
            });

        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { ShortCircuit, ZeroDay, Collision };

        /// <summary>
        /// His uniform shape, for drafting. No aura; the passive is his burden,
        /// and it carries no magnitude — a burden is not speed.
        /// </summary>
        public static OperatorDefinition Definition { get; } = new OperatorDefinition(
            name: "Sanity",
            maxHealth: MaxHealth,
            baseSpeed: Speed,
            abilities: All,
            passive: StatusKind.Burdened,
            passiveDescription: "The heaviest thing on the board. Every roll carries him a little less far than it should.");
    }
}
