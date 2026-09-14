// Assets/_Project/Scripts/Core/Services/NeutralizeRules.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// What a neutralize produced besides a yarded operator: the mark payout,
    /// and the bounty paid to whoever landed the kill.
    /// </summary>
    /// <remarks>
    /// A struct rather than two out-parameters because both are reported to the
    /// view, and a caller that forgets one shows a board that changed for no
    /// visible reason — which is exactly what the upkeep mark payout did for as
    /// long as it was silently discarded.
    /// </remarks>
    public readonly struct NeutralizeOutcome
    {
        public NeutralizeOutcome(
            IReadOnlyList<OperatorState> hastened,
            PlayerColor? bountyPaidTo,
            EnergyGrant bounty)
        {
            Hastened = hastened ?? Array.Empty<OperatorState>();
            BountyPaidTo = bountyPaidTo;
            Bounty = bounty;
        }

        /// <summary>Operators hastened by a mark payout. Empty when the dead operator was not marked.</summary>
        public IReadOnlyList<OperatorState> Hastened { get; }

        /// <summary>The seat that collected the bounty, or null when nothing was owed.</summary>
        public PlayerColor? BountyPaidTo { get; }

        /// <summary>What the bounty actually credited. Its burn may be the whole of it (§3.1).</summary>
        public EnergyGrant Bounty { get; }

        public bool PaidABounty => BountyPaidTo != null;
    }

    /// <summary>
    /// Everything that happens to an operator reduced to zero health
    /// (COMBAT_SYSTEMS §1.2), including Tagged From Above's payout (§10.2) and
    /// the attacker's bounty.
    /// </summary>
    /// <remarks>
    /// Its own service because four different things neutralize — a collision,
    /// an ability, a bleed tick and a mark tick — and each would otherwise carry
    /// its own copy of the consequences. That is the shape of the bug ADR-0004
    /// was written about, and it is also why the payout lives here: the mark is
    /// read at the one place every death funnels through, so no call site can
    /// forget it.
    ///
    /// <b>Neutralize is a setback, not a removal.</b> There is no permanent
    /// death in the MVP: the operator goes back to its yard at full health and
    /// re-enters on a 6 like any other deployment. Its passives survive, because
    /// a passive is who an operator is (§5.1) — <c>StatusRegistry</c> keeps them
    /// in a store <see cref="StatusRegistry.ClearAll"/> does not touch.
    ///
    /// <b>The victim's own pool is untouched.</b> Energy is player-level, so a
    /// yarded operator costs its owner nothing economically. That is unchanged;
    /// what is new is that the <i>attacker's</i> pool is not.
    /// </remarks>
    public sealed class NeutralizeRules
    {
        private static readonly OperatorState[] NoPayout = new OperatorState[0];

        private readonly StatusRegistry _statuses;
        private readonly AbilityResolver _abilities;
        private readonly EnergyLedger _energy;
        private readonly IReadOnlyList<OperatorState> _operators;
        private readonly IReadOnlyList<PlayerState> _players;
        private readonly CombatConfig _config;

        public NeutralizeRules(
            StatusRegistry statuses,
            AbilityResolver abilities,
            EnergyLedger energy,
            IReadOnlyList<OperatorState> operators,
            IReadOnlyList<PlayerState> players,
            CombatConfig config)
        {
            _statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            _abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
            _energy = energy ?? throw new ArgumentNullException(nameof(energy));
            _operators = operators ?? throw new ArgumentNullException(nameof(operators));
            _players = players ?? throw new ArgumentNullException(nameof(players));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Sends an operator to its yard and reports what that produced — the
        /// mark payout, and the bounty owed to <paramref name="killerOperatorId"/>.
        /// The caller emits the events; this service owns the state.
        /// </summary>
        /// <param name="killerOperatorId">
        /// Whoever dealt the finishing damage, or null when nothing is
        /// creditable. Every call site knows this without <c>DamageResult</c>
        /// carrying it: a collision has its mover, an ability has its caster,
        /// and an upkeep tick has the source recorded on the status.
        /// </param>
        public NeutralizeOutcome Apply(OperatorState op, int? killerOperatorId = null)
        {
            if (op == null) throw new ArgumentNullException(nameof(op));

            // Read the mark before anything clears it. Neutralize strips every
            // status, so a payout resolved after ClearAll would find nothing —
            // the mark's whole purpose is to be readable at this instant.
            var hastened = PayOutMark(op);

            // Likewise before the yard move: the bounty is refused when the
            // killer is the victim, and that comparison reads the victim.
            var bounty = PayBounty(op, killerOperatorId);

            op.MoveTo(PathMap.YardProgress);   // track progress entirely lost
            op.RestoreHealth();
            _statuses.ClearAll(op);
            _abilities.ResetCooldowns(op);

            // The victim's own energy is untouched: the pool is player-level, so
            // a yarded operator costs its owner nothing economically.

            return new NeutralizeOutcome(hastened, bounty.Key, bounty.Value);
        }

        /// <summary>
        /// Credits the killer's pool with <c>NeutralizeEnergyBounty</c>.
        /// </summary>
        /// <remarks>
        /// <b>Why energy and not position.</b> A kill previously paid the
        /// attacker nothing, which at four players makes killing a public good
        /// bought with private resources — the victim loses a lap and every
        /// opponent collects it, not just the one who paid. Energy is the least
        /// snowballing way to close that: it buys another fight, not another lap.
        /// It softens the problem rather than solving it, and that is the most
        /// any affordable reward can do — see
        /// <c>_HANDOFF_neutralize_rewards.md</c>.
        ///
        /// <b>Nothing is owed for a death you caused to your own side.</b> A
        /// self-inflicted kill — All-In Mauling is the only route (§2.3) — and a
        /// kill of an operator you own both pay zero. Otherwise the Bouncer
        /// could farm his own pool, and the rule that makes the bounty a reward
        /// for fighting would make it a reward for anything.
        ///
        /// <b>A killer already in its own yard still collects.</b> A bleed or a
        /// mark can outlive the operator that applied it, and the pool belongs
        /// to the player rather than the piece.
        /// </remarks>
        private KeyValuePair<PlayerColor?, EnergyGrant> PayBounty(OperatorState victim, int? killerOperatorId)
        {
            var nothing = new KeyValuePair<PlayerColor?, EnergyGrant>(null, default);

            if (_config.NeutralizeEnergyBounty <= 0) return nothing;
            if (killerOperatorId == null) return nothing;
            if (killerOperatorId.Value == victim.Id) return nothing;

            var killer = FindOperator(killerOperatorId.Value);
            if (killer == null || killer.Owner == victim.Owner) return nothing;

            var player = FindPlayer(killer.Owner);
            if (player == null) return nothing;

            return new KeyValuePair<PlayerColor?, EnergyGrant>(
                killer.Owner, _energy.GrantBounty(player, _config.NeutralizeEnergyBounty));
        }

        /// <summary>
        /// Grants the marker's whole squad <c>Hastened</c> if the operator dying
        /// here carried a mark (§10.2).
        /// </summary>
        /// <remarks>
        /// <b>Any death of a marked operator pays out</b>, whatever killed it —
        /// a collision, an ability, the mark's own tick. That matches the design
        /// intent that the mark <i>hands</i> a kill to its owner's squad rather
        /// than scoring one itself.
        ///
        /// It does mean an operator who kills itself while marked pays out the
        /// enemy squad that marked him. **The killer's id is now available to
        /// fix that**, and §10.2's wording — "neutralized by Syla's side" —
        /// says it should be. Left alone here deliberately: it changes when an
        /// existing, tested behaviour fires, and belongs in its own commit with
        /// its own test rather than riding along with the bounty.
        /// </remarks>
        private IReadOnlyList<OperatorState> PayOutMark(OperatorState dying)
        {
            int? markerId = _statuses.MarkedBy(dying);
            if (markerId == null) return NoPayout;

            OperatorState marker = FindOperator(markerId.Value);

            // The marker may itself have been neutralized since casting, and a
            // yarded operator is still an operator — its squad still collects.
            if (marker == null) return NoPayout;

            var squad = new List<OperatorState>();

            foreach (var candidate in _operators)
            {
                if (candidate.Owner != marker.Owner) continue;

                _statuses.Apply(candidate, StatusKind.Hastened, _config.HasteDurationTurns);
                squad.Add(candidate);
            }

            return squad;
        }

        private OperatorState FindOperator(int id)
        {
            foreach (var op in _operators)
                if (op.Id == id) return op;

            return null;
        }

        private PlayerState FindPlayer(PlayerColor colour)
        {
            foreach (var player in _players)
                if (player.Color == colour) return player;

            return null;
        }
    }
}