// Assets/_Project/Scripts/Core/Services/CollisionResult.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// What a landing turned into, and where the mover ends up as a result.
    /// The caller applies <see cref="MoverFinalProgress"/>; nothing here has
    /// moved the mover yet.
    /// </summary>
    /// <remarks>
    /// A contested cell can hold <b>more than one</b> enemy, because friendly
    /// operators stack freely (§4.5) and both bounce-back and pulls are
    /// placement that never collides. So the occupants are a list.
    /// </remarks>
    public readonly struct CollisionResult
    {
        private static readonly IReadOnlyList<OperatorState> NoOccupants = Array.Empty<OperatorState>();
        private static readonly IReadOnlyList<DamageResult> NoDamage = Array.Empty<DamageResult>();

        private CollisionResult(
            bool occurred,
            IReadOnlyList<OperatorState> occupants,
            IReadOnlyList<DamageResult> damage,
            int moverFinalProgress,
            bool moverBouncedBack)
        {
            Occurred = occurred;
            Occupants = occupants;
            Damage = damage;
            MoverFinalProgress = moverFinalProgress;
            MoverBouncedBack = moverBouncedBack;
        }

        /// <summary>False when the landing was uncontested, safe, friendly, or off the loop.</summary>
        public bool Occurred { get; }

        /// <summary>Every enemy struck, in board order.</summary>
        public IReadOnlyList<OperatorState> Occupants { get; }

        /// <summary>How each occupant's damage resolved, index-matched to <see cref="Occupants"/>.</summary>
        public IReadOnlyList<DamageResult> Damage { get; }

        /// <summary>Where the mover actually ends this move: its landing, or one step back.</summary>
        public int MoverFinalProgress { get; }

        /// <summary>
        /// At least one occupant survived and held the cell. Bounce-back is
        /// placement, so the caller must not re-run collision on the new
        /// position (§7.2).
        /// </summary>
        public bool MoverBouncedBack { get; }

        /// <summary>The single occupant, for the overwhelmingly common 1v1 case.</summary>
        public OperatorState Occupant => Occupants.Count > 0 ? Occupants[0] : null;

        /// <summary>That occupant's damage result. Meaningless unless <see cref="Occurred"/>.</summary>
        public DamageResult FirstDamage => Damage.Count > 0 ? Damage[0] : default;

        /// <summary>True when every enemy on the cell fell, which is when the mover takes it.</summary>
        public bool AllOccupantsNeutralized
        {
            get
            {
                if (!Occurred) return false;

                foreach (var result in Damage)
                    if (result.TargetSurvived) return false;

                return true;
            }
        }

        public static CollisionResult None(int moverProgress) =>
            new CollisionResult(false, NoOccupants, NoDamage, moverProgress, false);

        public static CollisionResult Bounced(
            IReadOnlyList<OperatorState> occupants, IReadOnlyList<DamageResult> damage, int bounceProgress) =>
            new CollisionResult(true, occupants, damage, bounceProgress, true);

        public static CollisionResult CellTaken(
            IReadOnlyList<OperatorState> occupants, IReadOnlyList<DamageResult> damage, int landingProgress) =>
            new CollisionResult(true, occupants, damage, landingProgress, false);

        public override string ToString() =>
            Occurred
                ? $"collision with {Occupants.Count} enemy/enemies, mover -> {MoverFinalProgress}"
                : $"no collision, mover -> {MoverFinalProgress}";
    }
}