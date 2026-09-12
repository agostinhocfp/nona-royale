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
    /// Wires a match: builds every service, hands each its dependencies, and
    /// returns the one object a caller needs.
    /// </summary>
    /// <remarks>
    /// This is the composition root for the core. There are no singletons and no
    /// <c>.Instance</c> anywhere below it — dependencies are passed in, which is
    /// what makes each service testable in isolation and what the old codebase's
    /// five interlocking singletons could not do (ADR-0004).
    ///
    /// Unity gets its own thin composition root on top of this, which supplies
    /// the seed and the roster and then subscribes the view to events.
    /// </remarks>
    public static class MatchFactory
    {
        /// <summary>
        /// Rebuilds a set of abilities with their reach adjusted. A balance knob
        /// for the simulation harness — both the single-target range and any
        /// area radius move together, since "reach" means both.
        /// </summary>
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

                tuned.Add(new AbilityDefinition(
                    ability.Id, ability.Name, ability.EnergyCost, ability.CooldownTurns,
                    ability.Range + rangeBonus, effects, ability.RequiresTarget));
            }

            return tuned;
        }

        /// <summary>Everything a match is made of, for callers that need to look inside.</summary>
        public sealed class Match
        {
            public Match(
                GameEngine engine,
                IReadOnlyList<PlayerState> players,
                IReadOnlyList<OperatorState> operators,
                PathMap map,
                IReadOnlyDictionary<int, IReadOnlyList<AbilityDefinition>> abilitiesByOperator)
            {
                Engine = engine;
                Players = players;
                Operators = operators;
                Map = map;
                AbilitiesByOperator = abilitiesByOperator;
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
        }

        /// <summary>
        /// Builds a match with the alpha roster: each seat fields a Bouncer, a
        /// Syla and a Kurbyn.
        /// </summary>
        public static Match CreateAlphaMatch(
            IReadOnlyList<PlayerColor> seats,
            int seed,
            BoardProfile board = null,
            GameConfig gameConfig = null,
            CombatConfig combatConfig = null,
            EnergyConfig energyConfig = null,
            RosterSpeeds speeds = null,
            int openingDeployments = 0,
            int abilityRangeBonus = 0)
        {
            if (seats == null) throw new ArgumentNullException(nameof(seats));
            if (seats.Count == 0) throw new ArgumentException("A match needs at least one seat.", nameof(seats));

            board = board ?? BoardProfile.Standard;
            gameConfig = gameConfig ?? GameConfig.Default;
            combatConfig = combatConfig ?? CombatConfig.Default;
            energyConfig = energyConfig ?? EnergyConfig.Default;
            speeds = speeds ?? RosterSpeeds.Default;

            var map = new PathMap(board);
            var operators = new List<OperatorState>();
            var players = new List<PlayerState>();
            var auras = new Dictionary<int, AuraDefinition>();
            var abilitiesByOperator = new Dictionary<int, IReadOnlyList<AbilityDefinition>>();

            int nextId = 1;
            foreach (var seat in seats)
            {
                int bouncerId = nextId++;
                int sylaId = nextId++;
                int kurbynId = nextId++;

                var squad = new[]
                {
                    new OperatorState(bouncerId, "Bouncer", seat, AlphaRoster.BouncerMaxHealth, speeds.Bouncer),
                    new OperatorState(sylaId, "Syla", seat, AlphaRoster.SylaMaxHealth, speeds.Syla),
                    new OperatorState(kurbynId, "Kurbyn", seat, AlphaRoster.KurbynMaxHealth, speeds.KurbynBase)
                };

                auras[bouncerId] = AlphaRoster.IntimidatingPresence;
                abilitiesByOperator[bouncerId] = Retune(AlphaRoster.BouncerAbilities, abilityRangeBonus);
                abilitiesByOperator[sylaId] = Retune(AlphaRoster.SylaAbilities, abilityRangeBonus);
                abilitiesByOperator[kurbynId] = Retune(AlphaRoster.KurbynAbilities, abilityRangeBonus);

                // Operators that start the match already on the board, skipping
                // the deploy roll. Start cells are safe and one per colour, so
                // an opening deployment can never contest anything.
                for (int i = 0; i < openingDeployments && i < squad.Length; i++)
                    squad[i].MoveTo(0);

                operators.AddRange(squad);
                players.Add(new PlayerState(seat, squad));
            }

            var clock = new MatchClock(players);
            var random = new SeededRandom(seed);
            var statuses = new StatusRegistry(clock, combatConfig);
            var energy = new EnergyLedger(energyConfig);
            var damage = new DamagePipeline(statuses, random);
            var targeting = new TargetingRules(map, statuses);
            var movement = new MovementResolver(map, gameConfig);
            var collisions = new CollisionResolver(map, combatConfig, damage, movement);
            var abilities = new AbilityResolver(map, clock, energy, statuses, targeting, damage);
            var auraRules = new AuraRules(targeting, auras);
            var neutralize = new NeutralizeRules(statuses, abilities);
            var win = new WinConditions(map);

            // Kurbyn's Evasive Protocol is permanent and never "used", so it is
            // granted once here rather than resolved as an ability. Its speed
            // bonus rides on the status magnitude — without that it was a
            // declared constant nothing consumed, and Kurbyn moved at his base
            // speed for every match and every simulation run.
            foreach (var op in operators)
                if (op.Name == "Kurbyn")
                    statuses.ApplyPassive(op, StatusKind.Evasion, AlphaRoster.KurbynPassiveSpeedBonus);

            var turns = new TurnStateMachine(
                players, clock, gameConfig, random, energy, statuses, damage, neutralize, win);

            var abilityBook = new Dictionary<int, AbilityDefinition>();
            foreach (var list in abilitiesByOperator.Values)
                foreach (var ability in list) abilityBook[ability.Id] = ability;

            var engine = new GameEngine(
                operators, abilityBook, map, turns, movement, collisions,
                abilities, statuses, auraRules, neutralize, win);

            return new Match(engine, players, operators, map, abilitiesByOperator);
        }
    }
}