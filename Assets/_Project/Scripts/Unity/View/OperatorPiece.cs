// // Assets/_Project/Scripts/Unity/View/OperatorPiece.cs
// using NonaRoyale.Core.Model;
// using UnityEngine;

// namespace NonaRoyale.Unity.View
// {
//     /// <summary>
//     /// One operator on screen. Holds no game state — it is told where to be and
//     /// slides there.
//     /// </summary>
//     /// <remarks>
//     /// The piece animates toward its target rather than snapping, purely so a
//     /// human can follow what happened. A move of eight cells is legible when it
//     /// travels; it is a teleport when it snaps, and telling a bounce-back from a
//     /// normal move becomes impossible.
//     /// </remarks>
//     public sealed class OperatorPiece : MonoBehaviour
//     {
//         private const float SlideSpeed = 9f;

//         private SpriteRenderer _body;
//         private SpriteRenderer _outline;
//         private Vector3 _target;

//         public OperatorState Operator { get; private set; }

//         public void Bind(OperatorState op, float size)
//         {
//             Operator = op;
//             name = $"{op.Owner}_{op.Name}";

//             _body = gameObject.AddComponent<SpriteRenderer>();
//             _body.sprite = Primitives.Disc;
//             _body.color = BoardLayout.ColourOf(op.Owner);
//             _body.sortingOrder = 2;

//             var outlineGo = new GameObject("outline");
//             outlineGo.transform.SetParent(transform, false);
//             outlineGo.transform.localScale = Vector3.one * 1.35f;

//             _outline = outlineGo.AddComponent<SpriteRenderer>();
//             _outline.sprite = Primitives.Ring;
//             _outline.color = new Color(0.05f, 0.05f, 0.07f);
//             _outline.sortingOrder = 1;

//             transform.localScale = Vector3.one * size;
//         }

//         public void MoveTo(Vector3 position, bool immediate)
//         {
//             _target = position;
//             if (immediate) transform.position = position;
//         }

//         /// <summary>Dims a wounded operator, so health is readable without a bar.</summary>
//         public void Refresh()
//         {
//             if (Operator == null || _body == null) return;

//             float health = Mathf.Clamp01((float)Operator.Health / Operator.MaxHealth);
//             _body.color = Color.Lerp(
//                 BoardLayout.ColourOf(Operator.Owner) * 0.35f,
//                 BoardLayout.ColourOf(Operator.Owner),
//                 0.35f + 0.65f * health);
//         }

//         private void Update()
//         {
//             transform.position = Vector3.Lerp(transform.position, _target, Time.deltaTime * SlideSpeed);
//         }
//     }
// }

// Assets/_Project/Scripts/Unity/View/OperatorPiece.cs
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// One operator on screen. Holds no game state — it is told where to be and
    /// slides there.
    /// </summary>
    /// <remarks>
    /// The piece animates toward its target rather than snapping, purely so a
    /// human can follow what happened. A move of eight cells is legible when it
    /// travels; it is a teleport when it snaps, and telling a bounce-back from a
    /// normal move becomes impossible.
    /// </remarks>
    public sealed class OperatorPiece : MonoBehaviour
    {
        private const float SlideSpeed = 9f;

        private SpriteRenderer _body;
        private SpriteRenderer _outline;
        private Vector3 _target;

        public OperatorState Operator { get; private set; }

        public void Bind(OperatorState op, float size)
        {
            Operator = op;
            name = $"{op.Owner}_{op.Name}";

            _body = gameObject.AddComponent<SpriteRenderer>();
            _body.sprite = Primitives.Disc;
            _body.color = BoardLayout.ColourOf(op.Owner);
            _body.sortingOrder = 2;

            var outlineGo = new GameObject("outline");
            outlineGo.transform.SetParent(transform, false);
            outlineGo.transform.localScale = Vector3.one * 1.35f;

            _outline = outlineGo.AddComponent<SpriteRenderer>();
            _outline.sprite = Primitives.Ring;
            _outline.color = new Color(0.05f, 0.05f, 0.07f);
            _outline.sortingOrder = 1;

            transform.localScale = Vector3.one * size;
        }

        public void MoveTo(Vector3 position, bool immediate)
        {
            _target = position;
            if (immediate) transform.position = position;
        }

        /// <summary>Dims a wounded operator, so health is readable without a bar.</summary>
        public void Refresh()
        {
            if (Operator == null || _body == null) return;

            float health = Mathf.Clamp01((float)Operator.Health / Operator.MaxHealth);
            _body.color = Color.Lerp(
                BoardLayout.ColourOf(Operator.Owner) * 0.35f,
                BoardLayout.ColourOf(Operator.Owner),
                0.35f + 0.65f * health);
        }

        private void Update()
        {
            transform.position = Vector3.Lerp(transform.position, _target, Time.deltaTime * SlideSpeed);
        }
    }
}