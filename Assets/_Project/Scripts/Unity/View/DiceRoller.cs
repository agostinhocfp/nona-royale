// Assets/_Project/Scripts/Unity/View/DiceRoller.cs
using System;
using NonaRoyale.Core.Board;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The dice moment (MOTION.md decision 1): two dice tumble over the vault
    /// at the board's centre, land on the engine's faces, hold a beat, then
    /// fly to the tray.
    /// </summary>
    /// <remarks>
    /// <b>The landing is always the engine's.</b> The faces shown while the
    /// dice tumble are view-only and come from this component's own random
    /// stream, never the match's. What they settle on is what
    /// <c>DiceRolled</c> reported.
    ///
    /// <b>Pips in the tumble, digits in the tray.</b> Pips read as dice at a
    /// glance, which is the point of the moment; the tray keeps its digits
    /// for counting. The flying dice fade as they arrive, and the tray pops its
    /// faces in once they have (<see cref="ActionTray"/>).
    ///
    /// <b>Doubles get a callout</b>: "DOUBLES", plus "roll again" when the
    /// engine grants another roll. The hold is longer so it can be read.
    ///
    /// A soft glow in the rolling seat's colour sits under the dice, so the
    /// roll says whose it is. Scaled time, times <see cref="Speed"/>. Nothing
    /// here catches the pointer.
    /// </remarks>
    public sealed class DiceRoller : MonoBehaviour
    {
        /// <summary>Canvas units per die.</summary>
        private const float DieSize = 92f;

        /// <summary>Canvas units from the centre to each die's resting spot.</summary>
        private const float RestOffset = 64f;

        private const float TumbleSeconds = 0.75f;
        private const float HoldSeconds = 0.4f;
        private const float DoublesHoldSeconds = 0.85f;
        private const float FlySeconds = 0.32f;

        /// <summary>How often the tumbling faces change at the start, in seconds. It slows toward the landing.</summary>
        private const float FaceChangeSeconds = 0.06f;

        /// <summary>Full turns each die spins while tumbling.</summary>
        private const float SpinTurns = 1.6f;

        /// <summary>How far away the dice are thrown from, in canvas units.</summary>
        private const float ThrowDistance = 150f;

        private enum Phase { Idle, Tumble, Hold, Fly }

        private sealed class Die
        {
            public RectTransform Root;
            public Image[] Pips;
            public TMP_Text Digit;
            public Vector2 From;
            public Vector2 Rest;
            public float Spin;
            public int Face;
            public int Shown;
        }

        /// <summary>Multiplier on the roller's clock. 1 is normal.</summary>
        public float Speed { get; set; } = 1f;

        /// <summary>Whether the dice are still on screen: tumbling, holding or flying.</summary>
        public bool IsRolling => _phase != Phase.Idle;

        /// <summary>Whether the dice are still tumbling, before they show the engine's faces.</summary>
        public bool IsTumbling => _phase == Phase.Tumble;

        private readonly System.Random _faces = new System.Random();

        private RectTransform _layer;
        private RectTransform _root;
        private CanvasGroup _group;
        private Image _glow;
        private TMP_Text _callout;
        private Die[] _dice;
        private Func<RectTransform> _flyTarget;

        private Phase _phase;
        private float _time;
        private float _sinceFace;
        private float _hold;
        private Vector2 _flyTo;

        /// <param name="layer">The match HUD layer the dice draw on.</param>
        /// <param name="flyTarget">Where the dice fly to: the tray's dice row, looked up when the flight starts. May return null.</param>
        public void Bind(RectTransform layer, Func<RectTransform> flyTarget)
        {
            _flyTarget = flyTarget;
            if (_root == null) Build(layer);
            Skip();
        }

        /// <summary>
        /// Throws the dice.
        /// </summary>
        /// <param name="first">The engine's first face.</param>
        /// <param name="second">The engine's second face.</param>
        /// <param name="seat">Whose roll it is, for the glow.</param>
        /// <param name="boardCentre">The vault's world position; the dice land over it.</param>
        /// <param name="rollAgain">Whether the engine grants another roll.</param>
        public void Play(int first, int second, PlayerColor seat, Vector3 boardCentre, bool rollAgain)
        {
            if (_root == null) return;

            var centre = CanvasPoint(boardCentre);
            _root.anchoredPosition = centre;
            _root.SetAsLastSibling();

            // Thrown in from one side, a little different each time.
            float angle = (float)(_faces.NextDouble() * Mathf.PI * 2f);
            var throwFrom = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ThrowDistance;

            Setup(_dice[0], first, new Vector2(-RestOffset, 0f), throwFrom + new Vector2(-24f, 10f));
            Setup(_dice[1], second, new Vector2(RestOffset, 0f), throwFrom + new Vector2(24f, -10f));

            _glow.color = UiTheme.WithAlpha(BoardLayout.ColourOf(seat), 0.5f);

            bool doubles = first == second;
            _callout.text = doubles
                ? rollAgain ? "DOUBLES  ·  <size=70%>roll again</size>" : "DOUBLES"
                : "";
            _callout.gameObject.SetActive(false);
            _hold = doubles ? DoublesHoldSeconds : HoldSeconds;

            _group.alpha = 1f;
            _root.gameObject.SetActive(true);

            _phase = Phase.Tumble;
            _time = 0f;
            _sinceFace = 0f;
        }

        /// <summary>Hides the dice at once, wherever they are.</summary>
        public void Skip()
        {
            _phase = Phase.Idle;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private void Setup(Die die, int face, Vector2 rest, Vector2 from)
        {
            die.Face = face;
            die.Rest = rest;
            die.From = from;
            die.Spin = (_faces.Next(2) == 0 ? -1f : 1f) * SpinTurns * 360f;
            die.Root.anchoredPosition = from;
            die.Root.localScale = Vector3.one;
            ShowFace(die, RandomFace(0));
        }

        private int RandomFace(int avoid)
        {
            // Six-sided pips; the tumble never repeats a face twice in a row.
            int face = _faces.Next(1, 7);
            if (face == avoid) face = face % 6 + 1;
            return face;
        }

        private void Update()
        {
            if (_phase == Phase.Idle) return;

            float delta = Time.deltaTime * Mathf.Max(0f, Speed);
            _time += delta;

            switch (_phase)
            {
                case Phase.Tumble: Tumble(delta); break;
                case Phase.Hold: HoldStill(); break;
                case Phase.Fly: Fly(); break;
            }
        }

        private void Tumble(float delta)
        {
            float t = Mathf.Clamp01(_time / TumbleSeconds);

            // Fast at first, easing into the landing.
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);

            // The face changes often at first and less as the die slows.
            _sinceFace += delta;
            float interval = Mathf.Lerp(FaceChangeSeconds, FaceChangeSeconds * 4f, t);

            foreach (var die in _dice)
            {
                var along = Vector2.Lerp(die.From, die.Rest, eased);

                // Two shrinking bounces on the way in.
                float bounce = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * (1f - t) * 26f;
                die.Root.anchoredPosition = along + new Vector2(0f, bounce);
                die.Root.localRotation = Quaternion.Euler(0f, 0f, die.Spin * (1f - eased));
                die.Root.localScale = Vector3.one * (1f + 0.12f * (1f - t));

                if (_sinceFace >= interval) ShowFace(die, RandomFace(die.Shown));
            }

            if (_sinceFace >= interval) _sinceFace = 0f;

            if (t < 1f) return;

            foreach (var die in _dice)
            {
                die.Root.anchoredPosition = die.Rest;
                die.Root.localRotation = Quaternion.identity;
                die.Root.localScale = Vector3.one;
                ShowFace(die, die.Face);
            }

            _callout.gameObject.SetActive(_callout.text.Length > 0);

            _phase = Phase.Hold;
            _time = 0f;
        }

        private void HoldStill()
        {
            // A small settle pop as the faces land.
            float pop = Mathf.Clamp01(_time / 0.12f);
            float scale = 1f + 0.1f * Mathf.Sin(pop * Mathf.PI);
            foreach (var die in _dice) die.Root.localScale = Vector3.one * scale;

            if (_time < _hold) return;

            _flyTo = FlyTarget();
            _callout.gameObject.SetActive(false);
            _phase = Phase.Fly;
            _time = 0f;
        }

        private void Fly()
        {
            float t = Mathf.Clamp01(_time / FlySeconds);
            float eased = t * t;

            foreach (var die in _dice)
            {
                // Each die heads for its own side of the tray's row.
                var to = _flyTo + new Vector2(die.Rest.x * 0.55f, 0f);
                die.Root.anchoredPosition = Vector2.Lerp(die.Rest, to, eased);
                die.Root.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, eased);
            }

            _glow.color = UiTheme.WithAlpha(_glow.color, 0.5f * (1f - t));
            _group.alpha = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);

            if (t < 1f) return;

            _phase = Phase.Idle;
            _root.gameObject.SetActive(false);
        }

        /// <summary>The tray's dice row, in the dice root's space; straight down if the tray is not there.</summary>
        private Vector2 FlyTarget()
        {
            var fallback = new Vector2(-_root.anchoredPosition.x - 400f, -_root.anchoredPosition.y - 300f);

            var target = _flyTarget?.Invoke();
            if (target == null) return fallback;

            var world = target.TransformPoint(target.rect.center);
            var local = (Vector2)_layer.InverseTransformPoint(world);
            return local - _layer.rect.center - _root.anchoredPosition;
        }

        /// <summary>A world position as a point on the layer, measured from its centre.</summary>
        private Vector2 CanvasPoint(Vector3 world)
        {
            var camera = Camera.main;
            if (camera == null) return Vector2.zero;

            var screen = RectTransformUtility.WorldToScreenPoint(camera, world);

            // Overlay canvas: no camera for the screen-to-rect step.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, null, out var local);
            return local - _layer.rect.center;
        }

        // ── Faces ────────────────────────────────────────────────────────

        /// <summary>Pip positions on a 3×3 grid, per face. -1, 0, 1 on each axis.</summary>
        private static readonly Vector2[][] PipLayout =
        {
            new Vector2[0],
            new[] { new Vector2(0, 0) },
            new[] { new Vector2(-1, 1), new Vector2(1, -1) },
            new[] { new Vector2(-1, 1), new Vector2(0, 0), new Vector2(1, -1) },
            new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1) },
            new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(-1, -1), new Vector2(1, -1) },
            new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, 0), new Vector2(1, 0), new Vector2(-1, -1), new Vector2(1, -1) },
        };

        private const float PipSpacing = 24f;
        private const float PipSize = 17f;

        private static void ShowFace(Die die, int face)
        {
            die.Shown = face;

            // A face beyond six (a config with bigger dice) shows as a digit.
            bool pips = face >= 1 && face < PipLayout.Length;
            die.Digit.gameObject.SetActive(!pips);
            if (!pips) die.Digit.text = face.ToString();

            var layout = pips ? PipLayout[face] : PipLayout[0];

            for (int i = 0; i < die.Pips.Length; i++)
            {
                bool on = i < layout.Length;
                die.Pips[i].enabled = on;
                if (on) die.Pips[i].rectTransform.anchoredPosition = layout[i] * PipSpacing;
            }
        }

        // ── Build ────────────────────────────────────────────────────────

        private void Build(RectTransform layer)
        {
            _layer = layer;

            _root = UiKit.Rect("dice_roller", layer);
            _root.anchorMin = new Vector2(0.5f, 0.5f);
            _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(DieSize * 2f + RestOffset * 2f, DieSize * 2f);

            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // A dark pool lifts the dice off the board; the seat glow sits in it.
            var shade = UiKit.Rect("shade", _root);
            shade.sizeDelta = new Vector2(440f, 300f);
            var shadeImage = UiKit.Fill(shade, UiTheme.WithAlpha(Color.black, 0.6f));
            shadeImage.sprite = DecoSprites.Glow;

            var glow = UiKit.Rect("glow", _root);
            glow.sizeDelta = new Vector2(360f, 220f);
            _glow = UiKit.Fill(glow, UiTheme.WithAlpha(UiTheme.GoldBright, 0.5f));
            _glow.sprite = DecoSprites.Glow;

            _dice = new[] { BuildDie("die_a"), BuildDie("die_b") };

            var callout = UiKit.Rect("callout", _root);
            callout.anchoredPosition = new Vector2(0f, -DieSize * 0.5f - 34f);
            callout.sizeDelta = new Vector2(420f, 40f);
            _callout = UiKit.Caption(callout, "", UiTheme.FontTitle, UiTheme.GoldBright, TextAlignmentOptions.Center);
            _callout.fontStyle = FontStyles.Bold;
            _callout.characterSpacing = UiTheme.HeadingSpacing;
            _callout.overflowMode = TextOverflowModes.Overflow;

            // The words breathe; a framed pulse would read as a button.
            var pulse = callout.gameObject.AddComponent<UiPulse>();
            pulse.Targets = new Graphic[] { _callout };
            pulse.Min = 0.55f;

            _root.gameObject.SetActive(false);
        }

        private Die BuildDie(string dieName)
        {
            var root = UiKit.Rect(dieName, _root);
            root.sizeDelta = new Vector2(DieSize, DieSize);

            // A drop shadow, then the ivory face with a brass edge, as in the tray.
            var shadow = UiKit.Rect("shadow", root);
            UiKit.Stretch(shadow);
            shadow.anchoredPosition = new Vector2(5f, -7f);
            UiKit.Sliced(shadow, DecoSprites.ButtonFill, UiTheme.WithAlpha(Color.black, 0.55f));

            var face = UiKit.Rect("face", root);
            UiKit.Stretch(face);
            UiKit.Sliced(face, DecoSprites.ButtonFill, UiTheme.DieFace);
            UiKit.Overlay(face, DecoSprites.ButtonEdgeDouble, UiTheme.Gold);

            var pips = new Image[6];
            for (int i = 0; i < pips.Length; i++)
            {
                var pip = UiKit.Rect("pip", face);
                pip.sizeDelta = new Vector2(PipSize, PipSize);
                pips[i] = UiKit.Fill(pip, UiTheme.DieInk);
                pips[i].sprite = Primitives.Disc;
            }

            var digit = UiKit.Caption(face, "", 48f, UiTheme.DieInk, TextAlignmentOptions.Center);
            digit.fontStyle = FontStyles.Bold;

            return new Die { Root = root, Pips = pips, Digit = digit };
        }
    }
}