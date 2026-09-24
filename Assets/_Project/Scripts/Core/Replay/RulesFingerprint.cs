// Assets/_Project/Scripts/Core/Replay/RulesFingerprint.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Config;

namespace NonaRoyale.Core.Replay
{
    /// <summary>
    /// A short hash of every number the rules are made of: the three configs,
    /// the alpha measurement speeds and the whole roster.
    /// </summary>
    /// <remarks>
    /// <b>Why a hash and not a version number (REPLAY.md, decision 2).</b> A
    /// version is bumped by hand, and the change nobody remembers to bump for
    /// is exactly the one that breaks a replay quietly. The hash moves on its
    /// own, so a replay recorded before any rules change is refused with both
    /// hashes named instead of desyncing forty commands in.
    ///
    /// <b>Written out by hand, property by property.</b> Reflection would pick
    /// up a new dial on its own, but code stripping on a device build can drop
    /// a getter nothing else calls and give the phone a different hash than
    /// the editor. So the dump is explicit, and a test uses reflection to fail
    /// on any public property this file does not mention or deliberately
    /// leave out.
    ///
    /// <b>What is left out:</b> text (ability names and descriptions, passive
    /// and aura copy), which changes wording and not play, and properties
    /// computed from ones already written. The operator name stays in because
    /// it is how a file names a squad.
    ///
    /// <b>The roster is written in roster order, not sorted.</b> A random squad
    /// is drawn by index into <see cref="Roster.All"/>, so reordering the
    /// roster changes every random match; the hash has to see that.
    ///
    /// <b>Doubles are written twice</b>: readable, with the invariant culture so
    /// a Portuguese machine never writes <c>0,12</c>, and as their exact bits,
    /// so two values that print alike still hash apart.
    /// </remarks>
    public static class RulesFingerprint
    {
        private static string _current;

        /// <summary>The hash of this build's rules. Computed once.</summary>
        public static string Current =>
            _current ?? (_current = Of(
                GameConfig.Default, CombatConfig.Default, EnergyConfig.Default,
                Roster.All, RosterSpeeds.Default));

        public static string Of(
            GameConfig game,
            CombatConfig combat,
            EnergyConfig energy,
            IReadOnlyList<OperatorDefinition> roster,
            RosterSpeeds alphaSpeeds) =>
            Hash(Dump(game, combat, energy, roster, alphaSpeeds));

        /// <summary>The canonical text the hash is taken of, one <c>key=value</c> per line.</summary>
        public static string Dump(
            GameConfig game,
            CombatConfig combat,
            EnergyConfig energy,
            IReadOnlyList<OperatorDefinition> roster,
            RosterSpeeds alphaSpeeds)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (combat == null) throw new ArgumentNullException(nameof(combat));
            if (energy == null) throw new ArgumentNullException(nameof(energy));
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            if (alphaSpeeds == null) throw new ArgumentNullException(nameof(alphaSpeeds));

            var d = new Dumper();

            d.Line("GameConfig.DeployRequirement", game.DeployRequirement);
            d.Line("GameConfig.MaxRollsPerTurn", game.MaxRollsPerTurn);
            d.Line("GameConfig.MinSpeedMultiplier", game.MinSpeedMultiplier);
            d.Line("GameConfig.SpeedMultiplierMin", game.SpeedMultiplierMin);
            d.Line("GameConfig.SpeedMultiplierMax", game.SpeedMultiplierMax);
            d.Line("GameConfig.DiceSides", game.DiceSides);
            d.Line("GameConfig.DicePerRoll", game.DicePerRoll);
            d.Line("GameConfig.PityDeployAfterTurns", game.PityDeployAfterTurns);

            d.Line("CombatConfig.CollisionDamage", combat.CollisionDamage);
            d.Line("CombatConfig.EvasionChance", combat.EvasionChance);
            d.Line("CombatConfig.BleedDamagePerStack", combat.BleedDamagePerStack);
            d.Line("CombatConfig.SlowSpeedPenalty", combat.SlowSpeedPenalty);
            d.Line("CombatConfig.MarkDamagePerTurn", combat.MarkDamagePerTurn);
            d.Line("CombatConfig.HasteRollThreshold", combat.HasteRollThreshold);
            d.Line("CombatConfig.HasteCellsAtOrBelowThreshold", combat.HasteCellsAtOrBelowThreshold);
            d.Line("CombatConfig.HasteCellsAboveThreshold", combat.HasteCellsAboveThreshold);
            d.Line("CombatConfig.BurdenCellsAtOrBelowThreshold", combat.BurdenCellsAtOrBelowThreshold);
            d.Line("CombatConfig.BurdenCellsAboveThreshold", combat.BurdenCellsAboveThreshold);
            d.Line("CombatConfig.EquilibriumCheapCostMax", combat.EquilibriumCheapCostMax);
            d.Line("CombatConfig.EquilibriumDearCostMin", combat.EquilibriumDearCostMin);
            d.Line("CombatConfig.HasteDurationTurns", combat.HasteDurationTurns);
            d.Line("CombatConfig.HasteBonusCellCap", combat.HasteBonusCellCap);
            d.Line("CombatConfig.SpeedBonusCellCap", combat.SpeedBonusCellCap);
            d.Line("CombatConfig.NeutralizeEnergyBounty", combat.NeutralizeEnergyBounty);
            d.Line("CombatConfig.ShieldPoolDefault", combat.ShieldPoolDefault);
            d.Line("CombatConfig.RegenEveryTurns", combat.RegenEveryTurns);
            d.Line("CombatConfig.RegenAmount", combat.RegenAmount);

            d.Line("EnergyConfig.EnergyCap", energy.EnergyCap);
            d.Line("EnergyConfig.DiceDivisor", energy.DiceDivisor);
            d.Line("EnergyConfig.CashedDieEnergy", energy.CashedDieEnergy);
            d.Line("EnergyConfig.DebtCap", energy.DebtCap);
            d.Line("EnergyConfig.DebtInterest", energy.DebtInterest);

            d.Line("RosterSpeeds.Bouncer", alphaSpeeds.Bouncer);
            d.Line("RosterSpeeds.Syla", alphaSpeeds.Syla);
            d.Line("RosterSpeeds.KurbynBase", alphaSpeeds.KurbynBase);

            d.Line("Roster.SquadSize", Roster.SquadSize);
            d.Line("Roster.Count", roster.Count);

            for (int i = 0; i < roster.Count; i++)
                DumpOperator(d, $"op[{i}]", roster[i]);

            return d.ToString();
        }

        /// <summary>FNV-1a, 32 bits, over the UTF-8 bytes of <paramref name="text"/>, as 8 lowercase hex digits.</summary>
        public static string Hash(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;

            uint hash = offsetBasis;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash = unchecked(hash * prime);
            }

            return hash.ToString("x8", CultureInfo.InvariantCulture);
        }

        // ── Operators ────────────────────────────────────────────────────

        private static void DumpOperator(Dumper d, string at, OperatorDefinition op)
        {
            d.Line(at + ".Name", op.Name);
            d.Line(at + ".MaxHealth", op.MaxHealth);
            d.Line(at + ".BaseSpeed", op.BaseSpeed);
            d.Line(at + ".Passive", op.Passive.HasValue ? op.Passive.Value.ToString() : "none");
            d.Line(at + ".PassiveMagnitude", op.PassiveMagnitude);
            d.Line(at + ".Passive2", op.Passive2.HasValue ? op.Passive2.Value.ToString() : "none");
            d.Line(at + ".Passive2Magnitude", op.Passive2Magnitude);
            d.Line(at + ".HasteCellCap", op.HasteCellCap.HasValue ? op.HasteCellCap.Value.ToString(CultureInfo.InvariantCulture) : "none");

            if (op.Aura == null)
            {
                d.Line(at + ".Aura", "none");
            }
            else
            {
                d.Line(at + ".Aura.Radius", op.Aura.Radius);
                d.Line(at + ".Aura.Trail", op.Aura.Trail);
                d.Line(at + ".Aura.SpeedModifier", op.Aura.SpeedModifier);
                d.Line(at + ".Aura.Side", op.Aura.Side.ToString());
                d.Line(at + ".Aura.GrantsHaste", op.Aura.GrantsHaste);
            }

            // Abilities sorted by id: the order a kit is listed in is layout,
            // not rules, and lookups go through the id.
            var abilities = new List<AbilityDefinition>(op.Abilities);
            abilities.Sort((a, b) => a.Id.CompareTo(b.Id));

            d.Line(at + ".Abilities.Count", abilities.Count);
            foreach (var ability in abilities)
                DumpAbility(d, $"{at}.ability[{ability.Id}]", ability);
        }

        private static void DumpAbility(Dumper d, string at, AbilityDefinition ability)
        {
            d.Line(at + ".Id", ability.Id);
            d.Line(at + ".EnergyCost", ability.EnergyCost);
            d.Line(at + ".CooldownTurns", ability.CooldownTurns);
            d.Line(at + ".Range", ability.Range);
            d.Line(at + ".Targeting", ability.Targeting.ToString());
            d.Line(at + ".AllowsSelfTarget", ability.AllowsSelfTarget);

            // Effects keep their declared order: they resolve in it.
            d.Line(at + ".Effects.Count", ability.Effects.Count);
            for (int i = 0; i < ability.Effects.Count; i++)
                DumpEffect(d, $"{at}.effect[{i}]", ability.Effects[i]);
        }

        private static void DumpEffect(Dumper d, string at, AbilityEffect e)
        {
            d.Line(at + ".Kind", e.Kind.ToString());
            d.Line(at + ".Scope", e.Scope.ToString());
            d.Line(at + ".Audience", e.Audience.ToString());
            d.Line(at + ".Amount", e.Amount);
            d.Line(at + ".DamageType", e.DamageType.ToString());
            d.Line(at + ".Radius", e.Radius);
            d.Line(at + ".Status", e.Status.ToString());
            d.Line(at + ".Duration", e.Duration);
            d.Line(at + ".Stacks", e.Stacks);
            d.Line(at + ".Magnitude", e.Magnitude);
            d.Line(at + ".BonusIfBleeding", e.BonusIfBleeding);
            d.Line(at + ".BonusInOwnZone", e.BonusInOwnZone);
            d.Line(at + ".ExecuteNumerator", e.ExecuteNumerator);
            d.Line(at + ".ExecuteDenominator", e.ExecuteDenominator);
            d.Line(at + ".CritChance", e.CritChance);
            d.Line(at + ".CritMultiplier", e.CritMultiplier);
            d.Line(at + ".HeavyCritMultiplier", e.HeavyCritMultiplier);
            d.Line(at + ".HeavyAboveMaxHealth", e.HeavyAboveMaxHealth);
            d.Line(at + ".HeavyBonus", e.HeavyBonus);
            d.Line(at + ".ScalesWithCrowd", e.ScalesWithCrowd);
            d.Line(at + ".Lifesteal", e.Lifesteal);
            d.Line(at + ".StrikesOnCast", e.StrikesOnCast);
            d.Line(at + ".MinimumDamage", e.MinimumDamage);
        }

        // ── Formatting ───────────────────────────────────────────────────

        private sealed class Dumper
        {
            private readonly StringBuilder _text = new StringBuilder();

            public void Line(string key, string value) => _text.Append(key).Append('=').Append(value).Append('\n');

            public void Line(string key, int value) => Line(key, value.ToString(CultureInfo.InvariantCulture));

            public void Line(string key, bool value) => Line(key, value ? "true" : "false");

            public void Line(string key, double value) =>
                Line(key,
                    value.ToString("0.0#####", CultureInfo.InvariantCulture)
                    + " (0x"
                    + BitConverter.DoubleToInt64Bits(value).ToString("X16", CultureInfo.InvariantCulture)
                    + ")");

            public override string ToString() => _text.ToString();
        }
    }
}