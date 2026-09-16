// Assets/Tests/EditMode/Abilities/PredatorsReadTests.cs
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;
using NUnit.Framework;

namespace NonaRoyale.Core.Tests.Abilities
{
    /// <summary>
    /// Kurbyn's Predator's Read: the watch shape of the operator-anchored
    /// deferred registry (§6.7). A marker the target carries until Kurbyn's
    /// next upkeep; the target's first <b>dice movement</b> trips it for
    /// 2 Normal, once — and placement never trips it (§7.4).
    /// </summary>
    [TestFixture]
    public class PredatorsReadTests
    {
        private PathMap _map;
        private FakeClock _clock;
        private StatusRegistry _statuses;
        private TargetingRules _targeting;
        private EnergyLedger _energy;
        private DamagePipeline _damage;
        private DeferredCellEffects _cellEffects;
        private DeferredOperatorEffects _operatorEffects;
        private AbilityResolver _abilities;
        private NeutralizeRules _neutralize;

        private OperatorState _kurbyn;
        private OperatorState _kian;
        private OperatorState _target;
        private OperatorState _mimi;
        private OperatorState _bouncer;
        private PlayerState _red;
        private PlayerState _blue;
        private List<OperatorState> _board;

        [SetUp]
        public void SetUp()
        {
            _map = new PathMap(BoardProfile.Standard);
            _clock = new FakeClock();
            _statuses = new StatusRegistry(_clock, CombatConfig.Default);
            _targeting = new TargetingRules(_map, _statuses);
            _energy = new EnergyLedger(EnergyConfig.Default);
            _damage = new DamagePipeline(_statuses, new SeededRandom(1));
            _cellEffects = new DeferredCellEffects(_clock, _targeting, _damage, _statuses);
            _operatorEffects = new DeferredOperatorEffects(_clock, _targeting, _damage, _statuses);
            _abilities = new AbilityResolver(
                _map, _clock, _energy, _statuses, _targeting, _damage, _cellEffects, _operatorEffects);

            // Positions are TRACK cells, never progress, and none is a start
            // cell (1, 14, 27, 40), so nothing here is safe. Kurbyn at 10, two
            // behind the target at 12 — inside the read's range 3. Kian beside
            // him at 11 so Sonic Disrupter's radius reaches the target. Mimi at
            // 9 and the Bouncer at 15: both close enough for their placement
            // tools, and placed so neither a swap nor a pull carries the
            // target across its own progress-0 boundary, which §7.4 refuses.
            _kurbyn = AtTrack(1, "Kurbyn", PlayerColor.Red, Kurbyn.MaxHealth, 10);
            _kian = AtTrack(2, "Kian", PlayerColor.Red, 7, 11);
            _target = AtTrack(3, "Target", PlayerColor.Blue, 6, 12);
            _mimi = AtTrack(4, "Mimi", PlayerColor.Blue, 6, 9);
            _bouncer = AtTrack(5, "Bouncer", PlayerColor.Blue, 9, 15);

            _red = new PlayerState(PlayerColor.Red, new[] { _kurbyn, _kian });
            _blue = new PlayerState(PlayerColor.Blue, new[] { _target, _mimi, _bouncer });
            _board = new List<OperatorState> { _kurbyn, _kian, _target, _mimi, _bouncer };

            _neutralize = new NeutralizeRules(
                _statuses, _abilities, _energy, _board,
                new PlayerState[] { _red, _blue }, CombatConfig.Default, _operatorEffects);

            _clock.BeginTurnFor(PlayerColor.Red);
            _red.BeginTurn();
            Fund(_red, 12);
        }

        private OperatorState AtTrack(int id, string name, PlayerColor owner, int hp, int track)
        {
            var op = new OperatorState(id, name, owner, hp, 1.0);
            op.MoveTo(ProgressAtTrack(owner, track));
            return op;
        }

        private int ProgressAtTrack(PlayerColor owner, int track)
        {
            int circuit = _map.Profile.CircuitLength;
            int start = _map.StartTrackIndex(owner);

            return ((track - start) % circuit + circuit) % circuit;
        }

        private int TrackOf(OperatorState op) => _map.CellAt(op.Owner, op.Progress).Index;

        /// <summary>Tops a pool up to a known figure without going through the dice.</summary>
        private void Fund(PlayerState player, int amount)
        {
            _energy.GrantForTurn(player, new DiceRoll(6, 6));   // +6
            player.BeginTurn();
            _energy.GrantForTurn(player, new DiceRoll(6, 6));   // +6, capped at 12
            if (amount < 12) _energy.Spend(player, 12 - amount);
        }

        private AbilityResolution Cast(OperatorState target) =>
            _abilities.Use(_kurbyn, Kurbyn.PredatorsRead, target, _red, _board);

        /// <summary>Past the target's turn to Red's next upkeep, when a watch lapses.</summary>
        private void AdvanceToCasterUpkeep()
        {
            _clock.BeginTurnFor(PlayerColor.Blue);
            _clock.BeginTurnFor(PlayerColor.Red);
        }

        private IReadOnlyList<OperatorEffectResolution> Fire() =>
            _operatorEffects.Fire(PlayerColor.Red, _board);

        // ── The cast ─────────────────────────────────────────────────────

        [Test]
        public void Cast_MarksTheTarget_AndTelegraphsIt_ButStrikesNothingYet()
        {
            // The telegraph is the counterplay: an event announces the read and
            // the marker keeps it on the board, but nobody is struck by the
            // cast itself.
            var result = Cast(_target);

            Assert.That(result.Approved, Is.True);
            Assert.That(result.Outcomes.Any(o => o.Kind == EffectOutcomeKind.WatchMarked), Is.True,
                "the telegraph the ability is balanced around");
            Assert.That(_operatorEffects.HasWatchOn(_target, PlayerColor.Red), Is.True);
            Assert.That(_operatorEffects.HasFollowUpOn(_target, PlayerColor.Red), Is.False,
                "a watch is not a follow-up");
            Assert.That(_target.Health, Is.EqualTo(6), "nothing strikes at cast time");

            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.Has(_target, StatusKind.Watched), Is.True,
                "the marker takes hold on the target's next turn, like any debuff");
        }

        [Test]
        public void Cast_OnAnAlly_IsRefusedAsWrongSide()
        {
            int before = _red.Energy;

            var result = Cast(_kian);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.TargetingVerdict, Is.EqualTo(TargetingVerdict.WrongSide));
            Assert.That(_red.Energy, Is.EqualTo(before), "a refusal costs nothing");
        }

        [Test]
        public void Cast_WithoutTheEnergy_IsRefused()
        {
            Fund(_red, 2);   // one short of the read's 3

            var result = Cast(_target);

            Assert.That(result.Approved, Is.False);
            Assert.That(result.Refusal, Is.EqualTo(AbilityRefusal.InsufficientEnergy));
            Assert.That(_red.Energy, Is.EqualTo(2), "a refusal costs nothing");
            Assert.That(_operatorEffects.HasWatchOn(_target, PlayerColor.Red), Is.False);
        }

        [Test]
        public void Cast_OnCooldown_IsRefused()
        {
            var first = Cast(_target);
            Assert.That(first.Approved, Is.True, "precondition");
            int afterCast = _red.Energy;

            AdvanceToCasterUpkeep();   // Red's second turn; cooldown 2 sits out two
            var second = Cast(_target);

            Assert.That(second.Approved, Is.False);
            Assert.That(second.Refusal, Is.EqualTo(AbilityRefusal.OnCooldown));
            Assert.That(_red.Energy, Is.EqualTo(afterCast), "a refusal costs nothing");
        }

        // ── The trigger ──────────────────────────────────────────────────

        [Test]
        public void DiceMovement_TripsTheWatch_ForTwoNormal_ExactlyOnce()
        {
            Cast(_target);
            _clock.BeginTurnFor(PlayerColor.Blue);

            _target.MoveTo(ProgressAtTrack(PlayerColor.Blue, 16));
            var tripped = _operatorEffects.NotifyDiceMovement(_target);

            Assert.That(tripped.Count, Is.EqualTo(1));
            Assert.That(tripped[0].Cause, Is.EqualTo(DeferredOperatorEffects.WatchCause));
            Assert.That(tripped[0].DamagePerTarget, Is.EqualTo(2));
            Assert.That(tripped[0].Cell, Is.EqualTo(CellRef.Track(16)), "it strikes where the move ended");
            Assert.That(tripped[0].Target, Is.SameAs(_target));
            Assert.That(_target.Health, Is.EqualTo(4));
            Assert.That(_statuses.Has(_target, StatusKind.Watched), Is.False, "sprung is spent");
            Assert.That(_operatorEffects.HasWatchOn(_target, PlayerColor.Red), Is.False);

            // A second move in the same turn springs nothing: the read is gone.
            _target.MoveTo(ProgressAtTrack(PlayerColor.Blue, 17));
            Assert.That(_operatorEffects.NotifyDiceMovement(_target), Is.Empty);
            Assert.That(_target.Health, Is.EqualTo(4));
        }

        [Test]
        public void TheStrike_IsNormal_AndAShieldAbsorbsIt()
        {
            // Pipeline mitigations apply: a plate eats the whole hit, and the
            // read is spent either way.
            Cast(_target);
            _statuses.Apply(_target, StatusKind.Shield, duration: 2, magnitude: 2);

            _clock.BeginTurnFor(PlayerColor.Blue);
            _target.MoveTo(ProgressAtTrack(PlayerColor.Blue, 16));
            var tripped = _operatorEffects.NotifyDiceMovement(_target);

            Assert.That(tripped.Count, Is.EqualTo(1));
            Assert.That(tripped[0].Damage[0].Outcome, Is.EqualTo(DamageOutcome.Absorbed),
                "Normal damage meets the shield; Atomic would have gone through");
            Assert.That(_target.Health, Is.EqualTo(6));
            Assert.That(_operatorEffects.HasWatchOn(_target, PlayerColor.Red), Is.False,
                "absorbed is still sprung");
        }

        // ── The escape hatches ───────────────────────────────────────────

        [Test]
        public void StandingStill_TheReadLapsesAtKurbynsNextUpkeep_AndNothingHappens()
        {
            // The denial was the value: a target that never moves is never
            // struck, and the lapsed read retires silently with its badge.
            Cast(_target);

            AdvanceToCasterUpkeep();
            var fired = Fire();

            Assert.That(fired, Is.Empty, "a watch has no upkeep resolution — its moment was the move");
            Assert.That(_operatorEffects.HasWatchOn(_target, PlayerColor.Red), Is.False);
            Assert.That(_statuses.Has(_target, StatusKind.Watched), Is.False,
                "a spent read does not keep drawing a badge");
            Assert.That(_target.Health, Is.EqualTo(6));

            // A move after the lapse springs nothing.
            _target.MoveTo(ProgressAtTrack(PlayerColor.Blue, 16));
            Assert.That(_operatorEffects.NotifyDiceMovement(_target), Is.Empty);
        }

        [Test]
        public void Placement_ASwap_DoesNotTripTheWatch()
        {
            // §7.4: placement never triggers anything. The target changes
            // cells without spending its move, and the read keeps waiting.
            Cast(_target);
            _clock.BeginTurnFor(PlayerColor.Blue);
            Fund(_blue, 12);

            var swap = _abilities.Use(_mimi, Mimi.Translocation, _target, _blue, _board);

            Assert.That(swap.Approved, Is.True, "precondition");
            Assert.That(TrackOf(_target), Is.EqualTo(9), "the placement happened");
            Assert.That(_target.Health, Is.EqualTo(6), "and the read did not answer it");
            Assert.That(_operatorEffects.HasWatchOn(_target, PlayerColor.Red), Is.True);

            // The read was live throughout: a dice move still springs it.
            _target.MoveTo(ProgressAtTrack(PlayerColor.Blue, 10));
            var tripped = _operatorEffects.NotifyDiceMovement(_target);
            Assert.That(tripped.Count, Is.EqualTo(1));
            Assert.That(_target.Health, Is.EqualTo(4));
        }

        [Test]
        public void Placement_APull_DoesNotTripTheWatch()
        {
            // Velvet Rope on an ally is pure placement: repositioned, unharmed.
            Cast(_target);
            _clock.BeginTurnFor(PlayerColor.Blue);
            Fund(_blue, 12);

            int trackBefore = TrackOf(_target);
            var pull = _abilities.Use(_bouncer, Bouncer.VelvetRope, _target, _blue, _board);

            Assert.That(pull.Approved, Is.True, "precondition");
            Assert.That(TrackOf(_target), Is.Not.EqualTo(trackBefore), "the placement happened");
            Assert.That(_target.Health, Is.EqualTo(6), "and the read did not answer it");
            Assert.That(_operatorEffects.HasWatchOn(_target, PlayerColor.Red), Is.True);
        }

        [Test]
        public void Placement_APush_DoesNotTripTheWatch()
        {
            // The push's own 2 Normal lands — that is the disrupter, not the
            // read. The read is still waiting afterwards, and a dice move then
            // springs it for its own 2.
            Cast(_target);

            // Re-staged off the target's home mouth: at track 12 it sits one
            // step short of it, and a forward push clamps there (§7.4) — a
            // shove that moves nobody proves nothing about the watch.
            _target.MoveTo(ProgressAtTrack(PlayerColor.Blue, 16));
            _kian.MoveTo(ProgressAtTrack(PlayerColor.Red, 15));

            var push = _abilities.Use(_kian, Kian.SonicDisrupter, null, _red, _board);

            Assert.That(push.Approved, Is.True, "precondition");
            Assert.That(TrackOf(_target), Is.Not.EqualTo(16), "the placement happened");
            Assert.That(_target.Health, Is.EqualTo(4), "exactly the disrupter's damage, no more");
            Assert.That(_operatorEffects.HasWatchOn(_target, PlayerColor.Red), Is.True);

            _clock.BeginTurnFor(PlayerColor.Blue);
            _target.MoveTo(ProgressAtTrack(PlayerColor.Blue, 20));
            var tripped = _operatorEffects.NotifyDiceMovement(_target);
            Assert.That(tripped.Count, Is.EqualTo(1));
            Assert.That(_target.Health, Is.EqualTo(2), "the push's two, then the read's two");
        }

        [Test]
        public void Cleanse_CancelsTheWatch_AndNoStrikeFollows()
        {
            // Neural Purge's answer, invoked through the same registry call it
            // makes: stripping the marker cancels the read outright (§5.15).
            Cast(_target);
            _clock.BeginTurnFor(PlayerColor.Blue);
            Assert.That(_statuses.Has(_target, StatusKind.Watched), Is.True, "precondition");

            _statuses.ClearApplied(_target);

            _target.MoveTo(ProgressAtTrack(PlayerColor.Blue, 16));
            Assert.That(_operatorEffects.NotifyDiceMovement(_target), Is.Empty,
                "a cleansed read springs nothing");
            Assert.That(_operatorEffects.HasWatchOn(_target, PlayerColor.Red), Is.False,
                "and the orphaned entry is retired rather than left to its upkeep");
            Assert.That(_target.Health, Is.EqualTo(6));

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(Fire(), Is.Empty, "nothing is left to lapse, either");
        }

        // ── Death on both sides ──────────────────────────────────────────

        [Test]
        public void TargetNeutralized_TheReadDiesWithIt_AndARedeployedTargetIsClean()
        {
            // Neutralize strips every applied status (§1.2), the marker is the
            // attachment, so the read dies with the target. A re-deployed
            // target carrying the same piece identity is clean.
            Cast(_target);
            _clock.BeginTurnFor(PlayerColor.Blue);

            var killed = _damage.Apply(_target, new DamageInstance(6, DamageType.Atomic, _kian.Id, "ability"));
            Assert.That(killed.Outcome, Is.EqualTo(DamageOutcome.Neutralized), "precondition");
            _neutralize.Apply(_target, _kian.Id);
            Assert.That(_target.IsInYard, Is.True, "precondition");

            // Re-entered and moving: no marker, no strike.
            _target.MoveTo(ProgressAtTrack(PlayerColor.Blue, 16));
            Assert.That(_operatorEffects.NotifyDiceMovement(_target), Is.Empty);
            Assert.That(_target.Health, Is.EqualTo(6), "re-entry restored it, and the read is gone");

            _clock.BeginTurnFor(PlayerColor.Red);
            Assert.That(Fire(), Is.Empty, "the orphaned entry retires at the upkeep");
            Assert.That(_operatorEffects.HasWatchOn(_target, PlayerColor.Red), Is.False);
        }

        [Test]
        public void KurbynNeutralized_TheReadStillTrips_AndCreditsHim()
        {
            // The condition reads only the target's conduct, never Kurbyn's
            // position — the charge's deployed-device precedent (ADR-0006),
            // not the follow-up's. Kurbyn in his yard changes nothing.
            Cast(_target);

            var killed = _damage.Apply(_kurbyn, new DamageInstance(
                Kurbyn.MaxHealth, DamageType.Atomic, _target.Id, "ability"));
            Assert.That(killed.Outcome, Is.EqualTo(DamageOutcome.Neutralized), "precondition");
            _neutralize.Apply(_kurbyn, _target.Id);
            Assert.That(_kurbyn.IsInYard, Is.True, "precondition");

            _clock.BeginTurnFor(PlayerColor.Blue);
            _target.MoveTo(ProgressAtTrack(PlayerColor.Blue, 16));
            var tripped = _operatorEffects.NotifyDiceMovement(_target);

            Assert.That(tripped.Count, Is.EqualTo(1));
            Assert.That(tripped[0].SourceOperatorId, Is.EqualTo(_kurbyn.Id), "a kill still credits him");
            Assert.That(_target.Health, Is.EqualTo(4));
        }

        // ── The registry's duplicate answer ──────────────────────────────

        [Test]
        public void ReSettingAWatch_ReplacesRatherThanStacks()
        {
            // The registry's answer for every shape: one pending entry per
            // shape per target per seat, so a second setting refreshes the
            // first. Tested at the registry because Predator's Read's cooldown
            // outlasts its own marker — the duplicate case is unreachable
            // through the ability in a real match.
            _statuses.Apply(_target, StatusKind.Watched,
                DeferredOperatorEffects.MarkerDurationTurns, sourceOperatorId: _kurbyn.Id);
            _operatorEffects.SetWatch(_target, PlayerColor.Red, _kurbyn.Id, 2, DamageType.Normal);
            _operatorEffects.SetWatch(_target, PlayerColor.Red, _kurbyn.Id, 2, DamageType.Normal);

            _clock.BeginTurnFor(PlayerColor.Blue);
            _target.MoveTo(ProgressAtTrack(PlayerColor.Blue, 16));
            var tripped = _operatorEffects.NotifyDiceMovement(_target);

            Assert.That(tripped.Count, Is.EqualTo(1), "one watch, one strike");
            Assert.That(_target.Health, Is.EqualTo(4));
            Assert.That(_operatorEffects.HasWatchOn(_target, PlayerColor.Red), Is.False);
        }

        // ── Roster ───────────────────────────────────────────────────────

        [Test]
        public void PredatorsRead_FillsKurbynsThirdSlot_WithTheApprovedNumbers()
        {
            var kurbyn = Roster.ByName("Kurbyn");

            Assert.That(kurbyn.Abilities.Count, Is.EqualTo(3),
                "three actives plus the passive — the pool is complete");
            Assert.That(Kurbyn.PredatorsRead.Id, Is.EqualTo(303));
            Assert.That(Kurbyn.PredatorsRead.EnergyCost, Is.EqualTo(3),
                "the cheapest rung: his lowest cast was 6 before (2026-09-16)");
            Assert.That(Kurbyn.PredatorsRead.CooldownTurns, Is.EqualTo(2));
            Assert.That(Kurbyn.PredatorsRead.Range, Is.EqualTo(3));
            Assert.That(Kurbyn.PredatorsRead.RequiresTarget, Is.True);
            Assert.That(Kurbyn.PredatorsRead.Description.Any(char.IsDigit), Is.False,
                "descriptions carry no numbers — the fields beside them do");
        }

        // ── Engine wiring ────────────────────────────────────────────────

        [Test]
        public void Engine_ADiceMoveByTheMarkedTarget_TripsTheWatch()
        {
            // The registry hook lives on GameEngine's move path; this walks the
            // whole command route so the wiring itself is under test.
            var match = MatchFactory.Create(
                new[] { PlayerColor.Red, PlayerColor.Blue }, seed: 11,
                squads: new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>
                {
                    [PlayerColor.Red] = new[]
                    {
                        Kurbyn.Definition, Bouncer.Definition, Syla.Definition
                    },
                    [PlayerColor.Blue] = new[]
                    {
                        Bouncer.Definition, Syla.Definition, Mimi.Definition
                    },
                });
            var engine = match.Engine;
            engine.Start();

            var kurbyn = match.Operators.First(o => o.Owner == PlayerColor.Red && o.Name == "Kurbyn");
            var quarry = match.Operators.First(o => o.Owner == PlayerColor.Blue && o.Name == "Bouncer");

            // Nobody is on the board, so no seat owes a move: end turns until
            // Red holds a roll worth at least 3 energy (grant is half the
            // total, §3.1).
            bool holding = false;
            for (int i = 0; i < 400 && !holding; i++)
            {
                var rolled = engine.Execute(new RollDiceCommand());
                var dice = rolled.OfType<DiceRolled>().FirstOrDefault();

                if (engine.CurrentPlayer.Color == PlayerColor.Red && dice != null && dice.Roll.Total >= 6)
                {
                    holding = true;
                }
                else
                {
                    engine.Execute(new EndTurnCommand());
                }
            }

            Assert.That(holding, Is.True, "the dice never produced a fundable Red turn");

            // Kurbyn steps out; the quarry stands two cells BEHIND him, inside
            // the read's range 3, so Kurbyn's own compulsory move carries him
            // away from it rather than onto it.
            var map = match.Map;
            kurbyn.MoveTo(5);
            int kurbynCell = map.CellAt(PlayerColor.Red, 5).Index;
            int quarryCell = (kurbynCell - 2 + map.Profile.CircuitLength) % map.Profile.CircuitLength;
            int quarryProgress =
                ((quarryCell - map.StartTrackIndex(PlayerColor.Blue)) % map.Profile.CircuitLength
                 + map.Profile.CircuitLength) % map.Profile.CircuitLength;
            quarry.MoveTo(quarryProgress);

            var cast = engine.Execute(
                new UseAbilityCommand(kurbyn.Id, Kurbyn.PredatorsRead.Id, quarry.Id));

            Assert.That(cast.Any(e => e is CommandRejected), Is.False,
                "the cast is legal: in range, in play, funded");
            Assert.That(cast.Any(e => e is WatchMarked), Is.True, "the telegraph event");
            Assert.That(cast.Any(e => e is WatchTripped), Is.False, "nothing has moved yet");

            // Red's move is compulsory: Kurbyn spends the roll walking away.
            engine.Execute(new MoveCommand(kurbyn.Id));
            engine.Execute(new EndTurnCommand());

            // Blue's turn: the quarry moves by dice, and the read answers.
            engine.Execute(new RollDiceCommand());
            var move = engine.Execute(new MoveCommand(quarry.Id));

            Assert.That(move.Any(e => e is WatchTripped), Is.True,
                "the dice move is the trigger");
            var hit = move.OfType<DamageDealt>()
                .FirstOrDefault(d => d.Cause == DeferredOperatorEffects.WatchCause);
            Assert.That(hit, Is.Not.Null, "the strike reports its cause for the view");
            Assert.That(hit.Target, Is.SameAs(quarry));
            Assert.That(hit.Amount, Is.EqualTo(2));
        }
    }
}
