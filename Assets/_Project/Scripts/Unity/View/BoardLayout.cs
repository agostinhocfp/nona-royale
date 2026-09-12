// // Assets/_Project/Scripts/Unity/View/BoardLayout.cs
// using NonaRoyale.Core.Board;
// using UnityEngine;

// namespace NonaRoyale.Unity.View
// {
//     /// <summary>
//     /// Turns a <see cref="CellRef"/> into a world position.
//     /// </summary>
//     /// <remarks>
//     /// <b>The core has no geometry.</b> <see cref="PathMap"/> knows that cell 14
//     /// follows cell 13; it has no idea where either one is in space. That split
//     /// is deliberate (ADR-0004), and this class is the whole of what the view
//     /// adds back.
//     ///
//     /// <b>This draws a ring, not the Ludo cross.</b> The cross geometry in
//     /// ADR-0003 depends on corner-cell accounting that has not yet been checked
//     /// against the GeoGebra reference, and building on an unverified derivation
//     /// would bake the error into board art later. A ring is honest about being a
//     /// placeholder, and it reads laps better than a cross does — you can watch a
//     /// piece come round again. Replacing it touches this file and nothing else.
//     /// </remarks>
//     public sealed class BoardLayout
//     {
//         private readonly BoardProfile _profile;
//         private readonly float _radius;

//         public BoardLayout(BoardProfile profile, float radius = 5f)
//         {
//             _profile = profile;
//             _radius = radius;
//         }

//         public float CellSize => Mathf.Min(0.8f, 2f * Mathf.PI * _radius / _profile.CircuitLength * 0.8f);

//         public Vector3 PositionOf(CellRef cell)
//         {
//             switch (cell.Kind)
//             {
//                 case CellKind.Track:
//                     return OnRing(cell.Index, _radius);

//                 case CellKind.HomeColumn:
//                     // A spoke running inward from the colour's start angle.
//                     // Depth 0 sits just inside the ring; the last cell stops
//                     // short of HOME at the centre.
//                     return Spoke(cell.Owner, _radius * (1f - 0.7f * (cell.Index + 1) / (_profile.HomeColumnLength + 1)));

//                 case CellKind.Yard:
//                     return Spoke(cell.Owner, _radius * 1.32f);

//                 default:
//                     return Vector3.zero;   // HOME
//             }
//         }

//         /// <summary>Fans stacked operators apart so a shared cell is visibly a stack.</summary>
//         public Vector3 Offset(int indexInStack, int stackSize)
//         {
//             if (stackSize <= 1) return Vector3.zero;

//             float step = CellSize * 0.32f;
//             float start = -step * (stackSize - 1) * 0.5f;
//             return new Vector3(start + step * indexInStack, step * 0.6f, 0f);
//         }

//         private Vector3 OnRing(int index, float radius)
//         {
//             // Counter-clockwise from the top, matching ADR-0003's travel direction.
//             float angle = (90f + 360f * index / _profile.CircuitLength) * Mathf.Deg2Rad;
//             return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
//         }

//         private Vector3 Spoke(PlayerColor colour, float radius)
//         {
//             int startIndex = (int)colour * _profile.PlayerStartOffset;
//             return OnRing(startIndex, radius);
//         }

//         public static Color ColourOf(PlayerColor colour)
//         {
//             switch (colour)
//             {
//                 case PlayerColor.Red: return new Color(0.85f, 0.25f, 0.30f);
//                 case PlayerColor.Blue: return new Color(0.30f, 0.55f, 0.90f);
//                 case PlayerColor.Green: return new Color(0.35f, 0.75f, 0.45f);
//                 case PlayerColor.Yellow: return new Color(0.92f, 0.78f, 0.30f);
//                 default: return Color.grey;
//             }
//         }
//     }
// }

// Assets/_Project/Scripts/Unity/View/BoardLayout.cs
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
    /// The whole layout derives from <see cref="BoardProfile.CircuitLength"/>:
    /// <code>
    /// ArmLength = Circuit / 8     GridSize = Circuit / 4 + 3
    /// </code>
    /// 48 gives arms of 6 on a 15×15 grid; 24 gives arms of 3 on 9×9. The same
    /// code draws both profiles.
    ///
    /// <b>The walk</b> threads each arm the way Ludo does: outward along one
    /// flanking lane, across the tip, back inward along the other, then on to
    /// the next arm. Two lanes of <c>ArmLength</c> per arm, four arms, which is
    /// exactly <c>CircuitLength</c>. The four cells where the arms meet are
    /// *not* track — that is what makes this a 48-cell board rather than classic
    /// Ludo's 52 — so the path steps diagonally past each inner corner.
    /// </remarks>
    public sealed class BoardLayout
    {
        private readonly BoardProfile _profile;
        private readonly int _arm;
        private readonly int _grid;
        private readonly int _centre;
        private readonly float _spacing;

        /// <summary>Grid coordinates of every outer-track cell, in walk order.</summary>
        private readonly Vector2Int[] _walk;

        /// <summary>
        /// How far the drawn walk is rotated from the core's cell 0.
        /// </summary>
        /// <remarks>
        /// The core places a colour's start at <c>colour × CircuitLength / 4</c>,
        /// and an operator traverses every outer cell before turning in
        /// (Journey = Circuit + HomeColumn). So home entry happens at the cell
        /// immediately before the start comes round again — which means the
        /// start has to sit just past the arm tip, where the home column
        /// attaches. Shifting the drawing by one arm length puts it there.
        ///
        /// Classic Ludo places the start a couple of cells further along,
        /// because its journey is two cells short of a full lap. That is a
        /// different board, and ADR-0002's arithmetic picks this one.
        /// </remarks>
        private readonly int _shift;

        public BoardLayout(BoardProfile profile, float spacing = 1f)
        {
            _profile = profile;
            _arm = Mathf.Max(1, profile.CircuitLength / 8);
            _grid = profile.CircuitLength / 4 + 3;
            _centre = _grid / 2;
            _spacing = spacing;
            _shift = _arm;
            _walk = BuildWalk();
        }

        /// <summary>Half the board's width in world units, for framing the camera.</summary>
        public float Extent => _grid * 0.5f * _spacing;

        public float CellSize => _spacing * 0.86f;

        public Vector3 HomeGoalPosition => World(new Vector2Int(_centre, _centre));

        public float HomeGoalSize => _spacing * 2.6f;

        public Vector3 PositionOf(CellRef cell)
        {
            switch (cell.Kind)
            {
                case CellKind.Track:
                    return World(_walk[(cell.Index + _shift) % _walk.Length]);

                case CellKind.HomeColumn:
                    return World(HomeCell(cell.Owner, cell.Index));

                case CellKind.Yard:
                    return World(YardCell(cell.Owner));

                default:
                    return HomeGoalPosition;
            }
        }

        /// <summary>Fans stacked operators apart so a shared cell reads as a stack.</summary>
        public Vector3 Offset(int indexInStack, int stackSize)
        {
            if (stackSize <= 1) return Vector3.zero;

            float step = CellSize * 0.3f;
            return new Vector3(-step * (stackSize - 1) * 0.5f + step * indexInStack, step * 0.5f, 0f);
        }

        // ── Geometry ─────────────────────────────────────────────────────

        private Vector3 World(Vector2Int cell) =>
            new Vector3((cell.x - _centre) * _spacing, (_centre - cell.y) * _spacing, 0f);

        /// <summary>
        /// Arm per seat: Red north, Blue west, Green south, Yellow east — the
        /// order the walk visits them, so a colour's start lands in its own arm
        /// with no per-colour special case.
        /// </summary>
        private static int ArmOf(PlayerColor colour) => Mathf.Max(0, (int)colour);

        /// <summary>
        /// The middle lane of a colour's arm, running from the tip inward.
        /// Depth 0 is the cell an operator turns into off the track, which is
        /// why it sits at the tip and not beside HOME.
        /// </summary>
        private Vector2Int HomeCell(PlayerColor colour, int depth)
        {
            switch (ArmOf(colour))
            {
                case 0: return new Vector2Int(_centre, depth);                  // north
                case 1: return new Vector2Int(depth, _centre);                  // west
                case 2: return new Vector2Int(_centre, _grid - 1 - depth);      // south
                default: return new Vector2Int(_grid - 1 - depth, _centre);     // east
            }
        }

        private Vector2Int YardCell(PlayerColor colour)
        {
            int near = _arm / 2;
            int far = _grid - 1 - near;

            switch (ArmOf(colour))
            {
                case 0: return new Vector2Int(near, near);      // north arm, north-west block
                case 1: return new Vector2Int(near, far);       // west arm, south-west block
                case 2: return new Vector2Int(far, far);        // south arm, south-east block
                default: return new Vector2Int(far, near);      // east arm, north-east block
            }
        }

        /// <summary>
        /// Builds the outer track: per arm, outward along one flanking lane,
        /// across the tip, back inward along the other.
        /// </summary>
        private Vector2Int[] BuildWalk()
        {
            var cells = new List<Vector2Int>(_profile.CircuitLength);

            int low = _centre - 1;
            int high = _centre + 1;
            int last = _grid - 1;

            // North arm: out up the east lane, back down the west lane.
            for (int row = _arm - 1; row >= 0; row--) cells.Add(new Vector2Int(high, row));
            for (int row = 0; row < _arm; row++) cells.Add(new Vector2Int(low, row));

            // West arm: out along the north lane, back along the south lane.
            for (int col = _arm - 1; col >= 0; col--) cells.Add(new Vector2Int(col, low));
            for (int col = 0; col < _arm; col++) cells.Add(new Vector2Int(col, high));

            // South arm: out down the west lane, back up the east lane.
            for (int row = _grid - _arm; row < _grid; row++) cells.Add(new Vector2Int(low, row));
            for (int row = last; row >= _grid - _arm; row--) cells.Add(new Vector2Int(high, row));

            // East arm: out along the south lane, back along the north lane.
            for (int col = _grid - _arm; col < _grid; col++) cells.Add(new Vector2Int(col, high));
            for (int col = last; col >= _grid - _arm; col--) cells.Add(new Vector2Int(col, low));

            return cells.ToArray();
        }

        public static Color ColourOf(PlayerColor colour)
        {
            switch (colour)
            {
                case PlayerColor.Red: return new Color(0.84f, 0.27f, 0.31f);
                case PlayerColor.Blue: return new Color(0.32f, 0.56f, 0.88f);
                case PlayerColor.Green: return new Color(0.34f, 0.72f, 0.44f);
                case PlayerColor.Yellow: return new Color(0.90f, 0.76f, 0.30f);
                default: return Color.grey;
            }
        }
    }
}