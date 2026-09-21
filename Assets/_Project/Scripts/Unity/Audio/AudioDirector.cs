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
    ///
    /// <b>Ability signatures (AU3)</b> play through <see cref="PlaySignature"/>,
    /// which reports whether it found a clip so the caller can fall back to
    /// the generic cue. <see cref="Hush"/> drops the whole mix for a moment,
    /// before an execute lands: effects and the voice stop, the music dips
    /// and comes back slowly, and whatever plays after the hush plays at full.
    ///
    /// <b>Playlists (AU4).</b> A cue with several tracks (the match has five)
    /// plays each take through to its own ending, then, after a breath of
    /// <see cref="TrackGapSeconds"/>, the next one from a shuffle bag
    /// (<see cref="MusicPlaylist"/>), never the same twice in a row. A cue
    /// with one track loops it, as before.
    /// </remarks>
    public sealed class AudioDirector : MonoBehaviour
    {
        private const int SfxVoices = 12;
        private const int UiVoices = 2;
        private const float CrossfadeSeconds = 1.2f;

        /// <summary>Fade-in when a track starts from silence; slower than a crossfade so the music never just starts.</summary>
        private const float OpeningFadeSeconds = 2.5f;
        private const float StingFadeSeconds = 0.25f;
        private const float StingReturnSeconds = 1.5f;
        private const float DuckSeconds = 0.12f;
        private const float UnduckSeconds = 0.5f;

        /// <summary>The silence between one track of a playlist ending and the next starting, in real seconds.</summary>
        private const float TrackGapSeconds = 1.5f;

        /// <summary>
        /// The fade-in on a playlist's next track: short, since the takes
        /// open on their own intros and follow silence.
        /// </summary>
        private const float TrackFadeSeconds = 0.25f;

        /// <summary>How fast the music glides to its pause level and back.</summary>
        private const float PauseDipSeconds = 0.4f;

        /// <summary>How fast a hush takes the mix down: quick, but not a click.</summary>
        private const float HushFadeSeconds = 0.03f;

        /// <summary>How long the music takes to come back after a hush.</summary>
        private const float HushReturnSeconds = 0.8f;

        /// <summary>Gain of a voice line on top of the Voice bus.</summary>
        private const float VoiceGain = 1f;

        /// <summary>How far an effect at the edge of the view is panned.</summary>
        private const float PanWidth = 0.4f;

        private readonly SoundBank _bank = new SoundBank();
        private readonly SoundMixer _mixer = new SoundMixer();
        private readonly System.Random _random = new System.Random();
        private readonly float[] _lastPlayed = new float[64];
        private readonly System.Collections.Generic.Dictionary<string, float> _lastSignature =
            new System.Collections.Generic.Dictionary<string, float>();

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
        private readonly MusicPlaylist _playlist = new MusicPlaylist();
        private float _nextTrackAt = -1f;
        private float _fadeIn = 1f;
        private float _fadeInSeconds = OpeningFadeSeconds;
        private float _fadeOut;
        private float _pauseDip = 1f;
        private float _stingEnds = -1f;
        private float _stingLevel = 1f;
        private float _hushEnds = -1f;
        private float _hushFast = 1f;
        private float _hushMusic = 1f;
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
            Emit(clip, spec, volume, SoundBank.BusOf(cue) == AudioBus.Ui, at);
        }

        /// <summary>
        /// Plays an ability's signature for <paramref name="moment"/>, panned
        /// by <paramref name="at"/> (AU3).
        /// </summary>
        /// <returns>
        /// True if the ability has a sound for the moment, whether it played
        /// now or was held back because it played a moment ago. False when it
        /// has none (or its stand-in is not built yet): the caller plays the
        /// generic cue instead.
        /// </returns>
        public bool PlaySignature(string slug, SignatureMoment moment, Vector3? at = null)
        {
            if (!_built || string.IsNullOrEmpty(slug)) return false;

            var clip = _bank.Signature(slug, moment, _random);
            if (clip == null) return false;

            var spec = SoundBank.SpecOf(moment);
            string key = AbilitySounds.Key(slug, moment);
            float now = Time.unscaledTime;
            if (_lastSignature.TryGetValue(key, out float last) && now - last < spec.MinGap) return true;

            _lastSignature[key] = now;
            Emit(clip, spec, 1f, ui: false, at);
            return true;
        }

        /// <summary>Loads (or starts synthesizing) the signatures of the operators about to play.</summary>
        public void WarmSignatures(System.Collections.Generic.IEnumerable<string> slugs)
        {
            if (!_built || slugs == null) return;
            foreach (var slug in slugs) _bank.WarmSignature(slug);
        }

        /// <summary>
        /// Drops the whole mix: the silence before an execute (AU3). Effects
        /// and the voice line stop; the music dips. It lasts until
        /// <see cref="ReleaseHush"/>, or at most <paramref name="maxSeconds"/>
        /// of real time if nothing releases it.
        /// </summary>
        /// <remarks>
        /// Released by the caller rather than timed here, because the step
        /// that follows runs on the presentation queue's clock, which the
        /// animation speed and the hurry key stretch: a timed hush could still
        /// be silencing the blow it was meant to set up.
        /// </remarks>
        public void Hush(float maxSeconds)
        {
            if (!_built || maxSeconds <= 0f) return;
            _hushEnds = Time.unscaledTime + maxSeconds;
        }

        /// <summary>
        /// Lifts a hush at once: what plays next plays at full, and the music
        /// returns over <see cref="HushReturnSeconds"/>. Harmless with no hush.
        /// </summary>
        public void ReleaseHush()
        {
            if (!_built) return;
            _hushEnds = -1f;
            _hushFast = 1f;
        }

        private void Emit(AudioClip clip, SoundBank.CueSpec spec, float volume, bool ui, Vector3? at)
        {
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
            UpdateHush(dt);
            UpdateVoice(dt);
            UpdateMusic(dt);
            UpdateVolumes();
        }

        private void UpdateHush(float dt)
        {
            if (Time.unscaledTime < _hushEnds)
            {
                _hushFast = Mathf.MoveTowards(_hushFast, 0f, dt / HushFadeSeconds);
                _hushMusic = Mathf.MoveTowards(_hushMusic, 0f, dt / HushFadeSeconds);

                // Down: stop what was ringing, so nothing resumes when the hush lifts.
                if (_hushFast <= 0f)
                {
                    foreach (var source in _sfx) if (source.isPlaying) source.Stop();
                    if (_voice.isPlaying) StopVoice();
                }

                return;
            }

            // The blow after the hush plays at full at once: easing effects
            // back in would soften its attack. Only the music takes its time.
            _hushFast = 1f;
            _hushMusic = Mathf.MoveTowards(_hushMusic, 1f, dt / HushReturnSeconds);
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
            // A new cue, once a clip for it exists: the old one fades out on B.
            if (_playing != Music)
            {
                StartTrack(Music, afterGap: false);
            }
            else if (!_musicA.loop && !_musicA.isPlaying && _bank.MusicTracks(Music).Count > 1)
            {
                // A playlist track has ended on its own: a breath, then the next.
                if (_nextTrackAt < 0f) _nextTrackAt = Time.unscaledTime + TrackGapSeconds;
                else if (Time.unscaledTime >= _nextTrackAt) StartTrack(Music, afterGap: true);
            }

            _fadeIn = Mathf.MoveTowards(_fadeIn, 1f, dt / _fadeInSeconds);
            _fadeOut = Mathf.MoveTowards(_fadeOut, 0f, dt / CrossfadeSeconds);
            if (_fadeOut <= 0f && _musicB.isPlaying) _musicB.Stop();

            // The pause dip glides rather than steps.
            _pauseDip = Mathf.MoveTowards(_pauseDip, Paused ? AudioLevels.PausedMusic : 1f, dt / PauseDipSeconds);

            // The sting takes the floor, then hands it back slowly.
            bool stinging = _stingEnds > 0f && Time.unscaledTime < _stingEnds;
            if (!stinging) _stingEnds = -1f;
            _stingLevel = stinging
                ? Mathf.MoveTowards(_stingLevel, 0f, dt / StingFadeSeconds)
                : Mathf.MoveTowards(_stingLevel, 1f, dt / StingReturnSeconds);
        }

        /// <summary>
        /// Starts a track of <paramref name="cue"/> on source A: the only one,
        /// or the playlist's next. Does nothing while the cue has no clip yet.
        /// </summary>
        /// <param name="afterGap">True for a playlist's next track, which follows silence.</param>
        private void StartTrack(MusicCue cue, bool afterGap)
        {
            var tracks = _bank.MusicTracks(cue);
            if (tracks.Count == 0) return;

            var clip = tracks.Count == 1 ? tracks[0] : tracks[_playlist.Next(tracks.Count, _random)];

            // From silence a cue opens slowly; under a playing track it
            // crossfades; after a playlist's gap the take's own intro leads.
            bool under = _musicA.isPlaying;
            _fadeInSeconds = afterGap ? TrackFadeSeconds : under ? CrossfadeSeconds : OpeningFadeSeconds;
            if (under)
            {
                (_musicA, _musicB) = (_musicB, _musicA);
                _fadeOut = _fadeIn;
            }

            _musicA.clip = clip;
            _musicA.loop = MusicRecipes.Loops(cue) && tracks.Count == 1;
            _musicA.Play();
            _playing = cue;
            _fadeIn = 0f;
            _nextTrackAt = -1f;
        }

        private void UpdateVolumes()
        {
            _mixer.Apply(_levels);

            float music = Level(AudioBus.Music) * _stingLevel * _duck * _pauseDip * Fade(_hushMusic);
            _musicA.volume = music * Fade(_fadeIn);
            _musicB.volume = music * Fade(_fadeOut);
            _sting.volume = Level(AudioBus.Music);

            // Effects and the voice follow a hush, and without the mixer a
            // slider moved while they ring. With the mixer and no hush, this
            // writes the volumes they already have.
            float sfx = Level(AudioBus.Sfx) * _hushFast;
            for (int i = 0; i < _sfx.Length; i++)
                if (_sfx[i].isPlaying) _sfx[i].volume = _sfxBase[i] * sfx;

            if (_voice.isPlaying) _voice.volume = VoiceGain * Level(AudioBus.Voice) * _hushFast;

            if (_mixer.Ready) return;

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

        /// <summary>
        /// Smoothstep: a fade that eases off at both ends, so it never
        /// starts or stops as abruptly as a linear ramp sounds.
        /// </summary>
        private static float Fade(float t) => t * t * (3f - 2f * t);

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

        /// <summary>
        /// How far left or right of the middle of the screen a world point
        /// sounds.
        /// </summary>
        /// <remarks>
        /// <b>Measured on screen, not in the world</b> (VISUAL_PASS.md, V1).
        /// This used to divide the world offset by the orthographic half-width
        /// and gave up on any other camera, so under the tilted view every
        /// sound played dead centre. The viewport is the same measure for both
        /// cameras — for an orthographic one it works out to exactly the old
        /// arithmetic — and it is what the player sees, which is what panning
        /// is meant to follow.
        /// </remarks>
        private static float PanOf(Vector3 at)
        {
            var camera = Camera.main;
            if (camera == null) return 0f;

            var view = camera.WorldToViewportPoint(at);

            // Behind the lens there is no left or right to speak of.
            if (view.z <= 0f) return 0f;

            return Mathf.Clamp((view.x - 0.5f) * 2f, -1f, 1f) * PanWidth;
        }

        /// <summary>One listener in the scene, or nothing is heard. The template camera usually has it.</summary>
        private void EnsureListener()
        {
            if (FindAnyObjectByType<AudioListener>() != null) return;
            gameObject.AddComponent<AudioListener>();
        }
    }
}