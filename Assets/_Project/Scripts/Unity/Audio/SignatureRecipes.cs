// Assets/_Project/Scripts/Unity/Audio/SignatureRecipes.cs
using System;
using System.Collections.Generic;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>
    /// Synthesized stand-ins for the alpha three's signature sounds (AUDIO.md
    /// AU3, decision 3), keyed like the files that replace them:
    /// <c>Bouncer_VelvetRope_Tell</c> (<see cref="AbilitySounds.Key"/>).
    /// </summary>
    /// <remarks>
    /// <b>Same palette as <see cref="SfxRecipes"/></b> (AU1d): physical things
    /// in a quiet room, bodies rung out of noise, no melody and no interval.
    /// Each camp has a material, so a player can tell who acted without
    /// looking:
    /// <list type="bullet">
    ///   <item>Bouncer, the house: aged brass, hydraulics, weight.</item>
    ///   <item>Syla, a contractor: obsidian and cold air. Glass, darkened so it
    ///   never rings like chimes.</item>
    ///   <item>Kurbyn, the house: the neural rig, electrical.</item>
    /// </list>
    ///
    /// <b>A file wins, key by key.</b> A clip named after the key in
    /// <c>Audio/SFX/Abilities/</c> replaces the stand-in for that moment only.
    /// A moment with neither a file nor a recipe here plays the generic cue.
    ///
    /// <b>Exceptions to the palette, both deliberate:</b> Dargin Pulse's stun
    /// carries a narrow band of noise near 2.8 kHz, the ringing in the ears
    /// after a blast. It is noise through a high-Q filter, not a tone, and it
    /// is darkened well under the music.
    ///
    /// Plain C#, like the other recipes, so every key can be rendered to WAV
    /// outside the editor.
    /// </remarks>
    public static class SignatureRecipes
    {
        private sealed class Recipe
        {
            public Recipe(int variants, Func<SynthRandom, int, float[]> build)
            {
                Variants = variants;
                Build = build;
            }

            public int Variants { get; }
            public Func<SynthRandom, int, float[]> Build { get; }
        }

        private static readonly Dictionary<string, Recipe> Recipes = new Dictionary<string, Recipe>
        {
            // Bouncer — Intimidating Presence is an aura with no event, so it has no sound.
            { "Bouncer_VelvetRope_Tell", new Recipe(1, (r, v) => VelvetRopeTell(r)) },
            { "Bouncer_VelvetRope_Impact", new Recipe(1, (r, v) => VelvetRopeImpact(r)) },
            { "Bouncer_AllInMauling_Impact", new Recipe(3, (r, v) => MaulingImpact(r)) },
            { "Bouncer_AllInMauling_Assist", new Recipe(1, (r, v) => MaulingAssist(r)) },

            // Syla
            { "Syla_FromTheHip_Tell", new Recipe(1, (r, v) => FromTheHipTell(r)) },
            { "Syla_FromTheHip_Impact", new Recipe(2, (r, v) => FromTheHipImpact(r)) },
            { "Syla_AceShards_Tell", new Recipe(1, (r, v) => AceShardsTell(r)) },
            { "Syla_AceShards_Impact", new Recipe(1, (r, v) => AceShardsImpact(r)) },
            { "Syla_TaggedFromAbove_Tell", new Recipe(1, (r, v) => TaggedFromAboveTell(r)) },

            // Kurbyn
            { "Kurbyn_DarginPulse_Tell", new Recipe(1, (r, v) => DarginPulseTell(r)) },
            { "Kurbyn_DarginPulse_Impact", new Recipe(1, (r, v) => DarginPulseImpact(r)) },
            { "Kurbyn_MiraclePull_Impact", new Recipe(1, (r, v) => MiraclePullImpact(r)) },
            { "Kurbyn_EvasiveProtocol_Impact", new Recipe(2, (r, v) => EvasiveProtocolDodge(r)) },
        };

        /// <summary>Every key with a stand-in.</summary>
        public static IEnumerable<string> Keys => Recipes.Keys;

        /// <summary>Whether a key has a stand-in.</summary>
        public static bool Has(string key) => key != null && Recipes.ContainsKey(key);

        /// <summary>How many variants a key gets; 0 for a key without a stand-in.</summary>
        public static int VariantsOf(string key) => Has(key) ? Recipes[key].Variants : 0;

        /// <summary>Builds one variant. Throws for a key without a stand-in.</summary>
        public static float[] Build(string key, int variant)
        {
            if (!Has(key)) throw new ArgumentException($"No signature recipe for '{key}'.", nameof(key));

            var random = new SynthRandom(StableHash(key) + variant * 104729);
            return Recipes[key].Build(random, variant);
        }

        /// <summary>
        /// FNV-1a over the key. <c>string.GetHashCode</c> is randomized per
        /// process on .NET, and a stand-in must sound the same every run.
        /// </summary>
        private static int StableHash(string key)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in key) h = (h ^ c) * 16777619u;
                return (int)(h & 0x7fffffff);
            }
        }

        // ── Bouncer: aged brass, hydraulics, weight ──────────────────────

        /// <summary>A brass plate: a hard knock with a short, dull metallic ring. No clean pitch.</summary>
        private static void Plate(float[] b, float at, float gain, float hz, SynthRandom r)
        {
            SfxRecipes.Clack(b, at, gain, hz, r);
            Synth.Resonate(b, at, 0.09f, gain * 1.6f, 0.0004f, 0.018f, hz * 1.41f, hz * 1.38f, 14f, r);
            SfxRecipes.Thud(b, at, gain * 0.35f, 190f, r);
        }

        private static float[] VelvetRopeTell(SynthRandom r)
        {
            // The arm's plates unlocking down its length, faster as they go,
            // over a hydraulic hiss that swells and bleeds off at the end.
            var b = Synth.Buffer(0.62f);

            float t = 0.02f;
            float gap = 0.075f;
            for (int i = 0; i < 6; i++)
            {
                Plate(b, t, 0.75f + 0.05f * i, r.Range(900f, 1150f), r);
                t += gap;
                gap *= 0.8f;
            }

            Synth.Noise(b, 0f, 0.5f, 0.35f, 0.25f, 0.12f, 6000f, 1800f, r);
            Synth.Noise(b, 0.34f, 0.24f, 0.3f, 0.005f, 0.07f, 5000f, 2400f, r);
            SfxRecipes.Thud(b, t + 0.01f, 0.9f, 120f, r, ring: 1.4f, weight: 0.3f);

            Synth.Darken(b, 5500f);
            Synth.Normalize(b, 0.85f);
            return b;
        }

        private static float[] VelvetRopeImpact(SynthRandom r)
        {
            // The clamp latching, then the target hauled across the felt.
            var b = Synth.Buffer(0.8f);

            Plate(b, 0f, 1.1f, 780f, r);
            SfxRecipes.Clack(b, 0.028f, 0.8f, 1250f, r);
            SfxRecipes.Thud(b, 0f, 1f, 110f, r, ring: 1.6f, weight: 0.4f);

            // The drag: cloth under weight, with a little grit through it.
            SfxRecipes.Swish(b, 0.07f, 0.55f, 0.9f, 420f, 260f, 0.08f, 0.2f, r, q: 1.1f);
            SfxRecipes.Swish(b, 0.07f, 0.5f, 0.35f, 1800f, 1200f, 0.1f, 0.15f, r, q: 0.7f);
            for (int i = 0; i < 7; i++)
                SfxRecipes.Tick(b, 0.1f + i * r.Range(0.05f, 0.075f), r.Range(0.08f, 0.16f), r.Range(700f, 1000f), r);

            Synth.Drive(b, 1.6f);
            Synth.Darken(b, 4500f);
            Synth.Normalize(b, 0.9f);
            return b;
        }

        private static float[] MaulingImpact(SynthRandom r)
        {
            // A bare-knuckle body blow: harder and heavier than the generic
            // hit, with the knuckles in it, and a chest under it.
            var b = Synth.Buffer(0.4f);
            float j = r.Range(0.93f, 1.07f);

            Synth.Noise(b, 0f, 0.02f, 0.9f, 0.0002f, 0.005f, 4500f, 900f, r);
            SfxRecipes.Tick(b, 0f, 0.5f, 1700f * j, r);
            SfxRecipes.Thud(b, 0f, 1.4f, 95f * j, r, ring: 1.8f, weight: 0.35f);
            Synth.Resonate(b, 0.002f, 0.1f, 2.6f, 0.0005f, 0.014f, 240f * j, 200f * j, 2.2f, r);

            Synth.Drive(b, 3f);
            Synth.Darken(b, 4200f);
            Synth.Normalize(b, 0.95f);
            return b;
        }

        private static float[] MaulingAssist(SynthRandom r)
        {
            // On an ally: a heavy open palm on the shoulder, a grip of cloth.
            var b = Synth.Buffer(0.35f);

            Synth.Noise(b, 0f, 0.05f, 1f, 0.0004f, 0.012f, 3200f, 700f, r);
            SfxRecipes.Thud(b, 0f, 0.8f, 150f, r, ring: 1.2f);
            SfxRecipes.Swish(b, 0.04f, 0.22f, 0.35f, 900f, 600f, 0.04f, 0.08f, r, q: 0.8f);

            Synth.Darken(b, 4000f);
            Synth.Normalize(b, 0.8f);
            return b;
        }

        // ── Syla: obsidian and cold air ──────────────────────────────────

        /// <summary>A dark glass shard: a small hard tick, shorter and duller than a chime.</summary>
        private static void Shard(float[] b, float at, float gain, float hz, SynthRandom r)
        {
            Synth.Resonate(b, at, 0.03f, gain * 2f, 0.0002f, 0.006f, hz, hz * 0.98f, 12f, r);
            Synth.Resonate(b, at, 0.02f, gain * 0.9f, 0.0002f, 0.003f, hz * 1.63f, hz * 1.6f, 9f, r);
        }

        private static float[] FromTheHipTell(SynthRandom r)
        {
            // A flicked shard: the snap of the wrist and a thin cut of air.
            var b = Synth.Buffer(0.3f);

            SfxRecipes.Tick(b, 0f, 0.7f, 1900f, r);
            SfxRecipes.Swish(b, 0.01f, 0.2f, 0.9f, 6500f, 2600f, 0.05f, 0.05f, r, q: 1.6f);

            Synth.Darken(b, 7000f);
            Synth.Normalize(b, 0.38f);
            return b;
        }

        private static float[] FromTheHipImpact(SynthRandom r)
        {
            // The shard finding its mark: a glass tick and a small, soft body.
            var b = Synth.Buffer(0.2f);

            Shard(b, 0f, 1f, r.Range(2600f, 3100f), r);
            SfxRecipes.Thud(b, 0.002f, 0.5f, 180f, r);

            Synth.Darken(b, 5500f);
            Synth.Normalize(b, 0.75f);
            return b;
        }

        private static float[] AceShardsTell(SynthRandom r)
        {
            // A fan of shards drawn like cards: quick flicks of air, spreading.
            var b = Synth.Buffer(0.32f);

            for (int i = 0; i < 5; i++)
            {
                float at = i * 0.035f;
                SfxRecipes.Swish(b, at, 0.1f, 0.45f, 4200f - i * 300f, 2600f, 0.02f, 0.03f, r, q: 1.3f);
                SfxRecipes.Tick(b, at, 0.25f, 1600f + i * 90f, r);
            }

            Synth.Darken(b, 6500f);
            Synth.Normalize(b, 0.41f);
            return b;
        }

        private static float[] AceShardsImpact(SynthRandom r)
        {
            // One burst of dark glass spreading outward: dense at the centre,
            // thinning and quieting as the fragments land further off.
            var b = Synth.Buffer(0.45f);

            SfxRecipes.Thud(b, 0f, 0.6f, 160f, r);
            Synth.Noise(b, 0f, 0.1f, 0.3f, 0.001f, 0.03f, 6000f, 1500f, r);

            for (int i = 0; i < 16; i++)
            {
                float u = r.Next();
                float at = u * u * 0.3f;
                Shard(b, at, r.Range(0.4f, 1f) * (1f - 0.7f * u), r.Range(2200f, 4200f), r);
            }

            Synth.Darken(b, 5000f);
            Synth.Normalize(b, 0.8f);
            return b;
        }

        private static float[] TaggedFromAboveTell(SynthRandom r)
        {
            // Two soft mechanical lock clicks, then a held breath: an inhale
            // that stops, and nothing after it.
            var b = Synth.Buffer(0.6f);

            SfxRecipes.Clack(b, 0f, 0.6f, 1500f, r);
            SfxRecipes.Tick(b, 0.004f, 0.4f, 900f, r);
            SfxRecipes.Clack(b, 0.12f, 0.7f, 1350f, r);
            SfxRecipes.Tick(b, 0.124f, 0.45f, 820f, r);

            SfxRecipes.Swish(b, 0.16f, 0.4f, 0.55f, 700f, 1500f, 0.3f, 0.5f, r, q: 0.8f);

            Synth.Darken(b, 5000f);
            Synth.Normalize(b, 0.37f);
            return b;
        }

        // ── Kurbyn: the neural rig ───────────────────────────────────────

        private static float[] DarginPulseTell(SynthRandom r)
        {
            // The rig charging: a low swell and crackle that thickens toward the release.
            var b = Synth.Buffer(0.55f);

            SfxRecipes.Swish(b, 0f, 0.5f, 0.8f, 180f, 420f, 0.35f, 0.1f, r, q: 1.6f);
            Synth.Tone(b, 0f, 0.5f, 48f, 70f, Wave.Sine, 0.25f, 0.4f, 0.08f);
            SfxRecipes.Crackle(b, 0.05f, 0.42f, 0.9f, 40, r, build: true);

            Synth.Darken(b, 6000f);
            Synth.Normalize(b, 0.95f);
            return b;
        }

        private static float[] DarginPulseImpact(SynthRandom r)
        {
            // The discharge: a low thump, crackle radiating out and thinning,
            // and the muffled ringing of the stun underneath.
            var b = Synth.Buffer(0.8f);

            SfxRecipes.Thud(b, 0f, 1.1f, 85f, r, ring: 2f, weight: 0.3f);
            Synth.Tone(b, 0f, 0.4f, 60f, 38f, Wave.Sine, 0.2f, 0.002f, 0.12f);
            Synth.Resonate(b, 0.001f, 0.1f, 2.2f, 0.0005f, 0.014f, 320f, 260f, 2.2f, r);
            Synth.Noise(b, 0f, 0.06f, 0.8f, 0.0003f, 0.015f, 6000f, 400f, r);
            SfxRecipes.Crackle(b, 0.005f, 0.45f, 1.6f, 48, r);

            // The ringing in the ears: narrow noise, not a tone, well under the rest.
            Synth.Resonate(b, 0.02f, 0.7f, 0.5f, 0.04f, 0.3f, 2800f, 2750f, 30f, r);

            Synth.Drive(b, 1.8f);
            Synth.Darken(b, 5500f);
            Synth.Normalize(b, 0.9f);
            return b;
        }

        private static float[] MiraclePullImpact(SynthRandom r)
        {
            // A heavy, dead blow on the target and the splash crackling
            // outward to everyone beside it.
            var b = Synth.Buffer(0.7f);

            Synth.Noise(b, 0f, 0.03f, 0.8f, 0.0003f, 0.008f, 3800f, 500f, r);
            SfxRecipes.Thud(b, 0f, 1.4f, 78f, r, ring: 2.4f, weight: 0.35f);
            Synth.Resonate(b, 0.002f, 0.12f, 2.6f, 0.0005f, 0.02f, 230f, 170f, 2f, r);
            SfxRecipes.Crackle(b, 0.03f, 0.5f, 1.3f, 44, r);
            Synth.Noise(b, 0.03f, 0.35f, 0.2f, 0.01f, 0.1f, 1200f, 90f, r);

            Synth.Drive(b, 2.4f);
            Synth.Darken(b, 4500f);
            Synth.Normalize(b, 0.95f);
            return b;
        }

        private static float[] EvasiveProtocolDodge(SynthRandom r)
        {
            // His own miss: a quick slip of air with a flicker of static in it,
            // so the passive is noticed when it saves him.
            var b = Synth.Buffer(0.3f);

            SfxRecipes.Swish(b, 0f, 0.2f, 1f, r.Range(2200f, 2600f), 650f, 0.04f, 0.05f, r, q: 1.3f);
            SfxRecipes.Crackle(b, 0.02f, 0.14f, 0.45f, 10, r);

            Synth.Darken(b, 6000f);
            Synth.Normalize(b, 0.47f);
            return b;
        }
    }
}
