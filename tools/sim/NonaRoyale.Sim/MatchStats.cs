// tools/sim/NonaRoyale.Sim/MatchStats.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Events;

namespace NonaRoyale.Sim
{
    /// <summary>
    /// What one simulated match produced, counted from the event stream.
    /// </summary>
    /// <remarks>
    /// Counting events rather than inspecting state is deliberate: the harness
    /// observes exactly what the Unity view will observe, so a metric that can
    /// be measured here is one the game can also display.
    /// </remarks>
    public sealed class MatchStats
    {
        public int Turns;
        public int Neutralizes;
        public int Collisions;
        public int AbilitiesFired;
        public int EnergyBurned;
        public int Evasions;
        public int Executes;

        /// <summary>Turns in which a player had every operator on the board at once.</summary>
        public int FullSquadTurns;

        /// <summary>Turns counted for the occupancy figure above.</summary>
        public int OccupancySamples;

        /// <summary>
        /// True once somebody has won. <b>It was never set until now</b> —
        /// declared, read by nothing, and false on every match the harness has
        /// ever run. A sweep that wanted to discard matches hitting the turn
        /// guard had no way to tell which those were.
        /// </summary>
        public bool Completed;

        /// <summary>
        /// The seat that won, or null if the match hit its turn guard.
        /// </summary>
        /// <remarks>
        /// <b>Nothing has ever recorded this.</b> Every figure the harness
        /// produces is pacing and throughput — how long a match runs and how much
        /// combat happens in it — and none of them answers who won. With a fixed
        /// alpha squad in every seat that was defensible: there was nothing to
        /// compare. It stops being defensible the moment two seats play
        /// differently.
        /// </remarks>
        public PlayerColor? Winner;

        /// <summary>
        /// How many times each ability id was actually cast.
        /// </summary>
        /// <remarks>
        /// <b>This exists because a nerf to Tagged From Above moved nothing.</b>
        /// Two full policy sweeps came back bit-identical on every spendthrift
        /// row after its cooldown doubled — identical rows mean an identical
        /// command stream, which means the ability was never being cast.
        /// <c>SpendthriftPolicy</c> fires the first affordable thing it finds, so
        /// it reaches Bouncer's 6-cost abilities every turn and never banks to 9.
        ///
        /// <b>The consequence is larger than one ability.</b> Every other sweep
        /// runs spendthrift on all four seats, so every figure in COMBAT_SYSTEMS
        /// §12 describes a game in which the 9-cost tier is not cast at all. That
        /// is a bigger caveat than any already listed there.
        ///
        /// <b>An ability that is never cast is a design failure independent of
        /// balance</b>, and it is the one thing a harness can report without
        /// needing a good player to do it. Counting is almost free; knowing which
        /// abilities never come up is not.
        /// </remarks>
        public readonly Dictionary<int, int> CastsByAbility = new Dictionary<int, int>();

        public double FullSquadShare =>
            OccupancySamples == 0 ? 0.0 : (double)FullSquadTurns / OccupancySamples;

        public void Observe(IReadOnlyList<IGameEvent> events)
        {
            foreach (var e in events)
            {
                if (e is OperatorNeutralized) Neutralizes++;
                else if (e is CollisionResolved) Collisions++;
                else if (e is EnergySpent) AbilitiesFired++;
                else if (e is DamageEvaded) Evasions++;
                else if (e is EnergyGranted granted) EnergyBurned += granted.Burned;
                else if (e is GameWon won)
                {
                    Winner = won.Winner;
                    Completed = true;
                }
            }
        }

        /// <summary>
        /// Records one approved cast.
        /// </summary>
        /// <remarks>
        /// <b>Called by the policy rather than derived from the event stream</b>,
        /// because no event carries the ability's id. <c>EnergySpent</c> reports a
        /// seat and an amount; two abilities at the same cost are
        /// indistinguishable in it. Adding the id to that event would change the
        /// core to serve the harness, which is the wrong direction — the policy
        /// already has the id in hand when it sends the command.
        /// </remarks>
        public void RecordCast(int abilityId)
        {
            int count;
            CastsByAbility.TryGetValue(abilityId, out count);
            CastsByAbility[abilityId] = count + 1;
        }
    }
}