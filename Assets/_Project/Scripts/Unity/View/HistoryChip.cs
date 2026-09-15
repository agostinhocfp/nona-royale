// Assets/_Project/Scripts/Unity/View/HistoryChip.cs
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Reports the pointer entering and leaving one history chip, so the strip
    /// can show that chip's card.
    /// </summary>
    public sealed class HistoryChip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action Entered;
        public Action Exited;

        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();

        public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();

        // A chip scrolled off or destroyed under the pointer never gets an exit.
        private void OnDisable() => Exited?.Invoke();
    }
}
