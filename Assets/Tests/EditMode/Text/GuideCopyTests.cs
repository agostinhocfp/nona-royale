// Assets/Tests/EditMode/Text/GuideCopyTests.cs
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Text;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Text
{
    /// <summary>
    /// The guide's hand-written copy (OPERATOR_GUIDE.md D3, OG5): every
    /// operator has it, and none of it carries a number.
    /// </summary>
    /// <remarks>
    /// <b>The digit rule is D2 enforced.</b> A number typed into a strategy
    /// paragraph is wrong the day the next balance pass lands; every number in
    /// the guide comes from the rules lines. Words like "half" and "sixes"
    /// describe a rule's shape and are allowed; "3" is not.
    /// </remarks>
    [TestFixture]
    public class GuideCopyTests
    {
        [Test]
        public void EveryOperatorInTheRoster_HasCopy()
        {
            foreach (var op in Roster.All)
            {
                var entry = GuideCopy.For(op.Name);
                Assert.That(entry, Is.Not.Null, $"{op.Name} has no guide copy");
                Assert.That(entry.Tagline, Is.Not.Empty, op.Name);
                Assert.That(entry.HowToPlay, Is.Not.Empty, op.Name);
                Assert.That(entry.HowToBeat, Is.Not.Empty, op.Name);
            }
        }

        [Test]
        public void TheCopy_HasNoDigits()
        {
            foreach (var pair in GuideCopy.All)
            {
                var entry = pair.Value;
                foreach (var text in new[] { entry.Tagline, entry.HowToPlay, entry.HowToBeat })
                    Assert.That(text.Any(char.IsDigit), Is.False, $"{pair.Key}: \"{text}\"");
            }
        }

        [Test]
        public void EveryEntry_IsForAnOperatorThatExists()
        {
            var names = Roster.All.Select(o => o.Name).ToList();
            foreach (var name in GuideCopy.All.Keys)
                Assert.That(names, Does.Contain(name), $"copy written for '{name}', who is not in the roster");
        }

        [Test]
        public void TheCamps_MatchOperatorsMd()
        {
            // OPERATORS.md, "The camps" (2026-09-19): four house, four
            // contractors, three owners, and Luka alone.
            Assert.That(GuideCopy.All.Values.Count(e => e.Camp == Camp.House), Is.EqualTo(4));
            Assert.That(GuideCopy.All.Values.Count(e => e.Camp == Camp.Contractor), Is.EqualTo(4));
            Assert.That(GuideCopy.All.Values.Count(e => e.Camp == Camp.Owner), Is.EqualTo(3));
            Assert.That(GuideCopy.For("Luka").Camp, Is.EqualTo(Camp.Alone));
        }
    }
}
