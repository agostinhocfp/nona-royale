// Assets/_Project/Scripts/Core/Replay/RecipeCodec.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Replay.Json;

namespace NonaRoyale.Core.Replay
{
    /// <summary>A <see cref="MatchRecipe"/> to and from the <c>recipe</c> object of a replay header.</summary>
    /// <remarks>
    /// <b>The board is written as its geometry</b>, not a preset name: the
    /// view builds a compact board with <c>Cross("Compact", 3, laps: 2)</c>
    /// that no preset holds, and a replay must rebuild whatever was played.
    ///
    /// <b>Sides are written as a team number per seat</b>, Red to Violet, the
    /// way <see cref="TeamMap"/> stores them. The three named maps read back as
    /// themselves; anything else is rebuilt with <see cref="TeamMap.Of"/>.
    /// </remarks>
    public static class RecipeCodec
    {
        private static readonly PlayerColor[] AllSeats =
            { PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Violet };

        public static JsonNode Write(MatchRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));

            var seats = JsonNode.NewArray();
            foreach (var seat in recipe.Seats) seats.Add(JsonNode.String(seat.ToString()));

            var node = JsonNode.NewObject()
                .Set("seed", recipe.Seed)
                .Set("seats", seats)
                .Set("source", recipe.Source.ToString())
                .Set("board", WriteBoard(recipe.Board))
                .Set("opening", recipe.OpeningDeployments)
                .Set("teams", WriteTeams(recipe.Teams));

            if (recipe.Source == SquadSource.Drafted)
            {
                var squads = new Dictionary<PlayerColor, IReadOnlyList<string>>();
                foreach (var seat in recipe.Seats) squads[seat] = NamesOf(recipe.SquadOf(seat));
                node.Set("squads", WriteSquads(recipe.Seats, squads));
            }

            return node;
        }

        public static MatchRecipe Read(JsonNode node)
        {
            node.AsObject("recipe");

            var seats = new List<PlayerColor>();
            foreach (var seat in node.Get("seats").AsArray("seats"))
                seats.Add(EnumText.Parse<PlayerColor>(seat.AsString("seats"), "seats"));

            var source = EnumText.Get<SquadSource>(node, "source");

            Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>> squads = null;
            if (source == SquadSource.Drafted)
            {
                squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>();
                foreach (var pair in ReadSquads(node.Get("squads")))
                {
                    var definitions = new List<OperatorDefinition>(pair.Value.Count);
                    foreach (var name in pair.Value) definitions.Add(OperatorByName(name));
                    squads[pair.Key] = definitions;
                }
            }
            else if (node.Has("squads"))
            {
                throw new JsonFormatException($"a {source} recipe names no squads");
            }

            try
            {
                return new MatchRecipe(
                    seats,
                    node.GetInt("seed"),
                    source,
                    squads,
                    ReadBoard(node.Get("board")),
                    node.GetInt("opening"),
                    ReadTeams(node.Get("teams")));
            }
            catch (ArgumentException e)
            {
                throw new JsonFormatException($"the recipe does not describe a match: {e.Message}");
            }
        }

        // ── Squads ───────────────────────────────────────────────────────

        public static JsonNode WriteSquads(
            IReadOnlyList<PlayerColor> seats, IReadOnlyDictionary<PlayerColor, IReadOnlyList<string>> squads)
        {
            var node = JsonNode.NewObject();

            foreach (var seat in seats)
            {
                var names = JsonNode.NewArray();
                foreach (var name in squads[seat]) names.Add(JsonNode.String(name));
                node.Set(seat.ToString(), names);
            }

            return node;
        }

        public static Dictionary<PlayerColor, IReadOnlyList<string>> ReadSquads(JsonNode node)
        {
            node.AsObject("squads");
            var squads = new Dictionary<PlayerColor, IReadOnlyList<string>>();

            foreach (var member in node.Members)
            {
                var seat = EnumText.Parse<PlayerColor>(member.Key, "squads");
                var names = new List<string>();
                foreach (var name in member.Value.AsArray(member.Key)) names.Add(name.AsString(member.Key));
                squads[seat] = names;
            }

            return squads;
        }

        public static IReadOnlyList<string> NamesOf(IReadOnlyList<OperatorDefinition> squad)
        {
            var names = new List<string>(squad.Count);
            foreach (var op in squad) names.Add(op.Name);
            return names;
        }

        private static OperatorDefinition OperatorByName(string name)
        {
            // Exact match only. Roster.ByName ignores case, which is right for
            // a person typing a name and wrong for a file that wrote it.
            foreach (var op in Roster.All)
                if (string.Equals(op.Name, name, StringComparison.Ordinal)) return op;

            throw new JsonFormatException($"no operator named '{name}' in this build's roster");
        }

        // ── Board ────────────────────────────────────────────────────────

        private static JsonNode WriteBoard(BoardProfile board) =>
            JsonNode.NewObject()
                .Set("name", board.Name)
                .Set("circuit", board.CircuitLength)
                .Set("home", board.HomeColumnLength)
                .Set("laps", board.Laps);

        private static BoardProfile ReadBoard(JsonNode node)
        {
            node.AsObject("board");

            string name = node.GetString("name");
            int circuit = node.GetInt("circuit");
            int home = node.GetInt("home");
            int laps = node.GetInt("laps");

            // The standard board comes back as the shared instance, so a
            // replayed standard match is built from exactly the object the live
            // one was.
            var standard = BoardProfile.Standard;
            if (name == standard.Name && circuit == standard.CircuitLength
                && home == standard.HomeColumnLength && laps == standard.Laps)
            {
                return standard;
            }

            try
            {
                return new BoardProfile(name, circuit, home, laps);
            }
            catch (ArgumentException e)
            {
                throw new JsonFormatException($"'board' is not a legal board: {e.Message}");
            }
        }

        // ── Sides ────────────────────────────────────────────────────────

        private static JsonNode WriteTeams(TeamMap teams)
        {
            var node = JsonNode.NewArray();
            foreach (var seat in AllSeats) node.Add(JsonNode.Number(teams.TeamOf(seat)));
            return node;
        }

        private static TeamMap ReadTeams(JsonNode node)
        {
            var items = node.AsArray("teams");
            if (items.Count != AllSeats.Length)
                throw new JsonFormatException($"'teams' holds {items.Count} entries; there are {AllSeats.Length} seats");

            var ids = new int[AllSeats.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = items[i].AsInt("teams");

            foreach (var named in new[] { TeamMap.FreeForAll, TeamMap.CrossedPairs, TeamMap.AdjacentPairs })
                if (SameSides(named, ids)) return named;

            // Anything else: group the seats by team number, in first-seen order.
            var sides = new List<List<PlayerColor>>();
            var sideIds = new List<int>();

            for (int i = 0; i < ids.Length; i++)
            {
                int index = sideIds.IndexOf(ids[i]);
                if (index < 0)
                {
                    sideIds.Add(ids[i]);
                    sides.Add(new List<PlayerColor>());
                    index = sides.Count - 1;
                }

                sides[index].Add(AllSeats[i]);
            }

            var arrays = new PlayerColor[sides.Count][];
            for (int i = 0; i < sides.Count; i++) arrays[i] = sides[i].ToArray();

            try
            {
                return TeamMap.Of(arrays);
            }
            catch (ArgumentException e)
            {
                throw new JsonFormatException($"'teams' is not a legal side map: {e.Message}");
            }
        }

        /// <summary>Whether <paramref name="map"/> groups the seats exactly as <paramref name="ids"/> does.</summary>
        private static bool SameSides(TeamMap map, int[] ids)
        {
            for (int a = 0; a < AllSeats.Length; a++)
            {
                for (int b = 0; b < AllSeats.Length; b++)
                {
                    bool together = ids[a] == ids[b];
                    bool mapTogether = map.TeamOf(AllSeats[a]) == map.TeamOf(AllSeats[b]);
                    if (together != mapTogether) return false;
                }
            }

            return true;
        }
    }
}