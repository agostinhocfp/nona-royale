// Assets/_Project/Scripts/Unity/Audio/AudioLevels.cs
using System;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>The buses, one per group of the mixer (AUDIO.md decision 2).</summary>
    public enum AudioBus
    {
        Music,
        Sfx,
        Voice,

        /// <summary>Button clicks. Follows the Effects slider; keeps playing in pause.</summary>
        Ui,
    }

    /// <summary>
    /// The player's volume settings and what they add up to per bus
    /// (AUDIO.md decisions 2 and 5). Remembered between sessions.
    /// </summary>
    /// <remarks>
    /// <b>Plain C#.</b> Sliders are linear 0–1 and are mapped onto a
    /// perceptual curve here, so the middle of a slider sounds like the
    /// middle. With the mixer loaded (AU1f), each curve value becomes a
    /// group's fader in decibels (<see cref="Decibels"/>); without it, the
    /// director multiplies every source by <see cref="Gain"/> instead.
    /// </remarks>
    public sealed class AudioLevels
    {
        public const float DefaultMaster = 0.8f;

        /// <summary>
        /// 36%, asked for by the designer 2026-09-17: the score still sat on
        /// top of the effects at the AU1f default.
        /// </summary>
        public const float DefaultMusic = 0.36f;

        public const float DefaultSfx = 0.8f;
        public const float DefaultVoice = 0.8f;

        /// <summary>The Music default before 2026-09-17 (AU1f's). A saved value equal to it was never chosen.</summary>
        public const float FormerDefaultMusic = 0.42f;

        /// <summary>The first Music default (AU1). Saves that skipped AU1f can still hold it.</summary>
        public const float OriginalDefaultMusic = 0.6f;

        /// <summary>
        /// The version of the saved settings. 1 was AU1 (unversioned);
        /// 2 was AU1f (42%); 3 is the 36% music default.
        /// </summary>
        public const int Version = 3;

        /// <summary>A fader's floor, and what Mute sets Master to. Unity's mixer bottoms out here.</summary>
        public const float SilentDecibels = -80f;

        /// <summary>Music level while the pause menu is open.</summary>
        public const float PausedMusic = 0.35f;

        /// <summary>Music level while a voice line plays.</summary>
        public const float DuckedMusic = 0.45f;

        public float Master { get; set; } = DefaultMaster;
        public float Music { get; set; } = DefaultMusic;
        public float Sfx { get; set; } = DefaultSfx;
        public float Voice { get; set; } = DefaultVoice;
        public bool Muted { get; set; }

        /// <summary>The Master fader's gain: its curve, or 0 when muted.</summary>
        public float MasterGain => Muted ? 0f : Curve(Master);

        /// <summary>True when every value is the default, so Restore defaults has nothing to do.</summary>
        public bool IsDefault =>
            Master == DefaultMaster && Music == DefaultMusic && Sfx == DefaultSfx &&
            Voice == DefaultVoice && !Muted;

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

        /// <summary>The bus's own fader gain, without Master.</summary>
        public float BusGain(AudioBus bus) => Curve(SliderOf(bus));

        /// <summary>The linear gain a source on <paramref name="bus"/> plays at: master times bus, on a perceptual curve.</summary>
        public float Gain(AudioBus bus) => MasterGain * BusGain(bus);

        /// <summary>Back to the defaults, unmuted.</summary>
        public void Reset()
        {
            Master = DefaultMaster;
            Music = DefaultMusic;
            Sfx = DefaultSfx;
            Voice = DefaultVoice;
            Muted = false;
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

        /// <summary>
        /// A linear gain as a mixer fader: 0 dB at 1, and
        /// <see cref="SilentDecibels"/> at 0.0001 (-80 dB) or below.
        /// </summary>
        public static float Decibels(float gain)
        {
            if (!(gain > 0.0001f)) return SilentDecibels; // also catches NaN
            return Math.Clamp(20f * MathF.Log10(gain), SilentDecibels, 0f);
        }

        /// <summary>
        /// The Music value to use for one saved under settings version
        /// <paramref name="savedVersion"/>. A value saved under an older
        /// version that still equals either earlier default was never picked
        /// by the player, so it moves to the new default; anything the
        /// player set stays.
        /// </summary>
        public static float MigrateMusic(int savedVersion, float savedMusic)
        {
            if (savedVersion >= Version) return savedMusic;
            return Math.Abs(savedMusic - FormerDefaultMusic) < 0.005f ||
                   Math.Abs(savedMusic - OriginalDefaultMusic) < 0.005f
                ? DefaultMusic
                : savedMusic;
        }

        /// <summary>A slider value as the percentage the settings row shows.</summary>
        public static int Percent(float slider) => (int)MathF.Round(Math.Clamp(slider, 0f, 1f) * 100f);
    }
}