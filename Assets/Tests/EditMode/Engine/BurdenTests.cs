// Assets/Tests/EditMode/Engine/BurdenTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Bots;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Engine
{
    /// <summary>
    /// Burdened (COMBAT_SYSTEMS §5.16, designer, 2026-09-17): haste run
    /// backwards. −1 cell when the roll totals 6 or less, −2 above, on the
    /// first move from each roll, never below 1 cell. Sanity's passive,
    /// replacing his 0.5 speed.
    /// </summary>
    /// <remarks>
    /// Driven through the engine, like <c>HasteCapTests</c>: the bookkeeping
    /// lives there. A solo seat of Sanity, Bouncer and Mimi, all deployed,
    /// so no aura or collision touches the distance measured.
    /// </remarks>
    [TestFixture]
    public class BurdenTests
    {
        private static CombatConfig Config => CombatConfig.Default;

        private static bool Low(DiceRoll r) => r.Total <= Config.HasteRollThreshold;

        private static int Burden(int rollTotal) => Config.BurdenCellsFor(rollTotal);

        private static OperatorState Named(MatchFactory.Match match, string name) =>
            match.Operators.First(o => o.Name == name);

        private static MatchFactory.Match RolledSquad(Func<DiceRoll, bool> wanted, out DiceRoll roll)
        {
            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
            {
                [PlayerColor.Red] = new[] { Sanity.Definition, Bouncer.Definition, Mimi.Definition }
            };

            for (int seed = 1; seed < 3000; seed++)
            {
                var match = MatchFactory.Create(new[] { PlayerColor.Red }, seed, squads, openingDeployments: 3);
                match.Engine.Start();

                var rolled = match.Engine.Execute(new RollDiceCommand()).OfType<DiceRolled>().FirstOrDefault();

                if (rolled != null && wanted(rolled.Roll))
                {
                    roll = rolled.Roll;
                    return match;
                }
            }

            throw new InvalidOperationException("No seed produced the roll this test needs.");
        }

        private static int Travelled(MatchFactory.Match match, ICommand move)
        {
            var moved = match.Engine.Execute(move).OfType<OperatorMoved>().FirstOrDefault();

            Assert.That(moved, Is.Not.Null, "the move should have been accepted");
            return moved.To - moved.From;
        }

        [Test]
        public void TheDesignersNumbers()
        {
            // 2026-09-17. Changing them should be a deliberate act that also
            // updates §5.16 and §10.8.
            Assert.That(Burden(2), Is.EqualTo(1));
            Assert.That(Burden(6), Is.EqualTo(1));
            Assert.That(Burden(7), Is.EqualTo(2));
            Assert.That(Burden(12), Is.EqualTo(2));

            var sanity = Roster.ByName("Sanity");
            Assert.That(sanity.BaseSpeed, Is.EqualTo(1.0));
            Assert.That(sanity.Passive, Is.EqualTo(StatusKind.Burdened));
            Assert.That(sanity.PassiveMagnitude, Is.EqualTo(0.0), "a burden is not speed");
        }

        [Test]
        public void ALowRoll_CostsOneCell()
        {
            var match = RolledSquad(r => !r.IsDouble && Low(r) && r.Total >= 3, out var roll);

            Assert.That(Travelled(match, new MoveCommand(Named(match, "Sanity").Id)), Is.EqualTo(roll.Total - 1));
        }

        [Test]
        public void AHighRoll_CostsTwoCells()
        {
            var match = RolledSquad(r => !r.IsDouble && !Low(r), out var roll);

            Assert.That(Travelled(match, new MoveCommand(Named(match, "Sanity").Id)), Is.EqualTo(roll.Total - 2));
        }

        [Test]
        public void ASplitRoll_PaysTheBurdenOnce_AndTheWholeRollSetsIt()
        {
            // Each die alone is 6 or less, but a high roll costs 2, on the
            // first move only.
            var match = RolledSquad(r => !r.IsDouble && !Low(r) && r.First >= 3, out var roll);
            var sanity = Named(match, "Sanity");

            int first = Travelled(match, new MoveCommand(sanity.Id, roll.First));
            int second = Travelled(match, new MoveCommand(sanity.Id, roll.Second));

            Assert.That(first, Is.EqualTo(roll.First - 2));
            Assert.That(second, Is.EqualTo(roll.Second));
        }

        [Test]
        public void AMove_NeverDropsBelowOneCell()
        {
            var match = RolledSquad(r => !r.IsDouble && (r.First == 1 || r.First == 2) && !Low(r), out var roll);

            Assert.That(Travelled(match, new MoveCommand(Named(match, "Sanity").Id, roll.First)), Is.EqualTo(1),
                "a spent die always moves (§6.3), burden or not");
        }

        [Test]
        public void ThePreview_ShowsTheBurden_ThenDropsIt()
        {
            var match = RolledSquad(r => !r.IsDouble && r.First >= 3, out var roll);
            var sanity = Named(match, "Sanity");

            var before = match.Engine.PreviewLandings()
                .First(p => p.OperatorId == sanity.Id && p.DieFace == roll.First);
            Assert.That(before.Cells, Is.EqualTo(roll.First - Burden(roll.Total)));

            match.Engine.Execute(new MoveCommand(sanity.Id, roll.First));

            var after = match.Engine.PreviewLandings()
                .First(p => p.OperatorId == sanity.Id && p.IsPooled);
            Assert.That(after.Cells, Is.EqualTo(roll.Second), "already paid this roll");
        }

        [Test]
        public void OnlyTheBurdenedOperatorPaysIt()
        {
            var match = RolledSquad(r => !r.IsDouble, out var roll);

            Assert.That(Travelled(match, new MoveCommand(Named(match, "Bouncer").Id)), Is.EqualTo(roll.Total));
        }

        [Test]
        public void HasteAndBurden_Cancel()
        {
            var match = RolledSquad(r => !r.IsDouble, out var roll);
            var sanity = Named(match, "Sanity");
            match.Statuses.Apply(sanity, StatusKind.Hastened, Config.HasteDurationTurns);

            Assert.That(Travelled(match, new MoveCommand(sanity.Id)), Is.EqualTo(roll.Total));
        }

        [Test]
        public void ASlow_BitesBeforeTheBurden()
        {
            // No more floor immunity: 0.5 with the half cell rounded up, then
            // the burden, then the one-cell clamp.
            var match = RolledSquad(r => !r.IsDouble && r.Total >= 7, out var roll);
            var sanity = Named(match, "Sanity");
            match.Statuses.Apply(sanity, StatusKind.Slow, 1);

            int slowed = (int)Math.Round(roll.Total * 0.5, MidpointRounding.AwayFromZero);

            Assert.That(Travelled(match, new MoveCommand(sanity.Id)), Is.EqualTo(Math.Max(1, slowed - 2)));
        }

        [Test]
        public void TheBotsDraft_ABurdenedOperator_AsSlowerThanItsBase()
        {
            Assert.That(DraftPicker.EffectiveSpeed(Sanity.Definition), Is.EqualTo(1.0 - 57.0 / 252.0).Within(1e-9));
            Assert.That(DraftPicker.EffectiveSpeed(Lethe.Definition), Is.EqualTo(1.0 + 57.0 / 252.0).Within(1e-9));
            Assert.That(DraftPicker.EffectiveSpeed(Kurbyn.Definition), Is.EqualTo(1.5), "a speed passive is still speed");
        }

        [Test]
        public void TheBurden_IsNotSpeed_AndACleanseCannotLiftIt()
        {
            var match = RolledSquad(r => true, out _);
            var sanity = Named(match, "Sanity");

            Assert.That(match.Statuses.SpeedModifier(sanity), Is.EqualTo(0.0));
            Assert.That(match.Statuses.IsBurdened(sanity), Is.True);

            match.Statuses.ClearApplied(sanity);

            Assert.That(match.Statuses.IsBurdened(sanity), Is.True, "a passive survives a cleanse (§1.2)");
            Assert.That(match.Engine.ActiveStatusesOn(sanity), Has.Member(StatusKind.Burdened), "the tag shows it");
        }
    }
}