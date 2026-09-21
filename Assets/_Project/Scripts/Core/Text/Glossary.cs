// Assets/_Project/Scripts/Core/Text/Glossary.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Text
{
    /// <summary>What a glossary entry is about, for grouping and colour.</summary>
    public enum GlossaryGroup { Status = 0, Damage = 1, Term = 2 }

    /// <summary>One keyword: its title and what it means (OPERATOR_GUIDE.md §3).</summary>
    public sealed class GlossaryEntry
    {
        public GlossaryEntry(string id, string title, GlossaryGroup group, RulesLine definition,
            StatusKind? status = null, DamageType? damage = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Group = group;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Status = status;
            Damage = damage;
        }

        public string Id { get; }
        public string Title { get; }
        public GlossaryGroup Group { get; }
        public RulesLine Definition { get; }

        /// <summary>Set for a status entry, so the view can colour it the way the board tags it.</summary>
        public StatusKind? Status { get; }

        /// <summary>Set for a damage-type entry.</summary>
        public DamageType? Damage { get; }
    }

    /// <summary>
    /// Every keyword a rules line can link to, with a definition built from the
    /// configs (OPERATOR_GUIDE.md §3).
    /// </summary>
    /// <remarks>
    /// <b>The same rule as the rules lines: no typed numbers.</b> Slow's
    /// penalty, the burden's cells, evasion's chance, bleed and mark per tick
    /// all come from <see cref="CombatConfig"/>; the cap from
    /// <see cref="EnergyConfig"/>; the deploy roll from
    /// <see cref="GameConfig"/>. The words around them are the only thing
    /// written by hand.
    ///
    /// <b>It answers two of the Stranger Test's debrief questions</b>
    /// (STRANGER_TEST.md §4): what a status tag on a piece means, and what a
    /// safe cell is. Neither needed anything added to the match HUD.
    /// </remarks>
    public static class Glossary
    {
        /// <summary>The display name of a status, as the guide writes it.</summary>
        public static string TitleOf(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.ZeroDayCharge: return "Zero-Day charge";
                case StatusKind.TechWard: return "Tech Ward";
                case StatusKind.CryoField: return "Cryo Field";
                case StatusKind.HouseEdge: return "House Edge";
                default: return kind.ToString();
            }
        }

        /// <summary>Every entry, statuses first, then damage types, then board terms.</summary>
        public static IReadOnlyList<GlossaryEntry> Entries(
            CombatConfig combat = null, EnergyConfig energy = null, GameConfig game = null)
        {
            combat = combat ?? CombatConfig.Default;
            energy = energy ?? EnergyConfig.Default;
            game = game ?? GameConfig.Default;

            var list = new List<GlossaryEntry>();

            foreach (StatusKind kind in Enum.GetValues(typeof(StatusKind)))
                list.Add(new GlossaryEntry(Keywords.Status(kind), TitleOf(kind), GlossaryGroup.Status,
                    StatusDefinition(kind, combat, energy), status: kind));

            foreach (DamageType type in Enum.GetValues(typeof(DamageType)))
                list.Add(new GlossaryEntry(Keywords.Damage(type), type + " damage", GlossaryGroup.Damage,
                    DamageDefinition(type), damage: type));

            Term(list, Keywords.SafeCell, "Safe cell", new RulesLine()
                .Text("The start cells and the first cell of each home column. No collisions happen there, enemies cannot pick you out with a single-target ability, and you take no damage while you stand on one."));
            Term(list, Keywords.SpawnCell, "Spawn cell", new RulesLine()
                .Text("Your own colour's start cell, where you deploy. It is safe, and on top of that you cannot be ")
                .Keyword("slowed", Keywords.Status(StatusKind.Slow)).Text(" or ")
                .Keyword("stunned", Keywords.Status(StatusKind.Stun)).Text(" while you stand on it."));
            Term(list, Keywords.Yard, "Yard", new RulesLine()
                .Text("Where operators wait before they deploy and after they are neutralized. A die showing ")
                .Number(game.DeployRequirement).Text(" deploys one onto your spawn cell."));
            Term(list, Keywords.Upkeep, "Upkeep", new RulesLine()
                .Text("The start of a seat's turn, before it rolls. Bleed, marks, beacons, zones, charges, fields and follow-ups resolve here."));
            Term(list, Keywords.Energy, "Energy", new RulesLine()
                .Text("Your seat's shared pool: every ability is paid from it, and it holds at most ")
                .Number(energy.EnergyCap).Text(". It refills from your rolls, and a knockout pays a bounty of ")
                .Number(combat.NeutralizeEnergyBounty).Text("."));
            Term(list, Keywords.Critical, "Critical", new RulesLine()
                .Text("A hit that rolls a critical is multiplied. The ability says the chance and the multiplier."));
            Term(list, Keywords.Heavy, "Heavy", new RulesLine()
                .Text("An operator whose maximum health is above the line the ability names. Heavy targets take more from abilities that say so."));
            Term(list, Keywords.Lifesteal, "Lifesteal", new RulesLine()
                .Text("The caster heals the health the hit actually removed, never past its own maximum. Overkill heals nothing."));
            Term(list, Keywords.Execute, "Execute", new RulesLine()
                .Text("Neutralized outright: sent to the yard whatever its health, if it was below the ability's line when the cast began."));
            Term(list, Keywords.Cleanse, "Cleanse", new RulesLine()
                .Text("Removes every status from the target, helpful ones included. Passives stay."));
            Term(list, Keywords.Placement, "Placement", new RulesLine()
                .Text("Moved by an ability rather than by dice. It never collides and never triggers anything on the cells it passes."));
            Term(list, Keywords.Mode, "Enemy and ally casts", new RulesLine()
                .Text("An ability that can be aimed at either side does a different thing to each. The line says which part lands on which."));

            return list;
        }

        /// <summary>One entry by id, or null.</summary>
        public static GlossaryEntry Find(string id, CombatConfig combat = null, EnergyConfig energy = null, GameConfig game = null)
        {
            foreach (var entry in Entries(combat, energy, game))
                if (entry.Id == id) return entry;
            return null;
        }

        private static void Term(List<GlossaryEntry> list, string id, string title, RulesLine definition) =>
            list.Add(new GlossaryEntry(id, title, GlossaryGroup.Term, definition));

        private static RulesLine DamageDefinition(DamageType type)
        {
            var line = new RulesLine();
            switch (type)
            {
                case DamageType.Normal:
                    return line.Text("Stopped by ").Keyword("Evasion", Keywords.Status(StatusKind.Evasion))
                        .Text(" and ").Keyword("Shields", Keywords.Status(StatusKind.Shield)).Text(".");
                case DamageType.Tech:
                    return line.Text("Normal damage from a device, and a ")
                        .Keyword("Tech Ward", Keywords.Status(StatusKind.TechWard)).Text(" blocks it outright.");
                case DamageType.Atomic:
                    return line.Text("Ignores evasion, shields and wards. Nothing but a safe cell stops it.");
                default:
                    return line.Text(RulesText.UnwrittenPrefix + type + "]");
            }
        }

        private static RulesLine StatusDefinition(StatusKind kind, CombatConfig combat, EnergyConfig energy)
        {
            var line = new RulesLine();

            switch (kind)
            {
                case StatusKind.Stun:
                    return line.Text("Cannot move or spend energy on its next turn. Passives keep working, and others can still move it.");

                case StatusKind.Slow:
                    return line.Text("Speed ").Number(RulesText.Signed(-combat.SlowSpeedPenalty))
                        .Text(": each die covers fewer cells. Slows do not stack; the strongest applies.");

                case StatusKind.Bleed:
                    return line.Text("Each stack deals ").Number(combat.BleedDamagePerStack).Text(" ")
                        .Keyword("Atomic", Keywords.Damage(DamageType.Atomic)).Text(" at the holder's next ")
                        .Keyword("upkeep", Keywords.Upkeep).Text(", then is spent. Stacks add up.");

                case StatusKind.Stealth:
                    return line.Text("Enemies cannot pick it out with a single-target ability. Areas still find it.");

                case StatusKind.Evasion:
                    return line.Text("The first Normal or Tech hit each round misses ").Number(RulesText.Percent(combat.EvasionChance))
                        .Text(" of the time. Atomic never misses.");

                case StatusKind.Shield:
                    return line.Text("Absorbs Normal and Tech damage until its pool is spent. ")
                        .Keyword("Atomic", Keywords.Damage(DamageType.Atomic)).Text(" goes straight through.");

                case StatusKind.Mark:
                    return line.Text("Takes ").Number(combat.MarkDamagePerTurn).Text(" ")
                        .Keyword("Atomic", Keywords.Damage(DamageType.Atomic)).Text(" at each of its ")
                        .Keyword("upkeeps", Keywords.Upkeep).Text(" while the mark lasts.");

                case StatusKind.Hastened:
                    return line.Text("Extra cells: ").Number("+" + combat.HasteCellsAtOrBelowThreshold)
                        .Text(" on a roll of ").Number(combat.HasteRollThreshold).Text(" or less, ")
                        .Number("+" + combat.HasteCellsAboveThreshold).Text(" above, once per roll, at most ")
                        .Number(combat.HasteBonusCellCap).Text(" a turn.");

                case StatusKind.ZeroDayCharge:
                    return line.Text("A charge rides this operator and goes off at its setter's next upkeep. A cleanse removes it.");

                case StatusKind.TechWard:
                    return line.Text("Blocks ").Keyword("Tech", Keywords.Damage(DamageType.Tech)).Text(" damage outright.");

                case StatusKind.Hunted:
                    return line.Text("A follow-up strike is coming at its setter's next upkeep, if the setter is still close. Run, or be cleansed.");

                case StatusKind.CryoField:
                    return line.Text("This operator is projecting a field that hits enemies near it at each of its upkeeps.");

                case StatusKind.Watched:
                    return line.Text("Moving by dice before the watcher's next upkeep springs a strike. Standing still is the other answer.");

                case StatusKind.Burdened:
                    return line.Text("Fewer cells: ").Number("−" + combat.BurdenCellsAtOrBelowThreshold)
                        .Text(" on a roll of ").Number(combat.HasteRollThreshold).Text(" or less, ")
                        .Number("−" + combat.BurdenCellsAboveThreshold).Text(" above, once per roll, never below ")
                        .Number(1).Text(" cell. It cancels ").Keyword("Hastened", Keywords.Status(StatusKind.Hastened))
                        .Text(" cell for cell.");

                case StatusKind.Equilibrium:
                    return line.Text("A cast's instant damage to the holder is doubled at cost ").Number(combat.EquilibriumCheapCostMax)
                        .Text(" or less, and halved (at least ").Number(1).Text(") at cost ")
                        .Number(combat.EquilibriumDearCostMin).Text(" or more.");

                case StatusKind.HouseEdge:
                    return line.Text("Once a turn, an unspent die the operator could have moved can be cashed for ")
                        .Number(energy.CashedDieEnergy).Text(" ").Keyword("energy", Keywords.Energy).Text(".");
            }

            return line.Text(RulesText.UnwrittenPrefix + kind + "]");
        }
    }
}
