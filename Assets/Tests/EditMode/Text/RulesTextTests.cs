// Assets/Tests/EditMode/Text/RulesTextTests.cs
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Text;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Text
{
    /// <summary>
    /// The operator guide's generated text (OPERATOR_GUIDE.md OG1, §6): every
    /// roster ability, passive and aura has a rules line with nothing left
    /// unwritten, every keyword a line links to has a glossary entry, and the
    /// glossary's numbers are the configs'.
    /// </summary>
    /// <remarks>
    /// <b>The golden lines are meant to break.</b> A balance change that moves
    /// one of those five abilities fails its assertion here, and the fix is to
    /// update the expected line — which is the point: the guide's wording
    /// changes as a visible diff, never silently.
    /// </remarks>
    [TestFixture]
    public class RulesTextTests
    {
        private static IEnumerable<AbilityDefinition> AllAbilities =>
            Roster.All.SelectMany(op => op.Abilities);

        private static IEnumerable<RulesLine> AllLines()
        {
            foreach (var op in Roster.All)
            {
                foreach (var ability in op.Abilities) yield return RulesText.For(ability);
                foreach (var passive in RulesText.Passives(op)) yield return passive.Line;
                if (op.Aura != null) yield return RulesText.ForAura(op.Aura);
            }
        }

        // ── Coverage ─────────────────────────────────────────────────────

        [Test]
        public void EveryRosterAbility_HasALine_WithNothingUnwritten()
        {
            foreach (var ability in AllAbilities)
            {
                var line = RulesText.For(ability);
                Assert.That(line.IsEmpty, Is.False, ability.Name);
                Assert.That(line.HasUnwritten, Is.False, $"{ability.Name}: {line}");
            }
        }

        [Test]
        public void EveryPassiveAndAura_HasALine_WithNothingUnwritten()
        {
            foreach (var op in Roster.All)
            {
                foreach (var passive in RulesText.Passives(op))
                    Assert.That(passive.Line.HasUnwritten, Is.False, $"{op.Name}'s {passive.Name}: {passive.Line}");

                if (op.Aura != null)
                    Assert.That(RulesText.ForAura(op.Aura).HasUnwritten, Is.False, $"{op.Name}'s {op.Aura.Name}");
            }
        }

        [Test]
        public void AnOperatorWithAPassive_ListsIt()
        {
            foreach (var op in Roster.All.Where(o => o.Passive.HasValue))
                Assert.That(RulesText.Passives(op), Is.Not.Empty, op.Name);
        }

        [Test]
        public void TheUnwrittenMarker_IsReallyDetected()
        {
            // A status nobody carries as a passive has no passive line: the
            // detector the coverage tests lean on has to see that.
            var line = RulesText.ForPassive(StatusKind.Stun, null, CombatConfig.Default, EnergyConfig.Default);
            Assert.That(line.HasUnwritten, Is.True);
        }

        // ── The glossary ─────────────────────────────────────────────────

        [Test]
        public void EveryKeywordALineLinksTo_HasAGlossaryEntry()
        {
            var ids = new HashSet<string>(Glossary.Entries().Select(e => e.Id));

            foreach (var line in AllLines())
                foreach (var keyword in line.Keywords)
                    Assert.That(ids.Contains(keyword), Is.True, $"'{keyword}' in \"{line}\"");

            // Definitions link to each other too.
            foreach (var entry in Glossary.Entries())
                foreach (var keyword in entry.Definition.Keywords)
                    Assert.That(ids.Contains(keyword), Is.True, $"'{keyword}' in the {entry.Title} entry");
        }

        [Test]
        public void TheGlossary_HasUniqueIds_AndNothingUnwritten()
        {
            var entries = Glossary.Entries();

            Assert.That(entries.Select(e => e.Id).Distinct().Count(), Is.EqualTo(entries.Count));
            foreach (var entry in entries)
                Assert.That(entry.Definition.HasUnwritten, Is.False, entry.Title);
        }

        [Test]
        public void TheGlossary_CoversEveryStatus_AndEveryDamageType()
        {
            foreach (StatusKind kind in System.Enum.GetValues(typeof(StatusKind)))
                Assert.That(Glossary.Find(Keywords.Status(kind)), Is.Not.Null, kind.ToString());

            foreach (DamageType type in System.Enum.GetValues(typeof(DamageType)))
                Assert.That(Glossary.Find(Keywords.Damage(type)), Is.Not.Null, type.ToString());
        }

        [Test]
        public void TheGlossarysNumbers_AreTheConfigs()
        {
            // D2: nothing numeric is typed. Move the dial and the text follows.
            var tuned = new CombatConfig(evasionChance: 0.3, markDamagePerTurn: 5);

            Assert.That(Glossary.Find(Keywords.Status(StatusKind.Evasion), tuned).Definition.ToPlainText(),
                Does.Contain("30%"));
            Assert.That(Glossary.Find(Keywords.Status(StatusKind.Mark), tuned).Definition.ToPlainText(),
                Does.Contain("Takes 5 Atomic"));

            Assert.That(Glossary.Find(Keywords.Status(StatusKind.Evasion)).Definition.ToPlainText(),
                Does.Contain(RulesText.Percent(CombatConfig.Default.EvasionChance)));
        }

        // ── Modes ────────────────────────────────────────────────────────

        [Test]
        public void AnAbilityThatDiffersBySide_IsWrittenAsTwoModes()
        {
            string rope = RulesText.For(Bouncer.VelvetRope).ToPlainText();

            Assert.That(rope, Does.StartWith("Enemy: "));
            Assert.That(rope, Does.Contain("\nAlly: "));
        }

        [Test]
        public void AnAbilityThatIsTheSameEitherWay_IsWrittenOnce()
        {
            Assert.That(RulesText.For(Mimi.Translocation).ToPlainText(), Is.EqualTo("swap places with the target"));
        }

        [Test]
        public void ASelfCastableSupportAbility_SaysSo()
        {
            Assert.That(RulesText.For(Javi.TraumaPlate).ToPlainText(), Does.StartWith("Ally or self: "));
        }

        [Test]
        public void IdenticalBlows_AreCounted_NotRepeated()
        {
            Assert.That(RulesText.For(Luka.Vendetta).ToPlainText(), Does.StartWith("3 × 1 Atomic"));
        }

        // ── Golden lines ─────────────────────────────────────────────────

        [Test]
        public void Golden_BioLinkRage()
        {
            Assert.That(RulesText.For(Nuetu.BioLinkRage).ToPlainText(), Is.EqualTo(
                "3 Normal, Burdened 2 turns · you heal 1 (+1 while one of your zones is live)"));
        }

        [Test]
        public void Golden_Sadist()
        {
            Assert.That(RulesText.For(Revu.Sadist).ToPlainText(), Is.EqualTo(
                "its seat's debt as Normal damage, at least 2; half of that to enemies within 2 of it; the debt is cleared"));
        }

        [Test]
        public void Golden_Catalyst()
        {
            Assert.That(RulesText.ForAura(Lethe.Catalyst).ToPlainText(), Is.EqualTo(
                "allies within 2 of you, or up to 6 behind you, count as Hastened for any move they start there"));
        }

        [Test]
        public void Golden_ErisExploit()
        {
            Assert.That(RulesText.For(Lethe.ErisExploit).ToPlainText(), Does.StartWith(
                "draws enemies within 4 up to 2 cells toward it"));
        }

        [Test]
        public void Golden_LeechRound()
        {
            Assert.That(RulesText.For(Revu.LeechRound).ToPlainText(), Is.EqualTo("2 Normal, its seat takes on 2 debt"));
        }

        [Test]
        public void Golden_Vendetta()
        {
            Assert.That(RulesText.For(Luka.Vendetta).ToPlainText(), Is.EqualTo(
                "3 × 1 Atomic; each blow can crit: 10% for ×2, ×3 against heavy targets (max health above 7); " +
                "you heal what each blow removes"));
        }

        [Test]
        public void Golden_MiraclePull()
        {
            Assert.That(RulesText.For(Kurbyn.MiraclePull).ToPlainText(), Is.EqualTo(
                "neutralized outright if it is below half of its max health when you cast; otherwise 3 Atomic · " +
                "enemies within 3 of the target: 2 Atomic"));
        }

        [Test]
        public void Golden_TheTable()
        {
            Assert.That(RulesText.For(Fortuna.TheTable).ToPlainText(), Is.EqualTo(
                "a table on the cell for 3 of your turns: the first enemy dice move to cross it stops there " +
                "and takes 2 Normal, once per operator"));
        }

        // ── It follows the data ──────────────────────────────────────────

        [Test]
        public void TheLine_IsReadFromTheDefinition_NotRemembered()
        {
            var ability = new AbilityDefinition(
                id: 99971, name: "Test Jab", description: "Test double.",
                energyCost: 1, cooldownTurns: 0, range: 1,
                effects: new[]
                {
                    AbilityEffect.Damage(EffectScope.PrimaryTarget, 5, DamageType.Tech, EffectAudience.EnemyOnly),
                    AbilityEffect.Status_(EffectScope.PrimaryTarget, StatusKind.Stun, 3)
                });

            var line = RulesText.For(ability);

            Assert.That(line.ToPlainText(), Is.EqualTo("5 Tech, Stun 3 turns"));
            Assert.That(line.Keywords, Is.EqualTo(new[] { Keywords.Damage(DamageType.Tech), Keywords.Status(StatusKind.Stun) }));
        }
    }
}
