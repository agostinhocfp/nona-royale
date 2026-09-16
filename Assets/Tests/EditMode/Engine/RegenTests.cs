// Assets/Tests/EditMode/Engine/RegenTests.cs
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Engine
{
    /// <summary>
    /// Passive regeneration as amended on 2026-09-16 (COMBAT_SYSTEMS §5.11):
    /// +1 health after 3 straight owner-upkeeps spent wounded, in play and off
    /// any safe cell.
    /// </summary>
    /// <remarks>
    /// <b>The first tests regen has ever had, and the reason it needed them.</b>
    /// <c>CombatConfig</c>'s constructor took the two regen values and never
    /// assigned them, so regen read 0 and never ran, while the rules doc
    /// described it as live. Nothing failed, because nothing looked.
    ///
    /// A solo seat keeps the board quiet: no enemy can move, damage or collide
    /// with the operator being watched. Bouncer takes every roll, so the
    /// operator under test stays exactly where the test put it.
    /// </remarks>
    [TestFixture]
    public class RegenTests
    {
        private MatchFactory.Match _match;
        private GameEngine _engine;
        private OperatorState _mover;
        private OperatorState _subject;

        [SetUp]
        public void SetUp()
        {
            _match = MatchFactory.CreateAlphaMatch(new[] { PlayerColor.Red }, seed: 11, openingDeployments: 3);
            _engine = _match.Engine;
            _engine.Start();

            _mover = _match.Operators.First(o => o.Name == "Bouncer");
            _subject = _match.Operators.First(o => o.Name == "Syla");
        }

        private int ExposedProgress() =>
            Enumerable.Range(1, _match.Map.Profile.TrackLength - 1)
                .First(p => _match.Map.IsOnOuterTrack(p) &&
                            !_match.Map.IsSafe(_match.Map.CellAt(PlayerColor.Red, p)));

        /// <summary>Rolls, spends the whole roll on the mover, and ends the turn. Returns the handover's events.</summary>
        private IReadOnlyList<IGameEvent> PassTurn()
        {
            _engine.Execute(new RollDiceCommand());
            _engine.Execute(new MoveCommand(_mover.Id));

            while (_engine.CanRollAgain)
            {
                _engine.Execute(new RollDiceCommand());
                _engine.Execute(new MoveCommand(_mover.Id));
            }

            var events = _engine.Execute(new EndTurnCommand());
            Assert.That(events.OfType<CommandRejected>(), Is.Empty, "the turn should hand over");
            return events;
        }

        private static IEnumerable<OperatorRegenerated> Regens(IReadOnlyList<IGameEvent> events, OperatorState op) =>
            events.OfType<OperatorRegenerated>().Where(e => e.Target == op);

        [Test]
        public void TheDefaults_AreReallyAssigned()
        {
            // The regression: both used to read 0 whatever was passed.
            Assert.That(CombatConfig.Default.RegenEveryTurns, Is.EqualTo(3));
            Assert.That(CombatConfig.Default.RegenAmount, Is.EqualTo(1));
            Assert.That(new CombatConfig(regenEveryTurns: 5, regenAmount: 2).RegenEveryTurns, Is.EqualTo(5));
        }

        [Test]
        public void ALightlyWoundedOperator_HealsOneAfterThreeOfItsTurns()
        {
            // One below max: the old below-half gate would never have let this heal.
            _subject.MoveTo(ExposedProgress());
            _subject.SetHealth(_subject.MaxHealth - 1);

            Assert.That(Regens(PassTurn(), _subject), Is.Empty, "first wounded upkeep");
            Assert.That(Regens(PassTurn(), _subject), Is.Empty, "second");

            var third = Regens(PassTurn(), _subject).ToList();

            Assert.That(third.Count, Is.EqualTo(1), "third upkeep ticks");
            Assert.That(third[0].Amount, Is.EqualTo(1));
            Assert.That(_subject.Health, Is.EqualTo(_subject.MaxHealth));
        }

        [Test]
        public void ADeeplyWoundedOperator_KeepsHealingEveryThirdTurn()
        {
            _subject.MoveTo(ExposedProgress());
            _subject.SetHealth(1);

            for (int turn = 0; turn < 6; turn++) PassTurn();

            Assert.That(_subject.Health, Is.EqualTo(3), "two ticks in six turns");
        }

        [Test]
        public void AnOperatorOnASafeCell_DoesNotHeal()
        {
            // Progress 0 is the start cell, which is safe.
            _subject.MoveTo(0);
            Assert.That(_match.Map.IsSafe(_match.Map.CellAt(PlayerColor.Red, 0)), Is.True, "precondition");
            _subject.SetHealth(1);

            for (int turn = 0; turn < 4; turn++)
                Assert.That(Regens(PassTurn(), _subject), Is.Empty);

            Assert.That(_subject.Health, Is.EqualTo(1));
        }

        [Test]
        public void SteppingOntoASafeCell_RestartsTheClock()
        {
            _subject.MoveTo(ExposedProgress());
            _subject.SetHealth(1);

            PassTurn();
            PassTurn();                     // two wounded upkeeps banked

            _subject.MoveTo(0);
            PassTurn();                     // sheltered: the streak resets

            _subject.MoveTo(ExposedProgress());
            Assert.That(Regens(PassTurn(), _subject), Is.Empty, "one");
            Assert.That(Regens(PassTurn(), _subject), Is.Empty, "two");
            Assert.That(Regens(PassTurn(), _subject).Count(), Is.EqualTo(1), "three, counted from scratch");
        }

        [Test]
        public void AnUnwoundedOperator_NeverReportsARegen()
        {
            _subject.MoveTo(ExposedProgress());

            for (int turn = 0; turn < 4; turn++)
                Assert.That(Regens(PassTurn(), _subject), Is.Empty);
        }
    }
}