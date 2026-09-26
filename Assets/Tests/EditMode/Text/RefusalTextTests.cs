// Assets/Tests/EditMode/Text/RefusalTextTests.cs
using System;
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NonaRoyale.Core.Text;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Text
{
    /// <summary>
    /// Refused casts in the player's words (<see cref="RefusalText"/>,
    /// LAUNCH_UI_PASS.md G7b-2): never an enum's name, and each one says what
    /// to do about it where there is something to do.
    /// </summary>
    [TestFixture]
    public class RefusalTextTests
    {
        private static readonly OperatorState Kian = new OperatorState(1, "Kian", PlayerColor.Red, 8, 1.0);

        private static AbilityDefinition AnAbility =>
            Roster.All.SelectMany(o => o.Abilities).First(a => a.EnergyCost > 0);

        private static string Say(AbilityRefusal refusal, TargetingVerdict verdict = TargetingVerdict.Legal,
            int turns = 0, int energy = 0) =>
            RefusalText.Ability(AnAbility, Kian, refusal, verdict, turns, energy);

        [Test]
        public void NoRefusalOrVerdict_ReadsAsItsEnumName()
        {
            foreach (AbilityRefusal refusal in Enum.GetValues(typeof(AbilityRefusal)))
            foreach (TargetingVerdict verdict in Enum.GetValues(typeof(TargetingVerdict)))
            {
                string text = Say(refusal, verdict, 2, 1);
                if (refusal != AbilityRefusal.None)
                    Assert.That(text, Does.Not.Contain(refusal.ToString()), text);
                if (verdict != TargetingVerdict.Legal)
                    Assert.That(text, Does.Not.Contain(verdict.ToString()), text);
            }
        }

        [Test]
        public void EveryVerdict_HasItsOwnWords()
        {
            var said = Enum.GetValues(typeof(TargetingVerdict)).Cast<TargetingVerdict>()
                .Where(v => v != TargetingVerdict.Legal)
                .Select(RefusalText.Targeting)
                .ToList();

            Assert.That(said.Distinct().Count(), Is.EqualTo(said.Count));
        }

        [Test]
        public void ACooldown_SaysWhenItIsReady()
        {
            Assert.That(Say(AbilityRefusal.OnCooldown, turns: 2), Is.EqualTo($"{AnAbility.Name} is recharging: ready in 2 turns"));
            Assert.That(Say(AbilityRefusal.OnCooldown, turns: 1), Does.EndWith("ready in 1 turn"));
        }

        [Test]
        public void ShortOfEnergy_SaysTheCostAndWhatYouHave()
        {
            Assert.That(Say(AbilityRefusal.InsufficientEnergy, energy: 1),
                Is.EqualTo($"{AnAbility.Name} costs {AnAbility.EnergyCost} energy; you have 1"));
        }

        [Test]
        public void AMissingPick_NamesTheGesture()
        {
            Assert.That(Say(AbilityRefusal.NoTarget), Does.Contain("click an operator"));
            Assert.That(Say(AbilityRefusal.NoCell), Does.Contain("click the board"));
        }

        [Test]
        public void ABadTarget_SaysWhy()
        {
            Assert.That(Say(AbilityRefusal.IllegalTarget, TargetingVerdict.OutOfRange), Is.EqualTo($"{AnAbility.Name}: out of range"));
            Assert.That(Say(AbilityRefusal.IllegalTarget, TargetingVerdict.CasterOutOfPlay), Is.EqualTo("Kian is out of the fight"));
        }
    }
}
