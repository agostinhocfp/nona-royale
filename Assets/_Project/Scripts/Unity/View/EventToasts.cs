// Assets/_Project/Scripts/Unity/View/EventToasts.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Short-lived lines at the top of the board for the actions that matter:
    /// casts, hits, knockouts, upkeep damage, refusals (GUI increment F2).
    /// </summary>
    /// <remarks>
    /// <b>The history strip is where a player looks back; this is what they
    /// see without looking.</b> Upkeep damage is the case it exists for: it
    /// lands when nobody acted (PRESENTATION §2), so a line saying so has to
    /// come to the player. A refused command gets a line too, since that is
    /// how a player learns a rule the screen did not teach.
    ///
    /// At most <see cref="MaxToasts"/> at once, oldest dropped first. Each
    /// fades on its own clock. Nothing here catches the pointer.
    /// </remarks>
    public sealed class EventToasts : MonoBehaviour
    {
        private const int MaxToasts = 3;
        private const float Life = 3.6f;
        private const float FadeTime = 0.6f;

        private static readonly Color RejectColour = new Color(0.95f, 0.45f, 0.35f);

        private sealed class Toast
        {
            public GameObject Root;
            public CanvasGroup Group;
            public float Age;
        }

        private readonly List<Toast> _toasts = new List<Toast>();
        private RectTransform _root;

        public void Bind(RectTransform canvasRect)
        {
            if (_root == null)
            {
                _root = UiKit.Rect("toasts", canvasRect);
                _root.anchorMin = new Vector2(0f, 1f);
                _root.anchorMax = new Vector2(1f, 1f);
                _root.pivot = new Vector2(0.5f, 1f);

                var column = UiKit.Column(_root, 4f);
                column.childAlignment = TextAnchor.UpperCenter;
                column.childForceExpandWidth = false;

                _root.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            Clear();
        }

        /// <summary>Keeps the toasts inside the free board area, just under the top bar.</summary>
        public void SetArea(float left, float right, float top)
        {
            if (_root == null) return;

            _root.offsetMin = new Vector2(left, _root.offsetMin.y);
            _root.offsetMax = new Vector2(-right, _root.offsetMax.y);
            _root.anchoredPosition = new Vector2(_root.anchoredPosition.x, -top - 10f);
        }

        public void Clear()
        {
            foreach (var toast in _toasts)
                if (toast.Root != null) Destroy(toast.Root);

            _toasts.Clear();
        }

        /// <summary>Toasts for a batch's loud items and its refusals.</summary>
        public void Show(HistoryBatch batch)
        {
            if (_root == null || batch == null) return;

            foreach (var reason in batch.Rejections)
                Push($"Can't do that: {reason}", RejectColour);

            foreach (var item in batch.Items)
            {
                if (!item.Toast) continue;

                var seat = BoardLayout.ColourOf(item.Seat);
                string value = string.IsNullOrEmpty(item.Value)
                    ? ""
                    : $"   <color=#{UiKit.Hex(item.ValueColour)}><b>{item.Value}</b></color>";
                string knockout = item.Knockout ? $"   <color=#{UiKit.Hex(UiKit.Danger)}><b>KO</b></color>" : "";

                Push($"<color=#{UiKit.Hex(UiKit.Readable(seat))}>{item.Seat}</color>  {item.Title}{value}{knockout}", seat);
            }
        }

        private void Push(string text, Color accent)
        {
            var row = UiKit.Rect("toast", _root);
            UiKit.Fill(row, new Color(0.02f, 0.015f, 0.02f, 0.88f));

            var layout = UiKit.Row(row, 10f);
            layout.padding = new RectOffset(0, 14, 5, 5);
            layout.childForceExpandHeight = true;

            var bar = UiKit.Rect("accent", row);
            UiKit.Fill(bar, accent);
            UiKit.Fixed(bar, 4f);

            UiKit.Label(row, text, UiKit.FontBody).overflowMode = TextOverflowModes.Overflow;

            var group = row.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            _toasts.Add(new Toast { Root = row.gameObject, Group = group });

            while (_toasts.Count > MaxToasts)
            {
                Destroy(_toasts[0].Root);
                _toasts.RemoveAt(0);
            }
        }

        private void Update()
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var toast = _toasts[i];
                toast.Age += Time.unscaledDeltaTime;

                if (toast.Age >= Life)
                {
                    Destroy(toast.Root);
                    _toasts.RemoveAt(i);
                    continue;
                }

                toast.Group.alpha = Mathf.Clamp01((Life - toast.Age) / FadeTime);
            }
        }
    }
}
