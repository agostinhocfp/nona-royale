// Assets/_Project/Scripts/Core/Abilities/Roster/Roster.cs
using System.Collections.Generic;

namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// Every ability in the game, across every operator file.
    /// </summary>
    /// <remarks>
    /// <b>It exists for the invariants, not for lookup.</b> Ability ids must be
    /// unique and costs must sit on the 3/6/9 tier (§3.2), and both were once
    /// checked against a single three-operator file — which stopped covering the
    /// roster the moment a fourth operator was written. A duplicate id between
    /// two operator files would compile, run, and silently register one ability
    /// under the other's key in <c>MatchFactory</c>'s ability book.
    ///
    /// Adding an operator means adding one line here. That is deliberate: the
    /// alternative is reflection over the assembly, which would make the roster
    /// implicit and a missing operator invisible.
    /// </remarks>
    public static class Roster
    {
        public static IReadOnlyList<AbilityDefinition> AllAbilities { get; } = Build();

        private static IReadOnlyList<AbilityDefinition> Build()
        {
            var all = new List<AbilityDefinition>();

            all.AddRange(Bouncer.All);
            all.AddRange(Syla.All);
            all.AddRange(Kurbyn.All);
            all.AddRange(Mimi.All);
            all.AddRange(Javi.All);

            return all;
        }
    }
}