// Assets/_Project/Scripts/Unity/View/ActionTray.cs
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
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
    /// <b>The whole rules line is one look away</b> (G7b-3). A card has room
    /// for two lines of it and ends longer ones in "…". Hovering a card opens
    /// a peek above the bar with the ability's name and its whole line. On a
    /// touch screen, or upright where the cards carry no rules line, the peek
    /// shows the armed ability's, since a tap arms rather than hovers.
    ///
    /// Its left and right edges follow whatever the side panels reserve, and
    /// the composition root sets them through <see cref="SetInsets"/>. The
    /// background blocks board clicks.
    /// </remarks>
    public sealed class ActionTray : MonoBehaviour
    {
        /// <summary>
        /// What the bar claims from the bottom edge when it is wide (H1; it was
        /// 196). Sized to the fullest state - an operator with statuses and
        /// traits, three ability cards, and a chosen ability's aim and Cast -
        /// because the band cannot change while a turn is being played.
        /// </summary>
        /// <remarks>
        /// 122 of usable height inside the row's padding. The slots that can
        /// fill it come to 120 (operator), 92 (cards) and 96 (aim and Cast).
        /// The aim slot set this number until G6b moved the ability's rules
        /// onto its card.
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

        /// <summary>Two lines of the rules line on a wide card; the card's 92 units hold exactly that (G6b).</summary>
        private const float RulesLineHeight = 32f;

        /// <summary>A castable card's edge: cyan, but quieter than a chosen card's double edge.</summary>
        private const float ReadyEdgeAlpha = 0.55f;

        /// <summary>How strongly a card that can't be cast shows its text.</summary>
        private const float DimmedAlpha = 0.45f;

        /// <summary>Ability slots in the row, filled or not. No operator has more than three abilities.</summary>
        private const int CardSlots = 3;

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
        // The peek above the bar (G7b-3).
        private RectTransform _peek;
        private TMP_Text _peekTitle;
        private TMP_Text _peekBody;
        private RectTransform _peekOwner;
        private readonly Vector3[] _corners = new Vector3[4];
        private const float PeekWidth = 380f;

        /// <summary>How long the armed ability's peek stays up on touch, before it gets out of the aim's way.</summary>
        private const float ArmedPeekSeconds = 4f;
        private float _peekHideAt = -1f;
        private int _peekArmedId = -1;

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

            if (_peekHideAt > 0f && Time.unscaledTime >= _peekHideAt)
            {
                _peekHideAt = -1f;
                HidePeek(null);
            }

            if (_tray.gameObject.activeSelf != Visible)
            {
                if (!Visible) HidePeek(null);
                _tray.gameObject.SetActive(Visible);
                if (Visible) _dirty = true;
            }

            if (!_dirty || !Visible) return;

            _dirty = false;
            Rebuild();
        }

        private void Rebuild()
        {
            // The card a hover peek hangs from is about to go. The armed
            // ability's peek has no card and runs on its own clock.
            if (_peekOwner != null) HidePeek(null);

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

            // No hover to open it on a touch screen, and no rules line on an
            // upright card: the armed ability's line is shown instead, once per
            // arming and for a few seconds, so it never sits over the cells
            // the player is about to tap.
            var armed = _host.SelectedAbility;
            if (armed == null) _peekArmedId = -1;
            else if ((ScreenLayout.Touch || _builtPortrait) && armed.Id != _peekArmedId)
            {
                _peekArmedId = armed.Id;
                ShowPeek(armed, null);
                _peekHideAt = Time.unscaledTime + ArmedPeekSeconds;
            }
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
                           (engine.Phase == TurnPhase.AwaitingRoll || (engine.CanRollAgain && !engine.DiceOwed));
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

            // A slot that holds the width, and the card laid out inside it
            // (G6b, flag 7). The bar asks the slot for its size and the slot
            // answers from its LayoutElement alone, so nothing the card holds
            // can change what the bar gives it. As a direct child with its own
            // layout group, the card was once seen laid out at almost no width
            // in a build, drawing its icon and bar under the ability cards.
            var slot = UiKit.Rect("operator", parent);
            if (width >= 0f) UiKit.Fixed(slot, width);
            else UiKit.Size(slot, flexibleWidth: 1f);

            var card = UiKit.Rect("card", slot);
            UiKit.Stretch(card);
            UiKit.Column(card, compact ? 3f : 6f);

            var seatColour = BoardLayout.ColourOf(op.Owner);

            var top = UiKit.Rect("top", card);
            UiKit.Row(top, 10f);
            UiKit.Size(top, height: compact ? 34f : 48f);

            UiKit.Icon(top, PieceShape.For(op), seatColour, compact ? 30f : 44f);

            var name = UiKit.Rect("name", top);
            UiKit.Column(name, 2f);
            UiKit.Size(name, flexibleWidth: 1f);
            UiKit.Label(name, op.Name, compact ? UiTheme.FontBody : UiTheme.FontLarge, bold: true);

            // The one place the tray says why none of the cards can be cast
            // when the reason is the operator's, not the ability's (G6b).
            string where = engine.IsHome(op) ? "home"
                : engine.CanDeploy(op)
                    ? (ScreenLayout.Touch ? "ready to deploy — tap it" : "ready to deploy — click it")
                : op.IsInYard ? "waiting in the yard"
                : _host.Match.Map.CellAt(op.Owner, op.Progress).Kind == CellKind.HomeColumn
                    ? "in the home column, out of the fight"
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

            // Overflow, not Ellipsis: body text is taller than the 18-unit row,
            // and an Ellipsis label whose line does not fit its height draws
            // nothing, which is why the number never showed (G6b). "8/8" is
            // too short to spill sideways.
            UiKit.Label(health, $"<b>{op.Health}</b>/{op.MaxHealth}", compact ? UiTheme.FontSmall : UiTheme.FontBody)
                .overflowMode = TextOverflowModes.Overflow;

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

            // Three slots whoever is selected, so a card keeps its place and
            // width from one operator to the next (H1's stable geometry). A
            // two-ability kit leaves the third slot empty instead of stretching
            // its cards across it.
            for (int i = abilities.Count; i < CardSlots; i++)
                EqualShare(UiKit.Rect("empty_card", cards));
        }

        /// <summary>
        /// An equal share of the row, whatever the child holds (G6b). A card's
        /// preferred width is its longest line unwrapped, so cards sized by
        /// content came out 150 and 600 wide side by side; with no preferred
        /// width every card is given the same share of the room.
        /// </summary>
        private static void EqualShare(Component child)
        {
            var element = UiKit.Size(child, flexibleWidth: 1f);
            element.minWidth = 0f;
            element.preferredWidth = 0f;
        }

        /// <summary>
        /// One ability: its key and name, its cost, reach and cooldown worded
        /// as the draft and the dossier word them, and — wide — its rules line
        /// (G6b, flag 6).
        /// </summary>
        /// <remarks>
        /// <b>Readiness is on the frame, not in a word.</b> A castable card
        /// carries a faint cyan edge (the live register, ART_DIRECTION §8), a
        /// chosen one the full cyan fill and double edge, and one that cannot
        /// be cast the dimmed frame with the reason after its meta. The READY
        /// word said what the frame already did.
        ///
        /// <b>The rules line lives on the card</b>, so a player reads what an
        /// ability does before choosing it, not after. It is the same
        /// <see cref="RulesText"/> line the draft and the dossier print, never
        /// the flavour paragraph. Upright, a card is a third of a phone wide
        /// and has no room for it; the aim line above the block carries the
        /// armed ability's instead (MOBILE.md M3).
        /// </remarks>
        private void AbilityCard(Transform parent, OperatorState op, AbilityDefinition ability, int index, Core.GameEngine engine)
        {
            bool chosen = _host.SelectedAbility != null && _host.SelectedAbility.Id == ability.Id;
            var availability = engine.CheckAbility(op, ability);
            bool usable = availability == AbilityAvailability.Ready;

            string reason;
            switch (availability)
            {
                case AbilityAvailability.Ready:
                    reason = null;
                    break;
                case AbilityAvailability.OnCooldown:
                    int turns = engine.TurnsUntilReady(op, ability);
                    reason = turns == 1 ? "ready next turn" : $"ready in {turns} turns";
                    break;
                case AbilityAvailability.InsufficientEnergy:
                    reason = $"needs {ability.EnergyCost}e, have {engine.CurrentPlayer.Energy}";
                    break;
                case AbilityAvailability.CasterOutOfPlay:
                case AbilityAvailability.CasterStunned:
                    // The operator's state, not the ability's: the operator
                    // card says it once ("waiting in the yard", the STUN tag)
                    // rather than every card repeating "out of play" (G6b).
                    reason = null;
                    break;
                default:
                    reason = ControlPanel.Explain(availability);
                    break;
            }

            var button = UiKit.Button(parent, "", () => _host.ToggleAbility(ability), MarkDirty,
                interactable: usable || chosen, selected: chosen,
                edge: usable ? UiTheme.WithAlpha(UiTheme.Cyan, ReadyEdgeAlpha) : (Color?)null);
            EqualShare(button);

            var card = (RectTransform)button.transform;
            var column = UiKit.Column(card, 3f, 8);
            column.padding.top = 10;
            column.childAlignment = TextAnchor.UpperLeft;

            // A card that can't be cast is dimmed as a whole, keyword colours
            // included, rather than greying the plain text and leaving the cyan
            // cost and the coloured keywords at full strength (G6b).
            bool live = usable || chosen;
            var textColour = UiTheme.Text;

            // Smaller than body text (feedback 2026-09-15): three names side by
            // side read as a row of buttons, not a row of headlines. The number
            // is a keyboard shortcut, so it goes when there is no keyboard.
            var title = UiKit.Label(card,
                $"{ScreenLayout.KeyMarkup($"<color=#{UiTheme.Hex(UiTheme.GoldBright)}>{index + 1}</color>  ")}<b>{ability.Name}</b>",
                AbilityNameSize, textColour);
            Dim(title, live);

            string meta = OperatorDossier.Meta(ability);
            if (reason != null) meta += $"  <color=#{UiTheme.Hex(UiTheme.TextDim)}>· {reason}</color>";
            Dim(UiKit.Label(card, meta, AbilityMetaSize, textColour), live);

            // Hover opens the whole line above the bar (G7b-3). Not on a touch
            // screen, where a tap enters and leaves at once and arms instead.
            if (!ScreenLayout.Touch)
                HoverRelay.On(button, () => ShowPeek(ability, card), () => HidePeek(card));

            if (_builtPortrait) return;

            var rules = UiKit.Label(card, RulesMarkup.For(RulesText.For(ability), linked: false),
                AbilityMetaSize, UiTheme.TextDim, wrap: true);
            rules.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.Size(rules, height: RulesLineHeight);
            Dim(rules, live);
        }

        /// <summary>Fades a label, rich-text colours and all, when its card can't be cast.</summary>
        private static void Dim(Component label, bool live)
        {
            if (live) return;
            label.gameObject.AddComponent<CanvasGroup>().alpha = DimmedAlpha;
        }

        // ── The peek (G7b-3) ─────────────────────────────────────────────

        /// <summary>
        /// Opens the peek with <paramref name="ability"/>'s name and whole rules
        /// line, above <paramref name="card"/>, or centred over the bar when
        /// there is no card to hang it from.
        /// </summary>
        private void ShowPeek(AbilityDefinition ability, RectTransform card)
        {
            if (ability == null || _canvas == null) return;
            if (_peek == null) BuildPeek();

            _peekTitle.text = $"<b>{ability.Name}</b>  <size=85%><color=#{UiTheme.Hex(UiTheme.TextDim)}>{OperatorDossier.Meta(ability)}</color></size>";
            _peekBody.text = RulesMarkup.For(RulesText.For(ability), linked: false);

            _peekOwner = card;
            _peekHideAt = -1f;
            _peek.gameObject.SetActive(true);
            _peek.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_peek);

            // Above the bar's top edge, over the card, kept on screen. World
            // units are pixels on this canvas, as the history card assumes.
            _tray.GetWorldCorners(_corners);
            float top = _corners[1].y;
            float x = (_corners[0].x + _corners[3].x) * 0.5f;

            if (card != null)
            {
                card.GetWorldCorners(_corners);
                x = (_corners[0].x + _corners[3].x) * 0.5f;
            }

            float halfWidth = _peek.rect.width * _canvas.lossyScale.x * 0.5f;
            x = Mathf.Clamp(x, halfWidth + 4f, Screen.width - halfWidth - 4f);
            _peek.position = new Vector3(x, top + 8f, 0f);
        }

        /// <summary>Closes the peek, unless it now belongs to another card (a late exit).</summary>
        private void HidePeek(RectTransform card)
        {
            if (_peek == null) return;
            if (card != null && !ReferenceEquals(card, _peekOwner)) return;

            _peekOwner = null;
            _peek.gameObject.SetActive(false);
        }

        private void BuildPeek()
        {
            _peek = UiKit.Rect("ability_peek", _canvas);
            _peek.anchorMin = _peek.anchorMax = new Vector2(0.5f, 0f);
            _peek.pivot = new Vector2(0.5f, 0f);
            _peek.sizeDelta = new Vector2(Mathf.Min(PeekWidth, ScreenLayout.Reference.x - 24f), 0f);
            UiKit.Panel(_peek, blocksPointer: false);

            var column = UiKit.Column(_peek, 4f, 16);
            column.padding.left = 20;
            column.padding.right = 20;
            _peek.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Wrapped labels: no single-line slack to pull them into each other.
            _peekTitle = UiKit.Label(_peek, "", UiTheme.FontBody, wrap: true);
            _peekBody = UiKit.Label(_peek, "", UiTheme.FontSmall, UiTheme.Text, wrap: true);

            // It must never take the pointer from the card under it.
            var group = _peek.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            _peek.gameObject.SetActive(false);
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
                // The name in its seat's colour, not "Blue Kian" (G7b).
                if (target != null)
                    return $"Target: <b>{RulesMarkup.For(new RulesLine().Named(target.Name, target.Owner), linked: false)}</b> {target.Health}/{target.MaxHealth}";

                return _host.CastTargets().Count == 0
                    ? "Nothing in reach."
                    : touch ? "Tap an amber-ringed piece." : "Click an amber-ringed piece.";
            }

            if (ability.RequiresCell)
            {
                // The board marks the chosen cell; its index ("Track[39]") means
                // nothing to a player (G7b).
                if (_host.SelectedCell != null)
                    return touch
                        ? "Cell chosen — tap another to change."
                        : "Cell chosen — click another to change.";

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

            // Where it goes. What it does is on its card now (G6b): the rules
            // line there replaced the flavour paragraph that sat here, which
            // ran to four lines in a two-line box.
            var aimLabel = UiKit.Label(box, AimText(ability), UiTheme.FontSmall, wrap: true);
            aimLabel.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.Size(aimLabel, height: 40f);

            var cast = UiKit.Button(box, $"<b>Cast</b>{ScreenLayout.KeyMarkup("  <size=70%>Enter</size>")}",
                _host.Cast, MarkDirty, interactable: ready,
                tint: ready ? UiTheme.CyanDeep : (Color?)null,
                edge: ready ? UiTheme.Cyan : (Color?)null);
            UiKit.Size(cast, height: 48f);
        }
    }
}
