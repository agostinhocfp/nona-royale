// Assets/_Project/Scripts/Unity/Audio/AudioDirector.cs
using UnityEngine;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>
    /// Plays every sound in the game (AUDIO.md increment AU1): effects on
    /// pooled sources, music with crossfades and a win sting, all through
    /// the mixer's buses (<see cref="SoundMixer"/>, AU1f) set from
    /// <see cref="AudioLevels"/>.
    /// </summary>
    /// <remarks>
    /// <b>Told what to play, never deciding why.</b> The composition root
    /// calls <see cref="Play"/> from the presentation steps, so a sound lands
    /// on the same beat as the thing it belongs to, and sets
    /// <see cref="Music"/> and <see cref="Paused"/> from the app's state.
    ///
    /// <b>Pause</b> (AUDIO.md decision 3): effects stop with the game
    /// (<c>AudioListener.pause</c>); music and interface clicks ignore that
    /// pause, and the music drops to <see cref="AudioLevels.PausedMusic"/>.
    ///
    /// <b>Real time.</b> Fades and the gap between repeats use unscaled time,
    /// so a hit-stop never bends the sound.
    ///
    /// <b>Effects are panned by where they happen</b>, lightly, from the
    /// world position the caller passes. Nothing here is spatial audio.
    ///
    /// <b>Voices (AU2)</b> play one at a time on their own source, through
    /// <see cref="VoiceRules"/>, and pause with the game like effects. The
    /// rules run on a clock that stops while paused, so a line frozen mid-way
    /// still counts as speaking when play resumes. The music ducks to
    /// <see cref="AudioLevels.DuckedMusic"/> while a line plays.
    ///
    /// <b>The mixer (AU1f)</b> carries the player's levels: every source is
    /// routed to its group, and a source's own volume is only its cue level
    /// and the fades above. Without the mixer, <see cref="Level"/> folds the
    /// player's levels back into each source, as AU1 did.
    /// </remarks>
    public sealed class AudioDirector : MonoBehaviour
    {
        private const int SfxVoices = 12;
        private const int UiVoices = 2;
        private const float CrossfadeSeconds = 1.2f;
        private const float StingFadeSeconds = 0.25f;
        private const float StingReturnSeconds = 1.5f;
        private const float DuckSeconds = 0.12f;
        private const float UnduckSeconds = 0.5f;

        /// <summary>Gain of a voice line on top of the Voice bus.</summary>
        private const float VoiceGain = 1f;

        /// <summary>How far an effect at the edge of the view is panned.</summary>
        private const float PanWidth = 0.4f;

        private readonly SoundBank _bank = new SoundBank();
        private readonly SoundMixer _mixer = new SoundMixer();
        private readonly System.Random _random = new System.Random();
        private readonly float[] _lastPlayed = new float[64];

        private AudioLevels _levels = new AudioLevels();
        private AudioSource[] _sfx;
        private AudioSource[] _ui;
        private float[] _sfxBase;
        private float[] _uiBase;
        private int _nextSfx;
        private int _nextUi;

        private AudioSource _musicA;
        private AudioSource _musicB;
        private AudioSource _sting;
        private AudioSource _voice;
        private VoiceRules _voiceRules;
        private double _voiceClock;
        private float _duck = 1f;
        private AudioClip _heldClip;
        private Vector3? _heldAt;
        private MusicCue? _playing;
        private float _fadeIn = 1f;
        private float _fadeOut;
        private float _stingEnds = -1f;
        private float _stingLevel = 1f;
        private bool _built;

        /// <summary>The track that should be playing. Changing it crossfades.</summary>
        public MusicCue Music { get; set; } = MusicCue.Title;

        /// <summary>True while the pause menu is open.</summary>
        public bool Paused { get; set; }

        /// <summary>Binds the shared volume settings and builds the sources. Safe to call again.</summary>
        public void Bind(AudioLevels levels)
        {
            _levels = levels ?? _levels;
            if (_built) return;

            _built = true;
            EnsureListener();
            _mixer.Load();

            _sfx = Pool("sfx", SfxVoices, AudioBus.Sfx, ignorePause: false);
            _ui = Pool("ui", UiVoices, AudioBus.Ui, ignorePause: true);
            _sfxBase = new float[SfxVoices];
            _uiBase = new float[UiVoices];

            _musicA = Source("music_a", AudioBus.Music, ignorePause: true);
            _musicB = Source("music_b", AudioBus.Music, ignorePause: true);
            _sting = Source("sting", AudioBus.Music, ignorePause: true);
            _voice = Source("voice", AudioBus.Voice, ignorePause: false);
            _voiceRules = new VoiceRules(_random.NextDouble);

            _bank.Warm();
        }

        /// <summary>
        /// Plays a cue, panned by <paramref name="at"/> when given.
        /// Dropped if it played too recently or is not ready yet.
        /// </summary>
        public void Play(SoundCue cue, Vector3? at = null, float volume = 1f)
        {
            if (!_built) return;

            var spec = SoundBank.SpecOf(cue);
            int index = (int)cue;
            float now = Time.unscaledTime;
            if (now - _lastPlayed[index] < spec.MinGap && _lastPlayed[index] > 0f) return;

            var clip = _bank.Pick(cue, _random);
            if (clip == null) return;

            _lastPlayed[index] = now;

            bool ui = SoundBank.BusOf(cue) == AudioBus.Ui;
            var pool = ui ? _ui : _sfx;
            var bases = ui ? _uiBase : _sfxBase;
            int slot = ui ? Next(ref _nextUi, pool) : Next(ref _nextSfx, pool);

            var source = pool[slot];
            float gain = spec.Volume * volume;
            bases[slot] = gain;

            source.Stop();
            source.clip = clip;
            source.volume = gain * Level(ui ? AudioBus.Ui : AudioBus.Sfx);
            source.pitch = 1f + (float)(_random.NextDouble() * 2.0 - 1.0) * spec.PitchJitter;
            source.panStereo = at.HasValue ? PanOf(at.Value) : 0f;
            source.Play();
        }

        /// <summary>
        /// Asks for a voice line from <paramref name="speaker"/> (an operator's
        /// name). Whether it plays is up to <see cref="VoiceRules"/>.
        /// </summary>
        public void Speak(VoiceSlot slot, string speaker, Vector3? at = null)
        {
            if (!_built || string.IsNullOrEmpty(speaker)) return;

            var clip = _bank.Voice(speaker, slot, _random);
            if (clip == null) return;

            switch (_voiceRules.Request(slot, speaker, clip.length, _voiceClock))
            {
                case VoiceVerdict.Play:
                case VoiceVerdict.Interrupt:
                    StartVoice(clip, at);
                    break;

                case VoiceVerdict.Hold:
                    _heldClip = clip;
                    _heldAt = at;
                    break;
            }
        }

        /// <summary>Loads (or starts synthesizing) the voices of the operators about to play.</summary>
        public void WarmVoices(System.Collections.Generic.IEnumerable<string> names)
        {
            if (!_built || names == null) return;
            foreach (var name in names) _bank.WarmVoice(name);
        }

        /// <summary>Cuts off any voice line, playing or waiting.</summary>
        public void StopVoice()
        {
            if (!_built) return;

            _voiceRules.Stop();
            _voice.Stop();
            _heldClip = null;
        }

        /// <summary>Plays the win sting over the music, which dips and returns after it.</summary>
        public void Sting()
        {
            if (!_built) return;

            var clip = _bank.Music(MusicCue.Win);
            if (clip == null) return;

            _sting.Stop();
            _sting.clip = clip;
            _sting.loop = false;
            _sting.Play();
            _stingEnds = Time.unscaledTime + clip.length;
        }

        private void Update()
        {
            if (!_built) return;

            _bank.Pump();

            if (AudioListener.pause != Paused) AudioListener.pause = Paused;

            float dt = Time.unscaledDeltaTime;
            UpdateVoice(dt);
            UpdateMusic(dt);
            UpdateVolumes();
        }

        private void UpdateVoice(float dt)
        {
            if (!Paused) _voiceClock += dt;

            if (_heldClip != null && _voiceRules.TryRelease(_voiceClock, out _, out _))
            {
                StartVoice(_heldClip, _heldAt);
                _heldClip = null;
            }

            // Clip lengths are what the rules were told, so these agree; the
            // source is checked too, in case a line was cut off from outside.
            bool speaking = _voiceRules.Speaking(_voiceClock) && _voice.isPlaying;
            _duck = speaking
                ? Mathf.MoveTowards(_duck, AudioLevels.DuckedMusic, dt * (1f - AudioLevels.DuckedMusic) / DuckSeconds)
                : Mathf.MoveTowards(_duck, 1f, dt * (1f - AudioLevels.DuckedMusic) / UnduckSeconds);
        }

        private void StartVoice(AudioClip clip, Vector3? at)
        {
            _voice.Stop();
            _voice.clip = clip;
            _voice.pitch = 1f;
            _voice.panStereo = at.HasValue ? PanOf(at.Value) : 0f;
            _voice.volume = VoiceGain * Level(AudioBus.Voice);
            _voice.Play();
        }

        private void UpdateMusic(float dt)
        {
            // A new track, once its clip exists: the old one fades out on B.
            if (_playing != Music)
            {
                var clip = _bank.Music(Music);
                if (clip != null)
                {
                    if (_musicA.isPlaying)
                    {
                        (_musicA, _musicB) = (_musicB, _musicA);
                        _fadeOut = _fadeIn;
                    }

                    _musicA.clip = clip;
                    _musicA.loop = MusicRecipes.Loops(Music);
                    _musicA.Play();
                    _playing = Music;
                    _fadeIn = 0f;
                }
            }

            _fadeIn = Mathf.MoveTowards(_fadeIn, 1f, dt / CrossfadeSeconds);
            _fadeOut = Mathf.MoveTowards(_fadeOut, 0f, dt / CrossfadeSeconds);
            if (_fadeOut <= 0f && _musicB.isPlaying) _musicB.Stop();

            // The sting takes the floor, then hands it back slowly.
            bool stinging = _stingEnds > 0f && Time.unscaledTime < _stingEnds;
            if (!stinging) _stingEnds = -1f;
            _stingLevel = stinging
                ? Mathf.MoveTowards(_stingLevel, 0f, dt / StingFadeSeconds)
                : Mathf.MoveTowards(_stingLevel, 1f, dt / StingReturnSeconds);
        }

        private void UpdateVolumes()
        {
            _mixer.Apply(_levels);

            float music = Level(AudioBus.Music) * _stingLevel * _duck *
                          (Paused ? AudioLevels.PausedMusic : 1f);
            _musicA.volume = music * _fadeIn;
            _musicB.volume = music * _fadeOut;
            _sting.volume = Level(AudioBus.Music);

            // Without the mixer, a slider moved while effects ring: follow it.
            if (_mixer.Ready) return;

            float sfx = Level(AudioBus.Sfx);
            for (int i = 0; i < _sfx.Length; i++)
                if (_sfx[i].isPlaying) _sfx[i].volume = _sfxBase[i] * sfx;

            if (_voice.isPlaying) _voice.volume = VoiceGain * Level(AudioBus.Voice);

            float ui = Level(AudioBus.Ui);
            for (int i = 0; i < _ui.Length; i++)
                if (_ui[i].isPlaying) _ui[i].volume = _uiBase[i] * ui;
        }

        /// <summary>
        /// The player's level a source on <paramref name="bus"/> carries
        /// itself: none with the mixer, which applies it on the group; the
        /// whole of it without.
        /// </summary>
        private float Level(AudioBus bus) => _mixer.Ready ? 1f : _levels.Gain(bus);

        private void OnDisable()
        {
            // Never leave the whole game's audio paused behind us.
            if (_built) AudioListener.pause = false;
        }

        // ── Building ─────────────────────────────────────────────────────

        private AudioSource[] Pool(string name, int count, AudioBus bus, bool ignorePause)
        {
            var pool = new AudioSource[count];
            for (int i = 0; i < count; i++) pool[i] = Source($"{name}_{i}", bus, ignorePause);
            return pool;
        }

        private AudioSource Source(string name, AudioBus bus, bool ignorePause)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = ignorePause;
            source.volume = 0f;
            _mixer.Route(source, bus);
            return source;
        }

        /// <summary>A free source, or the one that started longest ago.</summary>
        private static int Next(ref int cursor, AudioSource[] pool)
        {
            for (int k = 0; k < pool.Length; k++)
            {
                int i = (cursor + k) % pool.Length;
                if (pool[i].isPlaying) continue;

                cursor = (i + 1) % pool.Length;
                return i;
            }

            int steal = cursor;
            cursor = (cursor + 1) % pool.Length;
            return steal;
        }

        private static float PanOf(Vector3 at)
        {
            var camera = Camera.main;
            if (camera == null || !camera.orthographic) return 0f;

            float half = Mathf.Max(0.01f, camera.orthographicSize * camera.aspect);
            return Mathf.Clamp((at.x - camera.transform.position.x) / half, -1f, 1f) * PanWidth;
        }

        /// <summary>One listener in the scene, or nothing is heard. The template camera usually has it.</summary>
        private void EnsureListener()
        {
            if (FindAnyObjectByType<AudioListener>() != null) return;
            gameObject.AddComponent<AudioListener>();
        }
    }
}