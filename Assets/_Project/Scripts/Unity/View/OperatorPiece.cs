// Assets/_Project/Scripts/Unity/View/OperatorPiece.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// One operator on screen: silhouette, health bar, and the walk between
    /// cells. Holds no game state — it is told what is true and shows it.
    /// </summary>
    /// <remarks>
    /// Status badges used to live here, as world-space squares. They moved to
    /// <see cref="PieceHudLayer"/> as named screen-space tags (ADR-0008
    /// increment C): at board scale a world-space badge was a few pixels
    /// wide, and a word does not fit in that.
    ///
    /// <b>It walks the track rather than sliding to the destination.</b> A
    /// straight-line slide was fine on the old ring layout; on the cross it cuts
    /// diagonally through the board interior and reads as a teleport. Following
    /// the cells is also the only way a player can count a move and check it.
    /// </remarks>
    public sealed class OperatorPiece : MonoBehaviour
    {
        private const float CellsPerSecond = 11f;
        private const float SettleSpeed = 12f;

        private readonly Queue<Vector3> _path = new Queue<Vector3>();

        private SpriteRenderer _body;
        private SpriteRenderer _healthFill;
        private Color _seatColour;
        private Vector3 _target;
        private float _stepDistance = 1f;
        private float _flash;

        public OperatorState Operator { get; private set; }

        /// <summary>True while the piece is still travelling, so the view can wait before re-posing it.</summary>
        public bool IsWalking => _path.Count > 0;

        public void Bind(OperatorState op, float cellSize, float cellSpacing)
        {
            Operator = op;
            name = $"{op.Owner}_{op.Name}";

            _seatColour = BoardLayout.ColourOf(op.Owner);
            _stepDistance = cellSpacing;

            var shape = PieceShape.For(op);

            _body = gameObject.AddComponent<SpriteRenderer>();
            _body.sprite = shape;
            _body.color = _seatColour;
            _body.sortingOrder = 4;

            // The same silhouette, larger and dark, behind: an outline that
            // works for any shape without a second sprite per shape.
            var outline = Child("outline", 1.28f, Vector3.zero);
            outline.sprite = shape;
            outline.color = new Color(0.04f, 0.04f, 0.06f);
            outline.sortingOrder = 3;

            BuildHealthBar();

            transform.localScale = Vector3.one * cellSize * PieceShape.SizeFor(op);
        }

        /// <summary>Places the piece with no animation. For the opening layout.</summary>
        public void Place(Vector3 position)
        {
            _path.Clear();
            _target = position;
            transform.position = position;
        }

        /// <summary>Walks a sequence of cells, ending at the last.</summary>
        public void Walk(IReadOnlyList<Vector3> waypoints)
        {
            _path.Clear();

            foreach (var point in waypoints) _path.Enqueue(point);
            if (waypoints.Count > 0) _target = waypoints[waypoints.Count - 1];
        }

        /// <summary>Sets the destination without a path — placement, not movement.</summary>
        public void Settle(Vector3 position)
        {
            if (IsWalking) return;
            _target = position;
        }

        /// <summary>
        /// A brief white-out on the silhouette. The floater says how much; this
        /// says <i>who</i>, which a number rising off a crowded cell does not.
        /// </summary>
        public void Flash() => _flash = 1f;

        public void Refresh()
        {
            if (Operator == null || _body == null) return;

            float health = Mathf.Clamp01((float)Operator.Health / Operator.MaxHealth);

            var tint = Operator.IsInYard
                ? Color.Lerp(_seatColour, new Color(0.4f, 0.4f, 0.42f), 0.55f)
                : _seatColour;

            _body.color = Color.Lerp(tint, Color.white, _flash);

            if (_healthFill != null)
            {
                // Anchored left so the bar drains rightward rather than shrinking
                // toward its centre, which reads as distance rather than loss.
                _healthFill.transform.localScale = new Vector3(1.1f * health, 0.13f, 1f);
                _healthFill.transform.localPosition = new Vector3(-0.55f * (1f - health), 0.78f, 0f);
                _healthFill.color = Color.Lerp(new Color(0.80f, 0.25f, 0.25f), tint, health);
            }
        }

        private void BuildHealthBar()
        {
            var back = Child("health_back", 1f, new Vector3(0f, 0.78f, 0f));
            back.sprite = Primitives.Square;
            back.color = new Color(0.06f, 0.06f, 0.08f, 0.85f);
            back.sortingOrder = 5;
            back.transform.localScale = new Vector3(1.2f, 0.2f, 1f);

            _healthFill = Child("health_fill", 1f, new Vector3(0f, 0.78f, 0f));
            _healthFill.sprite = Primitives.Square;
            _healthFill.sortingOrder = 6;
        }

        private SpriteRenderer Child(string childName, float scale, Vector3 localPosition)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * scale;

            return go.AddComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - Time.deltaTime * 4f);
                if (_body != null) _body.color = Color.Lerp(_body.color, Color.white, _flash * 0.5f);
            }

            if (_path.Count > 0)
            {
                var next = _path.Peek();
                float step = CellsPerSecond * _stepDistance * Time.deltaTime;

                transform.position = Vector3.MoveTowards(transform.position, next, step);

                if (Vector3.Distance(transform.position, next) < 0.01f) _path.Dequeue();
                return;
            }

            transform.position = Vector3.Lerp(transform.position, _target, Time.deltaTime * SettleSpeed);
        }
    }
}