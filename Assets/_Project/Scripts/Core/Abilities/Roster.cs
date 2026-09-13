// Assets/_Project/Scripts/Core/Abilities/Roster/Roster.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Rng;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Every operator in the game, and the squads drafted from them.
    /// </summary>
    /// <remarks>
    /// <b>It exists for the invariants, not for lookup.</b> Ability ids must be
    /// unique and costs must sit on the 3/6/9 tier (§3.2), and both were once
    /// checked against a single three-operator file — which stopped covering the
    /// roster the moment a fourth operator was written. A duplicate id between
    /// two operator files would compile, run, and silently register one ability
    /// under the other's key in <c>MatchFactory</c>'s ability book.
    ///
    /// Adding an operator means adding one line to <see cref="All"/>. That is
    /// deliberate: the alternative is reflection over the assembly, which would
    /// make the roster implicit and a missing operator invisible.
    /// </remarks>
    public static class Roster
    {
        /// <summary>Operators per seat. Fixed at three (GDD §2.2).</summary>
        public const int SquadSize = 3;

        /// <summary>The pool. One line per operator; nothing else needs editing.</summary>
        public static IReadOnlyList<OperatorDefinition> All { get; } = new[]
        {
            Bouncer.Definition,
            Syla.Definition,
            Kurbyn.Definition,
            Mimi.Definition,
            Javi.Definition
        };

        /// <summary>The alpha three, in order. What every sweep and test measured.</summary>
        public static IReadOnlyList<OperatorDefinition> Alpha { get; } = new[]
        {
            Bouncer.Definition,
            Syla.Definition,
            Kurbyn.Definition
        };

        /// <summary>Every ability across every operator, for the id and cost invariants.</summary>
        public static IReadOnlyList<AbilityDefinition> AllAbilities { get; } = BuildAbilities();

        /// <summary>Finds an operator by name, or throws. For an explicit draft.</summary>
        public static OperatorDefinition ByName(string name)
        {
            foreach (var op in All)
                if (string.Equals(op.Name, name, StringComparison.OrdinalIgnoreCase)) return op;

            throw new ArgumentException($"No operator named '{name}' in the roster.", nameof(name));
        }

        /// <summary>
        /// Draws a squad of <see cref="SquadSize"/> distinct operators from the
        /// pool.
        /// </summary>
        /// <remarks>
        /// <b>Distinct within a seat, duplicated freely across seats.</b> Three
        /// Bouncers on one side is a legitimate configuration to measure but a
        /// poor one to play against while the roster is this small, and with
        /// five operators and four seats no rule could give every seat a unique
        /// squad anyway. The GDD's draft rules are open (§2.2); this is the
        /// simplest thing that produces varied matches.
        ///
        /// <b>It draws from the match's own RNG.</b> That keeps a seed
        /// reproducible end to end — the same seed yields the same squads and
        /// the same dice — which the simulation harness depends on entirely.
        /// It also means adding an operator shifts the dice stream, so figures
        /// measured before a roster change cannot be compared to figures after
        /// one without re-running both.
        ///
        /// <b>A draft is not balanced.</b> Nothing here checks that a squad has
        /// an answer to Evasion, a way to heal, or any reliable damage at all.
        /// With Atomic concentrated in two operators (§2.2), a legal draw can
        /// produce a squad with no way through Kurbyn. That is a real drafting
        /// question and it is open.
        /// </remarks>
        public static IReadOnlyList<OperatorDefinition> DraftRandom(IRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            if (All.Count < SquadSize)
                throw new InvalidOperationException(
                    $"The roster holds {All.Count} operators; a squad needs {SquadSize}.");

            var pool = new List<OperatorDefinition>(All);
            var squad = new List<OperatorDefinition>(SquadSize);

            for (int i = 0; i < SquadSize; i++)
            {
                int index = random.NextInt(0, pool.Count);
                squad.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return squad;
        }

        private static IReadOnlyList<AbilityDefinition> BuildAbilities()
        {
            var abilities = new List<AbilityDefinition>();

            foreach (var op in All) abilities.AddRange(op.Abilities);

            return abilities;
        }
    }
}