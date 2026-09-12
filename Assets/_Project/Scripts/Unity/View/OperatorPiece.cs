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
        private Color _seatColour;
        private Vector3 _target;

        public OperatorState Operator { get; private set; }

        public void Bind(OperatorState op, float cellSize)
        {
            Operator = op;
            name = $"{op.Owner}_{op.Name}";

            _seatColour = BoardLayout.ColourOf(op.Owner);
            var shape = PieceShape.For(op);

            _body = gameObject.AddComponent<SpriteRenderer>();
            _body.sprite = shape;
            _body.color = _seatColour;
            _body.sortingOrder = 3;

            // The same shape, larger and dark, behind: an outline that works for
            // any silhouette without a second sprite per shape.
            var outlineGo = new GameObject("outline");
            outlineGo.transform.SetParent(transform, false);
            outlineGo.transform.localScale = Vector3.one * 1.28f;

            _outline = outlineGo.AddComponent<SpriteRenderer>();
            _outline.sprite = shape;
            _outline.color = new Color(0.04f, 0.04f, 0.06f);
            _outline.sortingOrder = 2;

            transform.localScale = Vector3.one * cellSize * PieceShape.SizeFor(op);
            Refresh();
        }

        public void MoveTo(Vector3 position, bool immediate)
        {
            _target = position;
            if (immediate) transform.position = position;
        }

        /// <summary>
        /// Dims a wounded operator and greys out one in the yard, so health and
        /// availability are readable without a bar or a label.
        /// </summary>
        public void Refresh()
        {
            if (Operator == null || _body == null) return;

            float health = Mathf.Clamp01((float)Operator.Health / Operator.MaxHealth);
            var tint = Color.Lerp(_seatColour * 0.3f, _seatColour, 0.3f + 0.7f * health);

            if (Operator.IsInYard) tint = Color.Lerp(tint, new Color(0.4f, 0.4f, 0.42f), 0.55f);

            _body.color = tint;
        }

        private void Update()
        {
            transform.position = Vector3.Lerp(transform.position, _target, Time.deltaTime * SlideSpeed);
        }
    }
}