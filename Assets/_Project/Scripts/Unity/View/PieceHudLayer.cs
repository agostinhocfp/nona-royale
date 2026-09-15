// Assets/_Project/Scripts/Unity/View/PieceHudLayer.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A small always-on health readout tracked to every deployed piece.
    /// </summary>
    /// <remarks>
    /// Exists because the debug tray's largest gap against PRESENTATION §2 was
    /// enemy health: visible only inside a target list, and only mid-cast.
    /// "Every combat decision is a threshold question" applies to enemies most
    /// of all — whether to charge a Bouncer is decided by his number, and the
    /// number must be readable before a cast is started, not after.
    ///
    /// <b>Screen-space, not world-space.</b> A world-space label shrinks with
    /// the framing, and the framing changes with board profile, panel state and
    /// window shape (FrameCamera). A readout legible at one zoom and not
    /// another fails the stranger test at exactly the window sizes nobody
    /// tuned for. So: one screen-space element per piece, repositioned from
    /// the piece's world position every LateUpdate — after walks have moved
    /// the transforms, so labels ride along with a walking piece.
    ///
    /// <b>Yard operators show nothing.</b> Neutralize restores health on the
    /// way to the yard (<c>OperatorState.RestoreHealth</c>), so a yard label
    /// always reads full and says nothing three stacked corner labels are
    /// worth. Operators that reached home still show theirs; whether that is
    /// clutter is a stranger-test finding, not a guess to make here.
    ///
    /// Togglable (phase brief §5.2): whether always-on health is signal or
    /// noise is exactly what the stranger test exists to judge, so the view
    /// makes it a switch rather than a commitment.
    ///
    /// Nothing here is a raycast target. Labels sit over the board, and a
    /// label that swallowed a click would re-aim a cell cast — the exact
    /// failure the panel-rect guard exists to prevent.
    /// </remarks>
    public sealed class PieceHudLayer : MonoBehaviour
    {
        /// <summary>Master switch. MatchBootstrap drives it from its inspector flag and the H key.</summary>
        public bool Visible { get; set; } = true;

        private sealed class Entry
        {
            public OperatorPiece Piece;
            public GameObject Root;
            public RectTransform Rect;
            public TMP_Text Text;
            public int LastHealth = int.MinValue;
            public bool Shown = true;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private RectTransform _canvasRect;
        private float _worldOffset;

        /// <summary>
        /// Builds one label per piece under the HUD canvas. Called from
        /// NewMatch, so it clears whatever the previous match left behind.
        /// </summary>
        /// <param name="canvasRect">The HUD canvas rect (<see cref="HudRoot.Root"/>).</param>
        /// <param name="pieces">The live piece list. Read, never mutated.</param>
        /// <param name="worldOffset">How far above the piece's centre the label sits, in world units.</param>
        public void Bind(RectTransform canvasRect, IReadOnlyList<OperatorPiece> pieces, float worldOffset)
        {
            _canvasRect = canvasRect;
            _worldOffset = worldOffset;

            Clear();

            foreach (var piece in pieces)
                _entries.Add(Build(piece));
        }

        public void Clear()
        {
            foreach (var entry in _entries)
                if (entry.Root != null) Destroy(entry.Root);

            _entries.Clear();
        }

        private Entry Build(OperatorPiece piece)
        {
            var go = new GameObject($"hp_{piece.Operator.Owner}_{piece.Operator.Name}", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_canvasRect, false);
            rect.sizeDelta = new Vector2(64f, 24f);

            // An Image with no sprite draws a plain tinted quad — a dark
            // backing that keeps the number readable over safe-cell colours
            // and highlight rings alike.
            var backing = go.AddComponent<Image>();
            backing.color = new Color(0f, 0f, 0f, 0.45f);
            backing.raycastTarget = false;

            var textGo = new GameObject("text", typeof(RectTransform));
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 16f;
            text.fontStyle = FontStyles.Bold;

            // Seat colour, so the label answers "whose" as well as "how much"
            // — but nudged toward white, because the raw seat blue is nearly
            // invisible on a dark backing over a dark board.
            text.color = Color.Lerp(BoardLayout.ColourOf(piece.Operator.Owner), Color.white, 0.35f);

            return new Entry { Piece = piece, Root = go, Rect = rect, Text = text };
        }

        private void LateUpdate()
        {
            if (_canvasRect == null || _entries.Count == 0) return;

            var camera = Camera.main;
            if (camera == null) return;

            foreach (var entry in _entries)
            {
                // NewMatch destroys pieces before Bind rebuilds; a frame can
                // slip between the two.
                if (entry.Piece == null || entry.Root == null) continue;

                var op = entry.Piece.Operator;
                bool show = Visible && !op.IsInYard;

                if (show != entry.Shown)
                {
                    entry.Root.SetActive(show);
                    entry.Shown = show;
                }

                if (!show) continue;

                // Text is rebuilt only on change — TMP re-layouts on every
                // assignment, and health changes on events, not frames.
                if (op.Health != entry.LastHealth)
                {
                    entry.LastHealth = op.Health;
                    entry.Text.text = $"{op.Health}/{op.MaxHealth}";
                }

                Vector3 world = entry.Piece.transform.position + Vector3.up * _worldOffset;
                Vector2 screen = camera.WorldToScreenPoint(world);

                // Overlay canvas, so no camera in the conversion. The scaler
                // makes canvas units ≠ pixels; this is the conversion that
                // respects it.
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, screen, null, out var local);

                entry.Rect.anchoredPosition = local;
            }
        }
    }
}