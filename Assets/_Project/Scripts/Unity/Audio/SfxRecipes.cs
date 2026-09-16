// Assets/_Project/Scripts/Unity/Audio/SfxRecipes.cs
using System;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>
    /// Every sound effect the game asks for by name (AUDIO.md). A clip at
    /// <c>Assets/_Project/Audio/Resources/Audio/SFX/&lt;Cue&gt;</c> replaces the synthesized one.
    /// </summary>
    public enum SoundCue
    {
        DiceShake,
        DiceLand,
        Doubles,
        Step,
        Rise,
        CastTell,
        CastCell,
        Hit,
        HitBig,
        Heal,
        Miss,
        Block,
        Knockout,
        TurnStart,
        UiClick,
    }

    /// <summary>
    /// The synthesized placeholder for each <see cref="SoundCue"/>
    /// (AUDIO.md decision 1): ivory dice, soft steps, thumps, a cyan zap for
    /// casts, a glassy shatter for knockouts, and a casino bell for the turn.
    /// </summary>
    /// <remarks>
    /// Plain C#. Each cue has a few variants built from different seeds, so
    /// repeats don't sound identical. Buffers are mono at
    /// <see cref="Synth.SampleRate"/>, peak-normalized per cue; the per-cue
    /// playback volume lives in <c>SoundBank</c>.
    /// </remarks>
    public static class SfxRecipes
    {
        /// <summary>How many variants a cue gets.</summary>
        public static int VariantsOf(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.DiceShake:
                case SoundCue.DiceLand:
                case SoundCue.Hit:
                case SoundCue.HitBig: return 3;
                case SoundCue.Step: return 4;
                default: return 1;
            }
        }

        public static float[] Build(SoundCue cue, int variant)
        {
            var random = new SynthRandom(((int)cue + 1) * 7919 + variant * 104729);

            switch (cue)
            {
                case SoundCue.DiceShake: return DiceShake(random);
                case SoundCue.DiceLand: return DiceLand(random);
                case SoundCue.Doubles: return Doubles();
                case SoundCue.Step: return Step(random, variant);
                case SoundCue.Rise: return Rise(random);
                case SoundCue.CastTell: return CastTell(random, falling: false);
                case SoundCue.CastCell: return CastTell(random, falling: true);
                case SoundCue.Hit: return Hit(random, big: false);
                case SoundCue.HitBig: return Hit(random, big: true);
                case SoundCue.Heal: return Heal();
                case SoundCue.Miss: return Miss(random);
                case SoundCue.Block: return Block(random);
                case SoundCue.Knockout: return Knockout(random);
                case SoundCue.TurnStart: return TurnStart();
                case SoundCue.UiClick: return UiClick(random);
                default: throw new ArgumentOutOfRangeException(nameof(cue), cue, null);
            }
        }

        /// <summary>One ivory-on-felt clack: a short band of noise and a hard, quickly damped tone.</summary>
        private static void Clack(float[] b, float at, float gain, float pitch, SynthRandom r)
        {
            Synth.Noise(b, at, 0.03f, gain, 0.0005f, 0.006f, 7000f, 1800f, r);
            Synth.Tone(b, at, 0.05f, pitch, pitch * 0.92f, Wave.Triangle, gain * 0.6f, 0.0005f, 0.012f);
            Synth.Tone(b, at, 0.04f, pitch * 2.63f, pitch * 2.5f, Wave.Sine, gain * 0.25f, 0.0005f, 0.006f);
        }

        private static float[] DiceShake(SynthRandom r)
        {
            // The dice rattle as they are thrown in: a burst of clacks that thins out.
            var b = Synth.Buffer(0.62f);
            float t = 0.01f;

            while (t < 0.52f)
            {
                Clack(b, t, r.Range(0.35f, 1f) * (1f - t), r.Range(1700f, 2600f), r);
                t += r.Range(0.035f, 0.07f) * (1f + t * 2f);
            }

            Synth.Normalize(b, 0.9f);
            return b;
        }

        private static float[] DiceLand(SynthRandom r)
        {
            // Two dice settling: two firm knocks and a small bounce each.
            var b = Synth.Buffer(0.4f);

            Knock(b, 0.0f, 1f, r.Range(1150f, 1350f), r);
            Knock(b, r.Range(0.05f, 0.08f), 0.85f, r.Range(950f, 1150f), r);
            Clack(b, r.Range(0.16f, 0.2f), 0.25f, 1500f, r);
            Clack(b, r.Range(0.22f, 0.26f), 0.15f, 1300f, r);

            Synth.Normalize(b, 0.95f);
            return b;
        }

        private static void Knock(float[] b, float at, float gain, float pitch, SynthRandom r)
        {
            Clack(b, at, gain, pitch, r);

            // The table's body under the die.
            Synth.Tone(b, at, 0.09f, 240f, 150f, Wave.Sine, gain * 0.5f, 0.001f, 0.03f);
            Synth.Noise(b, at, 0.05f, gain * 0.35f, 0.0005f, 0.015f, 2500f, 0f, r);
        }

        private static float[] Doubles()
        {
            // A bright two-note chime, up a fourth.
            var b = Synth.Buffer(0.8f);
            float[] partials = { 1f, 2.0f, 3.01f };

            Synth.Bell(b, 0.0f, 0.5f, Synth.Midi(88), partials, 0.6f, 0.18f);
            Synth.Bell(b, 0.12f, 0.68f, Synth.Midi(93), partials, 0.7f, 0.28f);

            Synth.Normalize(b, 0.8f);
            return b;
        }

        private static float[] Step(SynthRandom r, int variant)
        {
            // A soft shoe on marble: a low tick and a breath of noise. Quiet by design.
            var b = Synth.Buffer(0.09f);
            float pitch = 420f * (1f + 0.06f * (variant - 1.5f));

            Synth.Tone(b, 0f, 0.06f, pitch, pitch * 0.7f, Wave.Sine, 0.7f, 0.001f, 0.014f);
            Synth.Noise(b, 0f, 0.05f, 0.45f, 0.001f, 0.01f, 3200f, 400f, r);

            Synth.Normalize(b, 0.8f);
            return b;
        }

        private static float[] Rise(SynthRandom r)
        {
            // Standing up from the table: a rising swell over a soft thump.
            var b = Synth.Buffer(0.42f);

            Synth.Tone(b, 0f, 0.4f, 260f, 620f, Wave.Triangle, 0.45f, 0.08f, 0.2f, lowpassHz: 2200f);
            Synth.Tone(b, 0.02f, 0.38f, 390f, 930f, Wave.Sine, 0.25f, 0.1f, 0.18f);
            Synth.Tone(b, 0.24f, 0.16f, 110f, 70f, Wave.Sine, 0.8f, 0.002f, 0.05f);
            Synth.Noise(b, 0.24f, 0.06f, 0.2f, 0.001f, 0.015f, 1800f, 0f, r);

            Synth.Normalize(b, 0.8f);
            return b;
        }

        private static float[] CastTell(SynthRandom r, bool falling)
        {
            // The cyan register: a filtered zap with a shimmer on top. A cell
            // cast falls and ends on a ping where it lands.
            var b = Synth.Buffer(0.55f);

            float from = falling ? 1500f : 380f;
            float to = falling ? 320f : 1400f;

            Synth.Tone(b, 0f, 0.3f, from, to, Wave.Triangle, 0.5f, 0.005f, 0.14f, lowpassHz: 3200f);
            Synth.Tone(b, 0f, 0.35f, from * 2f, to * 2f, Wave.Sine, 0.25f, 0.02f, 0.12f,
                vibratoHz: 24f, vibratoDepth: 0.03f);
            Synth.Noise(b, 0f, 0.25f, 0.12f, 0.03f, 0.08f, 9000f, 4000f, r);

            float ping = falling ? Synth.Midi(84) : Synth.Midi(91);
            Synth.Bell(b, 0.3f, 0.25f, ping, new[] { 1f, 2.76f }, 0.35f, 0.07f);

            Synth.Normalize(b, 0.75f);
            return b;
        }

        private static float[] Hit(SynthRandom r, bool big)
        {
            // A body blow: a pitched-down thump and a crack of noise. The big
            // one is lower, longer and driven.
            float length = big ? 0.45f : 0.22f;
            var b = Synth.Buffer(length);
            float jitter = r.Range(0.92f, 1.08f);

            Synth.Tone(b, 0f, length, (big ? 150f : 190f) * jitter, (big ? 45f : 70f) * jitter,
                Wave.Sine, 1f, 0.001f, big ? 0.12f : 0.05f);
            Synth.Noise(b, 0f, big ? 0.12f : 0.05f, big ? 0.7f : 0.5f, 0.0005f, big ? 0.03f : 0.012f,
                big ? 3000f : 4500f, 150f, r);

            if (big)
            {
                Synth.Tone(b, 0.005f, 0.3f, 75f, 40f, Wave.Triangle, 0.5f, 0.002f, 0.1f, lowpassHz: 400f);
                Synth.Drive(b, 2.2f);
            }

            Synth.Normalize(b, big ? 0.95f : 0.85f);
            return b;
        }

        private static float[] Heal()
        {
            // A rising, soft arpeggio: C, E, G, C.
            var b = Synth.Buffer(0.7f);
            int[] notes = { 84, 88, 91, 96 };

            for (int i = 0; i < notes.Length; i++)
                Synth.Tone(b, i * 0.07f, 0.45f, Synth.Midi(notes[i]), Synth.Midi(notes[i]), Wave.Sine,
                    0.35f, 0.01f, 0.15f, vibratoHz: 6f, vibratoDepth: 0.004f);

            Synth.Normalize(b, 0.7f);
            return b;
        }

        private static float[] Miss(SynthRandom r)
        {
            // A whoosh past the target: noise swelling and fading, high-passed.
            var b = Synth.Buffer(0.32f);

            Synth.Noise(b, 0f, 0.3f, 1f, 0.12f, 0.08f, 5000f, 900f, r);
            Synth.Noise(b, 0.05f, 0.22f, 0.5f, 0.08f, 0.06f, 2500f, 500f, r);

            Synth.Normalize(b, 0.7f);
            return b;
        }

        private static float[] Block(SynthRandom r)
        {
            // Plate armour: an inharmonic clank over a click.
            var b = Synth.Buffer(0.5f);

            Synth.Noise(b, 0f, 0.02f, 0.8f, 0.0005f, 0.004f, 9000f, 2000f, r);
            Synth.Bell(b, 0f, 0.48f, 523f, new[] { 1f, 2.49f, 3.99f, 6.41f }, 0.6f, 0.12f);
            Synth.Tone(b, 0f, 0.1f, 200f, 120f, Wave.Sine, 0.4f, 0.001f, 0.03f);

            Synth.Normalize(b, 0.8f);
            return b;
        }

        private static float[] Knockout(SynthRandom r)
        {
            // Glass giving way over a low boom: a spray of tiny bright shards.
            var b = Synth.Buffer(0.9f);

            Synth.Tone(b, 0f, 0.7f, 90f, 32f, Wave.Sine, 1f, 0.002f, 0.22f);
            Synth.Noise(b, 0f, 0.2f, 0.6f, 0.001f, 0.06f, 2000f, 60f, r);

            for (int i = 0; i < 26; i++)
            {
                float at = r.Range(0f, 0.45f) * r.Range(0.3f, 1f);
                float pitch = r.Range(2400f, 6200f);
                float gain = r.Range(0.12f, 0.35f) * (1f - at);
                Synth.Tone(b, at, 0.12f, pitch, pitch * 1.02f, Wave.Sine, gain, 0.0005f, 0.025f);
                Synth.Noise(b, at, 0.02f, gain * 0.6f, 0.0005f, 0.005f, 11000f, 3500f, r);
            }

            Synth.Drive(b, 1.4f);
            Synth.Normalize(b, 0.95f);
            return b;
        }

        private static float[] TurnStart()
        {
            // The casino floor bell, soft: two strikes a third apart.
            var b = Synth.Buffer(1.1f);
            float[] partials = { 1f, 2.4f, 3.0f, 4.5f };

            Synth.Bell(b, 0f, 0.9f, Synth.Midi(76), partials, 0.5f, 0.35f);
            Synth.Bell(b, 0.11f, 0.95f, Synth.Midi(79), partials, 0.4f, 0.4f);

            Synth.Normalize(b, 0.6f);
            return b;
        }

        private static float[] UiClick(SynthRandom r)
        {
            // A small brass tick.
            var b = Synth.Buffer(0.05f);

            Synth.Tone(b, 0f, 0.04f, 2100f, 1800f, Wave.Triangle, 0.6f, 0.0005f, 0.006f);
            Synth.Noise(b, 0f, 0.012f, 0.3f, 0.0005f, 0.003f, 8000f, 2500f, r);

            Synth.Normalize(b, 0.6f);
            return b;
        }
    }
}