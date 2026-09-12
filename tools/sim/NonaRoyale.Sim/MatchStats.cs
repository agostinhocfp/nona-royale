// tools/sim/NonaRoyale.Sim/MatchStats.cs
using System.Collections.Generic;
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

        public bool Completed;

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
            }
        }
    }
}