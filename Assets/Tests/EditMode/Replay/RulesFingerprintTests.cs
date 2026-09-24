// Assets/Tests/EditMode/Replay/RulesFingerprintTests.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Replay;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Replay
{
    [TestFixture]
    public class RulesFingerprintTests
    {
        /// <summary>
        /// The hash of the rules as they stand. <b>When this test fails after a
        /// deliberate rules change, update it</b>, knowing that every replay
        /// recorded before the change will now be refused. When it fails after
        /// a change that was not meant to touch the rules, the change touched
        /// them.
        /// </summary>
        /// <remarks>
        /// It also pins the hash across runtimes: the same value must come out
        /// of Unity's Mono and of the .NET 8 harness, or a replay recorded in
        /// the editor could not be played back by a tool.
        /// </remarks>
        private const string Golden = "71ebce6e";

        private static string Dump() =>
            RulesFingerprint.Dump(
                GameConfig.Default, CombatConfig.Default, EnergyConfig.Default, Roster.All, RosterSpeeds.Default);

        [Test]
        public void Fingerprint_MatchesTheGoldenHash()
        {
            Assert.AreEqual(Golden, RulesFingerprint.Current,
                "The rules changed. If that was deliberate, set Golden to the new hash; " +
                "replays recorded before this change will be refused from now on.");
        }

        [Test]
        public void Fingerprint_IsStable_AndIsTheHashOfTheDump()
        {
            Assert.AreEqual(Dump(), Dump());
            Assert.AreEqual(RulesFingerprint.Hash(Dump()), RulesFingerprint.Current);
            Assert.AreEqual(8, RulesFingerprint.Current.Length);
            StringAssert.IsMatch("^[0-9a-f]{8}$", RulesFingerprint.Current);
        }

        [Test]
        public void Fingerprint_Hash_IsFnv1a()
        {
            // Published FNV-1a 32-bit vectors.
            Assert.AreEqual("811c9dc5", RulesFingerprint.Hash(""));
            Assert.AreEqual("e40c292c", RulesFingerprint.Hash("a"));
            Assert.AreEqual("bf9cf968", RulesFingerprint.Hash("foobar"));
        }

        [Test]
        public void Fingerprint_FormatsDoublesInvariantly()
        {
            var before = Thread.CurrentThread.CurrentCulture;
            var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            comma.NumberFormat.NumberGroupSeparator = ".";

            string underComma;
            try
            {
                Thread.CurrentThread.CurrentCulture = comma;
                Assert.AreEqual("0,5", 0.5.ToString(), "the test culture writes a decimal comma");
                underComma = Dump();
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = before;
            }

            Assert.AreEqual(Dump(), underComma);
            StringAssert.Contains("CombatConfig.EvasionChance=0.12 (0x", underComma);
        }

        // ── Every dial moves the hash ────────────────────────────────────

        [Test]
        public void Fingerprint_ChangesWhenAnyConfigDialChanges()
        {
            string baseline = RulesFingerprint.Current;
            int dials = 0;

            dials += ForEachDial(GameConfig.Default, game =>
                RulesFingerprint.Of(game, CombatConfig.Default, EnergyConfig.Default, Roster.All, RosterSpeeds.Default), baseline);
            dials += ForEachDial(CombatConfig.Default, combat =>
                RulesFingerprint.Of(GameConfig.Default, combat, EnergyConfig.Default, Roster.All, RosterSpeeds.Default), baseline);
            dials += ForEachDial(EnergyConfig.Default, energy =>
                RulesFingerprint.Of(GameConfig.Default, CombatConfig.Default, energy, Roster.All, RosterSpeeds.Default), baseline);
            dials += ForEachDial(RosterSpeeds.Default, speeds =>
                RulesFingerprint.Of(GameConfig.Default, CombatConfig.Default, EnergyConfig.Default, Roster.All, speeds), baseline);

            Assert.AreEqual(8 + 19 + 5 + 3, dials, "a constructor gained or lost a dial; check the dump covers it");
        }

        /// <summary>
        /// Rebuilds <paramref name="defaults"/> through its constructor once per
        /// parameter with that one parameter nudged, and asserts the hash moves
        /// each time. Returns how many parameters it nudged.
        /// </summary>
        private static int ForEachDial<T>(T defaults, Func<T, string> hash, string baseline)
        {
            var ctor = typeof(T).GetConstructors()[0];
            var parameters = ctor.GetParameters();

            for (int i = 0; i < parameters.Length; i++)
            {
                var args = new object[parameters.Length];
                for (int j = 0; j < parameters.Length; j++) args[j] = CurrentValue(defaults, parameters[j]);

                T nudged = Nudge<T>(ctor, args, i);
                Assert.AreNotEqual(baseline, hash(nudged), $"{typeof(T).Name}.{parameters[i].Name} does not move the hash");
            }

            return parameters.Length;
        }

        private static object CurrentValue(object instance, ParameterInfo parameter)
        {
            var property = instance.GetType().GetProperty(
                parameter.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            Assert.IsNotNull(property, $"{instance.GetType().Name} has no property for parameter {parameter.Name}");
            return property.GetValue(instance);
        }

        private static T Nudge<T>(ConstructorInfo ctor, object[] args, int index)
        {
            object original = args[index];

            // Up first, then down: some dials have a floor or a ceiling.
            foreach (int direction in new[] { 1, -1 })
            {
                if (original is int n) args[index] = n + direction;
                else if (original is double d) args[index] = d + direction * 0.01;
                else Assert.Fail($"No nudge for a {original.GetType().Name}");

                try
                {
                    return (T)ctor.Invoke(args);
                }
                catch (TargetInvocationException e) when (e.InnerException is ArgumentException)
                {
                    // Out of range that way; try the other.
                }
            }

            Assert.Fail($"{typeof(T).Name}: parameter {index} refuses both nudges");
            return default(T);
        }

        [Test]
        public void Fingerprint_ChangesWhenTheRosterChanges()
        {
            string baseline = RulesFingerprint.Current;

            // One operator's health.
            var tougher = new List<OperatorDefinition>(Roster.All);
            var mimi = tougher[3];
            tougher[3] = new OperatorDefinition(
                mimi.Name, mimi.MaxHealth + 1, mimi.BaseSpeed, mimi.Abilities, mimi.Aura,
                mimi.Passive, mimi.PassiveMagnitude, mimi.PassiveName, mimi.Passive2, mimi.Passive2Magnitude,
                mimi.HasteCellCap, mimi.PassiveDescription);
            Assert.AreNotEqual(baseline, Of(tougher), "operator health");

            // One ability's cost.
            var cheaper = new List<OperatorDefinition>(Roster.All);
            var luka = cheaper[8];
            var abilities = new List<AbilityDefinition>(luka.Abilities);
            var first = abilities[0];
            abilities[0] = new AbilityDefinition(
                first.Id, first.Name, first.Description, first.EnergyCost + 1, first.CooldownTurns,
                first.Range, first.Effects, first.Targeting, first.AllowsSelfTarget);
            cheaper[8] = new OperatorDefinition(
                luka.Name, luka.MaxHealth, luka.BaseSpeed, abilities, luka.Aura,
                luka.Passive, luka.PassiveMagnitude, luka.PassiveName, luka.Passive2, luka.Passive2Magnitude,
                luka.HasteCellCap, luka.PassiveDescription);
            Assert.AreNotEqual(baseline, Of(cheaper), "ability cost");

            // One effect's amount.
            var harder = new List<OperatorDefinition>(Roster.All);
            var bouncer = harder[0];
            var kit = new List<AbilityDefinition>(bouncer.Abilities);
            var hit = kit[0];
            var effects = new List<AbilityEffect>(hit.Effects);
            effects[0] = effects[0].WithRadius(effects[0].Radius + 1);
            kit[0] = new AbilityDefinition(
                hit.Id, hit.Name, hit.Description, hit.EnergyCost, hit.CooldownTurns,
                hit.Range, effects, hit.Targeting, hit.AllowsSelfTarget);
            harder[0] = new OperatorDefinition(
                bouncer.Name, bouncer.MaxHealth, bouncer.BaseSpeed, kit, bouncer.Aura,
                bouncer.Passive, bouncer.PassiveMagnitude, bouncer.PassiveName, bouncer.Passive2, bouncer.Passive2Magnitude,
                bouncer.HasteCellCap, bouncer.PassiveDescription);
            Assert.AreNotEqual(baseline, Of(harder), "effect radius");

            // The order: a random squad is drawn by index into the roster.
            var reordered = new List<OperatorDefinition>(Roster.All);
            reordered.Reverse();
            Assert.AreNotEqual(baseline, Of(reordered), "roster order");

            // One operator fewer.
            var shorter = new List<OperatorDefinition>(Roster.All);
            shorter.RemoveAt(shorter.Count - 1);
            Assert.AreNotEqual(baseline, Of(shorter), "roster size");
        }

        [Test]
        public void Fingerprint_IgnoresText()
        {
            // Rewording a description is not a rules change and must not
            // invalidate every replay on disk.
            var reworded = new List<OperatorDefinition>(Roster.All);
            var syla = reworded[1];
            var abilities = new List<AbilityDefinition>();
            foreach (var a in syla.Abilities)
                abilities.Add(new AbilityDefinition(
                    a.Id, a.Name + " (renamed)", a.Description + " Reworded.", a.EnergyCost, a.CooldownTurns,
                    a.Range, a.Effects, a.Targeting, a.AllowsSelfTarget));
            reworded[1] = new OperatorDefinition(
                syla.Name, syla.MaxHealth, syla.BaseSpeed, abilities, syla.Aura,
                syla.Passive, syla.PassiveMagnitude, (syla.PassiveName ?? "") + " renamed", syla.Passive2, syla.Passive2Magnitude,
                syla.HasteCellCap, (syla.PassiveDescription ?? "") + " Reworded.");

            Assert.AreEqual(RulesFingerprint.Current, Of(reworded));
        }

        private static string Of(IReadOnlyList<OperatorDefinition> roster) =>
            RulesFingerprint.Of(GameConfig.Default, CombatConfig.Default, EnergyConfig.Default, roster, RosterSpeeds.Default);

        // ── Nothing forgotten ────────────────────────────────────────────

        /// <summary>
        /// Public properties the dump leaves out on purpose: text, which
        /// changes wording and not play, and values computed from properties
        /// already written.
        /// </summary>
        private static readonly Dictionary<Type, string[]> Excluded = new Dictionary<Type, string[]>
        {
            { typeof(GameConfig), new[] { "MaxDiceTotal" } },
            { typeof(OperatorDefinition), new[] { "PassiveName", "PassiveDescription" } },
            { typeof(AbilityDefinition), new[] { "Name", "Description", "RequiresTarget", "RequiresCell", "HasUnlimitedRange", "ContainsPlacement" } },
            { typeof(AbilityEffect), new[] { "CarriesStatus" } },
            { typeof(AuraDefinition), new[] { "Name", "Description" } }
        };

        /// <summary>Where each type's properties sit in the dump.</summary>
        private static readonly Dictionary<Type, string> KeyPattern = new Dictionary<Type, string>
        {
            { typeof(GameConfig), @"^GameConfig\.{0}[=.]" },
            { typeof(CombatConfig), @"^CombatConfig\.{0}[=.]" },
            { typeof(EnergyConfig), @"^EnergyConfig\.{0}[=.]" },
            { typeof(RosterSpeeds), @"^RosterSpeeds\.{0}[=.]" },
            { typeof(OperatorDefinition), @"^op\[\d+\]\.{0}[=.]" },
            { typeof(AuraDefinition), @"^op\[\d+\]\.Aura\.{0}=" },
            { typeof(AbilityDefinition), @"^op\[\d+\]\.ability\[\d+\]\.{0}[=.]" },
            { typeof(AbilityEffect), @"\.effect\[\d+\]\.{0}=" }
        };

        [Test]
        public void Fingerprint_DumpNamesEveryRulesProperty()
        {
            string dump = Dump();
            var missing = new List<string>();

            foreach (var pair in KeyPattern)
            {
                string[] skip;
                Excluded.TryGetValue(pair.Key, out skip);

                foreach (var property in pair.Key.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (skip != null && Array.IndexOf(skip, property.Name) >= 0) continue;

                    string pattern = string.Format(CultureInfo.InvariantCulture, pair.Value, property.Name);
                    if (!Regex.IsMatch(dump, pattern, RegexOptions.Multiline))
                        missing.Add($"{pair.Key.Name}.{property.Name}");
                }
            }

            Assert.IsEmpty(missing,
                "RulesFingerprint does not write: " + string.Join(", ", missing) +
                ". Add each to the dump, or to Excluded here if it is text or derived.");
        }
    }
}