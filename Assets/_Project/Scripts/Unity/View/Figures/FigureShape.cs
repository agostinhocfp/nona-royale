// Assets/_Project/Scripts/Unity/View/Figures/FigureShape.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>An axis-aligned box in figure units. Infinite for half-planes.</summary>
    public readonly struct FigureBounds
    {
        public readonly float MinX, MinY, MaxX, MaxY;

        public static readonly FigureBounds Infinite =
            new FigureBounds(float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity);

        public static readonly FigureBounds Empty =
            new FigureBounds(float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity);

        public FigureBounds(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public bool IsEmpty => MinX > MaxX || MinY > MaxY;
        public bool IsInfinite => float.IsInfinity(MinX) || float.IsInfinity(MinY) || float.IsInfinity(MaxX) || float.IsInfinity(MaxY);

        public FigureBounds Expand(float by) => IsEmpty ? this : new FigureBounds(MinX - by, MinY - by, MaxX + by, MaxY + by);

        public FigureBounds Union(FigureBounds other) => new FigureBounds(
            Math.Min(MinX, other.MinX), Math.Min(MinY, other.MinY),
            Math.Max(MaxX, other.MaxX), Math.Max(MaxY, other.MaxY));

        public FigureBounds Intersect(FigureBounds other) => new FigureBounds(
            Math.Max(MinX, other.MinX), Math.Max(MinY, other.MinY),
            Math.Min(MaxX, other.MaxX), Math.Min(MaxY, other.MaxY));

        public bool Contains(float x, float y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;

        /// <summary>
        /// A lower bound on the distance from a point to anything inside the
        /// box: the distance to the box outside it, minus infinity inside.
        /// </summary>
        public float LowerBound(float x, float y)
        {
            if (IsEmpty) return float.PositiveInfinity;

            float dx = Math.Max(Math.Max(MinX - x, x - MaxX), 0f);
            float dy = Math.Max(Math.Max(MinY - y, y - MaxY), 0f);
            if (dx == 0f && dy == 0f) return float.NegativeInfinity;

            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }

    /// <summary>
    /// A flat shape for a procedural figure, as a signed distance: negative
    /// inside, positive outside, in figure units (OPERATOR_LOOKBOOK.md, LB0).
    /// </summary>
    /// <remarks>
    /// <b>Plain C#, no Unity types</b>, like <see cref="FigureLayout"/>, so it
    /// is tested and previewed outside the editor.
    ///
    /// <b>Why distances and not a polygon scan.</b> A distance gives analytic
    /// anti-aliasing for free (the same <c>0.5 − d</c> rule as
    /// <c>DecoSprites.Coverage</c>), and the one-weight ink line of
    /// ART_DIRECTION §2.2 rule 2 is just the silhouette offset outward. The
    /// Boolean operators below are exact for union and a close bound for
    /// intersection and subtraction, which is all a 4× supersampled flat
    /// fill needs.
    ///
    /// <b>Figure space.</b> x runs across the figure with 0 on the centre
    /// line; y runs up from the feet at 0. Recipes are authored in that space
    /// and the rasteriser maps it onto a canvas.
    /// </remarks>
    public abstract class FigureShape
    {
        /// <summary>A box that contains every point with distance ≤ 0.</summary>
        public abstract FigureBounds Bounds { get; }

        /// <summary>Signed distance: negative inside, positive outside.</summary>
        public abstract float Distance(float x, float y);

        // ── Primitives ──────────────────────────────────────────────────

        /// <summary>A closed polygon from (x, y) pairs. Either winding; self-overlap is even-odd.</summary>
        public static FigureShape Polygon(params float[] xy) => new PolygonShape(xy);

        /// <summary>A circle.</summary>
        public static FigureShape Circle(float cx, float cy, float r) => new CircleShape(cx, cy, r);

        /// <summary>An ellipse. The distance is approximate away from the edge, exact enough on it.</summary>
        public static FigureShape Ellipse(float cx, float cy, float rx, float ry) => new EllipseShape(cx, cy, rx, ry);

        /// <summary>An axis-aligned rectangle from its corners.</summary>
        public static FigureShape Rect(float x0, float y0, float x1, float y1) =>
            new BoxShape((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, Math.Abs(x1 - x0) * 0.5f, Math.Abs(y1 - y0) * 0.5f);

        /// <summary>
        /// Everything on one side of the line through (x, y). The normal
        /// (nx, ny) points <i>out</i> of the shape. The way to cut a hard
        /// shadow or highlight edge across a form.
        /// </summary>
        public static FigureShape HalfPlane(float x, float y, float nx, float ny) => new HalfPlaneShape(x, y, nx, ny);

        /// <summary>
        /// An open polyline with no inside: its distance is unsigned. Drawn
        /// with an ink line it is a stroke; filled it is nothing.
        /// </summary>
        public static FigureShape Polyline(params float[] xy) => new PolylineShape(xy);

        // ── Combinators ─────────────────────────────────────────────────

        public static FigureShape Union(params FigureShape[] shapes) => new UnionShape(shapes);

        public static FigureShape Intersect(FigureShape a, FigureShape b) => new IntersectShape(a, b);

        public static FigureShape Subtract(FigureShape a, FigureShape b) => new SubtractShape(a, b);

        public FigureShape Or(FigureShape other) => Union(this, other);
        public FigureShape And(FigureShape other) => Intersect(this, other);
        public FigureShape Minus(FigureShape other) => Subtract(this, other);

        /// <summary>Grown outward by <paramref name="by"/> (shrunk if negative).</summary>
        public FigureShape Offset(float by) => new OffsetShape(this, by);

        /// <summary>This shape reflected across the centre line.</summary>
        public FigureShape Mirrored() => new MirrorShape(this);

        /// <summary>
        /// This shape and its reflection. Deco is symmetric: author the
        /// figure's left half and let this draw the right.
        /// </summary>
        public FigureShape Symmetric() => Union(this, Mirrored());

        /// <summary>Moved by (dx, dy).</summary>
        public FigureShape Translate(float dx, float dy) => new TransformShape(this, dx, dy, 1f, 0f, 0f, 0f);

        /// <summary>Scaled uniformly about (px, py).</summary>
        public FigureShape Scale(float factor, float px = 0f, float py = 0f) => new TransformShape(this, 0f, 0f, factor, 0f, px, py);

        /// <summary>Rotated counter-clockwise by <paramref name="degrees"/> about (px, py).</summary>
        public FigureShape Rotate(float degrees, float px = 0f, float py = 0f) => new TransformShape(this, 0f, 0f, 1f, degrees, px, py);

        // ── Implementations ─────────────────────────────────────────────

        private sealed class PolygonShape : FigureShape
        {
            private readonly float[] _x;
            private readonly float[] _y;
            private readonly FigureBounds _bounds;

            public PolygonShape(float[] xy)
            {
                if (xy == null || xy.Length < 6 || xy.Length % 2 != 0)
                    throw new ArgumentException("A polygon needs at least three (x, y) pairs.", nameof(xy));

                int n = xy.Length / 2;
                _x = new float[n];
                _y = new float[n];
                var bounds = FigureBounds.Empty;

                for (int i = 0; i < n; i++)
                {
                    _x[i] = xy[2 * i];
                    _y[i] = xy[2 * i + 1];
                    bounds = bounds.Union(new FigureBounds(_x[i], _y[i], _x[i], _y[i]));
                }

                _bounds = bounds;
            }

            public override FigureBounds Bounds => _bounds;

            // Inigo Quilez's exact polygon distance: nearest edge for the
            // magnitude, crossings for the sign.
            public override float Distance(float px, float py)
            {
                int n = _x.Length;
                float dx0 = px - _x[0], dy0 = py - _y[0];
                float best = dx0 * dx0 + dy0 * dy0;
                bool inside = false;

                for (int i = 0, j = n - 1; i < n; j = i, i++)
                {
                    float ex = _x[j] - _x[i], ey = _y[j] - _y[i];
                    float wx = px - _x[i], wy = py - _y[i];
                    float t = (wx * ex + wy * ey) / (ex * ex + ey * ey);
                    if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                    float bx = wx - ex * t, by = wy - ey * t;
                    float d = bx * bx + by * by;
                    if (d < best) best = d;

                    bool c1 = py >= _y[i];
                    bool c2 = py < _y[j];
                    bool c3 = ex * wy > ey * wx;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) inside = !inside;
                }

                float distance = (float)Math.Sqrt(best);
                return inside ? -distance : distance;
            }
        }

        private sealed class PolylineShape : FigureShape
        {
            private readonly float[] _x;
            private readonly float[] _y;
            private readonly FigureBounds _bounds;

            public PolylineShape(float[] xy)
            {
                if (xy == null || xy.Length < 4 || xy.Length % 2 != 0)
                    throw new ArgumentException("A polyline needs at least two (x, y) pairs.", nameof(xy));

                int n = xy.Length / 2;
                _x = new float[n];
                _y = new float[n];
                var bounds = FigureBounds.Empty;

                for (int i = 0; i < n; i++)
                {
                    _x[i] = xy[2 * i];
                    _y[i] = xy[2 * i + 1];
                    bounds = bounds.Union(new FigureBounds(_x[i], _y[i], _x[i], _y[i]));
                }

                _bounds = bounds;
            }

            // Zero-width: nothing is inside, so the box is the line itself.
            public override FigureBounds Bounds => _bounds;

            public override float Distance(float px, float py)
            {
                float best = float.PositiveInfinity;

                for (int i = 1; i < _x.Length; i++)
                {
                    float ex = _x[i] - _x[i - 1], ey = _y[i] - _y[i - 1];
                    float wx = px - _x[i - 1], wy = py - _y[i - 1];
                    float len = ex * ex + ey * ey;
                    float t = len > 0f ? (wx * ex + wy * ey) / len : 0f;
                    if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                    float bx = wx - ex * t, by = wy - ey * t;
                    float d = bx * bx + by * by;
                    if (d < best) best = d;
                }

                return (float)Math.Sqrt(best);
            }
        }

        private sealed class CircleShape : FigureShape
        {
            private readonly float _cx, _cy, _r;

            public CircleShape(float cx, float cy, float r)
            {
                _cx = cx;
                _cy = cy;
                _r = r;
            }

            public override FigureBounds Bounds => new FigureBounds(_cx - _r, _cy - _r, _cx + _r, _cy + _r);

            public override float Distance(float x, float y)
            {
                float dx = x - _cx, dy = y - _cy;
                return (float)Math.Sqrt(dx * dx + dy * dy) - _r;
            }
        }

        private sealed class EllipseShape : FigureShape
        {
            private readonly float _cx, _cy, _rx, _ry;

            public EllipseShape(float cx, float cy, float rx, float ry)
            {
                _cx = cx;
                _cy = cy;
                _rx = Math.Abs(rx);
                _ry = Math.Abs(ry);
            }

            public override FigureBounds Bounds => new FigureBounds(_cx - _rx, _cy - _ry, _cx + _rx, _cy + _ry);

            // First-order distance: the implicit value over its gradient.
            // Exact on the edge, which is the only place coverage reads it.
            public override float Distance(float x, float y)
            {
                float nx = (x - _cx) / _rx, ny = (y - _cy) / _ry;
                float k0 = (float)Math.Sqrt(nx * nx + ny * ny);
                if (k0 < 1e-6f) return -Math.Min(_rx, _ry);

                float gx = nx / _rx, gy = ny / _ry;
                float k1 = (float)Math.Sqrt(gx * gx + gy * gy);
                return k0 * (k0 - 1f) / k1;
            }
        }

        private sealed class BoxShape : FigureShape
        {
            private readonly float _cx, _cy, _hx, _hy;

            public BoxShape(float cx, float cy, float hx, float hy)
            {
                _cx = cx;
                _cy = cy;
                _hx = hx;
                _hy = hy;
            }

            public override FigureBounds Bounds => new FigureBounds(_cx - _hx, _cy - _hy, _cx + _hx, _cy + _hy);

            public override float Distance(float x, float y)
            {
                float qx = Math.Abs(x - _cx) - _hx;
                float qy = Math.Abs(y - _cy) - _hy;
                float ox = Math.Max(qx, 0f), oy = Math.Max(qy, 0f);
                return (float)Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0f);
            }
        }

        private sealed class HalfPlaneShape : FigureShape
        {
            private readonly float _x, _y, _nx, _ny;

            public HalfPlaneShape(float x, float y, float nx, float ny)
            {
                float length = (float)Math.Sqrt(nx * nx + ny * ny);
                if (length < 1e-6f) throw new ArgumentException("A half-plane needs a non-zero normal.");

                _x = x;
                _y = y;
                _nx = nx / length;
                _ny = ny / length;
            }

            public override FigureBounds Bounds => FigureBounds.Infinite;

            public override float Distance(float x, float y) => (x - _x) * _nx + (y - _y) * _ny;
        }

        private sealed class UnionShape : FigureShape
        {
            private readonly FigureShape[] _shapes;
            private readonly FigureBounds _bounds;

            public UnionShape(FigureShape[] shapes)
            {
                if (shapes == null || shapes.Length == 0) throw new ArgumentException("A union needs a shape.", nameof(shapes));

                // Flatten nested unions so the bounds test below skips whole branches.
                var flat = new List<FigureShape>();
                foreach (var shape in shapes)
                {
                    if (shape is UnionShape union) flat.AddRange(union._shapes);
                    else if (shape != null) flat.Add(shape);
                }

                _shapes = flat.ToArray();
                var bounds = FigureBounds.Empty;
                foreach (var shape in _shapes) bounds = bounds.Union(shape.Bounds);
                _bounds = bounds;
            }

            public override FigureBounds Bounds => _bounds;

            public override float Distance(float x, float y)
            {
                float best = float.PositiveInfinity;

                foreach (var shape in _shapes)
                {
                    // Exact, not an approximation: a child can never be
                    // nearer than its box, so a far box cannot change the min.
                    if (shape.Bounds.LowerBound(x, y) >= best) continue;

                    float d = shape.Distance(x, y);
                    if (d < best) best = d;
                }

                return best;
            }
        }

        private sealed class IntersectShape : FigureShape
        {
            private readonly FigureShape _a, _b;

            public IntersectShape(FigureShape a, FigureShape b)
            {
                _a = a ?? throw new ArgumentNullException(nameof(a));
                _b = b ?? throw new ArgumentNullException(nameof(b));
            }

            public override FigureBounds Bounds => _a.Bounds.Intersect(_b.Bounds);

            public override float Distance(float x, float y) => Math.Max(_a.Distance(x, y), _b.Distance(x, y));
        }

        private sealed class SubtractShape : FigureShape
        {
            private readonly FigureShape _a, _b;

            public SubtractShape(FigureShape a, FigureShape b)
            {
                _a = a ?? throw new ArgumentNullException(nameof(a));
                _b = b ?? throw new ArgumentNullException(nameof(b));
            }

            public override FigureBounds Bounds => _a.Bounds;

            public override float Distance(float x, float y) => Math.Max(_a.Distance(x, y), -_b.Distance(x, y));
        }

        private sealed class OffsetShape : FigureShape
        {
            private readonly FigureShape _shape;
            private readonly float _by;

            public OffsetShape(FigureShape shape, float by)
            {
                _shape = shape ?? throw new ArgumentNullException(nameof(shape));
                _by = by;
            }

            public override FigureBounds Bounds => _by > 0f ? _shape.Bounds.Expand(_by) : _shape.Bounds;

            public override float Distance(float x, float y) => _shape.Distance(x, y) - _by;
        }

        private sealed class MirrorShape : FigureShape
        {
            private readonly FigureShape _shape;

            public MirrorShape(FigureShape shape) => _shape = shape ?? throw new ArgumentNullException(nameof(shape));

            public override FigureBounds Bounds
            {
                get
                {
                    var b = _shape.Bounds;
                    return b.IsEmpty ? b : new FigureBounds(-b.MaxX, b.MinY, -b.MinX, b.MaxY);
                }
            }

            public override float Distance(float x, float y) => _shape.Distance(-x, y);
        }

        /// <summary>Scale and rotate about a pivot, then translate.</summary>
        private sealed class TransformShape : FigureShape
        {
            private readonly FigureShape _shape;
            private readonly float _dx, _dy, _scale, _cos, _sin, _px, _py;
            private readonly FigureBounds _bounds;

            public TransformShape(FigureShape shape, float dx, float dy, float scale, float degrees, float px, float py)
            {
                if (scale <= 0f) throw new ArgumentException("Scale must be positive.", nameof(scale));

                _shape = shape ?? throw new ArgumentNullException(nameof(shape));
                _dx = dx;
                _dy = dy;
                _scale = scale;
                double radians = degrees * Math.PI / 180.0;
                _cos = (float)Math.Cos(radians);
                _sin = (float)Math.Sin(radians);
                _px = px;
                _py = py;
                _bounds = TransformBounds(shape.Bounds);
            }

            public override FigureBounds Bounds => _bounds;

            public override float Distance(float x, float y)
            {
                // Undo the translation, then the rotation and scale about the pivot.
                float lx = x - _dx - _px, ly = y - _dy - _py;
                float rx = (lx * _cos + ly * _sin) / _scale;
                float ry = (-lx * _sin + ly * _cos) / _scale;
                return _shape.Distance(rx + _px, ry + _py) * _scale;
            }

            private FigureBounds TransformBounds(FigureBounds b)
            {
                if (b.IsEmpty || b.IsInfinite) return b;

                var result = FigureBounds.Empty;
                for (int corner = 0; corner < 4; corner++)
                {
                    float x = ((corner & 1) == 0 ? b.MinX : b.MaxX) - _px;
                    float y = ((corner & 2) == 0 ? b.MinY : b.MaxY) - _py;
                    float tx = (x * _cos - y * _sin) * _scale + _px + _dx;
                    float ty = (x * _sin + y * _cos) * _scale + _py + _dy;
                    result = result.Union(new FigureBounds(tx, ty, tx, ty));
                }

                return result;
            }
        }
    }
}
