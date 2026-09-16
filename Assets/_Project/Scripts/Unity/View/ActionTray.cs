// Assets/_Project/Scripts/Unity/View/ActionTray.cs
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The bottom tray: dice with Roll and End turn, the selected operator's
    /// card, its abilities, and Cast (GUI increment F).
    /// </summary>
    /// <remarks>
    /// <b>Left to right is the order of a turn:</b> dice, pick an operator,
    /// pick an ability, cast. Roll and End turn are on the turn button at the
    /// board's corner (<see cref="TurnButton"/>). The board does the same things by clicking
    /// (PRESENTATION §4.1), and everything here calls the same intents.
    ///
    /// <b>Readiness shows when, not only whether.</b> A cooling-down ability
    /// shows "ready in N" from <c>GameEngine.TurnsUntilReady</c>, and a
    /// too-expensive one shows its cost against the pool. Both are engine
    /// answers (PRESENTATION §1).
    ///
    /// <b>Select, then cast</b> (PRESENTATION §4): choosing an ability draws
    /// its reach on the board, and nothing is spent until Cast or Enter.
    ///
    /// <b>The dice arrive from the dice moment</b> (MOTION.md increment MO1).
    /// While the roller still owns a new roll (<c>DiceHeld</c>), the slots
    /// read "rolling"; once it lets go, the faces pop in. The roller flies to
    /// <see cref="DiceFaces"/>.
    ///
    /// Its left and right edges follow whatever the side panels reserve, and
    /// the composition root sets them through <see cref="SetInsets"/>. The
    /// background blocks board clicks.
    /// </remarks>
    public sealed class ActionTray : MonoBehaviour
    {
        public const float Height = 196f;

        private const float AbilityNameSize = 15f;
        private const float AbilityMetaSize = 13f;

        /// <summary>Canvas units the tray claims from the bottom edge.</summary>
        public static float ReservedHeight => Height;

        public bool Visible { get; set; } = true;

        /// <summary>
        /// The row the dice faces sit in, as last built. The dice roller flies
        /// here. Null (or destroyed) between rebuilds.
        /// </summary>
        public RectTransform DiceFaces { get; private set; }

        private IControlPanelHost _host;
        private RectTransform _tray;
        private RectTransform _content;
        private bool _dirty;

        /// <summary>The faces last shown, so a new roll pops in once rather than on every rebuild.</summary>
        private string _shownDice = "";

        public void Bind(RectTransform canvasRect, IControlPanelHost host)
        {
            _host = host;
            if (_tray == null) Build(canvasRect);
            _dirty = true;
        }

        public void MarkDirty() => _dirty = true;

        /// <summary>Canvas units kept clear on the left and right.</summary>
        public void SetInsets(float left, float right)
        {
            if (_tray == null) return;

            _tray.offsetMin = new Vector2(left, 0f);
            _tray.offsetMax = new Vector2(-right, Height);
        }

        private void Build(RectTransform canvasRect)
        {
            _tray = UiKit.Rect("action_tray", canvasRect);
            _tray.anchorMin = new Vector2(0f, 0f);
            _tray.anchorMax = new Vector2(1f, 0f);
            _tray.pivot = new Vector2(0.5f, 0f);
            _tray.offsetMin = Vector2.zero;
            _tray.offsetMax = new Vector2(0f, Height);
            UiKit.Dock(_tray, true, RectTransform.Edge.Top);

            _content = UiKit.Rect("content", _tray);
            UiKit.Stretch(_content);
            var row = UiKit.Row(_content, 14f, 12);
            row.padding.top = 16; // clear of the double rule
            row.childForceExpandHeight = true;
            row.childAlignment = TextAnchor.UpperLeft;
        }

        private void LateUpdate()
        {
            if (_tray == null) return;

            if (_tray.gameObject.activeSelf != Visible)
            {
                _tray.gameObject.SetActive(Visible);
                if (Visible) _dirty = true;
            }

            if (!_dirty || !Visible) return;

            _dirty = false;
            Rebuild();
        }

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

            DiceSection();
            Divider();
            OperatorCard();
            Divider();
            AbilitySection();
            CastSection();
        }

        private void Divider() => UiKit.Divider(_content, vertical: true);

        // ── Dice ─────────────────────────────────────────────────────────

        private void DiceSection()
        {
            var engine = _host.Match.Engine;

            var box = UiKit.Rect("dice", _content);
            UiKit.Column(box, 10f);
            UiKit.Fixed(box, 250f);

            UiKit.Heading(box, "Dice");

            var faces = UiKit.Rect("faces", box);
            UiKit.Row(faces, 10f);
            UiKit.Size(faces, height: 64f);
            DiceFaces = faces;

            var dice = engine.UnspentDice;
            bool held = _host.DiceHeld;

            if (held)
            {
                // The roller has the new faces; the slots wait for them.
                Die(faces, "·", false);
                Die(faces, "·", false);
                _shownDice = "";
            }
            else if (dice.Count == 0)
            {
                Die(faces, "–", false);
                Die(faces, "–", false);
                _shownDice = "";
            }
            else
            {
                string key = $"{engine.CurrentPlayer.Color}|{engine.CurrentPlayer.TurnIndex}|{string.Join(",", dice)}";
                bool arriving = key != _shownDice && dice.Count >= 2;
                _shownDice = key;

                for (int i = 0; i < dice.Count; i++)
                {
                    var die = Die(faces, dice[i].ToString(), true);
                    if (arriving) UiPopIn.On(die, i * 0.06f);
                }
            }

            bool matchOn = !engine.MatchOver;
            bool canRoll = matchOn &&
                           (engine.Phase == TurnPhase.AwaitingRoll || (engine.CanRollAgain && dice.Count == 0));
            bool canEnd = matchOn && engine.Phase == TurnPhase.Action && !engine.MustSpendRoll;

            // Roll and End turn live on the turn button at the board's corner
            // (TurnButton); the tray only shows the dice and what they need.
            string hintText = held ? "Rolling…" : DiceHint(engine, dice.Count, canRoll, canEnd);
            var hint = UiKit.Label(box, hintText, UiTheme.FontSmall,
                canEnd && !canRoll ? UiTheme.Cyan : UiTheme.TextDim, wrap: true);
            UiKit.Size(hint, height: 40f);
        }

        /// <summary>One line on what the dice need, from the engine's answers only.</summary>
        private static string DiceHint(Core.GameEngine engine, int unspent, bool canRoll, bool canEnd)
        {
            if (engine.MatchOver) return "Match over.";
            if (engine.Phase == TurnPhase.AwaitingRoll) return "Roll to start your turn.";
            if (canRoll) return "Doubles: roll again.";
            if (unspent > 0 && engine.MustSpendRoll) return "Move a piece to spend these.";
            if (canEnd) return unspent > 0 ? "No legal move. End your turn." : "All spent. Cast, or end your turn.";
            return "";
        }

        /// <summary>An ivory die with a brass edge; a spent slot is a dim inset.</summary>
        private static RectTransform Die(Transform parent, string face, bool live)
        {
            var die = UiKit.Rect("die", parent);
            UiKit.Sliced(die, DecoSprites.ButtonFill, live ? UiTheme.DieFace : UiTheme.PanelInset);
            UiKit.Overlay(die, DecoSprites.ButtonEdge, live ? UiTheme.Gold : UiTheme.WithAlpha(UiTheme.Line, 0.4f));
            UiKit.Size(die, 64f, 64f);
            UiKit.Caption(die, face, 36f, live ? UiTheme.DieInk : UiTheme.TextOff,
                TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            return die;
        }

        // ── Operator ─────────────────────────────────────────────────────

        private void OperatorCard()
        {
            var engine = _host.Match.Engine;
            var op = _host.SelectedOperator;

            var card = UiKit.Rect("operator", _content);
            UiKit.Column(card, 6f);
            UiKit.Fixed(card, 300f);

            UiKit.Heading(card, "Operator");

            if (op == null)
            {
                UiKit.Label(card, "None selected", UiTheme.FontLarge, UiTheme.TextDim);
                UiKit.Label(card, "Click one of your pieces, or its row on the left.", UiTheme.FontSmall, UiTheme.TextDim, wrap: true);
                return;
            }

            var seatColour = BoardLayout.ColourOf(op.Owner);

            var top = UiKit.Rect("top", card);
            UiKit.Row(top, 10f);
            UiKit.Size(top, height: 56f);

            UiKit.Icon(top, PieceShape.For(op), seatColour, 52f);

            var name = UiKit.Rect("name", top);
            UiKit.Column(name, 2f);
            UiKit.Size(name, flexibleWidth: 1f);
            UiKit.Label(name, op.Name, UiTheme.FontLarge, bold: true);

            string where = engine.IsHome(op) ? "home"
                : engine.CanDeploy(op) ? "ready to deploy — click it"
                : op.IsInYard ? "waiting in the yard"
                : "on the board";
            UiKit.Label(name, where, UiTheme.FontSmall, UiTheme.TextDim);

            var health = UiKit.Rect("health", card);
            UiKit.Row(health, 8f);
            UiKit.Size(health, height: 20f);
            float fraction = (float)op.Health / Mathf.Max(1, op.MaxHealth);
            UiKit.Bar(health, fraction, Color.Lerp(UiTheme.Danger, seatColour, fraction), 200f, 10f);
            UiKit.Label(health, $"<b>{op.Health}</b>/{op.MaxHealth}", UiTheme.FontBody);

            var tags = UiKit.Rect("statuses", card);
            UiKit.Row(tags, 4f);
            UiKit.Size(tags, height: 20f);

            if (!op.IsInYard)
            {
                var statuses = engine.ActiveStatusesOn(op);
                if (statuses.Count == 0) UiKit.Label(tags, "no statuses", UiTheme.FontSmall, UiTheme.TextOff);

                foreach (var kind in statuses)
                    UiKit.Tag(tags, StatusPalette.Label(kind), StatusPalette.For(kind), 12f);
            }
        }

        // ── Abilities ────────────────────────────────────────────────────

        private void AbilitySection()
        {
            var engine = _host.Match.Engine;
            var op = _host.SelectedOperator;

            var box = UiKit.Rect("abilities", _content);
            UiKit.Column(box, 6f);
            UiKit.Size(box, flexibleWidth: 1f);

            UiKit.Heading(box, "Abilities");

            if (op == null || !_host.Match.AbilitiesByOperator.TryGetValue(op.Id, out var abilities))
            {
                UiKit.Label(box, "Select an operator to see its abilities.", UiTheme.FontBody, UiTheme.TextDim);
                return;
            }

            var cards = UiKit.Rect("cards", box);
            var row = UiKit.Row(cards, 8f);
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            UiKit.Size(cards, height: 92f);

            for (int i = 0; i < abilities.Count; i++)
                AbilityCard(cards, op, abilities[i], i, engine);

            var chosen = _host.SelectedAbility;
            string description = chosen != null
                ? chosen.Description
                : "Pick an ability (1–3) to see its reach on the board. Nothing is spent until you cast.";

            UiKit.Label(box, description, UiTheme.FontSmall, chosen != null ? UiTheme.Text : UiTheme.TextDim, wrap: true);
        }

        private void AbilityCard(Transform parent, OperatorState op, AbilityDefinition ability, int index, Core.GameEngine engine)
        {
            bool chosen = _host.SelectedAbility != null && _host.SelectedAbility.Id == ability.Id;
            var availability = engine.CheckAbility(op, ability);
            bool usable = availability == AbilityAvailability.Ready;

            string reach = ability.HasUnlimitedRange ? "any range"
                : ability.Range == 0 ? "self"
                : $"range {ability.Range}";

            string state;
            switch (availability)
            {
                case AbilityAvailability.Ready:
                    state = $"<color=#{UiTheme.Hex(UiTheme.Cyan)}>READY</color>";
                    break;
                case AbilityAvailability.OnCooldown:
                    int turns = engine.TurnsUntilReady(op, ability);
                    state = turns == 1 ? "ready next turn" : $"ready in {turns} turns";
                    break;
                case AbilityAvailability.InsufficientEnergy:
                    state = $"needs {ability.EnergyCost}e, have {engine.CurrentPlayer.Energy}";
                    break;
                default:
                    state = ControlPanel.Explain(availability);
                    break;
            }

            var button = UiKit.Button(parent, "", () => _host.ToggleAbility(ability), MarkDirty,
                interactable: usable || chosen, selected: chosen);
            UiKit.Size(button, flexibleWidth: 1f);

            var card = (RectTransform)button.transform;
            var column = UiKit.Column(card, 3f, 8);
            column.padding.top = 10;
            column.childAlignment = TextAnchor.UpperLeft;

            var textColour = usable || chosen ? UiTheme.Text : UiTheme.TextOff;

            // Smaller than body text (feedback 2026-09-15): three names side by
            // side read as a row of buttons, not a row of headlines.
            UiKit.Label(card,
                $"<color=#{UiTheme.Hex(UiTheme.GoldBright)}>{index + 1}</color>  <b>{ability.Name}</b>",
                AbilityNameSize, textColour);
            UiKit.Label(card,
                $"<color=#{UiTheme.Hex(UiTheme.Cyan)}>{ability.EnergyCost}e</color> · {reach} · cd {ability.CooldownTurns}",
                AbilityMetaSize, textColour);
            UiKit.Label(card, state, AbilityMetaSize, usable ? UiTheme.Text : UiTheme.TextDim);
        }

        // ── Cast ─────────────────────────────────────────────────────────

        private void CastSection()
        {
            var ability = _host.SelectedAbility;

            var box = UiKit.Rect("cast", _content);
            UiKit.Column(box, 8f);
            UiKit.Fixed(box, 210f);

            UiKit.Heading(box, "Aim");

            string aim;

            if (ability == null) aim = "No ability selected.";
            else if (ability.RequiresTarget)
            {
                var target = _host.SelectedTarget;
                aim = target != null
                    ? $"Target: <b>{target.Owner} {target.Name}</b> {target.Health}/{target.MaxHealth}"
                    : _host.CastTargets().Count == 0
                        ? "Nothing in reach."
                        : "Click an amber-ringed piece.";
            }
            else if (ability.RequiresCell)
            {
                aim = _host.SelectedCell != null
                    ? $"Cell <b>{_host.SelectedCell.Value}</b> — click another to change."
                    : "Click a highlighted cell.";
            }
            else aim = "No aim needed — it fires from the caster.";

            var aimLabel = UiKit.Label(box, aim, UiTheme.FontSmall, wrap: true);
            UiKit.Size(aimLabel, height: 58f);

            bool ready = _host.CastReady;

            var cast = UiKit.Button(box,
                ability == null ? "Cast" : $"<b>Cast</b>  <size=70%>Enter</size>",
                _host.Cast, MarkDirty, interactable: ready,
                tint: ready ? UiTheme.CyanDeep : (Color?)null,
                edge: ready ? UiTheme.Cyan : (Color?)null);
            UiKit.Size(cast, height: 52f);
        }
    }
}
