// Assets/_Project/Scripts/Unity/View/OperatorPiece.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// One operator on screen: a figure, its shape pin, a health bar, and the
    /// walk between cells. Holds no game state — it is told what is true and
    /// shows it.
    /// </summary>
    /// <remarks>
    /// <b>A person, not a token</b> (GUI increment G2, ART_DIRECTION §6.1).
    /// In the yard the operator is a bust seated at its table; on the floor it
    /// is a standing figure. Both wear the operator's shape
    /// (<see cref="PieceShape"/>) as a gilt pin, so identity reads at a glance
    /// until the rendered character models replace the figures. Size still
    /// carries maximum health.
    ///
    /// Status badges used to live here, as world-space squares. They moved to
    /// <see cref="PieceHudLayer"/> as named screen-space tags (ADR-0008
    /// increment C): at board scale a world-space badge was a few pixels
    /// wide, and a word does not fit in that.
    ///
    /// <b>Evasion is the exception: it fades the piece instead of tagging it.</b>
    /// An evasive operator is drawn at <see cref="EvasiveAlpha"/>, which reads
    /// as "hard to pin down" without a word. The panel and log still name it.
    /// If Stealth ever wants a look of its own, it must not be this one.
    ///
    /// <b>It walks the track rather than sliding to the destination.</b> A
    /// straight-line slide was fine on the old ring layout; on the cross it cuts
    /// diagonally through the board interior and reads as a teleport. Following
    /// the cells is also the only way a player can count a move and check it.
    ///
    /// <b>It draws the board-first marks it is given</b> (<see cref="PieceMark"/>):
    /// a cyan glow and a halo over the head when selected, a pulsing glow when
    /// a click would deploy it, an amber ring when it can be targeted, and a
    /// slight lift under the pointer. Cyan is the tech register for live,
    /// interactive states (ART_DIRECTION §8). Which marks apply is decided
    /// elsewhere.
    ///
    /// <b>Motion (MOTION.md increment MO2).</b> The piece keeps a logical
    /// ground point and draws itself above it: a hop per cell with a small
    /// squash on landing, a rise when it deploys, a shatter when it is knocked
    /// out and a pop when it reappears seated, and idle breathing (standing)
    /// or a slow sway (seated). Reduced motion keeps the walk as a glide and
    /// drops the hop, the squash and the idle. Every clock is scaled time,
    /// times <see cref="MotionSettings.Rate"/>, so pause freezes the piece.
    /// </remarks>
    public sealed class OperatorPiece : MonoBehaviour
    {
        /// <summary>Cells per second while hopping.</summary>
        private const float HopsPerSecond = 8f;

        /// <summary>Cells per second while gliding under Reduced motion.</summary>
        private const float GlidePerSecond = 11f;

        /// <summary>Peak hop height, in cells. Kept low on the designer's note (2026-09-16): a step, not a jump.</summary>
        private const float HopHeight = 0.08f;

        private const float SettleSpeed = 12f;

        /// <summary>
        /// How long a bounced piece rests on the contested cell before it is
        /// thrown back (PRESENTATION §3). Long enough to read, short enough not
        /// to drag a match already over its length budget.
        /// </summary>
        private const float BounceHoldSeconds = 0.3f;

        /// <summary>Opacity of an evasive operator's silhouette and outline.</summary>
        private const float EvasiveAlpha = 0.7f;

        private const float HoverLift = 1.12f;
        private const float PulseSpeed = 4f;

        /// <summary>A figure is drawn this much larger than the old shape token, since a person is mostly air.</summary>
        private const float FigureScale = 1.45f;

        /// <summary>The shape pin's size, as a fraction of the figure.</summary>
        private const float PinSize = 0.18f;

        /// <summary>Health bar height above the figure's centre, in figure units.</summary>
        private const float BarHeight = 0.6f;

        private const float PopSeconds = 0.3f;
        private const float ShatterSeconds = 0.45f;
        private const int ShardCount = 9;

        private static Color SelectColour => UiTheme.Select;   // holo cyan, a live state
        private static Color TargetColour => UiTheme.Threat;   // amber, a warning

        private readonly Queue<Vector3> _path = new Queue<Vector3>();

        private SpriteRenderer _body;
        private SpriteRenderer _outline;
        private SpriteRenderer _pin;
        private SpriteRenderer _healthBack;
        private SpriteRenderer _healthFill;
        private float _alpha = 1f;
        private Color _seatColour;
        private Vector3 _target;
        private float _stepDistance = 1f;
        private float _flash;
        private float _hold;

        private SpriteRenderer _glow;
        private SpriteRenderer _halo;
        private SpriteRenderer _targetRing;
        private bool? _seated;
        private PieceMark _marks;
        private float _baseScale = 1f;

        private MotionSettings _motion;

        /// <summary>Where the piece stands, before hop, squash and idle are drawn on top.</summary>
        private Vector3 _ground;
        private bool _hopping;
        private Vector3 _hopFrom;
        private float _hopT;
        private float _lift;
        private float _land;
        private float _hover = 1f;
        private float _pop = -1f;
        private float _popFrom = 1f;
        private bool _popRise;
        private bool _hidden;
        private float _idleTime;
        private float _idlePhase;

        /// <summary>Raised each time a hop lands on a cell, for the presentation beats.</summary>
        public event System.Action<OperatorPiece> Stepped;

        public OperatorState Operator { get; private set; }

        /// <summary>True while the piece is still travelling, so the view can wait before re-posing it.</summary>
        public bool IsWalking => _path.Count > 0;

        /// <summary>
        /// True while the piece walks or rests on a contested cell before a
        /// bounce, so the presentation queue can wait for the whole move
        /// (MOTION.md increment MO1).
        /// </summary>
        public bool IsMoving => _path.Count > 0 || _hold > 0f;

        /// <summary>True between a knockout's shatter and the piece reappearing in its yard.</summary>
        public bool IsHidden => _hidden;

        /// <summary>Whether the piece is drawn seated. Follows the presentation, not the engine.</summary>
        public bool Seated => _seated ?? true;

        /// <summary>The health the piece last showed. The HUD label reads this, so it never runs ahead of the hit.</summary>
        public int ShownHealth { get; private set; }

        /// <summary>The drawn radius in world units, for hit testing. Ignores the hover lift.</summary>
        public float Radius => _baseScale * 0.4f;

        public PieceMark Marks => _marks;

        private bool Reduced => _motion != null && _motion.ReducedMotion;
        private float Rate => _motion != null ? _motion.Rate : 1f;

        public void Bind(OperatorState op, float cellSize, float cellSpacing, MotionSettings motion)
        {
            Operator = op;
            name = $"{op.Owner}_{op.Name}";
            _motion = motion;
            ShownHealth = op.Health;

            // The figure's shading darkens the tint, so the seat colour is
            // lifted a little to land on its true value.
            _seatColour = Color.Lerp(BoardLayout.ColourOf(op.Owner), Color.white, UiTheme.FigureLift);
            _stepDistance = cellSpacing;

            // Pieces breathe out of step with each other.
            _idlePhase = (op.Id * 0.618f) % 1f * Mathf.PI * 2f;

            _body = gameObject.AddComponent<SpriteRenderer>();
            _body.color = _seatColour;
            _body.sortingOrder = 4;

            _outline = Child("outline", 1f, Vector3.zero);
            _outline.color = UiTheme.PieceOutline;
            _outline.sortingOrder = 3;

            _pin = Child("pin", PinSize, Vector3.zero);
            _pin.sprite = PieceShape.For(op);
            _pin.color = UiTheme.PieceEmblem;
            _pin.sortingOrder = 5;

            BuildHealthBar();
            BuildMarks();
            SetPose(op.IsInYard);

            _baseScale = cellSize * PieceShape.SizeFor(op) * FigureScale;
            transform.localScale = Vector3.one * _baseScale;
            _ground = transform.position;
            _target = _ground;
        }

        /// <summary>
        /// Seated in the yard, standing anywhere else. Swaps sprites and moves
        /// the pin and the halo; cheap, and a no-op when the pose is unchanged.
        /// </summary>
        private void SetPose(bool seated)
        {
            if (_seated == seated) return;
            _seated = seated;

            _body.sprite = seated ? BoardArt.Bust : BoardArt.Pawn;
            _outline.sprite = seated ? BoardArt.BustOutline : BoardArt.PawnOutline;
            _pin.transform.localPosition = seated ? BoardArt.BustPin : BoardArt.PawnPin;

            // The halo sits over the head; a seated bust's head is lower.
            var head = seated ? new Vector2(0f, 0.16f) : BoardArt.PawnHead;
            _halo.transform.localPosition = head;
        }

        /// <summary>
        /// Sets the board-first marks. Cheap to call every refresh: nothing is
        /// rebuilt, rings are only shown, hidden and tinted.
        /// </summary>
        public void SetMarks(PieceMark marks)
        {
            _marks = marks;

            if (_glow == null) return;

            bool selected = (marks & PieceMark.Selected) != 0;
            bool deployable = (marks & PieceMark.Deployable) != 0;
            _glow.enabled = selected || deployable;
            _halo.enabled = selected;

            bool target = (marks & PieceMark.Target) != 0;
            bool targetable = (marks & PieceMark.Targetable) != 0;
            _targetRing.enabled = target || targetable;
            _targetRing.color = UiTheme.WithAlpha(TargetColour, target ? 1f : 0.45f);
            _targetRing.transform.localScale = Vector3.one * (target ? 1.15f : 1f);
        }

        /// <summary>Places the piece with no animation. For the opening layout and for snaps.</summary>
        public void Place(Vector3 position)
        {
            _path.Clear();
            _hopping = false;
            _hold = 0f;
            _lift = 0f;
            _target = position;
            _ground = position;
            Draw();
        }

        /// <summary>Walks a sequence of cells, ending at the last.</summary>
        public void Walk(IReadOnlyList<Vector3> waypoints) => Walk(waypoints, null);

        /// <summary>
        /// Walks a sequence of cells, then — if <paramref name="bouncedTo"/> is
        /// given — pauses on the last one and slides back to it.
        /// </summary>
        /// <remarks>
        /// <b>The slide back is placement, not a walk</b> (PRESENTATION §3): it
        /// uses the same settle as a pull, so it never reads as a second move.
        /// The pause is what makes the contested cell visible at all.
        /// </remarks>
        public void Walk(IReadOnlyList<Vector3> waypoints, Vector3? bouncedTo)
        {
            _path.Clear();
            _hopping = false;

            foreach (var point in waypoints) _path.Enqueue(point);

            if (bouncedTo.HasValue)
            {
                _target = bouncedTo.Value;
                _hold = BounceHoldSeconds;
            }
            else if (waypoints.Count > 0)
            {
                _target = waypoints[waypoints.Count - 1];
                _hold = 0f;
            }
        }

        /// <summary>Sets the destination without a path — placement, not movement.</summary>
        public void Settle(Vector3 position)
        {
            if (IsWalking) return;
            _target = position;
        }

        /// <summary>
        /// A deploy: the piece snaps onto its cell and rises from the seated
        /// bust to the standing figure with a pop and a ring in its seat colour.
        /// </summary>
        public void Rise(Vector3 position)
        {
            Place(position);
            SetPose(false);
            DrawHealth(Operator.Health);
            StartPop(0.45f, rise: true);

            var ring = UiTheme.WithAlpha(BoardLayout.ColourOf(Operator.Owner), 0.9f);
            FxSprite.Spawn(transform.parent, Primitives.Ring, ring, 2,
                new FxPose(position, Vector3.one * (_baseScale * 0.5f)),
                new FxPose(position, Vector3.one * (_baseScale * 1.8f), 0f, 0f),
                _motion != null ? _motion.Tween(0.4f) : 0.4f, _motion);
        }

        /// <summary>
        /// A knockout: the figure breaks into shards in its seat colour and is
        /// hidden until <see cref="Reappear"/>.
        /// </summary>
        public void Shatter()
        {
            if (_hidden) return;

            var at = transform.position;
            var colour = BoardLayout.ColourOf(Operator.Owner);
            float seconds = _motion != null ? _motion.Tween(ShatterSeconds) : ShatterSeconds;
            float reach = (Reduced ? 0.5f : 0.9f) * _stepDistance;

            for (int i = 0; i < ShardCount; i++)
            {
                float angle = (i + 0.35f * Mathf.Sin(i * 2.3f + _idlePhase)) / ShardCount * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                float size = _baseScale * (0.16f + 0.06f * ((i * 7) % 3));
                float spin = (i % 2 == 0 ? 1f : -1f) * 220f;

                FxSprite.Spawn(transform.parent, Primitives.Polygon(3, 90f), colour, 7,
                    new FxPose(at, Vector3.one * size, 0f),
                    new FxPose(at + direction * reach * (0.7f + 0.1f * (i % 4)), Vector3.one * (size * 0.4f), spin, 0f),
                    seconds, _motion);
            }

            _hidden = true;
            _path.Clear();
            _hopping = false;
            _hold = 0f;
            Draw();
        }

        /// <summary>Shows a hidden piece again, snapped to <paramref name="position"/>, with a small pop.</summary>
        public void Reappear(Vector3 position)
        {
            _hidden = false;
            Place(position);
            StartPop(0.4f, rise: false);
        }

        private void StartPop(float from, bool rise)
        {
            _pop = 0f;
            _popFrom = from;
            _popRise = rise;
        }

        /// <summary>
        /// A brief white-out on the silhouette. The floater says how much; this
        /// says <i>who</i>, which a number rising off a crowded cell does not.
        /// </summary>
        public void Flash() => _flash = 1f;

        /// <summary>
        /// Shows <paramref name="health"/> on the bar and the label at the
        /// moment a hit or a heal lands, rather than at the settle. The value
        /// comes from the event (<c>DamageDealt.RemainingHealth</c>, or the shown
        /// health plus a heal). A seated piece shows no bar and ignores it.
        /// </summary>
        public void ShowHealth(int health)
        {
            if (Operator == null || Seated) return;
            DrawHealth(Mathf.Clamp(health, 0, Operator.MaxHealth));
        }

        /// <summary>Redraws health and tint from the operator's state.</summary>
        /// <param name="evasive">
        /// Whether the engine reports Evasion on this operator. Passed in, never
        /// inferred: the piece does not read the status registry (PRESENTATION §1).
        /// </param>
        public void Refresh(bool evasive)
        {
            if (Operator == null || _body == null) return;

            _alpha = evasive ? EvasiveAlpha : 1f;

            // The pose says "waiting"; the figure keeps its full colour.
            SetPose(Operator.IsInYard);

            _body.color = WithAlpha(Color.Lerp(_seatColour, Color.white, _flash));
            _outline.color = WithAlpha(_outline.color);
            _pin.color = WithAlpha(_pin.color);

            DrawHealth(Operator.Health);
        }

        private void DrawHealth(int shown)
        {
            ShownHealth = shown;

            // A seated operator is at full health by the rules; no bar.
            bool showBar = !Seated;
            _healthBack.enabled = showBar;
            _healthFill.enabled = showBar;

            if (!showBar) return;

            float health = Mathf.Clamp01((float)shown / Mathf.Max(1, Operator.MaxHealth));

            // Anchored left so the bar drains rightward rather than shrinking
            // toward its centre, which reads as distance rather than loss.
            _healthFill.transform.localScale = new Vector3(0.66f * health, 0.08f, 1f);
            _healthFill.transform.localPosition = new Vector3(-0.33f * (1f - health), BarHeight, 0f);
            _healthFill.color = Color.Lerp(UiTheme.Danger, _seatColour, health);
        }

        private Color WithAlpha(Color colour)
        {
            colour.a = _alpha;
            return colour;
        }

        private void BuildHealthBar()
        {
            _healthBack = Child("health_back", 1f, new Vector3(0f, BarHeight, 0f));
            _healthBack.sprite = Primitives.Square;
            _healthBack.color = UiTheme.PieceBarBack;
            _healthBack.sortingOrder = 5;
            _healthBack.transform.localScale = new Vector3(0.74f, 0.13f, 1f);

            _healthFill = Child("health_fill", 1f, new Vector3(0f, BarHeight, 0f));
            _healthFill.sprite = Primitives.Square;
            _healthFill.sortingOrder = 6;
        }

        private void BuildMarks()
        {
            // A glow behind the figure: it reads around any silhouette and
            // never hides the one it marks.
            _glow = Child("select_glow", 1.35f, Vector3.zero);
            _glow.sprite = DecoSprites.Glow;
            _glow.color = SelectColour;
            _glow.sortingOrder = 1;
            _glow.enabled = false;

            _halo = Child("halo", 0.5f, Vector3.zero);
            _halo.sprite = BoardArt.Halo;
            _halo.color = SelectColour;
            _halo.sortingOrder = 6;
            _halo.enabled = false;

            _targetRing = Child("target_ring", 1f, Vector3.zero);
            _targetRing.sprite = Primitives.Ring;
            _targetRing.sortingOrder = 2;
            _targetRing.enabled = false;
        }

        private SpriteRenderer Child(string childName, float scale, Vector3 localPosition)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * scale;

            return go.AddComponent<SpriteRenderer>();
        }

        // ── Frame ────────────────────────────────────────────────────────

        private void Update()
        {
            float delta = Time.deltaTime;
            float rated = delta * Rate;

            AnimateMarks(delta);

            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - delta * 4f);
                // The flash must not undo the evasive fade, so alpha is put back.
                if (_body != null) _body.color = WithAlpha(Color.Lerp(_body.color, Color.white, _flash * 0.5f));
            }

            _land = Mathf.Max(0f, _land - rated * 10f);
            if (_pop >= 0f)
            {
                _pop += rated;
                float length = _motion != null ? _motion.Tween(PopSeconds) : PopSeconds;
                if (_pop >= length) _pop = -1f;
            }

            if (_path.Count > 0) Hop(rated);
            else if (_hold > 0f) _hold -= rated;   // a bounced piece rests on the contested cell
            else _ground = Vector3.Lerp(_ground, _target, Mathf.Clamp01(rated * SettleSpeed));

            if (!_hopping && _path.Count == 0 && _hold <= 0f) _idleTime += delta;

            Draw();
        }

        /// <summary>One step of the walk: a hop (or a glide under Reduced motion) toward the next cell.</summary>
        private void Hop(float rated)
        {
            var next = _path.Peek();

            if (!_hopping)
            {
                _hopping = true;
                _hopFrom = _ground;
                _hopT = 0f;
            }

            // A segment is normally one cell; a longer one takes proportionally longer.
            float cells = Mathf.Max(0.25f, Vector3.Distance(_hopFrom, next) / Mathf.Max(0.01f, _stepDistance));
            float speed = Reduced ? GlidePerSecond : HopsPerSecond;
            _hopT += rated * speed / cells;

            if (_hopT < 1f)
            {
                _ground = Vector3.Lerp(_hopFrom, next, _hopT);
                _lift = Reduced ? 0f : Mathf.Sin(_hopT * Mathf.PI) * HopHeight * _stepDistance;
                return;
            }

            _ground = next;
            _lift = 0f;
            _path.Dequeue();
            _hopping = false;
            if (!Reduced) _land = 1f;

            Stepped?.Invoke(this);
        }

        /// <summary>Puts the transform where the ground, the hop, the pop and the idle say.</summary>
        private void Draw()
        {
            float sx = 1f, sy = 1f, roll = 0f;

            if (!Reduced)
            {
                // Stretch in the air, squash on landing.
                float air = _hopping ? Mathf.Sin(_hopT * Mathf.PI) : 0f;
                sx *= 1f - 0.015f * air + 0.04f * _land;
                sy *= 1f + 0.025f * air - 0.06f * _land;

                if (!_hopping && _path.Count == 0)
                {
                    if (Seated)
                    {
                        roll = 1.8f * Mathf.Sin(_idleTime * 0.9f + _idlePhase);
                    }
                    else
                    {
                        float breath = Mathf.Sin(_idleTime * 1.8f + _idlePhase);
                        sy *= 1f + 0.02f * breath;
                        sx *= 1f - 0.01f * breath;
                    }
                }
            }

            float pop = 1f;
            if (_pop >= 0f)
            {
                float length = _motion != null ? _motion.Tween(PopSeconds) : PopSeconds;
                float t = Mathf.Clamp01(_pop / length);
                pop = t < 0.55f
                    ? Mathf.Lerp(_popFrom, 1.15f, t / 0.55f)
                    : Mathf.Lerp(1.15f, 1f, (t - 0.55f) / 0.45f);

                // A rise stands up: taller before it is wider.
                if (_popRise) sy *= 1f + 0.18f * Mathf.Sin(t * Mathf.PI);
            }

            float scale = _hidden ? 0f : _baseScale * _hover * pop;

            transform.position = _ground + Vector3.up * _lift;
            transform.localScale = new Vector3(scale * sx, scale * sy, 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        }

        /// <summary>The hover lift, and the pulse that says "click to deploy".</summary>
        private void AnimateMarks(float delta)
        {
            float wanted = (_marks & PieceMark.Hovered) != 0 ? HoverLift : 1f;
            _hover = Mathf.MoveTowards(_hover, wanted, delta * 2f);

            if (_glow == null || !_glow.enabled) return;

            // Selected is steady; deployable-only pulses, so the two read apart.
            bool pulsing = (_marks & PieceMark.Deployable) != 0 && (_marks & PieceMark.Selected) == 0;
            float alpha = pulsing ? 0.2f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed)) : 0.6f;

            _glow.color = UiTheme.WithAlpha(SelectColour, alpha);
        }
    }
}