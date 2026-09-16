// Assets/_Project/Scripts/Unity/View/ISettingsHost.cs
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>How fast CPU seats act (BOTS.md decision 8). Remembered between sessions.</summary>
    public enum BotSpeed
    {
        Normal = 0,
        Fast = 1,
        Instant = 2
    }

    /// <summary>
    /// The player's display settings, as the settings pages see them (GUI
    /// increment J). The pause menu and the title screen share it.
    /// </summary>
    public interface ISettingsHost
    {
        bool ShowPieceHealth { get; set; }
        bool ShowFullLog { get; set; }
        bool ShowDevPanel { get; set; }

        /// <summary>How fast CPU seats act (BOT3).</summary>
        BotSpeed CpuSpeed { get; set; }
    }

    /// <summary>The settings page's rows, shared by the pause menu and the title screen.</summary>
    public static class SettingsRows
    {
        public const float RowHeight = 54f;

        /// <param name="slot">Makes a laid-out slot on the caller's card.</param>
        /// <param name="rebuild">Called after a toggle, so the page redraws.</param>
        public static void Build(System.Func<string, RectTransform> slot, ISettingsHost host, System.Action rebuild)
        {
            Row(slot, "Health above pieces", "H", host.ShowPieceHealth, v => host.ShowPieceHealth = v, rebuild);
            Row(slot, "Event log", "L", host.ShowFullLog, v => host.ShowFullLog = v, rebuild);
            Row(slot, "Dev panel", "Tab", host.ShowDevPanel, v => host.ShowDevPanel = v, rebuild);
            CpuSpeedRow(slot, host, rebuild);
        }

        /// <summary>CPU speed: one wide button that cycles Normal, Fast, Instant.</summary>
        private static void CpuSpeedRow(System.Func<string, RectTransform> slot, ISettingsHost host, System.Action rebuild)
        {
            var speed = host.CpuSpeed;
            var button = UiKit.ChoiceRow(slot("cycle"), "CPU speed", "hold Space", speed.ToString().ToUpperInvariant(),
                speed != BotSpeed.Normal, () => { host.CpuSpeed = NextSpeed(speed); rebuild(); });
            UiKit.Size(button, height: RowHeight);
        }

        public static BotSpeed NextSpeed(BotSpeed speed) =>
            speed == BotSpeed.Normal ? BotSpeed.Fast
            : speed == BotSpeed.Fast ? BotSpeed.Instant
            : BotSpeed.Normal;

        private static void Row(System.Func<string, RectTransform> slot, string label, string key, bool on,
            System.Action<bool> set, System.Action rebuild)
        {
            var button = UiKit.ToggleRow(slot("toggle"), label, key, on, () => { set(!on); rebuild(); });
            UiKit.Size(button, height: RowHeight);
        }
    }

    /// <summary>
    /// Remembers the display settings between sessions, in <c>PlayerPrefs</c>
    /// (decided 2026-09-16).
    /// </summary>
    /// <remarks>
    /// A missing key means "never saved", and the caller's default (the
    /// inspector value) stands. Keys are namespaced so a later settings
    /// screen can add to them without colliding.
    /// </remarks>
    public static class SettingsStore
    {
        public const string PieceHealth = "nr.settings.pieceHealth";
        public const string FullLog = "nr.settings.fullLog";
        public const string DevPanel = "nr.settings.devPanel";
        public const string CpuSpeed = "nr.settings.cpuSpeed";

        public static BotSpeed LoadSpeed(BotSpeed fallback)
        {
            if (!PlayerPrefs.HasKey(CpuSpeed)) return fallback;

            int value = PlayerPrefs.GetInt(CpuSpeed);
            return value >= (int)BotSpeed.Normal && value <= (int)BotSpeed.Instant ? (BotSpeed)value : fallback;
        }

        public static void SaveSpeed(BotSpeed speed)
        {
            PlayerPrefs.SetInt(CpuSpeed, (int)speed);
            PlayerPrefs.Save();
        }

        public static bool Load(string key, bool fallback) =>
            PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) != 0 : fallback;

        /// <summary>Writes the three flags at once and flushes them to disk.</summary>
        public static void Save(bool pieceHealth, bool fullLog, bool devPanel)
        {
            PlayerPrefs.SetInt(PieceHealth, pieceHealth ? 1 : 0);
            PlayerPrefs.SetInt(FullLog, fullLog ? 1 : 0);
            PlayerPrefs.SetInt(DevPanel, devPanel ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
