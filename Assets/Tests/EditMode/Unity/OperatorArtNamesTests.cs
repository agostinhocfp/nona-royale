// Assets/Tests/EditMode/Unity/OperatorArtNamesTests.cs
using NonaRoyale.Unity.View;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.View
{
    [TestFixture]
    public class OperatorArtNamesTests
    {
        [Test]
        public void Key_LowercasesTheName()
        {
            Assert.AreEqual("luka", OperatorArtNames.Key("Luka"));
            Assert.AreEqual("kurbyn", OperatorArtNames.Key("KURBYN"));
        }

        [Test]
        public void Key_StripsAccents()
        {
            Assert.AreEqual("revu", OperatorArtNames.Key("Revú"));
            Assert.AreEqual("aeiou", OperatorArtNames.Key("Áéîõü"));
        }

        [Test]
        public void Key_CollapsesGapsToOneUnderscore()
        {
            Assert.AreEqual("bio_link", OperatorArtNames.Key("Bio Link"));
            Assert.AreEqual("eris_exploit", OperatorArtNames.Key("Eris' Exploit"));
            Assert.AreEqual("a_b", OperatorArtNames.Key("  a -- b  "));
            Assert.AreEqual("luka", OperatorArtNames.Key("Luka!"));
            Assert.AreEqual("mr_x", OperatorArtNames.Key("Mr. X."));
        }

        [Test]
        public void Key_OfNothing_IsEmpty()
        {
            Assert.AreEqual(string.Empty, OperatorArtNames.Key(null));
            Assert.AreEqual(string.Empty, OperatorArtNames.Key(""));
            Assert.AreEqual(string.Empty, OperatorArtNames.Key(" ' "));
        }

        [Test]
        public void Key_EveryRosterName_IsPlainAscii()
        {
            foreach (var name in new[] { "Bouncer", "Syla", "Kurbyn", "Mimi", "Javi", "Kian", "Sanity", "Luka", "Nuetu", "Lethe", "Revú" })
            {
                foreach (char c in OperatorArtNames.Key(name))
                    Assert.That(c >= 'a' && c <= 'z', $"{name} → {OperatorArtNames.Key(name)}");
            }
        }
    }
}