// Assets/_Project/Scripts/Unity/Audio/SoundMixer.cs
using UnityEngine;
using UnityEngine.Audio;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>
    /// The game's AudioMixer (AUDIO.md increment AU1f): finds its groups,
    /// routes sources into them, and sets the exposed faders from
    /// <see cref="AudioLevels"/>.
    /// </summary>
    /// <remarks>
    /// <b>The asset</b> is <c>Assets/_Project/Audio/Resources/Audio/Mixer.mixer</c>,
    /// loaded by name, so there is still no scene wiring:
    /// <code>
    /// Master            MasterVolume
    /// ├─ Music          MusicVolume
    /// ├─ Effects        EffectsVolume
    /// │  └─ Interface   (follows Effects)
    /// └─ Voice          VoiceVolume
    /// </code>
    /// <b>Only the player's levels live on the mixer.</b> Per-cue levels,
    /// crossfades, the sting dip, ducking under voices and the pause dip stay
    /// on the sources, where the director already shapes them. Snapshots and
    /// effects (reverb, a low-pass in pause) can be added in the Audio Mixer
    /// window later without touching this class, as long as the groups and
    /// the four exposed names stay.
    ///
    /// <b>Exposed faders ignore snapshots</b> once set from code, so tuning
    /// them in the Audio Mixer window during Play Mode is overridden every
    /// frame. Tune the groups' effects, or the sliders, instead.
    ///
    /// <b>Fallback.</b> If the asset is missing, or a group or a name was
    /// renamed, <see cref="Ready"/> stays false, one warning is logged, and
    /// the director goes back to multiplying source volumes (AU1).
    /// </remarks>
    public sealed class SoundMixer
    {
        public const string ResourcePath = "Audio/Mixer";

        public const string MasterVolume = "MasterVolume";
        public const string MusicVolume = "MusicVolume";
        public const string EffectsVolume = "EffectsVolume";
        public const string VoiceVolume = "VoiceVolume";

        public const string MusicPath = "Master/Music";
        public const string EffectsPath = "Master/Effects";
        public const string InterfacePath = "Master/Effects/Interface";
        public const string VoicePath = "Master/Voice";

        /// <summary>Every exposed fader the code sets.</summary>
        public static readonly string[] Parameters = { MasterVolume, MusicVolume, EffectsVolume, VoiceVolume };

        /// <summary>Every group a source is routed to.</summary>
        public static readonly string[] GroupPaths = { MusicPath, EffectsPath, InterfacePath, VoicePath };

        private AudioMixer _mixer;
        private AudioMixerGroup _music;
        private AudioMixerGroup _effects;
        private AudioMixerGroup _interface;
        private AudioMixerGroup _voice;

        /// <summary>True when the asset loaded with every group and fader.</summary>
        public bool Ready { get; private set; }

        /// <summary>Loads the asset and checks it. Returns <see cref="Ready"/>.</summary>
        public bool Load()
        {
            Ready = false;
            _mixer = Resources.Load<AudioMixer>(ResourcePath);
            if (_mixer == null)
            {
                Debug.LogWarning($"[Audio] No mixer at Resources/{ResourcePath}; using code-side levels.");
                return false;
            }

            _music = Find(_mixer, MusicPath);
            _effects = Find(_mixer, EffectsPath);
            _interface = Find(_mixer, InterfacePath);
            _voice = Find(_mixer, VoicePath);

            string missing = null;
            foreach (var path in GroupPaths)
                if (Find(_mixer, path) == null) missing = missing == null ? path : $"{missing}, {path}";
            foreach (var name in Parameters)
                if (!_mixer.GetFloat(name, out _)) missing = missing == null ? name : $"{missing}, {name}";

            if (missing != null)
            {
                Debug.LogWarning($"[Audio] The mixer is missing {missing}; using code-side levels.");
                return false;
            }

            Ready = true;
            return true;
        }

        /// <summary>Sends <paramref name="source"/> into its bus's group. Does nothing without the mixer.</summary>
        public void Route(AudioSource source, AudioBus bus)
        {
            if (!Ready || source == null) return;
            source.outputAudioMixerGroup = GroupOf(bus);
        }

        /// <summary>
        /// Sets the four faders. Called every frame: it is cheap, and a value
        /// set before the mixer has started processing can otherwise be lost.
        /// </summary>
        public void Apply(AudioLevels levels)
        {
            if (!Ready || levels == null) return;

            _mixer.SetFloat(MasterVolume, AudioLevels.Decibels(levels.MasterGain));
            _mixer.SetFloat(MusicVolume, AudioLevels.Decibels(levels.BusGain(AudioBus.Music)));
            _mixer.SetFloat(EffectsVolume, AudioLevels.Decibels(levels.BusGain(AudioBus.Sfx)));
            _mixer.SetFloat(VoiceVolume, AudioLevels.Decibels(levels.BusGain(AudioBus.Voice)));
        }

        private AudioMixerGroup GroupOf(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Music: return _music;
                case AudioBus.Voice: return _voice;
                case AudioBus.Ui: return _interface;
                default: return _effects;
            }
        }

        /// <summary>
        /// The group at exactly <paramref name="path"/>. Unity matches sub-paths
        /// loosely, so the result is filtered to the one whose name ends the path.
        /// </summary>
        public static AudioMixerGroup Find(AudioMixer mixer, string path)
        {
            if (mixer == null) return null;

            string name = path.Substring(path.LastIndexOf('/') + 1);
            var found = mixer.FindMatchingGroups(path);
            if (found == null) return null;

            foreach (var group in found)
                if (group != null && group.name == name) return group;
            return null;
        }
    }
}