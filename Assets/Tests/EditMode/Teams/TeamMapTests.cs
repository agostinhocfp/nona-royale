// Assets/Tests/EditMode/Teams/TeamMapTests.cs
using System;
using NonaRoyale.Core.Board;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Teams
{
    /// <summary>
    /// The side map itself (ADR-0012): who is allied with whom, and the
    /// guarantee that free-for-all is seat equality and nothing more.
    /// </summary>
    /// <remarks>
    /// The free-for-all assertions matter more than the team ones. Every rule
    /// in the game now asks this type instead of comparing two colours, so if
    /// <see cref="TeamMap.FreeForAll"/> ever answered anything other than
    /// <c>a == b</c>, the four-way game would change without a single test
    /// about four-way play failing.
    /// </remarks>
    [TestFixture]
    public class TeamMapTests
    {
        private static readonly PlayerColor[] Seats =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet
        };

        [Test]
        public void FreeForAll_IsExactlySeatEquality()
        {
            foreach (var a in Seats)
            {
                foreach (var b in Seats)
                {
                    Assert.That(TeamMap.FreeForAll.AreAllied(a, b), Is.EqualTo(a == b),
                        $"{a} vs {b}: allied");
                    Assert.That(TeamMap.FreeForAll.AreEnemies(a, b), Is.EqualTo(a != b),
                        $"{a} vs {b}: enemies");
                }
            }
        }

        [Test]
        public void FreeForAll_PutsEachSeatOnASideOfItsOwn()
        {
            Assert.That(TeamMap.FreeForAll.HasTeams, Is.False);

            foreach (var seat in Seats)
            {
                Assert.That(TeamMap.FreeForAll.SeatsOn(seat), Is.EqualTo(new[] { seat }));
                Assert.That(TeamMap.FreeForAll.LeadSeat(seat), Is.EqualTo(seat));
            }
        }

        [Test]
        public void CrossedPairs_PartnersRedWithGreen_AndBlueWithViolet()
        {
            var teams = TeamMap.CrossedPairs;

            Assert.That(teams.HasTeams, Is.True);
            Assert.That(teams.AreAllied(PlayerColor.Red, PlayerColor.Green), Is.True);
            Assert.That(teams.AreAllied(PlayerColor.Blue, PlayerColor.Violet), Is.True);

            Assert.That(teams.AreEnemies(PlayerColor.Red, PlayerColor.Blue), Is.True);
            Assert.That(teams.AreEnemies(PlayerColor.Red, PlayerColor.Violet), Is.True);
            Assert.That(teams.AreEnemies(PlayerColor.Green, PlayerColor.Blue), Is.True);
            Assert.That(teams.AreEnemies(PlayerColor.Green, PlayerColor.Violet), Is.True);
        }

        [Test]
        public void CrossedPairs_PartnersSitOppositeEachOther()
        {
            // The claim in the doc comment, measured rather than asserted by
            // assertion: partners' starts are half a circuit apart, which is
            // the whole reason the pairing is crossed and not adjacent.
            var profile = BoardProfile.Standard;
            int half = profile.CircuitLength / 2;
            var map = new PathMap(profile);

            foreach (var seat in Seats)
            {
                foreach (var partner in TeamMap.CrossedPairs.SeatsOn(seat))
                {
                    if (partner == seat) continue;

                    int gap = Math.Abs(map.StartTrackIndex(seat) - map.StartTrackIndex(partner));
                    Assert.That(Math.Min(gap, profile.CircuitLength - gap), Is.EqualTo(half),
                        $"{seat} and {partner} should start half a circuit apart");
                }
            }
        }

        [Test]
        public void ASeatIsAlwaysItsOwnAlly()
        {
            foreach (var seat in Seats)
            {
                Assert.That(TeamMap.CrossedPairs.AreAllied(seat, seat), Is.True);
                Assert.That(TeamMap.CrossedPairs.AreEnemies(seat, seat), Is.False);
            }
        }

        [Test]
        public void NoneIsNeitherAllyNorEnemy()
        {
            // Outer-track cells carry None as their owner, and a cell has no
            // side. Both answers are false, which is why AreEnemies is not
            // written as !AreAllied anywhere.
            foreach (var seat in Seats)
            {
                Assert.That(TeamMap.CrossedPairs.AreAllied(PlayerColor.None, seat), Is.False);
                Assert.That(TeamMap.CrossedPairs.AreEnemies(PlayerColor.None, seat), Is.False);
                Assert.That(TeamMap.CrossedPairs.AreAllied(seat, PlayerColor.None), Is.False);
                Assert.That(TeamMap.CrossedPairs.AreEnemies(seat, PlayerColor.None), Is.False);
            }

            Assert.That(TeamMap.CrossedPairs.TeamOf(PlayerColor.None), Is.EqualTo(TeamMap.NoTeam));
        }

        [Test]
        public void SeatsOnASide_AreInTableOrder_AndIncludeTheSeatItself()
        {
            Assert.That(TeamMap.CrossedPairs.SeatsOn(PlayerColor.Green),
                Is.EqualTo(new[] { PlayerColor.Red, PlayerColor.Green }));

            Assert.That(TeamMap.CrossedPairs.SeatsOn(PlayerColor.Violet),
                Is.EqualTo(new[] { PlayerColor.Blue, PlayerColor.Violet }));
        }

        [Test]
        public void LeadSeat_IsTheSidesFirstSeatInTableOrder()
        {
            Assert.That(TeamMap.CrossedPairs.LeadSeat(PlayerColor.Green), Is.EqualTo(PlayerColor.Red));
            Assert.That(TeamMap.CrossedPairs.LeadSeat(PlayerColor.Red), Is.EqualTo(PlayerColor.Red));
            Assert.That(TeamMap.CrossedPairs.LeadSeat(PlayerColor.Violet), Is.EqualTo(PlayerColor.Blue));
            Assert.That(TeamMap.CrossedPairs.LeadSeat(PlayerColor.Blue), Is.EqualTo(PlayerColor.Blue));
        }

        [Test]
        public void Of_RefusesAMapThatDoesNotCoverEverySeatExactlyOnce()
        {
            Assert.Throws<ArgumentException>(() => TeamMap.Of(
                new[] { PlayerColor.Red, PlayerColor.Green },
                new[] { PlayerColor.Blue }));                       // Violet on no side

            Assert.Throws<ArgumentException>(() => TeamMap.Of(
                new[] { PlayerColor.Red, PlayerColor.Green },
                new[] { PlayerColor.Green, PlayerColor.Blue, PlayerColor.Violet }));  // Green twice
        }

        [Test]
        public void Of_BuildsTheCrossedLayoutTheSameWayTheSharedOneDoes()
        {
            var built = TeamMap.Of(
                new[] { PlayerColor.Red, PlayerColor.Green },
                new[] { PlayerColor.Blue, PlayerColor.Violet });

            foreach (var a in Seats)
                foreach (var b in Seats)
                    Assert.That(built.AreAllied(a, b),
                        Is.EqualTo(TeamMap.CrossedPairs.AreAllied(a, b)), $"{a} vs {b}");
        }
    }
}
