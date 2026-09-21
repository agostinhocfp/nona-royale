// Assets/_Project/Scripts/Unity/View/KeywordLinks.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Makes the keywords in a rules label tappable (OPERATOR_GUIDE.md §1.4):
    /// a click or tap on a <c>&lt;link&gt;</c> reports its glossary id.
    /// </summary>
    /// <remarks>
    /// <b>A click, never a hover.</b> The same gesture works for a mouse and a
    /// finger (MOBILE.md, M4), and a card that followed the pointer across a
    /// paragraph would flicker over every word it passed.
    ///
    /// <b>It does not fight the scroll.</b> The label becomes a raycast target
    /// so it can be hit at all, but it handles only the click; a drag starting
    /// on it bubbles up to the <c>ScrollRect</c> above, and the event system
    /// withdraws the click from any press that turned into a drag. That is
    /// what lets a swipe that begins on a keyword scroll the dossier instead of
    /// opening a card.
    /// </remarks>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class KeywordLinks : MonoBehaviour, IPointerClickHandler
    {
        private TMP_Text _text;

        public Action<string> Clicked;

        /// <summary>Wires a label. Returns the label for chaining.</summary>
        public static TMP_Text Attach(TMP_Text label, Action<string> clicked)
        {
            if (label == null || clicked == null) return label;

            label.raycastTarget = true;
            var links = label.GetComponent<KeywordLinks>() ?? label.gameObject.AddComponent<KeywordLinks>();
            links.Clicked = clicked;
            return label;
        }

        private void Awake() => _text = GetComponent<TMP_Text>();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_text == null || Clicked == null) return;

            int index = TMP_TextUtilities.FindIntersectingLink(_text, eventData.position, eventData.pressEventCamera);
            if (index < 0 || index >= _text.textInfo.linkCount) return;

            Clicked(_text.textInfo.linkInfo[index].GetLinkID());
        }
    }
}
