// Assets/_Project/Scripts/Core/Text/KitTrait.cs
using System;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Text
{
    /// <summary>Whether a trait is carried by the operator or projected onto others.</summary>
    public enum TraitKind
    {
        /// <summary>A permanent status on the operator itself.</summary>
        Passive = 0,

        /// <summary>An effect on the pieces standing near it.</summary>
        Aura = 1
    }

    /// <summary>
    /// The part of a kit that is never pressed: a passive or an aura, written
    /// out for a reader (OPERATOR_GUIDE.md §1).
    /// </summary>
    /// <remarks>
    /// <b>Why a list of these exists.</b> Every screen that showed a kit used
    /// to rebuild "the abilities, and then whatever else" on its own: the
    /// dossier listed passives and the aura, the draft card showed one of
    /// them in a spare slot and dropped the rest, and the match showed none.
    /// <see cref="RulesText.Traits"/> is the one answer to "what does this
    /// operator carry that is not a button", so every screen lists the same
    /// things in the same order.
    ///
    /// <b>The words, not the look.</b> The view decides chips, colours and the
    /// "aura · r2" tag; this carries only what it needs to decide them.
    /// </remarks>
    public sealed class KitTrait
    {
        public KitTrait(string name, TraitKind kind, RulesLine line, string description,
            StatusKind? status = null, int radius = 0)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A trait needs a name.", nameof(name));

            Name = name;
            Kind = kind;
            Line = line ?? throw new ArgumentNullException(nameof(line));
            Description = description;
            Status = status;
            Radius = radius;
        }

        /// <summary>The trait's own name, or the status's glossary title for an unnamed passive.</summary>
        public string Name { get; }

        public TraitKind Kind { get; }

        /// <summary>The generated rules line (D2).</summary>
        public RulesLine Line { get; }

        /// <summary>The number-free flavour line, or null where the definition has none.</summary>
        public string Description { get; }

        /// <summary>The status a passive puts on its carrier, for its board tag. The first one for a two-status passive; null for an aura.</summary>
        public StatusKind? Status { get; }

        /// <summary>An aura's reach in track steps. Zero for a passive.</summary>
        public int Radius { get; }

        public override string ToString() => $"{Name} ({Kind})";
    }
}
