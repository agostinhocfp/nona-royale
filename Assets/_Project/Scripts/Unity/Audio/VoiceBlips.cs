// Assets/_Project/Scripts/Unity/Audio/VoiceBlips.cs
using System;
using System.Text;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>
    /// How an operator's placeholder voice sounds (AUDIO.md decision 3): the
    /// register, the timbre and the pace of its babble.
    /// </summary>
    public readonly struct VoiceSignature
    {
        public VoiceSignature(float root, Wave wave, float brightness, float pace, float vibrato, float grit, float breath)
        {
            Root = root;
            Wave = wave;
            Brightness = brightness;
            Pace = pace;
            Vibrato = vibrato;
            Grit = grit;
            Breath = breath;
        }

        /// <summary>The speaking pitch, as a MIDI note.</summary>
        public float Root { get; }

        /// <summary>The raw tone before it is darkened.</summary>
        public Wave Wave { get; }

        /// <summary>Low-pass corner in Hz: low is chesty and muffled, high is bright.</summary>
        public float Brightness { get; }

        /// <summary>Syllable length multiplier: above 1 is slower.</summary>
        public float Pace { get; }

        /// <summary>Vibrato depth as a fraction of the pitch.</summary>
        public float Vibrato { get; }

        /// <summary>Saturation amount (1 is clean).</summary>
        public float Grit { get; }

        /// <summary>Gain of the breathy consonant at each syllable's start.</summary>
        public float Breath { get; }
    }

    /// <summary>
    /// Placeholder voice lines (AUDIO.md decision 3, increment AU2): a short
    /// synthesized babble per operator and slot, so the voice rules can be
    /// heard before anything is recorded.
    /// </summary>
    /// <remarks>
    /// <b>The shape carries the meaning.</b> Every slot has its own syllable
    /// contour (a rising call to deploy, a falling grunt when hit, a long
    /// fall at death, a laugh at victory); the operator's
    /// <see cref="VoiceSignature"/> decides who seems to say it.
    ///
    /// <b>Deterministic.</b> The same operator, slot and variant always give
    /// the same buffer, seeded from the name with a stable hash
    /// (<c>string.GetHashCode</c> is not stable between runtimes).
    ///
    /// <b>Plain C#</b>, like <see cref="Synth"/>.
    /// </remarks>
    public static class VoiceBlips
    {
        /// <summary>Loudest sample of a finished blip.</summary>
        public const float Peak = 0.8f;

        /// <summary>Seconds of silence after the last syllable, so a blip never ends on its own tail.</summary>
        private const float Tail = 0.08f;

        /// <summary>One syllable: length in seconds, pitch glide in semitones from the root, and level.</summary>
        private readonly struct Syllable
        {
            public Syllable(float length, float from, float to, float gain = 1f)
            {
                Length = length;
                From = from;
                To = to;
                Gain = gain;
            }

            public float Length { get; }
            public float From { get; }
            public float To { get; }
            public float Gain { get; }
        }

        private static readonly Syllable[] MoveShape =
        {
            new Syllable(0.08f, 0f, 1f), new Syllable(0.11f, 2f, -1f, 0.8f),
        };

        private static readonly Syllable[] DeployShape =
        {
            new Syllable(0.07f, 0f, 0f, 0.8f), new Syllable(0.07f, 3f, 3f, 0.9f), new Syllable(0.14f, 5f, 7f),
        };

        private static readonly Syllable[] HitTakenShape =
        {
            new Syllable(0.16f, 4f, -5f),
        };

        private static readonly Syllable[] CastShape =
        {
            new Syllable(0.06f, 2f, 2f, 0.8f), new Syllable(0.06f, 4f, 4f, 0.9f), new Syllable(0.13f, 7f, 5f),
        };

        private static readonly Syllable[] KillShape =
        {
            new Syllable(0.08f, 5f, 5f), new Syllable(0.08f, 7f, 7f), new Syllable(0.22f, 12f, 10f),
        };

        private static readonly Syllable[] DeathShape =
        {
            new Syllable(0.1f, 5f, 3f, 0.9f), new Syllable(0.45f, 3f, -12f),
        };

        private static readonly Syllable[] VictoryShape =
        {
            new Syllable(0.07f, 7f, 7f, 0.8f), new Syllable(0.07f, 5f, 5f, 0.7f),
            new Syllable(0.07f, 7f, 7f, 0.8f), new Syllable(0.07f, 5f, 5f, 0.7f),
            new Syllable(0.28f, 9f, 12f),
        };

        private static readonly Syllable[] QuitShape =
        {
            new Syllable(0.18f, 2f, -2f, 0.8f), new Syllable(0.3f, -1f, -7f),
        };

        /// <summary>How many placeholder variants a slot gets.</summary>
        public static int VariantsOf(VoiceSlot slot) =>
            slot == VoiceSlot.Move || slot == VoiceSlot.HitTaken ? 3 : 2;

        /// <summary>
        /// The signature for an operator by name. Known operators are voiced by
        /// hand from <c>OPERATORS.md</c>; anyone else gets one derived from the name.
        /// </summary>
        public static VoiceSignature SignatureOf(string name)
        {
            switch (name)
            {
                // The house: low, heavy, unhurried.
                case "Bouncer": return new VoiceSignature(38f, Wave.Saw, 700f, 1.3f, 0.004f, 2.2f, 0.15f);
                case "Kurbyn": return new VoiceSignature(43f, Wave.Reed, 1100f, 1.0f, 0.006f, 1.8f, 0.2f);
                case "Javi": return new VoiceSignature(48f, Wave.Saw, 1500f, 0.9f, 0.01f, 1.2f, 0.2f);
                case "Sanity": return new VoiceSignature(36f, Wave.Saw, 600f, 1.5f, 0.003f, 1.6f, 0.12f);

                // Contractors: brighter and quicker.
                case "Syla": return new VoiceSignature(60f, Wave.Saw, 2600f, 0.8f, 0.012f, 1.1f, 0.3f);
                case "Mimi": return new VoiceSignature(64f, Wave.Square, 3000f, 0.9f, 0.02f, 1f, 0.15f);
                case "Kian": return new VoiceSignature(52f, Wave.Saw, 1800f, 1.0f, 0.008f, 1.3f, 0.2f);
                case "Nuetu": return new VoiceSignature(50f, Wave.Reed, 1600f, 1.1f, 0.01f, 1.2f, 0.2f);

                // The grudge: mid-low, terse, rough.
                case "Luka": return new VoiceSignature(45f, Wave.Reed, 1300f, 0.85f, 0.004f, 1.5f, 0.25f);
            }

            uint h = Hash(name ?? "");
            var waves = new[] { Wave.Saw, Wave.Reed, Wave.Square };
            return new VoiceSignature(
                root: 40f + h % 23,
                wave: waves[(h >> 8) % (uint)waves.Length],
                brightness: 900f + (h >> 11) % 2000,
                pace: 0.85f + ((h >> 16) % 40) / 100f,
                vibrato: 0.004f + ((h >> 20) % 10) / 1000f,
                grit: 1.1f + ((h >> 24) % 10) / 10f,
                breath: 0.2f);
        }

        /// <summary>Builds one placeholder line.</summary>
        public static float[] Build(string name, VoiceSlot slot, int variant)
        {
            var voice = SignatureOf(name);
            var shape = ShapeOf(slot);
            var random = new SynthRandom((int)(Hash(name ?? "") ^ (uint)((int)slot * 7919 + variant * 104729)));

            // Jitter first, so the buffer is sized to the syllables it holds.
            var lengths = new float[shape.Length];
            var froms = new float[shape.Length];
            var tos = new float[shape.Length];
            float total = 0f;
            for (int i = 0; i < shape.Length; i++)
            {
                float shift = random.Range(-1.5f, 1.5f);
                lengths[i] = shape[i].Length * voice.Pace * random.Range(0.85f, 1.15f);
                froms[i] = shape[i].From + shift;
                tos[i] = shape[i].To + shift;
                total += lengths[i];
            }

            var b = Synth.Buffer(total + Tail);
            float t = 0f;

            for (int i = 0; i < shape.Length; i++)
            {
                float length = lengths[i];
                float from = Synth.Midi(voice.Root + froms[i]);
                float to = Synth.Midi(voice.Root + tos[i]);
                float gain = shape[i].Gain;

                // The voiced part, and a softer copy an octave and a fifth up for vowel colour.
                Synth.Tone(b, t, length, from, to, voice.Wave, gain, 0.012f, length * 0.9f,
                    vibratoHz: 5.5f, vibratoDepth: voice.Vibrato, lowpassHz: voice.Brightness);
                Synth.Tone(b, t, length, from * 3f, to * 3f, Wave.Sine, gain * 0.12f, 0.02f, length * 0.6f,
                    vibratoHz: 5.5f, vibratoDepth: voice.Vibrato);

                // The consonant: a breath of high noise at the onset.
                if (voice.Breath > 0f)
                    Synth.Noise(b, t, 0.03f, gain * voice.Breath, 0.002f, 0.01f, 6000f, 2000f, random);

                t += length;
            }

            if (slot == VoiceSlot.HitTaken || slot == VoiceSlot.Death)
                Synth.Noise(b, 0f, total, 0.12f * voice.Breath / 0.2f, 0.01f, total * 0.5f, 2500f, 300f, random);

            if (voice.Grit > 1f) Synth.Drive(b, voice.Grit);
            Synth.Normalize(b, Peak);
            return b;
        }

        /// <summary>An operator name as ASCII letters and digits, the way it appears in voice file names.</summary>
        public static string FileKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            var decomposed = name.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
                if (c < 128 && char.IsLetterOrDigit(c))
                    sb.Append(c);

            return sb.ToString();
        }

        private static Syllable[] ShapeOf(VoiceSlot slot)
        {
            switch (slot)
            {
                case VoiceSlot.Move: return MoveShape;
                case VoiceSlot.Deploy: return DeployShape;
                case VoiceSlot.HitTaken: return HitTakenShape;
                case VoiceSlot.Cast: return CastShape;
                case VoiceSlot.Kill: return KillShape;
                case VoiceSlot.Death: return DeathShape;
                case VoiceSlot.Victory: return VictoryShape;
                case VoiceSlot.Quit: return QuitShape;
                default: throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
            }
        }

        /// <summary>FNV-1a over the UTF-16 code units: stable across runtimes.</summary>
        private static uint Hash(string s)
        {
            uint h = 2166136261u;
            foreach (char c in s)
            {
                h ^= c;
                h *= 16777619u;
            }

            return h;
        }
    }
}