// Assets/_Project/Scripts/Core/Services/DeployOption.cs
using System;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    /// <summary>
    /// What a roll offers a player with operators still in the yard.
    /// Possibilities, not a decision: deploying is optional, and declining to
    /// deploy keeps the full dice total as movement (COMBAT_SYSTEMS §1.3).
    /// </summary>
    public readonly struct DeployOption
    {
        private readonly DiceRoll _roll;
        private readonly int _deployFace;

        public DeployOption(int maxOperators, int deployFace, DiceRoll roll)
        {
            if (maxOperators < 1) throw new ArgumentOutOfRangeException(nameof(maxOperators));

            IsAvailable = true;
            MaxOperators = maxOperators;
            _deployFace = deployFace;
            _roll = roll;
            TotalIfDeclined = roll.Total;
        }

        private DeployOption(int totalIfDeclined)
        {
            IsAvailable = false;
            MaxOperators = 0;
            _deployFace = 0;
            _roll = default;
            TotalIfDeclined = totalIfDeclined;
        }

        public static DeployOption Unavailable(int totalIfDeclined) => new DeployOption(totalIfDeclined);

        public bool IsAvailable { get; }

        /// <summary>
        /// How many operators this roll could put on the board — one per die
        /// showing the deploy face, capped by how many are actually waiting.
        /// </summary>
        public int MaxOperators { get; }

        /// <summary>Movement value if the player deploys nothing: the full total.</summary>
        public int TotalIfDeclined { get; }

        /// <summary>
        /// Movement left after deploying <paramref name="count"/> operators.
        /// Each deploy consumes one die showing the deploy face, and whatever
        /// remains is that turn's movement.
        /// </summary>
        /// <remarks>
        /// So a double 6 deploying both operators forfeits movement entirely,
        /// exactly as COMBAT_SYSTEMS §1.3 states. The case that section does not
        /// spell out is a double 6 with only <i>one</i> operator waiting: one
        /// die is consumed, the other 6 remains, and it becomes the movement
        /// roll. Consuming both dice to deploy one operator would be a strictly
        /// worse turn than rolling a single 6, which cannot be the intent.
        /// </remarks>
        public int MovementAfterDeploying(int count)
        {
            if (count < 0 || count > MaxOperators)
                throw new ArgumentOutOfRangeException(nameof(count),
                    $"This roll can deploy between 0 and {MaxOperators} operators; asked for {count}.");

            if (count == 0) return TotalIfDeclined;

            return count == 1 ? _roll.OtherThan(_deployFace) : 0;
        }
    }
}