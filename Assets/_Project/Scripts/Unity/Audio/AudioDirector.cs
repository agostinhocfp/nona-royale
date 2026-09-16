// Assets/_Project/Scripts/Unity/Audio/AudioDirector.cs
using UnityEngine;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>
    /// Plays every sound in the game (AUDIO.md increment AU1): effects on
    /// pooled sources, music with crossfades and a win sting, all through
    /// the code-side buses in <see cref="AudioLevels"/>.
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
    /// </remarks>
    public sealed class AudioDirector : MonoBehaviour
    {
        private const int SfxVoices = 12;
        private const int UiVoices = 2;
        private const float CrossfadeSeconds = 1.2f;
        private const float StingFadeSeconds = 0.25f;
        private const float StingReturnSeconds = 1.5f;

        /// <summary>How far an effect at the edge of the view is panned.</summary>
        private const float PanWidth = 0.4f;

        private readonly SoundBank _bank = new SoundBank();
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

            _sfx = Pool("sfx", SfxVoices, ignorePause: false);
            _ui = Pool("ui", UiVoices, ignorePause: true);
            _sfxBase = new float[SfxVoices];
            _uiBase = new float[UiVoices];

            _musicA = Source("music_a", ignorePause: true);
            _musicB = Source("music_b", ignorePause: true);
            _sting = Source("sting", ignorePause: true);

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
            source.volume = gain * _levels.Gain(ui ? AudioBus.Ui : AudioBus.Sfx);
            source.pitch = 1f + (float)(_random.NextDouble() * 2.0 - 1.0) * spec.PitchJitter;
            source.panStereo = at.HasValue ? PanOf(at.Value) : 0f;
            source.Play();
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
            UpdateMusic(dt);
            UpdateVolumes();
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
            float music = _levels.Gain(AudioBus.Music) * _stingLevel * (Paused ? AudioLevels.PausedMusic : 1f);
            _musicA.volume = music * _fadeIn;
            _musicB.volume = music * _fadeOut;
            _sting.volume = _levels.Gain(AudioBus.Music);

            // A slider moved while effects ring: follow it.
            float sfx = _levels.Gain(AudioBus.Sfx);
            for (int i = 0; i < _sfx.Length; i++)
                if (_sfx[i].isPlaying) _sfx[i].volume = _sfxBase[i] * sfx;

            float ui = _levels.Gain(AudioBus.Ui);
            for (int i = 0; i < _ui.Length; i++)
                if (_ui[i].isPlaying) _ui[i].volume = _uiBase[i] * ui;
        }

        private void OnDisable()
        {
            // Never leave the whole game's audio paused behind us.
            if (_built) AudioListener.pause = false;
        }

        // ── Building ─────────────────────────────────────────────────────

        private AudioSource[] Pool(string name, int count, bool ignorePause)
        {
            var pool = new AudioSource[count];
            for (int i = 0; i < count; i++) pool[i] = Source($"{name}_{i}", ignorePause);
            return pool;
        }

        private AudioSource Source(string name, bool ignorePause)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = ignorePause;
            source.volume = 0f;
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