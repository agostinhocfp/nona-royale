// Assets/_Project/Scripts/Core/Config/EnergyConfig.cs
using System;

namespace NonaRoyale.Core.Config
{
    /// <summary>
    /// The energy economy (COMBAT_SYSTEMS §3). One shared pool per player, fed
    /// by the dice, capped hard.
    /// </summary>
    /// <remarks>
    /// The superseded tiered rule (≤4 → 1, 5–8 → 2, ≥9 → 3) generated about 34
    /// energy across a match and left operators standing around with nothing to
    /// spend. <c>floor(total / 2)</c> generates roughly 52 — about three
    /// ultimates plus change, or a steadier drip of cheap abilities. That is the
    /// difference between a combat-first game and a race with abilities
    /// attached.
    /// </remarks>
    public sealed class EnergyConfig
    {
        public EnergyConfig(int energyCap = 12, int diceDivisor = 2, int cashedDieEnergy = 2)
        {
            if (energyCap < 1) throw new ArgumentOutOfRangeException(nameof(energyCap));
            if (diceDivisor < 1) throw new ArgumentOutOfRangeException(nameof(diceDivisor));
            if (cashedDieEnergy < 0) throw new ArgumentOutOfRangeException(nameof(cashedDieEnergy));

            EnergyCap = energyCap;
            DiceDivisor = diceDivisor;
            CashedDieEnergy = cashedDieEnergy;
        }

        /// <summary>
        /// Energy above this is burned, not stored. The cap is what forces
        /// spending — and at 12 it is exactly one ultimate and nothing else, so
        /// hoarding for an ult is a visible commitment rather than a free option.
        /// </summary>
        public int EnergyCap { get; }

        /// <summary>Dice total is divided by this and floored. Range 1–6, mean 3.5.</summary>
        public int DiceDivisor { get; }

        /// <summary>
        /// What one cashed die pays the seat holding Fortuna's House Edge
        /// (§3.4, §5.18). Flat, not scaled to the face: the die most worth
        /// cashing is the 1, and a formula that paid by pips would pay nothing
        /// for it.
        /// </summary>
        /// <remarks>
        /// <b>Two is priced against a die, not against an ability.</b> A die is
        /// worth about 3.5 pips, and a seat needs very nearly every pip it rolls
        /// to get three operators home — so cashing is a loss on the race that
        /// pays for itself only when the board makes that movement worthless or
        /// dangerous. It is the first dial if she reads as too strong.
        /// </remarks>
        public int CashedDieEnergy { get; }

        public static EnergyConfig Default => new EnergyConfig();
    }
}