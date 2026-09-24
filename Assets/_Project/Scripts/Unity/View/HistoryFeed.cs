// Assets/_Project/Scripts/Unity/View/HistoryFeed.cs
using System.Collections.Generic;
using System.Text;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>What a history chip stands for.</summary>
    public enum HistoryKind
    {
        Divider,
        Move,
        Hit,
        Deploy,
        Cast,
        Upkeep,
        Home,
        Win,
    }

    /// <summary>
    /// One entry in the history strip: an action and everything it caused.
    /// </summary>
    public sealed class HistoryItem
    {
        public HistoryKind Kind;
        public PlayerColor Seat;
        public OperatorState Actor;
        public int Round;

        /// <summary>A short word over the value, like "CAST" or "UPKEEP".</summary>
        public string Word = "";

        /// <summary>The large mark on the chip, like "-3", "+2" or "7". May be empty.</summary>
        public string Value = "";

        public Color ValueColour = Color.white;
        public bool Knockout;

        /// <summary>The hover card's heading.</summary>
        public string Title = "";

        /// <summary>The hover card's body, one consequence per line.</summary>
        public readonly List<string> Lines = new List<string>();

        /// <summary>Whether this item is loud enough for a toast as well as a chip.</summary>
        public bool Toast;
    }

    /// <summary>What one batch of events adds to the history, the toasts and the turn banner.</summary>
    public sealed class HistoryBatch
    {
        public readonly List<HistoryItem> Items = new List<HistoryItem>();
        public readonly List<string> Rejections = new List<string>();

        /// <summary>The turn this batch began, or null.</summary>
        public TurnBegan TurnBegan;

        /// <summary>What happened at that turn's upkeep, for the banner. Null if nothing did.</summary>
        public HistoryItem Upkeep;
    }

    /// <summary>
    /// Turns a batch of engine events into history items (GUI increment F2).
    /// </summary>
    /// <remarks>
    /// <b>One chip per action, not per event.</b> A cast is an energy spend,
    /// three hits and a status; the player did one thing, so the strip shows
    /// one chip and the hover card lists the rest. A batch is split at turn
    /// boundaries: what comes before <c>TurnEnded</c> belongs to the command,
    /// and what comes after <c>TurnBegan</c> is the next seat's upkeep.
    ///
    /// <b>The cast is named by the view, because it sent it.</b> The engine
    /// reports what a cast did, not which ability it was, and the composition
    /// root knows because it just issued the command. That is bookkeeping about
    /// the view's own action, not a rule.
    ///
    /// <b>Rolls are left out.</b> The dice are on screen in the tray, and a
    /// chip for every roll would bury the plays.
    /// </remarks>
    public static class HistoryFeed
    {
        private static Color DamageColour => UiTheme.Damage;
        private static Color HealColour => UiTheme.Heal;
        private static Color MoveColour => UiTheme.Move;

        public static HistoryBatch Build(
            IReadOnlyList<IGameEvent> events, OperatorState castBy, AbilityDefinition cast, int round)
        {
            var batch = new HistoryBatch();
            var segment = new List<IGameEvent>();
            bool upkeep = false;
            PlayerColor upkeepSeat = default;

            void Flush()
            {
                if (segment.Count > 0)
                {
                    var item = upkeep
                        ? BuildUpkeep(segment, upkeepSeat, round)
                        : BuildAction(segment, castBy, cast, round);

                    if (item != null)
                    {
                        batch.Items.Add(item);
                        if (upkeep) batch.Upkeep = item;
                    }
                }

                segment.Clear();

                // Only the first action segment is the command that was sent.
                castBy = null;
                cast = null;
            }

            foreach (var e in events)
            {
                switch (e)
                {
                    case CommandRejected rejected:
                        batch.Rejections.Add(rejected.Reason);
                        break;

                    case TurnEnded _:
                        Flush();
                        upkeep = false;
                        break;

                    case TurnBegan began:
                        Flush();
                        upkeep = true;
                        upkeepSeat = began.Player;
                        batch.TurnBegan = began;
                        batch.Items.Add(new HistoryItem
                        {
                            Kind = HistoryKind.Divider,
                            Seat = began.Player,
                            Round = round,
                            Title = $"{began.Player} — turn {began.TurnIndex}",
                        });
                        break;

                    case GameWon won:
                        Flush();
                        batch.Items.Add(new HistoryItem
                        {
                            Kind = HistoryKind.Win,
                            Seat = won.Winner,
                            Round = round,
                            Word = "WIN",
                            Value = "",
                            Title = $"{won.Winner} wins the match",
                            Toast = true,
                        });
                        break;

                    default:
                        segment.Add(e);
                        break;
                }
            }

            Flush();
            return batch;
        }

        private static HistoryItem BuildAction(
            List<IGameEvent> segment, OperatorState castBy, AbilityDefinition cast, int round)
        {
            OperatorMoved moved = null;
            OperatorDeployed deployed = null;
            OperatorPityDeployed pity = null;
            OperatorReachedHome home = null;
            DebtAccrued debt = null;
            bool collided = false;

            foreach (var e in segment)
            {
                if (e is OperatorMoved m && moved == null) moved = m;
                else if (e is OperatorDeployed d && deployed == null) deployed = d;
                else if (e is OperatorPityDeployed p && pity == null) pity = p;
                else if (e is OperatorReachedHome h && home == null) home = h;
                else if (e is CollisionResolved) collided = true;
                else if (e is DebtAccrued c && debt == null) debt = c;
            }

            Tally(segment, out int damage, out int heal, out bool knockout);

            HistoryItem item;

            if (cast != null && castBy != null)
            {
                item = New(HistoryKind.Cast, castBy, round, "CAST", $"{castBy.Name} · {cast.Name}");
                item.Value = damage > 0 ? $"-{damage}" : heal > 0 ? $"+{heal}" : Initials(cast.Name);
                item.ValueColour = damage > 0 ? DamageColour : heal > 0 ? HealColour : UiTheme.GoldBright;
                item.Toast = true;
            }
            else if (moved != null && collided)
            {
                item = New(HistoryKind.Hit, moved.Operator, round, "HIT", $"{moved.Operator.Name} collides");
                item.Value = $"-{damage}";
                item.ValueColour = DamageColour;
                item.Toast = true;
            }
            else if (moved != null && home != null)
            {
                item = New(HistoryKind.Home, moved.Operator, round, "HOME", $"{moved.Operator.Name} reaches home");
                item.Value = "";
                item.Toast = true;
            }
            else if (moved != null)
            {
                item = New(HistoryKind.Move, moved.Operator, round, "MOVE", $"{moved.Operator.Name} moves");
                item.Value = (moved.To - moved.From).ToString();
                item.ValueColour = MoveColour;
            }
            else if (deployed != null)
            {
                item = New(HistoryKind.Deploy, deployed.Operator, round, "DEPLOY", $"{deployed.Operator.Name} deploys");
                item.Toast = false;
            }
            else if (pity != null)
            {
                item = New(HistoryKind.Deploy, pity.Operator, round, "DEPLOY", $"{pity.Operator.Name} deploys (no 6 for a while)");
                item.Toast = true;
            }
            else if (damage > 0 || heal > 0 || knockout)
            {
                var subject = FirstSubject(segment);
                item = New(HistoryKind.Upkeep, subject, round, "EFFECT", "Effects");
                item.Value = damage > 0 ? $"-{damage}" : $"+{heal}";
                item.ValueColour = damage > 0 ? DamageColour : HealColour;
                item.Toast = true;
            }
            else if (debt != null)
            {
                // A turn closed by the end-turn command has nothing else in its
                // segment, and the interest would otherwise vanish from the
                // strip (§3.3). Quiet: no toast, the chip and its card only.
                item = New(HistoryKind.Upkeep, null, round, "DEBT", $"{debt.Player}'s debt grows");
                item.Seat = debt.Player;
                item.Value = DebtMark.Roman(debt.Owed);
                item.ValueColour = UiTheme.Debt;
                item.Toast = false;
            }
            else
            {
                return null;
            }

            item.Knockout = knockout;
            Describe(segment, item.Lines);
            return item;
        }

        private static HistoryItem BuildUpkeep(List<IGameEvent> segment, PlayerColor seat, int round)
        {
            bool notable = false;

            foreach (var e in segment)
            {
                if (e is DamageDealt || e is HealApplied || e is OperatorRegenerated ||
                    e is OperatorNeutralized || e is BeaconFired || e is ZoneTicked ||
                    e is ZeroDayDetonated || e is FollowUpResolved || e is OperatorPityDeployed ||
                    e is DamageEvaded || e is DamageAbsorbed || e is DamageSheltered)
                {
                    notable = true;
                    break;
                }
            }

            if (!notable) return null;

            Tally(segment, out int damage, out int heal, out bool knockout);

            var item = New(HistoryKind.Upkeep, FirstSubject(segment), round, "UPKEEP", $"{seat} — start of turn");
            item.Seat = seat;
            item.Value = damage > 0 ? $"-{damage}" : heal > 0 ? $"+{heal}" : "";
            item.ValueColour = damage > 0 ? DamageColour : HealColour;
            item.Knockout = knockout;
            item.Toast = true;

            Describe(segment, item.Lines);
            return item;
        }

        private static HistoryItem New(HistoryKind kind, OperatorState actor, int round, string word, string title) =>
            new HistoryItem
            {
                Kind = kind,
                Actor = actor,
                Seat = actor != null ? actor.Owner : default,
                Round = round,
                Word = word,
                Title = title,
            };

        private static void Tally(List<IGameEvent> segment, out int damage, out int heal, out bool knockout)
        {
            damage = 0;
            heal = 0;
            knockout = false;

            foreach (var e in segment)
            {
                switch (e)
                {
                    case DamageDealt d: damage += d.Amount; break;
                    case HealApplied h: heal += h.Amount; break;
                    case OperatorRegenerated r: heal += r.Amount; break;
                    case OperatorNeutralized _: knockout = true; break;
                }
            }
        }

        private static OperatorState FirstSubject(List<IGameEvent> segment)
        {
            foreach (var e in segment)
            {
                switch (e)
                {
                    case DamageDealt d: return d.Target;
                    case HealApplied h: return h.Target;
                    case OperatorRegenerated r: return r.Target;
                    case OperatorNeutralized n: return n.Operator;
                    case OperatorPityDeployed p: return p.Operator;
                    case DamageEvaded v: return v.Target;
                    case DamageAbsorbed a: return a.Target;
                    case DamageSheltered h: return h.Target;
                }
            }

            return null;
        }

        /// <summary>The hover card's lines: the events a player cares about, in order.</summary>
        private static void Describe(List<IGameEvent> segment, List<string> lines)
        {
            foreach (var e in segment)
            {
                switch (e)
                {
                    case DiceRolled _:
                    case EnergyGranted _:
                    case StatusExpired _:
                        continue;

                    case EnergySpent spent:
                        lines.Add($"spends {spent.Amount} energy ({spent.Remaining} left)");
                        continue;

                    case OperatorMoved moved:
                        int cells = moved.To - moved.From;
                        if (moved.Bounced)
                            lines.Add($"{moved.Operator.Name} is bounced back from {moved.AttemptedTo - moved.From} cells");
                        else if (cells > 0)
                            lines.Add($"{moved.Operator.Name} moves {cells} cells");
                        else
                            lines.Add($"{moved.Operator.Name} is placed on {moved.Cell}");
                        continue;

                    default:
                        lines.Add(e.ToString());
                        continue;
                }
            }
        }

        /// <summary>"Velvet Rope" to "VR", "Zero-Day" to "ZD".</summary>
        public static string Initials(string name)
        {
            var text = new StringBuilder(2);
            bool start = true;

            foreach (char c in name)
            {
                if (c == ' ' || c == '-')
                {
                    start = true;
                    continue;
                }

                if (start && char.IsLetter(c))
                {
                    text.Append(char.ToUpperInvariant(c));
                    if (text.Length == 2) break;
                }

                start = false;
            }

            return text.Length > 0 ? text.ToString() : "?";
        }
    }
}
