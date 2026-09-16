// Assets/_Project/Scripts/Unity/Audio/Synth.cs
using System;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>Oscillator shapes for <see cref="Synth.Tone"/>.</summary>
    public enum Wave
    {
        Sine,
        Triangle,
        Square,
        Saw,

        /// <summary>A narrow pulse (30% duty): the buzz of a free reed, for the accordion.</summary>
        Reed,
    }

    /// <summary>
    /// A small deterministic random stream for synthesis (xorshift32). The
    /// sounds are view-only, so this never touches the match RNG.
    /// </summary>
    public sealed class SynthRandom
    {
        private uint _state;

        public SynthRandom(int seed) => _state = (uint)seed * 2654435761u + 1u;

        public uint NextUInt()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        /// <summary>[0, 1).</summary>
        public float Next() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>[-1, 1).</summary>
        public float Signed() => Next() * 2f - 1f;

        public float Range(float min, float max) => min + (max - min) * Next();
    }

    /// <summary>
    /// The placeholder sound engine (AUDIO.md decision 1): tones, noise and
    /// filters written into mono float buffers.
    /// </summary>
    /// <remarks>
    /// <b>Plain C#, no Unity types</b>, like the core, so the recipes can be
    /// rendered to WAV and heard outside the editor. <c>SoundBank</c> turns the
    /// buffers into clips.
    ///
    /// <b>Everything is written additively.</b> A recipe lays events into one
    /// buffer at chosen times. With <c>loop</c> set, the part of an event
    /// that runs past the end wraps onto the start, so a music bar can ring
    /// across the loop point without a seam.
    /// </remarks>
    public static class Synth
    {
        public const int SampleRate = 44100;

        private const float TwoPi = MathF.PI * 2f;

        /// <summary>Seconds of fade-out every event gets, so nothing ends on a click.</summary>
        private const float Declick = 0.004f;

        public static int Samples(float seconds) => Math.Max(1, (int)(seconds * SampleRate));

        public static float[] Buffer(float seconds) => new float[Samples(seconds)];

        /// <summary>Frequency of a MIDI note (69 = A4 = 440 Hz).</summary>
        public static float Midi(float note) => 440f * MathF.Pow(2f, (note - 69f) / 12f);

        /// <summary>A decibel value as a linear gain.</summary>
        public static float Db(float db) => MathF.Pow(10f, db / 20f);

        /// <summary>
        /// Attack, then exponential decay with the given time constant, then a
        /// short fade at <paramref name="duration"/>.
        /// </summary>
        public static float Envelope(float t, float duration, float attack, float decay)
        {
            if (t < 0f || t >= duration) return 0f;

            float env = t < attack ? t / attack : MathF.Exp(-(t - attack) / MathF.Max(0.0001f, decay));
            float tail = duration - t;
            if (tail < Declick) env *= tail / Declick;
            return env;
        }

        /// <summary>One sample of a wave at a phase in cycles.</summary>
        public static float Osc(Wave wave, float phase)
        {
            float p = phase - MathF.Floor(phase);

            switch (wave)
            {
                case Wave.Triangle: return 4f * MathF.Abs(p - 0.5f) - 1f;
                case Wave.Square: return p < 0.5f ? 1f : -1f;
                case Wave.Saw: return 2f * p - 1f;
                case Wave.Reed: return p < 0.3f ? 1f : -0.43f;
                default: return MathF.Sin(TwoPi * p);
            }
        }

        /// <summary>
        /// Adds a tone that glides exponentially from <paramref name="from"/>
        /// to <paramref name="to"/> Hz. With <paramref name="loop"/>, samples past
        /// the end wrap to the start.
        /// </summary>
        public static void Tone(float[] buffer, float start, float duration, float from, float to,
            Wave wave, float gain, float attack, float decay,
            float vibratoHz = 0f, float vibratoDepth = 0f, float lowpassHz = 0f, bool loop = false)
        {
            int first = (int)(start * SampleRate);
            int count = Samples(duration);
            float phase = 0f;
            float lp = 0f;
            float coef = lowpassHz > 0f ? Coefficient(lowpassHz) : 1f;
            float ratio = to / MathF.Max(1f, from);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float env = Envelope(t, duration, attack, decay);

                float freq = from * MathF.Pow(ratio, t / duration);
                if (vibratoDepth > 0f) freq *= 1f + vibratoDepth * MathF.Sin(TwoPi * vibratoHz * t);

                phase += freq / SampleRate;

                float s = Osc(wave, phase);
                lp += coef * (s - lp);

                Write(buffer, first + i, lp * env * gain, loop);
            }
        }

        /// <summary>
        /// Adds filtered noise. <paramref name="lowpassHz"/> and
        /// <paramref name="highpassHz"/> are one-pole corners; 0 turns one off.
        /// </summary>
        public static void Noise(float[] buffer, float start, float duration, float gain,
            float attack, float decay, float lowpassHz, float highpassHz, SynthRandom random, bool loop = false)
        {
            int first = (int)(start * SampleRate);
            int count = Samples(duration);
            float lowCoef = lowpassHz > 0f ? Coefficient(lowpassHz) : 1f;
            float highCoef = highpassHz > 0f ? Coefficient(highpassHz) : 0f;
            float low = 0f, slow = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float env = Envelope(t, duration, attack, decay);

                low += lowCoef * (random.Signed() - low);
                slow += highCoef * (low - slow);
                float s = highpassHz > 0f ? low - slow : low;

                Write(buffer, first + i, s * env * gain, loop);
            }
        }

        /// <summary>
        /// Adds noise through a resonant band-pass whose centre glides
        /// exponentially from <paramref name="fromHz"/> to <paramref name="toHz"/>:
        /// the body of a physical thing (felt, wood, leather, a plate).
        /// </summary>
        /// <remarks>
        /// A very short excitation (a few milliseconds) rings the filter the way
        /// a knock rings an object: the ring lasts about Q / (π · f), so low Q
        /// gives a dull thump and high Q a hard knock. A longer, swelling
        /// excitation with low Q gives a whoosh. Unlike <see cref="Tone"/>, the
        /// result has no clean pitch, which keeps effects from sounding like
        /// notes. The filter is a trapezoidal state-variable one, stable under
        /// any glide; its output is scaled to unity gain at the centre.
        /// </remarks>
        public static void Resonate(float[] buffer, float start, float duration, float gain,
            float attack, float decay, float fromHz, float toHz, float q, SynthRandom random, bool loop = false)
        {
            int first = (int)(start * SampleRate);
            int count = Samples(duration);
            float k = 1f / MathF.Max(0.3f, q);
            float ratio = toHz / MathF.Max(1f, fromHz);
            float ic1 = 0f, ic2 = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float env = Envelope(t, duration, attack, decay);

                float hz = MathF.Min(fromHz * MathF.Pow(ratio, t / duration), SampleRate * 0.45f);
                float g = MathF.Tan(MathF.PI * hz / SampleRate);
                float a1 = 1f / (1f + g * (g + k));
                float a2 = g * a1;
                float a3 = g * a2;

                float v3 = random.Signed() * env - ic2;
                float v1 = a1 * ic1 + a2 * v3;
                float v2 = ic2 + a2 * ic1 + a3 * v3;
                ic1 = 2f * v1 - ic1;
                ic2 = 2f * v2 - ic2;

                float tail = count - i < Declick * SampleRate ? (count - i) / (Declick * SampleRate) : 1f;
                Write(buffer, first + i, k * v1 * gain * tail, loop);
            }
        }

        /// <summary>
        /// Rolls the highs off the whole buffer (two one-pole stages, 12 dB per
        /// octave above <paramref name="hz"/>): the difference between a sound
        /// heard across a room and one held to the ear.
        /// </summary>
        public static void Darken(float[] buffer, float hz)
        {
            float coef = Coefficient(hz);
            float a = 0f, b = 0f;

            for (int i = 0; i < buffer.Length; i++)
            {
                a += coef * (buffer[i] - a);
                b += coef * (a - b);
                buffer[i] = b;
            }
        }

        /// <summary>
        /// Adds a metallic ring: inharmonic partials, each decaying on its own.
        /// </summary>
        public static void Bell(float[] buffer, float start, float duration, float fundamental,
            float[] partials, float gain, float decay, bool loop = false)
        {
            for (int k = 0; k < partials.Length; k++)
            {
                float partialGain = gain / (1f + k * 0.7f);
                Tone(buffer, start, duration, fundamental * partials[k], fundamental * partials[k],
                    Wave.Sine, partialGain, 0.001f, decay / (1f + k * 0.5f), loop: loop);
            }
        }

        /// <summary>
        /// Adds a plucked string (Karplus–Strong): a burst of noise circulating
        /// in a tuned delay line that loses its highs on every pass.
        /// </summary>
        /// <param name="brightness">0–1: how much treble the pluck starts with.</param>
        /// <param name="ring">Seconds for the string to fall to about a third.</param>
        public static void Pluck(float[] buffer, float start, float duration, float freq, float gain,
            float brightness, float ring, SynthRandom random, bool loop = false)
        {
            // The averaging filter below adds half a sample of delay; take it off
            // the line so the pitch stays true. Read with linear interpolation
            // so high notes aren't pulled out of tune by whole-sample rounding.
            float delay = MathF.Max(2f, SampleRate / freq - 0.5f);
            int size = (int)delay + 3;
            var line = new float[size];

            float coef = Math.Clamp(brightness, 0.05f, 1f);
            float lp = 0f;
            for (int i = 0; i < size; i++)
            {
                lp += coef * (random.Signed() - lp);
                line[i] = lp;
            }

            float loss = MathF.Exp(-delay / (SampleRate * MathF.Max(0.01f, ring)));
            int first = (int)(start * SampleRate);
            int count = Samples(duration);
            int write = 0;
            float last = 0f;

            for (int i = 0; i < count; i++)
            {
                float readPos = write - delay;
                if (readPos < 0f) readPos += size;
                int i0 = (int)readPos;
                float frac = readPos - i0;
                float y = line[i0] * (1f - frac) + line[(i0 + 1) % size] * frac;

                line[write] = 0.5f * (y + last) * loss;
                last = y;
                write = (write + 1) % size;

                float tail = count - i < Declick * SampleRate ? (count - i) / (Declick * SampleRate) : 1f;
                Write(buffer, first + i, y * gain * tail, loop);
            }
        }

        /// <summary>Soft saturation, for weight on big hits.</summary>
        public static void Drive(float[] buffer, float amount)
        {
            float norm = MathF.Tanh(amount);
            for (int i = 0; i < buffer.Length; i++) buffer[i] = MathF.Tanh(buffer[i] * amount) / norm;
        }

        /// <summary>Scales the buffer so its loudest sample sits at <paramref name="peak"/>.</summary>
        public static void Normalize(float[] buffer, float peak)
        {
            float max = 0f;
            for (int i = 0; i < buffer.Length; i++) max = MathF.Max(max, MathF.Abs(buffer[i]));
            if (max < 1e-6f) return;

            float scale = peak / max;
            for (int i = 0; i < buffer.Length; i++) buffer[i] *= scale;
        }

        /// <summary>The loudest absolute sample.</summary>
        public static float Peak(float[] buffer)
        {
            float max = 0f;
            for (int i = 0; i < buffer.Length; i++) max = MathF.Max(max, MathF.Abs(buffer[i]));
            return max;
        }

        private static float Coefficient(float hz) =>
            1f - MathF.Exp(-TwoPi * MathF.Min(hz, SampleRate * 0.45f) / SampleRate);

        private static void Write(float[] buffer, int index, float value, bool loop)
        {
            if (index < 0) return;

            if (index >= buffer.Length)
            {
                if (!loop) return;
                index %= buffer.Length;
            }

            buffer[index] += value;
        }
    }
}