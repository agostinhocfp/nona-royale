// Assets/_Project/Scripts/Core/Events/GameEvents.cs
using System.Collections.Generic;
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Events
{
    /// <summary>
    /// Something that happened. The core returns these; the view renders them
    /// and never reads core internals (ADR-0004).
    /// </summary>
    public interface IGameEvent
    {
    }

    /// <summary>A command was refused. Carries why, so the view can say so.</summary>
    public sealed class CommandRejected : IGameEvent
    {
        public CommandRejected(string reason) { Reason = reason; }
        public string Reason { get; }
        public override string ToString() => $"rejected: {Reason}";
    }

    public sealed class TurnBegan : IGameEvent
    {
        public TurnBegan(PlayerColor player, int turnIndex) { Player = player; TurnIndex = turnIndex; }
        public PlayerColor Player { get; }
        public int TurnIndex { get; }
        public override string ToString() => $"{Player} begins turn {TurnIndex}";
    }

    public sealed class DiceRolled : IGameEvent
    {
        public DiceRolled(DiceRoll roll, bool grantsAnotherRoll) { Roll = roll; GrantsAnotherRoll = grantsAnotherRoll; }
        public DiceRoll Roll { get; }
        public bool GrantsAnotherRoll { get; }
        public override string ToString() => $"rolled {Roll}";
    }

    public sealed class EnergyGranted : IGameEvent
    {
        public EnergyGranted(PlayerColor player, int stored, int burned, int total)
        {
            Player = player; Stored = stored; Burned = burned; Total = total;
        }
        public PlayerColor Player { get; }
        public int Stored { get; }

        /// <summary>Destroyed by the cap. Its own field because burn is a design signal, not bookkeeping.</summary>
        public int Burned { get; }
        public int Total { get; }
        public override string ToString() => $"{Player} +{Stored} energy (burned {Burned})";
    }

    public sealed class EnergySpent : IGameEvent
    {
        public EnergySpent(PlayerColor player, int amount, int remaining)
        {
            Player = player; Amount = amount; Remaining = remaining;
        }
        public PlayerColor Player { get; }
        public int Amount { get; }
        public int Remaining { get; }
        public override string ToString() => $"{Player} spends {Amount}";
    }

    /// <summary>
    /// A die was cashed for energy instead of being moved (§3.4, §6.8).
    /// Fortuna's House Edge.
    /// </summary>
    /// <remarks>
    /// Its own event rather than an <see cref="EnergyGranted"/>: the view has to
    /// show a die leaving the tray, and the harness counts sold dice separately
    /// from the drip.
    /// </remarks>
    public sealed class DieCashed : IGameEvent
    {
        public DieCashed(OperatorState op, int dieFace, int stored, int total)
        {
            Operator = op; DieFace = dieFace; Stored = stored; Total = total;
        }

        public OperatorState Operator { get; }
        public int DieFace { get; }

        /// <summary>What the pool actually took. Zero at the cap.</summary>
        public int Stored { get; }
        public int Total { get; }
        public override string ToString() => $"{Operator?.Name} cashes a {DieFace} for {Stored}";
    }

    /// <summary>
    /// The dice in hand were changed by a cast (§6.8): re-rolled, or set to a
    /// face. Fortuna's Deal Again and Boxcars.
    /// </summary>
    public sealed class DiceDealt : IGameEvent
    {
        public DiceDealt(OperatorState caster, IReadOnlyList<int> faces, bool grantsAnotherRoll)
        {
            Caster = caster; Faces = faces; GrantsAnotherRoll = grantsAnotherRoll;
        }

        public OperatorState Caster { get; }

        /// <summary>The unspent dice as they now stand, not only the ones that changed.</summary>
        public IReadOnlyList<int> Faces { get; }

        /// <summary>Whether a dealt double bought an extra roll (§6.8).</summary>
        public bool GrantsAnotherRoll { get; }
        public override string ToString() => $"{Caster?.Name} deals {string.Join(",", Faces)}";
    }

    /// <summary>
    /// A table was dealt on a cell (§7.7). Nothing has happened yet — it waits
    /// for traffic rather than for an upkeep, and the board must keep showing it.
    /// </summary>
    public sealed class TableDealt : IGameEvent
    {
        public TableDealt(OperatorState caster, CellRef cell, int stopDamage)
        {
            Caster = caster; Cell = cell; StopDamage = stopDamage;
        }

        public OperatorState Caster { get; }
        public CellRef Cell { get; }
        public int StopDamage { get; }
        public override string ToString() => $"{Caster?.Name} deals a table on {Cell}";
    }

    /// <summary>
    /// A dice move was stopped by a table (§7.7). The move that follows is the
    /// truncated one, so this is emitted before it.
    /// </summary>
    /// <remarks>
    /// Its own event because the view must explain why a piece stopped short of
    /// the landing it was promised — without it, the board looks like it
    /// miscounted.
    /// </remarks>
    public sealed class MoveIntercepted : IGameEvent
    {
        public MoveIntercepted(OperatorState mover, CellRef cell, PlayerColor owner, int cells)
        {
            Mover = mover; Cell = cell; Owner = owner; Cells = cells;
        }

        public OperatorState Mover { get; }

        /// <summary>The table's cell — where the mover is about to stop.</summary>
        public CellRef Cell { get; }

        /// <summary>The seat that dealt the table.</summary>
        public PlayerColor Owner { get; }

        /// <summary>How far the mover travels instead of its whole move.</summary>
        public int Cells { get; }
        public override string ToString() => $"{Mover?.Name} stopped at {Cell} by {Owner}";
    }

    /// <summary>
    /// A seat lost energy to an enemy ability (§3.3). Revú's Leech Round.
    /// </summary>
    public sealed class EnergyDrained : IGameEvent
    {
        public EnergyDrained(PlayerColor player, int amount, int remaining, OperatorState source)
        {
            Player = player; Amount = amount; Remaining = remaining; Source = source;
        }
        public PlayerColor Player { get; }
        public int Amount { get; }
        public int Remaining { get; }
        public OperatorState Source { get; }
        public override string ToString() =>
            $"{Source?.Name} drains {Amount} energy from {Player} ({Remaining} left)";
    }

    public sealed class OperatorDeployed : IGameEvent
    {
        public OperatorDeployed(OperatorState op, CellRef cell) { Operator = op; Cell = cell; }
        public OperatorState Operator { get; }
        public CellRef Cell { get; }
        public override string ToString() => $"{Operator.Name} deploys to {Cell}";
    }

    /// <summary>
    /// Bad-luck deploy protection fired: the turn ended as its player's
    /// <see cref="DroughtTurns"/>-th straight eligible turn without the deploy
    /// face, so a yard operator walks on free.
    /// </summary>
    public sealed class OperatorPityDeployed : IGameEvent
    {
        public OperatorPityDeployed(OperatorState op, CellRef cell, int droughtTurns)
        {
            Operator = op;
            Cell = cell;
            DroughtTurns = droughtTurns;
        }

        public OperatorState Operator { get; }
        public CellRef Cell { get; }

        /// <summary>Consecutive eligible turns without the deploy face that triggered this.</summary>
        public int DroughtTurns { get; }

        public override string ToString() =>
            $"{Operator.Name} deploys to {Cell} after {DroughtTurns} turns without a deploy face";
    }

    /// <summary>An operator changed progress, by moving or by being placed.</summary>
    /// <remarks>
    /// <b>A bounced move carries both landings</b> (PRESENTATION §3). <see cref="To"/>
    /// is where the operator ended up; <see cref="AttemptedTo"/> is the cell the
    /// dice sent it to before the collision threw it back. Without the second,
    /// the view could only draw the piece arriving at the bounce cell, and the
    /// collision was legible in the log but not on the board.
    /// </remarks>
    public sealed class OperatorMoved : IGameEvent
    {
        public OperatorMoved(OperatorState op, int from, int to, CellRef cell, int? attemptedTo = null)
        {
            Operator = op; From = from; To = to; Cell = cell;
            AttemptedTo = attemptedTo ?? to;
        }
        public OperatorState Operator { get; }
        public int From { get; }
        public int To { get; }
        public CellRef Cell { get; }

        /// <summary>
        /// The progress the move aimed for. Equal to <see cref="To"/> unless the
        /// mover was bounced back (§7.2). Placement reports it equal to
        /// <see cref="To"/>.
        /// </summary>
        public int AttemptedTo { get; }

        /// <summary>True when a collision threw the mover back from where it aimed.</summary>
        public bool Bounced => AttemptedTo != To;

        public override string ToString() =>
            Bounced
                ? $"{Operator.Name} {From} -> {AttemptedTo}, bounced to {To}"
                : $"{Operator.Name} {From} -> {To}";
    }

    public sealed class CollisionResolved : IGameEvent
    {
        public CollisionResolved(OperatorState mover, OperatorState occupant, bool moverBouncedBack)
        {
            Mover = mover; Occupant = occupant; MoverBouncedBack = moverBouncedBack;
        }
        public OperatorState Mover { get; }
        public OperatorState Occupant { get; }
        public bool MoverBouncedBack { get; }
        public override string ToString() =>
            $"{Mover.Name} hits {Occupant.Name}" + (MoverBouncedBack ? " and bounces" : " and takes the cell");
    }

    /// <summary>Health was lost. Carries what took it.</summary>
    /// <remarks>
    /// <b>The cause is presentation, never a rule.</b> Nothing in the core reads
    /// it. It exists because upkeep damage resolves in a phase where nothing else
    /// moves: a bleed or mark tick drops an operator's health with no visible
    /// agent on the board, so without a stated cause the player is left to infer
    /// one (<c>PRESENTATION.md</c> §2).
    ///
    /// It is optional so a caller with nothing useful to say can omit it rather
    /// than invent a label.
    /// </remarks>
    public sealed class DamageDealt : IGameEvent
    {
        public DamageDealt(OperatorState target, int amount, int remainingHealth, string cause = null,
            DamageType? type = null)
        {
            Target = target; Amount = amount; RemainingHealth = remainingHealth; Cause = cause; Type = type;
        }
        public OperatorState Target { get; }
        public int Amount { get; }
        public int RemainingHealth { get; }

        /// <summary>"bleed", "mark", "collision", "ability", "execute", "self", or null.</summary>
        public string Cause { get; }

        /// <summary>
        /// Normal, Tech or Atomic, as the instance arrived; null where no
        /// instance existed (an execute, a self-inflicted price).
        /// </summary>
        /// <remarks>
        /// Optional and appended, like <see cref="Cause"/>: for the view's
        /// damage-type layer under an impact (AUDIO.md AU3), read by no rule.
        /// </remarks>
        public DamageType? Type { get; }

        public override string ToString() =>
            Cause == null
                ? $"{Target.Name} takes {Amount} ({RemainingHealth} left)"
                : $"{Target.Name} takes {Amount} from {Cause} ({RemainingHealth} left)";
    }

    public sealed class DamageEvaded : IGameEvent
    {
        public DamageEvaded(OperatorState target) { Target = target; }
        public OperatorState Target { get; }
        public override string ToString() => $"{Target.Name} evades";
    }

    public sealed class DamageAbsorbed : IGameEvent
    {
        public DamageAbsorbed(OperatorState target) { Target = target; }
        public OperatorState Target { get; }
        public override string ToString() => $"{Target.Name}'s shield absorbs it";
    }

    /// <summary>
    /// A hit that arrived on safe ground and did nothing (§4.4, third
    /// amendment). Separate from <see cref="DamageAbsorbed"/> because the
    /// player has to learn where the target was standing, not what it had up.
    /// </summary>
    public sealed class DamageSheltered : IGameEvent
    {
        public DamageSheltered(OperatorState target) { Target = target; }
        public OperatorState Target { get; }
        public override string ToString() => $"{Target.Name} is on safe ground";
    }

    public sealed class HealApplied : IGameEvent
    {
        public HealApplied(OperatorState target, int amount) { Target = target; Amount = amount; }
        public OperatorState Target { get; }
        public int Amount { get; }
        public override string ToString() => $"{Target.Name} heals {Amount}";
    }

    /// <summary>
    /// Passive regeneration ticked (§5.11): the operator spent
    /// <see cref="WoundedTurns"/> straight owner-upkeeps in play, below half
    /// health and off any safe cell, and knits a point back.
    /// </summary>
    public sealed class OperatorRegenerated : IGameEvent
    {
        public OperatorRegenerated(OperatorState target, int amount, int woundedTurns)
        {
            Target = target;
            Amount = amount;
            WoundedTurns = woundedTurns;
        }

        public OperatorState Target { get; }
        public int Amount { get; }
        public int WoundedTurns { get; }

        public override string ToString() =>
            $"{Target.Name} regenerates {Amount} after {WoundedTurns} wounded turns";
    }

    public sealed class StatusApplied : IGameEvent
    {
        public StatusApplied(OperatorState target, StatusKind status, int duration)
        {
            Target = target; Status = status; Duration = duration;
        }
        public OperatorState Target { get; }
        public StatusKind Status { get; }
        public int Duration { get; }
        public override string ToString() => $"{Target.Name} gains {Status}";
    }

    public sealed class StatusExpired : IGameEvent
    {
        public StatusExpired(OperatorState target, StatusKind status) { Target = target; Status = status; }
        public OperatorState Target { get; }
        public StatusKind Status { get; }
        public override string ToString() => $"{Status} expires on {Target.Name}";
    }

    /// <summary>An operator reached zero health and went to its yard (§1.2). Carries what finished it.</summary>
    /// <remarks>
    /// The most consequential event in the game, and until now it did not say why
    /// it happened. A kill at upkeep is the case that forced this: the piece
    /// simply disappears from the track, and nothing on screen accounts for it.
    /// </remarks>
    public sealed class OperatorNeutralized : IGameEvent
    {
        public OperatorNeutralized(OperatorState op, string cause = null, PlayerColor? creditedTo = null)
        {
            Operator = op; Cause = cause; CreditedTo = creditedTo;
        }
        public OperatorState Operator { get; }

        /// <summary>The seat the knockout counts for, or null (see <c>NeutralizeOutcome.CreditedTo</c>).</summary>
        public PlayerColor? CreditedTo { get; }

        /// <summary>"bleed", "mark", "collision", "ability", "execute", "self", or null.</summary>
        public string Cause { get; }

        public override string ToString() =>
            Cause == null
                ? $"{Operator.Name} is neutralized"
                : $"{Operator.Name} is neutralized by {Cause}";
    }

    public sealed class OperatorReachedHome : IGameEvent
    {
        public OperatorReachedHome(OperatorState op) { Operator = op; }
        public OperatorState Operator { get; }
        public override string ToString() => $"{Operator.Name} is home";
    }

    public sealed class TurnEnded : IGameEvent
    {
        public TurnEnded(PlayerColor player) { Player = player; }
        public PlayerColor Player { get; }
        public override string ToString() => $"{Player} ends turn";
    }

    public sealed class GameWon : IGameEvent
    {
        /// <param name="seats">
        /// Every seat of the winning side, in table order (ADR-0012). Null is
        /// just <paramref name="winner"/>, which is what a free-for-all match
        /// means by "the winner" anyway.
        /// </param>
        public GameWon(PlayerColor winner, IReadOnlyList<PlayerColor> seats = null)
        {
            Winner = winner;
            Seats = seats ?? new[] { winner };
        }

        /// <summary>The seat the winning side is named after: its first at the table.</summary>
        public PlayerColor Winner { get; }

        /// <summary>
        /// Every seat of the winning side. One under free-for-all, two in a
        /// 1v1 team match — so a view that wants to say "RED &amp; GREEN TAKE
        /// THE HOUSE" has both without inferring the partner from the map.
        /// </summary>
        public IReadOnlyList<PlayerColor> Seats { get; }

        public override string ToString() => $"{string.Join(" & ", Seats)} wins";
    }
}