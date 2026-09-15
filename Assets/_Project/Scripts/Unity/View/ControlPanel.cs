// Assets/_Project/Scripts/Unity/View/ControlPanel.cs
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The uGUI controls panel: roll, end turn, deploy, move, select and cast,
    /// plus the event log. It replaces the OnGUI panel once the stranger test
    /// passes (ADR-0008 increment D).
    /// </summary>
    /// <remarks>
    /// <b>Rebuilt, not updated.</b> The panel's contents change shape with
    /// the game (a caster selected adds an ability list, a target ability adds
    /// a target list), so it is rebuilt from the host's state whenever
    /// something changes, like a React render. Nothing is patched in place.
    /// At the rate a player clicks, the garbage this creates does not matter,
    /// and the panel can never show a stale button.
    ///
    /// <b>The rebuild waits until LateUpdate.</b> A click marks the panel
    /// dirty instead of rebuilding right away, so a button is never destroyed
    /// while its own click handler is running. Old children are deactivated
    /// before <c>Destroy</c>, because Destroy only takes effect at the end of
    /// the frame and a layout group still counts active children until then.
    ///
    /// <b>Every rule comes from the host</b> (PRESENTATION §1): readiness from
    /// <c>CheckAbility</c>, targets from <c>CastTargets</c>, whether End Turn
    /// is allowed from <c>MustSpendRoll</c>. The panel decides only how
    /// things look.
    ///
    /// <b>The background catches the pointer, on purpose.</b> It is the one
    /// display element that must block board clicks: the EventSystem guard in
    /// <c>HandleBoardClick</c> relies on it (ADR-0008 consequence 4). Text
    /// never catches the pointer (consequence 9).
    ///
    /// <b>Whose turn and energy are not repeated here.</b> The turn strip owns
    /// them, and a second copy would be one more place for them to disagree.
    /// </remarks>
    public sealed class ControlPanel : MonoBehaviour
    {
        /// <summary>Panel width in canvas units, at 1080p.</summary>
        public const float Width = 400f;

        private const float Margin = 10f;
        private const float ButtonHeight = 38f;

        // Text sizes in canvas units at 1080p. They shrink with the window
        // (CanvasScaler), so they are set for a 1280×720 Game view to stay
        // readable: the smallest here renders at about 11 px there.
        private const float FontBody = 20f;
        private const float FontSmall = 17f;
        private const float FontHeading = 23f;
        private const float FontButton = 19f;
        private const float FontLog = 16f;
        private const float FontHint = 18f;
        private const int LogLines = 10;

        /// <summary>Canvas units the panel claims from the left edge, margins included.</summary>
        public static float ReservedWidth => Margin + Width + Margin;

        private static readonly Color Background = new Color(0.07f, 0.07f, 0.09f, 0.92f);
        private static readonly Color ButtonNormal = new Color(0.20f, 0.20f, 0.24f);
        private static readonly Color ButtonSelected = new Color(0.55f, 0.42f, 0.16f);
        private static readonly Color ButtonDisabled = new Color(0.13f, 0.13f, 0.15f);
        private static readonly Color TextNormal = new Color(0.92f, 0.92f, 0.94f);
        private static readonly Color TextDim = new Color(0.60f, 0.60f, 0.64f);
        private static readonly Color TextDisabled = new Color(0.46f, 0.46f, 0.50f);

        /// <summary>Whether the panel is shown. MatchBootstrap sets it every frame.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>Whether the one-line key hint is shown. MatchBootstrap sets it every frame.</summary>
        public bool HintVisible { get; set; }

        private IControlPanelHost _host;
        private RectTransform _panel;
        private RectTransform _content;
        private GameObject _hint;
        private bool _dirty;

        /// <summary>
        /// Builds the panel once and points it at the host. Safe to call on
        /// every NewMatch.
        /// </summary>
        public void Bind(RectTransform canvasRect, IControlPanelHost host)
        {
            _host = host;

            if (_panel == null) Build(canvasRect);

            _dirty = true;
        }

        /// <summary>Asks for a rebuild at the end of this frame.</summary>
        public void MarkDirty() => _dirty = true;

        /// <summary>Player-facing words for why an ability cannot be cast.</summary>
        public static string Explain(AbilityAvailability availability)
        {
            switch (availability)
            {
                case AbilityAvailability.OnCooldown: return "cooling down";
                case AbilityAvailability.InsufficientEnergy: return "not enough energy";
                case AbilityAvailability.CasterStunned: return "stunned";
                case AbilityAvailability.CasterOutOfPlay: return "out of play";
                default: return "";
            }
        }

        private void LateUpdate()
        {
            if (_panel == null) return;

            if (_panel.gameObject.activeSelf != Visible)
            {
                _panel.gameObject.SetActive(Visible);

                // Nothing is rebuilt while hidden, so catch up on the way back.
                if (Visible) _dirty = true;
            }

            if (_hint.activeSelf != HintVisible) _hint.SetActive(HintVisible);

            if (_dirty && Visible)
            {
                _dirty = false;
                Rebuild();
            }
        }

        // ── Scaffold ─────────────────────────────────────────────────────

        private void Build(RectTransform canvasRect)
        {
            _panel = NewRect("control_panel", canvasRect);
            _panel.anchorMin = new Vector2(0f, 0f);
            _panel.anchorMax = new Vector2(0f, 1f);
            _panel.pivot = new Vector2(0f, 0.5f);
            _panel.offsetMin = new Vector2(Margin, Margin);
            _panel.offsetMax = new Vector2(Margin + Width, -Margin);

            var background = _panel.gameObject.AddComponent<Image>();
            background.color = Background;
            background.raycastTarget = true;   // see remarks: this one blocks board clicks

            // Scrolling is the floor, as it was for the OnGUI panel: six
            // enemies in reach plus an ability list outgrow a short window.
            var scroll = _panel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var viewport = NewRect("viewport", _panel);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            _content = NewRect("content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;

            var column = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(12, 12, 12, 12);
            column.spacing = 4f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            var fit = _content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = _content;

            // The key hint lives on the canvas, not in the panel, because its
            // job is to be seen when the panel is not.
            var hint = NewRect("controls_hint", canvasRect);
            hint.anchorMin = new Vector2(0f, 1f);
            hint.anchorMax = new Vector2(0f, 1f);
            hint.pivot = new Vector2(0f, 1f);
            hint.anchoredPosition = new Vector2(Margin, -Margin);
            hint.sizeDelta = new Vector2(460f, 24f);

            var hintText = hint.gameObject.AddComponent<TextMeshProUGUI>();
            hintText.raycastTarget = false;
            hintText.fontSize = FontHint;
            hintText.color = TextDim;
            hintText.textWrappingMode = TextWrappingModes.NoWrap;
            hintText.text = "<b>Tab</b> controls   <b>H</b> health   <b>F2</b> switch panel";

            _hint = hint.gameObject;
            _hint.SetActive(false);
        }

        // ── Content ──────────────────────────────────────────────────────

        private void Rebuild()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            var match = _host?.Match;
            if (match == null) return;

            var engine = match.Engine;

            // The profile's own description, not restated numbers: a
            // hard-coded label is one more place to forget.
            AddLabel(_content, $"{match.Map.Profile} · {(_host.RandomSquads ? "drafted squads" : "alpha three")}",
                size: FontSmall, colour: TextDim);

            AddButton(_content, "New match", _host.Reseed);

            if (engine.MatchOver)
            {
                DrawLog();
                return;
            }

            AddSpace(6f);

            // Movement and rolling are compulsory (§6.1), so the engine refuses
            // End Turn while a roll is owed. The button says so before the click.
            bool owesMovement = engine.MustSpendRoll;

            var turnRow = Row(_content);
            AddButton(turnRow, "Roll", _host.Roll, flexible: true);
            AddButton(turnRow, "End turn", _host.EndTurn, interactable: !owesMovement, flexible: true);

            if (owesMovement)
            {
                AddLabel(_content, engine.Phase == TurnPhase.AwaitingRoll
                        ? "Roll before ending the turn."
                        : "Spend your roll before ending the turn.",
                    size: FontSmall, colour: TextDim);
            }

            DrawOperators(engine.CurrentPlayer, engine.UnspentDice);

            if (_host.SelectedCaster != null) DrawAbilities(engine.CurrentPlayer);

            DrawLog();
        }

        private void DrawOperators(PlayerState seat, IReadOnlyList<int> dice)
        {
            Heading("Your operators");

            foreach (var op in seat.Operators)
            {
                var row = Row(_content);

                AddLabel(row, $"{op.Name} <color=#{Hex(TextDim)}>{op.Health}/{op.MaxHealth}</color>", width: 150f);

                if (op.IsInYard) AddButton(row, "Deploy", () => _host.Deploy(op), width: 104f);
                else DrawMoveButtons(row, op, dice);

                Filler(row);

                bool selected = ReferenceEquals(op, _host.SelectedCaster);
                AddButton(row, "Cast", () => _host.ToggleCaster(op), selected: selected, width: 68f);
            }
        }

        /// <summary>
        /// One button per way to spend the roll: all of it, or a single die.
        /// </summary>
        /// <remarks>
        /// Labelled with pips, not cells, for the same reason as the OnGUI
        /// panel: cells depend on speed, and the board already shows where
        /// each option lands.
        /// </remarks>
        private void DrawMoveButtons(Transform row, OperatorState op, IReadOnlyList<int> dice)
        {
            if (dice.Count == 0)
            {
                AddLabel(row, "—", width: 104f, colour: TextDim);
                return;
            }

            if (dice.Count == 1)
            {
                AddButton(row, $"Move {dice[0]}", () => _host.Move(op, null), width: 104f);
                return;
            }

            int total = dice.Sum();
            AddButton(row, $"<b>{total}</b>", () => _host.Move(op, null), width: 46f);

            foreach (int face in dice.Distinct())
                AddButton(row, face.ToString(), () => _host.Move(op, face), width: 34f);
        }

        private void DrawAbilities(PlayerState seat)
        {
            var caster = _host.SelectedCaster;
            var engine = _host.Match.Engine;

            Heading(caster.Name);

            if (!_host.Match.AbilitiesByOperator.TryGetValue(caster.Id, out var abilities)) return;

            // Select, then cast (PRESENTATION §4): selecting shows the reach
            // on the board before any energy is spent.
            foreach (var ability in abilities)
            {
                bool chosen = _host.SelectedAbility != null && _host.SelectedAbility.Id == ability.Id;

                var availability = engine.CheckAbility(caster, ability);
                bool usable = availability == AbilityAvailability.Ready;

                string reach = ability.HasUnlimitedRange ? "any range" : $"range {ability.Range}";

                string label = usable
                    ? $"{ability.Name}  <color=#{Hex(TextDim)}>{ability.EnergyCost}e · {reach}</color>"
                    : $"{ability.Name} — {Explain(availability)}";

                AddButton(_content, label, () => _host.ToggleAbility(ability),
                    interactable: usable || chosen, selected: chosen, leftAlign: true);

                if (chosen && !string.IsNullOrEmpty(ability.Description))
                    AddLabel(_content, ability.Description, size: FontSmall, colour: TextDim);
            }

            var selectedAbility = _host.SelectedAbility;
            if (selectedAbility == null) return;

            if (selectedAbility.RequiresTarget) DrawTargets(seat);
            else if (selectedAbility.RequiresCell)
            {
                AddLabel(_content, _host.SelectedCell == null
                        ? "Click a highlighted cell on the board."
                        : $"Target cell: <b>{_host.SelectedCell.Value}</b> — click another to change.",
                    size: FontSmall, colour: TextDim);
            }
            else AddLabel(_content, "No target — it fires around the caster.", size: FontSmall, colour: TextDim);

            AddSpace(2f);

            bool ready = _host.CastReady;
            string missing = selectedAbility.RequiresCell ? "click a cell" : "pick a target";

            AddButton(_content,
                ready ? $"<b>CAST {selectedAbility.Name}</b>" : $"CAST {selectedAbility.Name} — {missing}",
                _host.Cast, interactable: ready);
        }

        /// <summary>Legal targets, split by side. The list comes from the host.</summary>
        private void DrawTargets(PlayerState seat)
        {
            Heading("Target");

            var legal = _host.CastTargets();

            if (legal.Count == 0)
            {
                AddLabel(_content, "Nothing in reach.", size: FontSmall, colour: TextDim);
                return;
            }

            DrawTargetGroup("Enemies", legal.Where(o => o.Owner != seat.Color));
            DrawTargetGroup("Allies", legal.Where(o => o.Owner == seat.Color));
        }

        private void DrawTargetGroup(string heading, IEnumerable<OperatorState> candidates)
        {
            var list = candidates.ToList();
            if (list.Count == 0) return;

            AddLabel(_content, heading, size: FontSmall, colour: TextDim);

            foreach (var candidate in list)
            {
                bool selected = ReferenceEquals(candidate, _host.SelectedTarget);
                string owner = Hex(Color.Lerp(BoardLayout.ColourOf(candidate.Owner), Color.white, 0.35f));

                AddButton(_content,
                    $"<color=#{owner}>{candidate.Owner}</color> {candidate.Name}  {candidate.Health}/{candidate.MaxHealth}",
                    () => _host.ToggleTarget(candidate), selected: selected, leftAlign: true);
            }
        }

        /// <summary>
        /// The last few events, newest first. A tail, not a record, so there
        /// is no scroll of its own inside the panel's scroll.
        /// </summary>
        private void DrawLog()
        {
            Heading("Events");

            var log = _host.Log;

            for (int i = log.Count - 1; i >= 0 && i >= log.Count - LogLines; i--)
                AddLabel(_content, log[i], size: FontLog, colour: TextDim, rich: false);
        }

        // ── Widgets ──────────────────────────────────────────────────────

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static string Hex(Color colour) => ColorUtility.ToHtmlStringRGB(colour);

        private void Heading(string text)
        {
            AddSpace(8f);
            AddLabel(_content, $"<b>{text}</b>", size: FontHeading);
        }

        private void AddSpace(float height)
        {
            var rect = NewRect("space", _content);
            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.minHeight = height;
            size.preferredHeight = height;
        }

        private static RectTransform Row(Transform parent)
        {
            var rect = NewRect("row", parent);

            var row = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 4f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            return rect;
        }

        /// <summary>Takes up the slack in a row, pushing what follows to the right edge.</summary>
        private static void Filler(Transform row)
        {
            var rect = NewRect("filler", row);
            rect.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private static void AddLabel(
            Transform parent, string text,
            float width = -1f, float size = FontBody, Color? colour = null, bool rich = true)
        {
            var rect = NewRect("label", parent);

            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.richText = rich;
            label.fontSize = size;
            label.color = colour ?? TextNormal;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = width > 0f ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
            label.overflowMode = width > 0f ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            label.text = text;

            if (width > 0f)
            {
                var fixedWidth = rect.gameObject.AddComponent<LayoutElement>();
                fixedWidth.minWidth = width;
                fixedWidth.preferredWidth = width;
            }
        }

        private void AddButton(
            Transform parent, string text, System.Action onClick,
            bool interactable = true, bool selected = false,
            float width = -1f, bool flexible = false, bool leftAlign = false)
        {
            var rect = NewRect("button", parent);

            // White, so the button's tint is the colour shown rather than
            // being multiplied by a second one.
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = interactable;

            // No keyboard navigation: Enter would re-press whatever was
            // clicked last, and arrow keys have no meaning on this panel.
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var tint = selected ? ButtonSelected : ButtonNormal;
            var colours = ColorBlock.defaultColorBlock;
            colours.normalColor = tint;
            colours.highlightedColor = Color.Lerp(tint, Color.white, 0.15f);
            colours.pressedColor = Color.Lerp(tint, Color.black, 0.25f);
            colours.selectedColor = tint;
            colours.disabledColor = ButtonDisabled;
            colours.colorMultiplier = 1f;
            button.colors = colours;

            button.onClick.AddListener(() =>
            {
                // Drop focus, so Enter cannot press this button again.
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

                onClick();
                MarkDirty();
            });

            var size = rect.gameObject.AddComponent<LayoutElement>();
            size.minHeight = ButtonHeight;
            size.preferredHeight = ButtonHeight;

            if (width > 0f)
            {
                size.minWidth = width;
                size.preferredWidth = width;
            }

            if (flexible) size.flexibleWidth = 1f;

            var labelRect = NewRect("text", rect);
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(leftAlign ? 10f : 4f, 0f);
            labelRect.offsetMax = new Vector2(leftAlign ? -10f : -4f, 0f);

            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.fontSize = FontButton;
            label.color = interactable ? TextNormal : TextDisabled;
            label.alignment = leftAlign ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.text = text;
        }
    }
}
