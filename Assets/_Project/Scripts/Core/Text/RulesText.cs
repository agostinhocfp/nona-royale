// Assets/_Project/Scripts/Core/Text/RulesText.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Text
{
    /// <summary>
    /// Writes what an ability, a passive or an aura does, from its definition
    /// (OPERATOR_GUIDE.md D2, §2). Every number in the guide comes from here.
    /// </summary>
    /// <remarks>
    /// <b>Why generated.</b> <c>OPERATORS.md</c> once carried its own copies of
    /// the stats and they drifted until they were stripped out; the balance
    /// passes of 2026-09-20/21 alone changed eight roster numbers. A line read
    /// off the definition cannot drift, because there is nothing to keep in
    /// step.
    ///
    /// <b>It describes, it never decides.</b> Nothing here computes a rule's
    /// outcome — no damage is added up, no status is resolved. It reads the
    /// fields an effect declares and says them in words, so the resolver stays
    /// the only place the rules live (PRESENTATION §1).
    ///
    /// <b>Shape of a line.</b> An ability that can be aimed at either side is
    /// written as two modes, "Enemy: … · Ally: …", from the audiences its
    /// effects declare. Within a mode, consecutive effects that land on the
    /// same people share one clause ("enemies within 3 of you: 2 Tech, Slow
    /// 1 turn, pushed 2 cells away"), and identical consecutive effects are
    /// counted ("3 × 1 Atomic").
    ///
    /// <b>It refuses to guess.</b> An effect it cannot describe is written as
    /// <see cref="UnwrittenPrefix"/> plus the kind, and a test fails on it.
    /// </remarks>
    public static class RulesText
    {
        /// <summary>What an indescribable effect is written as. A test fails on any line containing it.</summary>
        public const string UnwrittenPrefix = "[unwritten: ";

        private const string ClauseGap = " · ";

        /// <summary>Between an ability's enemy and ally modes: a line of its own, so the two cannot be misread as one sequence.</summary>
        private const string ModeGap = "\n";
        private const string ItemGap = ", ";

        // ── Abilities ────────────────────────────────────────────────────

        /// <summary>The rules line for one ability.</summary>
        public static RulesLine For(AbilityDefinition ability)
        {
            if (ability == null) throw new ArgumentNullException(nameof(ability));

            var effects = ability.Effects;
            var line = new RulesLine();

            if (ability.Targeting != AbilityTargeting.Operator)
            {
                WriteMode(line, effects, e => true);
                return line;
            }

            bool enemyMode = TargetsSide(effects, EffectAudience.EnemyOnly);
            bool allyMode = TargetsSide(effects, EffectAudience.AllyOnly);

            if (enemyMode && allyMode)
            {
                var enemy = new RulesLine();
                WriteMode(enemy, effects, e => e.Audience != EffectAudience.AllyOnly);
                var ally = new RulesLine();
                WriteMode(ally, effects, e => e.Audience != EffectAudience.EnemyOnly);

                // A swap is the same thing either way; saying it twice would
                // suggest the two modes differ.
                if (enemy.ToPlainText() == ally.ToPlainText()) return line.Append(enemy);

                line.Keyword("Enemy", Keywords.Mode).Text(": ").Append(enemy);
                line.Text(ModeGap);
                line.Keyword(ability.AllowsSelfTarget ? "Ally or self" : "Ally", Keywords.Mode).Text(": ").Append(ally);
                return line;
            }

            if (allyMode)
            {
                line.Keyword(ability.AllowsSelfTarget ? "Ally or self" : "Ally", Keywords.Mode).Text(": ");
                WriteMode(line, effects, e => e.Audience != EffectAudience.EnemyOnly);
                return line;
            }

            WriteMode(line, effects, e => e.Audience != EffectAudience.AllyOnly);
            return line;
        }

        /// <summary>
        /// Whether an aimed cast can pick this side at all: some effect that
        /// lands on or around the target accepts it.
        /// </summary>
        private static bool TargetsSide(IReadOnlyList<AbilityEffect> effects, EffectAudience side)
        {
            foreach (var effect in effects)
            {
                if (!IsTargetAnchored(effect.Scope)) continue;
                if (effect.Audience == side || effect.Audience == EffectAudience.Any) return true;
            }
            return false;
        }

        private static bool IsTargetAnchored(EffectScope scope) =>
            scope == EffectScope.PrimaryTarget ||
            scope == EffectScope.EnemiesAroundPrimaryTarget ||
            scope == EffectScope.EnemiesAroundPrimaryTargetInclusive ||
            scope == EffectScope.AlliesAroundPrimaryTarget;

        /// <summary>One cast mode: the surviving effects, grouped into clauses by who they land on.</summary>
        private static void WriteMode(RulesLine line, IReadOnlyList<AbilityEffect> effects, Func<AbilityEffect, bool> survives)
        {
            var kept = new List<AbilityEffect>();
            foreach (var effect in effects)
                if (survives(effect)) kept.Add(effect);

            bool firstClause = true;
            int i = 0;

            while (i < kept.Count)
            {
                var head = kept[i];
                var group = new List<(AbilityEffect effect, int count)>();

                // One clause: consecutive effects on the same people. Identical
                // consecutive effects are counted rather than repeated.
                // The head always opens its own clause, even one that stands alone.
                while (i < kept.Count && (group.Count == 0 || SameRecipients(kept[i], head)))
                {
                    int count = 1;
                    while (i + count < kept.Count && Identical(kept[i], kept[i + count])) count++;
                    group.Add((kept[i], count));
                    i += count;
                }

                if (!firstClause) line.Text(ClauseGap);
                firstClause = false;

                WriteSubject(line, head);

                for (int g = 0; g < group.Count; g++)
                {
                    if (g > 0) line.Text(ItemGap);
                    WriteItem(line, group[g].effect, group[g].count);
                }
            }
        }

        private static bool SameRecipients(AbilityEffect a, AbilityEffect b)
        {
            if (a.Scope != b.Scope) return false;

            // Anything that moves the caster, or happens later, is its own
            // sentence: "jump to the target" and "at your next upkeep" do not
            // read as items in a list of what the target takes.
            if (StandsAlone(a.Kind) || StandsAlone(b.Kind)) return false;

            // The caster and the primary target are one operator each; only an
            // area needs its radius to match.
            if (a.Scope == EffectScope.PrimaryTarget || a.Scope == EffectScope.Caster) return true;
            return a.Radius == b.Radius;
        }

        private static bool StandsAlone(EffectKind kind) =>
            kind == EffectKind.DashToTarget || kind == EffectKind.FollowUp || kind == EffectKind.AttachCharge ||
            kind == EffectKind.PaintCell || kind == EffectKind.DeployZone || kind == EffectKind.ProjectField ||
            kind == EffectKind.Watch || kind == EffectKind.SetTable || kind == EffectKind.Execute;

        private static bool Identical(AbilityEffect a, AbilityEffect b) =>
            a.Kind == b.Kind && a.Scope == b.Scope && a.Audience == b.Audience &&
            a.Amount == b.Amount && a.DamageType == b.DamageType && a.Radius == b.Radius &&
            a.Status == b.Status && a.Duration == b.Duration && a.Stacks == b.Stacks &&
            a.Magnitude.Equals(b.Magnitude) && a.BonusIfBleeding == b.BonusIfBleeding &&
            a.BonusInOwnZone == b.BonusInOwnZone && a.CritChance.Equals(b.CritChance) &&
            a.CritMultiplier == b.CritMultiplier && a.HeavyCritMultiplier == b.HeavyCritMultiplier &&
            a.HeavyAboveMaxHealth == b.HeavyAboveMaxHealth && a.HeavyBonus == b.HeavyBonus &&
            a.Lifesteal == b.Lifesteal && a.MinimumDamage == b.MinimumDamage;

        /// <summary>Who a clause lands on, when it is not simply the target.</summary>
        private static void WriteSubject(RulesLine line, AbilityEffect effect)
        {
            switch (effect.Scope)
            {
                case EffectScope.EnemiesAroundCaster:
                    line.Text("enemies within ").Number(effect.Radius).Text(" of you: ");
                    break;
                case EffectScope.EnemiesAroundPrimaryTarget:
                    line.Text("enemies within ").Number(effect.Radius).Text(" of the target: ");
                    break;
                case EffectScope.EnemiesAroundPrimaryTargetInclusive:
                    line.Text("the target and enemies within ").Number(effect.Radius).Text(" of it: ");
                    break;
                case EffectScope.AlliesAroundPrimaryTarget:
                    line.Text("your allies within ").Number(effect.Radius).Text(" of the target: ");
                    break;
                case EffectScope.EnemiesInLineFromCaster:
                    line.Text("enemies on the next ").Number(effect.Radius).Text(" cells ahead of you: ");
                    break;
            }
        }

        private static bool OnCaster(AbilityEffect effect) => effect.Scope == EffectScope.Caster;

        // ── Items, one per effect kind ───────────────────────────────────

        private static void WriteItem(RulesLine line, AbilityEffect e, int count)
        {
            switch (e.Kind)
            {
                case EffectKind.Damage: Damage(line, e, count); return;
                case EffectKind.Heal: Heal(line, e); return;
                case EffectKind.ApplyStatus: Status(line, e); return;

                case EffectKind.PullToCaster:
                    line.Keyword("pulled", Keywords.Placement).Text(" to the cell next to you");
                    return;

                case EffectKind.PushFromCaster:
                    line.Keyword("pushed", Keywords.Placement).Text(" ").Number(e.Amount)
                        .Text(e.Amount == 1 ? " cell away from you" : " cells away from you");
                    return;

                case EffectKind.SwapWithCaster:
                    line.Keyword("swap places", Keywords.Placement).Text(" with the target");
                    return;

                case EffectKind.RemoveStatuses:
                    line.Keyword("cleanse", Keywords.Cleanse).Text(": every status is removed");
                    return;

                case EffectKind.Execute: Execute(line, e); return;
                case EffectKind.PaintCell: Beacon(line, e); return;
                case EffectKind.DeployZone: Zone(line, e); return;
                case EffectKind.AttachCharge: Charge(line, e); return;
                case EffectKind.DashToTarget: Dash(line, e); return;
                case EffectKind.FollowUp: FollowUp(line, e); return;
                case EffectKind.ProjectField: Field(line, e); return;

                case EffectKind.Watch:
                    line.Text("if it moves by dice before your next ").Keyword("upkeep", Keywords.Upkeep).Text(": ");
                    Hit(line, e.Amount, e.DamageType);
                    return;

                case EffectKind.IncurDebt:
                    line.Text("its seat takes on ").Number(e.Amount).Text(" ").Keyword("debt", Keywords.Debt);
                    return;

                case EffectKind.DebtDamage: DebtDamage(line, e); return;
                case EffectKind.DealDice: Dice(line, e); return;
                case EffectKind.SetTable: Table(line, e); return;
            }

            line.Text(UnwrittenPrefix + e.Kind + "]");
        }

        /// <summary>"3 Normal": the figure and its type, the type a keyword.</summary>
        private static void Hit(RulesLine line, int amount, DamageType type)
        {
            line.Number(amount).Text(" ").Keyword(type.ToString(), Keywords.Damage(type));
        }

        private static void Damage(RulesLine line, AbilityEffect e, int count)
        {
            if (OnCaster(e)) line.Text("you take ");
            if (count > 1) line.Number(count).Text(" × ");

            Hit(line, e.Amount, e.DamageType);

            if (e.BonusIfBleeding > 0)
                line.Text(" (+").Number(e.BonusIfBleeding).Text(" if it is ").Keyword("bleeding", Keywords.Status(StatusKind.Bleed)).Text(")");

            if (e.HeavyBonus > 0)
            {
                line.Text(", +").Number(e.HeavyBonus).Text(" against ");
                Heavy(line, e.HeavyAboveMaxHealth);
            }

            if (e.CritChance > 0.0)
            {
                line.Text(count > 1 ? "; each blow can " : "; it can ")
                    .Keyword("crit", Keywords.Critical).Text(": ").Number(Percent(e.CritChance))
                    .Text(" for ×").Number(e.CritMultiplier);

                if (e.HeavyCritMultiplier != e.CritMultiplier && e.HeavyAboveMaxHealth > 0)
                {
                    line.Text(", ×").Number(e.HeavyCritMultiplier).Text(" against ");
                    Heavy(line, e.HeavyAboveMaxHealth);
                }
            }

            if (e.Lifesteal)
                line.Text("; you ").Keyword("heal what each blow removes", Keywords.Lifesteal);
        }

        /// <summary>"heavy (max health above 7)".</summary>
        private static void Heavy(RulesLine line, int above)
        {
            line.Keyword("heavy", Keywords.Heavy).Text(" targets (max health above ").Number(above).Text(")");
        }

        private static void Heal(RulesLine line, AbilityEffect e)
        {
            line.Text(OnCaster(e) ? "you heal " : "heal ").Number(e.Amount);

            if (e.BonusInOwnZone > 0)
                line.Text(" (+").Number(e.BonusInOwnZone).Text(" while one of your zones is live)");
        }

        private static void Status(RulesLine line, AbilityEffect e)
        {
            if (OnCaster(e)) line.Text("you gain ");

            line.Keyword(Glossary.TitleOf(e.Status), Keywords.Status(e.Status));

            switch (e.Status)
            {
                // Bleed is spent at the holder's next upkeep, so its duration
                // says nothing; its stacks are the figure.
                case StatusKind.Bleed:
                    if (e.Stacks > 1) line.Text(" ×").Number(e.Stacks);
                    return;

                case StatusKind.Shield:
                    line.Text(" ").Number(Whole(e.Magnitude)).Text(", ");
                    Turns(line, e.Duration);
                    return;

                default:
                    line.Text(" ");
                    Turns(line, e.Duration);
                    return;
            }
        }

        private static void Turns(RulesLine line, int turns)
        {
            line.Number(turns).Text(turns == 1 ? " turn" : " turns");
        }

        private static void Execute(RulesLine line, AbilityEffect e)
        {
            line.Keyword("neutralized outright", Keywords.Execute).Text(" if it is below ");
            Fraction(line, e.ExecuteNumerator, e.ExecuteDenominator);
            line.Text(" of its max health when you cast; otherwise ");
            Hit(line, e.Amount, e.DamageType);
        }

        private static void Fraction(RulesLine line, int numerator, int denominator)
        {
            if (numerator == 1 && denominator == 2) { line.Text("half"); return; }
            line.Number(numerator.ToString(CultureInfo.InvariantCulture) + "/" + denominator.ToString(CultureInfo.InvariantCulture));
        }

        private static void Beacon(RulesLine line, AbilityEffect e)
        {
            line.Text("a beacon on the cell: at your next ").Keyword("upkeep", Keywords.Upkeep).Text(", ");
            Hit(line, e.Amount, e.DamageType);
            line.Text(" split among enemies within ").Number(e.Radius).Text(" of it");
        }

        private static void Zone(RulesLine line, AbilityEffect e)
        {
            int ticks = e.Stacks;

            if (e.ScalesWithCrowd)
            {
                // Eris' Exploit: the figure is per other victim, per tick.
                line.Text("a zone of radius ").Number(e.Radius).Text(": ");
                line.Text(e.StrikesOnCast ? "now, and at your next " : "at your next ");
                if (ticks > 1) line.Number(ticks).Text(" ");
                line.Keyword(ticks > 1 ? "upkeeps" : "upkeep", Keywords.Upkeep)
                    .Text(", each enemy inside takes ");
                Hit(line, e.Amount, e.DamageType);
                line.Text(" for every other enemy inside");
                return;
            }

            // Killzone: a detonation with a status, then lingering ticks.
            line.Text("a zone of radius ").Number(e.Radius).Text(": at your next ")
                .Keyword("upkeep", Keywords.Upkeep).Text(" it goes off for ");
            Hit(line, e.Amount, e.DamageType);

            if (e.CarriesStatus)
            {
                line.Text(" and ").Keyword(Glossary.TitleOf(e.Status), Keywords.Status(e.Status)).Text(" ");
                Turns(line, e.Duration);
            }

            if (ticks > 0 && e.Magnitude > 0.0)
            {
                line.Text(", then ");
                Hit(line, Whole(e.Magnitude), e.DamageType);
                line.Text(" at each of your next ").Number(ticks)
                    .Text(ticks == 1 ? " upkeep" : " upkeeps").Text(" to enemies still inside");
            }
        }

        private static void Charge(RulesLine line, AbilityEffect e)
        {
            line.Text("a charge rides the target and goes off at your next ").Keyword("upkeep", Keywords.Upkeep)
                .Text(": ");
            Hit(line, e.Amount, e.DamageType);
            line.Text(" to enemies within ").Number(e.Radius).Text(" of it");
            if (e.Stacks > 0) line.Text(", +").Number(e.Stacks).Text(" to the target");

            if (e.CarriesStatus)
            {
                line.Text(", and ").Keyword(Glossary.TitleOf(e.Status), Keywords.Status(e.Status)).Text(" ");
                Turns(line, e.Duration);
                line.Text(" to all of them");
            }
        }

        private static void Dash(RulesLine line, AbilityEffect e)
        {
            if (e.Amount > 0)
            {
                line.Keyword("dash", Keywords.Placement).Text(" to the target, ");
                Hit(line, e.Amount, e.DamageType);
                line.Text(" to every enemy you pass, and land one cell past it");
                return;
            }

            line.Keyword("jump", Keywords.Placement).Text(" to the target and land one cell past it");
        }

        private static void FollowUp(RulesLine line, AbilityEffect e)
        {
            line.Text("at your next ").Keyword("upkeep", Keywords.Upkeep).Text(", if you are within ")
                .Number(e.Radius).Text(" of it: ");
            Hit(line, e.Amount, e.DamageType);

            if (e.HeavyBonus > 0)
            {
                line.Text(", +").Number(e.HeavyBonus).Text(" against ");
                Heavy(line, e.HeavyAboveMaxHealth);
            }
        }

        private static void Field(RulesLine line, AbilityEffect e)
        {
            // Self-applied on the cast turn, the marker counts that turn as its
            // first, so a field that ticks twice lasts three (§6.6).
            int ticks = Math.Max(1, e.Duration - 1);

            line.Text("a field around you: at each of your next ").Number(ticks).Text(" ")
                .Keyword(ticks == 1 ? "upkeep" : "upkeeps", Keywords.Upkeep).Text(", ");
            Hit(line, e.Amount, e.DamageType);
            line.Text(" to enemies within ").Number(e.Radius).Text(" of wherever you stand");
        }

        private static void DebtDamage(RulesLine line, AbilityEffect e)
        {
            line.Text("its seat's ").Keyword("debt", Keywords.Debt).Text(" as ")
                .Keyword(e.DamageType.ToString(), Keywords.Damage(e.DamageType)).Text(" damage");

            if (e.MinimumDamage > 0) line.Text(", at least ").Number(e.MinimumDamage);

            if (e.Radius > 0)
            {
                line.Text("; ");
                if (e.Stacks == 2) line.Text("half");
                else line.Text("1/").Number(e.Stacks);
                line.Text(" of that to enemies within ").Number(e.Radius).Text(" of it");
            }

            line.Text("; the debt is cleared");
        }

        private static void Dice(RulesLine line, AbilityEffect e)
        {
            if (e.Stacks == 0)
            {
                if (e.Amount == 1) line.Text("re-roll your lowest unspent die");
                else line.Text("re-roll your ").Number(e.Amount).Text(" lowest unspent dice");
                return;
            }

            if (e.Amount == 2) line.Text("both unspent dice become ");
            else line.Number(e.Amount).Text(" unspent dice become ");
            line.Number(e.Stacks);

            if (e.Amount >= 2) line.Text(", and the double still earns its extra roll");
        }

        private static void Table(RulesLine line, AbilityEffect e)
        {
            line.Text("a table on the cell for ").Number(e.Stacks).Text(" of your turns")
                .Text(": the first enemy dice move to cross it stops there and takes ");
            Hit(line, e.Amount, e.DamageType);
            line.Text(", once per operator");
        }

        // ── Passives and auras ───────────────────────────────────────────

        /// <summary>
        /// Everything an operator carries that is not an ability — its passives,
        /// then its aura — in the order every screen lists them.
        /// </summary>
        /// <remarks>
        /// The dossier, the draft card and the match's operator card all read
        /// this, so a passive cannot be shown on one screen and missing from
        /// another (2026-09-21: the draft card dropped all but one, the match
        /// showed none).
        /// </remarks>
        public static IReadOnlyList<KitTrait> Traits(
            OperatorDefinition op, CombatConfig combat = null, EnergyConfig energy = null)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            var list = new List<KitTrait>(Passives(op, combat, energy));

            if (op.Aura != null)
                list.Add(new KitTrait(op.Aura.Name, TraitKind.Aura, ForAura(op.Aura), op.Aura.Description,
                    radius: op.Aura.Radius));

            return list;
        }

        /// <summary>
        /// An operator's passives: a name, a line and the flavour each. A named
        /// passive that carries two statuses (Kurbyn's Evasive Protocol) is one
        /// entry with both halves.
        /// </summary>
        /// <remarks>
        /// An unnamed passive is titled by its status's glossary entry
        /// (Lethe's Hastened, Sanity's Burdened). The definition's one
        /// description belongs to the first entry; an unnamed second passive
        /// has none, and the roster has no such operator.
        /// </remarks>
        public static IReadOnlyList<KitTrait> Passives(
            OperatorDefinition op, CombatConfig combat = null, EnergyConfig energy = null)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));
            combat = combat ?? CombatConfig.Default;
            energy = energy ?? EnergyConfig.Default;

            var list = new List<KitTrait>();
            if (!op.Passive.HasValue) return list;

            var first = ForPassive(op.Passive.Value, op, combat, energy);

            if (op.Passive2.HasValue && op.PassiveName != null)
            {
                first.Text(ClauseGap).Append(ForPassive(op.Passive2.Value, op, combat, energy));
                list.Add(new KitTrait(op.PassiveName, TraitKind.Passive, first, op.PassiveDescription, op.Passive.Value));
                return list;
            }

            list.Add(new KitTrait(op.PassiveName ?? Glossary.TitleOf(op.Passive.Value), TraitKind.Passive, first,
                op.PassiveDescription, op.Passive.Value));

            if (op.Passive2.HasValue)
                list.Add(new KitTrait(Glossary.TitleOf(op.Passive2.Value), TraitKind.Passive,
                    ForPassive(op.Passive2.Value, op, combat, energy), null, op.Passive2.Value));

            return list;
        }

        /// <summary>A permanent status, as this operator carries it.</summary>
        public static RulesLine ForPassive(StatusKind kind, OperatorDefinition op, CombatConfig combat, EnergyConfig energy)
        {
            var line = new RulesLine();

            switch (kind)
            {
                case StatusKind.Evasion:
                    line.Text("the first ").Keyword("Normal", Keywords.Damage(DamageType.Normal)).Text(" or ")
                        .Keyword("Tech", Keywords.Damage(DamageType.Tech)).Text(" hit each round misses ")
                        .Number(Percent(combat.EvasionChance)).Text(" of the time");
                    return line;

                case StatusKind.Hastened:
                    line.Text("always ").Keyword("Hastened", Keywords.Status(StatusKind.Hastened)).Text(": ");
                    Cells(line, "+", combat.HasteCellsAtOrBelowThreshold, combat.HasteCellsAboveThreshold, combat.HasteRollThreshold);
                    line.Text("; at most ").Number(op?.HasteCellCap ?? combat.HasteBonusCellCap).Text(" extra a turn");
                    return line;

                case StatusKind.Burdened:
                    line.Text("always ").Keyword("Burdened", Keywords.Status(StatusKind.Burdened)).Text(": ");
                    Cells(line, "−", combat.BurdenCellsAtOrBelowThreshold, combat.BurdenCellsAboveThreshold, combat.HasteRollThreshold);
                    line.Text("; never below ").Number(1).Text(" cell");
                    return line;

                case StatusKind.Equilibrium:
                    line.Text("a cast's instant damage to you is doubled at cost ").Number(combat.EquilibriumCheapCostMax)
                        .Text(" or less, and halved (at least ").Number(1).Text(") at cost ")
                        .Number(combat.EquilibriumDearCostMin).Text(" or more");
                    return line;

                case StatusKind.HouseEdge:
                    line.Text("once a turn, cash an unspent die you could have moved for ")
                        .Number(energy.CashedDieEnergy).Text(" ").Keyword("energy", Keywords.Energy);
                    return line;
            }

            return line.Text(UnwrittenPrefix + kind + "]");
        }

        /// <summary>"+1 cell on a roll of 6 or less, +2 above, once per roll".</summary>
        private static void Cells(RulesLine line, string sign, int low, int high, int threshold)
        {
            line.Number(sign + low).Text(low == 1 ? " cell" : " cells").Text(" on a roll of ").Number(threshold)
                .Text(" or less, ").Number(sign + high).Text(" above, once per roll");
        }

        /// <summary>What an aura does, to whom, and how far.</summary>
        public static RulesLine ForAura(AuraDefinition aura)
        {
            if (aura == null) throw new ArgumentNullException(nameof(aura));

            var line = new RulesLine();
            string who = aura.Side == AuraSide.Allies ? "allies" : "enemies";

            if (aura.GrantsHaste)
            {
                line.Text(who + " within ").Number(aura.Radius).Text(" of you count as ")
                    .Keyword("Hastened", Keywords.Status(StatusKind.Hastened)).Text(" for any move they start there");
                return line;
            }

            if (aura.SpeedModifier < 0.0)
            {
                line.Text(who + " within ").Number(aura.Radius).Text(" of you are ")
                    .Keyword("slowed", Keywords.Status(StatusKind.Slow)).Text(": speed ")
                    .Number(Signed(aura.SpeedModifier));
                return line;
            }

            if (aura.SpeedModifier > 0.0)
            {
                line.Text(who + " within ").Number(aura.Radius).Text(" of you move faster: speed ")
                    .Number(Signed(aura.SpeedModifier));
                return line;
            }

            return line.Text(UnwrittenPrefix + "aura " + aura.Name + "]");
        }

        // ── Numbers ──────────────────────────────────────────────────────

        public static string Percent(double fraction) =>
            Math.Round(fraction * 100.0).ToString(CultureInfo.InvariantCulture) + "%";

        public static string Signed(double value) =>
            (value < 0 ? "−" : "+") + Math.Abs(value).ToString("0.0##", CultureInfo.InvariantCulture);

        internal static int Whole(double value) => (int)Math.Round(value);
    }
}
