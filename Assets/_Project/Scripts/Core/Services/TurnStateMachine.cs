// Assets/_Project/Scripts/Core/Services/TurnStateMachine.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Drives the turn: upkeep, roll, action, end (COMBAT_SYSTEMS §6).
    /// </summary>
    /// <remarks>
    /// <b>It orchestrates, it does not decide.</b> Upkeep damage goes through the
    /// pipeline, energy through the ledger, expiry through the registry, beacons
    /// through <see cref="DeferredCellEffects"/>. Nothing here re-implements a
    /// rule that lives elsewhere — this type owns only the order things happen
    /// in, which is itself a rule and the one no other service can own.
    ///
    /// <b>The phases are enforced.</b> Rolling before upkeep, or twice without
    /// doubles, throws rather than silently misbehaving. An illegal sequence is a
    /// bug in the caller and should be loud.
    ///
    /// <b>The action phase is the caller's.</b> Deploying, moving and using
    /// abilities happen between <see cref="Roll"/> and <see cref="EndTurn"/>,
    /// through the services that own them. The machine only bounds the window.
    /// </remarks>
    public sealed class TurnStateMachine
    {
        private readonly IReadOnlyList<PlayerState> _players;

        /// <summary>The seats in turn order. Read-only; for queries that look across the table.</summary>
        public IReadOnlyList<PlayerState> Players => _players;
        private readonly MatchClock _clock;
        private readonly GameConfig _config;
        private readonly IRandom _random;
        private readonly EnergyLedger _energy;
        private readonly StatusRegistry _statuses;
        private readonly DamagePipeline _damage;
        private readonly NeutralizeRules _neutralize;
        private readonly WinConditions _win;
        private readonly DeferredCellEffects _cellEffects;
        private readonly DeferredOperatorEffects _operatorEffects;

        /// <summary>
        /// Every operator in the match, flattened once at construction.
        /// </summary>
        /// <remarks>
        /// A beacon strikes the current player's <i>enemies</i>, who are not in
        /// the loop over <c>CurrentPlayer.Operators</c> that bleed and marks use.
        /// Built here rather than taken as a constructor argument because squads
        /// are fixed for the life of a match and the machine already holds the
        /// players they came from — a second list to keep in step would be a
        /// second thing that can disagree.
        /// </remarks>
        private readonly List<OperatorState> _allOperators = new List<OperatorState>();

        private int _seatIndex = -1;
        private int _rollsThisTurn;
        private bool _lastRollWasDouble;

        public TurnStateMachine(
            IReadOnlyList<PlayerState> players,
            MatchClock clock,
            GameConfig config,
            IRandom random,
            EnergyLedger energy,
            StatusRegistry statuses,
            DamagePipeline damage,
            NeutralizeRules neutralize,
            WinConditions win,
            DeferredCellEffects cellEffects,
            DeferredOperatorEffects operatorEffects = null)
        {
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _energy = energy ?? throw new ArgumentNullException(nameof(energy));
            _statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _neutralize = neutralize ?? throw new ArgumentNullException(nameof(neutralize));
            _win = win ?? throw new ArgumentNullException(nameof(win));
            _cellEffects = cellEffects ?? throw new ArgumentNullException(nameof(cellEffects));

            // Optional for the same reason AbilityResolver's is: fixtures older
            // than Zero-Day build the machine without it, and one is frozen
            // red. Null means no operator-anchored charges ever fire.
            _operatorEffects = operatorEffects;

            if (_players.Count == 0)
                throw new ArgumentException("A match needs at least one player.", nameof(players));

            foreach (var player in _players) _allOperators.AddRange(player.Operators);
        }

        public TurnPhase Phase { get; private set; } = TurnPhase.BetweenTurns;

        /// <summary>Null between turns.</summary>
        public PlayerState CurrentPlayer { get; private set; }

        public int RollsRemaining => Math.Max(0, _config.MaxRollsPerTurn - _rollsThisTurn);

        /// <summary>The energy cap every pool is held to (§3.1).</summary>
        public int EnergyCap => _energy.Cap;

        /// <summary>What one cashed die pays (§3.4).</summary>
        public int CashedDieEnergy => _energy.CashedDieEnergy;

        /// <summary>The most a seat can owe (§3.3).</summary>
        public int DebtCap => _energy.DebtCap;

        /// <summary>
        /// The current round: 1 while the first seat's first turn has not yet
        /// been followed by its second, and so on.
        /// </summary>
        /// <remarks>
        /// The highest turn count any seat has reached. The first seat always
        /// starts a round, so its count is the round, and taking the maximum
        /// saves this from having to know which seat that is. 0 before the
        /// match starts.
        /// </remarks>
        public int Round
        {
            get
            {
                int round = 0;
                foreach (var player in _players)
                    if (player.TurnIndex > round) round = player.TurnIndex;

                return round;
            }
        }

        /// <summary>
        /// Whether the player may roll again. Doubles buy an extra movement
        /// roll, bounded by <see cref="GameConfig.MaxRollsPerTurn"/> so one hot
        /// streak cannot run forever.
        /// </summary>
        public bool CanRollAgain =>
            Phase == TurnPhase.Action && _lastRollWasDouble && RollsRemaining > 0;

        /// <summary>
        /// Starts the next seat's turn and runs upkeep: bleed ticks, then mark
        /// ticks, then due beacons, then evasion charges re-arm. Cooldowns need
        /// no work — they are absolute turn indices, so advancing the turn
        /// advances them.
        /// </summary>
        /// <remarks>
        /// <b>Beacons fire last, and as a separate pass.</b> The bleed and mark
        /// loop walks the current player's own operators, because that is who
        /// carries those statuses; a beacon belongs to the current player and
        /// strikes everybody else, so it cannot ride in that loop at all. Placing
        /// it after leaves the two tested orders untouched (ADR-0006).
        ///
        /// <b>An operator killed by a beam still never gets its turn</b>, the same
        /// as one killed by bleed — but the operator that dies is an opponent, so
        /// what it loses is the rest of its lap rather than the turn now starting.
        /// </remarks>
        public UpkeepReport BeginTurn()
        {
            RequirePhase(TurnPhase.BetweenTurns, nameof(BeginTurn));

            _seatIndex = (_seatIndex + 1) % _players.Count;
            CurrentPlayer = _players[_seatIndex];
            _clock.BeginTurnFor(CurrentPlayer);

            _rollsThisTurn = 0;
            _lastRollWasDouble = false;

            var ticks = new List<DamageResult>();
            var neutralized = new List<UpkeepNeutralize>();

            foreach (var op in CurrentPlayer.Operators)
            {
                _statuses.RefreshEvasion(op);

                // Both sources are read before any damage lands. Neutralizing
                // clears every status, so a mark that kills — or a bleed that
                // kills a marked operator — would otherwise erase the source
                // that Tagged From Above's payout has to credit (§10.2).
                int markDamage = _statuses.MarkTickDamage(op);
                int markSource = _statuses.MarkedBy(op) ?? op.Id;

                int bleedDamage = _statuses.ConsumeBleed(op);

                // Bleed first, then marks. Nothing on the alpha roster
                // distinguishes the order, but an unspecified one is a bug
                // waiting for the operator that cares (§6). Both are Atomic, so
                // both go around evasion and shields, and an operator that dies
                // here never gets its turn.
                if (!TickUpkeepDamage(op, bleedDamage, op.Id, "bleed", ticks, neutralized))
                    continue;

                TickUpkeepDamage(op, markDamage, markSource, "mark", ticks, neutralized);
            }

            var beacons = FireBeacons(neutralized);
            var charges = FireCharges(neutralized);

            Phase = TurnPhase.AwaitingRoll;
            return new UpkeepReport(CurrentPlayer.Color, ticks, neutralized, beacons, charges);
        }

        /// <summary>
        /// Rolls the dice and, on the turn's first roll only, grants energy.
        /// </summary>
        /// <remarks>
        /// Doubles grant an extra movement roll but never a second energy grant —
        /// otherwise one double snowballs both axes at once (§3.1). The ledger
        /// enforces that itself; this method simply calls it every time.
        /// </remarks>
        public RollReport Roll()
        {
            if (Phase != TurnPhase.AwaitingRoll && !CanRollAgain)
            {
                throw new InvalidOperationException(
                    Phase == TurnPhase.Action
                        ? "A second roll needs doubles and a remaining roll in the budget."
                        : $"Cannot roll during {Phase}.");
            }

            var roll = DiceRoll.Roll(_random, _config);
            _rollsThisTurn++;
            _lastRollWasDouble = roll.IsDouble;

            var grant = _energy.GrantForTurn(CurrentPlayer, roll);

            Phase = TurnPhase.Action;
            return new RollReport(roll, grant, CanRollAgain, RollsRemaining);
        }

        /// <summary>
        /// Cashes one die for the seat (§3.4): the pool gains the configured
        /// figure, filling to the cap. Fortuna's House Edge.
        /// </summary>
        /// <remarks>
        /// The die itself belongs to <c>GameEngine</c>, which removes it and
        /// decides whether cashing was legal at all. This owns only the money,
        /// because the ledger lives here.
        /// </remarks>
        public EnergyGrant CashDie(PlayerState player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));

            return _energy.GrantCash(player, _energy.CashedDieEnergy);
        }

        /// <summary>
        /// Grants the extra roll a <i>dealt</i> double is owed (§6.8), inside the
        /// same budget a rolled one obeys. Returns whether the roll was granted.
        /// Fortuna's Boxcars.
        /// </summary>
        /// <remarks>
        /// <b>Setting a double and then denying the roll it is owed would be a
        /// special case</b>, and the roster avoids those. The budget still binds:
        /// at <see cref="GameConfig.MaxRollsPerTurn"/> the faces change and
        /// nothing else does.
        /// </remarks>
        public bool GrantDealtDouble()
        {
            if (Phase != TurnPhase.Action || RollsRemaining <= 0) return false;

            _lastRollWasDouble = true;
            return true;
        }

        /// <summary>
        /// Burns a seat's whole debt (§3.3): one of its operators has collided
        /// with a creditor. Returns what was cleared. The debt lives in the
        /// ledger, and the ledger lives here.
        /// </summary>
        public int BurnDebt(PlayerState debtor)
        {
            if (debtor == null) throw new ArgumentNullException(nameof(debtor));

            return _energy.WriteOffDebt(debtor);
        }

        /// <summary>One die from the match's own stream (§6.8). Fortuna's Deal Again.</summary>
        /// <remarks>
        /// The same stream and the same bounds the roll itself uses, so a re-roll
        /// is an ordinary die — and a seed still reproduces the whole match, with
        /// the stream shifted by every die she re-deals.
        /// </remarks>
        public int RollOneDie() => _random.NextInt(1, _config.DiceSides + 1);

        /// <summary>
        /// Closes the turn: the seat's debt is collected, status durations
        /// expire, then the win check runs.
        /// </summary>
        /// <remarks>
        /// <b>Debt is collected here, not at upkeep</b> (§3.3). A debt run up on
        /// an opponent's turn is paid from what the seat chose to keep after its
        /// own turn, income included, so paying and spending are a decision it
        /// makes with the bill in front of it. At upkeep the first payment would
        /// come out of a pool spent before the debt existed.
        ///
        /// Expiry sits here rather than at upkeep so a 1-turn stun applied during
        /// an opponent's turn blocks a real action phase before it lapses (§6).
        ///
        /// <b>It accepts <see cref="TurnPhase.AwaitingRoll"/>, and that is not a
        /// missing rule.</b> Rolling is compulsory, but that is enforced once, in
        /// <c>GameEngine.EndTurn</c>, because its twin — compulsory movement —
        /// depends on the unspent dice, which only the engine tracks. Splitting
        /// "you must play your turn" across two files, half throwing and half
        /// refusing, would cost more than it bought. This method stays permissive
        /// so fixtures can drive the machine directly; no production path reaches
        /// it without a roll. <b>Do not add a second check here.</b>
        /// </remarks>
        public EndTurnReport EndTurn()
        {
            if (Phase != TurnPhase.Action && Phase != TurnPhase.AwaitingRoll)
                throw new InvalidOperationException($"Cannot end a turn during {Phase}.");

            var debt = _energy.CollectDebt(CurrentPlayer);

            var expired = new List<ExpiredStatus>();

            foreach (var op in CurrentPlayer.Operators)
            {
                foreach (var kind in _statuses.ExpireCompleted(op))
                    expired.Add(new ExpiredStatus(op, kind));
            }

            // One call, both answers: asking Winner() and WinningSeats()
            // separately would walk the board twice and let the two disagree.
            var winningSeats = _win.WinningSeats(_players);
            PlayerColor? winner = winningSeats.Count == 0 ? (PlayerColor?)null : winningSeats[0];

            Phase = winner == null ? TurnPhase.BetweenTurns : TurnPhase.MatchOver;
            var report = new EndTurnReport(CurrentPlayer.Color, expired, winner, winningSeats, debt);

            if (winner == null) CurrentPlayer = null;
            return report;
        }

        /// <summary>
        /// Resolves every beacon of the current seat that has come due, and folds
        /// any kills into the upkeep's neutralize list (ADR-0006).
        /// </summary>
        /// <remarks>
        /// <b>The damage is already applied</b> by the time this sees it —
        /// <see cref="DeferredCellEffects"/> owns the split and the pipeline call,
        /// the same division of labour <c>CollisionResolver</c> has. What is left
        /// here is the one thing the registry deliberately does not know about:
        /// the consequences of reaching zero.
        ///
        /// <b>Kills are reported through the existing channel</b> rather than a
        /// new one. A beacon kill yards its victim, pays the mark payout and pays
        /// the bounty exactly as any other kill does, so it belongs in the list
        /// <c>GameEngine</c> already walks — only the cause label is new.
        /// </remarks>
        private IReadOnlyList<CellEffectResolution> FireBeacons(List<UpkeepNeutralize> neutralized)
        {
            var resolutions = _cellEffects.Fire(CurrentPlayer.Color, _allOperators);

            foreach (var resolution in resolutions)
            {
                for (int i = 0; i < resolution.Damage.Count; i++)
                {
                    if (resolution.Damage[i].Outcome != DamageOutcome.Neutralized) continue;

                    var victim = resolution.Caught[i];
                    var outcome = _neutralize.Apply(victim, resolution.SourceOperatorId);

                    neutralized.Add(new UpkeepNeutralize(victim, resolution.Cause, outcome));
                }
            }

            return resolutions;
        }

        /// <summary>
        /// Resolves every operator-anchored charge of the current seat that has
        /// come due, and folds any kills into the upkeep's neutralize list
        /// (§6.4). The operator-anchored twin of <see cref="FireBeacons"/>,
        /// with the same division of labour: the registry owns the blast and
        /// the pipeline calls, this owns what reaching zero means.
        /// </summary>
        private IReadOnlyList<OperatorEffectResolution> FireCharges(List<UpkeepNeutralize> neutralized)
        {
            if (_operatorEffects == null) return Array.Empty<OperatorEffectResolution>();

            var resolutions = _operatorEffects.Fire(CurrentPlayer.Color, _allOperators);

            foreach (var resolution in resolutions)
            {
                for (int i = 0; i < resolution.Damage.Count; i++)
                {
                    if (resolution.Damage[i].Outcome != DamageOutcome.Neutralized) continue;

                    var victim = resolution.Caught[i];
                    var outcome = _neutralize.Apply(victim, resolution.SourceOperatorId);

                    neutralized.Add(new UpkeepNeutralize(victim, resolution.Cause, outcome));
                }
            }

            return resolutions;
        }

        /// <summary>
        /// Runs one source of upkeep damage. Returns <c>false</c> if the operator
        /// was neutralized, so the caller stops billing a piece that is already
        /// back in its yard.
        /// </summary>
        /// <remarks>
        /// Extracted rather than written twice: bleed and marks differ only in
        /// what they owe and who is credited for it, and two copies of the
        /// neutralize handling is exactly the shape of duplication ADR-0004 was
        /// written about.
        ///
        /// <b>The label is the cause the view will show</b>, and the payout is
        /// captured rather than discarded. Both travel out on
        /// <see cref="UpkeepNeutralize"/> because this method applies the
        /// neutralize itself — <c>GameEngine</c> only reports what happened here,
        /// so anything not carried out is lost.
        /// </remarks>
        private bool TickUpkeepDamage(
            OperatorState op,
            int amount,
            int sourceOperatorId,
            string label,
            List<DamageResult> ticks,
            List<UpkeepNeutralize> neutralized)
        {
            if (amount <= 0) return true;

            var result = _damage.Apply(
                op, new DamageInstance(amount, DamageType.Atomic, sourceOperatorId, label));

            ticks.Add(result);

            if (result.Outcome != DamageOutcome.Neutralized) return true;

            // The source is already in hand — it is what the damage instance was
            // built with. A bleed credits the bleeding operator itself, so it
            // pays no bounty; a mark credits whoever applied it, and can pay one
            // on a turn that is not even theirs.
            var outcome = _neutralize.Apply(op, sourceOperatorId);
            neutralized.Add(new UpkeepNeutralize(op, label, outcome));
            return false;
        }

        private void RequirePhase(TurnPhase expected, string action)
        {
            if (Phase != expected)
                throw new InvalidOperationException($"{action} is not legal during {Phase}.");
        }
    }
}