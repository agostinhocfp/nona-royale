// Assets/Tests/EditMode/Abilities/KurbynTests.cs
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Abilities
{
    /// <summary>
    /// Kurbyn's 2026-09-17 rebuild (COMBAT_SYSTEMS §10.3): two actives and
    /// Evasive Protocol — one fiction carried as two permanent statuses,
    /// Evasion at 12% and Hastened capped at 2 cells a turn where the roster's
    /// cap is 3. Predator's Read is removed; the watch machinery stays in the
    /// core, dormant.
    /// </summary>
    [TestFixture]
    public class KurbynTests
    {
        [Test]
        public void TheDesignersNumbers()
        {
            // 2026-09-17. A test so that changing them is a deliberate act that
            // also updates §10.3.
            Assert.That(Kurbyn.BaseSpeed, Is.EqualTo(1.0));
            // 2026-09-24: Evasion removed; the passive is haste alone.
            Assert.That(Kurbyn.Definition.Passive, Is.EqualTo(StatusKind.Hastened));
            Assert.That(Kurbyn.Definition.Passive2, Is.Null);
            Assert.That(Kurbyn.Definition.HasteCellCap, Is.EqualTo(2));
            Assert.That(Kurbyn.Definition.PassiveName, Is.EqualTo("Evasive Protocol"));
        }

        [Test]
        public void TheKit_IsTwoActives_AndThePassive()
        {
            var ids = Kurbyn.All.Select(a => a.Id).ToArray();

            Assert.That(ids, Is.EquivalentTo(new[] { 301, 302 }));
            Assert.That(Kurbyn.All.Any(a => a.Name == "Predator's Read"), Is.False,
                "removed 2026-09-17 — the kit is two actives and the passive again");
        }

        [Test]
        public void HisHaste_IsLiveFromMatchStart_AndEvasionIsGone()
        {
            var match = MatchFactory.CreateAlphaMatch(
                new[] { PlayerColor.Red }, 1, openingDeployments: 3);
            match.Engine.Start();
            var kurbyn = match.Operators.First(o => o.Name == "Kurbyn");

            Assert.That(match.Statuses.Has(kurbyn, StatusKind.Hastened), Is.True);
            Assert.That(match.Statuses.Has(kurbyn, StatusKind.Evasion), Is.False,
                "removed 2026-09-24");
            Assert.That(match.Statuses.SpeedModifier(kurbyn), Is.EqualTo(0.0),
                "haste is not speed");
        }
    }
}
