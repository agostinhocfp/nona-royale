// Assets/_Project/Scripts/Core/Abilities/Roster/Javi.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Operator #5 — Support, and the first operator to fill the fourth base
    /// archetype. Content, not logic.
    /// </summary>
    /// <remarks>
    /// <b>He is the reason the shield layer matters.</b> Shields were granted
    /// only by a deferred board space, which could never generate enough uptime
    /// for a mitigation type to mean anything. A support who puts them up on
    /// demand is what makes the Normal/Force/Tech distinction real, and
    /// therefore what unblocks Mimi's identity as well as his own. <b>The Tech
    /// and Force types are still unbuilt</b> — this removed their blocker, not
    /// the work.
    ///
    /// <b>Complete as of 2026-09-13.</b> Trauma Plate landed with the shield
    /// half of the mitigation pass, which reworked
    /// <c>IDamageMitigation.TryAbsorb</c> into <c>AbsorbFrom</c> and turned a
    /// whole-instance absorb into a pool. The evasion half of that pass — a flat
    /// reduction replacing the roll — was <b>declined</b>; the rate moved to 0.3
    /// instead. Anyone reading <c>_HANDOFF_mitigation.md</c> should know only
    /// half of it was adopted.
    ///
    /// <b>Ranges raised on 2026-09-15</b> (<c>e85d710</c>): Nanite Infusion
    /// and Neural Purge 3 → 5 (CPR keeps the 5), Trauma Plate 3 → 4. The kit was designed at 3
    /// across the board. Neural Purge now reaches one cell short of Mimi's
    /// Translocation, which COMBAT_SYSTEMS §10.4 treats as her only
    /// compensation for 5 health — the gap to watch if either moves again.
    ///
    /// <b>He may be the operator that tips a combat game into a race.</b> A
    /// dedicated healer with a castable shield makes kills materially harder to
    /// land, on a board whose stated priority is 70% combat. The kill bounty
    /// (§1.2) pushes the other way and landed at the same time. Neither
    /// direction has been measured, and no figure in §12 was taken with either
    /// of them in play — so his strength is an open question, not an unsettled
    /// design.
    /// </remarks>
    public static class Javi
    {
        /// <summary>
        /// Seven: 6 plus the roster-wide +1 of 2026-09-16 (COMBAT_SYSTEMS
        /// §1.1), which cut knockouts, and so match length, in the bots sweep.
        ///
        /// Raised by 1 again on 2026-09-25 (designer, to shorten matches; COMBAT_SYSTEMS §1.1): now 8.
        /// </summary>
        public const int MaxHealth = 8;

        /// <summary>
        /// His abilities reach 5, 4 and 5. A support who cannot reach the fight
        /// is a dead ability list, so he pays for his reach in fragility rather
        /// than in speed.
        /// </summary>
        public const double Speed = 1.5;

        /// <summary>
        /// How far Nanite Infusion's runoff heal reaches from <b>Javi</b>
        /// (designer, 2026-10-01). Deliberately his cast range, not the old
        /// radius of 2: the clause now covers everyone he could have aimed at.
        /// </summary>
        public const int RunoffRadius = 5;

        /// <summary>
        /// How many turns a primed ally stays primed (designer, 2026-10-01).
        /// Two against CPR’s cooldown of 4: the save is a bet on the next round,
        /// not standing cover.
        /// </summary>
        public const int CprTurns = 2;

        /// <summary>
        /// Nanites seal breached suits and cauterize wounds. Turned on an enemy
        /// they do the opposite, and the squad standing around <b>Javi</b> gets
        /// the runoff.
        /// </summary>
        /// <remarks>
        /// <b>Heal 2, not 3.</b> Collision is 3, so healing 2 never fully undoes
        /// a hit — he blunts damage rather than erasing it, and Bouncer's heal-3
        /// keeps a reason to exist. Cooldown 2 on a 3-cost ability is the first
        /// cooldown on the roster that binds tighter than the energy drip (§3.1),
        /// which is the only way a cheap ability gets limited at all.
        ///
        /// <b>The splash heal is declared <see cref="EffectAudience.EnemyOnly"/>
        /// despite healing allies.</b> Audience selects which <i>cast mode</i> an
        /// effect belongs to, not who receives it — the scope does that. This is
        /// the All-In Mauling pattern, where a self-damage effect is enemy-only
        /// so that the friendly cast costs nothing.
        ///
        /// <b>The runoff heal moved off the target and onto Javi (designer,
        /// 2026-10-01).</b> It was <c>AlliesAroundPrimaryTarget</c> at radius 2
        /// and is now <see cref="EffectScope.AlliesAroundCaster"/> at radius
        /// <see cref="RunoffRadius"/>. Three things change, and only the first
        /// was the brief:
        ///
        /// <b>1. The anchor.</b> Allies are measured from Javi, so the hostile
        /// cast rewards a squad for grouping around its support instead of
        /// around the enemy it is shooting at.
        ///
        /// <b>2. Javi always heals himself.</b> He stands at distance zero from
        /// his own cell, so every hostile cast returns him 1 health
        /// unconditionally — a self-heal with no positioning and no second
        /// ability spent. The 2026-09-17 self-cast opt-in gave him a way to
        /// treat himself for 3 energy; this gives him a smaller one for free,
        /// attached to the cast he was making anyway.
        ///
        /// <b>3. The footprint quadruples.</b> Radius 2 is five cells of track,
        /// radius 5 is eleven, so on a 52-cell loop a fifth of the board heals
        /// rather than a tenth of it.
        ///
        /// <b>What was given up was the tension that justified the clause.</b>
        /// The old scope paid a squad for standing next to an enemy, which is
        /// exactly where Ace Shards and Dargin Pulse punish them for standing:
        /// the heal was a reason to take a risk. Anchored on Javi it asks for
        /// nothing a squad would not do anyway, so it is a buff at unchanged
        /// numbers, landing on the operator the 2026-10-01 sweep had joint top
        /// of the board. Measured below and in COMBAT_SYSTEMS §10.5.
        /// </remarks>
        public static AbilityDefinition NaniteInfusion { get; } = new AbilityDefinition(
            id: 501, name: "Nanite Infusion",
            description:
                "Heals an ally, or yourself. Aimed at an enemy it wounds them instead, and you and any of your operators standing near you are healed a little.",
            energyCost: 3, cooldownTurns: 2, range: 5,
            // Opts in to self-cast (§10, 2026-09-17): his toolkit is defensive,
            // and a healer who cannot treat himself is half one.
            allowsSelfTarget: true,
            effects: new[]
            {
                AbilityEffect.Heal(EffectScope.PrimaryTarget, 2, EffectAudience.AllyOnly),
                AbilityEffect.Damage(EffectScope.PrimaryTarget, 2, DamageType.Normal,
                    EffectAudience.EnemyOnly),
                AbilityEffect.Heal(EffectScope.AlliesAroundCaster, 1,
                    EffectAudience.EnemyOnly, radius: RunoffRadius)
            });

        /// <summary>
        /// A ballistic insert bolted onto an ally's plate carrier. It takes what
        /// comes, and then it fails.
        /// </summary>
        /// <remarks>
        /// <b>The name is the mechanic.</b> A ballistic insert absorbs a fixed
        /// amount and then stops working, which is exactly what a pool does.
        /// Nothing about it needs a number to be understood, which is the bar
        /// every ability description has to clear.
        ///
        /// <b>Pool 2, against a Normal spread of 1, 2, 2, 2, 3 and collision at
        /// 3.</b> It eats one small hit whole or takes the edge off a collision,
        /// never both. A 1-point pool was rejected: it cancels From the Hip
        /// outright, halves three of the four 2-damage abilities, and saves
        /// nobody from the collision that actually kills them — blunting
        /// everything that does not matter and nothing that does.
        ///
        /// <b>Cost walked 3 → 6 → 4.</b> The original sketch was 3, which was
        /// too cheap next to a 6-energy Velvet Rope. 6 was settled on 2026-09-12
        /// and then reconsidered: the same 6 buys Atomic damage that ignores
        /// every defence in the game, and 2 points of absorb is a poor rate
        /// against that. 4 is the compromise and is <b>reasoned, not
        /// measured</b> — revisit after the harness re-baseline.
        ///
        /// <b>Cooldown 3 against duration 2, deliberately.</b> The original
        /// cooldown 1 gave permanent uptime: he could hold plates on two
        /// operators forever and near-cover all three, which is not a shield at
        /// all but flat damage reduction on a squad. At 3 the plate is up for two
        /// of every four of the holder's turns, so choosing <i>when</i> is the
        /// whole skill of the ability.
        ///
        /// <b>Range 4, not the sketched 6.</b> Range 6 was Mimi's (9 since
        /// 2026-09-24), and §10.4 made it the sole compensation for her 5 health. Designed at 3; raised to 4
        /// in the 2026-09-15 pass, one less than his other two abilities.
        ///
        /// <b>Ally-only, so a hostile cast is refused and costs nothing.</b>
        /// Every effect scoped away by the cast mode returns
        /// <c>TargetingVerdict.WrongSide</c> before payment.
        ///
        /// <b>CPR’s save destroys it (2026-10-01).</b> The save strips every
        /// status as it fires, shields included — which no longer costs the
        /// player anything to plan around, because by then the plate has either
        /// been spent or failed to matter. Neural Purge used to strip it on
        /// demand, so the two abilities could be cast in the wrong order and
        /// waste one; that trap is gone with the cleanse.
        /// </remarks>
        public static AbilityDefinition TraumaPlate { get; } = new AbilityDefinition(
            id: 502, name: "Trauma Plate",
            description:
                "Gives an ally, or yourself, a shield that soaks up damage until it is used up or wears off. Atomic hits go straight through it.",
            energyCost: 4, cooldownTurns: 3, range: 4,
            // Opts in to self-cast (§10, 2026-09-17): defensive toolkit — a
            // support plates himself when the fight comes to him.
            allowsSelfTarget: true,
            effects: new[]
            {
                AbilityEffect.Status_(
                    EffectScope.PrimaryTarget, StatusKind.Shield, duration: 2,
                    EffectAudience.AllyOnly, magnitude: 2)
            });

        /// <summary>
        /// A field kit and two hands. He primes an ally against the hit that
        /// would put it down, and when that hit lands he keeps it breathing.
        /// </summary>
        /// <remarks>
        /// <b>CPR replaced Neural Purge on 2026-10-01 (designer).</b> The cleanse
        /// was the least cast ability in the game at 0.13 casts a match across
        /// 4000 bot matches, and the slot was the obvious place to put something
        /// that mattered. What the cleanse was, for the record: 6 energy to strip
        /// every applied status from one ally, the only answer in the game to a
        /// Zero-Day charge, a Hunted follow-up, Syla’s mark, a stun or a burden.
        /// Its text and reasoning are in the 2026-10-01 entry of
        /// COMBAT_SYSTEMS §14.
        ///
        /// <b>It defends progress, which nothing else does.</b> Neutralize is a
        /// setback rather than a removal: health comes back for free, and what is
        /// actually lost is the track. <c>NeutralizeRules</c> resets progress to
        /// the yard and hands the killer a bounty, and roughly half of all
        /// movement in a match is somebody re-walking ground they had already
        /// covered. A save is therefore worth more the further along its holder
        /// is — the only defensive effect in the game with that property, and
        /// the reason it reads as a support’s ultimate rather than a bigger heal.
        ///
        /// <b>It is a bet placed a round early, like the rest of this game’s
        /// best abilities.</b> Zero-Day, Drone Strike and Killzone all ask a
        /// player to commit before they know; this asks the same question from
        /// the other side. Cast on the wrong operator it is 6 energy for
        /// nothing, and the badge is visible to opponents on purpose — a bet
        /// nobody can see is a trap, and the attacker has to be able to read it
        /// and go after somebody else.
        ///
        /// <b>Why 1 health and no heal.</b> The save refuses the consequence, not
        /// the damage: the pipeline has already taken the holder to zero, and
        /// this puts it back to exactly 1. So the next blow still finishes the
        /// job and the rescue is a reprieve rather than a second life. Pairing it
        /// with a heal was rejected for that reason, and because Nanite Infusion
        /// is the heal.
        ///
        /// <b>Duration 2 against cooldown 4.</b> Deliberately the inverse of
        /// Trauma Plate’s 2-against-3: the plate is up half the time, this is up
        /// two turns in four, because an always-available save is not a bet. The
        /// cost stays at Neural Purge’s 6 — it is the most valuable thing he can
        /// do with a turn, and at 4 or 5 he would simply always be holding one.
        ///
        /// <b>It carries the game’s only cleanse, narrowed.</b> The save strips
        /// every status as it fires (<c>StatusKind.Defiance</c>), so the
        /// mechanic survives with one caller — but at the moment of rescue
        /// instead of on demand. A charge can no longer be washed off before it
        /// blows, only survived, which makes Zero-Day and Blind Spot’s follow-up
        /// quietly stronger than their own reasoning assumes. §2.4 and §10.8
        /// carry that as a watch item.
        ///
        /// <b>Ally-only, and he can prime himself</b> (§10’s opt-in, 2026-09-17),
        /// which is the case the bots will never find: the support who dies is
        /// usually the one nobody plated.
        /// </remarks>
        public static AbilityDefinition Cpr { get; } = new AbilityDefinition(
            id: 503, name: "CPR",
            description:
                "Primes an ally, or yourself, for a short while. The next blow that would put them down leaves them barely standing instead, keeping their place on the track, and washes off everything they were carrying. Whoever struck them collects nothing.",
            energyCost: 6, cooldownTurns: 4, range: 5,
            // Opts in to self-cast (§10, 2026-09-17): defensive toolkit — he
            // primes himself as readily as anyone else.
            allowsSelfTarget: true,
            effects: new[]
            {
                AbilityEffect.Status_(
                    EffectScope.PrimaryTarget, StatusKind.Defiance,
                    duration: CprTurns, EffectAudience.AllyOnly)
            });

        /// <remarks>Cast order, and id order — 501, 502, 503.</remarks>
        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { NaniteInfusion, TraumaPlate, Cpr };

        /// <summary>
        /// His uniform shape, for drafting. No aura, no passive — three
        /// abilities, at ranges 5, 4 and 5, all pointed at keeping somebody else
        /// alive.
        /// </summary>
        public static OperatorDefinition Definition { get; } = new OperatorDefinition(
            name: "Javi",
            maxHealth: MaxHealth,
            baseSpeed: Speed,
            abilities: All);
    }
}