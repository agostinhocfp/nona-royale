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

        /// <summary>Under a Tech hit: an electrical fizz (AU3). Normal has no layer; the hit is its family.</summary>
        LayerTech,

        /// <summary>Under an Atomic hit: a sub drop and a pressure crack, so it sounds like nothing stopped it (AU3).</summary>
        LayerAtomic,
    }

    /// <summary>
    /// The synthesized placeholder for each <see cref="SoundCue"/>
    /// (AUDIO.md decision 1).
    /// </summary>
    /// <remarks>
    /// <b>The palette is the table, not a toy box</b> (AU1d). Every cue is a
    /// physical thing heard in a quiet room: weighted pieces set down on felt,
    /// clay chips, a card dealt across the cloth, a switch, a body blow, a
    /// pressure swell. Bodies come from noise ringing a resonant filter
    /// (<see cref="Synth.Resonate"/>), so nothing has a clean pitch, and no cue
    /// plays a melody or an interval. Sine tones appear only below about
    /// 120 Hz, as weight you feel rather than a note you hear. Most cues are
    /// darkened (<see cref="Synth.Darken"/>) so they sit under the music and
    /// the voices.
    ///
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
                case SoundCue.HitBig:
                case SoundCue.UiClick: return 3;
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
                case SoundCue.Doubles: return Doubles(random);
                case SoundCue.Step: return Step(random, variant);
                case SoundCue.Rise: return Rise(random);
                case SoundCue.CastTell: return CastTell(random);
                case SoundCue.CastCell: return CastCell(random);
                case SoundCue.Hit: return Hit(random, big: false);
                case SoundCue.HitBig: return Hit(random, big: true);
                case SoundCue.Heal: return Heal(random);
                case SoundCue.Miss: return Miss(random);
                case SoundCue.Block: return Block(random);
                case SoundCue.Knockout: return Knockout(random);
                case SoundCue.TurnStart: return TurnStart(random);
                case SoundCue.UiClick: return UiClick(random, variant);
                case SoundCue.LayerTech: return LayerTech(random);
                case SoundCue.LayerAtomic: return LayerAtomic(random);
                default: throw new ArgumentOutOfRangeException(nameof(cue), cue, null);
            }
        }

        // ── Building blocks ──────────────────────────────────────────────
        // Internal so SignatureRecipes (AU3) builds on the same palette.

        /// <summary>
        /// Something with weight meeting a soft surface: a low body pressed
        /// rather than struck (felt spreads the impact), a knock above it that
        /// small speakers can still carry, and a muffled contact.
        /// </summary>
        /// <param name="ring">Scales how long the body rings.</param>
        /// <param name="weight">Sub-bass under the body, for heavy things only (0 for none).</param>
        internal static void Thud(float[] b, float at, float gain, float bodyHz, SynthRandom r,
            float ring = 1f, float weight = 0f)
        {
            Synth.Resonate(b, at, 0.1f * ring, gain * 7f, 0.001f, 0.009f * ring, bodyHz, bodyHz * 0.9f, 5f, r);
            Synth.Resonate(b, at, 0.05f * ring, gain * 3.5f, 0.0005f, 0.005f * ring,
                bodyHz * 2.6f, bodyHz * 2.5f, 3.5f, r);
            Synth.Noise(b, at, 0.03f, gain * 0.35f, 0.0005f, 0.005f, 1600f, 250f, r);

            if (weight > 0f)
            {
                float sub = MathF.Min(bodyHz * 0.7f, 90f);
                Synth.Tone(b, at, 0.1f * ring, sub, sub * 0.7f, Wave.Sine, gain * weight, 0.002f, 0.025f * ring);
            }
        }

        /// <summary>
        /// A small, hard contact: two dull modes excited by a click. The pair
        /// never forms an interval, so it reads as material, not a note.
        /// </summary>
        internal static void Tick(float[] b, float at, float gain, float centerHz, SynthRandom r)
        {
            Synth.Resonate(b, at, 0.03f, gain * 2.2f, 0.0003f, 0.0015f, centerHz, centerHz * 0.97f, 3.5f, r);
            Synth.Resonate(b, at, 0.025f, gain * 1.1f, 0.0003f, 0.0012f,
                centerHz * 1.73f, centerHz * 1.7f, 4f, r);
        }

        /// <summary>A body moving through air, or cloth over cloth: a swelling, gliding band of noise.</summary>
        internal static void Swish(float[] b, float at, float duration, float gain, float fromHz, float toHz,
            float attack, float decay, SynthRandom r, float q = 0.9f)
        {
            Synth.Resonate(b, at, duration, gain, attack, decay, fromHz, toHz, q, r);
        }

        /// <summary>A clay chip or a die against another: a short, dense knock.</summary>
        internal static void Clack(float[] b, float at, float gain, float pitch, SynthRandom r)
        {
            Synth.Resonate(b, at, 0.03f, gain * 2f, 0.0003f, 0.002f, pitch, pitch * 0.96f, 5f, r);
            Synth.Resonate(b, at, 0.025f, gain * 1.2f, 0.0003f, 0.0015f, pitch * 2.31f, pitch * 2.25f, 6f, r);
            Synth.Noise(b, at, 0.015f, gain * 0.3f, 0.0003f, 0.002f, 6000f, 1500f, r);
        }

        /// <summary>
        /// Electrical crackle: a scatter of very short, bright noise bursts.
        /// Density falls off across the span unless <paramref name="build"/>
        /// is set, which grows it instead, like a charge gathering.
        /// </summary>
        internal static void Crackle(float[] b, float at, float duration, float gain, int bursts, SynthRandom r,
            bool build = false)
        {
            for (int i = 0; i < bursts; i++)
            {
                // Squaring the position crowds the bursts toward one end.
                float u = r.Next();
                u = build ? 1f - u * u : u * u;
                float t = at + u * duration;
                float g = gain * r.Range(0.4f, 1f) * (build ? 0.4f + 0.6f * u : 1f - 0.6f * u);
                Synth.Noise(b, t, r.Range(0.001f, 0.004f), g, 0.0002f, 0.0015f, 9000f, r.Range(2000f, 4000f), r);
            }
        }

        // ── Dice (fallbacks: the Kenney files replace these) ─────────────

        private static float[] DiceShake(SynthRandom r)
        {
            // The dice rattle as they are thrown in: a burst of clacks that thins out.
            var b = Synth.Buffer(0.62f);
            float t = 0.01f;

            while (t < 0.52f)
            {
                Clack(b, t, r.Range(0.35f, 1f) * (1f - t), r.Range(1500f, 2200f), r);
                t += r.Range(0.035f, 0.07f) * (1f + t * 2f);
            }

            Synth.Darken(b, 7000f);
            Synth.Normalize(b, 0.9f);
            return b;
        }

        private static float[] DiceLand(SynthRandom r)
        {
            // Two dice settling on felt: two firm knocks and a small bounce each.
            var b = Synth.Buffer(0.4f);

            Clack(b, 0f, 1f, r.Range(1100f, 1300f), r);
            Thud(b, 0f, 0.6f, 230f, r);
            float second = r.Range(0.05f, 0.08f);
            Clack(b, second, 0.85f, r.Range(950f, 1100f), r);
            Thud(b, second, 0.5f, 210f, r);
            Clack(b, r.Range(0.16f, 0.2f), 0.25f, 1400f, r);
            Clack(b, r.Range(0.22f, 0.26f), 0.15f, 1250f, r);

            Synth.Darken(b, 7000f);
            Synth.Normalize(b, 0.95f);
            return b;
        }

        // ── Board ────────────────────────────────────────────────────────

        private static float[] Doubles(SynthRandom r)
        {
            // Two chips knocked together on the rail: a quiet nod, not a fanfare.
            var b = Synth.Buffer(0.32f);

            Clack(b, 0f, 1f, 2300f, r);
            Clack(b, 0.075f, 0.75f, 2050f, r);
            Thud(b, 0.075f, 0.12f, 260f, r);

            Synth.Darken(b, 6500f);
            Synth.Normalize(b, 0.9f);
            return b;
        }

        private static float[] Step(SynthRandom r, int variant)
        {
            // A weighted piece set down on felt: a low body, a muffled edge, a scuff of cloth.
            var b = Synth.Buffer(0.13f);
            float body = 165f * (1f + 0.07f * (variant - 1.5f));

            Thud(b, 0f, 1f, body, r);
            Tick(b, 0.001f, 0.18f, r.Range(850f, 1050f), r);
            Swish(b, 0f, 0.05f, 0.12f, 700f, 500f, 0.006f, 0.015f, r);

            Synth.Darken(b, 3200f);
            Synth.Normalize(b, 0.75f);
            return b;
        }

        private static float[] Rise(SynthRandom r)
        {
            // A piece slid out onto the cloth and set down.
            var b = Synth.Buffer(0.5f);

            Swish(b, 0f, 0.3f, 0.55f, 450f, 850f, 0.14f, 0.08f, r);
            Thud(b, 0.27f, 1f, 130f, r, ring: 1.3f);
            Tick(b, 0.271f, 0.15f, 780f, r);

            Synth.Darken(b, 3500f);
            Synth.Normalize(b, 0.8f);
            return b;
        }

        private static float[] TurnStart(SynthRandom r)
        {
            // A card dealt across the cloth to the next seat, and a tap as it stops.
            var b = Synth.Buffer(0.4f);

            Swish(b, 0f, 0.2f, 0.45f, 3000f, 2100f, 0.08f, 0.05f, r, q: 0.8f);
            Swish(b, 0.02f, 0.16f, 0.25f, 900f, 700f, 0.06f, 0.05f, r);
            Tick(b, 0.17f, 0.4f, 1500f, r);
            Thud(b, 0.17f, 0.45f, 210f, r);

            Synth.Darken(b, 4500f);
            Synth.Normalize(b, 0.6f);
            return b;
        }

        // ── Abilities ────────────────────────────────────────────────────

        private static float[] CastTell(SynthRandom r)
        {
            // Pressure gathering: a low, breathing swell with weight under it.
            var b = Synth.Buffer(0.62f);

            Swish(b, 0f, 0.55f, 1f, 260f, 700f, 0.2f, 0.14f, r, q: 1.4f);
            Synth.Tone(b, 0f, 0.5f, 60f, 95f, Wave.Sine, 0.08f, 0.16f, 0.14f);
            Synth.Noise(b, 0.05f, 0.45f, 0.06f, 0.18f, 0.1f, 6000f, 2500f, r);

            Synth.Darken(b, 2800f);
            Synth.Normalize(b, 0.75f);
            return b;
        }

        private static float[] CastCell(SynthRandom r)
        {
            // Something thrown: it cuts the air, falls, and lands with a dull thump.
            var b = Synth.Buffer(0.6f);

            Swish(b, 0f, 0.34f, 0.8f, 1300f, 320f, 0.1f, 0.12f, r, q: 1.2f);
            Thud(b, 0.3f, 1f, 105f, r, ring: 1.5f);
            Synth.Noise(b, 0.3f, 0.2f, 0.12f, 0.004f, 0.06f, 1400f, 200f, r);

            Synth.Darken(b, 3200f);
            Synth.Normalize(b, 0.75f);
            return b;
        }

        private static float[] Heal(SynthRandom r)
        {
            // A long exhale over a low, soft fifth: the tension going out.
            var b = Synth.Buffer(0.9f);

            Swish(b, 0f, 0.8f, 1.4f, 380f, 950f, 0.3f, 0.25f, r, q: 0.8f);
            Synth.Tone(b, 0.05f, 0.8f, Synth.Midi(50), Synth.Midi(50), Wave.Triangle, 0.22f, 0.22f, 0.35f,
                vibratoHz: 4.5f, vibratoDepth: 0.003f, lowpassHz: 600f);
            Synth.Tone(b, 0.05f, 0.8f, Synth.Midi(57), Synth.Midi(57), Wave.Triangle, 0.14f, 0.26f, 0.32f,
                vibratoHz: 5f, vibratoDepth: 0.003f, lowpassHz: 600f);

            Synth.Darken(b, 2400f);
            Synth.Normalize(b, 0.6f);
            return b;
        }

        // ── Combat ───────────────────────────────────────────────────────

        private static float[] Hit(SynthRandom r, bool big)
        {
            // A body blow: a slap on top, a chest underneath. The big one is
            // lower, longer, and carries some debris.
            float length = big ? 0.45f : 0.22f;
            var b = Synth.Buffer(length);
            float j = r.Range(0.92f, 1.08f);
            float body = (big ? 88f : 125f) * j;

            Synth.Noise(b, 0f, 0.025f, 0.7f, 0.0003f, big ? 0.008f : 0.006f, 3800f, 600f, r);
            Thud(b, 0f, 1.2f, body, r, ring: big ? 2f : 1.2f);
            Synth.Resonate(b, 0.001f, 0.08f, 3f, 0.0005f, big ? 0.012f : 0.008f, body * 2.7f, body * 2.3f, 2f, r);
            Synth.Tone(b, 0f, big ? 0.35f : 0.16f, body * 0.75f, body * 0.45f, Wave.Sine, big ? 0.3f : 0.15f, 0.001f,
                big ? 0.09f : 0.045f);

            if (big) Synth.Noise(b, 0.01f, 0.2f, 0.25f, 0.002f, 0.06f, 1200f, 90f, r);

            Synth.Drive(b, big ? 3f : 1.8f);

            Synth.Darken(b, big ? 4000f : 5000f);
            Synth.Normalize(b, big ? 0.95f : 0.85f);
            return b;
        }

        private static float[] Miss(SynthRandom r)
        {
            // A blow that finds only air: a close, low whoosh.
            var b = Synth.Buffer(0.3f);

            Swish(b, 0f, 0.28f, 1f, 1500f, 450f, 0.09f, 0.07f, r, q: 1.1f);
            Swish(b, 0.03f, 0.22f, 0.4f, 700f, 300f, 0.07f, 0.06f, r, q: 0.8f);

            Synth.Darken(b, 4500f);
            Synth.Normalize(b, 0.65f);
            return b;
        }

        private static float[] Block(SynthRandom r)
        {
            // A guard taking the blow: a dull plate knock over a braced thump. No ring.
            var b = Synth.Buffer(0.3f);

            Synth.Noise(b, 0f, 0.015f, 0.6f, 0.0003f, 0.003f, 5000f, 1200f, r);
            Synth.Resonate(b, 0f, 0.12f, 2.4f, 0.0005f, 0.003f, 1250f, 1200f, 9f, r);
            Synth.Resonate(b, 0f, 0.09f, 1.3f, 0.0005f, 0.002f, 2080f, 2040f, 10f, r);
            Thud(b, 0f, 0.9f, 150f, r);

            Synth.Drive(b, 1.6f);
            Synth.Darken(b, 4500f);
            Synth.Normalize(b, 0.95f);
            return b;
        }

        private static float[] Knockout(SynthRandom r)
        {
            // A body going down hard, and a glass going with it: a heavy drop
            // and a short, dark break. The shards are noise, not notes.
            var b = Synth.Buffer(1f);

            Thud(b, 0f, 1.2f, 72f, r, ring: 3f);
            Synth.Tone(b, 0f, 0.7f, 62f, 34f, Wave.Sine, 0.3f, 0.002f, 0.2f);
            Synth.Noise(b, 0f, 0.35f, 0.35f, 0.002f, 0.09f, 450f, 40f, r);

            Synth.Noise(b, 0.015f, 0.2f, 0.6f, 0.0005f, 0.05f, 7000f, 1800f, r);
            for (int i = 0; i < 16; i++)
            {
                float at = 0.015f + r.Range(0f, 0.3f) * r.Range(0.2f, 1f);
                float hz = r.Range(2200f, 5200f);
                float gain = r.Range(0.6f, 1.4f) * (1f - at * 1.5f);
                Synth.Resonate(b, at, 0.04f, gain * 2f, 0.0003f, r.Range(0.004f, 0.012f), hz, hz * 0.98f,
                    r.Range(10f, 16f), r);
            }

            Synth.Drive(b, 1.3f);
            Synth.Darken(b, 6000f);
            Synth.Normalize(b, 0.95f);
            return b;
        }

        // ── Damage-type layers (AU3) ─────────────────────────────────────

        private static float[] LayerTech(SynthRandom r)
        {
            // A short fizz of current across the contact: crackle over a thin, dark hiss.
            var b = Synth.Buffer(0.22f);

            Crackle(b, 0f, 0.14f, 0.9f, 18, r);
            Synth.Noise(b, 0f, 0.16f, 0.25f, 0.002f, 0.06f, 7000f, 2500f, r);
            Tick(b, 0f, 0.35f, 2600f, r);

            Synth.Darken(b, 7000f);
            Synth.Normalize(b, 0.7f);
            return b;
        }

        private static float[] LayerAtomic(SynthRandom r)
        {
            // Pressure that nothing slowed: a dense crack on the contact and a
            // sub drop under it that keeps going after the blow.
            var b = Synth.Buffer(0.6f);

            Synth.Noise(b, 0f, 0.05f, 0.9f, 0.0003f, 0.012f, 5000f, 300f, r);
            Synth.Tone(b, 0f, 0.5f, 72f, 32f, Wave.Sine, 0.9f, 0.002f, 0.2f);
            Synth.Resonate(b, 0.005f, 0.4f, 1.6f, 0.01f, 0.12f, 140f, 60f, 1.2f, r);

            Synth.Drive(b, 2.2f);
            Synth.Darken(b, 3000f);
            Synth.Normalize(b, 0.9f);
            return b;
        }

        // ── Interface ────────────────────────────────────────────────────

        private static float[] UiClick(SynthRandom r, int variant)
        {
            // A bakelite switch: a dull click and the faint return of the lever.
            var b = Synth.Buffer(0.1f);
            float center = 1250f * (1f + 0.05f * (variant - 1));

            Tick(b, 0f, 1f, center, r);
            Thud(b, 0f, 0.3f, 240f, r);
            Tick(b, 0.014f, 0.22f, center * 1.2f, r);

            Synth.Darken(b, 4200f);
            Synth.Normalize(b, 0.55f);
            return b;
        }
    }
}