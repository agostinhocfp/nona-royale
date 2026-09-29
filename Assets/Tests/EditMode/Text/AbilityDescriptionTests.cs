// Assets/Tests/EditMode/Text/AbilityDescriptionTests.cs
using System.Linq;
using NonaRoyale.Core.Abilities;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Text
{
    /// <summary>
    /// The words a player reads when hovering or holding an ability card
    /// (CAST_ONBOARDING.md, CO0): what the ability does and what follows from
    /// it. The card itself shows only the cost, the reach and the cooldown,
    /// and the rules line carries every number.
    /// </summary>
    /// <remarks>
    /// <b>Enforced here, not in the constructor,</b> for the reason
    /// <see cref="KitTraitTests"/> gives: test and sweep kits are built with
    /// one-word descriptions nobody reads. The roster is what a player sees.
    ///
    /// <b>Facts, not advice</b> (<see cref="AbilityDefinition.Description"/>)
    /// is a rule for the writer, not something a test can read. It is kept in
    /// review.
    /// </remarks>
    [TestFixture]
    public class AbilityDescriptionTests
    {
        /// <summary>Fits the peek and the full card at small type without scrolling.</summary>
        private const int MaxLength = 320;

        [Test]
        public void NoAbilityDescription_HasADigit()
        {
            // The rules line carries every number (AbilityDefinition.Description).
            foreach (var ability in Roster.AllAbilities)
                Assert.That(ability.Description.Any(char.IsDigit), Is.False,
                    $"{ability.Name}: \"{ability.Description}\"");
        }

        [Test]
        public void EveryAbilityDescription_FitsTheCard()
        {
            foreach (var ability in Roster.AllAbilities)
                Assert.That(ability.Description.Length, Is.LessThanOrEqualTo(MaxLength),
                    $"{ability.Name} is {ability.Description.Length} characters");
        }
    }
}
