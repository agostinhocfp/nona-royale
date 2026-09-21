// Assets/Tests/EditMode/Unity/RulesMarkupTests.cs
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Text;
using NonaRoyale.Unity.View;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The guide's rules lines as TextMeshPro markup (OPERATOR_GUIDE.md OG2):
    /// keywords link only where something listens, and a status keyword wears
    /// the colour its tag has on the board.
    /// </summary>
    [TestFixture]
    public class RulesMarkupTests
    {
        private static RulesLine BioLinkRage => RulesText.For(Nuetu.BioLinkRage);

        [Test]
        public void ALinkedLine_LinksItsKeywords()
        {
            string markup = RulesMarkup.For(BioLinkRage, linked: true);

            Assert.That(markup, Does.Contain("<link=\"" + Keywords.Status(StatusKind.Burdened) + "\">"));
            Assert.That(markup, Does.Contain("<link=\"" + Keywords.Damage(DamageType.Normal) + "\">"));
        }

        [Test]
        public void AnUnlinkedLine_ColoursButDoesNotPromiseATap()
        {
            string markup = RulesMarkup.For(BioLinkRage, linked: false);

            Assert.That(markup, Does.Not.Contain("<link"));
            Assert.That(markup, Does.Not.Contain("<u>"));
            Assert.That(markup, Does.Contain("Burdened"));
        }

        [Test]
        public void AStatusKeyword_WearsItsBoardColour()
        {
            string markup = RulesMarkup.For(BioLinkRage, linked: false);
            string hex = UiTheme.Hex(StatusPalette.For(StatusKind.Burdened));

            Assert.That(markup, Does.Contain("<color=#" + hex + ">Burdened</color>"));
        }

        [Test]
        public void Numbers_AreBold()
        {
            Assert.That(RulesMarkup.For(BioLinkRage, linked: false), Does.Contain(">3</color></b>"));
        }

        [Test]
        public void TheModeWords_AreColouredBySide()
        {
            Assert.That(RulesMarkup.ColourOf(Keywords.Mode, "Enemy"), Is.EqualTo(UiTheme.Threat));
            Assert.That(RulesMarkup.ColourOf(Keywords.Mode, "Ally or self"), Is.EqualTo(UiTheme.Heal));
        }
    }
}
