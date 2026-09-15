// Scratch: which squads does the serialized MatchBootstrap seed actually draft?
// Compile-run evidence for "Sanity appears 4 games straight for the same color".
using System;
using System.Collections.Generic;
using System.Linq;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

internal static class DraftCheck
{
    private static void Main()
    {
        var seats = new List<PlayerColor>
            { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow };

        // Play mode resets `seed` to the serialized 20260912 every launch; the
        // "New match (reseed)" button increments it. Check both patterns.
        Console.WriteLine("== Same seed every launch (20260912, Play-mode restart) ==");
        for (int launch = 0; launch < 4; launch++)
            PrintSquads(20260912, seats);

        Console.WriteLine();
        Console.WriteLine("== seed++ via the reseed button, four matches ==");
        for (int seed = 20260912; seed < 20260916; seed++)
            PrintSquads(seed, seats);
    }

    private static void PrintSquads(int seed, List<PlayerColor> seats)
    {
        var match = NonaRoyale.Core.MatchFactory.Create(
            seats, seed, squads: null, board: BoardProfile.Standard, openingDeployments: 2);

        var bySeat = match.Operators.GroupBy(o => o.Owner)
            .Select(g => $"{g.Key}: {string.Join(",", g.Select(o => o.Name))}");
        Console.WriteLine($"seed {seed}: {string.Join(" | ", bySeat)}");
    }
}
