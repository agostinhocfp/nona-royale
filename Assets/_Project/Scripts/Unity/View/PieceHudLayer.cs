// Assets/_Project/Scripts/Unity/View/PieceHudLayer.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The screen-space readouts tracked to every deployed piece: a health
    /// label above it and a row of named status tags below it.
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
    /// <b>Status tags carry a word, not only a colour.</b> The old badges were
    /// world-space squares about six pixels wide at 1080p, told apart by colour
    /// alone — a stranger could not tell a stun from a mark, and "a status that
    /// cannot be seen may as well not apply" (§2). Single letters were rejected
    /// because four kinds start with S. The word comes from
    /// <see cref="StatusPalette.Label"/>, whose default case names any kind
    /// added later, so a new status still appears, named (§5).
    ///
    /// <b>Health is togglable; statuses are not.</b> Whether always-on health
    /// is signal or noise is exactly what the stranger test exists to judge
    /// (phase brief §5.2), so H switches it. Statuses are on the §2
    /// always-visible list without qualification, so H leaves them alone.
    ///
    /// <b>Yard operators show nothing.</b> Neutralize restores health on the
    /// way to the yard (<c>OperatorState.RestoreHealth</c>), so a yard label
    /// always reads full and says nothing three stacked corner labels are
    /// worth; a passive on a benched operator decides nothing until it deploys.
    /// Operators that reached home still show theirs; whether that is clutter
    /// is a stranger-test finding, not a guess to make here.
    ///
    /// Nothing here is a raycast target (ADR-0008 consequence 9). Labels sit
    /// over the board, and a label that swallowed a click would re-aim a cell
    /// cast — the exact failure the panel-rect guard exists to prevent.
    /// </remarks>
    public sealed class PieceHudLayer : MonoBehaviour
    {
        private const int MaxTags = 6;
        private const float TagHeight = 18f;
        private const int TagPadding = 5;
        private const float TagFontSize = 11f;

        /// <summary>Canvas units between the readouts of stacked pieces: a label's width plus a gap.</summary>
        private const float StackSpacing = 68f;

        /// <summary>Health switch. MatchBootstrap drives it from its inspector flag and the H key.</summary>
        public bool Visible { get; set; } = true;

        private sealed class Entry
        {
            public OperatorPiece Piece;

            public GameObject Root;
            public RectTransform Rect;
            public TMP_Text Text;
            public int LastHealth = int.MinValue;
            public bool Shown = true;

            public GameObject StatusRoot;
            public RectTransform StatusRect;
            public readonly List<GameObject> Tags = new List<GameObject>();
            public readonly List<Image> TagBacks = new List<Image>();
            public readonly List<TMP_Text> TagTexts = new List<TMP_Text>();
            public readonly List<StatusKind> Statuses = new List<StatusKind>();
            public bool StatusShown = true;

            /// <summary>Canvas units to shift this piece's readouts sideways, when it shares a cell.</summary>
            public float StackShift;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<StatusKind> _tagged = new List<StatusKind>();
        private RectTransform _canvasRect;
        private float _worldOffset;

        /// <summary>
        /// Builds one label and one status row per piece under the HUD canvas.
        /// Called from NewMatch, so it clears whatever the previous match left
        /// behind.
        /// </summary>
        /// <param name="canvasRect">The HUD canvas rect (<see cref="HudRoot.Root"/>).</param>
        /// <param name="pieces">The live piece list. Read, never mutated.</param>
        /// <param name="worldOffset">
        /// How far from the piece's centre the readouts sit, in world units:
        /// health this far above, statuses this far below.
        /// </param>
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
            {
                if (entry.Root != null) Destroy(entry.Root);
                if (entry.StatusRoot != null) Destroy(entry.StatusRoot);
            }

            _entries.Clear();
        }

        /// <summary>
        /// Sets the statuses shown under a piece. The list comes from
        /// <c>GameEngine.ActiveStatusesOn</c>, never from events (§1), and is
        /// copied, so the caller's list can be reused. Statuses the piece draws
        /// itself (<see cref="StatusPalette.IsDrawnOnPiece"/>) get no tag.
        /// </summary>
        public void ShowStatuses(OperatorPiece piece, IReadOnlyList<StatusKind> statuses)
        {
            var entry = _entries.Find(e => ReferenceEquals(e.Piece, piece));
            if (entry == null) return;

            _tagged.Clear();

            if (statuses != null)
            {
                foreach (var kind in statuses)
                    if (!StatusPalette.IsDrawnOnPiece(kind)) _tagged.Add(kind);
            }

            statuses = _tagged;
            int count = Mathf.Min(statuses.Count, MaxTags);

            if (SameAs(entry.Statuses, statuses, count)) return;

            entry.Statuses.Clear();

            for (int i = 0; i < MaxTags; i++)
            {
                bool used = i < count;
                entry.Tags[i].SetActive(used);
                if (!used) continue;

                var kind = statuses[i];
                entry.Statuses.Add(kind);

                var colour = StatusPalette.For(kind);
                string label = StatusPalette.Label(kind);

                entry.TagBacks[i].color = colour;
                entry.TagTexts[i].color = ReadableOn(colour);
                entry.TagTexts[i].text = label;
            }
        }

        /// <summary>
        /// Tells the layer where a piece sits in a shared cell, so its readouts
        /// can be spread apart.
        /// </summary>
        /// <remarks>
        /// The pieces themselves fan by a third of a cell (<c>BoardLayout.Offset</c>),
        /// but a health label is wider than that, so two labels on one cell
        /// printed on top of each other ("9/6/6"). Readouts are spread by a
        /// label's width instead, centred on the cell.
        /// </remarks>
        public void SetStack(OperatorPiece piece, int index, int count)
        {
            var entry = _entries.Find(e => ReferenceEquals(e.Piece, piece));
            if (entry == null) return;

            entry.StackShift = count <= 1 ? 0f : (index - (count - 1) * 0.5f) * StackSpacing;
        }

        private static bool SameAs(List<StatusKind> shown, IReadOnlyList<StatusKind> next, int count)
        {
            if (shown.Count != count) return false;

            for (int i = 0; i < count; i++)
                if (shown[i] != next[i]) return false;

            return true;
        }

        /// <summary>Dark text on a light tag, white on a dark one.</summary>
        private static Color ReadableOn(Color background) => UiTheme.TextOn(background);

        private Entry Build(OperatorPiece piece)
        {
            var entry = new Entry { Piece = piece };

            BuildHealth(entry);
            BuildStatusRow(entry);

            return entry;
        }

        private void BuildHealth(Entry entry)
        {
            var piece = entry.Piece;

            var go = new GameObject($"hp_{piece.Operator.Owner}_{piece.Operator.Name}", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_canvasRect, false);
            rect.sizeDelta = new Vector2(64f, 24f);

            // An Image with no sprite draws a plain tinted quad — a dark
            // backing that keeps the number readable over safe-cell colours
            // and highlight rings alike.
            var backing = go.AddComponent<Image>();
            backing.sprite = DecoSprites.ChipFill;
            backing.type = Image.Type.Sliced;
            backing.color = UiTheme.WithAlpha(UiTheme.Obsidian, 0.6f);
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
            text.color = UiTheme.Readable(BoardLayout.ColourOf(piece.Operator.Owner));

            entry.Root = go;
            entry.Rect = rect;
            entry.Text = text;
        }

        private void BuildStatusRow(Entry entry)
        {
            var piece = entry.Piece;

            var go = new GameObject($"status_{piece.Operator.Owner}_{piece.Operator.Name}", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_canvasRect, false);

            // Hangs from its top edge, so the row grows away from the piece.
            rect.pivot = new Vector2(0.5f, 1f);

            var row = go.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 3f;
            row.childAlignment = TextAnchor.UpperCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < MaxTags; i++)
            {
                var tag = new GameObject($"tag_{i}", typeof(RectTransform));
                tag.transform.SetParent(rect, false);

                var back = tag.AddComponent<Image>();
                back.sprite = DecoSprites.ChipFill;
                back.type = Image.Type.Sliced;
                back.raycastTarget = false;

                // Each tag is sized by its own layout group from the word it
                // holds. Nested layout groups are the supported way to do that;
                // a ContentSizeFitter per tag would fight the row's group, and
                // Unity warns about it.
                var pad = tag.AddComponent<HorizontalLayoutGroup>();
                pad.padding = new RectOffset(TagPadding, TagPadding, 0, 0);
                pad.childAlignment = TextAnchor.MiddleCenter;
                pad.childControlWidth = true;
                pad.childControlHeight = true;
                pad.childForceExpandWidth = false;
                pad.childForceExpandHeight = true;

                // Height is fixed; width is left to the group above.
                var size = tag.AddComponent<LayoutElement>();
                size.minHeight = TagHeight;
                size.preferredHeight = TagHeight;

                var textGo = new GameObject("text", typeof(RectTransform));
                textGo.transform.SetParent(tag.transform, false);

                var text = textGo.AddComponent<TextMeshProUGUI>();
                text.raycastTarget = false;
                text.alignment = TextAlignmentOptions.Center;
                text.fontSize = TagFontSize;
                text.fontStyle = FontStyles.Bold;
                text.textWrappingMode = TextWrappingModes.NoWrap;

                tag.SetActive(false);

                entry.Tags.Add(tag);
                entry.TagBacks.Add(back);
                entry.TagTexts.Add(text);
            }

            entry.StatusRoot = go;
            entry.StatusRect = rect;
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
                var centre = entry.Piece.transform.position;

                bool showHealth = Visible && !op.IsInYard;
                bool showStatuses = !op.IsInYard && entry.Statuses.Count > 0;

                if (showHealth != entry.Shown)
                {
                    entry.Root.SetActive(showHealth);
                    entry.Shown = showHealth;
                }

                if (showStatuses != entry.StatusShown)
                {
                    entry.StatusRoot.SetActive(showStatuses);
                    entry.StatusShown = showStatuses;
                }

                if (showHealth)
                {
                    // Text is rebuilt only on change — TMP re-layouts on every
                    // assignment, and health changes on events, not frames.
                    if (op.Health != entry.LastHealth)
                    {
                        entry.LastHealth = op.Health;
                        entry.Text.text = $"{op.Health}/{op.MaxHealth}";
                    }

                    entry.Rect.anchoredPosition = ToCanvas(camera, centre + Vector3.up * _worldOffset)
                                                  + new Vector2(entry.StackShift, 0f);
                }

                if (showStatuses)
                    entry.StatusRect.anchoredPosition = ToCanvas(camera, centre + Vector3.down * _worldOffset)
                                                        + new Vector2(entry.StackShift, 0f);
            }
        }

        private Vector2 ToCanvas(Camera camera, Vector3 world)
        {
            Vector2 screen = camera.WorldToScreenPoint(world);

            // Overlay canvas, so no camera in the conversion. The scaler
            // makes canvas units ≠ pixels; this is the conversion that
            // respects it.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screen, null, out var local);

            return local;
        }
    }
}
