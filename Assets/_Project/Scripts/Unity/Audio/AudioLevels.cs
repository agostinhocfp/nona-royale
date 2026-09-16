// Assets/_Project/Scripts/Unity/Audio/AudioLevels.cs
using System;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>The code-side mixer buses (AUDIO.md decision 2).</summary>
    public enum AudioBus
    {
        Music,
        Sfx,
        Voice,

        /// <summary>Button clicks. Follows the SFX slider; keeps playing in pause.</summary>
        Ui,
    }

    /// <summary>
    /// The player's volume settings and what they add up to per bus
    /// (AUDIO.md decisions 2 and 5). Remembered between sessions.
    /// </summary>
    /// <remarks>
    /// <b>Plain C#.</b> The director multiplies every source by
    /// <see cref="Gain"/>; there is no AudioMixer asset. Sliders are linear
    /// 0–1 and are mapped onto a perceptual curve here, so the middle of a
    /// slider sounds like the middle.
    /// </remarks>
    public sealed class AudioLevels
    {
        public const float DefaultMaster = 0.8f;
        public const float DefaultMusic = 0.6f;
        public const float DefaultSfx = 0.8f;
        public const float DefaultVoice = 0.8f;

        /// <summary>Music level while the pause menu is open.</summary>
        public const float PausedMusic = 0.35f;

        /// <summary>Music level while a voice line plays.</summary>
        public const float DuckedMusic = 0.45f;

        public float Master { get; set; } = DefaultMaster;
        public float Music { get; set; } = DefaultMusic;
        public float Sfx { get; set; } = DefaultSfx;
        public float Voice { get; set; } = DefaultVoice;
        public bool Muted { get; set; }

        /// <summary>The slider value for a bus. UI shares the SFX slider.</summary>
        public float SliderOf(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Music: return Music;
                case AudioBus.Voice: return Voice;
                default: return Sfx;
            }
        }

        /// <summary>The linear gain a source on <paramref name="bus"/> plays at: master times bus, on a perceptual curve.</summary>
        public float Gain(AudioBus bus)
        {
            if (Muted) return 0f;
            return Curve(Master) * Curve(SliderOf(bus));
        }

        /// <summary>Copies every value from <paramref name="other"/>.</summary>
        public void CopyFrom(AudioLevels other)
        {
            Master = other.Master;
            Music = other.Music;
            Sfx = other.Sfx;
            Voice = other.Voice;
            Muted = other.Muted;
        }

        public bool SameAs(AudioLevels other) =>
            Master == other.Master && Music == other.Music && Sfx == other.Sfx &&
            Voice == other.Voice && Muted == other.Muted;

        /// <summary>
        /// A slider position as gain: squared, which is close to how loudness
        /// is heard, and exactly 0 and 1 at the ends.
        /// </summary>
        public static float Curve(float slider)
        {
            float s = Math.Clamp(slider, 0f, 1f);
            return s * s;
        }

        /// <summary>A slider value as the percentage the settings row shows.</summary>
        public static int Percent(float slider) => (int)MathF.Round(Math.Clamp(slider, 0f, 1f) * 100f);
    }
}