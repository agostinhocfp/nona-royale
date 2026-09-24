// Assets/_Project/Scripts/Unity/View/DebtMark.cs
using TMPro;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// How a seat's debt is written (COMBAT_SYSTEMS §3.3): Roman numerals on
    /// the display face, in oxblood and brass.
    /// </summary>
    /// <remarks>
    /// <b>Roman, so a debt is never read as health.</b> Damage, healing,
    /// health and energy are all Arabic figures on the data face; "IV" can only
    /// mean one thing. The cap is six, so the longest figure is two characters.
    ///
    /// <b>One figure per seat, and never parked on a piece</b> (designer,
    /// 2026-09-24). The standing number lives in the HUD beside the pool it is
    /// paid from; the board only shows debt for the moment something happens
    /// to it (<see cref="FeedbackLayer"/>).
    /// </remarks>
    public static class DebtMark
    {
        private static readonly string[] Numerals =
            { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };

        /// <summary>1 → "I" … 12 → "XII". Nothing for zero or less; Arabic past twelve, which no rule reaches.</summary>
        public static string Roman(int n) =>
            n <= 0 ? "" : n < Numerals.Length ? Numerals[n] : n.ToString();

        /// <summary>
        /// A small blood-velvet plate with the figure in brass, hidden while
        /// nothing is owed. Returns the label; its parent is the plate.
        /// </summary>
        public static TMP_Text Chip(Transform parent, float size, float height)
        {
            var plate = UiKit.Rect("debt", parent);
            UiKit.Sliced(plate, DecoSprites.ChipFill, UiTheme.DebtPlate);
            UiKit.Overlay(plate, DecoSprites.ButtonEdge, UiTheme.WithAlpha(UiTheme.Gold, 0.55f));
            UiKit.Size(plate, height: height);

            var pad = plate.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            pad.padding = new RectOffset(6, 6, 0, 0);
            pad.childControlWidth = true;
            pad.childControlHeight = true;
            pad.childForceExpandWidth = false;
            pad.childForceExpandHeight = true;
            pad.childAlignment = TextAnchor.MiddleCenter;

            var label = UiKit.Label(plate, "", size, UiTheme.GoldBright, TextAlignmentOptions.Center, bold: true);
            label.overflowMode = TextOverflowModes.Overflow;
            UiFonts.ApplyDisplay(label);

            plate.gameObject.SetActive(false);
            return label;
        }

        /// <summary>
        /// Writes <paramref name="debt"/> into a chip from <see cref="Chip"/>,
        /// hiding it at zero. Pops it when the figure changed to something owed,
        /// so a loan or a turn's interest is seen arriving. Returns whether it popped.
        /// </summary>
        public static bool Set(TMP_Text chip, int debt, int previous)
        {
            if (chip == null) return false;

            var plate = chip.transform.parent;
            bool owed = debt > 0;
            if (plate.gameObject.activeSelf != owed) plate.gameObject.SetActive(owed);
            if (!owed) return false;

            string text = Roman(debt);
            if (chip.text != text) chip.text = text;

            if (debt == previous) return false;
            UiPopIn.On(plate);
            return true;
        }

        /// <summary>"+II", for a loan landing on the board.</summary>
        public static string Loan(int amount) => "+" + Roman(amount);

        /// <summary>A burned figure, struck through.</summary>
        public static string Burned(int amount) => $"<s>{Roman(amount)}</s>";
    }
}
