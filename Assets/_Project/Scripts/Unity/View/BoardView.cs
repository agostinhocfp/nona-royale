// // Assets/_Project/Scripts/Unity/View/BoardView.cs
// using System.Collections.Generic;
// using NonaRoyale.Core.Board;
// using UnityEngine;

// namespace NonaRoyale.Unity.View
// {
//     /// <summary>
//     /// Draws the cells. Static: it renders the board's shape and never changes
//     /// after construction, because the board itself never changes.
//     /// </summary>
//     public sealed class BoardView : MonoBehaviour
//     {
//         private readonly List<SpriteRenderer> _cells = new List<SpriteRenderer>();

//         public void Build(PathMap map, BoardLayout layout)
//         {
//             foreach (var cell in _cells)
//                 if (cell != null) Destroy(cell.gameObject);

//             _cells.Clear();

//             var profile = map.Profile;

//             for (int i = 0; i < profile.CircuitLength; i++)
//             {
//                 var cell = CellRef.Track(i);

//                 // Safe cells are drawn brighter. They are the only cells with a
//                 // rule attached that a player must be able to see at a glance.
//                 Spawn($"cell_{i}", layout.PositionOf(cell), layout.CellSize,
//                     map.IsSafe(cell) ? new Color(0.95f, 0.9f, 0.6f) : new Color(0.28f, 0.28f, 0.32f));
//             }

//             foreach (PlayerColor colour in new[]
//                      { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow })
//             {
//                 var tint = BoardLayout.ColourOf(colour);

//                 for (int depth = 0; depth < profile.HomeColumnLength; depth++)
//                 {
//                     Spawn($"home_{colour}_{depth}",
//                         layout.PositionOf(CellRef.HomeColumn(colour, depth)),
//                         layout.CellSize * 0.85f, tint * 0.55f);
//                 }

//                 // Kept dim and slightly smaller than before: a yard is a holding
//                 // area, and at 2.4x it overlapped the track cells beside it.
//                 Spawn($"yard_{colour}", layout.PositionOf(CellRef.Yard(colour)),
//                     layout.CellSize * 1.9f, tint * 0.25f);
//             }

//             Spawn("home", Vector3.zero, layout.CellSize * 1.6f, new Color(0.9f, 0.85f, 0.55f));
//         }

//         private void Spawn(string name, Vector3 position, float size, Color colour)
//         {
//             var go = new GameObject(name);
//             go.transform.SetParent(transform, false);
//             go.transform.position = position;
//             go.transform.localScale = Vector3.one * size;

//             var renderer = go.AddComponent<SpriteRenderer>();
//             renderer.sprite = Primitives.Disc;
//             renderer.color = colour;
//             renderer.sortingOrder = 0;

//             _cells.Add(renderer);
//         }
//     }
// }

// Assets/_Project/Scripts/Unity/View/BoardView.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// Draws the board once. It renders shape, never state, because the board
    /// itself never changes during a match.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private static readonly PlayerColor[] Seats =
        {
            PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow
        };

        private readonly List<GameObject> _drawn = new List<GameObject>();

        public void Build(PathMap map, BoardLayout layout)
        {
            foreach (var go in _drawn)
                if (go != null) Destroy(go);

            _drawn.Clear();

            var profile = map.Profile;

            // The shared track. Safe cells are drawn bright because they are the
            // only cells carrying a rule a player must see without asking.
            for (int i = 0; i < profile.CircuitLength; i++)
            {
                var cell = CellRef.Track(i);

                Spawn($"track_{i}", layout.PositionOf(cell), layout.CellSize,
                    map.IsSafe(cell) ? new Color(0.93f, 0.88f, 0.62f) : new Color(0.24f, 0.24f, 0.28f));
            }

            foreach (var seat in Seats)
            {
                var tint = BoardLayout.ColourOf(seat);

                // Home column, tip to centre. Deepening tint gives a direction
                // of travel without needing arrows.
                for (int depth = 0; depth < profile.HomeColumnLength; depth++)
                {
                    float t = depth / Mathf.Max(1f, profile.HomeColumnLength - 1f);

                    Spawn($"home_{seat}_{depth}",
                        layout.PositionOf(CellRef.HomeColumn(seat, depth)),
                        layout.CellSize, Color.Lerp(tint * 0.45f, tint * 0.85f, t));
                }

                // Yard: a large disc in the quadrant beside its arm.
                SpawnDisc($"yard_{seat}", layout.PositionOf(CellRef.Yard(seat)),
                    layout.CellSize * 3.2f, tint * 0.22f);
            }

            SpawnDisc("home_goal", layout.HomeGoalPosition, layout.HomeGoalSize,
                new Color(0.88f, 0.83f, 0.55f));
        }

        private void Spawn(string name, Vector3 position, float size, Color colour) =>
            Create(name, position, size, colour, Primitives.Square, -1);

        private void SpawnDisc(string name, Vector3 position, float size, Color colour) =>
            Create(name, position, size, colour, Primitives.Disc, -2);

        private void Create(string name, Vector3 position, float size, Color colour, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = colour;
            renderer.sortingOrder = order;

            _drawn.Add(go);
        }
    }
}