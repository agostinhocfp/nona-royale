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
    public interface IPauseHost : ISettingsHost
    {
        /// <summary>One line under the title: the round and the seat to play, or the result.</summary>
        string PauseSummary { get; }

        /// <summary>Opens the setup screen for a new match (GUI increment I). The current match stays until DEAL.</summary>
        void OpenSetup();

        /// <summary>Abandons the match and returns to the title screen (GUI increment J).</summary>
        void MainMenu();

        /// <summary>Opens the operator guide over the paused match; leaving it returns to the pause menu (OPERATOR_GUIDE.md OG4).</summary>
        void OpenGuide();
    }
}
