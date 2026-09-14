// Assets/_Project/Scripts/Core/LandingPreview.cs
namespace NonaRoyale.Core
{
    /// <summary>
    /// One thing a player could do with the dice they are holding: move this
    /// operator, with this die (or all of them), and end up here.
    /// </summary>
    /// <remarks>
    /// <b>This exists because splitting made the old answer a lie.</b>
    /// <c>PreviewLandings</c> used to return one landing per operator, which was
    /// complete when a roll could only be spent one way. It no longer is: a
    /// two-die roll offers each operator a pooled move and a move per distinct
    /// face, and a preview that shows only the pooled one hides exactly the
    /// choice the player is being asked to make.
    ///
    /// It reports where a move <i>lands</i>, not what happens there. Collision
    /// stays out, for the same reason it always did — showing the outcome of a
    /// fight before the player commits to it is not a preview, it is the answer.
    /// </remarks>
    public readonly struct LandingPreview
    {
        public LandingPreview(int operatorId, int? dieFace, int progress, int cells)
        {
            OperatorId = operatorId;
            DieFace = dieFace;
            Progress = progress;
            Cells = cells;
        }

        public int OperatorId { get; }

        /// <summary>The die this option spends, or null if it spends every unspent die.</summary>
        public int? DieFace { get; }

        /// <summary>Progress the operator would hold after the move.</summary>
        public int Progress { get; }

        /// <summary>
        /// Cells travelled. Worth showing next to the option: it is where the
        /// cost of splitting becomes visible, and it is not recoverable from
        /// the die face without redoing the speed arithmetic.
        /// </summary>
        public int Cells { get; }

        /// <summary>True if this option spends the whole roll at once.</summary>
        public bool IsPooled => DieFace == null;

        public override string ToString() =>
            IsPooled
                ? $"op {OperatorId}: pool -> {Progress} ({Cells} cells)"
                : $"op {OperatorId}: die {DieFace} -> {Progress} ({Cells} cells)";
    }
}