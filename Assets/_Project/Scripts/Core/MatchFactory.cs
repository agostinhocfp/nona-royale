// Assets/_Project/Scripts/Core/MatchFactory.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Rng;
using NonaRoyale.Core.Services;

namespace NonaRoyale.Core
{
    /// <summary>
    /// Wires a match: drafts the squads, builds every service, hands each its
    /// dependencies, and returns the one object a caller needs.
    /// </summary>
    /// <remarks>
    /// This is the composition root for the core. There are no singletons and no
    /// <c>.Instance</c> anywhere below it — dependencies are passed in, which is
    /// what makes each service testable in isolation and what the old codebase's
    /// five interlocking singletons could not do (ADR-0004).
    ///
    /// <b>Squads are drafted, not hardcoded.</b> Until an operator could be
    /// described uniformly (<see cref="OperatorDefinition"/>) this file named
    /// Bouncer, Syla and Kurbyn directly, which meant a fourth operator was
    /// unreachable no matter how completely it was written.
    /// </remarks>
    public static class MatchFactory
    {
        /// <summary>Everything a match is made of, for callers that need to look inside.</summary>
        public sealed class Match
        {
            public Match(
                GameEngine engine,
                IReadOnlyList<PlayerState> players,
                IReadOnlyList<OperatorState> operators,
                PathMap map,
                IReadOnlyDictionary<int, IReadOnlyList<AbilityDefinition>> abilitiesByOperator,
                StatusRegistry statuses,
                EnergyLedger energy,
                TeamMap teams = null)
            {
                Engine = engine;
                Players = players;
                Operators = operators;
                Map = map;
                AbilitiesByOperator = abilitiesByOperator;
                Statuses = statuses;
                Energy = energy;
                Teams = teams ?? TeamMap.FreeForAll;
            }

            public GameEngine Engine { get; }
            public IReadOnlyList<PlayerState> Players { get; }
            public IReadOnlyList<OperatorState> Operators { get; }
            public PathMap Map { get; }

            /// <summary>
            /// What each operator can cast. Needed by any automated player, and
            /// by the view to draw an ability tray.
            /// </summary>
            public IReadOnlyDictionary<int, IReadOnlyList<AbilityDefinition>> AbilitiesByOperator { get; }

            /// <summary>
            /// The match's status registry. For tests and tools that need to
            /// set up a status the dice would take many turns to produce — the
            /// haste cap's tests are the first. The view reads statuses through
            /// the engine and has no reason to touch this.
            /// </summary>
            public StatusRegistry Statuses { get; }

            /// <summary>
            /// The match's ledger, for the same reason <see cref="Statuses"/> is
            /// here: a test or a tool that needs a funded pool without playing
            /// four turns to earn it. The view spends through the engine and has
            /// no reason to touch this.
            /// </summary>
            public EnergyLedger Energy { get; }

            /// <summary>
            /// Who is on whose side (ADR-0012). Carried on the match so
            /// anything already holding one — the bot board, the sim harness,
            /// the view — asks the same map the rules were built with, rather
            /// than being handed a second copy that can drift.
            /// </summary>
            public TeamMap Teams { get; }
        }

        /// <summary>
        /// Builds a match. Each seat fields <see cref="Roster.SquadSize"/>
        /// operators, drafted at random from the roster unless
        /// <paramref name="squads"/> names them.
        /// </summary>
        /// <param name="squads">
        /// Explicit squads per seat, or null to draft at random. A seat absent
        /// from the dictionary is drafted.
        /// </param>
        /// <param name="speedOverrides">
        /// Base speed by operator name, for the simulation harness to sweep
        /// bands without editing the roster. Names not present keep their
        /// declared speed.
        /// </param>
        public static Match Create(
            IReadOnlyList<PlayerColor> seats,
            int seed,
            IReadOnlyDictionary<PlayerColor, IReadOnlyList<OperatorDefinition>> squads = null,
            BoardProfile board = null,
            GameConfig gameConfig = null,
            CombatConfig combatConfig = null,
            EnergyConfig energyConfig = null,
            IReadOnlyDictionary<string, double> speedOverrides = null,
            int openingDeployments = 0,
            int abilityRangeBonus = 0,
            TeamMap teams = null)
        {
            if (seats == null) throw new ArgumentNullException(nameof(seats));
            if (seats.Count == 0) throw new ArgumentException("A match needs at least one seat.", nameof(seats));

            // One map for the whole match, built here and handed to every
            // service that has to answer "friend or foe" (ADR-0012). Null is
            // free-for-all, which is what every caller written before team play
            // means and what keeps the four-way game on exactly the code path
            // it has always been on.
            teams = teams ?? TeamMap.FreeForAll;

            board = board ?? BoardProfile.Standard;
            gameConfig = gameConfig ?? GameConfig.Default;
            combatConfig = combatConfig ?? CombatConfig.Default;
            energyConfig = energyConfig ?? EnergyConfig.Default;

            var map = new PathMap(board);

            // One RNG for the whole match, drafting included, so a seed
            // reproduces the squads and the dice together. It also means adding
            // an operator shifts the dice stream — figures measured before a
            // roster change cannot be compared to figures after one.
            var random = new SeededRandom(seed);

            var operators = new List<OperatorState>();
            var players = new List<PlayerState>();
            var auras = new Dictionary<int, AuraDefinition>();
            var abilitiesByOperator = new Dictionary<int, IReadOnlyList<AbilityDefinition>>();
            var passives = new List<KeyValuePair<OperatorState, OperatorDefinition>>();
            var passives2 = new List<KeyValuePair<OperatorState, OperatorDefinition>>();

            int nextId = 1;

            foreach (var seat in seats)
            {
                var squad = SquadFor(seat, squads, random);
                var states = new List<OperatorState>(squad.Count);

                foreach (var definition in squad)
                {
                    int id = nextId++;

                    var state = new OperatorState(
                        id, definition.Name, seat,
                        definition.MaxHealth,
                        SpeedOf(definition, speedOverrides),
                        definition.HasteCellCap);

                    if (definition.Aura != null) auras[id] = definition.Aura;
                    abilitiesByOperator[id] = Retune(definition.Abilities, abilityRangeBonus);

                    if (definition.Passive != null)
                        passives.Add(new KeyValuePair<OperatorState, OperatorDefinition>(state, definition));
                    if (definition.Passive2 != null)
                        passives2.Add(new KeyValuePair<OperatorState, OperatorDefinition>(state, definition));

                    states.Add(state);
                }

                // Operators that start the match already on the board, skipping
                // the deploy roll. Start cells are safe and one per colour, so
                // an opening deployment can never contest anything.
                for (int i = 0; i < openingDeployments && i < states.Count; i++)
                    states[i].MoveTo(0);

                operators.AddRange(states);
                players.Add(new PlayerState(seat, states));
            }

            var clock = new MatchClock(players);
            var statuses = new StatusRegistry(clock, combatConfig, teams);
            var energy = new EnergyLedger(energyConfig);
            var damage = new DamagePipeline(statuses, random, combatConfig);
            var targeting = new TargetingRules(map, statuses, teams);
            var movement = new MovementResolver(map, gameConfig);
            var collisions = new CollisionResolver(map, combatConfig, damage, movement, teams);
            // Deferred cell effects (ADR-0006). Built before the resolver, which
            // writes beacons into it, and before the turn machine, which fires
            // them at upkeep — one registry, two callers, no second copy.
            var cellEffects = new DeferredCellEffects(clock, targeting, damage, statuses);
            // Operator-anchored charges (§6.4) — the same shape again: the
            // resolver attaches them, the turn machine fires them, and
            // NeutralizeRules reports death cells to them, so it is built
            // before all three.
            var operatorEffects = new DeferredOperatorEffects(clock, targeting, damage, statuses);
            // The match RNG goes in for critical hits (§2.4). Only an effect
            // with a crit chance draws from it, so a match without Luka rolls
            // exactly the dice it rolled before he existed.
            var abilities = new AbilityResolver(
                map, clock, energy, statuses, targeting, damage, cellEffects, operatorEffects, random, players);
            var auraRules = new AuraRules(targeting, auras);


            // NeutralizeRules needs the full roster to pay out Tagged From
            // Above's mark to the marker's squad (§10.2), which is why it is
            // built after the seat loop rather than inside it.
            // The ledger and the player list are here for the kill bounty: it credits
            // the attacker's pool, which is player-level, so neither the victim nor
            // the operator that landed the hit is enough on its own.
            var neutralize = new NeutralizeRules(
                statuses, abilities, energy, operators, players, combatConfig, operatorEffects, teams);

            var win = new WinConditions(map, teams);

            // Permanent passives are granted once here rather than resolved as
            // abilities — they are never "used" (§10.1, §10.3). The magnitude is
            // what StatusRegistry.SpeedModifier sums; since 2026-09-17 nothing
            // on the roster uses that channel for speed, and an operator may
            // carry two passives (Kurbyn: evasion and haste, §10.3).
            foreach (var pair in passives)
                statuses.ApplyPassive(pair.Key, pair.Value.Passive.Value, pair.Value.PassiveMagnitude);
            foreach (var pair in passives2)
                statuses.ApplyPassive(pair.Key, pair.Value.Passive2.Value, pair.Value.Passive2Magnitude);

            var turns = new TurnStateMachine(
     players, clock, gameConfig, random, energy, statuses, damage, neutralize, win, cellEffects, operatorEffects);


            var abilityBook = new Dictionary<int, AbilityDefinition>();
            foreach (var list in abilitiesByOperator.Values)
                foreach (var ability in list) abilityBook[ability.Id] = ability;

            var engine = new GameEngine(
    operators, abilityBook, map, turns, movement, collisions,
    abilities, statuses, auraRules, neutralize, win, combatConfig, cellEffects, random,
    // The move path trips watches on watched movers (§6.7); without the
    // registry the engine could not tell one.
    operatorEffects);


            return new Match(engine, players, operators, map, abilitiesByOperator, statuses, energy, teams);
        }

        /// <summary>
        /// Builds a match in which every seat fields the alpha three — Bouncer,
        /// Syla and Kurbyn, in that order.
        /// </summary>
        /// <remarks>
        /// Kept because every test and every sweep in ADR-0002 measured this
        /// exact squad. A random draft is a different experiment, and mixing the
        /// two silently would make every figure in §12 incomparable to the ones
        /// before it.
        /// </remarks>
        public static Match CreateAlphaMatch(
            IReadOnlyList<PlayerColor> seats,
            int seed,
            BoardProfile board = null,
            GameConfig gameConfig = null,
            CombatConfig combatConfig = null,
            EnergyConfig energyConfig = null,
            RosterSpeeds speeds = null,
            int openingDeployments = 0,
            int abilityRangeBonus = 0,
            TeamMap teams = null)
        {
            if (seats == null) throw new ArgumentNullException(nameof(seats));

            speeds = speeds ?? RosterSpeeds.Default;

            var squads = new Dictionary<PlayerColor, IReadOnlyList<OperatorDefinition>>();
            foreach (var seat in seats) squads[seat] = Roster.Alpha;

            var overrides = new Dictionary<string, double>
            {
                { "Bouncer", speeds.Bouncer },
                { "Syla", speeds.Syla },
                { "Kurbyn", speeds.KurbynBase }
            };

            return Create(
                seats, seed, squads, board, gameConfig, combatConfig, energyConfig,
                overrides, openingDeployments, abilityRangeBonus, teams);
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static IReadOnlyList<OperatorDefinition> SquadFor(
            PlayerColor seat,
            IReadOnlyDictionary<PlayerColor, IReadOnlyList<OperatorDefinition>> squads,
            IRandom random)
        {
            IReadOnlyList<OperatorDefinition> named;

            if (squads != null && squads.TryGetValue(seat, out named) && named != null)
            {
                if (named.Count != Roster.SquadSize)
                    throw new ArgumentException(
                        $"{seat} was given {named.Count} operators; a squad is {Roster.SquadSize}.",
                        nameof(squads));

                return named;
            }

            return Roster.DraftRandom(random);
        }

        private static double SpeedOf(
            OperatorDefinition definition, IReadOnlyDictionary<string, double> overrides)
        {
            double speed;

            return overrides != null && overrides.TryGetValue(definition.Name, out speed)
                ? speed
                : definition.BaseSpeed;
        }

        /// <summary>
        /// Rebuilds a set of abilities with their reach adjusted. A balance knob
        /// for the simulation harness — both the single-target range and any
        /// area radius move together, since "reach" means both.
        /// </summary>
        /// <remarks>
        /// <b>Every field has to be carried across by hand, and nothing warns
        /// you when one is not.</b> This rebuilds an ability from its parts, so a
        /// field added to <see cref="AbilityDefinition"/> and forgotten here is
        /// silently dropped from every swept match. Adding a field to that type
        /// means editing this method — the description was the first to catch it,
        /// and only because the constructor refuses an empty one; the self-target
        /// opt-in (§10, 2026-09-17) is the second.
        /// </remarks>
        private static IReadOnlyList<AbilityDefinition> Retune(
            IReadOnlyList<AbilityDefinition> abilities, int rangeBonus)
        {
            if (rangeBonus == 0) return abilities;

            var tuned = new List<AbilityDefinition>(abilities.Count);

            foreach (var ability in abilities)
            {
                var effects = new List<AbilityEffect>(ability.Effects.Count);

                foreach (var effect in ability.Effects)
                    effects.Add(effect.WithRadius(effect.Radius > 0 ? effect.Radius + rangeBonus : 0));

                // An unlimited range must not take the bonus: int.MaxValue plus
                // anything overflows to a negative, which the constructor rejects
                // — and if it did not, the ability would validate as illegal and
                // be silently uncastable in every swept match.
                int range = ability.Range == AbilityDefinition.UnlimitedRange
                    ? AbilityDefinition.UnlimitedRange
                    : ability.Range + rangeBonus;

                tuned.Add(new AbilityDefinition(
                    ability.Id, ability.Name, ability.Description,
                    ability.EnergyCost, ability.CooldownTurns,
                    range, effects, ability.Targeting, ability.AllowsSelfTarget));

            }

            return tuned;
        }
    }
}