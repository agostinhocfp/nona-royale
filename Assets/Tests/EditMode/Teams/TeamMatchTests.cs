// Assets/Tests/EditMode/Teams/TeamMatchTests.cs
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Teams
{
    /// <summary>
    /// A whole crossed 1v1 match (ADR-0012): that the factory hands the same
    /// side map to every service, and that the match is won by a side rather
    /// than a seat — all six operators home, not three.
    /// </summary>
    /// <remarks>
    /// <b>Wins are forced by placing pieces at HOME rather than played out.</b>
    /// The point under test is what the win condition counts, and rolling a
    /// full six-operator lap to find out would make the fixture a
    /// several-thousand-turn simulation whose failure mode is a timeout rather
    /// than an assertion. <c>BotTests</c> already plays matches to completion;
    /// this fixture asks a narrower question.
    /// </remarks>
    [TestFixture]
    public class TeamMatchTests
    {
        private static readonly PlayerColor[] FourSeats =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet
        };

        private MatchFactory.Match Crossed(int seed = 11) =>
            MatchFactory.CreateAlphaMatch(FourSeats, seed, teams: TeamMap.CrossedPairs);

        private MatchFactory.Match FreeForAll(int seed = 11) =>
            MatchFactory.CreateAlphaMatch(FourSeats, seed);

        private static void SendHome(MatchFactory.Match match, PlayerColor seat)
        {
            foreach (var op in match.Players.First(p => p.Color == seat).Operators)
                op.MoveTo(BoardProfile.Standard.Journey);
        }

        // ── Composition ──────────────────────────────────────────────────

        [Test]
        public void TheFactoryHandsTheSideMapToTheMatch()
        {
            Assert.That(Crossed().Teams, Is.SameAs(TeamMap.CrossedPairs));
            Assert.That(FreeForAll().Teams, Is.SameAs(TeamMap.FreeForAll),
                "a match built without a map is free-for-all, exactly as before");
        }

        [Test]
        public void EverySeatStillFieldsItsOwnSquadAndItsOwnPool()
        {
            // Team play changes who you may shoot, not who owns what. Energy
            // stays per seat and each seat keeps its own three operators.
            var match = Crossed();

            Assert.That(match.Players.Count, Is.EqualTo(4));

            foreach (var player in match.Players)
            {
                Assert.That(player.Operators.Count, Is.EqualTo(3), $"{player.Color} squad");
                Assert.That(player.Operators.All(o => o.Owner == player.Color), Is.True);
            }
        }

        [Test]
        public void ASeatStillCommandsOnlyItsOwnOperators()
        {
            // The partner seat acts on its own turn. A player holding two seats
            // does not get to move both from one turn — the seat rotation is
            // untouched, so R-B-G-V still alternates the two players.
            var match = Crossed();
            var engine = match.Engine;
            engine.Start();

            Assert.That(engine.CurrentPlayer.Color, Is.EqualTo(PlayerColor.Red), "precondition");

            engine.Execute(new RollDiceCommand());
            var partnersPiece = match.Operators.First(o => o.Owner == PlayerColor.Green);
            var events = engine.Execute(new MoveCommand(partnersPiece.Id));

            Assert.That(events.OfType<CommandRejected>().Any(), Is.True,
                "Red's turn commands Red's operators, partner or no partner");
        }

        // ── The win condition (§8) ───────────────────────────────────────

        [Test]
        public void OneSeatHome_IsNotAWinInATeamMatch()
        {
            var match = Crossed();
            var win = new WinConditions(match.Map, TeamMap.CrossedPairs);

            SendHome(match, PlayerColor.Red);

            Assert.That(win.Winner(match.Players), Is.Null,
                "Red is home; Green is not; the side has not won");
            Assert.That(win.WinningSeats(match.Players), Is.Empty);
        }

        [Test]
        public void BothSeatsHome_WinsForTheSide()
        {
            var match = Crossed();
            var win = new WinConditions(match.Map, TeamMap.CrossedPairs);

            SendHome(match, PlayerColor.Red);
            SendHome(match, PlayerColor.Green);

            Assert.That(win.Winner(match.Players), Is.EqualTo(PlayerColor.Red),
                "the side is named after its first seat at the table");
            Assert.That(win.WinningSeats(match.Players),
                Is.EqualTo(new[] { PlayerColor.Red, PlayerColor.Green }));
        }

        [Test]
        public void TheOtherSideWinsTheSameWay()
        {
            var match = Crossed();
            var win = new WinConditions(match.Map, TeamMap.CrossedPairs);

            SendHome(match, PlayerColor.Blue);
            SendHome(match, PlayerColor.Violet);

            Assert.That(win.Winner(match.Players), Is.EqualTo(PlayerColor.Blue));
            Assert.That(win.WinningSeats(match.Players),
                Is.EqualTo(new[] { PlayerColor.Blue, PlayerColor.Violet }));
        }

        [Test]
        public void UnderFreeForAll_OneSeatHomeStillWins()
        {
            var match = FreeForAll();
            var win = new WinConditions(match.Map);

            SendHome(match, PlayerColor.Green);

            Assert.That(win.Winner(match.Players), Is.EqualTo(PlayerColor.Green));
            Assert.That(win.WinningSeats(match.Players), Is.EqualTo(new[] { PlayerColor.Green }));
        }

        [Test]
        public void ASideWhosePartnerSeatIsNotPlaying_WinsOnItsOwn()
        {
            // A team map left switched on for a two-seat table must not
            // deadlock the match waiting for a seat nobody is sitting in.
            var match = MatchFactory.CreateAlphaMatch(
                new[] { PlayerColor.Red, PlayerColor.Blue }, seed: 3, teams: TeamMap.CrossedPairs);

            var win = new WinConditions(match.Map, TeamMap.CrossedPairs);
            SendHome(match, PlayerColor.Red);

            Assert.That(win.Winner(match.Players), Is.EqualTo(PlayerColor.Red));
        }

        // ── Through the engine ───────────────────────────────────────────

        [Test]
        public void TheEngineAnnouncesBothWinningSeats()
        {
            var match = Crossed();
            var engine = match.Engine;
            engine.Start();

            SendHome(match, PlayerColor.Red);
            engine.Execute(new RollDiceCommand());

            Assert.That(engine.Execute(new EndTurnCommand()).OfType<GameWon>().Any(), Is.False,
                "half a side home is not a win");
            Assert.That(engine.Winner, Is.Null);

            SendHome(match, PlayerColor.Green);

            // Blue's turn now; the win is checked at every turn's end, so the
            // side's win is announced on whoever's turn closes next.
            engine.Execute(new RollDiceCommand());
            var won = engine.Execute(new EndTurnCommand()).OfType<GameWon>().SingleOrDefault();

            Assert.That(won, Is.Not.Null);
            Assert.That(won.Winner, Is.EqualTo(PlayerColor.Red));
            Assert.That(won.Seats, Is.EqualTo(new[] { PlayerColor.Red, PlayerColor.Green }));
            Assert.That(engine.WinningSeats, Is.EqualTo(new[] { PlayerColor.Red, PlayerColor.Green }));
        }

        [Test]
        public void TheFinalStretchIsCountedPerSide()
        {
            var match = Crossed();
            var engine = match.Engine;
            engine.Start();

            // Five of Red-and-Green's six home. Counted per seat this would
            // have fired two operators ago.
            var red = match.Players.First(p => p.Color == PlayerColor.Red).Operators;
            var green = match.Players.First(p => p.Color == PlayerColor.Green).Operators;

            foreach (var op in red) op.MoveTo(BoardProfile.Standard.Journey);
            green[0].MoveTo(BoardProfile.Standard.Journey);

            Assert.That(engine.IsFinalStretch, Is.False, "two still out");

            green[1].MoveTo(BoardProfile.Standard.Journey);

            Assert.That(engine.IsFinalStretch, Is.True, "one more arrival wins it");
        }
    }
}
