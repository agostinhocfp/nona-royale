// Assets/_Project/Scripts/Core/Services/SpendResult.cs
namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// Whether a spend went through. A refusal is a normal answer, not an
    /// exception: trying an ability you cannot afford is an ordinary thing for a
    /// player to attempt, and the view needs to say why rather than crash.
    /// </summary>
    public readonly struct SpendResult
    {
        private SpendResult(bool approved, int cost, int remaining)
        {
            Approved = approved;
            Cost = cost;
            Remaining = remaining;
        }

        /// <summary>The pool covered it and has been debited.</summary>
        public static SpendResult Paid(int cost, int remaining) => new SpendResult(true, cost, remaining);

        /// <summary>The pool was short. Nothing was debited.</summary>
        public static SpendResult Refused(int cost, int remaining) => new SpendResult(false, cost, remaining);

        public bool Approved { get; }

        /// <summary>What was asked for. On a refusal, what was asked and not paid.</summary>
        public int Cost { get; }

        /// <summary>Pool size afterwards. Unchanged on a refusal.</summary>
        public int Remaining { get; }

        public int Shortfall => Approved ? 0 : Cost - Remaining;

        public override string ToString() =>
            Approved ? $"spent {Cost}, {Remaining} left" : $"refused: need {Cost}, have {Remaining}";
    }
}