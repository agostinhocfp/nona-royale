// Assets/Tests/EditMode/Unity/AbilitySoundsTests.cs
using System.Linq;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;
using NonaRoyale.Unity.Audio;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.Audio
{
    [TestFixture]
    public class AbilitySoundsTests
    {
        [Test]
        public void EveryAbilityInTheRoster_HasASlug()
        {
            // A new ability without a row would silently play only generic cues.
            foreach (var ability in Roster.AllAbilities)
                Assert.That(AbilitySounds.SlugOf(ability.Id), Is.Not.Null, $"{ability.Name} ({ability.Id})");
        }

        [Test]
        public void EverySlug_BelongsToAnAbilityThatExists()
        {
            var ids = Roster.AllAbilities.Select(a => a.Id).ToList();

            foreach (var id in AbilitySounds.All.Keys)
                Assert.That(ids, Does.Contain(id), AbilitySounds.All[id]);
        }

        [Test]
        public void EverySlug_StartsWithItsOperator_AndIsFileSafe()
        {
            foreach (var op in Roster.All)
            foreach (var ability in op.Abilities)
            {
                var slug = AbilitySounds.SlugOf(ability.Id);

                // Revú is Revu: the same ASCII key the voice files use.
                Assert.That(slug, Does.StartWith(VoiceBlips.FileKey(op.Name) + "_"), slug);
                Assert.That(slug.All(c => c < 128 && (char.IsLetterOrDigit(c) || c == '_')), Is.True, slug);
            }
        }

        [Test]
        public void Slugs_AreUnique()
        {
            var all = AbilitySounds.All.Values.Concat(AbilitySounds.AllEvasions.Values).ToList();

            Assert.That(all.Distinct().Count(), Is.EqualTo(all.Count));
        }

        [Test]
        public void AKey_IsTheSlugAndTheMoment()
        {
            Assert.That(AbilitySounds.Key("Bouncer_VelvetRope", SignatureMoment.Tell), Is.EqualTo("Bouncer_VelvetRope_Tell"));
        }

        [Test]
        public void OnlyKurbyn_HasADodgeOfHisOwn()
        {
            Assert.That(AbilitySounds.EvasionOf("Kurbyn"), Is.EqualTo("Kurbyn_EvasiveProtocol"));
            Assert.That(AbilitySounds.EvasionOf("Syla"), Is.Null);
            Assert.That(AbilitySounds.EvasionOf(null), Is.Null);
        }

        [Test]
        public void AnOperatorsSlugs_AreItsAbilitiesAndItsDodge()
        {
            CollectionAssert.AreEquivalent(
                new[] { "Kurbyn_DarginPulse", "Kurbyn_MiraclePull", "Kurbyn_EvasiveProtocol" },
                AbilitySounds.SlugsOf("Kurbyn").ToList());
            CollectionAssert.AreEquivalent(
                new[] { "Revu_LeechRound", "Revu_Sadist" },
                AbilitySounds.SlugsOf("Revú").ToList());
            Assert.That(AbilitySounds.SlugsOf("Nobody"), Is.Empty);
        }

        [Test]
        public void Layers_FollowTheDamageType()
        {
            Assert.That(AbilitySounds.LayerFor(DamageType.Atomic, "ability"), Is.EqualTo(SoundCue.LayerAtomic));
            Assert.That(AbilitySounds.LayerFor(DamageType.Tech, "ability"), Is.EqualTo(SoundCue.LayerTech));
            Assert.That(AbilitySounds.LayerFor(DamageType.Normal, "ability"), Is.Null, "the body blow is the Normal family");
            Assert.That(AbilitySounds.LayerFor(null, "self"), Is.Null);
        }

        [Test]
        public void OverTimeDamage_GetsNoLayer()
        {
            // Bleed is Atomic; a layer on every tick would make upkeep the loudest thing in the game.
            Assert.That(AbilitySounds.LayerFor(DamageType.Atomic, "bleed"), Is.Null);
            Assert.That(AbilitySounds.LayerFor(DamageType.Atomic, "mark"), Is.Null);
            Assert.That(AbilitySounds.LayerFor(DamageType.Tech, "follow-up"), Is.Null);
            Assert.That(AbilitySounds.LayerFor(DamageType.Tech, DeferredOperatorEffects.ChargeCause), Is.Null);
        }
    }
}
