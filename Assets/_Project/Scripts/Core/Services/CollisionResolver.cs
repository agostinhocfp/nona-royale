// Assets/_Project/Scripts/Core/Services/CollisionResolver.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Decides what happens when a move ends on an occupied cell.
    /// </summary>
    /// <remarks>
    /// This is where ADR-0005 lives. Landing on an enemy is <b>an attack
    /// delivered by movement</b>, not an execution: it deals
    /// <c>CollisionDamage</c> of type Normal through the same
    /// <see cref="DamagePipeline"/> an ability uses, and it removes the occupant
    /// only if that reduces it to zero. That is what makes health the single
    /// currency in the game, and what makes Bouncer's 12 health mean something.
    ///
    /// Three properties fall out of the rules and are worth stating plainly,
    /// because each one closes a class of bug:
    ///
    /// <list type="bullet">
    /// <item><b>Collision is one-directional.</b> The mover never takes damage.</item>
    /// <item><b>A contested cell can hold more than one enemy.</b> Friendly
    /// operators — in a team match, both of a side's seats (ADR-0012) —
    /// stack freely (§4.5), and both bounce-back and pulls are
    /// placement that never collides — so a stack of enemies on a non-safe cell
    /// is reachable by ordinary play. The mover strikes <i>every</i> enemy on
    /// the cell, and takes it only if all of them fall. Running into a pair
    /// should be dangerous for them and hard for you, not a coin flip over which
    /// one you hit.</item>
    /// <item><b>Bounce-back is placement, not movement.</b> It triggers nothing:
    /// no second collision, no special space, no home entry (§7.2).</item>
    /// </list>
    ///
    /// Like <see cref="MovementResolver"/>, this computes where the mover ends
    /// up and leaves applying it to the caller. Unlike it, the occupant's health
    /// does change here, because the pipeline owns that.
    /// </remarks>
    public sealed class CollisionResolver
    {
        private readonly PathMap _map;
        private readonly CombatConfig _config;
        private readonly DamagePipeline _damage;
        private readonly MovementResolver _movement;
        private readonly TeamMap _teams;

        /// <param name="teams">
        /// Who is on whose side (ADR-0012). Null is
        /// <see cref="TeamMap.FreeForAll"/>, under which "friendly" means the
        /// same seat and nothing here changes.
        /// </param>
        public CollisionResolver(
            PathMap map,
            CombatConfig config,
            DamagePipeline damage,
            MovementResolver movement,
            TeamMap teams = null)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _teams = teams ?? TeamMap.FreeForAll;
        }

        /// <summary>
        /// Resolves a completed move against everyone on the board.
        /// </summary>
        /// <param name="mover">The operator that moved. Never takes damage.</param>
        /// <param name="move">Where the move ended, from <see cref="MovementResolver.ResolveMove"/>.</param>
        /// <param name="allOperators">Every operator in the match, movers and occupants alike.</param>
        public CollisionResult Resolve(OperatorState mover, MoveResult move, IEnumerable<OperatorState> allOperators)
        {
            if (mover == null) throw new ArgumentNullException(nameof(mover));
            if (allOperators == null) throw new ArgumentNullException(nameof(allOperators));

            // Only a landing on the shared loop can be contested. A move that
            // ended in a home column or at HOME is out of the fight (§4.3), and
            // passing *through* an occupied cell never collides (§7.1).
            if (!move.CanBeContested)
                return CollisionResult.None(move.To);

            // Safe cells: the mover simply shares the cell. Both occupy,
            // nothing resolves (§4.4).
            if (_map.IsSafe(move.Destination))
                return CollisionResult.None(move.To);

            var occupants = FindEnemiesOn(move.Destination, mover, allOperators);

            if (occupants.Count == 0)
                return CollisionResult.None(move.To);

            var damage = new List<DamageResult>(occupants.Count);
            bool anySurvived = false;

            foreach (var occupant in occupants)
            {
                var result = _damage.Apply(occupant, new DamageInstance(
                    _config.CollisionDamage, DamageType.Normal, mover.Id, "collision"));

                damage.Add(result);
                if (result.TargetSurvived) anySurvived = true;
            }

            // Any survivor holds the cell — however it survived. Evasion negates
            // damage, never movement (§5.5).
            if (anySurvived)
                return CollisionResult.Bounced(occupants, damage, _movement.BounceBackProgress(move.To));

            return CollisionResult.CellTaken(occupants, damage, move.To);
        }

        private List<OperatorState> FindEnemiesOn(
            CellRef cell, OperatorState mover, IEnumerable<OperatorState> allOperators)
        {
            var found = new List<OperatorState>();

            foreach (var candidate in allOperators)
            {
                if (candidate == null || ReferenceEquals(candidate, mover)) continue;

                // Friendlies stack freely (§4.5), and in a team match a
                // partner seat's operators are friendlies (ADR-0012): landing
                // on your own side is never an attack, so a team can stack two
                // seats' pieces on one cell and a clumsy roll costs the
                // partnership nothing.
                if (!_teams.AreEnemies(candidate.Owner, mover.Owner)) continue;
                if (!_map.IsOnOuterTrack(candidate.Progress)) continue;
                if (_map.CellAt(candidate.Owner, candidate.Progress) != cell) continue;

                found.Add(candidate);
            }

            return found;
        }
    }
}