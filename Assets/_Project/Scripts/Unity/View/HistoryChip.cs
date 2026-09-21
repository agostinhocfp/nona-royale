// Assets/_Project/Scripts/Unity/View/HistoryChip.cs
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Reports the pointer entering, leaving and pressing one history chip, so
    /// the strip can show that chip's card.
    /// </summary>
    /// <remarks>
    /// <b>Enter and exit, or a click — never both</b> (MOBILE.md, M4). A mouse
    /// rests on a chip and the card follows it; a finger cannot rest anywhere,
    /// so on a touch screen the strip wires <see cref="Clicked"/> instead and
    /// leaves the hover callbacks null. Touch does raise enter and exit around
    /// a press, which would make the card flash under the finger if both paths
    /// were live at once.
    /// </remarks>
    public sealed class HistoryChip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Action Entered;
        public Action Exited;
        public Action Clicked;

        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();

        public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();

        // A chip scrolled off or destroyed under the pointer never gets an exit.
        private void OnDisable() => Exited?.Invoke();
    }
}
