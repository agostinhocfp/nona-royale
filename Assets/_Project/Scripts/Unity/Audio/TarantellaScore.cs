// Assets/_Project/Scripts/Unity/Audio/TarantellaScore.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>The parts of the tarantella.</summary>
    public enum ScoreVoice
    {
        /// <summary>The lead mandolin. Notes of three slots or more are played tremolo.</summary>
        Mandolin,

        /// <summary>The accordion's melody reeds, in musette tuning.</summary>
        Accordion,

        /// <summary>The accordion's right-hand chord stabs, the "pa-pa".</summary>
        Chord,

        /// <summary>The accordion's bass buttons, the "oom".</summary>
        Bass,

        /// <summary>The tamburello's jingles.</summary>
        Jingle,

        /// <summary>The tamburello's skin.</summary>
        Skin,
    }

    /// <summary>One note of the score, on the slot grid.</summary>
    public readonly struct ScoreNote
    {
        public ScoreNote(ScoreVoice voice, int slot, int length, int midi, float velocity)
        {
            Voice = voice;
            Slot = slot;
            Length = length;
            Midi = midi;
            Velocity = velocity;
        }

        public ScoreVoice Voice { get; }

        /// <summary>Start, in sixteenths of 12/8 from the top of the loop. Can be negative for a pickup; it wraps.</summary>
        public int Slot { get; }

        public int Length { get; }
        public int Midi { get; }
        public float Velocity { get; }
    }

    /// <summary>
    /// "Deborah", as a Sicilian tarantella: the match music (AUDIO.md,
    /// increment AU1b). Arranged from the designer's own song.
    /// </summary>
    /// <remarks>
    /// <b>One score, three outputs.</b> The game synthesizes it
    /// (<see cref="MusicRecipes"/>), and the cloud harness renders the same
    /// notes to WAV and to MIDI for a musician. Change a note here and all
    /// three follow.
    ///
    /// <b>From 4/4 to 12/8.</b> The source is in C minor at 138 bpm. Its two
    /// melodies were transcribed on a sixteenth grid and are kept here as
    /// transcribed (<see cref="Bridge"/>, <see cref="Hook"/>). Each beat of
    /// the original becomes a dotted-quarter beat. Inside the beat, the four
    /// sixteenths land on sixteenths 0, 2, 3 and 4 of six. That turns an even
    /// "C . C D" into dotted-eighth, sixteenth, eighth: the lilt of the
    /// siciliana, and of the tarantella.
    ///
    /// <b>The form, 26 bars of 12/8 at 126 to the dotted quarter (about 50 s):</b>
    /// <list type="bullet">
    /// <item>Intro (2 bars): the band alone.</item>
    /// <item>A (8 bars): the song's opening progression, Cm Cm Cm Cm Fm Fm Ab G7, with the opening arpeggio figure on mandolin.</item>
    /// <item>B (4 bars): the verse melody on accordion, over the 808 riff's C–D–F walk.</item>
    /// <item>C (8 bars): the high lead line on mandolin, over Fm Ab G.</item>
    /// <item>B′ (4 bars): the verse melody again with the mandolin on top, turning back to C minor for the loop.</item>
    /// </list>
    /// G is played as a dominant seventh (with B natural), the harmonic-minor
    /// colour every tarantella leans on.
    /// </remarks>
    public static class TarantellaScore
    {
        /// <summary>Dotted quarters per minute.</summary>
        public const float Bpm = 126f;

        public const int SlotsPerBeat = 6;
        public const int BeatsPerBar = 4;
        public const int SlotsPerBar = SlotsPerBeat * BeatsPerBar;

        public static float SlotSeconds => 60f / Bpm / SlotsPerBeat;

        /// <summary>
        /// The lead line from about 2:20 in the source, on its sixteenth grid
        /// (16 to a 4/4 bar), as MIDI notes. Played an octave lower than
        /// transcribed. Negative positions are the pickup.
        /// </summary>
        private static readonly int[,] Bridge =
        {
            { -10, 86 }, { -9, 84 }, { -8, 87 }, { -7, 84 }, { -5, 84 },
            { 0, 84 }, { 2, 84 }, { 3, 86 }, { 4, 87 }, { 5, 89 }, { 6, 86 }, { 7, 84 }, { 8, 87 }, { 9, 84 },
            { 16, 84 }, { 18, 84 }, { 19, 87 }, { 20, 89 }, { 21, 84 }, { 22, 86 }, { 23, 84 }, { 24, 87 }, { 25, 84 }, { 27, 87 }, { 28, 84 },
            { 32, 84 }, { 34, 84 }, { 35, 87 }, { 36, 92 }, { 37, 91 }, { 38, 87 }, { 40, 89 }, { 41, 84 },
            { 48, 84 }, { 50, 84 }, { 51, 86 }, { 53, 80 }, { 54, 86 }, { 55, 89 }, { 56, 87 }, { 57, 84 }, { 59, 87 }, { 60, 84 },
            { 64, 84 }, { 66, 84 }, { 67, 86 }, { 68, 87 }, { 69, 89 }, { 70, 86 }, { 71, 84 }, { 72, 87 }, { 73, 84 },
            { 75, 79 }, { 76, 80 }, { 77, 82 }, { 78, 79 }, { 79, 80 },
            { 80, 84 }, { 82, 84 }, { 83, 87 }, { 85, 84 }, { 86, 86 }, { 87, 84 }, { 88, 87 }, { 89, 84 }, { 91, 87 },
            { 92, 92 }, { 93, 91 }, { 94, 89 }, { 95, 87 },
            { 96, 84 }, { 98, 84 }, { 99, 87 }, { 100, 92 }, { 101, 91 }, { 102, 87 }, { 105, 84 },
        };

        /// <summary>
        /// The verse melody from about 0:40, over the C–C–D–F–F 808 riff: a
        /// two-bar phrase on the same grid, with its pickup at -1.
        /// </summary>
        private static readonly int[,] Hook =
        {
            { -1, 72 }, { 1, 72 }, { 3, 74 }, { 7, 74 }, { 8, 75 }, { 11, 80 }, { 12, 70 }, { 13, 79 }, { 15, 72 },
            { 17, 74 }, { 19, 75 }, { 24, 74 }, { 25, 75 }, { 29, 79 },
        };

        /// <summary>The verse phrase's last note ends where the next pass's pickup begins.</summary>
        private const int HookSpan = 31;

        /// <summary>Where each original sixteenth lands inside a beat of six.</summary>
        private static readonly int[] Lilt = { 0, 2, 3, 4 };

        // ── Harmony ──────────────────────────────────────────────────────

        private sealed class Chord
        {
            public Chord(int[] voicing, int[] arpeggio, int root, int fifth)
            {
                Voicing = voicing;
                Arpeggio = arpeggio;
                Root = root;
                Fifth = fifth;
            }

            /// <summary>The right hand's stab, around middle C.</summary>
            public int[] Voicing { get; }

            /// <summary>The mandolin's broken chord, lowest note first.</summary>
            public int[] Arpeggio { get; }

            public int Root { get; }
            public int Fifth { get; }
        }

        private static readonly Chord Cm = new Chord(new[] { 55, 60, 63 }, new[] { 72, 75, 79 }, 36, 43);
        private static readonly Chord Fm = new Chord(new[] { 56, 60, 65 }, new[] { 77, 80, 84 }, 41, 36);
        private static readonly Chord Ab = new Chord(new[] { 56, 60, 63 }, new[] { 68, 72, 75 }, 44, 39);
        private static readonly Chord G7 = new Chord(new[] { 55, 59, 65 }, new[] { 67, 71, 74, 77 }, 43, 38);
        private static readonly Chord Gs = new Chord(new[] { 55, 60, 62 }, new[] { 67, 72, 74 }, 43, 38);
        private static readonly Chord Dh = new Chord(new[] { 56, 60, 62, 65 }, new[] { 74, 77, 80 }, 38, 44);

        private static Chord[] Bar(Chord all) => new[] { all, all, all, all };
        private static Chord[] Bar(Chord a, Chord b, Chord c, Chord d) => new[] { a, b, c, d };

        public const int IntroBars = 2;
        public const int ABars = 8;
        public const int BBars = 4;
        public const int CBars = 8;

        public static int Bars => IntroBars + ABars + BBars + CBars + BBars;

        public static int LengthSlots => Bars * SlotsPerBar;

        private static List<Chord[]> Progression()
        {
            var bars = new List<Chord[]>
            {
                // Intro
                Bar(Cm), Bar(Cm, Cm, G7, G7),
                // A: the song's opening changes
                Bar(Cm), Bar(Cm), Bar(Cm), Bar(Cm), Bar(Fm), Bar(Fm), Bar(Ab), Bar(G7),
                // B: the verse riff
                Bar(Cm, Cm, Cm, Dh), Bar(Dh, Fm, Fm, Fm), Bar(Cm, Cm, Cm, Dh), Bar(Dh, Fm, Fm, Fm),
            };

            // C: the lead line's changes, twice around
            for (int i = 0; i < CBars / 2; i++)
            {
                bars.Add(Bar(Fm, Fm, Ab, G7));
                bars.Add(Bar(Gs, Gs, G7, G7));
            }

            // B′, turning home at the end
            bars.Add(Bar(Cm, Cm, Cm, Dh));
            bars.Add(Bar(Dh, Fm, Fm, Fm));
            bars.Add(Bar(Cm, Cm, Cm, Dh));
            bars.Add(Bar(Dh, Fm, G7, G7));

            return bars;
        }

        // ── Building ─────────────────────────────────────────────────────

        public static List<ScoreNote> Build()
        {
            var notes = new List<ScoreNote>();
            var bars = Progression();

            int aStart = IntroBars * SlotsPerBar;
            int bStart = (IntroBars + ABars) * SlotsPerBar;
            int cStart = (IntroBars + ABars + BBars) * SlotsPerBar;
            int b2Start = (IntroBars + ABars + BBars + CBars) * SlotsPerBar;

            for (int bar = 0; bar < bars.Count; bar++)
            {
                int barSlot = bar * SlotsPerBar;
                bool intro = bar < IntroBars;
                bool full = barSlot >= cStart;

                for (int beat = 0; beat < BeatsPerBar; beat++)
                {
                    var chord = bars[bar][beat];
                    int at = barSlot + beat * SlotsPerBeat;

                    Band(notes, chord, at, beat, intro, full);

                    if (barSlot >= aStart && barSlot < bStart) Arpeggio(notes, chord, at, beat);
                }
            }

            // The intro's call: the dominant held on mandolin, into bar A.
            notes.Add(new ScoreNote(ScoreVoice.Mandolin, SlotsPerBar + 2 * SlotsPerBeat, 12, 67, 0.7f));

            // B: the verse on accordion.
            for (int pass = 0; pass < 2; pass++)
                Melody(notes, Hook, ScoreVoice.Accordion, bStart + pass * 2 * SlotsPerBar, 0, 1f, HookSpan);

            // C: the lead line on mandolin, an octave down.
            Melody(notes, Bridge, ScoreVoice.Mandolin, cStart, -12, 1f, CBars * 16);

            // B′: the verse, with the mandolin an octave above.
            for (int pass = 0; pass < 2; pass++)
            {
                int start = b2Start + pass * 2 * SlotsPerBar;
                Melody(notes, Hook, ScoreVoice.Accordion, start, 0, 1f, HookSpan);
                Melody(notes, Hook, ScoreVoice.Mandolin, start, 12, 0.55f, HookSpan);
            }

            return notes;
        }

        /// <summary>The oom-pa-pa and the tamburello, for one beat.</summary>
        private static void Band(List<ScoreNote> notes, Chord chord, int at, int beat, bool intro, bool full)
        {
            bool strong = beat % 2 == 0;

            notes.Add(new ScoreNote(ScoreVoice.Bass, at, 2, strong ? chord.Root : chord.Fifth, strong ? 1f : 0.85f));

            foreach (int pitch in chord.Voicing)
            {
                notes.Add(new ScoreNote(ScoreVoice.Chord, at + 2, 2, pitch, 0.8f));
                notes.Add(new ScoreNote(ScoreVoice.Chord, at + 4, 2, pitch, 0.65f));
            }

            // The tamburello: a strike on each beat, lighter jingles between,
            // a rolling shake of sixteenths once the lead line arrives.
            notes.Add(new ScoreNote(ScoreVoice.Jingle, at, 1, 0, strong ? 1f : 0.8f));
            notes.Add(new ScoreNote(ScoreVoice.Jingle, at + 2, 1, 0, 0.45f));
            notes.Add(new ScoreNote(ScoreVoice.Jingle, at + 4, 1, 0, 0.6f));

            if (full && !strong)
            {
                notes.Add(new ScoreNote(ScoreVoice.Jingle, at + 3, 1, 0, 0.3f));
                notes.Add(new ScoreNote(ScoreVoice.Jingle, at + 5, 1, 0, 0.35f));
            }

            if (strong && !intro) notes.Add(new ScoreNote(ScoreVoice.Skin, at, 1, 0, beat == 0 ? 1f : 0.7f));
        }

        /// <summary>The song's opening figure as a mandolin arpeggio: up on the strong beats, down on the weak.</summary>
        private static void Arpeggio(List<ScoreNote> notes, Chord chord, int at, int beat)
        {
            var a = chord.Arpeggio;
            int[] figure = beat % 2 == 0
                ? new[] { a[0], a[1], a[2] }
                : new[] { a.Length > 3 ? a[3] : a[0] + 12, a[2], a[1] };

            for (int i = 0; i < 3; i++)
                notes.Add(new ScoreNote(ScoreVoice.Mandolin, at + 2 * i, 2, figure[i], i == 0 ? 0.75f : 0.55f));
        }

        /// <summary>
        /// Lays a transcribed line into the score: each original sixteenth
        /// moved onto the lilt, each note lasting until the next.
        /// </summary>
        /// <param name="span">The phrase length in original sixteenths; the last note lasts to its end.</param>
        private static void Melody(List<ScoreNote> notes, int[,] line, ScoreVoice voice, int start, int transpose,
            float velocity, int span)
        {
            int count = line.GetLength(0);

            for (int i = 0; i < count; i++)
            {
                int here = ToSlot(line[i, 0]);
                int next = i + 1 < count ? ToSlot(line[i + 1, 0]) : ToSlot(span);
                int length = Math.Max(1, Math.Min(next - here, 2 * SlotsPerBeat));

                // A note on the beat speaks a little louder.
                float accent = here % SlotsPerBeat == 0 ? 1f : 0.82f;
                notes.Add(new ScoreNote(voice, start + here, length, line[i, 1] + transpose, velocity * accent));
            }
        }

        /// <summary>An original sixteenth position (16 to a 4/4 bar) as a slot of 12/8.</summary>
        public static int ToSlot(int sixteenth)
        {
            int beat = (int)Math.Floor(sixteenth / 4.0);
            int within = sixteenth - beat * 4;
            return beat * SlotsPerBeat + Lilt[within];
        }
    }
}