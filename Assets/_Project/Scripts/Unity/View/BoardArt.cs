// Assets/_Project/Scripts/Unity/View/BoardArt.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// The casino floor's sprites, drawn in code: the cross, the felt tables,
    /// the vault, and the operators as seated and standing figures (GUI
    /// increment G2).
    /// </summary>
    /// <remarks>
    /// <b>Shaded, then tinted.</b> The rims, felt, figures and the vault's boss
    /// bake a grey luminance into RGB: light from the top left, a bead across
    /// the rim, a vignette on the felt, a lit side on a figure. The renderer's
    /// colour multiplies it, so one sprite shades every seat. Everything else
    /// is a white alpha mask, like <see cref="DecoSprites"/>.
    ///
    /// <b>One world unit across</b> unless a builder says otherwise, so a
    /// renderer's scale is its size. The cross is built for a given grid and
    /// cached per grid.
    ///
    /// <b>Stand-ins for the art pass.</b> The figures follow ART_DIRECTION §6.1
    /// (operators sit at their table and rise on deploy) until the rendered
    /// character models replace them. The operator's shape stays on the figure
    /// as a pin (<see cref="PieceShape"/>), so identity survives the swap.
    /// </remarks>
    public static class BoardArt
    {
        // ── Tables ──────────────────────────────────────────────────────

        private static Sprite _rim, _felt, _dotted, _arc, _softDisc;

        /// <summary>A table's gilt rim: a bevelled ring, lit from the top left.</summary>
        public static Sprite TableRim => _rim ?? (_rim = BuildRim(256, 0.9f));

        /// <summary>The felt inside the rim: bright at the centre, darkening to the edge.</summary>
        public static Sprite Felt => _felt ?? (_felt = BuildFelt(256, 0.875f));

        /// <summary>A dashed ring just inside the rim.</summary>
        public static Sprite DottedRing => _dotted ?? (_dotted = BuildDotted(256, 0.79f, 64));

        /// <summary>A thin arc over the table's upper right quarter, the dealer's line.</summary>
        public static Sprite TableArc => _arc ?? (_arc = BuildArc(256, 0.5f, -8f, 98f));

        /// <summary>A disc with a soft edge, for drop shadows.</summary>
        public static Sprite SoftDisc => _softDisc ?? (_softDisc = BuildSoftDisc(128, 0.35f));

        // ── Figures ─────────────────────────────────────────────────────

        private static Sprite _bust, _bustOutline, _pawn, _pawnOutline, _halo;

        /// <summary>An operator seated at its table: head and shoulders.</summary>
        public static Sprite Bust => _bust ?? (_bust = BuildFigure(128, BustDistance, false));
        public static Sprite BustOutline => _bustOutline ?? (_bustOutline = BuildFigure(128, BustDistance, true));

        /// <summary>An operator standing on the floor: head, collar, flared body, base.</summary>
        public static Sprite Pawn => _pawn ?? (_pawn = BuildFigure(128, PawnDistance, false));
        public static Sprite PawnOutline => _pawnOutline ?? (_pawnOutline = BuildFigure(128, PawnDistance, true));

        /// <summary>Where the shape pin sits on each figure, in sprite units from the centre.</summary>
        public static readonly Vector2 BustPin = new Vector2(0f, -0.2f);
        public static readonly Vector2 PawnPin = new Vector2(0f, -0.14f);

        /// <summary>The pawn's head centre, in sprite units from the centre.</summary>
        public static readonly Vector2 PawnHead = new Vector2(0f, 0.30f);

        /// <summary>An arc over a selected figure's head.</summary>
        public static Sprite Halo => _halo ?? (_halo = BuildHalo(128));

        // ── Vault ───────────────────────────────────────────────────────

        private static Sprite _vaultPlate, _vaultFrame, _vaultDial, _boss;

        /// <summary>The vault door: a square with its corners scooped out.</summary>
        public static Sprite VaultPlate => _vaultPlate ?? (_vaultPlate = BuildVault(256, VaultPart.Plate));
        public static Sprite VaultFrame => _vaultFrame ?? (_vaultFrame = BuildVault(256, VaultPart.Frame));

        /// <summary>The dial on the door: a ring and a cross.</summary>
        public static Sprite VaultDial => _vaultDial ?? (_vaultDial = BuildVault(256, VaultPart.Dial));

        /// <summary>A polished sphere, for the dial's hub.</summary>
        public static Sprite Boss => _boss ?? (_boss = BuildBoss(64));

        // ── Floor ───────────────────────────────────────────────────────

        private static Sprite _veins, _solid;
        private static readonly Dictionary<int, Sprite[]> Crosses = new Dictionary<int, Sprite[]>();

        /// <summary>Texels per cell in the cross sprites.</summary>
        public const int CrossTexelsPerCell = 40;

        /// <summary>Cells of margin the cross sprites carry around the grid, for the shadow.</summary>
        public const float CrossMargin = 1f;

        /// <summary>A few faint curved lines, the grain of the table.</summary>
        public static Sprite Veins => _veins ?? (_veins = BuildVeins(512, 7));

        /// <summary>A solid white square, bilinear so a thin line stays smooth.</summary>
        public static Sprite Solid => _solid ?? (_solid = DecoSprites.Rasterize(4, 4, (x, y) => 1f, 4f, Vector4.zero));

        /// <summary>
        /// The cross-shaped floor for a grid of <paramref name="gridCells"/>
        /// with arms <paramref name="armWidth"/> cells wide: fill, gilt edge and
        /// shadow, in that order. Each is (grid + 2 x margin) cells across at
        /// <see cref="CrossTexelsPerCell"/>, so its scale is the cell spacing.
        /// </summary>
        public static Sprite[] Cross(int gridCells, int armWidth)
        {
            int key = gridCells * 100 + armWidth;
            if (Crosses.TryGetValue(key, out var sprites)) return sprites;

            sprites = new[]
            {
                BuildCross(gridCells, armWidth, CrossPart.Fill),
                BuildCross(gridCells, armWidth, CrossPart.Edge),
                BuildCross(gridCells, armWidth, CrossPart.Shadow),
            };

            Crosses[key] = sprites;
            return sprites;
        }

        // ── Builders: tables ────────────────────────────────────────────

        private static Sprite BuildRim(int size, float inner)
        {
            float half = size * 0.5f;
            float outer = 1f - 1.5f / half;

            return DecoSprites.RasterizeShaded(size, size, (px, py) =>
            {
                float dx = (px - half) / half;
                float dy = (py - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Coverage((r - outer) * half) * Coverage((inner - r) * half);
                if (alpha <= 0f) return Color.clear;

                // A rounded bead across the ring, and a light from the top left.
                float t = Mathf.Clamp01((r - inner) / (outer - inner));
                float bead = 0.62f + 0.38f * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.7f);

                float nx = dx / Mathf.Max(r, 0.0001f);
                float ny = dy / Mathf.Max(r, 0.0001f);
                float light = Mathf.Clamp01(0.5f + 0.5f * (-0.6f * nx + 0.8f * ny));

                float lum = bead * (0.62f + 0.5f * light);

                // A thin groove where the rim meets the felt.
                lum *= 1f - 0.45f * DecoSprites.Line(Mathf.Abs(r - (inner + 0.018f)) * half, 1.2f);

                return Grey(lum, alpha);
            }, size, Vector4.zero);
        }

        private static Sprite BuildFelt(int size, float radius)
        {
            float half = size * 0.5f;

            return DecoSprites.RasterizeShaded(size, size, (px, py) =>
            {
                float dx = (px - half) / half;
                float dy = (py - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Coverage((r - radius) * half);
                if (alpha <= 0f) return Color.clear;

                // Vignette, a soft highlight toward the light, and an inner
                // shadow under the rim.
                float lum = 1f - 0.5f * Mathf.Pow(Mathf.Clamp01(r / radius), 1.8f);

                float hx = dx + 0.18f;
                float hy = dy - 0.22f;
                float highlight = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy));
                lum *= 0.85f + 0.15f * highlight;
                lum *= 1f - 0.35f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, radius, r));

                return Grey(lum, alpha);
            }, size, Vector4.zero);
        }

        private static Sprite BuildDotted(int size, float radius, int dashes)
        {
            float half = size * 0.5f;

            return DecoSprites.Rasterize(size, size, (px, py) =>
            {
                float dx = (px - half) / half;
                float dy = (py - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                float phase = Mathf.Repeat(Mathf.Atan2(dy, dx) / (2f * Mathf.PI) * dashes, 1f);
                if (Mathf.Abs(phase - 0.5f) >= 0.22f) return 0f;

                return DecoSprites.Line(Mathf.Abs(r - radius) * half, 1.2f);
            }, size, Vector4.zero);
        }

        private static Sprite BuildArc(int size, float radius, float fromDegrees, float toDegrees)
        {
            float half = size * 0.5f;

            return DecoSprites.Rasterize(size, size, (px, py) =>
            {
                float dx = (px - half) / half;
                float dy = (py - half) / half;
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (angle < fromDegrees || angle > toDegrees) return 0f;

                float r = Mathf.Sqrt(dx * dx + dy * dy);
                return DecoSprites.Line(Mathf.Abs(r - radius) * half, 1.3f);
            }, size, Vector4.zero);
        }

        private static Sprite BuildSoftDisc(int size, float softness)
        {
            float half = size * 0.5f;

            return DecoSprites.Rasterize(size, size, (px, py) =>
            {
                float r = Mathf.Sqrt((px - half) * (px - half) + (py - half) * (py - half));
                return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half * (1f - softness) - 1f, half - 1f, r));
            }, size, Vector4.zero);
        }

        // ── Builders: figures ───────────────────────────────────────────

        /// <summary>Signed distance, in sprite units (0..1), to a seated bust.</summary>
        private static float BustDistance(float u, float v)
        {
            float head = Circle(u, v, 0.5f, 0.66f, 0.17f);

            // Shoulders: a dome with a flat base, parted from the head by a
            // thin gap so the two read as a person at any size.
            float shoulders = Mathf.Max(Circle(u, v, 0.5f, 0.12f, 0.33f), 0.12f - v);
            shoulders = Mathf.Max(shoulders, -Circle(u, v, 0.5f, 0.66f, 0.205f));

            return Mathf.Min(head, shoulders);
        }

        /// <summary>Signed distance, in sprite units (0..1), to a standing pawn.</summary>
        private static float PawnDistance(float u, float v)
        {
            float head = Circle(u, v, 0.5f, 0.80f, 0.125f);
            float collar = Ellipse(u, v, 0.5f, 0.645f, 0.15f, 0.045f);

            // The body flares from the collar to the base.
            float s = Mathf.Clamp01((v - 0.08f) / 0.54f);
            float halfWidth = 0.10f + 0.25f * Mathf.Pow(1f - s, 2.2f);
            float body = Mathf.Max(Mathf.Abs(u - 0.5f) - halfWidth, Mathf.Max(0.08f - v, v - 0.63f));

            float foot = Ellipse(u, v, 0.5f, 0.085f, 0.37f, 0.05f);

            return Mathf.Min(Mathf.Min(head, collar), Mathf.Min(body, foot));
        }

        private static Sprite BuildFigure(int size, System.Func<float, float, float> distance, bool outline)
        {
            return DecoSprites.RasterizeShaded(size, size, (px, py) =>
            {
                float u = px / size;
                float v = py / size;
                float d = distance(u, v) * size;

                if (outline)
                {
                    // A 4.4-texel stroke outside the silhouette.
                    float stroke = DecoSprites.Line(Mathf.Abs(d - 2.2f), 4.4f) * Mathf.Clamp01(d + 0.5f);
                    return new Color(1f, 1f, 1f, stroke);
                }

                float alpha = Coverage(d);
                if (alpha <= 0f) return Color.clear;

                // Lit from the left, darker toward the base, a touch of shade
                // along the edge so the silhouette has volume.
                float lum = 0.78f + 0.3f * Mathf.Clamp01(1f - Mathf.Abs(u - 0.42f) * 2.4f)
                            - 0.12f * Mathf.Clamp01(0.6f - v);
                lum *= 1f - 0.25f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-6f, 0f, d));

                return Grey(lum, alpha);
            }, size, Vector4.zero);
        }

        private static Sprite BuildHalo(int size)
        {
            float half = size * 0.5f;

            return DecoSprites.Rasterize(size, size, (px, py) =>
            {
                float dx = px - half;
                float dy = py - half;
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (angle <= 35f || angle >= 145f) return 0f;

                float r = Mathf.Sqrt(dx * dx + dy * dy);
                return DecoSprites.Line(Mathf.Abs(r - (half - 4f)), 3.5f);
            }, size, Vector4.zero);
        }

        // ── Builders: vault ─────────────────────────────────────────────

        private enum VaultPart { Plate, Frame, Dial }

        private static Sprite BuildVault(int size, VaultPart part)
        {
            float half = size * 0.46f;
            float scoop = size * 0.1f;

            return DecoSprites.Rasterize(size, size, (px, py) =>
            {
                float x = px - size * 0.5f;
                float y = py - size * 0.5f;
                float ax = Mathf.Abs(x);
                float ay = Mathf.Abs(y);

                // A square minus a quarter circle at each corner.
                float box = Mathf.Max(ax - half, ay - half);
                float cornerDistance = Mathf.Sqrt((ax - half) * (ax - half) + (ay - half) * (ay - half));
                float d = Mathf.Max(box, -(cornerDistance - scoop));

                switch (part)
                {
                    case VaultPart.Plate:
                        return Coverage(d);

                    case VaultPart.Frame:
                    {
                        float frame = DecoSprites.BandCoverage(d, 0f, 6f);

                        // A second, thin scoop inside each corner.
                        bool nearCorner = ax < half && ay < half && cornerDistance < scoop + 22f;
                        float inner = nearCorner ? DecoSprites.Line(Mathf.Abs(cornerDistance - (scoop + 13f)), 1.6f) : 0f;

                        return Mathf.Max(frame, inner);
                    }

                    default:
                    {
                        float r = Mathf.Sqrt(x * x + y * y);
                        float dial = DecoSprites.Line(Mathf.Abs(r - size * 0.30f), 5f);

                        if (r < size * 0.25f && r > size * 0.05f)
                        {
                            float diagonal = Mathf.Min(Mathf.Abs(x - y), Mathf.Abs(x + y)) / Mathf.Sqrt(2f);
                            dial = Mathf.Max(dial, DecoSprites.Line(diagonal, 5f));
                        }

                        return dial;
                    }
                }
            }, size, Vector4.zero);
        }

        private static Sprite BuildBoss(int size)
        {
            float half = size * 0.5f;
            var light = new Vector3(-0.45f, 0.55f, 0.7f).normalized;

            return DecoSprites.RasterizeShaded(size, size, (px, py) =>
            {
                float dx = (px - half) / (half - 1f);
                float dy = (py - half) / (half - 1f);
                float r2 = dx * dx + dy * dy;

                float alpha = Coverage((Mathf.Sqrt(r2) - 1f) * (half - 1f));
                if (alpha <= 0f) return Color.clear;

                float nz = Mathf.Sqrt(Mathf.Clamp01(1f - r2));
                float diffuse = Mathf.Clamp01(dx * light.x + dy * light.y + nz * light.z);
                float specular = Mathf.Pow(diffuse, 24f);

                return Grey(0.35f + 0.65f * diffuse + 0.5f * specular, alpha);
            }, size, Vector4.zero);
        }

        // ── Builders: floor ─────────────────────────────────────────────

        private enum CrossPart { Fill, Edge, Shadow }

        private static Sprite BuildCross(int gridCells, int armWidth, CrossPart part)
        {
            const float pad = 0.12f;
            int ppc = CrossTexelsPerCell;
            int size = Mathf.RoundToInt((gridCells + 2f * CrossMargin) * ppc);

            float length = gridCells * 0.5f + pad;
            float arm = armWidth * 0.5f + pad;

            return DecoSprites.Rasterize(size, size, (px, py) =>
            {
                float x = Mathf.Abs((px - size * 0.5f) / ppc);
                float y = Mathf.Abs((py - size * 0.5f) / ppc);

                // Two bars, unioned. Distances in texels.
                float across = Mathf.Max(x - length, y - arm);
                float down = Mathf.Max(x - arm, y - length);
                float d = Mathf.Min(across, down) * ppc;

                switch (part)
                {
                    case CrossPart.Fill: return Coverage(d);
                    case CrossPart.Edge: return DecoSprites.BandCoverage(d, 0f, 2.2f);
                    default: return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.9f * ppc, d));
                }
            }, ppc, Vector4.zero);
        }

        private static Sprite BuildVeins(int size, int seed)
        {
            // Fixed curves from a fixed seed: the table's grain never changes.
            var random = new System.Random(seed);
            const int count = 6;
            var curves = new float[count, 6];

            for (int i = 0; i < count; i++)
            {
                curves[i, 0] = Range(random, 40f, 140f);     // amplitude
                curves[i, 1] = Range(random, 0.004f, 0.012f); // frequency
                curves[i, 2] = Range(random, 0f, 6.28f);      // phase
                curves[i, 3] = Range(random, 0f, size);       // offset
                curves[i, 4] = Range(random, -0.6f, 0.6f);    // tilt
                curves[i, 5] = Range(random, 0.5f, 1f);       // strength
            }

            return DecoSprites.Rasterize(size, size, (px, py) =>
            {
                float alpha = 0f;

                for (int i = 0; i < count; i++)
                {
                    float f = curves[i, 0] * Mathf.Sin(curves[i, 1] * px + curves[i, 2]) + curves[i, 3] + curves[i, 4] * px;
                    float slope = curves[i, 0] * curves[i, 1] * Mathf.Cos(curves[i, 1] * px + curves[i, 2]) + curves[i, 4];
                    float off = Mathf.Abs(py - f) / Mathf.Sqrt(1f + slope * slope);

                    alpha = Mathf.Max(alpha, DecoSprites.Line(off, 1f) * curves[i, 5]);
                }

                return alpha;
            }, size, Vector4.zero);
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static float Coverage(float d) => DecoSprites.Coverage(d);

        private static Color Grey(float luminance, float alpha)
        {
            float l = Mathf.Clamp01(luminance);
            return new Color(l, l, l, alpha);
        }

        private static float Circle(float u, float v, float cx, float cy, float r) =>
            Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)) - r;

        /// <summary>An approximate distance to an ellipse, good near its edge.</summary>
        private static float Ellipse(float u, float v, float cx, float cy, float rx, float ry)
        {
            float nx = (u - cx) / rx;
            float ny = (v - cy) / ry;
            return (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(rx, ry);
        }

        private static float Range(System.Random random, float min, float max) =>
            min + (float)random.NextDouble() * (max - min);
    }
}
