// Assets/_Project/Scripts/Unity/Composition/MatchBootstrap.cs
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NonaRoyale.Unity.View;
using UnityEngine;

namespace NonaRoyale.Unity.Composition
{
    /// <summary>
    /// The Unity composition root. Builds a match, renders it, and turns clicks
    /// into commands.
    /// </summary>
    /// <remarks>
    /// <b>Drop this on one empty GameObject and press Play.</b> No prefabs, no
    /// Canvas, no imported art — the board, the pieces and the controls are all
    /// generated at runtime. The only thing this build exists to answer is
    /// whether the game is fun, and scene wiring does not help answer it.
    ///
    /// <b>It holds no rules.</b> Every action goes out as a command and comes
    /// back as events; the view never inspects a service or mutates state. If a
    /// move looks wrong on screen, the bug is in the core and there is an
    /// EditMode test missing for it.
    /// </remarks>
    public sealed class MatchBootstrap : MonoBehaviour
    {
        [Header("Match")]
        [Tooltip("Draft three distinct operators per seat from the whole roster. " +
                 "Off means the alpha three, which is what every measurement in ADR-0002 used.")]
        public bool randomSquads = false;

        [Tooltip("Compact 28x2 has a longer journey than Standard since ADR-0002 " +
                 "Amendment 6 (59 against 58). Kept switchable for comparison only.")]
        public bool useCompactBoard = false;

        [Range(2, 4)] public int players = 4;

        [Tooltip("Operators already on the board at the start. 2 is the adopted value.")]
        [Range(0, 3)] public int openingDeployments = 2;

        public int seed = 20260912;

        [Header("Presentation")]
        [Tooltip("World units per board cell.")]
        public float cellSpacing = 1f;

        [Tooltip("Show the controls panel. Tab toggles it while playing.")]
        public bool showPanel = true;

        private MatchFactory.Match _match;
        private BoardLayout _layout;
        private readonly List<OperatorPiece> _pieces = new List<OperatorPiece>();
        private readonly List<string> _log = new List<string>();

        private OperatorState _selectedCaster;
        private OperatorState _selectedTarget;
        private AbilityDefinition _selectedAbility;
        private HighlightLayer _highlights;
        private FeedbackLayer _feedback;
        private Vector2 _panelScroll;

        private void Start() => NewMatch();

        private void NewMatch()
        {
            foreach (var piece in _pieces)
                if (piece != null) Destroy(piece.gameObject);

            _pieces.Clear();
            _log.Clear();
            _selectedCaster = null;
            _selectedTarget = null;
            _selectedAbility = null;

            // Both profiles must be drawable crosses: BoardLayout rejects a
            // circuit outside the 8L+4 family rather than drawing a track with a
            // gap in it (ADR-0002 Amendment 6). 28x2 replaces the old 24x2.
            var board = useCompactBoard
                ? BoardProfile.Cross("Compact", 3, laps: 2)
                : BoardProfile.Standard;

            var seats = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow }
                .Take(players).ToList();

            _match = randomSquads
                ? MatchFactory.Create(
                    seats, seed,
                    squads: null,                      // null drafts every seat
                    board: board,
                    openingDeployments: openingDeployments)
                : MatchFactory.CreateAlphaMatch(
                    seats, seed,
                    board: board,
                    openingDeployments: openingDeployments);

            _layout = new BoardLayout(board, cellSpacing);

            var boardView = GetComponent<BoardView>() ?? gameObject.AddComponent<BoardView>();
            boardView.Build(_match.Map, _layout);

            _highlights = GetComponent<HighlightLayer>() ?? gameObject.AddComponent<HighlightLayer>();
            _highlights.Bind(_layout);
            _highlights.Clear();

            _feedback = GetComponent<FeedbackLayer>() ?? gameObject.AddComponent<FeedbackLayer>();
            _feedback.Bind(_layout.CellSize);

            foreach (var op in _match.Operators)
            {
                var go = new GameObject();
                go.transform.SetParent(transform, false);

                var piece = go.AddComponent<OperatorPiece>();
                piece.Bind(op, _layout.CellSize, cellSpacing);
                _pieces.Add(piece);
            }

            FrameCamera();
            Handle(_match.Engine.Start(), immediate: true);
        }

        /// <summary>Screen width the controls panel occupies, including its margin.</summary>
        private const float PanelWidth = 330f;

        private const float FrameMargin = 1.12f;

        private int _framedWidth;
        private int _framedHeight;
        private bool _framedWithPanel;

        /// <summary>
        /// Sizes the camera and slides the board clear of the controls panel.
        /// </summary>
        /// <remarks>
        /// <b>The camera moves away from the panel, not toward it.</b> Moving a
        /// camera right pushes the world left on screen, so a positive shift
        /// herded the board <i>under</i> the panel — which cost a quarter of a
        /// cell at 1920 wide and buried nearly half the board at 1024.
        ///
        /// <b>Width is sized for, not just height.</b> The panel is a fixed
        /// number of pixels, so the fraction of the view it eats grows as the
        /// Game view narrows. Framing on height alone was correct only at the
        /// aspect it happened to be tuned at.
        /// </remarks>
        private void FrameCamera()
        {
            var camera = Camera.main;
            if (camera == null) return;

            camera.orthographic = true;

            // Solid colour, not the skybox. A 2D board rendered against a sky
            // gradient washes out the cells, and clearFlags defaults to Skybox
            // on every camera the templates create.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.08f, 0.10f);

            float extent = _layout?.Extent ?? 8f;
            float aspect = Mathf.Max(0.1f, camera.aspect);

            float panelFraction = showPanel
                ? Mathf.Clamp01(PanelWidth / Mathf.Max(1f, Screen.width))
                : 0f;

            // Never let the panel claim so much of a tiny window that the board
            // is sized into nothing.
            float usable = Mathf.Max(0.25f, 1f - panelFraction);

            // Fit vertically, and fit horizontally in whatever the panel leaves.
            camera.orthographicSize = Mathf.Max(
                extent * FrameMargin,
                extent * FrameMargin / (aspect * usable));

            float halfWidth = camera.orthographicSize * aspect;
            camera.transform.position = new Vector3(-halfWidth * panelFraction, 0f, -10f);

            _framedWidth = Screen.width;
            _framedHeight = Screen.height;
            _framedWithPanel = showPanel;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) showPanel = !showPanel;

            // Game view resizing is routine while prototyping, and both the size
            // and the shift depend on aspect and on whether the panel is up.
            if (Screen.width != _framedWidth ||
                Screen.height != _framedHeight ||
                showPanel != _framedWithPanel)
            {
                FrameCamera();
            }
        }

        // ── Driving the engine ───────────────────────────────────────────

        private void Send(ICommand command) => Handle(_match.Engine.Execute(command), immediate: false);

        private void Handle(IReadOnlyList<IGameEvent> events, bool immediate)
        {
            foreach (var e in events)
            {
                _log.Add(e.ToString());

                // A rejection is information, not a failure. Surfacing it is how
                // a player learns the rules without a tutorial.
                if (e is CommandRejected) continue;
            }

            if (_log.Count > 200) _log.RemoveRange(0, _log.Count - 200);

            if (!immediate)
            {
                PlayFeedback(events);
                WalkMoves(events);
            }

            Reposition(immediate);
            RefreshHighlights();
        }

        /// <summary>
        /// Turns damage, healing and neutralize events into one-off effects.
        /// </summary>
        /// <remarks>
        /// Played <b>before</b> pieces are repositioned, so an effect lands on
        /// the cell where the thing happened rather than where the victim ends
        /// up. That matters most for a neutralize: the operator's progress is
        /// already the yard by the time the event arrives, and the burst belongs
        /// on the cell it fell on.
        /// </remarks>
        private void PlayFeedback(IReadOnlyList<IGameEvent> events)
        {
            if (_feedback == null) return;

            foreach (var e in events)
            {
                var damaged = e as DamageDealt;
                if (damaged != null && damaged.Amount > 0)
                {
                    var piece = PieceFor(damaged.Target);
                    if (piece == null) continue;

                    _feedback.Damage(piece.transform.position, damaged.Amount, damaged.Cause);
                    piece.Flash();
                    continue;
                }

                var evaded = e as DamageEvaded;
                if (evaded != null)
                {
                    var piece = PieceFor(evaded.Target);
                    if (piece != null) _feedback.Evaded(piece.transform.position);
                    continue;
                }

                var absorbed = e as DamageAbsorbed;
                if (absorbed != null)
                {
                    var piece = PieceFor(absorbed.Target);
                    if (piece != null) _feedback.Absorbed(piece.transform.position);
                    continue;
                }

                var healed = e as HealApplied;
                if (healed != null)
                {
                    var piece = PieceFor(healed.Target);
                    if (piece != null) _feedback.Heal(piece.transform.position, healed.Amount);
                    continue;
                }

                var down = e as OperatorNeutralized;
                if (down != null)
                {
                    var piece = PieceFor(down.Operator);
                    if (piece != null)
                    {
                        _feedback.Neutralized(
                            piece.transform.position,
                            BoardLayout.ColourOf(down.Operator.Owner),
                            down.Cause);
                    }
                }
            }
        }

        private OperatorPiece PieceFor(OperatorState op) =>
            op == null ? null : _pieces.Find(p => ReferenceEquals(p.Operator, op));

        /// <summary>
        /// Turns each move into a sequence of cells to walk through.
        /// </summary>
        /// <remarks>
        /// Only forward travel is walked. A pull reports the same progress twice
        /// and a bounce-back reports a lower one; both are placement rather than
        /// movement (§7.4, §7.2), and animating them as a walk would show the
        /// player a journey the rules say never happened.
        ///
        /// This is the only place the view enumerates intermediate cells, which
        /// makes it the one thing that can expose a discontinuous layout: if two
        /// consecutive progress values are not adjacent on screen, the piece
        /// visibly leaps. That is how the arm-tip gap in the old 48-cell board
        /// was caught (ADR-0002 Amendment 6). Keep it walking one cell at a time.
        ///
        /// <b>A split roll produces two walks, one per command</b> (§6). On two
        /// different pieces they run side by side and read fine. On the same
        /// piece twice they arrive back to back, and whether the second
        /// interrupts the first is <c>OperatorPiece.Walk</c>'s business —
        /// PRESENTATION §3 wants them sequenced, not overlapping.
        /// </remarks>
        private void WalkMoves(IReadOnlyList<IGameEvent> events)
        {
            foreach (var e in events)
            {
                var moved = e as OperatorMoved;
                if (moved == null || moved.To <= moved.From) continue;

                var piece = _pieces.Find(p => ReferenceEquals(p.Operator, moved.Operator));
                if (piece == null) continue;

                var path = new List<Vector3>();

                for (int progress = moved.From + 1; progress <= moved.To; progress++)
                    path.Add(_layout.PositionOf(_match.Map.CellAt(moved.Operator.Owner, progress)));

                piece.Walk(path);
            }
        }

        /// <summary>
        /// Rebuilds the landing ghosts and, if an ability is selected, the cells
        /// it reaches.
        /// </summary>
        /// <remarks>
        /// Landings come from the engine rather than being recomputed here.
        /// Distance depends on slows and auras, and a preview that did its own
        /// arithmetic would disagree with the rules exactly when a player is
        /// leaning on it.
        ///
        /// Since a roll can be split (§6) the engine reports several options per
        /// operator. The pooled landing is drawn bold and each single-die landing
        /// faintly, so what splitting costs is visible on the board before a die
        /// is clicked rather than discovered after.
        /// </remarks>
        private void RefreshHighlights()
        {
            if (_highlights == null || _match == null) return;

            _highlights.Clear();
            if (_match.Engine.MatchOver) return;

            var pooled = new List<CellRef>();
            var perDie = new List<CellRef>();

            foreach (var landing in _match.Engine.PreviewLandings())
            {
                var op = _match.Operators.FirstOrDefault(o => o.Id == landing.OperatorId);
                if (op == null) continue;

                var cell = _match.Map.CellAt(op.Owner, landing.Progress);

                if (landing.IsPooled) pooled.Add(cell);
                else perDie.Add(cell);
            }

            _highlights.ShowLandings(pooled, perDie);

            if (_selectedCaster != null && _selectedAbility != null && !_selectedCaster.IsInYard)
            {
                _highlights.ShowRange(
                    _match.Map,
                    _match.Map.CellAt(_selectedCaster.Owner, _selectedCaster.Progress),
                    _selectedAbility.Range);
            }
        }

        /// <summary>
        /// Places every piece, fanning out operators that share a cell.
        /// </summary>
        /// <remarks>
        /// The fan matters more than it looks: §7.5 makes a stack of enemies a
        /// real situation, and a player has to be able to see that two pieces
        /// are on one square before deciding to charge it.
        /// </remarks>
        private void Reposition(bool immediate)
        {
            var byCell = new Dictionary<CellRef, List<OperatorPiece>>();

            foreach (var piece in _pieces)
            {
                var cell = _match.Map.CellAt(piece.Operator.Owner, piece.Operator.Progress);

                if (!byCell.TryGetValue(cell, out var list))
                {
                    list = new List<OperatorPiece>();
                    byCell[cell] = list;
                }

                list.Add(piece);
            }

            foreach (var pair in byCell)
            {
                var basePosition = _layout.PositionOf(pair.Key);

                for (int i = 0; i < pair.Value.Count; i++)
                {
                    var piece = pair.Value[i];
                    var position = basePosition + _layout.Offset(i, pair.Value.Count);

                    if (immediate) piece.Place(position);
                    else piece.Settle(position);

                    piece.Refresh(_match.Engine.ActiveStatusesOn(piece.Operator));
                }
            }
        }

        // ── Controls ─────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (_match == null) return;

            // Rich text is off by default on the built-in skin, so the markup
            // below would otherwise render as literal angle brackets.
            GUI.skin.label.richText = true;

            if (!showPanel)
            {
                // One line, out of the way, so the key is discoverable without
                // the panel being up to advertise it.
                GUI.Label(new Rect(10, 10, 200, 20), "<b>Tab</b> — controls");
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, PanelWidth - 20f, Screen.height - 20), GUI.skin.box);

            // The panel outgrew the window the first time a caster was selected
            // with six enemies on the board. Scrolling is the floor, not the
            // fix — see DrawAbilities for the fix.
            _panelScroll = GUILayout.BeginScrollView(_panelScroll);
            DrawPanel();
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void DrawPanel()
        {
            var engine = _match.Engine;
            var seat = engine.CurrentPlayer;

            GUILayout.Label(engine.MatchOver
                ? "MATCH OVER"
                : $"<b>{seat.Color}</b>  turn {seat.TurnIndex}   energy {seat.Energy}");

            // Read the profile's own description rather than restating its
            // numbers. A hardcoded label is a second place to forget to update,
            // which is how "Standard 48x1" would have outlived the 48-cell board.
            GUILayout.Label($"{_match.Map.Profile}   phase {engine.Phase}");

            GUILayout.Label($"<i>{(randomSquads ? "drafted squads" : "alpha three")} — Tab hides this</i>");

            GUILayout.Space(6);

            if (GUILayout.Button("New match (reseed)"))
            {
                seed++;
                NewMatch();
                return;
            }

            if (engine.MatchOver)
            {
                DrawLog();
                return;
            }

            GUILayout.Space(6);

            // Movement is compulsory (§6.1), so the engine refuses both of these
            // while a die is still spendable. Greying them out says so before
            // the click rather than after — the same reasoning that put
            // CheckAbility behind the ability tray.
            bool owesMovement = engine.MustSpendRoll;
            var dice = engine.UnspentDice;

            GUILayout.Label(dice.Count == 0
                ? "<i>no dice in hand</i>"
                : $"unspent: <b>{Faces(dice)}</b>");

            GUI.enabled = !owesMovement;
            if (GUILayout.Button("Roll")) Send(new RollDiceCommand());
            if (GUILayout.Button(owesMovement ? "End turn — spend your roll first" : "End turn"))
                Send(new EndTurnCommand());
            GUI.enabled = true;

            GUILayout.Space(8);
            GUILayout.Label("<b>Your operators</b>");

            foreach (var op in seat.Operators)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label($"{op.Name} {op.Health}/{op.MaxHealth}", GUILayout.Width(110));

                if (op.IsInYard)
                {
                    if (GUILayout.Button("Deploy", GUILayout.Width(78))) Send(new DeployCommand(op.Id));
                }
                else
                {
                    DrawMoveButtons(op, dice);
                }

                bool selected = ReferenceEquals(op, _selectedCaster);
                if (GUILayout.Toggle(selected, "cast", GUI.skin.button, GUILayout.Width(42)) != selected)
                {
                    _selectedCaster = selected ? null : op;
                    _selectedAbility = null;      // an ability belongs to its caster
                    _selectedTarget = null;       // and a target belongs to its ability

                    RefreshHighlights();
                }

                GUILayout.EndHorizontal();
            }

            if (_selectedCaster != null) DrawAbilities(seat);

            DrawLog();
        }

        /// <summary>
        /// One button per way this operator could spend the roll: the whole thing
        /// at once, or a single die.
        /// </summary>
        /// <remarks>
        /// The split buttons only appear when there is a split to make. With one
        /// die left there is nothing to choose, and on a double the two faces are
        /// equal, so a second button would be a second way to press the first.
        ///
        /// Labelled with pips rather than cells. Cells depend on the operator's
        /// speed and the board already shows where each option lands, so putting
        /// the converted number here would be a third place for the same
        /// arithmetic to live.
        /// </remarks>
        private void DrawMoveButtons(OperatorState op, IReadOnlyList<int> dice)
        {
            if (dice.Count == 0)
            {
                GUILayout.Label("—", GUILayout.Width(78));
                return;
            }

            if (dice.Count == 1)
            {
                if (GUILayout.Button($"Move {dice[0]}", GUILayout.Width(78)))
                    Send(new MoveCommand(op.Id));

                return;
            }

            int total = 0;
            for (int i = 0; i < dice.Count; i++) total += dice[i];

            if (GUILayout.Button($"{total}", GUILayout.Width(30))) Send(new MoveCommand(op.Id));

            for (int i = 0; i < dice.Count; i++)
            {
                if (SeenEarlier(dice, i)) continue;

                if (GUILayout.Button($"{dice[i]}", GUILayout.Width(22)))
                    Send(new MoveCommand(op.Id, dice[i]));
            }
        }

        private static bool SeenEarlier(IReadOnlyList<int> dice, int index)
        {
            for (int i = 0; i < index; i++)
                if (dice[i] == dice[index]) return true;

            return false;
        }

        private static string Faces(IReadOnlyList<int> dice)
        {
            var text = "";

            for (int i = 0; i < dice.Count; i++)
                text += i == 0 ? dice[i].ToString() : $" + {dice[i]}";

            return text;
        }

        /// <summary>
        /// The caster's abilities, then — only if the chosen one needs one — a
        /// target.
        /// </summary>
        /// <remarks>
        /// <b>Ability first, and that ordering is the fix.</b> Asking for a
        /// target before knowing the ability meant listing every operator on the
        /// board: eleven buttons at four seats, and the panel ran off the bottom
        /// of the window. Most were never legal for the cast that followed.
        ///
        /// <b>Several abilities take no target at all</b> — every self-origin
        /// area, and both of Kian's, who has no single-target ability whatsoever.
        /// For those the list is not merely long, it is entirely noise.
        /// </remarks>
        private void DrawAbilities(PlayerState seat)
        {
            GUILayout.Space(8);
            GUILayout.Label($"<b>{_selectedCaster.Name}</b>");

            if (!_match.AbilitiesByOperator.TryGetValue(_selectedCaster.Id, out var abilities)) return;

            // Select, then cast. The extra click buys a look at the range before
            // spending energy, which matters on a board where reach turned out
            // to be the binding constraint on the whole combat layer.
            foreach (var ability in abilities)
            {
                bool chosen = _selectedAbility != null && _selectedAbility.Id == ability.Id;

                // Readiness comes from the engine, not from the view checking
                // energy and cooldowns itself (ADR-0004 amendment). Before this,
                // a player found out an ability was on cooldown by pressing it
                // and reading the rejection.
                var availability = _match.Engine.CheckAbility(_selectedCaster, ability);
                bool usable = availability == AbilityAvailability.Ready;

                string label = usable
                    ? $"{ability.Name}  ({ability.EnergyCost}e, r{ability.Range})"
                    : $"{ability.Name}  — {Explain(availability)}";

                var previous = GUI.color;
                if (!usable) GUI.color = new Color(0.6f, 0.6f, 0.62f);

                if (GUILayout.Toggle(chosen, label, GUI.skin.button) != chosen && usable)
                {
                    _selectedAbility = chosen ? null : ability;
                    _selectedTarget = null;     // a target belongs to its ability
                    RefreshHighlights();
                }

                GUI.color = previous;
            }

            if (_selectedAbility == null) return;

            if (_selectedAbility.RequiresTarget) DrawTargetList(seat);
            else GUILayout.Label("<i>no target — it fires around the caster</i>");

            GUILayout.Space(2);

            bool ready = !_selectedAbility.RequiresTarget || _selectedTarget != null;

            GUI.enabled = ready;

            if (GUILayout.Button(ready
                    ? $"CAST {_selectedAbility.Name}"
                    : $"CAST {_selectedAbility.Name} — pick a target"))
            {
                Send(new UseAbilityCommand(
                    _selectedCaster.Id, _selectedAbility.Id,
                    _selectedAbility.RequiresTarget && _selectedTarget != null
                        ? _selectedTarget.Id
                        : (int?)null));

                _selectedAbility = null;
                _selectedTarget = null;
            }

            GUI.enabled = true;
        }

        /// <summary>
        /// Who this cast could be aimed at, split by side.
        /// </summary>
        /// <remarks>
        /// <b>The engine decides who is legal.</b> Range, stealth and home
        /// columns are rules (§4) and the view must not evaluate them
        /// (<c>PRESENTATION.md</c> §1). The query also drops targets whose cast
        /// mode scopes every effect away — Neural Purge aimed at an enemy was
        /// never a legal cast — which is most of what makes the list short.
        ///
        /// Allies appear because they always were legal: Velvet Rope pulls a
        /// friend, All-In Mauling heals one, Nanite Infusion and Neural Purge
        /// exist for them, and Translocation swaps with one. This list showed
        /// enemies only, so the roster's healing had never been castable at all.
        ///
        /// The caster is filtered out here rather than in the engine. Aiming at
        /// yourself is legal by §10's mode rule and would let the Bouncer heal
        /// himself for the price of the ability — defensible, undecided, and not
        /// something the UI should settle by offering it.
        /// </remarks>
        private void DrawTargetList(PlayerState seat)
        {
            GUILayout.Space(4);
            GUILayout.Label("<b>Target</b>");

            var legal = _match.Engine
                .LegalTargetsFor(_selectedCaster, _selectedAbility)
                .Where(o => !ReferenceEquals(o, _selectedCaster))
                .ToList();

            if (legal.Count == 0)
            {
                GUILayout.Label("<i>nothing in reach</i>");
                return;
            }

            DrawTargets("Enemies", legal.Where(o => o.Owner != seat.Color));
            DrawTargets("Allies", legal.Where(o => o.Owner == seat.Color));
        }

        /// <summary>One side's targets, or nothing at all if that side has none.</summary>
        private void DrawTargets(string heading, IEnumerable<OperatorState> candidates)
        {
            var list = candidates.ToList();
            if (list.Count == 0) return;

            GUILayout.Label($"<i>{heading}</i>");

            foreach (var candidate in list)
            {
                bool selected = ReferenceEquals(candidate, _selectedTarget);

                if (GUILayout.Toggle(selected,
                        $"{candidate.Owner} {candidate.Name} {candidate.Health}/{candidate.MaxHealth}",
                        GUI.skin.button) != selected)
                {
                    _selectedTarget = selected ? null : candidate;
                }
            }
        }

        private static string Explain(AbilityAvailability availability)
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

        /// <summary>
        /// The last handful of events, newest first.
        /// </summary>
        /// <remarks>
        /// No scroll of its own: the whole panel scrolls now, and a scroll inside
        /// a scroll is miserable to use — the outer one steals the wheel the
        /// moment the pointer leaves the inner rect. Fewer lines shown instead,
        /// since the log is a tail rather than a record.
        /// </remarks>
        private void DrawLog()
        {
            GUILayout.Space(8);
            GUILayout.Label("<b>Events</b>");

            for (int i = _log.Count - 1; i >= 0 && i > _log.Count - 18; i--)
                GUILayout.Label(_log[i]);
        }
    }
}