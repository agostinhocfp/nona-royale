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
    /// <b>Nor do operators that reached HOME</b> (G6a). They are out of the
    /// fight for good, so their health and passives decide nothing, and every
    /// finished piece shares the one vault: three seats' labels and tags piled
    /// up on the busiest spot on the board.
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

        /// <summary>Canvas units between the status rows of stacked pieces: a tag's height plus a gap.</summary>
        private const float StackRowSpacing = TagHeight + 3f;

        /// <summary>Health switch. MatchBootstrap drives it from its inspector flag and the H key.</summary>
        public bool Visible { get; set; } = true;

        // What the player is pointing at and what they have selected, so a
        // full-health piece can still show its readout on request (H3).
        private OperatorState _hovered;
        private OperatorState _selected;

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

            /// <summary>Which line its status row takes under a shared cell: 0 alone, else its place in the stack.</summary>
            public int StackRow;

            /// <summary>Finished: at HOME, out of play, and so without readouts.</summary>
            public bool Retired;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<StatusKind> _tagged = new List<StatusKind>();
        private RectTransform _canvasRect;
        private float _worldOffset;

        /// <summary>
        /// Canvas units at the top of the screen that readouts may not enter
        /// (V1c). The top bar and the turn banner live there, and a stack at
        /// the head of the north arm spreads its labels straight into the
        /// banner's pill: the bug reads "RED10/10urn 7/7d 1". Zero disables it.
        /// </summary>
        private float _ceiling;

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

        /// <summary>
        /// How much of the screen's top edge is spoken for, in canvas units
        /// (V1c). The composition root sets it whenever it frames the camera,
        /// the same way it sets the toasts', banner's and turn button's areas.
        /// </summary>
        public void SetCeiling(float canvasUnits) => _ceiling = Mathf.Max(0f, canvasUnits);

        /// <summary>
        /// The two pieces whose readout shows whatever their health (H3): the
        /// one under the pointer and the one selected. Driven every frame by
        /// the composition root, so it cannot go stale behind a selection that
        /// changed somewhere else.
        /// </summary>
        public void SetFocus(OperatorState hovered, OperatorState selected)
        {
            _hovered = hovered;
            _selected = selected;
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

                // Muted for the board (H3). ReadableOn takes the muted colour
                // too, so the word keeps its contrast against what it sits on.
                var colour = StatusPalette.OnBoard(kind);
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
        ///
        /// Status rows also step down a line each (G6a). A row is as wide as
        /// its words: one BALANCE tag is wider than the label spacing, so two
        /// rows spread only sideways printed "BALANCE" over "…LANCE". Lines
        /// cannot collide whatever the words, and each row still leans
        /// toward its own piece.
        /// </remarks>
        public void SetStack(OperatorPiece piece, int index, int count)
        {
            var entry = _entries.Find(e => ReferenceEquals(e.Piece, piece));
            if (entry == null) return;

            entry.StackShift = count <= 1 ? 0f : (index - (count - 1) * 0.5f) * StackSpacing;
            entry.StackRow = count <= 1 ? 0 : index;
        }

        /// <summary>Marks a piece as finished (at HOME), which hides its readouts.</summary>
        public void SetRetired(OperatorPiece piece, bool retired)
        {
            var entry = _entries.Find(e => ReferenceEquals(e.Piece, piece));
            if (entry != null) entry.Retired = retired;
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
            UiFonts.ApplyData(text);

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
                UiFonts.ApplyData(text);
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

                // The label follows the piece, not the engine (MO2): a hit's
                // number and the label change together, and a shattered piece
                // shows nothing until it is seated again.
                bool standing = !entry.Piece.Seated && !entry.Piece.IsHidden && !entry.Retired;

                // A piece at full health says nothing (H3). Eight readouts over
                // eight untouched pieces was most of the board's clutter, and
                // every one repeated a number the rail already carried. Damage
                // is worth interrupting for; hover and selection are the player
                // asking.
                bool hurt = entry.Piece.ShownHealth < op.MaxHealth;
                bool focused = ReferenceEquals(op, _hovered) || ReferenceEquals(op, _selected);
                bool showHealth = Visible && standing && (hurt || focused);
                bool showStatuses = standing && entry.Statuses.Count > 0;

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
                    int shown = entry.Piece.ShownHealth;
                    if (shown != entry.LastHealth)
                    {
                        entry.LastHealth = shown;
                        entry.Text.text = $"{shown}/{op.MaxHealth}";
                    }

                    // Along whatever reads as up on screen, not world up: under
                    // the tilt those are different, and a readout offset along
                    // world up slides up the table instead of off it (V1c).
                    entry.Rect.anchoredPosition = UnderCeiling(
                        ToCanvas(camera, centre + BoardTilt.ScreenUp * _worldOffset)
                            + new Vector2(entry.StackShift, 0f),
                        entry.Rect);
                }

                if (showStatuses)
                    entry.StatusRect.anchoredPosition = ToCanvas(camera, centre - BoardTilt.ScreenUp * _worldOffset)
                                                        + new Vector2(entry.StackShift, -entry.StackRow * StackRowSpacing);
            }
        }

        /// <summary>
        /// Holds a readout below the reserved top edge (V1c). Clamped, not
        /// flipped: a piece in the north arm's top row only needs to come down
        /// by about its own height, so it stays plainly attached to its piece,
        /// and clamping cannot push it onto the status tags the way a flip can.
        /// </summary>
        private Vector2 UnderCeiling(Vector2 at, RectTransform rect)
        {
            if (_ceiling <= 0f || _canvasRect == null) return at;

            float top = _canvasRect.rect.height * 0.5f - _ceiling - rect.sizeDelta.y * 0.5f;
            if (at.y > top) at.y = top;
            return at;
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
