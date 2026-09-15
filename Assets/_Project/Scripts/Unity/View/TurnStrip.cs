// Assets/_Project/Scripts/Unity/View/TurnStrip.cs
using NonaRoyale.Core;
using NonaRoyale.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// One always-on line at the top of the screen: whose turn it is, their
    /// energy, and what the turn is waiting for.
    /// </summary>
    /// <remarks>
    /// PRESENTATION §2 puts "whose turn, and their energy" in the always-visible
    /// table, and until now it lived only in the OnGUI panel, so Tab took it
    /// away. A stranger who hides the panel must still know whose move it is.
    ///
    /// <b>Every value comes from the engine</b> (PRESENTATION §1). The prompt
    /// only reports the phase, the unspent dice and <c>MustSpendRoll</c>. It
    /// never works out what a die could do. The energy cap is not shown,
    /// because <c>GameEngine</c> does not expose it, and hard-coding 12 here
    /// would be a second place to forget when the cap is tuned. If the
    /// stranger test asks for "7/12", that is a new engine query, not a
    /// literal in this file.
    ///
    /// <b>It owns the top edge.</b> <see cref="ReservedHeight"/> is what
    /// FrameCamera keeps clear, so the top arm of the cross is never drawn
    /// under the strip. The height is fixed rather than fitted to the text,
    /// so the reservation is known before the first layout pass runs.
    ///
    /// <b>It is centred over the board, not over the screen.</b> While the
    /// OnGUI panel is up, the board sits right of centre; a strip centred on
    /// the screen would drift toward the panel at narrow widths.
    ///
    /// Nothing here is a raycast target (ADR-0008 consequence 9).
    /// </remarks>
    public sealed class TurnStrip : MonoBehaviour
    {
        private const float TopOffset = 12f;
        private const float Height = 44f;
        private const float Gap = 8f;

        /// <summary>Canvas units, at 1080p, that the strip claims from the top edge.</summary>
        public static float ReservedHeight => TopOffset + Height + Gap;

        private static readonly Color Backing = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color Neutral = new Color(0.55f, 0.55f, 0.58f);

        private RectTransform _rect;
        private Image _accent;
        private TMP_Text _text;
        private string _shown;

        /// <summary>
        /// Builds the strip under the HUD canvas. Safe to call on every
        /// NewMatch: the strip tracks no piece, so it survives a reseed.
        /// </summary>
        public void Bind(RectTransform canvasRect)
        {
            if (_rect != null) return;

            var go = new GameObject("turn_strip", typeof(RectTransform));
            _rect = (RectTransform)go.transform;
            _rect.SetParent(canvasRect, false);
            _rect.anchorMin = new Vector2(0.5f, 1f);
            _rect.anchorMax = new Vector2(0.5f, 1f);
            _rect.pivot = new Vector2(0.5f, 1f);
            _rect.anchoredPosition = new Vector2(0f, -TopOffset);
            _rect.sizeDelta = new Vector2(0f, Height);

            var backing = go.AddComponent<Image>();
            backing.color = Backing;
            backing.raycastTarget = false;

            // Width follows the text; height stays fixed (see remarks).
            var row = go.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(0, 18, 0, 0);
            row.spacing = 14f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;

            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            // A bar in the seat colour on the left edge. The name is coloured
            // too, but a block of colour can be read from across the table.
            var accentGo = new GameObject("seat", typeof(RectTransform));
            accentGo.transform.SetParent(_rect, false);

            _accent = accentGo.AddComponent<Image>();
            _accent.color = Neutral;
            _accent.raycastTarget = false;

            var accentSize = accentGo.AddComponent<LayoutElement>();
            accentSize.minWidth = 8f;
            accentSize.preferredWidth = 8f;

            var textGo = new GameObject("text", typeof(RectTransform));
            textGo.transform.SetParent(_rect, false);

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.fontSize = 22f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = true;

            _text = text;
            _shown = null;
        }

        /// <summary>
        /// Rewrites the line from the engine. Called after every batch of
        /// events. The text is set only when it changes, because TMP lays
        /// the text out again on every assignment.
        /// </summary>
        public void Refresh(GameEngine engine)
        {
            if (_text == null || engine == null) return;

            string line;
            Color seatColour;

            if (engine.MatchOver)
            {
                line = "<b>MATCH OVER</b>";
                seatColour = Neutral;
            }
            else
            {
                var seat = engine.CurrentPlayer;
                seatColour = BoardLayout.ColourOf(seat.Color);

                // Nudged toward white for the same reason as the piece
                // labels: raw seat blue is nearly invisible on a dark backing.
                var nameColour = ColorUtility.ToHtmlStringRGB(Color.Lerp(seatColour, Color.white, 0.35f));

                line = $"<color=#{nameColour}><b>{seat.Color.ToString().ToUpperInvariant()}</b></color>" +
                       $"   turn {seat.TurnIndex}   ·   energy <b>{seat.Energy}</b>   ·   {Prompt(engine)}";
            }

            if (line == _shown) return;

            _shown = line;
            _text.text = line;
            _accent.color = seatColour;
        }

        /// <summary>
        /// Moves the strip so it sits centred over the part of the screen the
        /// board uses.
        /// </summary>
        /// <param name="leftInsetPixels">Screen pixels reserved on the left (the OnGUI panel), or 0.</param>
        /// <param name="scaleFactor">The HUD canvas scale factor, to turn pixels into canvas units.</param>
        public void CentreOver(float leftInsetPixels, float scaleFactor)
        {
            if (_rect == null) return;

            float x = leftInsetPixels * 0.5f / Mathf.Max(0.01f, scaleFactor);
            _rect.anchoredPosition = new Vector2(x, -TopOffset);
        }

        private static string Prompt(GameEngine engine)
        {
            var dice = engine.UnspentDice;

            switch (engine.Phase)
            {
                case TurnPhase.AwaitingRoll:
                    return "roll the dice";

                case TurnPhase.Action:
                    if (dice.Count == 0) return "dice spent — end turn when ready";

                    // MustSpendRoll is the engine's answer to "is there a legal
                    // move for these dice". While dice are left, false means
                    // none of them can be spent.
                    return engine.MustSpendRoll
                        ? $"move <b>{string.Join(" + ", dice)}</b>"
                        : $"no legal move for {string.Join(" + ", dice)} — end turn";

                default:
                    return "";
            }
        }
    }
}
