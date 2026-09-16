// Assets/_Project/Scripts/Unity/Audio/MusicRecipes.cs
using System;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>
    /// The music the game asks for (AUDIO.md decision 4). A clip at
    /// <c>Assets/_Project/Audio/Resources/Audio/Music/&lt;Cue&gt;</c> replaces the synthesized one.
    /// </summary>
    public enum MusicCue
    {
        /// <summary>The title, setup and draft: the noir jazz loop.</summary>
        Title,
        /// <summary>A match in play: "Deborah", as a Sicilian tarantella.</summary>
        Match,
        /// <summary>A short win sting. Not a loop.</summary>
        Win,

        /// <summary>
        /// The final stretch of a match, once a seat is one operator from
        /// winning. File only (<c>Resources/Audio/Music/Showdown</c>): with no
        /// file, the match music simply carries on.
        /// </summary>
        Showdown,
    }

    /// <summary>
    /// The procedural score: the designer's "Deborah" as a tarantella for
    /// the match (<see cref="TarantellaScore"/>), a minor-key jazz loop for
    /// the menus, and a brass sting for a win.
    /// </summary>
    /// <remarks>
    /// Plain C#. The loops are written with <c>loop: true</c>, so notes ring
    /// across the loop point and the clip repeats without a seam. Everything
    /// sits low in level (peak 0.5 to 0.55) so the effects stay on top.
    /// </remarks>
    public static class MusicRecipes
    {
        public static bool Loops(MusicCue cue) => cue != MusicCue.Win;

        /// <summary>Whether a placeholder can be synthesized for the cue. The showdown has none.</summary>
        public static bool CanSynthesize(MusicCue cue) => cue != MusicCue.Showdown;

        public static float[] Build(MusicCue cue)
        {
            switch (cue)
            {
                case MusicCue.Title: return Jazz();
                case MusicCue.Match: return Tarantella();
                case MusicCue.Win: return Win();
                default: throw new ArgumentOutOfRangeException(nameof(cue), cue, null);
            }
        }

        // ── Match: "Deborah", the tarantella ─────────────────────────────

        /// <summary>
        /// Renders <see cref="TarantellaScore"/>: a mandolin (two plucked
        /// courses, tremolo on held notes), an accordion in musette tuning with
        /// its oom-pa-pa, and a tamburello.
        /// </summary>
        private static float[] Tarantella()
        {
            float slot = TarantellaScore.SlotSeconds;
            float length = TarantellaScore.LengthSlots * slot;
            var b = Synth.Buffer(length);
            var r = new SynthRandom(126);

            foreach (var note in TarantellaScore.Build()) Play(b, note, slot, length, r);

            Synth.Normalize(b, 0.55f);
            return b;
        }

        /// <summary>The level of each part. The mandolin and the accordion melody lead; the band sits under them.</summary>
        private const float MandolinGain = 0.55f;
        private const float MelodyGain = 0.16f;
        private const float ChordGain = 0.042f;
        private const float BassGain = 0.13f;
        private const float JingleGain = 0.17f;
        private const float SkinGain = 0.22f;

        /// <summary>Renders one note of the score into the loop.</summary>
        private static void Play(float[] b, ScoreNote note, float slot, float length, SynthRandom r)
        {
            float at = note.Slot * slot;
            if (at < 0f) at += length;          // a pickup before the top wraps to the end
            float dur = note.Length * slot;
            float v = note.Velocity;

            switch (note.Voice)
            {
                case ScoreVoice.Mandolin: Mandolin(b, at, dur, note.Midi, MandolinGain * v, slot, r); break;
                case ScoreVoice.Accordion: Accordion(b, at, dur, note.Midi, MelodyGain * v, 3); break;
                case ScoreVoice.Chord: Accordion(b, at, dur * 0.8f, note.Midi, ChordGain * v, 2); break;
                case ScoreVoice.Bass: AccordionBass(b, at, dur, note.Midi, BassGain * v); break;
                case ScoreVoice.Jingle: Jingle(b, at, JingleGain * v, r); break;
                case ScoreVoice.Skin: Skin(b, at, SkinGain * v, r); break;
            }
        }

        /// <summary>
        /// Two steel courses a hair apart. A held note (three slots or more)
        /// is picked tremolo, one stroke a slot, alternating down and up.
        /// </summary>
        private static void Mandolin(float[] b, float at, float dur, int note, float level, float slot, SynthRandom r)
        {
            float f = Synth.Midi(note);
            int strokes = dur >= slot * 2.5f ? Math.Max(1, (int)MathF.Round(dur / slot)) : 1;
            float strokeLength = strokes > 1 ? slot * 1.6f : MathF.Max(dur, 0.35f);

            for (int k = 0; k < strokes; k++)
            {
                float gain = level * (strokes > 1 ? (k % 2 == 0 ? 0.85f : 0.62f) : 1f);
                float t = at + k * slot;
                Synth.Pluck(b, t, strokeLength, f, gain, 0.55f, 0.55f, r, loop: true);
                Synth.Pluck(b, t + 0.004f, strokeLength, f * 1.0025f, gain * 0.8f, 0.5f, 0.5f, r, loop: true);
            }
        }

        /// <summary>
        /// Free reeds: the given number of reeds, detuned for the musette
        /// shimmer, with a soft bellows attack and a gentle vibrato.
        /// </summary>
        private static void Accordion(float[] b, float at, float dur, int note, float gain, int reeds)
        {
            float f = Synth.Midi(note);
            float[] cents = reeds >= 3 ? new[] { 0f, -14f, 14f } : new[] { 0f, 12f };
            float length = dur + 0.05f;

            for (int k = 0; k < cents.Length && k < reeds; k++)
            {
                float rf = f * MathF.Pow(2f, cents[k] / 1200f);
                Synth.Tone(b, at, length, rf, rf, Wave.Reed, gain / reeds * 1.6f, 0.025f, 3f,
                    vibratoHz: 5.2f, vibratoDepth: 0.0015f, lowpassHz: 2600f, loop: true);
            }
        }

        /// <summary>The bass buttons: a low reed with its fundamental reinforced, short.</summary>
        private static void AccordionBass(float[] b, float at, float dur, int note, float gain)
        {
            float f = Synth.Midi(note);
            float length = dur * 0.9f;
            Synth.Tone(b, at, length, f, f, Wave.Reed, gain * 0.6f, 0.012f, 0.35f, lowpassHz: 800f, loop: true);
            Synth.Tone(b, at, length, f * 2f, f * 2f, Wave.Reed, gain * 0.3f, 0.012f, 0.3f, lowpassHz: 1200f, loop: true);
            Synth.Tone(b, at, length, f, f, Wave.Sine, gain * 0.8f, 0.01f, 0.4f, loop: true);
        }

        /// <summary>The tamburello's jingles: bright metal and a hiss.</summary>
        private static void Jingle(float[] b, float at, float gain, SynthRandom r)
        {
            Synth.Noise(b, at, 0.12f, gain, 0.001f, 0.035f, 13000f, 5500f, r, loop: true);
            Synth.Bell(b, at, 0.1f, 6100f * r.Range(0.98f, 1.02f), new[] { 1f, 1.31f, 1.72f }, gain * 0.35f, 0.03f, loop: true);
        }

        /// <summary>The tamburello's goatskin, struck with the palm.</summary>
        private static void Skin(float[] b, float at, float gain, SynthRandom r)
        {
            Synth.Tone(b, at, 0.16f, 210f, 150f, Wave.Sine, gain, 0.002f, 0.045f, loop: true);
            Synth.Noise(b, at, 0.05f, gain * 0.4f, 0.001f, 0.012f, 1500f, 100f, r, loop: true);
        }

        // ── Title: the noir jazz loop ────────────────────────────────────

        private const float JazzBpm = 88f;

        /// <summary>A walking line, four notes a bar: | Dm7 | Dm7 | Gm7 | Gm7 | Em7b5 | A7 | Dm7 | A7 |.</summary>
        private static readonly int[][] JazzBass =
        {
            new[] { 38, 40, 41, 43 },
            new[] { 45, 43, 41, 40 },
            new[] { 43, 45, 46, 48 },
            new[] { 50, 48, 46, 41 },
            new[] { 40, 43, 46, 43 },
            new[] { 45, 49, 45, 39 },
            new[] { 38, 41, 45, 41 },
            new[] { 45, 43, 40, 37 },
        };

        /// <summary>Rootless voicings in the middle register, one per bar.</summary>
        private static readonly int[][] JazzChords =
        {
            new[] { 60, 64, 65, 69 },
            new[] { 60, 64, 65, 69 },
            new[] { 58, 62, 65, 69 },
            new[] { 58, 62, 65, 69 },
            new[] { 62, 64, 67, 70 },
            new[] { 61, 64, 67, 70 },
            new[] { 60, 64, 65, 69 },
            new[] { 61, 64, 67, 70 },
        };

        private static float[] Jazz()
        {
            float beat = 60f / JazzBpm;
            int bars = JazzBass.Length;
            var b = Synth.Buffer(bars * 4 * beat);
            var r = new SynthRandom(88);

            for (int bar = 0; bar < bars; bar++)
            {
                float barStart = bar * 4 * beat;

                for (int n = 0; n < 4; n++)
                {
                    float at = barStart + n * beat;
                    Bass(b, at, beat * 0.95f, JazzBass[bar][n], 0.55f);
                    Ride(b, at, n % 2 == 0 ? 0.16f : 0.2f, r);
                    Brush(b, at, beat, 0.05f, r);

                    // Swung skip notes on 2 and 4, and the hi-hat foot.
                    if (n % 2 == 1)
                    {
                        Ride(b, at + beat * 2f / 3f, 0.1f, r);
                        Synth.Noise(b, at, 0.04f, 0.09f, 0.001f, 0.01f, 9000f, 5000f, r, loop: true);
                    }
                }

                // A feathered kick on the one.
                Synth.Tone(b, barStart, 0.2f, 70f, 45f, Wave.Sine, 0.3f, 0.002f, 0.06f, loop: true);

                // The comp: a push on the "and" of two, and a short stab on four,
                // skipping some bars so it breathes.
                if (bar % 4 != 3)
                    Rhodes(b, barStart + beat * (1f + 2f / 3f), 1.1f, JazzChords[bar], 0.11f);
                if (bar % 2 == 1)
                    Rhodes(b, barStart + beat * 3f, 0.45f, JazzChords[bar], 0.07f);
            }

            Synth.Normalize(b, 0.5f);
            return b;
        }

        // ── Win ──────────────────────────────────────────────────────────

        private static float[] Win()
        {
            // Three brass hits climbing to D major, a cymbal on the last.
            var b = Synth.Buffer(3.2f);
            var r = new SynthRandom(7);

            Brass(b, 0f, 0.4f, new[] { 58, 62, 65, 70 }, 0.16f);
            Brass(b, 0.3f, 0.4f, new[] { 60, 64, 67, 72 }, 0.16f);
            Brass(b, 0.6f, 2.5f, new[] { 62, 66, 69, 74 }, 0.2f);
            Bass(b, 0.6f, 2.4f, 38, 0.6f);
            Synth.Noise(b, 0.6f, 2.2f, 0.25f, 0.003f, 0.7f, 12000f, 5000f, r);
            Synth.Tone(b, 0.6f, 0.4f, 80f, 45f, Wave.Sine, 0.5f, 0.002f, 0.12f);

            Synth.Normalize(b, 0.6f);
            return b;
        }

        // ── Voices ───────────────────────────────────────────────────────

        /// <summary>An upright-ish bass: a filtered triangle with a sine an octave down.</summary>
        private static void Bass(float[] b, float at, float length, int note, float gain)
        {
            float f = Synth.Midi(note);
            Synth.Tone(b, at, length, f, f, Wave.Triangle, gain, 0.006f, 0.35f, lowpassHz: 600f, loop: true);
            Synth.Tone(b, at, length, f * 0.5f, f * 0.5f, Wave.Sine, gain * 0.6f, 0.01f, 0.4f, loop: true);
        }

        /// <summary>An electric-piano chord: sine plus a soft octave, with a slow tremolo.</summary>
        private static void Rhodes(float[] b, float at, float length, int[] notes, float gain, float decay = 0.6f)
        {
            foreach (int note in notes)
            {
                float f = Synth.Midi(note);
                Synth.Tone(b, at, length, f, f, Wave.Sine, gain, 0.004f, decay,
                    vibratoHz: 5f, vibratoDepth: 0.002f, loop: true);
                Synth.Tone(b, at, length * 0.6f, f * 2f, f * 2f, Wave.Sine, gain * 0.25f, 0.002f, decay * 0.4f, loop: true);
            }
        }

        /// <summary>A section of saws, detuned, softened by a low-pass.</summary>
        private static void Brass(float[] b, float at, float length, int[] notes, float gain)
        {
            foreach (int note in notes)
            {
                float f = Synth.Midi(note);
                Synth.Tone(b, at, length, f * 0.997f, f * 0.997f, Wave.Saw, gain, 0.03f, length * 0.6f,
                    vibratoHz: 5.5f, vibratoDepth: 0.004f, lowpassHz: 1800f);
                Synth.Tone(b, at, length, f * 1.003f, f * 1.003f, Wave.Saw, gain * 0.8f, 0.04f, length * 0.6f,
                    lowpassHz: 1500f);
            }
        }

        /// <summary>A ride cymbal: a high metallic ping and a hiss.</summary>
        private static void Ride(float[] b, float at, float gain, SynthRandom r)
        {
            Synth.Bell(b, at, 0.35f, 3150f * r.Range(0.99f, 1.01f), new[] { 1f, 1.47f, 2.09f }, gain * 0.35f, 0.12f, loop: true);
            Synth.Noise(b, at, 0.3f, gain * 0.5f, 0.001f, 0.09f, 12000f, 6000f, r, loop: true);
        }

        /// <summary>A brush swish across the snare.</summary>
        private static void Brush(float[] b, float at, float length, float gain, SynthRandom r) =>
            Synth.Noise(b, at, length, gain, length * 0.4f, length * 0.3f, 6000f, 1500f, r, loop: true);
    }
}