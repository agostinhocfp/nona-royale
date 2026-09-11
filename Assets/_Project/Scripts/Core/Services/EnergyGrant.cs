// Assets/_Project/Scripts/Core/Services/EnergyGrant.cs
namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// What one turn's energy grant produced. Keeps <see cref="Burned"/>
    /// separate from <see cref="Stored"/> so the view can show a player that the
    /// cap cost them something — burn is a design signal, not an accounting
    /// detail (COMBAT_SYSTEMS §3.1).
    /// </summary>
    public readonly struct EnergyGrant
    {
        public EnergyGrant(int earned, int stored, int burned, int total)
        {
            Earned = earned;
            Stored = stored;
            Burned = burned;
            Total = total;
            WasGranted = true;
        }

        private EnergyGrant(int total, bool granted)
        {
            Earned = 0;
            Stored = 0;
            Burned = 0;
            Total = total;
            WasGranted = granted;
        }

        /// <summary>Refused because this turn's grant already happened — a doubles re-roll.</summary>
        public static EnergyGrant AlreadyGranted(int total) => new EnergyGrant(total, false);

        /// <summary>What the dice earned before the cap.</summary>
        public int Earned { get; }

        /// <summary>What actually reached the pool.</summary>
        public int Stored { get; }

        /// <summary>What the cap destroyed.</summary>
        public int Burned { get; }

        /// <summary>Pool size afterwards.</summary>
        public int Total { get; }

        public bool WasGranted { get; }

        public override string ToString() =>
            WasGranted ? $"+{Stored} (burned {Burned}, pool {Total})" : $"no grant (pool {Total})";
    }
}