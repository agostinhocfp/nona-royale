// Assets/_Project/Scripts/Unity/View/IPauseHost.cs
namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// What the pause menu may read and change. The composition root
    /// implements it (GUI increment H).
    /// </summary>
    /// <remarks>
    /// Same shape as <see cref="IControlPanelHost"/>: the menu owns no
    /// settings and no match. It shows a snapshot and reports intents, so
    /// the H, L and Tab keys and the menu's toggles change the same flags.
    /// </remarks>
    public interface IPauseHost
    {
        /// <summary>One line under the title: the round and the seat to play, or the result.</summary>
        string PauseSummary { get; }

        bool ShowPieceHealth { get; set; }
        bool ShowFullLog { get; set; }
        bool ShowDevPanel { get; set; }

        /// <summary>Throws the match away and deals a new one with the same settings.</summary>
        void Restart();

        /// <summary>Leaves the game. Stops Play Mode in the editor.</summary>
        void Quit();
    }
}
