// Assets/Tests/EditMode/Text/KitTraitTests.cs
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Text;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Text
{
    /// <summary>
    /// The part of a kit that is never pressed (OPERATOR_GUIDE.md §1,
    /// 2026-09-21): every passive and aura in the roster is listed, reads like
    /// an ability — a rules line and a flavour line — and the flavour carries
    /// no number.
    /// </summary>
    /// <remarks>
    /// <b>Why the description is enforced here and not in the constructor.</b>
    /// Test and sweep squads build operators with passives that nobody reads;
    /// a thirteenth roster operator is the one that must arrive with words,
    /// and this is what makes it.
    /// </remarks>
    [TestFixture]
    public class KitTraitTests
    {
        [Test]
        public void EveryPassiveAndAuraInTheRoster_IsATrait()
        {
            foreach (var op in Roster.All)
            {
                var traits = RulesText.Traits(op);

                int passives = op.Passive2.HasValue && op.PassiveName == null ? 2 : op.Passive.HasValue ? 1 : 0;
                Assert.That(traits.Count(t => t.Kind == TraitKind.Passive), Is.EqualTo(passives), op.Name);
                Assert.That(traits.Count(t => t.Kind == TraitKind.Aura), Is.EqualTo(op.Aura != null ? 1 : 0), op.Name);
            }
        }

        [Test]
        public void EveryRosterTrait_HasADescription()
        {
            foreach (var op in Roster.All)
            foreach (var trait in RulesText.Traits(op))
                Assert.That(trait.Description, Is.Not.Null.And.Not.Empty, $"{op.Name}'s {trait.Name} has no description");
        }

        [Test]
        public void NoTraitDescription_HasADigit()
        {
            // D2: the rules line carries every number.
            foreach (var op in Roster.All)
            foreach (var trait in RulesText.Traits(op).Where(t => t.Description != null))
                Assert.That(trait.Description.Any(char.IsDigit), Is.False, $"{op.Name}'s {trait.Name}: \"{trait.Description}\"");
        }

        [Test]
        public void PassivesComeFirst_ThenTheAura()
        {
            // Lethe carries both, and every screen lists them in this order.
            var traits = RulesText.Traits(Lethe.Definition);

            Assert.That(traits.Select(t => t.Kind), Is.EqualTo(new[] { TraitKind.Passive, TraitKind.Aura }));
            Assert.That(traits[0].Status, Is.EqualTo(StatusKind.Hastened));
            Assert.That(traits[1].Name, Is.EqualTo(Lethe.Catalyst.Name));
            Assert.That(traits[1].Radius, Is.EqualTo(Lethe.Catalyst.Radius));
        }

        [Test]
        public void ANamedTwoStatusPassive_IsOneTrait()
        {
            // Kurbyn's Evasive Protocol was Evasion and Hastened under one name
            // until 2026-09-24. No operator carries two now; the shape stays.
            var twoStatus = new OperatorDefinition("Test", 7, 1.0, Kurbyn.All,
                passive: StatusKind.Evasion, passiveName: "Evasive Protocol",
                passive2: StatusKind.Hastened, hasteCellCap: 2);
            var traits = RulesText.Traits(twoStatus);

            Assert.That(traits.Count, Is.EqualTo(1));
            Assert.That(traits[0].Name, Is.EqualTo("Evasive Protocol"));
            Assert.That(traits[0].Status, Is.EqualTo(StatusKind.Evasion));
            Assert.That(traits[0].Line.HasUnwritten, Is.False);
        }

        [Test]
        public void KurbynsPassive_IsHasteAlone()
        {
            var traits = RulesText.Traits(Kurbyn.Definition);

            Assert.That(traits.Count, Is.EqualTo(1));
            Assert.That(traits[0].Name, Is.EqualTo("Evasive Protocol"));
            Assert.That(traits[0].Status, Is.EqualTo(StatusKind.Hastened));
        }

        [Test]
        public void AnUnnamedPassive_TakesItsStatusTitle()
        {
            var traits = RulesText.Traits(Sanity.Definition);

            Assert.That(traits.Single().Name, Is.EqualTo(Glossary.TitleOf(StatusKind.Burdened)));
        }

        [Test]
        public void AnOperatorWithNeither_HasNoTraits()
        {
            foreach (var op in Roster.All.Where(o => !o.Passive.HasValue && o.Aura == null))
                Assert.That(RulesText.Traits(op), Is.Empty, op.Name);
        }
    }
}
