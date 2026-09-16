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
                case SoundCue.Doubles: return new CueSpec(0.6f, 0f, 0.2f);
                case SoundCue.Step: return new CueSpec(0.35f, 0.06f, 0.045f);
                case SoundCue.Rise: return new CueSpec(0.6f, 0.03f, 0.08f);
                case SoundCue.CastTell:
                case SoundCue.CastCell: return new CueSpec(0.55f, 0.02f, 0.1f);
                case SoundCue.Hit: return new CueSpec(0.75f, 0.06f, 0.04f);
                case SoundCue.HitBig: return new CueSpec(0.9f, 0.04f, 0.06f);
                case SoundCue.Heal: return new CueSpec(0.5f, 0.02f, 0.1f);
                case SoundCue.Miss: return new CueSpec(0.55f, 0.08f, 0.06f);
                case SoundCue.Block: return new CueSpec(0.6f, 0.04f, 0.06f);
                case SoundCue.Knockout: return new CueSpec(0.9f, 0.03f, 0.1f);
                case SoundCue.TurnStart: return new CueSpec(0.4f, 0f, 0.3f);
                case SoundCue.UiClick: return new CueSpec(0.45f, 0.05f, 0.03f);
                default: return new CueSpec(0.6f, 0f, 0.05f);
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

                if (LoadFiles(cue, list)) continue;

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

        private void Start(string name, Func<float[]> build, Action<AudioClip> done) =>
            _jobs.Add(new Job { Name = name, Work = Task.Run(build), Done = done });

        private static bool LoadFiles(SoundCue cue, List<AudioClip> into)
        {
            var first = Resources.Load<AudioClip>(SfxFolder + cue);
            if (first != null) into.Add(first);

            for (int n = 2; n <= MaxFileVariants; n++)
            {
                var more = Resources.Load<AudioClip>($"{SfxFolder}{cue}_{n}");
                if (more != null) into.Add(more);
            }

            return into.Count > 0;
        }
    }
}