// Assets/_Project/Scripts/Core/Services/TargetingResult.cs
namespace NonaRoyale.Core.Services
{
    /// <summary>The outcome of a targeting check, with its distance when one exists.</summary>
    public readonly struct TargetingResult
    {
        public TargetingResult(TargetingVerdict verdict, int? distance)
        {
            Verdict = verdict;
            Distance = distance;
        }

        public TargetingVerdict Verdict { get; }

        /// <summary>Steps along the track, or null when either party is off the loop.</summary>
        public int? Distance { get; }

        public bool IsLegal => Verdict == TargetingVerdict.Legal;

        public static TargetingResult Legal(int distance) =>
            new TargetingResult(TargetingVerdict.Legal, distance);

        public static TargetingResult Illegal(TargetingVerdict verdict, int? distance = null) =>
            new TargetingResult(verdict, distance);

        public override string ToString() =>
            IsLegal ? $"legal at {Distance}" : Verdict.ToString();
    }
}