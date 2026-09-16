// Assets/_Project/Scripts/Unity/View/HoverRelay.cs
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Reports pointer enter and exit on a uGUI element to a callback. The
    /// element needs a graphic that is a raycast target (a button has one).
    /// </summary>
    /// <remarks>
    /// Added for the draft screen's cards (DR2), where hovering a card shows
    /// its ability descriptions. <see cref="HistoryChip"/> predates it and
    /// keeps its own handlers.
    /// </remarks>
    public sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action Entered;
        public Action Exited;

        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();

        public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();

        public static HoverRelay On(Component target, Action entered, Action exited = null)
        {
            // Unity's == null, not ??: a destroyed or missing component can be a "fake null".
            var relay = target.gameObject.GetComponent<HoverRelay>();
            if (relay == null) relay = target.gameObject.AddComponent<HoverRelay>();
            relay.Entered = entered;
            relay.Exited = exited;
            return relay;
        }
    }
}