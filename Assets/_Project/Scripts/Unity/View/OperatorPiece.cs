// Assets/_Project/Scripts/Unity/View/OperatorPiece.cs
using System.Collections.Generic;
using NonaRoyale.Core.Model;
using UnityEngine;
using UnityEngine.Rendering;

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
    /// (<see cref="PieceShape"/>) as a gilt pin, so identity reads at a glance.
    /// Size still carries maximum health.
    ///
    /// <b>Rendered art, pose by pose</b> (ART_HOOKUP.md, increment ART1). When
    /// <see cref="OperatorArtLibrary"/> has a render for the pose, the body
    /// shows it instead of the procedural bust or pawn:
    /// <list type="bullet">
    /// <item>It is fitted into the procedural figure's frame
    /// (<see cref="FigureLayout.Fit"/>): feet on the pawn's feet, the pawn's
    /// height. The pin, halo and bar follow the fitted figure.</item>
    /// <item>It is drawn untinted. The seat is shown by a tinted disc and ring
    /// under the feet (standing only; a seated operator sits at its own seat's
    /// table), and by the pin (designer, 2026-09-17).</item>
    /// <item>The outline child is hidden: the render has its own.</item>
    /// <item>The hit flash is a white silhouette drawn over the body, since
    /// lerping an untinted sprite's colour toward white changes nothing.</item>
    /// </list>
    /// A missing pose falls back to the procedural figure on its own, so an
    /// operator with only a standing render still sits as a bust.
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
    /// Since ART1 the squash and the breathing keep the feet on the floor
    /// (<see cref="FigureLayout.FootAnchorOffset"/>); the transform's origin is
    /// still the figure's centre, which the floaters and sounds read.
    ///
    /// <b>Rigged figures (OPERATOR_LOOKBOOK.md, LB5b).</b> An operator with a
    /// rig and no render is drawn as parts that move at the joints
    /// (<see cref="RigView"/>, <see cref="RigAnimator"/>). The fallback order
    /// is a render, then the rig, then the look book, then the pawn. A rigged
    /// figure takes the render's place in everything above - fitted by
    /// <see cref="FigureLayout.Fit"/>, untinted over the seat disc, flashed
    /// white - and its own motion replaces the whole-sprite tricks: the step
    /// replaces the hop and its squash, the idle and the seated loop replace
    /// the breathing and the sway, and a rise stands up out of a crouch
    /// inside the same pop. It faces toward the next cell as it walks and
    /// toward the board centre at rest (<see cref="BoardCentre"/>). The pin
    /// rides the chest.
    ///
    /// <b>Event poses (LB5c).</b> A cast turns the figure to its target and
    /// raises the device toward it, lit cyan for the hold (<see cref="Cast"/>).
    /// A hit turns it to the striker and rocks it back (<see cref="Recoil"/>).
    /// A knockout folds the figure before the shatter, so the shards leave a
    /// body that has gone down rather than one standing at rest
    /// (<see cref="Shatter"/>, <see cref="IsKnockingOut"/>).
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

        /// <summary>The shape pin's size on a procedural figure, as a fraction of the figure.</summary>
        private const float PinSize = 0.18f;

        /// <summary>
        /// The pin's size on a rendered figure. Smaller than on the pawn, since
        /// it sits on painted clothes rather than a flat body. Tune in Play Mode.
        /// </summary>
        private const float ArtPinSize = 0.12f;

        /// <summary>A rendered standing figure's height against the pawn's. Tune in Play Mode.</summary>
        private const float ArtStandingHeightScale = 1f;

        /// <summary>
        /// A rendered seated figure's height against the bust's. The seated
        /// render is waist up with the arms forward, so at the bust's height
        /// its head came out about 30% smaller than the bust's head, and the
        /// figure read too small at the table (designer, 2026-09-17). 1.4
        /// matches the head sizes; the figure grows upward from the table
        /// line. Tune in Play Mode.
        /// </summary>
        private const float ArtSeatedHeightScale = 1.6f;

        /// <summary>Peak opacity of the white silhouette on a rendered figure's hit flash.</summary>
        private const float ArtFlashStrength = 0.75f;

        /// <summary>
        /// Everything drawn over the figure - the pin, the bar, the halo - sits
        /// at or above this order inside the piece's group, clear of a rig's
        /// parts, which take two orders each from <see cref="RigBaseOrder"/>.
        /// </summary>
        private const int OverlayOrder = 40;

        /// <summary>A rig's first part; with two orders a part, room for 18 parts under the overlay.</summary>
        private const int RigBaseOrder = 4;

        /// <summary>How long a rigged figure takes to stand out of its crouch on a deploy.</summary>
        private const float RigRiseSeconds = 0.4f;

        /// <summary>A rigged figure's recoil on a hit: the hit's hold in MOTION.md.</summary>
        private const float RigHitSeconds = 0.3f;

        /// <summary>A rigged figure's fold before the shatter.</summary>
        private const float RigFoldSeconds = 0.25f;

        /// <summary>How far across, in cells, a target must be before a rigged figure turns to it.</summary>
        private const float FacingDeadZone = 0.05f;

        /// <summary>The seat disc under a rendered figure: width and depth, in figure units.</summary>
        private const float SeatBaseWidth = 0.9f;
        private const float SeatBaseDepth = 0.36f;
        private const float SeatBaseAlpha = 0.45f;
        private const float SeatRingAlpha = 0.9f;

        // ── Contact shadow (VISUAL_PASS.md, V2) ─────────────────

        /// <summary>
        /// Where the figure meets the table: tighter than the seat disc and
        /// darker, because it is a contact and not a marker.
        /// </summary>
        private const float ContactWidth = 0.56f;
        private const float ContactAlpha = 0.5f;

        /// <summary>
        /// What a hop does to it: spreads it and takes the dark out, the way a
        /// shadow loses its edge as the thing casting it leaves the ground.
        /// </summary>
        private const float ContactSpread = 0.5f;
        private const float ContactFade = 0.62f;

        /// <summary>And what a landing does: one frame of a tighter, harder shadow under the impact.</summary>
        private const float ContactPunch = 0.4f;

        private const float PopSeconds = 0.3f;
        private const float ShatterSeconds = 0.45f;
        private const int ShardCount = 9;

        private static Color SelectColour => UiTheme.Select;   // holo cyan, a live state
        private static Color TargetColour => UiTheme.Threat;   // amber, a warning

        private readonly Queue<Vector3> _path = new Queue<Vector3>();

        /// <summary>Everything that stands up under the tilt (V1b). The ground markings stay on the root.</summary>
        private Transform _figure;

        /// <summary>Makes the piece one unit for depth sorting, so a near piece covers a far one.</summary>
        private SortingGroup _sorting;

        /// <summary>The pitch the ground markings were last shaped for, so they are reshaped only on a change.</summary>
        private float _shapedPitch = -1f;

        private SpriteRenderer _body;
        private SpriteRenderer _flashOverlay;
        private SpriteRenderer _outline;
        private SpriteRenderer _pin;
        private SpriteRenderer _healthBack;
        private SpriteRenderer _healthFill;
        private SpriteRenderer _seatBase;
        private SpriteRenderer _seatRing;
        private SpriteRenderer _contact;
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
        private bool _rendered;
        private FigureLayout _layout = FigureLayout.Bust;
        private PieceMark _marks;
        private float _baseScale = 1f;

        private MotionSettings _motion;

        // ── The rig (LB5b) ──────────────────────────────────────
        private RigView _rig;
        private RigAnimator _animator;
        private bool _facesLeft;

        /// <summary>Cells finished in the current walk, and the length of the one under way.</summary>
        private float _walkCells;
        private float _segmentCells = 1f;

        /// <summary>The pin's point on the chest bone, in the chest's rest space; recomputed on a pose or facing change.</summary>
        private Vector2 _pinOnChest;
        private bool _pinPlaced;

        /// <summary>Folding before the shatter (LB5c); the shards follow when the fold ends.</summary>
        private bool _collapsing;

        /// <summary>Scaled seconds the shards are still flying, so the queue can wait for them.</summary>
        private float _shardClock;

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

        /// <summary>True while a rigged figure folds, before its shatter (LB5c).</summary>
        public bool IsCollapsing => _collapsing;

        /// <summary>
        /// True from a knockout until its shards have landed: the fold, then
        /// the shatter. The presentation queue waits on it, so the piece is
        /// not brought back to its yard mid-fall.
        /// </summary>
        public bool IsKnockingOut => _collapsing || _shardClock > 0f;

        /// <summary>Where the piece stands on the board, before the hop, the squash and the idle are drawn on top.</summary>
        public Vector3 Ground => _ground;

        /// <summary>Whether the piece is drawn seated. Follows the presentation, not the engine.</summary>
        public bool Seated => _seated ?? true;

        /// <summary>Whether the current pose is a rendered figure rather than the procedural one. True for a rig too.</summary>
        public bool ShowsRenderedArt => _rendered;

        /// <summary>Whether the figure is drawn as a rig (LB5b).</summary>
        public bool ShowsRig => RigActive;

        /// <summary>Whether a rigged figure faces the left of the screen.</summary>
        public bool FacesLeft => _facesLeft;

        /// <summary>Where a rigged figure looks at rest. The board's centre, set by whoever places the pieces.</summary>
        public Vector3 BoardCentre { get; set; }

        /// <summary>The health the piece last showed. The HUD label reads this, so it never runs ahead of the hit.</summary>
        public int ShownHealth { get; private set; }

        /// <summary>The drawn radius in world units, for hit testing. Ignores the hover lift.</summary>
        public float Radius => _baseScale * 0.4f;

        /// <summary>
        /// The figure's footprint on screen, in pixels, for hit testing under
        /// the tilt (VISUAL_PASS.md, V1b).
        /// </summary>
        /// <remarks>
        /// A standing figure is drawn well above the cell it stands on, so a
        /// click on its head turns into a board point roughly a cell behind it.
        /// Testing against where the figure actually appears is the only way
        /// clicking a figure can select that figure. The body's world bounds
        /// already account for the lean, so its eight corners projected to the
        /// screen give the rectangle; a rectangle is generous at the shoulders,
        /// which suits a target this small.
        /// </remarks>
        public bool TryScreenBounds(Camera camera, out Rect rect)
        {
            rect = default;
            if (camera == null || _body == null || _hidden) return false;

            // A rig is the union of its parts; the body child draws nothing then.
            Bounds bounds;
            if (RigActive)
            {
                if (!_rig.TryBounds(out bounds)) return false;
            }
            else
            {
                if (!_body.enabled) return false;
                bounds = _body.bounds;
            }

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);

                var screen = camera.WorldToScreenPoint(point);
                if (screen.z <= 0f) return false;

                if (screen.x < minX) minX = screen.x;
                if (screen.x > maxX) maxX = screen.x;
                if (screen.y < minY) minY = screen.y;
                if (screen.y > maxY) maxY = screen.y;
            }

            rect = new Rect(minX, minY, maxX - minX, maxY - minY);
            return rect.width > 0f && rect.height > 0f;
        }

        public PieceMark Marks => _marks;

        private bool Reduced => _motion != null && _motion.ReducedMotion;
        private bool RigActive => _rig != null && _rig.Active;
        private float Rate => _motion != null ? _motion.Rate : 1f;

        /// <summary>A procedural figure takes the seat colour; a rendered one is drawn as painted.</summary>
        private Color BodyColour => _rendered ? Color.white : _seatColour;

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

            // A rig breathes on the same clock, from the same phase.
            _animator = new RigAnimator(_idlePhase / 1.8f);

            // Everything that stands up hangs off one child, so the lean is
            // applied once (V1b). The ground markings stay on the root, where
            // the camera foreshortens them as it should. A SortingGroup makes
            // the whole piece one unit for depth, with the parts inside it
            // keeping the orders they always had.
            _figure = new GameObject("figure").transform;
            _figure.SetParent(transform, false);

            _sorting = gameObject.GetComponent<SortingGroup>();
            if (_sorting == null) _sorting = gameObject.AddComponent<SortingGroup>();

            // The body is a child so a render can be scaled and moved inside
            // the figure's frame. For the procedural figure it sits at the
            // origin, exactly where the root's own renderer used to be.
            _body = Child("body", 1f, Vector3.zero, _figure);
            _body.color = _seatColour;
            _body.sortingOrder = 4;

            // Same order as the body, a hair nearer the camera, so it draws on top.
            _flashOverlay = Child("flash", 1f, new Vector3(0f, 0f, -0.001f), _body.transform);
            _flashOverlay.sortingOrder = 4;
            _flashOverlay.enabled = false;

            _outline = Child("outline", 1f, Vector3.zero, _figure);
            _outline.color = UiTheme.PieceOutline;
            _outline.sortingOrder = 3;

            _pin = Child("pin", PinSize, Vector3.zero, _figure);
            _pin.sprite = PieceShape.For(op);
            _pin.color = UiTheme.PieceEmblem;
            _pin.sortingOrder = OverlayOrder;

            BuildSeatBase();
            BuildHealthBar();
            BuildMarks();
            SetPose(op.IsInYard);

            _baseScale = cellSize * PieceShape.SizeFor(op) * FigureScale;
            transform.localScale = Vector3.one * _baseScale;
            _ground = transform.position;
            _target = _ground;
        }

        /// <summary>
        /// Seated in the yard, standing anywhere else. Swaps the figure, then
        /// moves the pin, the halo, the bar and the seat disc to fit it. Cheap,
        /// and a no-op when the pose is unchanged.
        /// </summary>
        private void SetPose(bool seated)
        {
            if (_seated == seated) return;
            _seated = seated;

            var frame = seated ? FigureLayout.Bust : FigureLayout.Pawn;
            var pose = seated ? FigurePose.Seated : FigurePose.Standing;

            // A render, then a rig, then the look book, then the pawn.
            var rig = OperatorArtLibrary.Rendered(Operator.Name, pose) == null ? OperatorRigArt.For(Operator.Name) : null;
            var art = rig == null ? OperatorArtLibrary.Figure(Operator.Name, pose) : null;
            _rendered = art != null || rig != null;

            if (rig != null)
            {
                _layout = FigureLayout.Fit(
                    seated ? rig.SeatedBottom : rig.RestBottom,
                    seated ? rig.SeatedTop : rig.RestTop,
                    frame,
                    seated ? ArtSeatedHeightScale : ArtStandingHeightScale,
                    seated ? FigureLayout.SeatedChest : FigureLayout.StandingChest);

                _body.sprite = null;
                _flashOverlay.sprite = null;
                _outline.enabled = false;
                _pin.transform.localPosition = new Vector3(0f, _layout.PinY, 0f);
                _pin.transform.localScale = Vector3.one * ArtPinSize;
            }
            else if (art != null)
            {
                _layout = FigureLayout.Fit(art.OpaqueBottom, art.OpaqueTop, frame,
                    seated ? ArtSeatedHeightScale : ArtStandingHeightScale,
                    seated ? FigureLayout.SeatedChest : FigureLayout.StandingChest);

                _body.sprite = art.Sprite;
                _flashOverlay.sprite = art.Silhouette;
                _outline.enabled = false;
                _pin.transform.localPosition = new Vector3(0f, _layout.PinY, 0f);
                _pin.transform.localScale = Vector3.one * ArtPinSize;
            }
            else
            {
                _layout = frame;

                _body.sprite = seated ? BoardArt.Bust : BoardArt.Pawn;
                _flashOverlay.sprite = null;
                _outline.enabled = true;
                _outline.sprite = seated ? BoardArt.BustOutline : BoardArt.PawnOutline;
                _pin.transform.localPosition = seated ? BoardArt.BustPin : BoardArt.PawnPin;
                _pin.transform.localScale = Vector3.one * PinSize;
            }

            _flashOverlay.enabled = false;
            _body.transform.localPosition = new Vector3(0f, _layout.ArtY, 0f);
            _body.transform.localScale = Vector3.one * _layout.ArtScale;

            ShowRig(rig, seated);

            // The halo sits over the head; a seated figure's head is lower.
            _halo.transform.localPosition = new Vector3(0f, _layout.HeadY, 0f);

            _healthBack.transform.localPosition = new Vector3(0f, _layout.BarY, 0f);

            // The seat disc stands in for the tint a render does not take.
            bool showBase = _rendered && !seated;
            _seatBase.enabled = showBase;
            _seatRing.enabled = showBase;
            // First placement only; GroundMarks owns these every frame after.
            _seatBase.transform.localPosition = new Vector3(0f, _layout.Feet, 0f);
            _seatRing.transform.localPosition = new Vector3(0f, _layout.Feet, 0f);

            // The contact is the figure's own, not the render's, so it goes
            // under a placeholder pawn too - but only while the figure stands
            // up out of the table.
            ShowContact(!seated);

            _body.color = WithAlpha(BodyColour);
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
            _walkCells = 0f;
            _animator?.StopCast();

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

            // A rigged figure stands up out of a crouch inside the pop.
            if (RigActive) _animator.Rise(_motion != null ? _motion.Tween(RigRiseSeconds) : RigRiseSeconds);

            var ring = UiTheme.WithAlpha(BoardLayout.ColourOf(Operator.Owner), 0.9f);
            FxSprite.Spawn(transform.parent, Primitives.Ring, ring, 2,
                new FxPose(position, Vector3.one * (_baseScale * 0.5f)),
                new FxPose(position, Vector3.one * (_baseScale * 1.8f), 0f, 0f),
                _motion != null ? _motion.Tween(0.4f) : 0.4f, _motion);
        }

        /// <summary>
        /// A knockout: the figure breaks into shards in its seat colour and is
        /// hidden until <see cref="Reappear"/>. A standing rigged figure folds
        /// first and shatters as the fold ends (LB5c).
        /// </summary>
        public void Shatter()
        {
            if (_hidden || _collapsing) return;

            if (RigActive && !Seated)
            {
                _collapsing = true;
                _path.Clear();
                _hopping = false;
                _hold = 0f;
                _animator.Knockout(_motion != null ? _motion.Tween(RigFoldSeconds) : RigFoldSeconds);
                return;
            }

            ShatterNow();
        }

        /// <summary>The shards, and the piece hidden.</summary>
        private void ShatterNow()
        {
            if (_hidden) return;
            _collapsing = false;

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
            _shardClock = seconds;
            _path.Clear();
            _hopping = false;
            _hold = 0f;
            _animator?.Stop();
            Draw();
        }

        /// <summary>Shows a hidden piece again, snapped to <paramref name="position"/>, with a small pop.</summary>
        public void Reappear(Vector3 position)
        {
            _hidden = false;
            _collapsing = false;
            _shardClock = 0f;
            _animator?.Clear();
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
        /// A rigged figure's recoil (LB5c): it turns to face
        /// <paramref name="from"/>, when the striker is known, and rocks back
        /// away from it. No-op for any other figure, whose hit is the flash alone.
        /// </summary>
        public void Recoil(Vector3? from)
        {
            if (!RigActive || Seated || _hidden || _collapsing) return;

            if (from.HasValue) TurnToward(from.Value.x);
            _animator.Hit(_motion != null ? _motion.Tween(RigHitSeconds) : RigHitSeconds);
        }

        /// <summary>
        /// A rigged figure's cast (LB5c): it turns to <paramref name="aim"/>
        /// and raises its device toward it, lit cyan for the hold, which ends
        /// with a cast tell of <paramref name="tellSeconds"/>. With no aim it
        /// casts where it faces. No-op for any other figure.
        /// </summary>
        public void Cast(Vector3? aim, float tellSeconds)
        {
            if (!RigActive || Seated || _hidden || _collapsing) return;

            float elevation = 0f;
            if (aim.HasValue)
            {
                TurnToward(aim.Value.x);
                elevation = RigAnimator.Elevation(_ground.x, _ground.y, aim.Value.x, aim.Value.y);
            }

            var pose = RigAnimator.Aimed(_rig.Current.Rig, elevation);
            _animator.Cast(pose, RigClips.CastLengthFor(tellSeconds));
        }

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

            _body.color = _rendered ? WithAlpha(BodyColour) : WithAlpha(Color.Lerp(BodyColour, Color.white, _flash));
            if (_rig != null) _rig.SetAlpha(_alpha);
            _outline.color = WithAlpha(_outline.color);
            _pin.color = WithAlpha(_pin.color);
            TintSeatBase();

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
            _healthFill.transform.localPosition = new Vector3(-0.33f * (1f - health), _layout.BarY, 0f);
            _healthFill.color = Color.Lerp(UiTheme.Danger, _seatColour, health);
        }

        private Color WithAlpha(Color colour)
        {
            colour.a = _alpha;
            return colour;
        }

        private void TintSeatBase()
        {
            var seat = BoardLayout.ColourOf(Operator.Owner);
            _seatBase.color = UiTheme.WithAlpha(seat, SeatBaseAlpha * _alpha);
            _seatRing.color = UiTheme.WithAlpha(seat, SeatRingAlpha * _alpha);
        }

        private void BuildSeatBase()
        {
            // Under everything else the piece draws, on its own negative order
            // inside the piece's SortingGroup - so it stays beneath this
            // figure while a nearer figure's shadow still draws over it.
            _contact = Child("contact_shadow", 1f, Vector3.zero);
            _contact.sprite = BoardArt.SoftDisc;
            _contact.sortingOrder = -1;
            _contact.color = UiTheme.WithAlpha(Color.black, ContactAlpha);
            _contact.enabled = false;

            // The floor it stands on.
            _seatBase = Child("seat_base", 1f, Vector3.zero);
            _seatBase.sprite = BoardArt.SoftDisc;
            _seatBase.sortingOrder = 0;
            _seatBase.transform.localScale = new Vector3(SeatBaseWidth, SeatBaseDepth, 1f);
            _seatBase.enabled = false;

            _seatRing = Child("seat_ring", 1f, Vector3.zero);
            _seatRing.sprite = Primitives.Ring;
            _seatRing.sortingOrder = 1;
            _seatRing.transform.localScale = new Vector3(SeatBaseWidth, SeatBaseDepth, 1f);
            _seatRing.enabled = false;

            TintSeatBase();
        }

        private void BuildHealthBar()
        {
            _healthBack = Child("health_back", 1f, new Vector3(0f, FigureLayout.Pawn.BarY, 0f), _figure);
            _healthBack.sprite = Primitives.Square;
            _healthBack.color = UiTheme.PieceBarBack;
            _healthBack.sortingOrder = OverlayOrder;
            _healthBack.transform.localScale = new Vector3(0.74f, 0.13f, 1f);

            _healthFill = Child("health_fill", 1f, new Vector3(0f, FigureLayout.Pawn.BarY, 0f), _figure);
            _healthFill.sprite = Primitives.Square;
            _healthFill.sortingOrder = OverlayOrder + 1;
        }

        private void BuildMarks()
        {
            // A glow behind the figure: it reads around any silhouette and
            // never hides the one it marks.
            _glow = Child("select_glow", 1.35f, Vector3.zero, _figure);
            _glow.sprite = DecoSprites.Glow;
            _glow.color = SelectColour;
            _glow.sortingOrder = 1;
            _glow.enabled = false;

            _halo = Child("halo", 0.5f, Vector3.zero, _figure);
            _halo.sprite = BoardArt.Halo;
            _halo.color = SelectColour;
            _halo.sortingOrder = OverlayOrder + 1;
            _halo.enabled = false;

            _targetRing = Child("target_ring", 1f, Vector3.zero);
            _targetRing.sprite = Primitives.Ring;
            _targetRing.sortingOrder = 2;
            _targetRing.enabled = false;
        }

        private SpriteRenderer Child(string childName, float scale, Vector3 localPosition, Transform parent = null)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(parent != null ? parent : transform, false);
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
            AnimateFlash(delta);

            _land = Mathf.Max(0f, _land - rated * 10f);
            if (_pop >= 0f)
            {
                _pop += rated;
                float length = _motion != null ? _motion.Tween(PopSeconds) : PopSeconds;
                if (_pop >= length) _pop = -1f;
            }

            if (_shardClock > 0f) _shardClock = Mathf.Max(0f, _shardClock - rated);

            if (_path.Count > 0) Hop(rated);
            else if (_hold > 0f) _hold -= rated;   // a bounced piece rests on the contested cell
            else _ground = Vector3.Lerp(_ground, _target, Mathf.Clamp01(rated * SettleSpeed));

            bool idle = !_hopping && _path.Count == 0 && _hold <= 0f;
            if (idle) _idleTime += delta;

            if (RigActive)
            {
                _animator.Advance(idle ? delta : 0f, rated);
                if (_collapsing && _animator.Folded) ShatterNow();
                Face();
            }

            Draw();
        }

        private void AnimateFlash(float delta)
        {
            if (_body == null) return;

            if (RigActive)
            {
                // Every part carries its own white silhouette.
                if (_flash > 0f) _flash = Mathf.Max(0f, _flash - delta * 4f);
                _rig.Flash(_flash * ArtFlashStrength * _alpha);
                return;
            }

            if (_flash <= 0f)
            {
                if (_flashOverlay.enabled) _flashOverlay.enabled = false;
                return;
            }

            _flash = Mathf.Max(0f, _flash - delta * 4f);

            if (_rendered)
            {
                // A painted figure cannot be tinted whiter; a white silhouette
                // is laid over it instead. No silhouette (unreadable texture): no flash.
                _flashOverlay.enabled = _flashOverlay.sprite != null && _flash > 0f;
                _flashOverlay.color = new Color(1f, 1f, 1f, _flash * ArtFlashStrength * _alpha);
            }
            else
            {
                // The flash must not undo the evasive fade, so alpha is put back.
                _body.color = WithAlpha(Color.Lerp(_body.color, Color.white, _flash * 0.5f));
            }
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
            _segmentCells = cells;

            // A rigged figure steps rather than hops: no lift, no landing squash.
            bool hops = !Reduced && !RigActive;

            if (_hopT < 1f)
            {
                _ground = Vector3.Lerp(_hopFrom, next, _hopT);
                _lift = hops ? Mathf.Sin(_hopT * Mathf.PI) * HopHeight * _stepDistance : 0f;
                return;
            }

            _ground = next;
            _lift = 0f;
            _path.Dequeue();
            _hopping = false;
            _walkCells += cells;
            if (hops) _land = 1f;

            Stepped?.Invoke(this);
        }

        /// <summary>Puts the transform where the ground, the hop, the pop and the idle say.</summary>
        private void Draw()
        {
            float sx = 1f, sy = 1f, roll = 0f;
            bool rig = RigActive;

            // A rig moves its own joints; the whole-sprite squash, breath and sway are for flat figures.
            if (!Reduced && !rig)
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
                if (_popRise && !rig) sy *= 1f + 0.18f * Mathf.Sin(t * Mathf.PI);
            }

            float scale = _hidden ? 0f : _baseScale * _hover * pop;

            // Squash, the rise's stretch and breathing scale about the centre;
            // this puts the feet back where they were, so a rise stands up from
            // the floor. The pop and the hover lift are meant to grow the whole
            // figure, so they are left alone.
            float anchor = _layout.FootAnchorOffset(scale, sy);

            // The squash compensation is along the board, where the feet are;
            // the hop rises on screen, which under the tilt is out of the table
            // as well as up it (V1b). Both are world up for the flat camera.
            transform.position = _ground + Vector3.up * anchor + BoardTilt.ScreenUp * _lift;

            // z takes the same scale as y, which it did not need to before: the
            // leaning child rotates about x, so it turns its own y into z, and
            // a parent that scaled y by 0.6 and z by 1 would shear the figure
            // instead of tipping it. Flat sprites do not care about z scale, so
            // the top-down view is unaffected.
            transform.localScale = new Vector3(scale * sx, scale * sy, scale * sy);
            transform.localRotation = Quaternion.Euler(0f, 0f, roll);

            GroundMarks(scale);
            Stand();
            Depth();
            PoseRig();
        }

        // ── The rig (LB5b) ───────────────────────────────────────────────

        /// <summary>
        /// Shows the rig for a pose, building its view on first use, or hides
        /// it when the pose draws something else.
        /// </summary>
        private void ShowRig(RigArt art, bool seated)
        {
            if (art == null)
            {
                if (_rig != null) _rig.Active = false;
                return;
            }

            if (_rig == null || _rig.Art != art)
            {
                _rig?.Destroy();
                _rig = new RigView(_body.transform, art, RigBaseOrder);
            }

            _rig.Active = true;
            _rig.Seated = seated;
            _rig.FacesLeft = _facesLeft;
            _rig.SetAlpha(_alpha);
            _animator.Seated = seated;
            _pinPlaced = false;
            PoseRig();
        }

        /// <summary>Turns a rigged figure toward where it is going, or toward the board centre at rest.</summary>
        private void Face()
        {
            // A cast, a recoil or a fold keeps the facing it was given.
            if (_animator.Casting || _animator.Recoiling || _animator.KnockedOut) return;

            float toX;
            if (_path.Count > 0) toX = _path.Peek().x;
            else if (_hold > 0f) return;   // resting on a contested cell: keep looking where it went
            else toX = BoardCentre.x;

            TurnToward(toX);
        }

        /// <summary>Faces a rigged figure toward a point across the board, outside the dead zone.</summary>
        private void TurnToward(float toX)
        {
            bool left = RigAnimator.FaceLeft(_ground.x, toX, _facesLeft, FacingDeadZone * _stepDistance);
            if (left == _facesLeft) return;

            _facesLeft = left;
            _rig.FacesLeft = left;
            _pinPlaced = false;

            // A cast built for the other facing would be mirrored wrong.
            _animator.StopCast();
        }

        /// <summary>Places the rig's parts for this frame, and the pin on its chest.</summary>
        private void PoseRig()
        {
            if (!RigActive) return;

            _animator.Seated = Seated;
            _animator.ReducedMotion = Reduced;
            _animator.WalkCells = _path.Count > 0 ? _walkCells + (_hopping ? _hopT * _segmentCells : 0f) : (float?)null;

            var facing = _rig.Current;
            if (!_pinPlaced) PlacePin(facing);

            _animator.Sample(facing.Rig, facing.Crouch, out var a, out var b, out float t);
            _rig.Powered = _animator.Powered;
            _rig.Apply(a, b, t);

            // The pin rides the chest: wherever the chest carries the point it sits on in the pose's base.
            var at = _rig.Place(RigBones.Chest, _pinOnChest.x, _pinOnChest.y);
            float scale = _layout.ArtScale;
            _pin.transform.localPosition = new Vector3(at.x * scale, _layout.ArtY + at.y * scale, 0f);
        }

        /// <summary>
        /// Finds the point on the chest bone that the layout's pin height
        /// lands on in the base pose (rest standing, the seated pose seated).
        /// </summary>
        private void PlacePin(RigFacingArt facing)
        {
            var rig = facing.Rig;
            var basePose = rig.Pose(Seated ? RigPoseNames.Seated : RigPoseNames.Rest);
            var world = rig.Skeleton.Evaluate(basePose);
            var chest = rig.Skeleton[RigBones.Chest];

            float y = (_layout.PinY - _layout.ArtY) / Mathf.Max(0.0001f, _layout.ArtScale);
            RigSkeleton.Untransform(world[RigBones.Chest], chest.PivotX, chest.PivotY, 0f, y, out float rx, out float ry);

            _pinOnChest = new Vector2(rx, ry);
            _pinPlaced = true;
        }

        /// <summary>
        /// Leans the figure up out of the table, about its feet (V1b). Nothing
        /// happens under the flat camera: the lean is zero and the pivot is the
        /// origin, so the piece is drawn exactly as it always was.
        /// </summary>
        private void Stand()
        {
            if (_figure == null) return;

            float lean = BoardTilt.Lean;
            FigureTilt.LeanPivot(BoardTilt.Pitch, _layout.Feet, out float py, out float pz);

            _figure.localPosition = new Vector3(0f, py, pz);
            _figure.localRotation = Quaternion.Euler(lean, 0f, 0f);

            if (Mathf.Approximately(_shapedPitch, BoardTilt.Pitch)) return;

            _shapedPitch = BoardTilt.Pitch;
            ShapeGround();
        }

        /// <summary>
        /// The seat disc and its ring. The flat view fakes a foreshortened
        /// ellipse, because nothing was foreshortening it; under the tilt the
        /// camera does that itself, so the disc is drawn as a true circle.
        /// </summary>
        private void ShapeGround()
        {
            float depth = BoardTilt.IsTilted ? SeatBaseWidth : SeatBaseDepth;
            var shape = new Vector3(SeatBaseWidth, depth, 1f);

            if (_seatBase != null) _seatBase.transform.localScale = shape;
            if (_seatRing != null) _seatRing.transform.localScale = shape;

            ShowContact(!Seated);
        }

        /// <summary>
        /// Whether the contact shadow is up. Tilted and standing, and nothing
        /// else: under the flat camera there is no gap between the figure and
        /// the table to cast one across, and the seat disc already says where
        /// the piece stands. That keeps the top-down view exactly as it was,
        /// which is the rule the rest of the tilt work followed.
        /// </summary>
        private void ShowContact(bool standing)
        {
            if (_contact != null) _contact.enabled = standing && BoardTilt.IsTilted;
        }

        /// <summary>
        /// Everything the piece draws on the table, every frame: the seat disc,
        /// its ring, and the contact shadow. All three stay on the table while
        /// the figure leaves it; the shadow also spreads and pales at the top
        /// of a hop and snaps tight and dark on the landing.
        /// </summary>
        /// <remarks>
        /// <b>They have to be un-hopped.</b> All three ride the root, and the
        /// root carries <c>ScreenUp * _lift</c>; left alone they sail up with
        /// the piece, which is the one thing a mark on the floor must not do.
        /// The lift is taken back out through
        /// <see cref="Transform.InverseTransformVector"/>, so the root's scale
        /// and roll are accounted for without repeating them here.
        ///
        /// <b>This owns their position from here on.</b> <c>SetPose</c> still
        /// places the seat disc and ring once, for the frame before the first
        /// draw; after that it is this, every frame, because the correction
        /// changes with the hop.
        ///
        /// The roll is deliberately left in. It is a degree or two of idle
        /// sway, and countering it every frame would buy nothing anyone could
        /// see.
        /// </remarks>
        private void GroundMarks(float scale)
        {
            var at = new Vector3(0f, _layout.Feet, 0f);

            if (_lift > 0f && scale > 0.0001f)
                at -= transform.InverseTransformVector(BoardTilt.ScreenUp * _lift);

            // The seat disc and its ring are the floor the figure stands on, so
            // they stay on the floor when it jumps. They rode the hop from the
            // day they were written - the root carries ScreenUp * _lift and
            // they are children of it - which nothing noticed under the flat
            // camera, where the lift is straight up the screen and a disc
            // sliding up behind a piece reads as the piece's own shadow moving.
            // The tilt made it a mark flying off the table.
            if (_seatBase != null) _seatBase.transform.localPosition = at;
            if (_seatRing != null) _seatRing.transform.localPosition = at;

            if (_contact == null || !_contact.enabled) return;

            _contact.transform.localPosition = at;

            // Reduced motion has no hop and never sets the landing, so both
            // terms fall to zero and the shadow simply sits there.
            float air = _hopping && !Reduced ? Mathf.Sin(_hopT * Mathf.PI) : 0f;

            float width = ContactWidth * (1f + ContactSpread * air) * (1f - 0.12f * _land);

            // A true circle under the tilt, which the camera foreshortens; the
            // flat view never shows this at all, so there is no ellipse to fake.
            _contact.transform.localScale = new Vector3(width, width, 1f);

            float alpha = ContactAlpha * _alpha * (1f - ContactFade * air) * (1f + ContactPunch * _land);
            _contact.color = UiTheme.WithAlpha(Color.black, Mathf.Clamp01(alpha));
        }

        /// <summary>
        /// Which pieces draw in front. Nearer the camera is a lower y, so the
        /// order rises as the piece comes forward. Left alone under the flat
        /// camera, where creation order never showed.
        /// </summary>
        private void Depth()
        {
            if (_sorting == null) return;

            int order = BoardTilt.IsTilted
                ? FigureTilt.SortOrder(transform.position.y, BoardTilt.SortExtent)
                : 0;

            if (_sorting.sortingOrder != order) _sorting.sortingOrder = order;
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