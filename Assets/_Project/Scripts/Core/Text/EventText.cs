// Assets/_Project/Scripts/Core/Text/EventText.cs
using System;
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Events;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;

namespace NonaRoyale.Core.Text
{
    /// <summary>
    /// What happened, in the player's words (LAUNCH_UI_PASS.md, G7b): one line
    /// per engine event, for the event log, the history cards and the toasts.
    /// </summary>
    /// <remarks>
    /// <b>Why not the events' own <c>ToString</c>.</b> Those are for
    /// developers: "Kian 8 -> 9", "Red +1 energy (burned 0)", "Kurbyn deploys
    /// to Track[39]". They stay as they are, for the dev panel and for tests
    /// that read them, and this writes what a player reads.
    ///
    /// <b>Runs, not strings</b>, like <see cref="RulesText"/>: a name is a
    /// <see cref="RunKind.Named"/> run carrying its seat, so the view can draw
    /// it in that seat's colour, and statuses and damage types are keywords, so
    /// they look the way they do in the guide.
    ///
    /// <b>It describes, it never decides</b> (PRESENTATION §1): every word comes
    /// from a field the event already carries. There are no board coordinates.
    /// A cell index means nothing to a player, and the board shows where
    /// things happened.
    ///
    /// <b>It refuses to guess.</b> An event it has no words for is written as
    /// <see cref="RulesText.UnwrittenPrefix"/> plus its type, and a test fails
    /// on any event type it does not cover.
    /// </remarks>
    public static class EventText
    {
        /// <summary>Every event type this writes. A test checks that it is every event type there is.</summary>
        private static readonly HashSet<Type> Written = new HashSet<Type>
        {
            typeof(CommandRejected), typeof(TurnBegan), typeof(TurnEnded), typeof(DiceRolled),
            typeof(EnergyGranted), typeof(EnergySpent), typeof(DieCashed), typeof(DiceDealt),
            typeof(TableDealt), typeof(MoveIntercepted), typeof(DebtIncurred), typeof(DebtAccrued),
            typeof(DebtCalled), typeof(DebtBurned), typeof(OperatorDeployed), typeof(OperatorPityDeployed),
            typeof(OperatorMoved), typeof(CollisionResolved), typeof(DamageDealt), typeof(DamageEvaded),
            typeof(DamageAbsorbed), typeof(DamageSheltered), typeof(HealApplied), typeof(OperatorRegenerated),
            typeof(StatusApplied), typeof(StatusExpired), typeof(OperatorNeutralized), typeof(OperatorReachedHome),
            typeof(GameWon), typeof(BeaconPlaced), typeof(BeaconFired), typeof(ZoneDeployed), typeof(ZoneTicked),
            typeof(FieldProjected), typeof(FieldTicked), typeof(FollowUpMarked), typeof(FollowUpResolved),
            typeof(WatchMarked), typeof(WatchTripped), typeof(ZeroDayAttached), typeof(ZeroDayDetonated),
        };

        /// <summary>Whether <paramref name="eventType"/> has words here.</summary>
        public static bool Writes(Type eventType) => eventType != null && Written.Contains(eventType);

        /// <summary>A seat's name as the screen writes it: "RED".</summary>
        public static string SeatName(PlayerColor seat) => seat.ToString().ToUpperInvariant();

        /// <summary>The heading over a turn's lines: "RED's turn".</summary>
        public static RulesLine TurnHeading(PlayerColor seat) =>
            new RulesLine().Named(SeatName(seat), seat).Text("'s turn");

        /// <summary>
        /// A refused command, as the log and the toast show it. The engine's
        /// reasons are whole sentences in the player's words (G7b-2), so this
        /// adds nothing; the refusal colour says the rest.
        /// </summary>
        public static RulesLine Refusal(string reason) =>
            new RulesLine().Text(string.IsNullOrWhiteSpace(reason) ? "Not now" : reason);

        /// <summary>
        /// The player's line for an event, or null for the ones that are
        /// structure rather than news: a turn's start and end, which the log
        /// shows as headings.
        /// </summary>
        public static RulesLine For(IGameEvent e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            var line = new RulesLine();

            switch (e)
            {
                case TurnBegan _:
                case TurnEnded _:
                    return null;

                case CommandRejected rejected:
                    return Refusal(rejected.Reason);

                case DiceRolled rolled:
                    line.Text("rolled ");
                    Faces(line, rolled.Roll.First, rolled.Roll.Second);
                    if (rolled.GrantsAnotherRoll) line.Text(": doubles, roll again");
                    return line;

                case EnergyGranted granted:
                    Seat(line, granted.Player).Text(" gains ").Number(granted.Stored).Text(" ").Keyword("energy", Keywords.Energy);
                    if (granted.Burned > 0) line.Text(" (").Number(granted.Burned).Text(" lost at the cap)");
                    return line;

                case EnergySpent spent:
                    Seat(line, spent.Player).Text(" spends ").Number(spent.Amount).Text(" ").Keyword("energy", Keywords.Energy)
                        .Text(" (").Number(spent.Remaining).Text(" left)");
                    return line;

                case DieCashed cashed:
                    Name(line, cashed.Operator).Text(" cashes a ").Number(cashed.DieFace).Text(" for ")
                        .Number(cashed.Stored).Text(" ").Keyword("energy", Keywords.Energy);
                    return line;

                case DiceDealt dealt:
                    Name(line, dealt.Caster).Text(" deals ");
                    if (dealt.Faces != null && dealt.Faces.Count == 2) Faces(line, dealt.Faces[0], dealt.Faces[1]);
                    else List(line, dealt.Faces);
                    if (dealt.GrantsAnotherRoll) line.Text(": doubles, roll again");
                    return line;

                case TableDealt table:
                    Name(line, table.Caster).Text(" deals a table");
                    if (table.StopDamage > 0)
                        line.Text(": the first enemy to cross it stops there and takes ").Number(table.StopDamage);
                    return line;

                case MoveIntercepted stopped:
                    Name(line, stopped.Mover).Text(" is stopped by ");
                    Seat(line, stopped.Owner).Text("'s table after ");
                    return Cells(line, stopped.Cells);

                case DebtIncurred debt:
                    if (debt.Amount > 0)
                    {
                        Name(line, debt.Source).Text(" puts ");
                        Seat(line, debt.Player).Text(" ").Number(debt.Amount).Text(" in ").Keyword("debt", Keywords.Debt)
                            .Text(" (").Number(debt.Owed).Text(" owed)");
                    }
                    else
                    {
                        Seat(line, debt.Player).Text(" already owes the most it can (").Number(debt.Owed).Text(")");
                    }
                    return line;

                case DebtAccrued accrued:
                    Seat(line, accrued.Player).Text("'s ").Keyword("debt", Keywords.Debt).Text(" grows by ")
                        .Number(accrued.Interest).Text(" (").Number(accrued.Owed).Text(" owed)");
                    return line;

                case DebtCalled called:
                    Name(line, called.Source).Text(" calls in ");
                    Seat(line, called.Player).Text("'s ").Keyword("debt", Keywords.Debt);
                    if (called.Amount > 0) line.Text(": ").Number(called.Amount);
                    else line.Text(", but nothing is owed");
                    return line;

                case DebtBurned burned:
                    Name(line, burned.Debtor).Text(" lands on ");
                    Name(line, burned.Creditor).Text(" and burns ");
                    Seat(line, burned.Player).Text("'s ").Keyword("debt", Keywords.Debt).Text(" of ").Number(burned.Amount);
                    return line;

                case OperatorDeployed deployed:
                    return Name(line, deployed.Operator).Text(" deploys");

                case OperatorPityDeployed pity:
                    Name(line, pity.Operator).Text(" deploys free after ").Number(pity.DroughtTurns)
                        .Text(pity.DroughtTurns == 1 ? " turn" : " turns").Text(" without a deploy roll");
                    return line;

                case OperatorMoved moved:
                    return Moved(line, moved);

                case CollisionResolved collision:
                    Name(line, collision.Mover).Text(" hits ");
                    Name(line, collision.Occupant).Text(collision.MoverBouncedBack ? " and bounces off" : " and takes the cell");
                    return line;

                case DamageDealt damage:
                    Name(line, damage.Target).Text(" takes ");
                    Hit(line, damage.Amount, damage.Type);
                    Source(line, damage.Cause, " from ");
                    line.Text(" (").Number(damage.RemainingHealth).Text(" left)");
                    return line;

                case DamageEvaded evaded:
                    return Name(line, evaded.Target).Text(" ").Keyword("evades", Keywords.Status(StatusKind.Evasion));

                case DamageAbsorbed absorbed:
                    return Name(line, absorbed.Target).Text("'s ").Keyword("shield", Keywords.Status(StatusKind.Shield))
                        .Text(" absorbs the hit");

                case DamageSheltered sheltered:
                    return Name(line, sheltered.Target).Text(" is on a ").Keyword("safe cell", Keywords.SafeCell)
                        .Text(": no damage");

                case HealApplied heal:
                    return Name(line, heal.Target).Text(" heals ").Number(heal.Amount);

                case OperatorRegenerated regen:
                    Name(line, regen.Target).Text(" regenerates ").Number(regen.Amount).Text(" after ")
                        .Number(regen.WoundedTurns).Text(regen.WoundedTurns == 1 ? " wounded turn" : " wounded turns");
                    return line;

                case StatusApplied applied:
                    Name(line, applied.Target).Text(" gains ");
                    Status(line, applied.Status);
                    if (applied.Status != StatusKind.Bleed && applied.Duration > 0)
                        line.Text(" for ").Number(applied.Duration).Text(applied.Duration == 1 ? " turn" : " turns");
                    return line;

                case StatusExpired expired:
                    Status(line, expired.Status);
                    line.Text(" wears off ");
                    return Name(line, expired.Target);

                case OperatorNeutralized down:
                    Name(line, down.Operator).Text(" is neutralized");
                    if (down.Cause == GameEngine.ExecuteCause) line.Text(" ").Keyword("outright", Keywords.Execute);
                    else Source(line, down.Cause, " by ");
                    return line;

                case OperatorReachedHome home:
                    return Name(line, home.Operator).Text(" reaches home");

                case GameWon won:
                    for (int i = 0; i < won.Seats.Count; i++)
                    {
                        if (i > 0) line.Text(i == won.Seats.Count - 1 ? " and " : ", ");
                        Seat(line, won.Seats[i]);
                    }
                    return line.Text(won.Seats.Count > 1 ? " win the match" : " wins the match");

                case BeaconPlaced beacon:
                    return Name(line, beacon.Caster).Text(" places a beacon");

                case BeaconFired fired:
                    Seat(line, fired.Owner).Text("'s beacon fires");
                    return Caught(line, fired.Caught, fired.DamagePerTarget);

                case ZoneDeployed zone:
                    return Name(line, zone.Caster).Text(" lays a zone");

                case ZoneTicked tick:
                    Seat(line, tick.Owner).Text(tick.IsDetonation ? "'s zone detonates" : "'s zone lingers");
                    return Caught(line, tick.Caught, tick.DamagePerTarget);

                case FieldProjected field:
                    return Name(line, field.Holder).Text(" raises a ")
                        .Keyword("Cryo Field", Keywords.Status(StatusKind.CryoField));

                case FieldTicked fieldTick:
                    Seat(line, fieldTick.Owner).Text("'s ").Keyword("Cryo Field", Keywords.Status(StatusKind.CryoField))
                        .Text(" bites");
                    return Caught(line, fieldTick.Caught, fieldTick.DamagePerTarget);

                case FollowUpMarked followUp:
                    Name(line, followUp.Caster).Text(" marks ");
                    return Name(line, followUp.Target).Text(" for a follow-up");

                case FollowUpResolved resolved:
                    if (resolved.Landed)
                    {
                        Seat(line, resolved.Owner).Text("'s follow-up lands on ");
                        Name(line, resolved.Target).Text(" for ").Number(resolved.Damage + resolved.HeavyBonus);
                    }
                    else
                    {
                        Name(line, resolved.Target).Text(" slips ");
                        Seat(line, resolved.Owner).Text("'s follow-up");
                    }
                    return line;

                case WatchMarked watch:
                    Name(line, watch.Caster).Text(" watches ");
                    return Name(line, watch.Target).Text("'s next move");

                case WatchTripped tripped:
                    Seat(line, tripped.Owner).Text("'s watch trips on ");
                    return Name(line, tripped.Target).Text(" for ").Number(tripped.Damage);

                case ZeroDayAttached attached:
                    Name(line, attached.Caster).Text("'s ")
                        .Keyword("Zero-Day charge", Keywords.Status(StatusKind.ZeroDayCharge)).Text(" snaps onto ");
                    return Name(line, attached.Target);

                case ZeroDayDetonated detonated:
                    Seat(line, detonated.Owner).Text("'s ")
                        .Keyword("Zero-Day charge", Keywords.Status(StatusKind.ZeroDayCharge)).Text(" detonates");
                    return Caught(line, detonated.Caught, detonated.DamagePerTarget);

                default:
                    return line.Text(RulesText.UnwrittenPrefix + e.GetType().Name + "]");
            }
        }

        // ── Pieces ───────────────────────────────────────────────────────

        private static RulesLine Name(RulesLine line, OperatorState op) =>
            op == null ? line.Text("someone") : line.Named(op.Name, op.Owner);

        private static RulesLine Seat(RulesLine line, PlayerColor seat) =>
            seat == PlayerColor.None ? line.Text("nobody") : line.Named(SeatName(seat), seat);

        private static RulesLine Faces(RulesLine line, int first, int second)
        {
            if (first == second) return line.Text("double ").Number(first).Text("s");
            return line.Number(first).Text(" and ").Number(second);
        }

        private static void List(RulesLine line, IReadOnlyList<int> faces)
        {
            if (faces == null || faces.Count == 0) { line.Text("nothing"); return; }

            for (int i = 0; i < faces.Count; i++)
            {
                if (i > 0) line.Text(i == faces.Count - 1 ? " and " : ", ");
                line.Number(faces[i]);
            }
        }

        private static RulesLine Cells(RulesLine line, int cells) =>
            line.Number(cells).Text(cells == 1 ? " cell" : " cells");

        private static void Hit(RulesLine line, int amount, DamageType? type)
        {
            line.Number(amount);
            if (type.HasValue && type.Value != DamageType.Normal)
                line.Text(" ").Keyword(type.Value.ToString(), Keywords.Damage(type.Value));
        }

        private static void Status(RulesLine line, StatusKind kind) =>
            line.Keyword(Glossary.TitleOf(kind), Keywords.Status(kind));

        private static RulesLine Caught(RulesLine line, int caught, int each)
        {
            if (caught == 0) return line.Text(" and catches nobody");
            return line.Text(": ").Number(caught).Text(" caught, ").Number(each).Text(" each");
        }

        private static RulesLine Moved(RulesLine line, OperatorMoved moved)
        {
            int cells = moved.To - moved.From;

            Name(line, moved.Operator);

            // Bounced covers both a collision that throws the mover back and an
            // overshoot of home: the event says how far, not why, and the
            // collision line that follows says the rest.
            if (moved.Bounced)
            {
                int back = moved.AttemptedTo - moved.To;
                if (cells > 0) return Cells(line.Text(" moves "), cells).Text(", bounced back ").Number(back);
                return Cells(line.Text(" is bounced back "), back);
            }

            if (cells > 0) return Cells(line.Text(" moves "), cells);
            if (cells < 0) return Cells(line.Text(" is pushed back "), -cells);

            // Placement (a swap, a table) is reported as a move to the same
            // progress (§7.4): the piece changed cell without travelling.
            return line.Text(" is repositioned");
        }

        /// <summary>
        /// Where damage or a knockout came from, after <paramref name="lead"/>
        /// (" from " or " by "). Nothing for a plain cast: the history card
        /// already names the cast.
        /// </summary>
        private static void Source(RulesLine line, string cause, string lead)
        {
            switch (cause)
            {
                case null:
                case AbilityResolver.AbilityCause:
                case GameEngine.ExecuteCause:
                    return;

                case AbilityResolver.CriticalCause:
                    line.Text(", a ").Keyword("critical", Keywords.Critical).Text(" hit");
                    return;

                case "self":
                    line.Text(lead).Text("its own cast");
                    return;

                case "collision":
                    line.Text(" in a collision");
                    return;

                case "bleed":
                    line.Text(lead).Keyword("Bleed", Keywords.Status(StatusKind.Bleed));
                    return;

                case "mark":
                    line.Text(lead).Keyword("Mark", Keywords.Status(StatusKind.Mark));
                    return;

                case "beacon":
                    line.Text(lead).Text("a beacon");
                    return;

                case "killzone":
                    line.Text(lead).Text("a zone");
                    return;

                case "zero-day":
                    line.Text(lead).Text("a ").Keyword("Zero-Day charge", Keywords.Status(StatusKind.ZeroDayCharge));
                    return;

                case "follow-up":
                    line.Text(lead).Text("a follow-up");
                    return;

                case "table":
                    line.Text(lead).Text("a table");
                    return;

                case "cryo-field":
                    line.Text(lead).Text("a ").Keyword("Cryo Field", Keywords.Status(StatusKind.CryoField));
                    return;

                case "watch":
                    line.Text(lead).Text("a watch");
                    return;

                case "upkeep":
                    line.Text(" at ").Keyword("upkeep", Keywords.Upkeep);
                    return;

                case GameEngine.DevCause:
                    line.Text(" (dev)");
                    return;

                default:
                    line.Text(lead).Text(cause);
                    return;
            }
        }
    }
}
