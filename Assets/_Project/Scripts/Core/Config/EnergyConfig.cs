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
        public EnergyConfig(
            int energyCap = 12, int diceDivisor = 2, int cashedDieEnergy = 2,
            int debtCap = 6, int debtInterest = 1)
        {
            if (energyCap < 1) throw new ArgumentOutOfRangeException(nameof(energyCap));
            if (diceDivisor < 1) throw new ArgumentOutOfRangeException(nameof(diceDivisor));
            if (cashedDieEnergy < 0) throw new ArgumentOutOfRangeException(nameof(cashedDieEnergy));
            if (debtCap < 1) throw new ArgumentOutOfRangeException(nameof(debtCap));
            if (debtInterest < 0) throw new ArgumentOutOfRangeException(nameof(debtInterest));

            EnergyCap = energyCap;
            DiceDivisor = diceDivisor;
            CashedDieEnergy = cashedDieEnergy;
            DebtCap = debtCap;
            DebtInterest = debtInterest;
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

        /// <summary>
        /// The most a seat can owe (§3.3, 2026-09-24). Debt above it is never
        /// written, whether it comes from a cast or from interest.
        /// </summary>
        /// <remarks>
        /// <b>Six, because the debt is Sadist's figure.</b> The ultimate deals
        /// what the target's seat owes, so the cap is the most it can ever
        /// compute — six against an eight-health roster is a near-kill that the
        /// debtor watched build for at least two of its own turns, never a
        /// surprise. It is the first dial if the rework reads as too strong.
        /// </remarks>
        public int DebtCap { get; }

        /// <summary>
        /// Added to a seat's debt when it ends its turn without paying all of
        /// it (§3.3). Charged once per turn, on the unpaid remainder only, and
        /// never past <see cref="DebtCap"/>.
        /// </summary>
        public int DebtInterest { get; }

        public static EnergyConfig Default => new EnergyConfig();
    }
}