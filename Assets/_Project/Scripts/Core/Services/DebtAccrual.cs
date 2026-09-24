// Assets/_Project/Scripts/Core/Services/DebtAccrual.cs
namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// What closing a seat's turn did to its debt (COMBAT_SYSTEMS §3.3): the
    /// interest it drew and what it owes now. Nothing is paid — since
    /// 2026-09-24 a debt only grows until Sadist calls it or a collision
    /// burns it.
    /// </summary>
    public readonly struct DebtAccrual
    {
        public DebtAccrual(int owedBefore, int interest, int owed)
        {
            OwedBefore = owedBefore;
            Interest = interest;
            Owed = owed;
        }

        /// <summary>Nothing was owed, so nothing happened.</summary>
        public static DebtAccrual None => default;

        /// <summary>The debt the seat ended its turn with.</summary>
        public int OwedBefore { get; }

        /// <summary>Added to the debt, within the cap; 0 at the cap.</summary>
        public int Interest { get; }

        /// <summary>The debt afterwards.</summary>
        public int Owed { get; }

        /// <summary>Whether there was a debt at all.</summary>
        public bool Happened => OwedBefore > 0;

        public override string ToString() =>
            Happened ? $"owed {OwedBefore}, +{Interest} interest, owes {Owed}" : "no debt";
    }
}