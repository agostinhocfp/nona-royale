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
    /// <b>Caches test for a destroyed sprite, not just a null one</b>
    /// (2026-09-21). The editor destroys a script-created texture when it
    /// changes scene, even while a static field still holds it, and C#'s
    /// <c>??</c> cannot see that: it only asks whether the reference is null.
    /// Unity's <c>!=</c> does see it, so every lazy sprite below is written
    /// <c>_x != null ? _x : (_x = Build())</c>, and a destroyed one is rebuilt.
    ///
    /// <b>One world unit across</b> unless a builder says otherwise, so a
    /// renderer's scale is its size. The cross is built for a given grid and
    /// cached per grid.
    ///
    /// <b>Material detail</b> (increment G3). The richness lives in the
    /// surfaces, not the trim: low-contrast veining in the cross's marble, a
    /// faint Deco sunburst set into it, fibre in the felt, one highlight
    /// streak across each gilt rim, stepped corners where the arms meet, and
    /// one cracked, tarnished length of the cross's edge (ART_DIRECTION §6:
    /// symmetry with a deliberate break). Noise comes from a fixed integer
    /// hash, so the room looks the same every run. Painted textures can
    /// replace any of these later through the same properties.
    ///
    /// <b>Painted textures slot in</b> (G4). When <see cref="BoardTextures"/>
    /// finds a painted marble or felt, the cross and the tables bake it in
    /// place of the procedural surface; everything else (the crack, the
    /// vignette, the edges) stays. Sprites are cached for the session, so a
    /// texture added while playing shows on the next run.
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

        /// <summary>Where the rim's highlight streak sits, in degrees counter-clockwise from east.</summary>
        private const float RimStreakDegrees = 128f;

        /// <summary>The streak's half-width, in degrees.</summary>
        private const float RimStreakWidth = 9f;

        /// <summary>A table's gilt rim: a bevelled ring, lit from the top left, with one highlight streak.</summary>
        public static Sprite TableRim => _rim != null ? _rim : (_rim = BuildRim(256, 0.9f));

        /// <summary>The felt inside the rim: bright at the centre, darkening to the edge.</summary>
        public static Sprite Felt => _felt != null ? _felt : (_felt = BuildFelt(256, 0.875f, BoardTextures.Felt));

        /// <summary>A dashed ring just inside the rim.</summary>
        public static Sprite DottedRing => _dotted != null ? _dotted : (_dotted = BuildDotted(256, 0.79f, 64));

        /// <summary>A thin arc over the table's upper right quarter, the dealer's line.</summary>
        public static Sprite TableArc => _arc != null ? _arc : (_arc = BuildArc(256, 0.5f, -8f, 98f));

        /// <summary>A disc with a soft edge, for drop shadows.</summary>
        public static Sprite SoftDisc => _softDisc != null ? _softDisc : (_softDisc = BuildSoftDisc(128, 0.35f));

        // ── Table body (VISUAL_PASS.md, V2) ──────────────────

        private static Sprite _edge, _band;

        /// <summary>
        /// The table's side, seen along its near edge: a lit lip over a face
        /// that falls away into the dark.
        /// </summary>
        /// <remarks>
        /// Authored lip at the top, the way it is seen. The slab hangs below
        /// the surface, so <see cref="TableBody"/> flips it rather than asking
        /// this to be written upside down.
        ///
        /// Eight texels wide because nothing varies across it - the whole face
        /// is one vertical ramp, and it is stretched to the table's width.
        /// </remarks>
        public static Sprite TableEdge => _edge != null ? _edge : (_edge = BuildEdge(8, 128));

        /// <summary>The gilt band around the table's lip: a bright roll over a body that melts into the face.</summary>
        public static Sprite TableBand => _band != null ? _band : (_band = BuildBand(8, 64));

        // ── Figures ─────────────────────────────────────────────────────

        private static Sprite _bust, _bustOutline, _pawn, _pawnOutline, _halo;

        /// <summary>An operator seated at its table: head and shoulders.</summary>
        public static Sprite Bust => _bust != null ? _bust : (_bust = BuildFigure(128, BustDistance, false));
        public static Sprite BustOutline => _bustOutline != null ? _bustOutline : (_bustOutline = BuildFigure(128, BustDistance, true));

        /// <summary>An operator standing on the floor: head, collar, flared body, base.</summary>
        public static Sprite Pawn => _pawn != null ? _pawn : (_pawn = BuildFigure(128, PawnDistance, false));
        public static Sprite PawnOutline => _pawnOutline != null ? _pawnOutline : (_pawnOutline = BuildFigure(128, PawnDistance, true));

        /// <summary>Where the shape pin sits on each figure, in sprite units from the centre.</summary>
        public static readonly Vector2 BustPin = new Vector2(0f, -0.2f);
        public static readonly Vector2 PawnPin = new Vector2(0f, -0.14f);

        /// <summary>The pawn's head centre, in sprite units from the centre.</summary>
        public static readonly Vector2 PawnHead = new Vector2(0f, 0.30f);

        /// <summary>An arc over a selected figure's head.</summary>
        public static Sprite Halo => _halo != null ? _halo : (_halo = BuildHalo(128));

        // ── Vault ───────────────────────────────────────────────────────

        private static Sprite _vaultPlate, _vaultFrame, _vaultDial, _boss;

        /// <summary>The vault door: a square with its corners scooped out.</summary>
        public static Sprite VaultPlate => _vaultPlate != null ? _vaultPlate : (_vaultPlate = BuildVault(256, VaultPart.Plate));
        public static Sprite VaultFrame => _vaultFrame != null ? _vaultFrame : (_vaultFrame = BuildVault(256, VaultPart.Frame));

        /// <summary>The dial on the door: a ring and a cross.</summary>
        public static Sprite VaultDial => _vaultDial != null ? _vaultDial : (_vaultDial = BuildVault(256, VaultPart.Dial));

        /// <summary>A polished sphere, for the dial's hub.</summary>
        public static Sprite Boss => _boss != null ? _boss : (_boss = BuildBoss(64));

        // ── Floor ───────────────────────────────────────────────────────

        private static Sprite _veins, _solid, _haze;
        private static readonly Dictionary<int, Sprite[]> Crosses = new Dictionary<int, Sprite[]>();
        private static readonly Dictionary<int, Sprite> TableRules = new Dictionary<int, Sprite>();

        /// <summary>Index of each part in the array <see cref="Cross"/> returns.</summary>
        public const int CrossFill = 0, CrossEdge = 1, CrossShadow = 2, CrossPattern = 3, CrossSheen = 4;

        /// <summary>Texels per cell in the cross sprites.</summary>
        public const int CrossTexelsPerCell = 40;

        /// <summary>Cells of margin the cross sprites carry around the grid, for the shadow.</summary>
        public const float CrossMargin = 1f;

        /// <summary>
        /// The table's side face. White with the shading in the luminance, like
        /// everything else here, so <see cref="UiTheme"/> still owns the colour.
        /// </summary>
        /// <remarks>
        /// <b>Not one gradient down the whole face.</b> A square edge lit from
        /// above reads as a hard catch of light just under the lip and a long
        /// dark body below it; a single ramp from top to bottom reads as a
        /// cylinder instead, which is what the first pass looked like.
        /// </remarks>
        private static Sprite BuildEdge(int w, int h)
        {
            return DecoSprites.RasterizeShaded(w, h, (px, py) =>
            {
                float v = py / h;

                // Squared, so the body darkens fastest near the lip and then
                // flattens out: the foot of a table edge is all one shadow.
                float body = Mathf.Lerp(0.05f, 0.30f, v * v);
                float lip = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.80f, 1f, v));

                float l = Mathf.Clamp01(body + lip * 0.5f);
                return new Color(l, l, l, 1f);
            }, h, Vector4.zero);
        }

        /// <summary>
        /// The gilt band at the lip. Opaque along its top and fading out at its
        /// bottom, so it sits on the face rather than being a stripe painted
        /// across it.
        /// </summary>
        private static Sprite BuildBand(int w, int h)
        {
            return DecoSprites.RasterizeShaded(w, h, (px, py) =>
            {
                float v = py / h;

                float body = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.34f, v));
                float roll = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.92f, v));

                float l = Mathf.Clamp01(Mathf.Lerp(0.26f, 0.58f, body) + roll * 0.42f);
                return new Color(l, l, l, body);
            }, h, Vector4.zero);
        }

        /// <summary>A few faint curved lines, the grain of the table.</summary>
        public static Sprite Veins => _veins != null ? _veins : (_veins = BuildVeins(512, 7));

        /// <summary>A solid white square, bilinear so a thin line stays smooth.</summary>
        public static Sprite Solid => _solid != null ? _solid : (_solid = DecoSprites.Rasterize(4, 4, (x, y) => 1f, 4f, Vector4.zero));

        /// <summary>
        /// Soft drifting haze for the light pools (G3): a cloud of noise that
        /// fades to nothing at its edge. One world unit across.
        /// </summary>
        public static Sprite Haze => _haze != null ? _haze : (_haze = BuildHaze(192));

        /// <summary>
        /// The cross-shaped floor for a grid of <paramref name="gridCells"/>
        /// with arms <paramref name="armWidth"/> cells wide: shaded marble fill,
        /// gilt edge, shadow and the inlaid sunburst, at the indices
        /// <see cref="CrossFill"/>, <see cref="CrossEdge"/>,
        /// <see cref="CrossShadow"/> and <see cref="CrossPattern"/>. Each is
        /// (grid + 2 x margin) cells across at <see cref="CrossTexelsPerCell"/>,
        /// so its scale is the cell spacing.
        /// </summary>
        public static Sprite[] Cross(int gridCells, int armWidth)
        {
            var marble = BoardTextures.Marble;
            int key = gridCells * 100 + armWidth + (marble != null ? 100000 : 0);
            if (Crosses.TryGetValue(key, out var sprites) && System.Array.TrueForAll(sprites, s => s != null)) return sprites;

            sprites = new[]
            {
                BuildCross(gridCells, armWidth, CrossPart.Fill, marble),
                BuildCross(gridCells, armWidth, CrossPart.Edge, null),
                BuildCross(gridCells, armWidth, CrossPart.Shadow, null),
                BuildCross(gridCells, armWidth, CrossPart.Pattern, null),
                BuildCross(gridCells, armWidth, CrossPart.Sheen, null),
            };

            Crosses[key] = sprites;
            return sprites;
        }

        /// <summary>
        /// The table's single gilt rule (G3): a thin square line
        /// <paramref name="insetCells"/> in from the edge of a table
        /// <paramref name="sideCells"/> across. One world unit across, so its
        /// scale is the table's side. No corner ornament: the corners stay dark.
        /// </summary>
        public static Sprite TableRule(float sideCells, float insetCells)
        {
            int key = Mathf.RoundToInt(sideCells * 100f) * 1000 + Mathf.RoundToInt(insetCells * 100f);
            if (TableRules.TryGetValue(key, out var sprite) && sprite != null) return sprite;

            sprite = BuildTableRule(512, sideCells, insetCells);
            TableRules[key] = sprite;
            return sprite;
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

                // The body stays under full brightness so the streak has room
                // above it: one glint across the bead, facing the light (G3).
                float lum = bead * (0.5f + 0.36f * light);

                float fromStreak = Mathf.DeltaAngle(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, RimStreakDegrees);
                float streak = Mathf.Exp(-(fromStreak * fromStreak) / (RimStreakWidth * RimStreakWidth));
                lum += 0.42f * streak * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 1.5f);

                // A thin groove where the rim meets the felt.
                lum *= 1f - 0.45f * DecoSprites.Line(Mathf.Abs(r - (inner + 0.018f)) * half, 1.2f);

                return Grey(lum, alpha);
            }, size, Vector4.zero);
        }

        private static Sprite BuildFelt(int size, float radius, TileSampler painted)
        {
            // A painted felt shows its grain around its own average, so the seat tint holds.
            float mean = painted != null ? Mathf.Max(0.02f, painted.MeanLuminance) : 1f;

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

                if (painted != null)
                {
                    float u = px / size * BoardTextures.FeltRepeats;
                    float v = py / size * BoardTextures.FeltRepeats;
                    lum *= Mathf.Clamp(painted.Brightness(u, v) / mean, 0.6f, 1.4f);
                }
                else
                {
                    // Fibre (G3): grain per texel, and a faint nap running one way.
                    float grain = Hash(Mathf.FloorToInt(px), Mathf.FloorToInt(py), 11) * 2f - 1f;
                    float nap = ValueNoise(px * 0.9f, py * 0.14f, 12) * 2f - 1f;
                    lum *= 1f + 0.045f * grain + 0.035f * nap;
                }

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

        private enum CrossPart { Fill, Edge, Shadow, Pattern, Sheen }

        /// <summary>Padding around the cells, in cells: how far the floor reaches past them.</summary>
        private const float CrossPad = 0.12f;

        /// <summary>The cross's gilt edge, in texels (G3: was 2.2).</summary>
        private const float CrossEdgeWidth = 1.4f;

        // ── The floor's sheen (VISUAL_PASS.md, V5) ────────────────

        /// <summary>
        /// Which way the raking light crosses the floor, and where its middle
        /// falls across the board as a fraction of the half-grid.
        /// </summary>
        /// <remarks>
        /// Off the diagonal and off centre, on purpose. A band along an arm
        /// lines up with a lane and reads as a seam; a band through the middle
        /// sits under the vault's own glow and is wasted there.
        /// </remarks>
        private const float SheenDegrees = 34f;
        private const float SheenCentre = -0.34f;
        private const float SheenWidth = 0.42f;

        /// <summary>
        /// How much of the sheen the bare stone takes. The rest belongs to the
        /// veins, which is the whole difference between a polished marble and
        /// a matte one.
        /// </summary>
        private const float SheenStone = 0.35f;

        /// <summary>
        /// Stepped Deco corners where the arms meet (ART_DIRECTION §6): three
        /// boxes stacked in each inner corner, as (width, height) in cells
        /// measured out from the corner. Each box also reaches half a cell
        /// back into the floor, so the joins are interior and draw no edge.
        /// </summary>
        private static readonly Vector2[] CornerSteps =
        {
            new Vector2(0.42f, 0.14f), new Vector2(0.28f, 0.28f), new Vector2(0.14f, 0.42f),
        };

        /// <summary>Rays in the floor's sunburst, all the way round.</summary>
        private const int PatternRays = 48;

        private static Sprite BuildCross(int gridCells, int armWidth, CrossPart part, TileSampler marble)
        {
            int ppc = CrossTexelsPerCell;
            int size = Mathf.RoundToInt((gridCells + 2f * CrossMargin) * ppc);

            float length = gridCells * 0.5f + CrossPad;
            float arm = armWidth * 0.5f + CrossPad;

            float sheenCos = Mathf.Cos(SheenDegrees * Mathf.Deg2Rad);
            float sheenSin = Mathf.Sin(SheenDegrees * Mathf.Deg2Rad);
            float sheenSpan = Mathf.Max(1f, gridCells * 0.5f);

            // The deliberate break: one length of the south arm's west edge,
            // a little under halfway out (ART_DIRECTION §6).
            var crack = new Vector2(-arm, -(armWidth * 0.5f + (length - armWidth * 0.5f) * 0.45f));

            return DecoSprites.RasterizeShaded(size, size, (px, py) =>
            {
                // Signed cell coordinates, y up; distances in texels.
                float x = (px - size * 0.5f) / ppc;
                float y = (py - size * 0.5f) / ppc;
                float d = CrossDistance(Mathf.Abs(x), Mathf.Abs(y), length, arm) * ppc;

                switch (part)
                {
                    case CrossPart.Fill:
                    {
                        float alpha = Coverage(d);
                        if (alpha <= 0f) return Color.clear;

                        if (marble == null) return Grey(Marble(x, y) * CrackShade(x, y, crack, ppc), alpha);

                        // A painted marble, in its own colours, with the same crack.
                        var paint = marble.Sample(x / BoardTextures.MarbleTileCells, y / BoardTextures.MarbleTileCells);
                        float shade = CrackShade(x, y, crack, ppc);
                        return new Color(paint.r * shade, paint.g * shade, paint.b * shade, alpha);
                    }

                    case CrossPart.Edge:
                    {
                        float alpha = DecoSprites.BandCoverage(d, 0f, CrossEdgeWidth);
                        return alpha <= 0f ? Color.clear : new Color(1f, 1f, 1f, alpha * CrackedTrim(x, y, crack, arm, ppc));
                    }

                    case CrossPart.Shadow:
                        return new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.9f * ppc, d)));

                    case CrossPart.Sheen:
                    {
                        // Kept off the edge, like the pattern: a highlight that
                        // runs into the gilt trim reads as a smear on the trim.
                        float inside = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.15f * ppc, -0.45f * ppc, d));
                        if (inside <= 0f) return Color.clear;

                        float along = (x * sheenCos + y * sheenSin) / sheenSpan - SheenCentre;
                        float band = Mathf.Exp(-(along * along) / (SheenWidth * SheenWidth));

                        // The veins take the light first. With a painted marble
                        // the fill has its own veins and these do not register
                        // with them, so the band simply falls back to an even
                        // rake - which is why this part is never painted.
                        Veining(x, y, out _, out float veins, out float threads);
                        float catches = SheenStone + (1f - SheenStone) * Mathf.Max(veins, threads * 0.6f);

                        return new Color(1f, 1f, 1f, band * catches * inside);
                    }

                    default:
                    {
                        // Kept off the edge and out of the vault's light.
                        float inside = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.15f * ppc, -0.4f * ppc, d));
                        if (inside <= 0f) return Color.clear;

                        float r = Mathf.Sqrt(x * x + y * y);
                        float centre = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.3f, 2.1f, r));
                        return new Color(1f, 1f, 1f, Sunburst(x, y, r, ppc) * inside * centre);
                    }
                }
            }, ppc, Vector4.zero);
        }

        /// <summary>Signed distance, in cells, to the cross with its stepped corners. Takes |x| and |y|.</summary>
        private static float CrossDistance(float ax, float ay, float length, float arm)
        {
            // Two bars, unioned.
            float across = Mathf.Max(ax - length, ay - arm);
            float down = Mathf.Max(ax - arm, ay - length);
            float d = Mathf.Min(across, down);

            foreach (var step in CornerSteps)
            {
                float from = arm - 0.5f;
                float halfX = (step.x + 0.5f) * 0.5f;
                float halfY = (step.y + 0.5f) * 0.5f;
                d = Mathf.Min(d, BoxDistance(ax - (from + halfX), ay - (from + halfY), halfX, halfY));
            }

            return d;
        }

        /// <summary>
        /// Marble luminance at a cell coordinate: a soft cloud and two sets of
        /// thin veins bent by noise. Kept between about 0.8 and 1, so the
        /// floor stays dark.
        /// </summary>
        private static float Marble(float x, float y)
        {
            Veining(x, y, out float cloud, out float veins, out float threads);

            float lum = 0.84f + 0.08f * (cloud - 0.5f) * 2f + 0.09f * veins + 0.045f * threads;
            return Mathf.Min(lum, 1f);
        }

        /// <summary>
        /// The marble's cloud and its two sets of veins, shared by the stone
        /// and by its sheen (V5) so the highlight lands on the veins that are
        /// actually there rather than on a second, unrelated pattern.
        /// </summary>
        private static void Veining(float x, float y, out float cloud, out float veins, out float threads)
        {
            cloud = Fbm(x * 1.1f, y * 1.1f, 3, 21);
            float warp = Fbm(x * 0.45f + 7.1f, y * 0.45f - 3.3f, 3, 22);

            float main = Mathf.Sin((x * 0.55f + y * 0.35f) * 2.1f + warp * 7f);
            veins = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(main) / 0.07f);

            float fine = Mathf.Sin((x * -0.3f + y * 0.62f) * 4.3f + warp * 11f);
            threads = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(fine) / 0.05f);
        }

        /// <summary>
        /// The hairline crack in the marble beside the broken trim: a short
        /// jagged line running in from the break, clear of the cells. A
        /// multiplier, 1 away from the crack.
        /// </summary>
        private static float CrackShade(float x, float y, Vector2 crack, int ppc)
        {
            float off = Mathf.Min(
                Segment(x, y, crack, crack + new Vector2(0.06f, -0.035f)),
                Mathf.Min(
                    Segment(x, y, crack + new Vector2(0.06f, -0.035f), crack + new Vector2(0.11f, 0.015f)),
                    Segment(x, y, crack + new Vector2(0.11f, 0.015f), crack + new Vector2(0.16f, -0.03f))));
            return 1f - 0.45f * DecoSprites.Line(off * ppc, 0.8f);
        }

        /// <summary>
        /// The broken length of trim: tarnished for about a cell and a half,
        /// with a slanted gap at its middle. 1 everywhere else.
        /// </summary>
        private static float CrackedTrim(float x, float y, Vector2 crack, float arm, int ppc)
        {
            // Only the west edge of the south arm.
            if (x > -arm + 0.1f || x < -arm - 0.1f || y > -arm) return 1f;

            float along = y - crack.y;
            float tarnish = 1f - 0.5f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 0.45f, Mathf.Abs(along)));

            float gap = Mathf.Abs(along + 0.6f * (x + arm)) * ppc;
            return tarnish * Mathf.Clamp01(gap - 1.4f);
        }

        /// <summary>
        /// The floor's Deco sunburst: rays from the vault, every other one
        /// starting further out, crossed by rings every two cells.
        /// </summary>
        private static float Sunburst(float x, float y, float r, int ppc)
        {
            float step = 360f / PatternRays;
            float angle = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            int index = Mathf.RoundToInt(angle / step);
            float delta = (angle - index * step) * Mathf.Deg2Rad;

            float start = (index & 1) == 0 ? 1.8f : 3.2f;
            float ray = r < start ? 0f : DecoSprites.Line(r * ppc * Mathf.Abs(Mathf.Sin(delta)), 1f);

            int ringIndex = Mathf.RoundToInt((r - 1f) * 0.5f);
            float ring = ringIndex < 1 ? 0f : DecoSprites.Line(Mathf.Abs(r - (1f + 2f * ringIndex)) * ppc, 1.2f);

            return Mathf.Max(ray, ring);
        }

        private static Sprite BuildTableRule(int size, float sideCells, float insetCells)
        {
            float texelsPerCell = size / sideCells;
            float half = size * 0.5f;
            float edge = half - insetCells * texelsPerCell;

            return DecoSprites.Rasterize(size, size, (px, py) =>
            {
                float d = Mathf.Max(Mathf.Abs(px - half), Mathf.Abs(py - half)) - edge;
                return DecoSprites.Line(Mathf.Abs(d), 1.2f);
            }, size, Vector4.zero);
        }

        private static Sprite BuildHaze(int size)
        {
            float half = size * 0.5f;

            return DecoSprites.Rasterize(size, size, (px, py) =>
            {
                float u = (px - half) / half;
                float v = (py - half) / half;
                float r = Mathf.Sqrt(u * u + v * v);
                if (r >= 1f) return 0f;

                float falloff = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, r));
                float cloud = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.85f, Fbm(u * 2.4f + 3f, v * 2.4f, 4, 31)));
                return cloud * falloff;
            }, size, Vector4.zero);
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

        private static float BoxDistance(float x, float y, float halfX, float halfY)
        {
            float qx = Mathf.Abs(x) - halfX;
            float qy = Mathf.Abs(y) - halfY;
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f);
        }

        /// <summary>Distance from a point to the segment a–b.</summary>
        private static float Segment(float x, float y, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var ap = new Vector2(x - a.x, y - a.y);
            float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab));
            return (ap - ab * t).magnitude;
        }

        /// <summary>A fixed pseudo-random value in [0, 1] per lattice point: the same room every run.</summary>
        internal static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144665 + 1013904223);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        /// <summary>Smoothly interpolated lattice noise in [0, 1].</summary>
        internal static float ValueNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float bottom = Mathf.Lerp(Hash(ix, iy, seed), Hash(ix + 1, iy, seed), fx);
            float top = Mathf.Lerp(Hash(ix, iy + 1, seed), Hash(ix + 1, iy + 1, seed), fx);
            return Mathf.Lerp(bottom, top, fy);
        }

        /// <summary>Layered <see cref="ValueNoise"/>, each octave twice as fine, in [0, 1].</summary>
        internal static float Fbm(float x, float y, int octaves, int seed)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float total = 0f;

            for (int i = 0; i < octaves; i++)
            {
                sum += amplitude * ValueNoise(x, y, seed + i);
                total += amplitude;
                x *= 2.03f;
                y *= 2.03f;
                amplitude *= 0.5f;
            }

            return sum / total;
        }

        private static float Range(System.Random random, float min, float max) =>
            min + (float)random.NextDouble() * (max - min);
    }
}
