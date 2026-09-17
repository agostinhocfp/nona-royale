// Assets/_Project/Scripts/Core/Model/DamageInstance.cs
using System;

namespace NonaRoyale.Core.Model
{
    /// <summary>
    /// One packet of damage entering the pipeline. Abilities, collisions and
    /// bleed ticks all arrive as this — that single shape is what makes
    /// "capture is damage" true in code and not just in the design doc
    /// (ADR-0005).
    /// </summary>
    public readonly struct DamageInstance
    {
        public DamageInstance(int amount, DamageType type, int sourceOperatorId, string sourceName,
            int? castCost = null)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Damage is never negative; healing is its own effect.");

            Amount = amount;
            Type = type;
            SourceOperatorId = sourceOperatorId;
            SourceName = sourceName ?? "unknown";
            CastCost = castCost;
        }

        public int Amount { get; }
        public DamageType Type { get; }

        /// <summary>Who dealt it. Carried so the view can attribute the hit and so Mark payouts can credit a kill.</summary>
        public int SourceOperatorId { get; }

        /// <summary>Human-readable source ("collision", "Ace Shards"). For events and logs, never for rules.</summary>
        public string SourceName { get; }

        /// <summary>
        /// The energy cost of the ability dealing this hit, when the hit lands
        /// the moment that ability is used; null for everything else —
        /// collisions, ticks, and devices resolving later. Read by Equilibrium
        /// (§5.17).
        /// </summary>
        public int? CastCost { get; }

        public override string ToString() => $"{Amount} {Type} from {SourceName}";
    }
}