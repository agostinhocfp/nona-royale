// Assets/_Project/Scripts/Core/Services/CollisionResult.cs
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// What a landing turned into, and where the mover ends up as a result.
    /// The caller applies <see cref="MoverFinalProgress"/>; nothing here has
    /// moved the mover yet.
    /// </summary>
    public readonly struct CollisionResult
    {
        private CollisionResult(
            bool occurred,
            OperatorState occupant,
            DamageResult damage,
            int moverFinalProgress,
            bool moverBouncedBack)
        {
            Occurred = occurred;
            Occupant = occupant;
            Damage = damage;
            MoverFinalProgress = moverFinalProgress;
            MoverBouncedBack = moverBouncedBack;
        }

        /// <summary>False when the landing was uncontested, safe, friendly, or off the loop.</summary>
        public bool Occurred { get; }

        /// <summary>The enemy that was struck, or null if none.</summary>
        public OperatorState Occupant { get; }

        /// <summary>How the collision's damage resolved. Meaningless unless <see cref="Occurred"/>.</summary>
        public DamageResult Damage { get; }

        /// <summary>Where the mover actually ends this move: its landing, or one step back.</summary>
        public int MoverFinalProgress { get; }

        /// <summary>
        /// The occupant survived and held the cell. Bounce-back is placement, so
        /// the caller must not re-run collision on the new position (§7.2).
        /// </summary>
        public bool MoverBouncedBack { get; }

        /// <summary>The occupant was neutralized. The caller owes it the §1.2 consequences.</summary>
        public bool OccupantNeutralized =>
            Occurred && Damage.Outcome == DamageOutcome.Neutralized;

        public static CollisionResult None(int moverProgress) =>
            new CollisionResult(false, null, default, moverProgress, false);

        public static CollisionResult Bounced(OperatorState occupant, DamageResult damage, int bounceProgress) =>
            new CollisionResult(true, occupant, damage, bounceProgress, true);

        public static CollisionResult CellTaken(OperatorState occupant, DamageResult damage, int landingProgress) =>
            new CollisionResult(true, occupant, damage, landingProgress, false);

        public override string ToString() =>
            Occurred
                ? $"collision with {Occupant.Name}: {Damage}, mover -> {MoverFinalProgress}"
                : $"no collision, mover -> {MoverFinalProgress}";
    }
}