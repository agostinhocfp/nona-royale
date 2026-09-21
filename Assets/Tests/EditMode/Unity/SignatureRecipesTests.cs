// Assets/Tests/EditMode/Unity/SignatureRecipesTests.cs
using System;
using System.Linq;
using NonaRoyale.Unity.Audio;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.Audio
{
    [TestFixture]
    public class SignatureRecipesTests
    {
        [Test]
        public void EveryKeyAndVariant_BuildsAShortCleanSound()
        {
            foreach (var key in SignatureRecipes.Keys)
            for (int v = 0; v < SignatureRecipes.VariantsOf(key); v++)
            {
                var b = SignatureRecipes.Build(key, v);
                float seconds = b.Length / (float)Synth.SampleRate;

                Assert.That(seconds, Is.InRange(0.1f, 1.2f), $"{key} {v}");
                // Wider than the cues' floor: bright signatures are level-matched
                // (A-weighted) to the generic cue they replace, so some peak low.
                Assert.That(Synth.Peak(b), Is.InRange(0.3f, 0.96f), $"{key} {v}");
                foreach (float s in b) Assert.IsFalse(float.IsNaN(s) || float.IsInfinity(s), $"{key} {v}");

                // Ends in silence: nothing is cut off mid-ring, so no click at the end.
                Assert.That(MathF.Abs(b[b.Length - 1]), Is.LessThan(0.002f), $"{key} {v}");
            }
        }

        [Test]
        public void EveryKey_BelongsToAnAbilityOrADodge_AndNamesAMoment()
        {
            // A typo in a key would build a sound nothing ever asks for.
            var known = AbilitySounds.All.Values.Concat(AbilitySounds.AllEvasions.Values).ToList();
            var moments = Enum.GetValues(typeof(SignatureMoment)).Cast<SignatureMoment>().ToList();

            foreach (var key in SignatureRecipes.Keys)
                Assert.That(known.Any(slug => moments.Any(m => AbilitySounds.Key(slug, m) == key)), Is.True, key);
        }

        [Test]
        public void TheAlphaThree_HaveATellOrAnImpactForEveryAbility()
        {
            foreach (int id in new[] { 101, 102, 201, 202, 203, 301, 302 })
            {
                var slug = AbilitySounds.SlugOf(id);
                bool any = SignatureRecipes.Has(AbilitySounds.Key(slug, SignatureMoment.Tell)) ||
                           SignatureRecipes.Has(AbilitySounds.Key(slug, SignatureMoment.Impact));

                Assert.That(any, Is.True, slug);
            }
        }

        [Test]
        public void AKey_IsTheSameEveryTime()
        {
            // Seeded from a stable hash, not string.GetHashCode, which .NET randomizes per process.
            CollectionAssert.AreEqual(
                SignatureRecipes.Build("Bouncer_VelvetRope_Tell", 0),
                SignatureRecipes.Build("Bouncer_VelvetRope_Tell", 0));
        }

        [Test]
        public void Variants_Differ()
        {
            CollectionAssert.AreNotEqual(
                SignatureRecipes.Build("Bouncer_AllInMauling_Impact", 0),
                SignatureRecipes.Build("Bouncer_AllInMauling_Impact", 1));
        }

        [Test]
        public void AKeyWithoutARecipe_HasNone()
        {
            Assert.That(SignatureRecipes.Has("Luka_Vendetta_Tell"), Is.False);
            Assert.That(SignatureRecipes.VariantsOf("Luka_Vendetta_Tell"), Is.EqualTo(0));
            Assert.That(SignatureRecipes.Has(null), Is.False);
            Assert.Throws<ArgumentException>(() => SignatureRecipes.Build("Luka_Vendetta_Tell", 0));
        }
    }
}
