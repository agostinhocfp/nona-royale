// Assets/_Project/Scripts/Unity/View/TurnBanner.cs
using NonaRoyale.Core.Board;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// A slim pill at the top of the board announcing whose turn began
    /// (GUI increment F2, made subtle on feedback).
    /// </summary>
    /// <remarks>
    /// <b>It started as a centred card with its own Roll button</b>, and the
    /// designer found it too big. The job it keeps is the hand-over cue: the
    /// seat's colour and name, the round, and what to press. The Roll button
    /// moved to <see cref="TurnButton"/>, and upkeep effects go to the toasts,
    /// which already call them out.
    ///
    /// It stays up until the seat rolls, then fades. Nothing here catches the
    /// pointer.
    /// </remarks>
    public sealed class TurnBanner : MonoBehaviour
    {
        private const float FadeSeconds = 0.5f;

        public bool IsShown => _root != null && _root.gameObject.activeSelf && !_fading;

        private RectTransform _area;
        private RectTransform _root;
        private CanvasGroup _group;
        private Image _dot;
        private TMP_Text _text;
        private bool _fading;
        private float _fade;

        public void Bind(RectTransform canvasRect)
        {
            if (_area == null) Build(canvasRect);
            HideNow();
        }

        /// <summary>Keeps the pill centred over the free board area, just under the top bar.</summary>
        public void SetArea(float left, float right, float top)
        {
            if (_area == null) return;

            _area.offsetMin = new Vector2(left, 0f);
            _area.offsetMax = new Vector2(-right, -top - 8f);
        }

        public void Show(PlayerColor seat, int round)
        {
            if (_root == null) return;

            var colour = BoardLayout.ColourOf(seat);
            _dot.color = colour;
            _text.text =
                $"<color=#{UiTheme.Hex(UiTheme.Readable(colour))}><b>{seat.ToString().ToUpperInvariant()}</b></color>'s turn" +
                $"   <color=#{UiTheme.Hex(UiTheme.TextDim)}>round {round}  ·  <b>Space</b> to roll</color>";

            _fading = false;
            _group.alpha = 1f;
            _root.gameObject.SetActive(true);
        }

        /// <summary>Fades the pill out.</summary>
        public void Hide()
        {
            if (_root == null || !_root.gameObject.activeSelf || _fading) return;

            _fading = true;
            _fade = FadeSeconds;
        }

        private void HideNow()
        {
            _fading = false;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        private void Build(RectTransform canvasRect)
        {
            _area = UiKit.Rect("turn_banner_area", canvasRect);
            _area.anchorMin = Vector2.zero;
            _area.anchorMax = Vector2.one;

            _root = UiKit.Rect("turn_banner", _area);
            _root.anchorMin = new Vector2(0.5f, 1f);
            _root.anchorMax = new Vector2(0.5f, 1f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = Vector2.zero;
            UiKit.Sliced(_root, DecoSprites.ChipFill, UiTheme.Scrim);
            UiKit.Overlay(_root, DecoSprites.ButtonEdge, UiTheme.Line);

            var row = UiKit.Row(_root, 10f);
            row.padding = new RectOffset(14, 16, 6, 6);
            row.childAlignment = TextAnchor.MiddleCenter;
            var fit = _root.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _dot = UiKit.Diamond(_root, UiTheme.SeatNone, 11f, 17f);

            _text = UiKit.Label(_root, "", UiTheme.FontBody);
            _text.overflowMode = TextOverflowModes.Overflow;

            _group = _root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private void Update()
        {
            if (!_fading) return;

            _fade -= Time.unscaledDeltaTime;
            _group.alpha = Mathf.Clamp01(_fade / FadeSeconds);

            if (_fade <= 0f) HideNow();
        }
    }
}
