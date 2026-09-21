// Assets/_Project/Scripts/Unity/View/ActionTray.cs
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NonaRoyale.Core.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The bottom bar: the dice, the selected operator, its abilities, and
    /// Cast (GUI increment F; reworked by HUD_PASS.md, H1).
    /// </summary>
    /// <remarks>
    /// <b>One bar that carries the turn</b> (H1). It was four labelled
    /// sections that each owned their width whether or not they had anything
    /// to say, so three of them read "nothing selected" for most of a turn.
    /// Now the headings are gone and an empty slot draws nothing at all.
    ///
    /// <b>Stable geometry, contextual content.</b> A slot that has nothing to
    /// say still keeps its width. Letting the bar reflow as the player selects
    /// would move every control out from under the pointer mid-turn, and
    /// letting it change height would resize the board through
    /// <c>FrameCamera</c> while they were aiming. So the bar fills in; it does
    /// not rearrange.
    ///
    /// <b>Left to right is the order of a turn:</b> dice, pick an operator,
    /// pick an ability, cast. Roll and End turn are on the turn button at the
    /// board's corner (<see cref="TurnButton"/>). The board does the same things by clicking
    /// (PRESENTATION §4.1), and everything here calls the same intents.
    ///
    /// <b>Upright, that order runs top to bottom</b> (MOBILE.md, M3). Four
    /// sections side by side need about a thousand units of width and a phone
    /// has under five hundred, so the bar becomes a block: the aim line, then
    /// dice and operator, then the three abilities across, then Cast beside the
    /// turn button along the bottom edge. The two live controls end up in the
    /// thumb's reach, which is where a phone wants them; the turn button keeps
    /// the screen's bottom-right corner and the bar leaves it a slot to land
    /// in, so the two never overlap.
    ///
    /// <b>Readiness shows when, not only whether.</b> A cooling-down ability
    /// shows "ready in N" from <c>GameEngine.TurnsUntilReady</c>, and a
    /// too-expensive one shows its cost against the pool. Both are engine
    /// answers (PRESENTATION §1).
    ///
    /// <b>Passives and auras are chips on the operator card</b> (2026-09-21).
    /// They are part of the kit but never pressed, so they are not ability
    /// cards: a card there would read as castable and push Fortuna, Sanity and
    /// Lethe to four across a phone. A chip opens the trait's rules over the
    /// match. The row fits inside the heights above, so the bar did not grow.
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
        /// <summary>
        /// What the bar claims from the bottom edge when it is wide (H1; it was
        /// 196). Sized to the fullest state - an operator, three ability cards,
        /// and a chosen ability's description over its aim and Cast - because
        /// the band cannot change while a turn is being played.
        /// </summary>
        /// <remarks>
        /// 122 of usable height inside the row's padding. The three slots that
        /// can fill it come to 96, 92 and 122, so the aim slot is what sets
        /// this number.
        /// </remarks>
        public const float Height = 150f;

        /// <summary>
        /// What the block claims upright: the aim line, the dice-and-operator
        /// row, the three ability cards and the Cast row, plus the padding
        /// between them.
        /// </summary>
        public const float UprightHeight = 292f;

        private const float AbilityNameSize = 15f;
        private const float AbilityMetaSize = 13f;

        /// <summary>The slot widths. Fixed, so nothing moves as the bar fills in (H1).</summary>
        private const float DiceWidth = 132f;
        private const float OperatorWidth = 300f;
        private const float CastWidth = 210f;

        private const float DieSize = 52f;

        /// <summary>Upright row heights.</summary>
        private const float UprightAimHeight = 18f;
        private const float UprightTopHeight = 78f;
        private const float UprightCardsHeight = 92f;
        private const float UprightCastHeight = 60f;

        /// <summary>The padding the block keeps, and the slot it leaves the turn button (M3).</summary>
        private const float UprightPadding = 12f;

        /// <summary>Canvas units the tray claims from the bottom edge.</summary>
        public static float ReservedHeight => ScreenLayout.Pick(Height, UprightHeight);

        public bool Visible { get; set; } = true;

        /// <summary>
        /// The row the dice faces sit in, as last built. The dice roller flies
        /// here. Null (or destroyed) between rebuilds.
        /// </summary>
        public RectTransform DiceFaces { get; private set; }

        private IControlPanelHost _host;
        private RectTransform _canvas;
        private RectTransform _tray;
        private RectTransform _content;
        private bool _dirty;
        private bool _builtPortrait;
        private float _insetLeft;
        private float _insetRight;

        /// <summary>The faces last shown, so a new roll pops in once rather than on every rebuild.</summary>
        private string _shownDice = "";

        // The operator card's last bar fill, so a hit or a heal glides from
        // the old value instead of snapping (UI_MOTION.md U2). One card shows
        // one operator at a time, so a single remembered fraction is enough.
        private OperatorState _barOperator;
        private float _barFraction = -1f;

        public void Bind(RectTransform canvasRect, IControlPanelHost host)
        {
            _host = host;
            _canvas = canvasRect;
            _barOperator = null;
            _barFraction = -1f;
            if (_tray == null) Build();
            _dirty = true;
        }

        public void MarkDirty() => _dirty = true;

        /// <summary>Canvas units kept clear on the left and right.</summary>
        public void SetInsets(float left, float right)
        {
            _insetLeft = left;
            _insetRight = right;

            if (_tray == null) return;

            _tray.offsetMin = new Vector2(left, 0f);
            _tray.offsetMax = new Vector2(-right, ReservedHeight);
        }

        private void Build()
        {
            _builtPortrait = ScreenLayout.IsPortrait;

            _tray = UiKit.Rect("action_tray", _canvas);
            _tray.anchorMin = new Vector2(0f, 0f);
            _tray.anchorMax = new Vector2(1f, 0f);
            _tray.pivot = new Vector2(0.5f, 0f);
            _tray.offsetMin = new Vector2(_insetLeft, 0f);
            _tray.offsetMax = new Vector2(-_insetRight, ReservedHeight);
            UiKit.Dock(_tray, true, RectTransform.Edge.Top);

            _content = UiKit.Rect("content", _tray);
            UiKit.Stretch(_content);

            if (_builtPortrait)
            {
                var column = UiKit.Column(_content, 6f, (int)UprightPadding);
                column.padding.top = 14; // clear of the rule
                column.childForceExpandHeight = false;
                column.childAlignment = TextAnchor.UpperLeft;
            }
            else
            {
                var row = UiKit.Row(_content, 14f, 12);
                row.padding.top = 16; // clear of the rule
                row.childForceExpandHeight = true;
                row.childAlignment = TextAnchor.UpperLeft;
            }
        }

        private void LateUpdate()
        {
            if (_tray == null) return;

            if (_builtPortrait != ScreenLayout.IsPortrait)
            {
                var old = _tray.gameObject;
                _tray = null;
                DiceFaces = null;
                old.SetActive(false);
                Destroy(old);

                Build();
                _dirty = true;
            }

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

            if (_builtPortrait) RebuildUpright();
            else RebuildWide();
        }

        private void RebuildWide()
        {
            DiceSection(_content, DiceWidth);
            Divider(_content);
            OperatorCard(_content, OperatorWidth, compact: false);
            Divider(_content);
            AbilitySection(_content);
            CastSection(_content, CastWidth, compact: false);
        }

        /// <summary>
        /// The same four sections, stacked (M3). The aim line moves to the top
        /// of the block, where the eye is already reading the top bar's
        /// instruction, and Cast keeps the bottom row with the turn button.
        /// </summary>
        private void RebuildUpright()
        {
            AimLine(_content);

            var top = UiKit.Rect("top", _content);
            UiKit.Row(top, 10f).childForceExpandHeight = true;
            UiKit.Size(top, height: UprightTopHeight, flexibleHeight: 0f);

            DiceSection(top, 124f);
            Divider(top);
            OperatorCard(top, -1f, compact: true);

            var cards = UiKit.Rect("abilities", _content);
            UiKit.Size(cards, height: UprightCardsHeight, flexibleHeight: 0f);
            AbilityRow(cards);

            var bottom = UiKit.Rect("bottom", _content);
            UiKit.Row(bottom, 8f).childForceExpandHeight = true;
            UiKit.Size(bottom, height: UprightCastHeight, flexibleHeight: 0f);

            CastSection(bottom, -1f, compact: true);

            // The turn button floats in the screen's bottom-right corner; this
            // holds its place open so Cast never grows under it.
            var slot = UiKit.Rect("turn_slot", bottom);
            UiKit.Fixed(slot, TurnButton.UprightWidth);
        }

        private void Divider(Transform parent) => UiKit.Divider(parent, vertical: true);

        /// <summary>
        /// Holds a slot's width open while it has nothing to show, so the bar
        /// fills in rather than reflowing under the pointer.
        /// </summary>
        private void Empty(Transform parent, float width)
        {
            var box = UiKit.Rect("empty", parent);
            if (width >= 0f) UiKit.Fixed(box, width);
            else UiKit.Size(box, flexibleWidth: 1f);
        }

        // ── The aim line (upright only) ──────────────────────────────────

        /// <summary>
        /// What the armed ability does and where it is going, on one line
        /// above the block (M3). Blank, but the same height, with nothing
        /// armed.
        /// </summary>
        private void AimLine(Transform parent)
        {
            var ability = _host.SelectedAbility;

            var line = UiKit.Label(parent,
                ability == null ? "" : $"<b>{ability.Name}</b> — {AimText(ability)}",
                13f, UiTheme.TextDim, TextAlignmentOptions.Midline);
            UiKit.Size(line, height: UprightAimHeight, flexibleHeight: 0f);
        }

        // ── Dice ─────────────────────────────────────────────────────────

        private void DiceSection(Transform parent, float width)
        {
            var engine = _host.Match.Engine;

            var box = UiKit.Rect("dice", parent);
            UiKit.Column(box, 8f);
            UiKit.Fixed(box, width);

            var faces = UiKit.Rect("faces", box);
            UiKit.Row(faces, 8f);
            UiKit.Size(faces, height: DieSize);
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
            UiKit.Size(hint, height: ScreenLayout.Pick(34f, 16f));
        }

        /// <summary>One line on what the dice need, from the engine's answers only.</summary>
        private static string DiceHint(Core.GameEngine engine, int unspent, bool canRoll, bool canEnd)
        {
            if (engine.MatchOver) return "Match over.";
            if (engine.Phase == TurnPhase.AwaitingRoll) return "Roll to start your turn.";
            if (canRoll) return "Doubles — roll again.";

            // The top bar already says to move a piece; saying it twice is what
            // H1 exists to stop. Only the states the top bar does not cover
            // get a line here.
            if (unspent > 0 && engine.MustSpendRoll) return "";
            if (canEnd) return unspent > 0 ? "No legal move." : "All spent.";
            return "";
        }

        /// <summary>An ivory die with a brass edge; a spent slot is a dim inset.</summary>
        private static RectTransform Die(Transform parent, string face, bool live)
        {
            var die = UiKit.Rect("die", parent);
            UiKit.Sliced(die, DecoSprites.ButtonFill, live ? UiTheme.DieFace : UiTheme.PanelInset);
            UiKit.Overlay(die, DecoSprites.ButtonEdge, live ? UiTheme.Gold : UiTheme.WithAlpha(UiTheme.Line, 0.4f));
            UiKit.Size(die, DieSize, DieSize);
            UiKit.Caption(die, face, 30f, live ? UiTheme.DieInk : UiTheme.TextOff,
                TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            return die;
        }

        // ── Operator ─────────────────────────────────────────────────────

        /// <summary>
        /// The selected operator. <paramref name="compact"/> drops the health
        /// bar's own row and the status tags onto one line, which is what fits
        /// beside the dice upright.
        /// </summary>
        private void OperatorCard(Transform parent, float width, bool compact)
        {
            var engine = _host.Match.Engine;
            var op = _host.SelectedOperator;

            // Nothing selected draws nothing. The top bar is already telling
            // them to pick a piece (H1).
            if (op == null)
            {
                Empty(parent, width);
                return;
            }

            var card = UiKit.Rect("operator", parent);
            UiKit.Column(card, compact ? 3f : 6f);
            if (width >= 0f) UiKit.Fixed(card, width);
            else UiKit.Size(card, flexibleWidth: 1f);

            var seatColour = BoardLayout.ColourOf(op.Owner);

            var top = UiKit.Rect("top", card);
            UiKit.Row(top, 10f);
            UiKit.Size(top, height: compact ? 34f : 48f);

            UiKit.Icon(top, PieceShape.For(op), seatColour, compact ? 30f : 44f);

            var name = UiKit.Rect("name", top);
            UiKit.Column(name, 2f);
            UiKit.Size(name, flexibleWidth: 1f);
            UiKit.Label(name, op.Name, compact ? UiTheme.FontBody : UiTheme.FontLarge, bold: true);

            string where = engine.IsHome(op) ? "home"
                : engine.CanDeploy(op)
                    ? (ScreenLayout.Touch ? "ready to deploy — tap it" : "ready to deploy — click it")
                : op.IsInYard ? "waiting in the yard"
                : "on the board";
            UiKit.Label(name, where, compact ? 12f : UiTheme.FontSmall, UiTheme.TextDim);

            var health = UiKit.Rect("health", card);
            UiKit.Row(health, 8f);
            UiKit.Size(health, height: 18f);
            float fraction = (float)op.Health / Mathf.Max(1, op.MaxHealth);

            // Build the bar at the last shown fraction and glide to the new
            // one; a first sight starts at the truth (UI_MOTION.md U2).
            bool seen = ReferenceEquals(_barOperator, op) && _barFraction >= 0f;
            var fill = UiKit.Bar(health, seen ? _barFraction : fraction,
                Color.Lerp(UiTheme.Danger, seatColour, fraction), compact ? 120f : 200f, 10f);
            if (seen && !Mathf.Approximately(_barFraction, fraction))
                UiKit.TweenBar(fill, _barFraction, fraction, 0.35f, seatColour);
            _barOperator = op;
            _barFraction = fraction;

            UiKit.Label(health, $"<b>{op.Health}</b>/{op.MaxHealth}", compact ? UiTheme.FontSmall : UiTheme.FontBody);

            // No "no statuses" line: an empty row says the same thing without
            // spending a line on it (H1).
            if (!op.IsInYard)
            {
                var tags = compact ? health : UiKit.Rect("statuses", card);

                if (!compact)
                {
                    UiKit.Row(tags, 4f);
                    UiKit.Size(tags, height: 18f);
                }

                foreach (var kind in engine.ActiveStatusesOn(op))
                    UiKit.Tag(tags, StatusPalette.Label(kind), StatusPalette.For(kind), 12f);
            }

            // Shown in the yard too: a kit is a kit wherever the piece stands.
            TraitChips(card, op);
        }

        /// <summary>
        /// One gold chip per passive and aura, in the dossier's order; a tap
        /// opens its rules and flavour over the match. Nothing for an operator
        /// that carries neither, and no row either.
        /// </summary>
        /// <remarks>
        /// 18 units tall, like the status tags, which is what the bar's height
        /// leaves in both orientations. On a touch screen the hit box reaches
        /// past the chip's edges, as the squad rail's pips do, so a finger can
        /// find it.
        /// </remarks>
        private void TraitChips(RectTransform card, OperatorState op)
        {
            var definition = DefinitionOf(op);
            if (definition == null) return;

            var traits = RulesText.Traits(definition);
            if (traits.Count == 0) return;

            var row = UiKit.Rect("traits", card);
            var layout = UiKit.Row(row, 4f);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            UiKit.Size(row, height: 18f);

            foreach (var trait in traits)
            {
                var shown = trait;
                var chip = UiKit.Button(row, trait.Name, () => GlossaryCard.ShowTrait(_canvas, op.Name, shown),
                    size: 11f, tint: UiTheme.GoldDeep, edge: UiTheme.Gold);
                chip.name = "trait_" + trait.Name;

                // The caption ignores layout, so the chip is sized to its words.
                var caption = chip.GetComponentInChildren<TMP_Text>();
                float width = caption != null ? caption.GetPreferredValues(trait.Name).x : 60f;
                UiKit.Fixed(chip, width + 20f);

                if (ScreenLayout.Touch && chip.targetGraphic != null)
                    chip.targetGraphic.raycastPadding = new Vector4(-2f, -8f, -2f, -8f);
            }
        }

        /// <summary>The roster entry a piece was dealt from. Null for a piece no roster operator matches.</summary>
        private static OperatorDefinition DefinitionOf(OperatorState op)
        {
            foreach (var definition in Roster.All)
                if (definition.Name == op.Name) return definition;

            return null;
        }

        // ── Abilities ────────────────────────────────────────────────────

        private void AbilitySection(Transform parent)
        {
            var box = UiKit.Rect("abilities", parent);
            UiKit.Column(box, 6f);
            UiKit.Size(box, flexibleWidth: 1f);

            // The widest slot, and the one most often empty. It holds its width
            // and draws nothing rather than explaining itself (H1): the cards
            // carry their own 1-3 keys, and the chosen ability's description
            // belongs beside Cast, where the player is looking by then.
            var cards = UiKit.Rect("cards", box);
            UiKit.Size(cards, height: 92f);
            AbilityRow(cards);
        }

        /// <summary>The three cards across a row, or nothing at all when there is no operator.</summary>
        private void AbilityRow(RectTransform cards)
        {
            var engine = _host.Match.Engine;
            var op = _host.SelectedOperator;

            var row = UiKit.Row(cards, 8f);
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;

            if (op == null || !_host.Match.AbilitiesByOperator.TryGetValue(op.Id, out var abilities)) return;

            for (int i = 0; i < abilities.Count; i++)
                AbilityCard(cards, op, abilities[i], i, engine);
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
            // side read as a row of buttons, not a row of headlines. The number
            // is a keyboard shortcut, so it goes when there is no keyboard.
            UiKit.Label(card,
                $"{ScreenLayout.KeyMarkup($"<color=#{UiTheme.Hex(UiTheme.GoldBright)}>{index + 1}</color>  ")}<b>{ability.Name}</b>",
                AbilityNameSize, textColour);
            UiKit.Label(card,
                $"<color=#{UiTheme.Hex(UiTheme.Cyan)}>{ability.EnergyCost}e</color> · {reach} · cd {ability.CooldownTurns}",
                AbilityMetaSize, textColour);
            UiKit.Label(card, state, AbilityMetaSize, usable ? UiTheme.Text : UiTheme.TextDim);
        }

        // ── Cast ─────────────────────────────────────────────────────────

        /// <summary>
        /// Where the armed ability is going, in one line. Shared by the wide
        /// bar's Cast slot and the upright block's aim line.
        /// </summary>
        private string AimText(AbilityDefinition ability)
        {
            bool touch = ScreenLayout.Touch;

            if (ability.RequiresTarget)
            {
                var target = _host.SelectedTarget;
                if (target != null)
                    return $"Target: <b>{target.Owner} {target.Name}</b> {target.Health}/{target.MaxHealth}";

                return _host.CastTargets().Count == 0
                    ? "Nothing in reach."
                    : touch ? "Tap an amber-ringed piece." : "Click an amber-ringed piece.";
            }

            if (ability.RequiresCell)
            {
                if (_host.SelectedCell != null)
                    return touch
                        ? $"Cell <b>{_host.SelectedCell.Value}</b> — tap another to change."
                        : $"Cell <b>{_host.SelectedCell.Value}</b> — click another to change.";

                return touch ? "Tap a highlighted cell." : "Click a highlighted cell.";
            }

            return "No aim needed — it fires from the caster.";
        }

        private void CastSection(Transform parent, float width, bool compact)
        {
            var ability = _host.SelectedAbility;

            // Nothing armed, nothing to aim, and a permanently dead Cast button
            // is the kind of furniture H1 is clearing out.
            if (ability == null)
            {
                Empty(parent, width);
                return;
            }

            bool ready = _host.CastReady;

            if (compact)
            {
                // The description and the aim are already on the block's aim
                // line, so the row is the button alone.
                var only = UiKit.Button(parent, $"<b>Cast</b>{ScreenLayout.KeyMarkup("  <size=70%>Enter</size>")}",
                    _host.Cast, MarkDirty, interactable: ready,
                    tint: ready ? UiTheme.CyanDeep : (Color?)null,
                    edge: ready ? UiTheme.Cyan : (Color?)null,
                    size: UiTheme.FontLarge);
                UiKit.Size(only, flexibleWidth: 1f);
                if (ready) UiKit.Pulse(only, UiTheme.Cyan);
                return;
            }

            var box = UiKit.Rect("cast", parent);
            UiKit.Column(box, 8f);
            UiKit.Fixed(box, width);

            // What it does, then where it goes. The ability card carries the
            // name, cost, reach and readiness; the prose was homeless once the
            // abilities slot stopped explaining itself (H1).
            var what = UiKit.Label(box, ability.Description, UiTheme.FontSmall, UiTheme.TextDim, wrap: true);
            UiKit.Size(what, height: 40f);

            var aimLabel = UiKit.Label(box, AimText(ability), UiTheme.FontSmall, wrap: true);
            UiKit.Size(aimLabel, height: 18f);

            var cast = UiKit.Button(box, $"<b>Cast</b>{ScreenLayout.KeyMarkup("  <size=70%>Enter</size>")}",
                _host.Cast, MarkDirty, interactable: ready,
                tint: ready ? UiTheme.CyanDeep : (Color?)null,
                edge: ready ? UiTheme.Cyan : (Color?)null);
            UiKit.Size(cast, height: 48f);
        }
    }
}
