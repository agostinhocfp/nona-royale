// Assets/_Project/Scripts/Core/Abilities/Roster/Fortuna.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Operator #12 — Fortuna, the dealer. Stats and kit, transcribed from
    /// <c>COMBAT_SYSTEMS.md</c> §10.12 (added 2026-09-18), designed in
    /// <c>OPERATOR_12.md</c>.
    /// </summary>
    /// <remarks>
    /// <b>Eleven operators fight over the board; she owns the dice.</b> Her kit
    /// never reads health except in passing: it converts a die into money (§3.4),
    /// re-deals a die she does not like, sets a cell nobody runs past (§7.7), and
    /// once a match deals the house a double six (§6.8).
    ///
    /// <b>Her numbers are small on purpose.</b> Two damage on the whole kit. What
    /// she sells is tempo and funding, and the race polices the passive by itself:
    /// a seat needs very nearly every pip it rolls to get three operators home, so
    /// cashing is a loss she takes only when the board makes that movement
    /// worthless or dangerous.
    ///
    /// <b>She cannot defend herself.</b> Seven health, 1.0×, no shield, no ward,
    /// no escape, no cleanse — the only operator with three abilities and no answer
    /// to anybody walking up to her. Diving her is correct, and Revú's drain takes
    /// exactly what she manufactures.
    ///
    /// Ids run 1201–1203, with the House Edge as the passive that fills no slot.
    /// Every number is the designer's, unmeasured when written; adding a twelfth
    /// operator shifts the draft's dice stream again.
    /// </remarks>
    public static class Fortuna
    {
        /// <summary>Seven: the roster's common figure since the 2026-09-16 health pass.</summary>
        public const int MaxHealth = 7;

        /// <summary>
        /// One. <b>Do not raise it.</b> At 1.5× she banks money and races, and the
        /// passive's whole cost is the tempo she gives up (§10.12).
        /// </summary>
        public const double Speed = 1.0;

        /// <summary>Dice Deal Again re-deals. One: the lowest in hand (§6.8).</summary>
        public const int DealAgainDice = 1;

        /// <summary>Dice Boxcars sets, and the face it sets them to.</summary>
        public const int BoxcarsDice = 2;
        public const int BoxcarsFace = 6;

        /// <summary>What a mover stopped by the table takes.</summary>
        public const int TableDamage = 2;

        /// <summary>
        /// Turns of hers the table stands for, counting the turn it is dealt. Two,
        /// so it is on the board for a full round of everyone else's movement.
        /// </summary>
        public const int TableLifetimeTurns = 2;

        /// <summary>
        /// The worst die on the table, re-dealt.
        /// </summary>
        /// <remarks>
        /// <b>Three energy for an expected two and a half pips is a bad trade, and
        /// that is the point:</b> it is cast for the specific, not the average. A
        /// six deploys an operator out of the yard — half a lap of lost progress
        /// bought back for three energy, the best outcome any 3-cost ability on the
        /// roster can produce — and an exact number lands a collision that costs no
        /// energy at all.
        ///
        /// <b>Priced beside Leech Round and Short Circuit</b> (3, cooldowns 1 and
        /// 2). Cooldown 1, because §3.1's rule is that an ability meant to be
        /// castable every turn has to cost 3 or less and carry a cooldown that does
        /// not bind.
        ///
        /// <b>It re-deals the lowest die</b>, by ruling rather than by choice
        /// (§6.8), and it never creates or destroys the doubles roll — that is
        /// decided when the dice leave the cup.
        /// </remarks>
        public static AbilityDefinition DealAgain { get; } = new AbilityDefinition(
            id: 1201, name: "Deal Again",
            description:
                "She sweeps the worst die off the table and throws it again. What comes up is the house's business.",
            energyCost: 3, cooldownTurns: 1, range: 0,
            effects: new[]
            {
                AbilityEffect.DealDice(DealAgainDice)
            },
            targeting: AbilityTargeting.None);

        /// <summary>
        /// A table on a cell: the first enemy run that crosses it stops there and
        /// pays for the privilege.
        /// </summary>
        /// <remarks>
        /// <b>The first ability that reads the cells a move passes through</b>
        /// rather than the cell it lands on (§7.7, ADR-0007 Amendment 3), and the
        /// roster's first structural counter to speed: the faster an operator
        /// moves, the more cells it crosses, and the likelier one of them is hers.
        /// Every other answer to speed is a slow, and slows have a floor.
        ///
        /// <b>Four answers, all of which cost something.</b> Route the die through
        /// another operator; split the roll and stop short, which costs a cell
        /// whenever both dice are odd and doubles the landings; bypass with a push,
        /// pull, swap or dash, none of which is a dice move (§7.4); or walk into it
        /// and stop where she chose, usually beside something of hers.
        ///
        /// <b>It makes the finish contestable without touching the finish.</b> A
        /// table one cell short of a home column's mouth does not reach into the
        /// column (§4.3) — it stops the leader on the approach.
        ///
        /// <b>Once per enemy operator</b>, so it can never hold anybody in place
        /// twice. Priced against Eris' Exploit (6, cooldown 4) and under Killzone
        /// (9, cooldown 6); two damage because the theft of pips is the ability and
        /// the damage is the receipt.
        /// </remarks>
        public static AbilityDefinition TheTable { get; } = new AbilityDefinition(
            id: 1202, name: "The Table",
            description:
                "She sets a game down on the board. Nobody walks past a game in progress, and nobody sits down for free.",
            energyCost: 6, cooldownTurns: 3, range: 4,
            effects: new[]
            {
                AbilityEffect.SetTable(TableDamage, TableLifetimeTurns)
            },
            targeting: AbilityTargeting.Cell);

        /// <summary>
        /// Both dice become sixes, and the double is owed a roll.
        /// </summary>
        /// <remarks>
        /// <b>The only ultimate on the roster that cannot kill anybody.</b> Twelve
        /// pips chosen rather than rolled, plus the roll a double is owed: about
        /// twelve pips of tempo over an average roll once the extra roll is
        /// counted — a fifth of the loop at 1.0×, nearly a third at 1.5× — or two
        /// deploys and a squad back from a wipe (§1.3).
        ///
        /// <b>Nine and cooldown 4 puts it beside the other ultimates</b>, and it is
        /// deliberately worth less than Miracle Pull, which deletes an operator and
        /// takes half a lap with it. She is never the reason somebody dies; she is
        /// the reason somebody arrives.
        ///
        /// <b>Both dice must be unspent</b>, so it is cast at the top of the action
        /// phase and never salvages a half-spent roll — the engine refuses it
        /// before payment (§6.8). The extra roll is granted only inside
        /// <c>GameConfig.MaxRollsPerTurn</c>; at the cap it sets the faces and
        /// nothing more, and the extra roll grants no energy (§3.1).
        /// </remarks>
        public static AbilityDefinition Boxcars { get; } = new AbilityDefinition(
            id: 1203, name: "Boxcars",
            description:
                "She takes the dice out of your hand, looks at them, and puts them back the way the house likes them.",
            energyCost: 9, cooldownTurns: 4, range: 0,
            effects: new[]
            {
                AbilityEffect.DealDice(BoxcarsDice, BoxcarsFace)
            },
            targeting: AbilityTargeting.None);

        public static IReadOnlyList<AbilityDefinition> All { get; } =
            new[] { DealAgain, TheTable, Boxcars };

        /// <summary>
        /// Three abilities and the House Edge, which is a capability rather than a
        /// slot: once a turn her seat may cash an unspent die she could have moved
        /// for two energy instead (§3.4, §5.18, §6.8).
        /// </summary>
        /// <remarks>
        /// <b>It is the third thing a die can be spent on.</b> §6 has said since
        /// the core was written that every die is consumed by a deploy or a move;
        /// she is the other answer, and the answer to the one rule in the game that
        /// can force a player into a mistake — compulsory movement (§6.1). It does
        /// not repeal that rule. It prices it.
        /// </remarks>
        public static OperatorDefinition Definition { get; } = new OperatorDefinition(
            name: "Fortuna",
            maxHealth: MaxHealth,
            baseSpeed: Speed,
            abilities: All,
            passive: StatusKind.HouseEdge,
            passiveName: "The House Edge");
    }
}
