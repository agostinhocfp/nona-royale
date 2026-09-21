// Assets/_Project/Scripts/Unity/Audio/AbilitySounds.cs
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>When in an ability's life its signature sound plays (AUDIO.md AU3).</summary>
    public enum SignatureMoment
    {
        /// <summary>The cast tell on the caster, in place of <see cref="SoundCue.CastTell"/> or <see cref="SoundCue.CastCell"/>.</summary>
        Tell,

        /// <summary>The effect landing on an enemy, in place of <see cref="SoundCue.Hit"/> or <see cref="SoundCue.HitBig"/>.</summary>
        Impact,

        /// <summary>The ability's ally mode landing: a heal or a hand, played over the heal cue.</summary>
        Assist,
    }

    /// <summary>
    /// Which sound files belong to which ability (AUDIO.md AU3, decision 1):
    /// ability ids mapped to readable slugs, so a renamed ability keeps its
    /// files and the file names stay legible.
    /// </summary>
    /// <remarks>
    /// <b>A file per moment.</b> <c>Bouncer_VelvetRope_Tell</c> in
    /// <c>Assets/_Project/Audio/Resources/Audio/SFX/Abilities/</c> (variants
    /// <c>_2</c> … <c>_8</c>) is that ability's tell. The moments are
    /// <see cref="SignatureMoment"/>. Every ability in the roster has a slug, so
    /// a file can be dropped in for any operator today; only the alpha three
    /// have synthesized stand-ins (<see cref="SignatureRecipes"/>). A moment
    /// with neither plays the generic cue, as before AU3.
    ///
    /// <b>The slug is fixed once written.</b> It was derived from the names
    /// when the table was made; renaming an ability does not change it, which
    /// is the point of keying by id. A new ability needs a row here, and
    /// <c>AbilitySoundsTests</c> fails until it has one.
    ///
    /// <b>Passives have no id</b>, so the one passive with a sound of its own,
    /// Kurbyn's Evasive Protocol, is looked up by operator name
    /// (<see cref="EvasionOf"/>).
    ///
    /// Plain C#: it reads the core's roster and nothing from Unity.
    /// </remarks>
    public static class AbilitySounds
    {
        /// <summary>Subfolder of <c>Audio/SFX/</c> the signature files live in.</summary>
        public const string Folder = "Abilities/";

        private static readonly Dictionary<int, string> Slugs = new Dictionary<int, string>
        {
            // House
            { 101, "Bouncer_VelvetRope" },
            { 102, "Bouncer_AllInMauling" },
            { 301, "Kurbyn_DarginPulse" },
            { 302, "Kurbyn_MiraclePull" },

            // Contractors
            { 201, "Syla_FromTheHip" },
            { 202, "Syla_AceShards" },
            { 203, "Syla_TaggedFromAbove" },
            { 401, "Mimi_CryoPulse" },
            { 402, "Mimi_Translocation" },
            { 403, "Mimi_CryoField" },
            { 501, "Javi_NaniteInfusion" },
            { 502, "Javi_TraumaPlate" },
            { 503, "Javi_NeuralPurge" },
            { 601, "Kian_InversionMatrix" },
            { 602, "Kian_SonicDisrupter" },
            { 603, "Kian_DroneStrike" },
            { 701, "Nuetu_BioLinkRage" },
            { 702, "Nuetu_AblativePlating" },
            { 703, "Nuetu_Killzone" },
            { 801, "Sanity_ShortCircuit" },
            { 802, "Sanity_ZeroDay" },
            { 803, "Sanity_Collision" },
            { 901, "Luka_BlindSpot" },
            { 902, "Luka_HermesRing" },
            { 903, "Luka_Vendetta" },

            // Owners
            { 1001, "Lethe_NanoCell" },
            { 1002, "Lethe_ErisExploit" },
            { 1101, "Revu_LeechRound" },
            { 1102, "Revu_Sadist" },
            { 1201, "Fortuna_DealAgain" },
            { 1202, "Fortuna_TheTable" },
            { 1203, "Fortuna_Boxcars" },
        };

        /// <summary>Operators whose passive is evasion, and the slug of their dodge.</summary>
        private static readonly Dictionary<string, string> Evasions = new Dictionary<string, string>
        {
            { "Kurbyn", "Kurbyn_EvasiveProtocol" },
        };

        /// <summary>Every ability id with its slug.</summary>
        public static IReadOnlyDictionary<int, string> All => Slugs;

        /// <summary>Every passive slug, by operator name.</summary>
        public static IReadOnlyDictionary<string, string> AllEvasions => Evasions;

        /// <summary>The ability's slug, or null for an id with no row.</summary>
        public static string SlugOf(int abilityId) => Slugs.TryGetValue(abilityId, out var slug) ? slug : null;

        /// <summary>
        /// The slug of an operator's own dodge, or null if its evasions are
        /// ordinary ones (<see cref="SoundCue.Miss"/>).
        /// </summary>
        public static string EvasionOf(string operatorName) =>
            operatorName != null && Evasions.TryGetValue(operatorName, out var slug) ? slug : null;

        /// <summary>The file and cache name of one moment: <c>Bouncer_VelvetRope_Tell</c>.</summary>
        public static string Key(string slug, SignatureMoment moment) => slug + "_" + moment;

        /// <summary>
        /// Every slug an operator can sound: its abilities and its dodge. For
        /// warming the bank when a match is dealt. Empty for an unknown name.
        /// </summary>
        public static IEnumerable<string> SlugsOf(string operatorName)
        {
            foreach (var op in Roster.All)
            {
                if (op.Name != operatorName) continue;

                foreach (var ability in op.Abilities)
                {
                    var slug = SlugOf(ability.Id);
                    if (slug != null) yield return slug;
                }

                var dodge = EvasionOf(op.Name);
                if (dodge != null) yield return dodge;
            }
        }

        /// <summary>
        /// The damage-type layer under a hit (AUDIO.md AU3, decision 2), or
        /// null for none.
        /// </summary>
        /// <remarks>
        /// <b>Normal has no layer of its own:</b> the body blow the hit already
        /// plays is the Normal family. Tech adds a fizz and Atomic a sub drop and
        /// a pressure crack, so a hit that went straight through a shield sounds
        /// like it.
        ///
        /// <b>Over-time damage gets no layer.</b> A bleed or mark tick is
        /// Atomic, and a layer on every upkeep tick would make the least
        /// important damage the loudest. The causes are the ones
        /// <c>FeedbackLayer</c> labels as over time.
        /// </remarks>
        public static SoundCue? LayerFor(DamageType? type, string cause)
        {
            if (type == null || IsOverTime(cause)) return null;

            switch (type.Value)
            {
                case DamageType.Tech: return SoundCue.LayerTech;
                case DamageType.Atomic: return SoundCue.LayerAtomic;
                default: return null;
            }
        }

        private static bool IsOverTime(string cause) =>
            cause == "bleed" || cause == "mark" || cause == "follow-up" ||
            cause == DeferredOperatorEffects.ChargeCause;
    }
}
