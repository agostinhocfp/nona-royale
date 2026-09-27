// Assets/_Project/Scripts/Unity/View/CoachCard.cs
using System;
using NonaRoyale.Core.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The first-match tip card (LAUNCH_UI_PASS.md, G10a): one tip at a time,
    /// in the board's lower-left corner above the tray, until the player
    /// dismisses it.
    /// </summary>
    /// <remarks>
    /// <b>Where.</b> Wide, the corner between the squad rail and the board is
    /// empty: the camera fits the board to the height, so the sides are void.
    /// The top centre is the toasts' and the turn banner's; the bottom centre
    /// is the tray's ability peek; the bottom right is the turn button.
    /// Upright there is no void, so the card spans the width just above the
    /// history band and covers the board's bottom rows until dismissed.
    ///
    /// <b>What it holds.</b> A gold TIP heading and the tip's title, the tip
    /// as rules markup (its keywords open the glossary), and two buttons: Got
    /// it, and No more tips, which turns the setting off. Which tip, and
    /// when, is <see cref="Coach"/>'s answer; what has been seen is the
    /// composition root's.
    /// </remarks>
    public sealed class CoachCard : MonoBehaviour
    {
        private const float WideWidth = 360f;
        private const float Gap = 16f;
        private const float UprightGap = 12f;

        private RectTransform _host;
        private RectTransform _card;
        private float _left;
        private float _right;
        private float _bottom;

        /// <summary>The tip on screen, or null.</summary>
        public CoachTip? Shown { get; private set; }

        /// <summary>Got it: the tip is done with.</summary>
        public Action Dismissed;

        /// <summary>No more tips: the player turned them off.</summary>
        public Action Silenced;

        public void Bind(RectTransform host)
        {
            _host = host;
            Hide();
        }

        /// <summary>Keeps the card inside the free board area: clear of the rail, the history strip and the tray.</summary>
        public void SetArea(float left, float right, float bottom)
        {
            _left = left;
            _right = right;
            _bottom = bottom;
            if (_card != null) Place(_card);
        }

        public void Show(CoachTip tip)
        {
            if (_host == null) return;

            Hide();
            Shown = tip;

            _card = UiKit.Rect("coach_tip", _host);
            Place(_card);
            UiKit.Panel(_card, blocksPointer: true);

            var column = UiKit.Column(_card, 8f, 16);
            column.padding.top = 14;
            column.childControlHeight = true;
            column.childForceExpandHeight = false;
            _card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var head = UiKit.Rect("head", _card);
            var headRow = UiKit.Row(head, 10f);
            headRow.childAlignment = TextAnchor.MiddleLeft;
            UiKit.Size(head, height: 22f);
            UiKit.Heading(head, "Tip");
            var title = UiKit.Label(head, Coach.Title(tip), UiTheme.FontBody, UiTheme.GoldBright, bold: true);
            UiKit.Size(title, flexibleWidth: 1f);

            var body = UiKit.Label(_card, RulesMarkup.For(Coach.Line(tip), linked: true),
                UiTheme.FontSmall, UiTheme.Text, wrap: true);
            body.lineSpacing = 4f;
            KeywordLinks.Attach(body, id => GlossaryCard.Show(_host, id));

            var foot = UiKit.Rect("foot", _card);
            var footRow = UiKit.Row(foot, 8f);
            footRow.childAlignment = TextAnchor.MiddleRight;
            UiKit.Size(foot, height: 34f);

            UiKit.Fixed(UiKit.Button(foot, "No more tips", () => Silenced?.Invoke(), size: UiTheme.FontSmall), 132f, 30f);
            UiKit.Size(UiKit.Space(foot), flexibleWidth: 1f);
            UiKit.Fixed(UiKit.Button(foot, "Got it", () => Dismissed?.Invoke(), size: UiTheme.FontSmall,
                selected: true), 96f, 30f);

            UiPopIn.On(_card);
        }

        public void Hide()
        {
            Shown = null;
            if (_card != null) Destroy(_card.gameObject);
            _card = null;
        }

        private void Place(RectTransform card)
        {
            if (ScreenLayout.IsPortrait)
            {
                card.anchorMin = new Vector2(0f, 0f);
                card.anchorMax = new Vector2(1f, 0f);
                card.pivot = new Vector2(0.5f, 0f);
                card.offsetMin = new Vector2(_left + UprightGap, card.offsetMin.y);
                card.offsetMax = new Vector2(-_right - UprightGap, card.offsetMax.y);
                card.anchoredPosition = new Vector2(card.anchoredPosition.x, _bottom + UprightGap);
            }
            else
            {
                card.anchorMin = card.anchorMax = new Vector2(0f, 0f);
                card.pivot = new Vector2(0f, 0f);
                card.sizeDelta = new Vector2(WideWidth, card.sizeDelta.y);
                card.anchoredPosition = new Vector2(_left + Gap, _bottom + Gap);
            }
        }
    }
}
