// Assets/_Project/Scripts/Core/Board/CellRef.cs
using System;

namespace NonaRoyale.Core.Board
{
    /// <summary>
    /// Identifies a single position on the board, independent of who is standing
    /// on it. This is the type occupancy is decided with, so its equality rules
    /// are load-bearing:
    ///
    /// <list type="bullet">
    /// <item>Outer-track cells are <b>shared</b>. Two operators of different
    /// colours standing on track cell 17 hold equal <see cref="CellRef"/>s —
    /// that is exactly what makes a collision detectable.</item>
    /// <item>Home-column, yard and home cells are <b>private</b>. Red's home
    /// column cell 0 and Blue's home column cell 0 are different places and
    /// never compare equal.</item>
    /// </list>
    ///
    /// Track cells therefore carry <see cref="PlayerColor.None"/> as their owner,
    /// rather than a colour that would be misleading to read.
    /// </summary>
    public readonly struct CellRef : IEquatable<CellRef>
    {
        public CellKind Kind { get; }

        /// <summary>
        /// Track: absolute circuit index, <c>0 .. CircuitLength - 1</c>.
        /// HomeColumn: depth into the column, <c>0 .. HomeColumnLength - 1</c>,
        /// where 0 is the mouth. Yard and Home: always 0.
        /// </summary>
        public int Index { get; }

        /// <summary>Owning colour, or <see cref="PlayerColor.None"/> for shared track cells.</summary>
        public PlayerColor Owner { get; }

        private CellRef(CellKind kind, int index, PlayerColor owner)
        {
            Kind = kind;
            Index = index;
            Owner = owner;
        }

        public static CellRef Track(int index) =>
            new CellRef(CellKind.Track, index, PlayerColor.None);

        public static CellRef HomeColumn(PlayerColor owner, int depth) =>
            new CellRef(CellKind.HomeColumn, depth, owner);

        public static CellRef Yard(PlayerColor owner) =>
            new CellRef(CellKind.Yard, 0, owner);

        public static CellRef Home(PlayerColor owner) =>
            new CellRef(CellKind.Home, 0, owner);

        /// <summary>True only for the shared outer circuit — the one place collisions can occur.</summary>
        public bool IsOnTrack => Kind == CellKind.Track;

        public bool Equals(CellRef other) =>
            Kind == other.Kind && Index == other.Index && Owner == other.Owner;

        public override bool Equals(object obj) => obj is CellRef other && Equals(other);

        public override int GetHashCode()
        {
            // Hand-rolled rather than HashCode.Combine: that lives in
            // System.HashCode, which is not guaranteed across every runtime
            // this assembly is expected to compile under.
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ Index;
                hash = (hash * 397) ^ (int)Owner;
                return hash;
            }
        }

        public static bool operator ==(CellRef left, CellRef right) => left.Equals(right);
        public static bool operator !=(CellRef left, CellRef right) => !left.Equals(right);

        public override string ToString()
        {
            switch (Kind)
            {
                case CellKind.Track: return $"Track[{Index}]";
                case CellKind.HomeColumn: return $"{Owner}.Home[{Index}]";
                case CellKind.Yard: return $"{Owner}.Yard";
                case CellKind.Home: return $"{Owner}.HOME";
                default: return $"Cell({Kind},{Index},{Owner})";
            }
        }
    }
}