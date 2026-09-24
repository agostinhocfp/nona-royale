// Assets/_Project/Scripts/Core/Services/DebtCollection.cs
namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// What closing a seat's turn did to its debt (COMBAT_SYSTEMS §3.3): what
    /// the pool paid, what interest the unpaid remainder drew, and what is
    /// owed now.
    /// </summary>
    public readonly struct DebtCollection
    {
        public DebtCollection(int owedBefore, int paid, int interest, int owed)
        {
            OwedBefore = owedBefore;
            Paid = paid;
            Interest = interest;
            Owed = owed;
        }

        /// <summary>Nothing was owed, so nothing happened.</summary>
        public static DebtCollection None => default;

        /// <summary>The debt the seat ended its turn with.</summary>
        public int OwedBefore { get; }

        /// <summary>Energy taken from the pool against it. Destroyed, not transferred.</summary>
        public int Paid { get; }

        /// <summary>Added to the unpaid remainder, within the cap.</summary>
        public int Interest { get; }

        /// <summary>The debt afterwards.</summary>
        public int Owed { get; }

        /// <summary>Whether there was a debt to collect at all.</summary>
        public bool Happened => OwedBefore > 0;

        public override string ToString() =>
            Happened ? $"paid {Paid} of {OwedBefore}, +{Interest} interest, owes {Owed}" : "no debt";
    }
}