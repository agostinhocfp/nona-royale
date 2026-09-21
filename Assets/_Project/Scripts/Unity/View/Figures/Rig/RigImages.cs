// Assets/_Project/Scripts/Unity/View/Figures/Rig/RigImages.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// One part, rasterised for play (OPERATOR_LOOKBOOK.md, LB5b): its rest
    /// image, and what the seated pose draws of it once the table line has
    /// taken what is under the table.
    /// </summary>
    public sealed class RigPartImages
    {
        public RigPart Part { get; }

        /// <summary>The bone's index in the skeleton's <see cref="RigSkeleton.Bones"/>.</summary>
        public int BoneIndex { get; }

        public FigureImage Standing { get; }

        /// <summary>
        /// The cel-facet normal maps (<see cref="FigureNormals"/>), texel for
        /// texel with <see cref="Standing"/> and <see cref="Seated"/>. The
        /// powered image shares the standing one's canvas, so its normals too.
        /// </summary>
        public byte[] StandingNormals { get; }
        public byte[] SeatedNormals { get; }

        /// <summary>The part with its cyan tell lit (LB5c), for the cast's hold; null for a part with no powered layer.</summary>
        public FigureImage Powered { get; }

        /// <summary>
        /// Seated: the same image when the part is wholly above the table,
        /// a cut one when the table line crosses it, and null when it is
        /// wholly under the table or the seated pose does not show it.
        /// </summary>
        public FigureImage Seated { get; }

        public RigPartImages(RigPart part, int boneIndex, FigureImage standing, FigureImage seated, FigureImage powered = null,
            byte[] standingNormals = null, byte[] seatedNormals = null)
        {
            StandingNormals = standingNormals;
            SeatedNormals = seatedNormals;
            Part = part;
            BoneIndex = boneIndex;
            Standing = standing;
            Seated = seated;
            Powered = powered;
        }
    }

    /// <summary>Every part of one facing of a rig, rasterised, and what bounds them.</summary>
    public sealed class RigFacingImages
    {
        public OperatorRig Rig { get; }

        /// <summary>Back to front.</summary>
        public IReadOnlyList<RigPartImages> Parts { get; }

        /// <summary>The crouch a rise starts from, built once.</summary>
        public RigPose Crouch { get; }

        public RigFacingImages(OperatorRig rig, IReadOnlyList<RigPartImages> parts)
        {
            Rig = rig;
            Parts = parts;
            Crouch = RigAnimator.Crouch(rig);
        }
    }

    /// <summary>
    /// A rig ready for the board, in plain C# (OPERATOR_LOOKBOOK.md, LB5b):
    /// both facings, each part rasterised once, and the seated cut. Built on
    /// the thread pool; <c>OperatorRigArt</c> turns it into sprites.
    /// </summary>
    /// <remarks>
    /// <b>The table line, on the board.</b> The composer clips a seated pose
    /// at the table line per pixel. On the board each part is a sprite, so the
    /// cut is drawn into the part instead: the table line is carried into
    /// the part's own rest space under the seated pose and the part is cut
    /// along it, ink and all, the way the look book cuts a bust at the waist.
    /// A part wholly above the line keeps its rest image; one wholly below
    /// is not drawn. The seated loop only breathes (a degree, a hundredth of
    /// a unit), so a cut made for the seated pose holds for all of it.
    /// </remarks>
    public sealed class RigImages
    {
        /// <summary>The look book's resolution, so a rigged figure is as sharp as a drawn one.</summary>
        public const float PixelsPerUnit = 96f;
        public const int Supersample = 4;

        public string Key { get; }
        public RigFacingImages Right { get; }
        public RigFacingImages Left { get; }

        /// <summary>The rest pose's lowest and highest opaque points, in figure units: what the layout fits.</summary>
        public float RestBottom { get; }
        public float RestTop { get; }

        /// <summary>The seated pose's: the table line, and the top of the head as it sits.</summary>
        public float SeatedBottom { get; }
        public float SeatedTop { get; }

        public double Milliseconds { get; }

        private RigImages(string key, RigFacingImages right, RigFacingImages left, double milliseconds)
        {
            Key = key;
            Right = right;
            Left = left;
            Milliseconds = milliseconds;

            var rest = right.Rig.Pose(RigPoseNames.Rest);
            Extent(right, rest, false, out float restBottom, out float restTop);
            RestBottom = restBottom;
            RestTop = restTop;

            var seated = right.Rig.Pose(RigPoseNames.Seated);
            Extent(right, seated, true, out float seatedBottom, out float seatedTop);
            SeatedBottom = seated.TableLine.HasValue ? Math.Max(seatedBottom, seated.TableLine.Value) : seatedBottom;
            SeatedTop = seatedTop;
        }

        public RigFacingImages Facing(bool left) => left ? Left : Right;

        /// <summary>Rasterises both facings of <paramref name="rig"/>. Plain C#, safe off the main thread.</summary>
        public static RigImages Build(OperatorRig rig, LookBookPalette palette)
        {
            if (rig == null) throw new ArgumentNullException(nameof(rig));

            var clock = System.Diagnostics.Stopwatch.StartNew();
            var right = BuildFacing(rig, palette);
            var left = BuildFacing(rig.Mirrored(), palette);
            return new RigImages(rig.Key, right, left, clock.Elapsed.TotalMilliseconds);
        }

        private static RigFacingImages BuildFacing(OperatorRig rig, LookBookPalette palette)
        {
            var seatedPose = rig.Pose(RigPoseNames.Seated);
            var seatedWorld = rig.Skeleton.Evaluate(seatedPose);
            var parts = new List<RigPartImages>();
            var metals = palette?.Metals();

            foreach (var part in rig.Parts(palette))
            {
                var bone = rig.Skeleton[part.Bone];
                var standing = Render(part.Drawing);
                var standingNormals = FigureNormals.Render(part.Drawing, standing.Canvas, metals, rig.FacesLeft);

                FigureImage seated = null;
                byte[] seatedNormals = null;
                if (part.VisibleIn(seatedPose))
                {
                    var cut = seatedPose.TableLine.HasValue
                        ? CutAtTable(part.Drawing, bone, seatedWorld[part.Bone], seatedPose.TableLine.Value)
                        : part.Drawing;

                    if (ReferenceEquals(cut, part.Drawing))
                    {
                        seated = standing;
                        seatedNormals = standingNormals;
                    }
                    else if (cut != null)
                    {
                        seated = Render(cut);
                        if (seated.IsEmpty) seated = null;
                        else seatedNormals = FigureNormals.Render(cut, seated.Canvas, metals, rig.FacesLeft);
                    }
                }

                // The tell is drawn only on the parts that carry it, and only standing: nobody casts from the table.
                var powered = part.Drawing.HasPowered ? Render(part.Drawing, powered: true) : null;

                parts.Add(new RigPartImages(part, rig.Skeleton.IndexOf(part.Bone), standing, seated, powered,
                    standingNormals, seatedNormals));
            }

            return new RigFacingImages(rig, parts);
        }

        /// <summary>
        /// The part as the seated pose draws it: the drawing itself when it is
        /// wholly above the table, the drawing cut along the table line
        /// carried into its rest space, or null when it is wholly below.
        /// </summary>
        private static FigureDrawing CutAtTable(FigureDrawing drawing, RigBone bone, BoneWorld placed, float tableLine)
        {
            var box = drawing.Silhouette.Bounds.Expand(drawing.LineWeight);
            float low = float.MaxValue, high = float.MinValue;

            for (int corner = 0; corner < 4; corner++)
            {
                float x = (corner & 1) == 0 ? box.MinX : box.MaxX;
                float y = (corner & 2) == 0 ? box.MinY : box.MaxY;
                RigSkeleton.Transform(placed, bone.PivotX, bone.PivotY, x, y, out _, out float wy);
                low = Math.Min(low, wy);
                high = Math.Max(high, wy);
            }

            if (low >= tableLine) return drawing;
            if (high <= tableLine) return null;

            // The line, and the way down, in the part's rest space. The cut
            // is inked like any edge, and the ink grows outward, so the cut
            // sits one line weight above the table: the ink ends on it.
            float cutAt = tableLine + drawing.LineWeight;
            RigSkeleton.Untransform(placed, bone.PivotX, bone.PivotY, placed.PivotX, cutAt, out float px, out float py);
            RigSkeleton.Untransform(placed, bone.PivotX, bone.PivotY, placed.PivotX, cutAt - 1f, out float qx, out float qy);

            return drawing.CroppedBy(px, py, qx - px, qy - py, py);
        }

        /// <summary>A part on a canvas just big enough for its ink and rim, as the composer does it.</summary>
        private static FigureImage Render(FigureDrawing drawing, bool powered = false)
        {
            float pad = drawing.LineWeight + drawing.RimWidth + 3f / PixelsPerUnit;
            var box = drawing.Silhouette.Bounds.Expand(pad);

            int width = Math.Max(1, (int)Math.Ceiling((box.MaxX - box.MinX) * PixelsPerUnit));
            int height = Math.Max(1, (int)Math.Ceiling((box.MaxY - box.MinY) * PixelsPerUnit));
            var canvas = new FigureCanvas(box.MinX, box.MinY, width, height, PixelsPerUnit, Supersample);

            return FigureRasterizer.Render(drawing, canvas, powered);
        }

        /// <summary>The lowest and highest opaque points of the visible parts in a pose.</summary>
        private static void Extent(RigFacingImages facing, RigPose pose, bool seated, out float bottom, out float top)
        {
            var world = facing.Rig.Skeleton.Evaluate(pose);
            bottom = float.MaxValue;
            top = float.MinValue;

            foreach (var entry in facing.Parts)
            {
                var image = seated ? entry.Seated : entry.Standing;
                if (image == null || image.IsEmpty || !entry.Part.VisibleIn(pose)) continue;

                var bone = facing.Rig.Skeleton[entry.Part.Bone];
                var placed = world[entry.Part.Bone];
                var canvas = image.Canvas;
                float y0 = canvas.Bottom + image.OpaqueBottomRow / canvas.PixelsPerUnit;
                float y1 = canvas.Bottom + (image.OpaqueTopRow + 1) / canvas.PixelsPerUnit;
                float x0 = canvas.Left, x1 = canvas.Left + image.Width / canvas.PixelsPerUnit;

                for (int corner = 0; corner < 4; corner++)
                {
                    RigSkeleton.Transform(placed, bone.PivotX, bone.PivotY,
                        (corner & 1) == 0 ? x0 : x1, (corner & 2) == 0 ? y0 : y1, out _, out float wy);
                    bottom = Math.Min(bottom, wy);
                    top = Math.Max(top, wy);
                }
            }

            if (bottom > top)
            {
                bottom = 0f;
                top = 1f;
            }
        }
    }
}
