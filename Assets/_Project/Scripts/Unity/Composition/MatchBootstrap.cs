// Assets/_Project/Scripts/Unity/Composition/MatchBootstrap.cs
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Commands;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
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
        [Tooltip("Compact 24x2 was withdrawn in ADR-0002 Amendment 4 — it moved " +
                 "pieces across half the loop per turn. Kept switchable for comparison only.")]
        public bool useCompactBoard = false;

        [Range(2, 4)] public int players = 4;

        [Tooltip("Operators already on the board at the start. 2 is the adopted value.")]
        [Range(0, 3)] public int openingDeployments = 2;

        public int seed = 20260912;

        [Header("Presentation")]
        [Tooltip("World units per board cell.")]
        public float cellSpacing = 1f;

        private MatchFactory.Match _match;
        private BoardLayout _layout;
        private readonly List<OperatorPiece> _pieces = new List<OperatorPiece>();
        private readonly List<string> _log = new List<string>();

        private OperatorState _selectedCaster;
        private OperatorState _selectedTarget;
        private Vector2 _logScroll;

        private void Start() => NewMatch();

        private void NewMatch()
        {
            foreach (var piece in _pieces)
                if (piece != null) Destroy(piece.gameObject);

            _pieces.Clear();
            _log.Clear();
            _selectedCaster = null;
            _selectedTarget = null;

            var board = useCompactBoard
                ? new BoardProfile("Compact", 24, 3, laps: 2)
                : BoardProfile.Standard;

            var seats = new[] { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow }
                .Take(players).ToList();

            _match = MatchFactory.CreateAlphaMatch(
                seats, seed, board, openingDeployments: openingDeployments);

            _layout = new BoardLayout(board, cellSpacing);

            var boardView = GetComponent<BoardView>() ?? gameObject.AddComponent<BoardView>();
            boardView.Build(_match.Map, _layout);

            foreach (var op in _match.Operators)
            {
                var go = new GameObject();
                go.transform.SetParent(transform, false);

                var piece = go.AddComponent<OperatorPiece>();
                piece.Bind(op, _layout.CellSize);
                _pieces.Add(piece);
            }

            FrameCamera();
            Handle(_match.Engine.Start(), immediate: true);
        }

        private const float PanelWidth = 330f;

        private int _framedWidth;
        private int _framedHeight;

        /// <summary>
        /// Sizes the camera and slides the board clear of the controls panel.
        /// </summary>
        /// <remarks>
        /// The panel is drawn in screen space over the left of the view, so a
        /// board centred on the world origin sits half-hidden behind it. The
        /// camera shifts right by half the panel's width in world units, which
        /// centres the board in the space actually visible.
        /// </remarks>
        private void FrameCamera()
        {
            var camera = Camera.main;
            if (camera == null) return;

            camera.orthographic = true;

            // Fit the whole cross with a little margin. Extent comes from the
            // layout, so Standard and Compact both frame correctly without a
            // per-profile number here.
            camera.orthographicSize = (_layout?.Extent ?? 8f) * 1.12f;

            // Solid colour, not the skybox. A 2D board rendered against a sky
            // gradient washes out the cells, and clearFlags defaults to Skybox
            // on every camera the templates create.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.08f, 0.10f);

            float halfWidth = camera.orthographicSize * camera.aspect;
            float shift = halfWidth * (PanelWidth / Mathf.Max(1, Screen.width));

            camera.transform.position = new Vector3(shift, 0f, -10f);

            _framedWidth = Screen.width;
            _framedHeight = Screen.height;
        }

        private void Update()
        {
            // Game view resizing is routine while prototyping, and the shift
            // above depends on aspect.
            if (Screen.width != _framedWidth || Screen.height != _framedHeight) FrameCamera();
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

            Reposition(immediate);
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
                    pair.Value[i].MoveTo(basePosition + _layout.Offset(i, pair.Value.Count), immediate);
                    pair.Value[i].Refresh();
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

            var engine = _match.Engine;
            var seat = engine.CurrentPlayer;

            GUILayout.BeginArea(new Rect(10, 10, 320, Screen.height - 20), GUI.skin.box);

            GUILayout.Label(engine.MatchOver
                ? "MATCH OVER"
                : $"<b>{seat.Color}</b>  turn {seat.TurnIndex}   energy {seat.Energy}");

            GUILayout.Label($"{(useCompactBoard ? "Compact 24x2" : "Standard 48x1")}   " +
                            $"phase {engine.Phase}");

            GUILayout.Space(6);

            if (GUILayout.Button("New match (reseed)"))
            {
                seed++;
                NewMatch();
                GUILayout.EndArea();
                return;
            }

            if (engine.MatchOver)
            {
                DrawLog();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Space(6);

            if (GUILayout.Button("Roll")) Send(new RollDiceCommand());
            if (GUILayout.Button("End turn")) Send(new EndTurnCommand());

            GUILayout.Space(8);
            GUILayout.Label("<b>Your operators</b>");

            foreach (var op in seat.Operators)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label($"{op.Name} {op.Health}/{op.MaxHealth}", GUILayout.Width(120));

                if (op.IsInYard)
                {
                    if (GUILayout.Button("Deploy")) Send(new DeployCommand(op.Id));
                }
                else if (GUILayout.Button("Move"))
                {
                    Send(new MoveCommand(op.Id));
                }

                bool selected = ReferenceEquals(op, _selectedCaster);
                if (GUILayout.Toggle(selected, "cast", GUI.skin.button, GUILayout.Width(46)) != selected)
                    _selectedCaster = selected ? null : op;

                GUILayout.EndHorizontal();
            }

            if (_selectedCaster != null) DrawAbilities(seat);

            DrawLog();
            GUILayout.EndArea();
        }

        private void DrawAbilities(PlayerState seat)
        {
            GUILayout.Space(8);
            GUILayout.Label($"<b>{_selectedCaster.Name} — target</b>");

            foreach (var candidate in _match.Operators.Where(o => o.Owner != seat.Color && !o.IsInYard))
            {
                bool selected = ReferenceEquals(candidate, _selectedTarget);

                if (GUILayout.Toggle(selected,
                        $"{candidate.Owner} {candidate.Name} {candidate.Health}/{candidate.MaxHealth}",
                        GUI.skin.button) != selected)
                {
                    _selectedTarget = selected ? null : candidate;
                }
            }

            GUILayout.Space(4);

            if (!_match.AbilitiesByOperator.TryGetValue(_selectedCaster.Id, out var abilities)) return;

            foreach (var ability in abilities)
            {
                string label = $"{ability.Name}  ({ability.EnergyCost}e, r{ability.Range})";

                if (GUILayout.Button(label))
                {
                    Send(new UseAbilityCommand(
                        _selectedCaster.Id, ability.Id,
                        ability.RequiresTarget && _selectedTarget != null ? _selectedTarget.Id : (int?)null));
                }
            }
        }

        private void DrawLog()
        {
            GUILayout.Space(8);
            GUILayout.Label("<b>Events</b>");

            _logScroll = GUILayout.BeginScrollView(_logScroll);

            for (int i = _log.Count - 1; i >= 0 && i > _log.Count - 40; i--)
                GUILayout.Label(_log[i]);

            GUILayout.EndScrollView();
        }
    }
}