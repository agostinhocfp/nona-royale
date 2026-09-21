// Assets/_Project/Scripts/Unity/Audio/SoundBank.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>
    /// The clips behind every cue: a real clip from
    /// <c>Assets/_Project/Audio/Resources/Audio</c> when one exists, the
    /// synthesized placeholder otherwise (AUDIO.md decision 1).
    /// </summary>
    /// <remarks>
    /// <b>Real clips win, cue by cue.</b> A clip named <c>HitBig</c> in
    /// <c>Assets/_Project/Audio/Resources/Audio/SFX/</c> (and <c>HitBig_2</c>,
    /// <c>HitBig_3</c>… for variants) replaces the synthesized big hit and
    /// nothing else. Music looks in <c>…/Resources/Audio/Music/&lt;Cue&gt;</c>.
    /// Any file type Unity imports works (.wav, .ogg, .mp3); only the name
    /// counts. Nothing needs wiring: dropping a file in is the whole hookup.
    ///
    /// <b>Why that path</b> (AUDIO.md decision 1): Unity loads by name only
    /// from folders called <c>Resources</c>. Every such folder in the project,
    /// packages included, shares one set of names, so the load paths start
    /// with <c>Audio/</c> to stay clear of anyone else's <c>SFX</c> or
    /// <c>Music</c>.
    ///
    /// <b>Synthesis runs on worker threads.</b> The recipes are plain C#, so
    /// their buffers are built off the main thread and turned into clips in
    /// <see cref="Pump"/>, a few per frame, so startup never stalls. Until a
    /// clip is ready its cue is silent.
    ///
    /// <b>Playback shaping lives here</b>: each cue's volume, pitch jitter and
    /// the least gap between two plays, so a walk of four pieces doesn't
    /// become a drum roll.
    ///
    /// <b>Voices (AU2)</b> are loaded per operator on first use, or ahead of
    /// time with <see cref="WarmVoice"/> when a match is dealt, from
    /// <c>…/Resources/Audio/Voice/</c> (<see cref="VoiceSet"/>). An operator
    /// with no files gets synthesized <see cref="VoiceBlips"/>.
    ///
    /// <b>Ability signatures (AU3)</b> load per slug on first use, or ahead of
    /// time with <see cref="WarmSignatures"/> when a match is dealt, from
    /// <c>…/Resources/Audio/SFX/Abilities/&lt;Slug&gt;_&lt;Moment&gt;</c>
    /// (<see cref="AbilitySounds"/>). A moment with no file falls back to its
    /// <see cref="SignatureRecipes"/> stand-in if it has one, and otherwise has
    /// no clip, so the director plays the generic cue.
    /// </remarks>
    public sealed class SoundBank
    {
        /// <summary>How a cue is played.</summary>
        public readonly struct CueSpec
        {
            public CueSpec(float volume, float pitchJitter, float minGap)
            {
                Volume = volume;
                PitchJitter = pitchJitter;
                MinGap = minGap;
            }

            /// <summary>Linear gain on top of the bus.</summary>
            public float Volume { get; }

            /// <summary>Random pitch spread, as a fraction (0.05 = ±5%).</summary>
            public float PitchJitter { get; }

            /// <summary>Real seconds that must pass before the cue plays again.</summary>
            public float MinGap { get; }
        }

        private const string SfxFolder = "Audio/SFX/";
        private const string MusicFolder = "Audio/Music/";

        /// <summary>Highest variant suffix looked for on real clips.</summary>
        private const int MaxFileVariants = 8;

        /// <summary>Clips created per <see cref="Pump"/> call.</summary>
        private const int ClipsPerPump = 4;

        private readonly Dictionary<SoundCue, List<AudioClip>> _sfx = new Dictionary<SoundCue, List<AudioClip>>();
        private readonly Dictionary<MusicCue, AudioClip> _music = new Dictionary<MusicCue, AudioClip>();
        private readonly Dictionary<string, VoiceSet> _voices = new Dictionary<string, VoiceSet>();
        private readonly Dictionary<string, List<AudioClip>> _signatures = new Dictionary<string, List<AudioClip>>();
        private readonly HashSet<string> _warmedSlugs = new HashSet<string>();
        private readonly List<Job> _jobs = new List<Job>();

        private sealed class Job
        {
            public string Name;
            public Task<float[]> Work;
            public Action<AudioClip> Done;
        }

        public static CueSpec SpecOf(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.DiceShake: return new CueSpec(0.8f, 0.04f, 0.1f);
                case SoundCue.DiceLand: return new CueSpec(0.9f, 0.03f, 0.1f);
                // AU1d, the grounded palette: balanced by A-weighted loudness.
                // Knockouts and big hits on top, casts and hits next, the
                // board below them, steps and the interface lowest.
                case SoundCue.Doubles: return new CueSpec(0.8f, 0.03f, 0.2f);
                // Step plays the Kenney chip files (AU1e); 0.41 matches the synthesized step's level.
                case SoundCue.Step: return new CueSpec(0.41f, 0.06f, 0.045f);
                case SoundCue.Rise: return new CueSpec(0.5f, 0.03f, 0.08f);
                case SoundCue.CastTell: return new CueSpec(0.8f, 0.03f, 0.1f);
                case SoundCue.CastCell: return new CueSpec(0.7f, 0.03f, 0.1f);
                case SoundCue.Hit: return new CueSpec(0.85f, 0.06f, 0.04f);
                case SoundCue.HitBig: return new CueSpec(1f, 0.04f, 0.06f);
                case SoundCue.Heal: return new CueSpec(0.75f, 0.02f, 0.1f);
                case SoundCue.Miss: return new CueSpec(0.5f, 0.08f, 0.06f);
                case SoundCue.Block: return new CueSpec(0.95f, 0.04f, 0.06f);
                case SoundCue.Knockout: return new CueSpec(1f, 0.03f, 0.1f);
                case SoundCue.TurnStart: return new CueSpec(0.45f, 0.03f, 0.3f);
                case SoundCue.UiClick: return new CueSpec(0.56f, 0.04f, 0.03f);
                // AU3: the layers sit under the impact they belong to, never over it.
                case SoundCue.LayerTech: return new CueSpec(0.6f, 0.05f, 0.04f);
                case SoundCue.LayerAtomic: return new CueSpec(0.8f, 0.03f, 0.05f);
                default: return new CueSpec(0.6f, 0f, 0.05f);
            }
        }

        /// <summary>
        /// How a signature moment is played (AU3). An impact sits at the big
        /// hit's level, since it replaces it; a tell at the generic tell's.
        /// </summary>
        public static CueSpec SpecOf(SignatureMoment moment)
        {
            switch (moment)
            {
                case SignatureMoment.Impact: return new CueSpec(0.95f, 0.04f, 0.05f);
                case SignatureMoment.Assist: return new CueSpec(0.8f, 0.03f, 0.1f);
                default: return new CueSpec(0.8f, 0.03f, 0.1f);
            }
        }

        /// <summary>Which bus a cue plays on.</summary>
        public static AudioBus BusOf(SoundCue cue) => cue == SoundCue.UiClick ? AudioBus.Ui : AudioBus.Sfx;

        /// <summary>Loads real clips and starts synthesizing the rest. Call once.</summary>
        public void Warm()
        {
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                var list = new List<AudioClip>();
                _sfx[cue] = list;

                if (LoadFiles(SfxFolder + cue, list)) continue;

                for (int v = 0; v < SfxRecipes.VariantsOf(cue); v++)
                {
                    var c = cue;
                    int variant = v;
                    Start($"sfx_{cue}_{v}", () => SfxRecipes.Build(c, variant), clip => list.Add(clip));
                }
            }

            foreach (MusicCue cue in Enum.GetValues(typeof(MusicCue)))
            {
                var file = Resources.Load<AudioClip>(MusicFolder + cue);
                if (file != null)
                {
                    _music[cue] = file;
                    continue;
                }

                if (!MusicRecipes.CanSynthesize(cue)) continue;

                var c = cue;
                Start($"music_{cue}", () => MusicRecipes.Build(c), clip => _music[c] = clip);
            }
        }

        /// <summary>Turns finished synthesis into clips. Call every frame, on the main thread.</summary>
        public void Pump()
        {
            int made = 0;

            for (int i = _jobs.Count - 1; i >= 0 && made < ClipsPerPump; i--)
            {
                var job = _jobs[i];
                if (!job.Work.IsCompleted) continue;

                _jobs.RemoveAt(i);

                if (job.Work.IsFaulted || job.Work.IsCanceled)
                {
                    Debug.LogWarning($"[Audio] {job.Name} could not be synthesized: {job.Work.Exception?.GetBaseException().Message}");
                    continue;
                }

                var data = job.Work.Result;
                var clip = AudioClip.Create(job.Name, data.Length, 1, Synth.SampleRate, false);
                clip.SetData(data, 0);
                job.Done(clip);
                made++;
            }
        }

        /// <summary>A clip for the cue, or null while none is ready.</summary>
        public AudioClip Pick(SoundCue cue, System.Random random)
        {
            if (!_sfx.TryGetValue(cue, out var list) || list.Count == 0) return null;
            return list[random.Next(list.Count)];
        }

        /// <summary>The music clip, or null while it is still being built.</summary>
        public AudioClip Music(MusicCue cue) => _music.TryGetValue(cue, out var clip) ? clip : null;

        /// <summary>
        /// Loads an operator's voice, or starts synthesizing its placeholder
        /// lines. Does nothing for an operator already loaded.
        /// </summary>
        public void WarmVoice(string name)
        {
            if (string.IsNullOrEmpty(name) || _voices.ContainsKey(name)) return;

            var set = VoiceSet.Load(name, out bool placeholder);
            _voices[name] = set;
            if (!placeholder) return;

            foreach (VoiceSlot slot in Enum.GetValues(typeof(VoiceSlot)))
            {
                for (int v = 0; v < VoiceBlips.VariantsOf(slot); v++)
                {
                    var s = slot;
                    int variant = v;
                    Start($"voice_{VoiceBlips.FileKey(name)}_{slot}_{v}",
                        () => VoiceBlips.Build(name, s, variant), clip => set.Add(s, clip));
                }
            }
        }

        /// <summary>A line for the operator and slot, or null while none is ready (or the set has none).</summary>
        public AudioClip Voice(string name, VoiceSlot slot, System.Random random)
        {
            if (string.IsNullOrEmpty(name)) return null;

            WarmVoice(name);
            return _voices[name].Pick(slot, random);
        }

        /// <summary>
        /// Loads an ability's signature files, or starts synthesizing its
        /// stand-ins. Does nothing for a slug already loaded.
        /// </summary>
        public void WarmSignature(string slug)
        {
            if (string.IsNullOrEmpty(slug) || !_warmedSlugs.Add(slug)) return;

            foreach (SignatureMoment moment in Enum.GetValues(typeof(SignatureMoment)))
            {
                string key = AbilitySounds.Key(slug, moment);
                var list = new List<AudioClip>();
                _signatures[key] = list;

                if (LoadFiles(SfxFolder + AbilitySounds.Folder + key, list)) continue;

                for (int v = 0; v < SignatureRecipes.VariantsOf(key); v++)
                {
                    int variant = v;
                    Start($"sig_{key}_{v}", () => SignatureRecipes.Build(key, variant), clip => list.Add(clip));
                }
            }
        }

        /// <summary>A clip for the moment, or null while none is ready (or the ability has none).</summary>
        public AudioClip Signature(string slug, SignatureMoment moment, System.Random random)
        {
            if (string.IsNullOrEmpty(slug)) return null;

            WarmSignature(slug);
            var list = _signatures[AbilitySounds.Key(slug, moment)];
            return list.Count == 0 ? null : list[random.Next(list.Count)];
        }

        private void Start(string name, Func<float[]> build, Action<AudioClip> done) =>
            _jobs.Add(new Job { Name = name, Work = Task.Run(build), Done = done });

        /// <summary>Loads <paramref name="path"/> and its <c>_2</c> … <c>_8</c> variants.</summary>
        private static bool LoadFiles(string path, List<AudioClip> into)
        {
            var first = Resources.Load<AudioClip>(path);
            if (first != null) into.Add(first);

            for (int n = 2; n <= MaxFileVariants; n++)
            {
                var more = Resources.Load<AudioClip>($"{path}_{n}");
                if (more != null) into.Add(more);
            }

            return into.Count > 0;
        }
    }
}