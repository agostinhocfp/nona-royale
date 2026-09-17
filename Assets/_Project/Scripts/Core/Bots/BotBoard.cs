// Assets/_Project/Scripts/Core/Bots/BotBoard.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Config;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Bots
{
    /// <summary>
    /// What a bot reads off the table: where operators stand, what they carry,
    /// and roughly how much harm a cell invites next turn.
    /// </summary>
    /// <remarks>
    /// <b>Estimates, never rules.</b> Reach, damage and collision odds here
    /// are rough and only rank options. Whether anything is legal is asked of
    /// the engine (BOTS.md decision 1), so an estimate that is wrong costs a
    /// bot a good move, never a legal one.
    ///
    /// Built fresh for each decision: the table changes after every command,
    /// and caching across commands would be a second copy of state to keep in
    /// step.
    /// </remarks>
    public sealed class BotBoard
    {
        private readonly Dictionary<int, IReadOnlyList<StatusKind>> _statuses =
            new Dictionary<int, IReadOnlyList<StatusKind>>();

        private readonly Dictionary<int, int> _shieldPools = new Dictionary<int, int>();

        public BotBoard(
            MatchFactory.Match match, BotConfig config = null,
            CombatConfig combat = null, GameConfig game = null)
        {
            Match = match ?? throw new ArgumentNullException(nameof(match));
            Config = config ?? BotConfig.Default;
            Combat = combat ?? CombatConfig.Default;
            Game = game ?? GameConfig.Default;
        }

        public MatchFactory.Match Match { get; }
        public BotConfig Config { get; }

        /// <summary>The match's combat numbers. Must match what the match was built with.</summary>
        public CombatConfig Combat { get; }

        /// <summary>The match's game numbers. Must match what the match was built with.</summary>
        public GameConfig Game { get; }

        public GameEngine Engine => Match.Engine;
        public PathMap Map => Match.Map;
        public int Circuit => Map.Profile.CircuitLength;

        // ── Positions ────────────────────────────────────────────────────

        /// <summary>On the shared loop: the only place an operator can hit or be hit.</summary>
        public bool OnLoop(OperatorState op) => Map.IsOnOuterTrack(op.Progress);

        public CellRef CellOf(OperatorState op) => Map.CellAt(op.Owner, op.Progress);

        public CellRef CellAt(PlayerColor seat, int progress) => Map.CellAt(seat, progress);

        /// <summary>Cells from <paramref name="from"/> forward to <paramref name="to"/> in the direction of travel.</summary>
        public int ForwardGap(CellRef from, CellRef to) => (to.Index - from.Index + Circuit) % Circuit;

        public int? Distance(CellRef a, CellRef b) => Map.TrackDistance(a, b);

        public OperatorState ById(int id)
        {
            foreach (var op in Match.Operators)
                if (op.Id == id) return op;
            return null;
        }

        public PlayerState SeatOf(PlayerColor color)
        {
            foreach (var player in Match.Players)
                if (player.Color == color) return player;
            return null;
        }

        public IReadOnlyList<AbilityDefinition> AbilitiesOf(OperatorState op) =>
            Match.AbilitiesByOperator.TryGetValue(op.Id, out var list) ? list : Array.Empty<AbilityDefinition>();

        /// <summary>Enemies of <paramref name="seat"/> standing on the loop within <paramref name="radius"/> of a cell.</summary>
        public List<OperatorState> EnemiesNear(PlayerColor seat, CellRef cell, int radius)
        {
            var found = new List<OperatorState>();
            if (!cell.IsOnTrack) return found;

            foreach (var op in Match.Operators)
            {
                if (op.Owner == seat || !OnLoop(op)) continue;
                var distance = Distance(CellOf(op), cell);
                if (distance.HasValue && distance.Value <= radius) found.Add(op);
            }

            return found;
        }

        /// <summary>Operators of <paramref name="seat"/> on the loop within <paramref name="radius"/> of a cell.</summary>
        public List<OperatorState> AlliesNear(PlayerColor seat, CellRef cell, int radius)
        {
            var found = new List<OperatorState>();
            if (!cell.IsOnTrack) return found;

            foreach (var op in Match.Operators)
            {
                if (op.Owner != seat || !OnLoop(op)) continue;
                var distance = Distance(CellOf(op), cell);
                if (distance.HasValue && distance.Value <= radius) found.Add(op);
            }

            return found;
        }

        /// <summary>Enemies on the next <paramref name="length"/> cells ahead of a cell.</summary>
        public List<OperatorState> EnemiesAhead(PlayerColor seat, CellRef cell, int length)
        {
            var found = new List<OperatorState>();
            if (!cell.IsOnTrack) return found;

            foreach (var op in Match.Operators)
            {
                if (op.Owner == seat || !OnLoop(op)) continue;
                int gap = ForwardGap(cell, CellOf(op));
                if (gap >= 1 && gap <= length) found.Add(op);
            }

            return found;
        }

        /// <summary>Enemies standing exactly on a cell.</summary>
        public List<OperatorState> EnemiesOn(PlayerColor seat, CellRef cell) => EnemiesNear(seat, cell, 0);

        // ── Landings and displacement ────────────────────────────────────

        private HashSet<int> _reachable;

        /// <summary>
        /// Track cells the current seat could land on with the dice in hand,
        /// from <c>PreviewLandings</c>. Cached for the decision.
        /// </summary>
        public HashSet<int> ReachableTrackCells()
        {
            if (_reachable != null) return _reachable;

            _reachable = new HashSet<int>();
            var seat = Engine.CurrentPlayer;
            if (seat == null) return _reachable;

            foreach (var preview in Engine.PreviewLandings())
            {
                if (!Map.IsOnOuterTrack(preview.Progress)) continue;
                _reachable.Add(Map.CellAt(seat.Color, preview.Progress).Index);
            }

            return _reachable;
        }

        /// <summary>
        /// Where a push would leave <paramref name="target"/>: away from the
        /// caster along the shorter arc, backwards when they share a cell,
        /// clamped to the target's own loop. A restatement of
        /// <c>AbilityResolver.PushAwayFromCaster</c> for scoring only: if the
        /// two ever disagree, the bot misjudges a push, and the engine still
        /// resolves it correctly.
        /// </summary>
        public int PredictPush(OperatorState caster, OperatorState target, int distance)
        {
            int casterCell = CellOf(caster).Index;
            int targetCell = CellOf(target).Index;

            int forward = (targetCell - casterCell + Circuit) % Circuit;
            int step;
            if (forward == 0) step = -1;
            else step = forward <= Circuit - forward ? 1 : -1;

            int progress = target.Progress + step * distance;
            if (progress < 0) progress = 0;
            if (progress > Map.Profile.TrackLength - 1) progress = Map.Profile.TrackLength - 1;
            return progress;
        }

        // ── Statuses ─────────────────────────────────────────────────────

        public bool Has(OperatorState op, StatusKind kind)
        {
            if (!_statuses.TryGetValue(op.Id, out var kinds))
            {
                kinds = Engine.ActiveStatusesOn(op);
                _statuses[op.Id] = kinds;
            }

            for (int i = 0; i < kinds.Count; i++)
                if (kinds[i] == kind) return true;

            return false;
        }

        /// <summary>
        /// What is left of an operator's shield pool, as the engine reports it.
        /// </summary>
        /// <remarks>
        /// Read rather than assumed (2026-09-17). Every shield used to be
        /// <c>ShieldPoolDefault</c> deep, so assuming it was harmless; Nano
        /// Cell's pool is 99, and a bot that read it as 2 kept hitting a bubble
        /// nothing could get through.
        /// </remarks>
        public int ShieldPool(OperatorState op)
        {
            if (!_shieldPools.TryGetValue(op.Id, out int pool))
            {
                pool = Engine.ShieldPoolOn(op);
                _shieldPools[op.Id] = pool;
            }

            return pool;
        }

        // ── Damage estimates ─────────────────────────────────────────────

        /// <summary>
        /// Roughly how much of a hit lands, after the target's mitigation
        /// (COMBAT_SYSTEMS §2): Atomic ignores everything, a tech ward stops
        /// Tech, a shield subtracts, evasion may negate Normal.
        /// </summary>
        /// <param name="castCost">
        /// The ability's cost for a hit that lands as it is cast, so
        /// Equilibrium can rescale it (§5.17); null for anything else.
        /// </param>
        public double ExpectedHit(OperatorState target, int amount, DamageType type, int? castCost = null)
        {
            if (castCost.HasValue && Has(target, StatusKind.Equilibrium))
                amount = Combat.EquilibriumScale(castCost.Value, amount);

            if (amount <= 0) return 0.0;
            if (type == DamageType.Atomic) return amount;
            if (type == DamageType.Tech && Has(target, StatusKind.TechWard)) return 0.0;

            double landed = amount;
            if (Has(target, StatusKind.Shield)) landed = Math.Max(0.0, landed - ShieldPool(target));
            if (type == DamageType.Normal && Has(target, StatusKind.Evasion))
                landed *= 1.0 - Combat.EvasionChance;

            return landed;
        }

        /// <summary>
        /// Everything one ability could do to a single operator it reaches:
        /// direct hits, splashes, beams, zones, charges and follow-ups. A rough
        /// upper figure, for judging danger.
        /// </summary>
        public static int DamageAgainstOne(AbilityDefinition ability)
        {
            int total = 0;

            foreach (var effect in ability.Effects)
            {
                switch (effect.Kind)
                {
                    case EffectKind.Damage:
                        if (effect.Scope != EffectScope.Caster) total += effect.Amount + effect.BonusIfBleeding;
                        break;
                    case EffectKind.Execute:
                    case EffectKind.PaintCell:
                    case EffectKind.FollowUp:
                        total += effect.Amount;
                        break;
                    case EffectKind.DeployZone:
                        // A crowd zone against one operator depends on the
                        // crowd; this is its figure with one neighbour.
                        total += effect.Amount + (int)Math.Round(effect.Magnitude * effect.Stacks);
                        break;
                    case EffectKind.AttachCharge:
                        total += effect.Amount + effect.Stacks;
                        break;
                    case EffectKind.ProjectField:
                        total += effect.Amount * Math.Max(0, effect.Duration - 1);
                        break;
                    case EffectKind.MissingEnergyDamage:
                        // Its most, against an empty pool.
                        total += EnergyConfig.Default.EnergyCap / effect.Amount;
                        break;
                    case EffectKind.DashToTarget:
                        total += effect.Amount;
                        break;
                }
            }

            return total;
        }

        /// <summary>How far from its caster an ability can reach an operator, counting area radii.</summary>
        public static int ReachOf(AbilityDefinition ability)
        {
            if (ability.HasUnlimitedRange) return int.MaxValue;

            int radius = 0;
            foreach (var effect in ability.Effects)
                if (effect.Scope != EffectScope.Caster && effect.Radius > radius) radius = effect.Radius;

            return ability.Targeting == AbilityTargeting.None
                ? Math.Max(ability.Range, radius)
                : ability.Range + radius;
        }

        /// <summary>
        /// Rough damage an operator of <paramref name="seat"/> standing on
        /// <paramref name="cell"/> could take before its next turn: each
        /// enemy's strongest affordable, nearly ready ability that reaches the
        /// cell, plus a chance of being landed on.
        /// </summary>
        public double Threat(PlayerColor seat, CellRef cell)
        {
            if (!cell.IsOnTrack) return 0.0;

            bool safe = Map.IsSafe(cell);
            double total = 0.0;

            foreach (var enemy in Match.Operators)
            {
                if (enemy.Owner == seat || !OnLoop(enemy)) continue;
                if (Has(enemy, StatusKind.Stun)) continue;

                var enemyCell = CellOf(enemy);
                int? distance = Distance(enemyCell, cell);
                var owner = SeatOf(enemy.Owner);
                double energy = (owner?.Energy ?? 0) + Config.EnergyHorizon;

                double worst = 0.0;

                foreach (var ability in AbilitiesOf(enemy))
                {
                    if (ability.EnergyCost > energy) continue;
                    if (Engine.TurnsUntilReady(enemy, ability) > 1) continue;
                    if (!distance.HasValue || distance.Value > ReachOf(ability)) continue;

                    worst = Math.Max(worst, DamageAgainstOne(ability));
                }

                if (!safe)
                {
                    int gap = ForwardGap(enemyCell, cell);
                    bool stillOnLoop = enemy.Progress + gap < Map.Profile.TrackLength;

                    if (gap >= 1 && stillOnLoop)
                    {
                        if (gap <= Config.NearCollisionReach)
                            worst += Combat.CollisionDamage * Config.NearCollisionOdds;
                        else if (gap <= Config.FarCollisionReach)
                            worst += Combat.CollisionDamage * Config.FarCollisionOdds;
                    }
                }

                total += worst;
            }

            return total;
        }

        /// <summary>
        /// How much losing this operator would hurt, as a multiplier: more for
        /// one far along its journey, and more for one already wounded.
        /// </summary>
        public double Fragility(OperatorState op)
        {
            double advanced = Math.Max(0, op.Progress) / (double)Map.Profile.Journey;
            double wounded = op.MaxHealth > 0 ? (op.MaxHealth - op.Health) / (double)op.MaxHealth : 0.0;
            return 1.0 + advanced + wounded;
        }
    }
}