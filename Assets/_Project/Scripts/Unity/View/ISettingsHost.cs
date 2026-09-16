// Assets/_Project/Scripts/Unity/View/ISettingsHost.cs
using NonaRoyale.Unity.Audio;
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

        /// <summary>No shake, hit-stop, hop or idle sway; shorter tweens (MO2).</summary>
        bool ReducedMotion { get; set; }

        /// <summary>How fast every seat's actions animate (MO2).</summary>
        AnimationSpeed AnimationSpeed { get; set; }

        /// <summary>Pools of light, powered-cell glow and bloom; off is the flat room (LT1).</summary>
        bool LightingEffects { get; set; }

        /// <summary>The volume settings (AU1). The sound page writes to this object directly.</summary>
        AudioLevels Audio { get; }
    }

    /// <summary>The settings pages' rows, shared by the pause menu and the title screen.</summary>
    public static class SettingsRows
    {
        public const float RowHeight = 54f;

        /// <summary>A volume row is a little shorter, so the sound page stays compact.</summary>
        public const float SliderHeight = 48f;

        /// <param name="slot">Makes a laid-out slot on the caller's card.</param>
        /// <param name="rebuild">Called after a toggle, so the page redraws.</param>
        /// <param name="openSound">Opens the sound page (AU1).</param>
        public static void Build(System.Func<string, RectTransform> slot, ISettingsHost host, System.Action rebuild,
            System.Action openSound)
        {
            var sound = UiKit.ChoiceRow(slot("cycle"), "Sound", "", SoundSummary(host.Audio), !host.Audio.Muted,
                openSound);
            UiKit.Size(sound, height: RowHeight);

            Row(slot, "Health above pieces", "H", host.ShowPieceHealth, v => host.ShowPieceHealth = v, rebuild);
            Row(slot, "Event log", "L", host.ShowFullLog, v => host.ShowFullLog = v, rebuild);
            Row(slot, "Dev panel", "Tab", host.ShowDevPanel, v => host.ShowDevPanel = v, rebuild);
            Row(slot, "Reduced motion", "", host.ReducedMotion, v => host.ReducedMotion = v, rebuild);
            Row(slot, "Lighting effects", "", host.LightingEffects, v => host.LightingEffects = v, rebuild);
            AnimationSpeedRow(slot, host, rebuild);
            CpuSpeedRow(slot, host, rebuild);
        }

        /// <summary>Animation speed: one wide button that cycles Normal and Fast.</summary>
        private static void AnimationSpeedRow(System.Func<string, RectTransform> slot, ISettingsHost host, System.Action rebuild)
        {
            var speed = host.AnimationSpeed;
            var next = speed == AnimationSpeed.Normal ? AnimationSpeed.Fast : AnimationSpeed.Normal;
            var button = UiKit.ChoiceRow(slot("cycle"), "Animation speed", "", speed.ToString().ToUpperInvariant(),
                speed != AnimationSpeed.Normal, () => { host.AnimationSpeed = next; rebuild(); });
            UiKit.Size(button, height: RowHeight);
        }

        /// <summary>CPU speed: one wide button that cycles Normal, Fast, Instant.</summary>
        private static void CpuSpeedRow(System.Func<string, RectTransform> slot, ISettingsHost host, System.Action rebuild)
        {
            var speed = host.CpuSpeed;
            var button = UiKit.ChoiceRow(slot("cycle"), "CPU speed", "hold Space", speed.ToString().ToUpperInvariant(),
                speed != BotSpeed.Normal, () => { host.CpuSpeed = NextSpeed(speed); rebuild(); });
            UiKit.Size(button, height: RowHeight);
        }

        /// <summary>
        /// The sound page (AUDIO.md decision 5): Mute, then Master, Music,
        /// Effects and Voice, then Restore defaults (AU1f). Sliders write
        /// straight into the levels and never rebuild; Mute and Restore
        /// rebuild, so the sliders dim or jump.
        /// </summary>
        public static void BuildSound(System.Func<string, RectTransform> slot, ISettingsHost host, System.Action rebuild)
        {
            var levels = host.Audio;
            bool muted = levels.Muted;

            Row(slot, "Mute", "", muted, v => levels.Muted = v, rebuild);
            Volume(slot, "Master", levels.Master, v => levels.Master = v, muted);
            Volume(slot, "Music", levels.Music, v => levels.Music = v, muted);
            Volume(slot, "Effects", levels.Sfx, v => levels.Sfx = v, muted);
            Volume(slot, "Voice", levels.Voice, v => levels.Voice = v, muted);

            var reset = UiKit.ChoiceRow(slot("cycle"), "Restore defaults", "", levels.IsDefault ? "DEFAULT" : "RESET",
                false, () => { levels.Reset(); rebuild(); });
            UiKit.Size(reset, height: SliderHeight);
        }

        /// <summary>"80%", or "MUTED".</summary>
        public static string SoundSummary(AudioLevels levels) =>
            levels.Muted ? "MUTED" : $"{AudioLevels.Percent(levels.Master)}%";

        private static void Volume(System.Func<string, RectTransform> slot, string label, float value,
            System.Action<float> set, bool dimmed)
        {
            var slider = UiKit.SliderRow(slot("slider"), label, value, set, dimmed);
            UiKit.Size(slider.transform.parent.GetComponent<RectTransform>(), height: SliderHeight);
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
        public const string ReducedMotion = "nr.settings.reducedMotion";
        public const string AnimSpeed = "nr.settings.animationSpeed";
        public const string Lighting = "nr.settings.lighting";
        public const string VolumeMaster = "nr.audio.master";
        public const string VolumeMusic = "nr.audio.music";
        public const string VolumeSfx = "nr.audio.sfx";
        public const string VolumeVoice = "nr.audio.voice";
        public const string Mute = "nr.audio.mute";

        /// <summary>The volume settings' version (<see cref="AudioLevels.Version"/>). Missing means 1.</summary>
        public const string AudioVersion = "nr.audio.version";

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

        public static AnimationSpeed LoadAnimationSpeed(AnimationSpeed fallback)
        {
            if (!PlayerPrefs.HasKey(AnimSpeed)) return fallback;

            int value = PlayerPrefs.GetInt(AnimSpeed);
            return value == (int)AnimationSpeed.Normal || value == (int)AnimationSpeed.Fast ? (AnimationSpeed)value : fallback;
        }

        /// <summary>Writes the motion settings (MO2) and flushes them to disk.</summary>
        public static void SaveMotion(bool reducedMotion, AnimationSpeed speed)
        {
            PlayerPrefs.SetInt(ReducedMotion, reducedMotion ? 1 : 0);
            PlayerPrefs.SetInt(AnimSpeed, (int)speed);
            PlayerPrefs.Save();
        }

        /// <summary>Writes the Lighting effects setting (LT1) and flushes it to disk.</summary>
        public static void SaveLighting(bool on)
        {
            PlayerPrefs.SetInt(Lighting, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Reads the volume settings into <paramref name="into"/>, keeping its
        /// values where nothing was saved. Music saved before AU1f at the old
        /// default moves to the new one (<see cref="AudioLevels.MigrateMusic"/>).
        /// </summary>
        public static void LoadAudio(AudioLevels into)
        {
            int version = PlayerPrefs.GetInt(AudioVersion, 1);
            into.Master = LoadVolume(VolumeMaster, into.Master);
            if (PlayerPrefs.HasKey(VolumeMusic))
                into.Music = AudioLevels.MigrateMusic(version, Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeMusic)));
            into.Sfx = LoadVolume(VolumeSfx, into.Sfx);
            into.Voice = LoadVolume(VolumeVoice, into.Voice);
            into.Muted = Load(Mute, into.Muted);
        }

        /// <summary>Writes the volume settings (AU1) and flushes them to disk.</summary>
        public static void SaveAudio(AudioLevels levels)
        {
            PlayerPrefs.SetFloat(VolumeMaster, levels.Master);
            PlayerPrefs.SetFloat(VolumeMusic, levels.Music);
            PlayerPrefs.SetFloat(VolumeSfx, levels.Sfx);
            PlayerPrefs.SetFloat(VolumeVoice, levels.Voice);
            PlayerPrefs.SetInt(Mute, levels.Muted ? 1 : 0);
            PlayerPrefs.SetInt(AudioVersion, AudioLevels.Version);
            PlayerPrefs.Save();
        }

        private static float LoadVolume(string key, float fallback) =>
            PlayerPrefs.HasKey(key) ? Mathf.Clamp01(PlayerPrefs.GetFloat(key)) : fallback;

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
