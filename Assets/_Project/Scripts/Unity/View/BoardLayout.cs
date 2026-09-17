// // // Assets/_Project/Scripts/Unity/View/BoardLayout.cs
// // using NonaRoyale.Core.Board;
// // using UnityEngine;

// // namespace NonaRoyale.Unity.View
// // {
// //     /// <summary>
// //     /// Turns a <see cref="CellRef"/> into a world position.
// //     /// </summary>
// //     /// <remarks>
// //     /// <b>The core has no geometry.</b> <see cref="PathMap"/> knows that cell 14
// //     /// follows cell 13; it has no idea where either one is in space. That split
// //     /// is deliberate (ADR-0004), and this class is the whole of what the view
// //     /// adds back.
// //     ///
// //     /// <b>This draws a ring, not the Ludo cross.</b> The cross geometry in
// //     /// ADR-0003 depends on corner-cell accounting that has not yet been checked
// //     /// against the GeoGebra reference, and building on an unverified derivation
// //     /// would bake the error into board art later. A ring is honest about being a
// //     /// placeholder, and it reads laps better than a cross does — you can watch a
// //     /// piece come round again. Replacing it touches this file and nothing else.
// //     /// </remarks>
// //     public sealed class BoardLayout
// //     {
// //         private readonly BoardProfile _profile;
// //         private readonly float _radius;

// //         public BoardLayout(BoardProfile profile, float radius = 5f)
// //         {
// //             _profile = profile;
// //             _radius = radius;
// //         }

// //         public float CellSize => Mathf.Min(0.8f, 2f * Mathf.PI * _radius / _profile.CircuitLength * 0.8f);

// //         public Vector3 PositionOf(CellRef cell)
// //         {
// //             switch (cell.Kind)
// //             {
// //                 case CellKind.Track:
// //                     return OnRing(cell.Index, _radius);

// //                 case CellKind.HomeColumn:
// //                     // A spoke running inward from the colour's start angle.
// //                     // Depth 0 sits just inside the ring; the last cell stops
// //                     // short of HOME at the centre.
// //                     return Spoke(cell.Owner, _radius * (1f - 0.7f * (cell.Index + 1) / (_profile.HomeColumnLength + 1)));

// //                 case CellKind.Yard:
// //                     return Spoke(cell.Owner, _radius * 1.32f);

// //                 default:
// //                     return Vector3.zero;   // HOME
// //             }
// //         }

// //         /// <summary>Fans stacked operators apart so a shared cell is visibly a stack.</summary>
// //         public Vector3 Offset(int indexInStack, int stackSize)
// //         {
// //             if (stackSize <= 1) return Vector3.zero;

// //             float step = CellSize * 0.32f;
// //             float start = -step * (stackSize - 1) * 0.5f;
// //             return new Vector3(start + step * indexInStack, step * 0.6f, 0f);
// //         }

// //         private Vector3 OnRing(int index, float radius)
// //         {
// //             // Counter-clockwise from the top, matching ADR-0003's travel direction.
// //             float angle = (90f + 360f * index / _profile.CircuitLength) * Mathf.Deg2Rad;
// //             return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
// //         }

// //         private Vector3 Spoke(PlayerColor colour, float radius)
// //         {
// //             int startIndex = (int)colour * _profile.PlayerStartOffset;
// //             return OnRing(startIndex, radius);
// //         }

// //         public static Color ColourOf(PlayerColor colour)
// //         {
// //             switch (colour)
// //             {
// //                 case PlayerColor.Red: return new Color(0.85f, 0.25f, 0.30f);
// //                 case PlayerColor.Blue: return new Color(0.30f, 0.55f, 0.90f);
// //                 case PlayerColor.Green: return new Color(0.35f, 0.75f, 0.45f);
// //                 case PlayerColor.Yellow: return new Color(0.92f, 0.78f, 0.30f);
// //                 default: return Color.grey;
// //             }
// //         }
// //     }
// // }

// // Assets/_Project/Scripts/Unity/View/BoardLayout.cs
// using System.Collections.Generic;
// using NonaRoyale.Core.Board;
// using UnityEngine;

// namespace NonaRoyale.Unity.View
// {
//     /// <summary>
//     /// Turns a <see cref="CellRef"/> into a world position, laid out as the
//     /// four-arm Ludo cross of ADR-0003.
//     /// </summary>
//     /// <remarks>
//     /// <b>The core has no geometry.</b> <see cref="PathMap"/> knows that cell 14
//     /// follows cell 13 and nothing about where either one is. Everything spatial
//     /// lives here, which is why the board could be drawn as a ring first and a
//     /// cross now with no change to a single rule or test.
//     ///
//     /// The whole layout derives from <see cref="BoardProfile.CircuitLength"/>:
//     /// <code>
//     /// ArmLength = Circuit / 8     GridSize = Circuit / 4 + 3
//     /// </code>
//     /// 48 gives arms of 6 on a 15×15 grid; 24 gives arms of 3 on 9×9. The same
//     /// code draws both profiles.
//     ///
//     /// <b>The walk</b> threads each arm the way Ludo does: outward along one
//     /// flanking lane, across the tip, back inward along the other, then on to
//     /// the next arm. Two lanes of <c>ArmLength</c> per arm, four arms, which is
//     /// exactly <c>CircuitLength</c>. The four cells where the arms meet are
//     /// *not* track — that is what makes this a 48-cell board rather than classic
//     /// Ludo's 52 — so the path steps diagonally past each inner corner.
//     /// </remarks>
//     public sealed class BoardLayout
//     {
//         private readonly BoardProfile _profile;
//         private readonly int _arm;
//         private readonly int _grid;
//         private readonly int _centre;
//         private readonly float _spacing;

//         /// <summary>Grid coordinates of every outer-track cell, in walk order.</summary>
//         private readonly Vector2Int[] _walk;

//         /// <summary>
//         /// How far the drawn walk is rotated from the core's cell 0.
//         /// </summary>
//         /// <remarks>
//         /// The core places a colour's start at <c>colour × CircuitLength / 4</c>,
//         /// and an operator traverses every outer cell before turning in
//         /// (Journey = Circuit + HomeColumn). So home entry happens at the cell
//         /// immediately before the start comes round again — which means the
//         /// start has to sit just past the arm tip, where the home column
//         /// attaches. Shifting the drawing by one arm length puts it there.
//         ///
//         /// Classic Ludo places the start a couple of cells further along,
//         /// because its journey is two cells short of a full lap. That is a
//         /// different board, and ADR-0002's arithmetic picks this one.
//         /// </remarks>
//         private readonly int _shift;

//         public BoardLayout(BoardProfile profile, float spacing = 1f)
//         {
//             _profile = profile;
//             _arm = Mathf.Max(1, profile.CircuitLength / 8);
//             _grid = profile.CircuitLength / 4 + 3;
//             _centre = _grid / 2;
//             _spacing = spacing;
//             _shift = _arm;
//             _walk = BuildWalk();
//         }

//         /// <summary>Half the board's width in world units, for framing the camera.</summary>
//         public float Extent => _grid * 0.5f * _spacing;

//         public float CellSize => _spacing * 0.86f;

//         public Vector3 HomeGoalPosition => World(new Vector2Int(_centre, _centre));

//         public float HomeGoalSize => _spacing * 2.6f;

//         public Vector3 PositionOf(CellRef cell)
//         {
//             switch (cell.Kind)
//             {
//                 case CellKind.Track:
//                     return World(_walk[(cell.Index + _shift) % _walk.Length]);

//                 case CellKind.HomeColumn:
//                     return World(HomeCell(cell.Owner, cell.Index));

//                 case CellKind.Yard:
//                     return World(YardCell(cell.Owner));

//                 default:
//                     return HomeGoalPosition;
//             }
//         }

//         /// <summary>Fans stacked operators apart so a shared cell reads as a stack.</summary>
//         public Vector3 Offset(int indexInStack, int stackSize)
//         {
//             if (stackSize <= 1) return Vector3.zero;

//             float step = CellSize * 0.3f;
//             return new Vector3(-step * (stackSize - 1) * 0.5f + step * indexInStack, step * 0.5f, 0f);
//         }

//         // ── Geometry ─────────────────────────────────────────────────────

//         private Vector3 World(Vector2Int cell) =>
//             new Vector3((cell.x - _centre) * _spacing, (_centre - cell.y) * _spacing, 0f);

//         /// <summary>
//         /// Arm per seat: Red north, Blue west, Green south, Violet east — the
//         /// order the walk visits them, so a colour's start lands in its own arm
//         /// with no per-colour special case.
//         /// </summary>
//         private static int ArmOf(PlayerColor colour) => Mathf.Max(0, (int)colour);

//         /// <summary>
//         /// The middle lane of a colour's arm, running from the tip inward.
//         /// Depth 0 is the cell an operator turns into off the track, which is
//         /// why it sits at the tip and not beside HOME.
//         /// </summary>
//         private Vector2Int HomeCell(PlayerColor colour, int depth)
//         {
//             switch (ArmOf(colour))
//             {
//                 case 0: return new Vector2Int(_centre, depth);                  // north
//                 case 1: return new Vector2Int(depth, _centre);                  // west
//                 case 2: return new Vector2Int(_centre, _grid - 1 - depth);      // south
//                 default: return new Vector2Int(_grid - 1 - depth, _centre);     // east
//             }
//         }

//         private Vector2Int YardCell(PlayerColor colour)
//         {
//             int near = _arm / 2;
//             int far = _grid - 1 - near;

//             switch (ArmOf(colour))
//             {
//                 case 0: return new Vector2Int(near, near);      // north arm, north-west block
//                 case 1: return new Vector2Int(near, far);       // west arm, south-west block
//                 case 2: return new Vector2Int(far, far);        // south arm, south-east block
//                 default: return new Vector2Int(far, near);      // east arm, north-east block
//             }
//         }

//         /// <summary>
//         /// Builds the outer track: per arm, outward along one flanking lane,
//         /// across the tip, back inward along the other.
//         /// </summary>
//         private Vector2Int[] BuildWalk()
//         {
//             var cells = new List<Vector2Int>(_profile.CircuitLength);

//             int low = _centre - 1;
//             int high = _centre + 1;
//             int last = _grid - 1;

//             // North arm: out up the east lane, back down the west lane.
//             for (int row = _arm - 1; row >= 0; row--) cells.Add(new Vector2Int(high, row));
//             for (int row = 0; row < _arm; row++) cells.Add(new Vector2Int(low, row));

//             // West arm: out along the north lane, back along the south lane.
//             for (int col = _arm - 1; col >= 0; col--) cells.Add(new Vector2Int(col, low));
//             for (int col = 0; col < _arm; col++) cells.Add(new Vector2Int(col, high));

//             // South arm: out down the west lane, back up the east lane.
//             for (int row = _grid - _arm; row < _grid; row++) cells.Add(new Vector2Int(low, row));
//             for (int row = last; row >= _grid - _arm; row--) cells.Add(new Vector2Int(high, row));

//             // East arm: out along the south lane, back along the north lane.
//             for (int col = _grid - _arm; col < _grid; col++) cells.Add(new Vector2Int(col, high));
//             for (int col = last; col >= _grid - _arm; col--) cells.Add(new Vector2Int(col, low));

//             return cells.ToArray();
//         }

//         public static Color ColourOf(PlayerColor colour)
//         {
//             switch (colour)
//             {
//                 case PlayerColor.Red: return new Color(0.84f, 0.27f, 0.31f);
//                 case PlayerColor.Blue: return new Color(0.32f, 0.56f, 0.88f);
//                 case PlayerColor.Green: return new Color(0.34f, 0.72f, 0.44f);
//                 case PlayerColor.Yellow: return new Color(0.90f, 0.76f, 0.30f);
//                 default: return Color.grey;
//             }
//         }
//     }
// }

// Assets/_Project/Scripts/Unity/View/BoardLayout.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Turns a <see cref="CellRef"/> into a world position, laid out as the
    /// four-arm Ludo cross of ADR-0003.
    /// </summary>
    /// <remarks>
    /// <b>The core has no geometry.</b> <see cref="PathMap"/> knows that cell 14
    /// follows cell 13 and nothing about where either one is. Everything spatial
    /// lives here, which is why the board could be drawn as a ring first and a
    /// cross now with no change to a single rule or test.
    ///
    /// <b>The cross constraint.</b> Every arm contributes two flanking lanes of
    /// <see cref="ArmLength"/> plus the tip cell of its centre lane, which the
    /// loop must cross. So a continuous single-file cross satisfies
    /// <code>
    /// CircuitLength = 4 x (2L + 1) = 8L + 4      GridSize = 2L + 3
    /// </code>
    /// L=6 gives 52 on a 15x15 grid — classic Ludo, and what ADR-0002
    /// Amendment 5 adopted. L=7 gives 60 on 17x17, L=3 gives 28 on 9x9.
    ///
    /// <b>A circuit outside that family cannot be drawn as a cross.</b> 48 needs
    /// L=5.5. The previous version of this file absorbed the mismatch by handing
    /// each arm tip to the home column and letting the loop hop over it, which
    /// put four visible two-cell gaps in the track and left every home column one
    /// cell short of HOME. <see cref="IsCrossCompatible"/> now refuses such a
    /// profile rather than drawing a broken board — the core tolerates any
    /// circuit length, so the check has to live here.
    ///
    /// <b>The walk</b> threads each arm the way Ludo does: outward along one
    /// flanking lane, across the tip, back inward along the other, then a
    /// diagonal step past the inner corner to the next arm. The four inner
    /// corners are never track — they belong to the central HOME area, in this
    /// board and in classic Ludo alike.
    /// </remarks>
    public sealed class BoardLayout
    {
        private readonly BoardProfile _profile;
        private readonly int _arm;
        private readonly int _grid;
        private readonly int _centre;
        private readonly int _last;
        private readonly float _spacing;

        /// <summary>Grid coordinates of every outer-track cell, in walk order.</summary>
        private readonly Vector2Int[] _walk;

        /// <summary>
        /// How far the drawn walk is rotated from the core's cell 0.
        /// </summary>
        /// <remarks>
        /// The walk is built starting part-way up the north arm's approach lane.
        /// The core places a colour's start at <c>colour x CircuitLength / 4</c>
        /// and an operator traverses every outer cell before turning in
        /// (Journey = Circuit + HomeColumn), so home entry happens on the cell
        /// immediately before its start comes round again.
        ///
        /// Shifting by <c>ArmLength + 1</c> puts each colour's start on the cell
        /// just past its own arm tip, which makes that tip the cell it turns in
        /// from — and the tip sits on the centre lane, so the turn is a single
        /// step straight down the home column. Verified for all four seats; no
        /// per-colour special case is needed.
        /// </remarks>
        private readonly int _shift;

        public BoardLayout(BoardProfile profile, float spacing = 1f)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));

            RequireCrossCompatible(profile);

            _arm = ArmLengthFor(profile.CircuitLength);
            _grid = 2 * _arm + 3;
            _centre = _grid / 2;
            _last = _grid - 1;
            _spacing = spacing;
            _shift = _arm + 1;
            _walk = BuildWalk();
        }

        /// <summary>Flanking-lane length of one arm. The board's single shape parameter.</summary>
        public int ArmLength => _arm;

        /// <summary>Width of the square grid in cells. 15 for the Standard 52/6 board.</summary>
        public int GridSize => _grid;

        /// <summary>Half the board's width in world units, for framing the camera.</summary>
        public float Extent => _grid * 0.5f * _spacing;

        public float CellSize => _spacing * 0.86f;

        /// <summary>World distance between neighbouring cell centres.</summary>
        public float Spacing => _spacing;

        public Vector3 HomeGoalPosition => World(new Vector2Int(_centre, _centre));

        /// <summary>
        /// Deliberately under two cells wide. The home columns now run all the
        /// way to HOME, so anything larger would cover their deepest cells and
        /// hide an operator one step from finishing.
        /// </summary>
        public float HomeGoalSize => _spacing * 1.7f;

        public Vector3 PositionOf(CellRef cell)
        {
            switch (cell.Kind)
            {
                case CellKind.Track:
                    return World(_walk[(cell.Index + _shift) % _walk.Length]);

                case CellKind.HomeColumn:
                    return World(HomeCell(cell.Owner, cell.Index));

                case CellKind.Yard:
                    return World(YardCentre(cell.Owner));

                default:
                    return HomeGoalPosition;
            }
        }

        /// <summary>
        /// A yard table's diameter: the corner block less half a cell of floor
        /// on each side (GUI increment G2). Five spacings on the standard board.
        /// </summary>
        public float TableDiameter => Mathf.Max(2f, _arm - 1f) * _spacing;

        /// <summary>
        /// Where the operator at <paramref name="seat"/> in its squad sits at
        /// its table (ART_DIRECTION §6.1).
        /// </summary>
        /// <remarks>
        /// Seats are fixed per operator, not per who is still seated, so a
        /// figure never shuffles along when a squadmate stands up. They fill
        /// west, north, east, south, then the diagonals.
        /// </remarks>
        public Vector3 YardSeat(PlayerColor colour, int seat)
        {
            var centre = PositionOf(CellRef.Yard(colour));
            float angle = SeatAngle(seat) * Mathf.Deg2Rad;
            float radius = TableDiameter * 0.5f * SeatRadius;

            return centre + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
        }

        /// <summary>The square table's border beyond the outermost cells, in cell spacings.</summary>
        public const float TableMargin = 0.8f;

        /// <summary>A seat's distance from the table's centre, as a fraction of its radius.</summary>
        public const float SeatRadius = 0.55f;

        private static readonly float[] SeatAngles = { 180f, 90f, 0f, 270f, 135f, 45f, 315f, 225f };

        /// <summary>Degrees, counter-clockwise from east, of a seat.</summary>
        public static float SeatAngle(int seat) =>
            SeatAngles[((seat % SeatAngles.Length) + SeatAngles.Length) % SeatAngles.Length];

        /// <summary>Fans stacked operators apart so a shared cell reads as a stack.</summary>
        public Vector3 Offset(int indexInStack, int stackSize)
        {
            if (stackSize <= 1) return Vector3.zero;

            float step = CellSize * 0.3f;
            return new Vector3(-step * (stackSize - 1) * 0.5f + step * indexInStack, step * 0.5f, 0f);
        }

        // ── Profile compatibility ────────────────────────────────────────

        /// <summary>
        /// Arm length implied by a circuit, or -1 if that circuit is not a
        /// drawable cross.
        /// </summary>
        public static int ArmLengthFor(int circuitLength)
        {
            if (circuitLength < 12 || (circuitLength - 4) % 8 != 0) return -1;
            return (circuitLength - 4) / 8;
        }

        /// <summary>True if this profile can be drawn as a continuous Ludo cross.</summary>
        public static bool IsCrossCompatible(BoardProfile profile)
        {
            if (profile == null) return false;

            int arm = ArmLengthFor(profile.CircuitLength);
            return arm >= 1 && profile.HomeColumnLength <= arm;
        }

        private static void RequireCrossCompatible(BoardProfile profile)
        {
            int arm = ArmLengthFor(profile.CircuitLength);

            if (arm < 1)
                throw new ArgumentException(
                    $"Circuit {profile.CircuitLength} cannot be drawn as a Ludo cross. A continuous " +
                    "cross needs CircuitLength = 8L + 4 for a whole arm length L — 12, 20, 28, 36, " +
                    "44, 52, 60. Anything else leaves a gap the track has to jump at each arm tip " +
                    "(ADR-0002 Amendment 5). Use BoardProfile.Cross(name, armLength).",
                    nameof(profile));

            if (profile.HomeColumnLength > arm)
                throw new ArgumentException(
                    $"Home column of {profile.HomeColumnLength} does not fit an arm of {arm}. The " +
                    "centre lane holds the tip (a track cell) plus the column, so the column can be " +
                    $"at most {arm} cells on a {profile.CircuitLength}-cell circuit.",
                    nameof(profile));
        }

        // ── Geometry ─────────────────────────────────────────────────────

        private Vector3 World(Vector2Int cell) =>
            new Vector3((cell.x - _centre) * _spacing, (_centre - cell.y) * _spacing, 0f);

        private Vector3 World(Vector2 cell) =>
            new Vector3((cell.x - _centre) * _spacing, (_centre - cell.y) * _spacing, 0f);

        /// <summary>
        /// Arm per seat: Red north, Blue west, Green south, Violet east — the
        /// order the walk visits them, so a colour's start lands in its own arm
        /// with no per-colour special case.
        /// </summary>
        private static int ArmOf(PlayerColor colour) => Mathf.Max(0, (int)colour);

        /// <summary>
        /// The colour's centre lane, running inward from just below the arm tip.
        /// Depth 0 is the mouth — one step in from the tip the operator turns off
        /// — and the deepest cell sits directly beside HOME.
        /// </summary>
        private Vector2Int HomeCell(PlayerColor colour, int depth)
        {
            switch (ArmOf(colour))
            {
                case 0: return new Vector2Int(_centre, 1 + depth);              // north
                case 1: return new Vector2Int(1 + depth, _centre);              // west
                case 2: return new Vector2Int(_centre, _last - 1 - depth);      // south
                default: return new Vector2Int(_last - 1 - depth, _centre);     // east
            }
        }

        /// <summary>
        /// The centre of the colour's corner block, where its table stands.
        /// The block is <see cref="ArmLength"/> cells square, so its centre
        /// falls between cells on an even arm; the yard is not a cell anyone
        /// walks through, so that is fine.
        /// </summary>
        private Vector2 YardCentre(PlayerColor colour)
        {
            float near = (_arm - 1) * 0.5f;
            float far = _last - near;

            switch (ArmOf(colour))
            {
                case 0: return new Vector2(near, near);      // north arm, north-west block
                case 1: return new Vector2(near, far);       // west arm, south-west block
                case 2: return new Vector2(far, far);        // south arm, south-east block
                default: return new Vector2(far, near);      // east arm, north-east block
            }
        }

        /// <summary>
        /// Builds the outer track: per arm, outward along one flanking lane,
        /// across the centre-lane tip, back inward along the other lane.
        /// </summary>
        private Vector2Int[] BuildWalk()
        {
            var cells = new List<Vector2Int>(_profile.CircuitLength);

            int low = _centre - 1;
            int high = _centre + 1;
            int inner = _grid - _arm;   // first row/col of the far arm

            // North arm: up the east lane, across the tip, down the west lane.
            for (int row = _arm - 1; row >= 0; row--) cells.Add(new Vector2Int(high, row));
            cells.Add(new Vector2Int(_centre, 0));
            for (int row = 0; row < _arm; row++) cells.Add(new Vector2Int(low, row));

            // West arm: out along the north lane, across the tip, back along the south lane.
            for (int col = _arm - 1; col >= 0; col--) cells.Add(new Vector2Int(col, low));
            cells.Add(new Vector2Int(0, _centre));
            for (int col = 0; col < _arm; col++) cells.Add(new Vector2Int(col, high));

            // South arm: down the west lane, across the tip, up the east lane.
            for (int row = inner; row < _grid; row++) cells.Add(new Vector2Int(low, row));
            cells.Add(new Vector2Int(_centre, _last));
            for (int row = _last; row >= inner; row--) cells.Add(new Vector2Int(high, row));

            // East arm: out along the south lane, across the tip, back along the north lane.
            for (int col = inner; col < _grid; col++) cells.Add(new Vector2Int(col, high));
            cells.Add(new Vector2Int(_last, _centre));
            for (int col = _last; col >= inner; col--) cells.Add(new Vector2Int(col, low));

            return cells.ToArray();
        }

        /// <summary>A seat's colour. Kept here for its many callers; the value lives in <see cref="UiTheme"/>.</summary>
        public static Color ColourOf(PlayerColor colour) => UiTheme.Seat(colour);
    }
}