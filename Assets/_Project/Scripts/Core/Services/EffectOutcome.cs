// Assets/_Project/Scripts/Core/Services/EffectOutcome.cs
using NonaRoyale.Core.Board;
using NonaRoyale.Core.Model;

namespace NonaRoyale.Core.Services
{
    public enum EffectOutcomeKind
    {
        Damaged = 0,
        Healed = 1,
        StatusApplied = 2,
        Pulled = 3,
        Executed = 4,

        /// <summary>
        /// One end of a swap. Two of these are emitted per swap, one per
        /// operator — a view told about only one would draw a board that is
        /// wrong.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="Pulled"/> even though both are placement,
        /// because the view plays them differently: a pull drags one piece
        /// toward another, a swap exchanges two at once.
        /// </remarks>
        Swapped = 5,

        /// <summary>One status stripped by a cleanse. One of these per status removed.</summary>
        StatusRemoved = 6,

        /// <summary>
        /// One operator shoved away from the caster. One of these per operator
        /// caught in the blast.
        /// </summary>
        /// <remarks>
        /// Its own kind rather than a negative <see cref="Pulled"/> for the same
        /// reason <see cref="Swapped"/> is: the view plays it differently, and a
        /// shockwave that shoves three pieces outward at once needs to read as
        /// one event with three subjects rather than three unrelated drags.
        ///
        /// <b>A clamped push reports the progress it actually reached</b>, which
        /// may be where the operator already stood. A recipient that could not
        /// move still appears in the list — the blast reached it, and a view
        /// that silently omitted it would imply it was outside the radius.
        /// </remarks>
        Pushed = 7,

        /// <summary>
        /// A beacon was placed on a cell. Nothing has happened to anybody yet
        /// (ADR-0006).
        /// </summary>
        /// <remarks>
        /// The only outcome whose subject is a place rather than an operator, so
        /// <see cref="EffectOutcome.Recipient"/> is the caster and
        /// <see cref="EffectOutcome.Cell"/> carries what actually matters. The
        /// view must draw it: an unannounced delayed strike is a trap, and the
        /// ability is designed around opponents seeing it and choosing.
        /// </remarks>
        BeaconPlaced = 8,

        /// <summary>
        /// A lingering zone was deployed on a cell (ADR-0007). Like
        /// <see cref="BeaconPlaced"/>, nothing has happened to anybody yet.
        /// </summary>
        /// <remarks>
        /// Distinct from a beacon because the view has to draw it differently and
        /// for longer: a beacon is a crosshair that resolves once, a zone is
        /// ground that stays dangerous for several rounds.
        /// </remarks>
        ZoneDeployed = 9,

        /// <summary>
        /// A charge was attached to an operator. Nothing has detonated yet — the
        /// charge fires at its owner's next upkeep (§6.4).
        /// </summary>
        /// <remarks>
        /// The telegraph the ability is balanced around: an attached grenade the
        /// opponent cannot see is a trap, not a prediction. <see cref="EffectOutcome.Recipient"/>
        /// is the operator now carrying the charge — the subject is a person,
        /// not a place, which is what separates it from <see cref="BeaconPlaced"/>.
        /// </remarks>
        ChargeAttached = 10,

        /// <summary>
        /// The caster was placed at the end of a dash (§7.6). One of these per
        /// dash; the enemies it struck on the way through are ordinary
        /// <see cref="Damaged"/> outcomes.
        /// </summary>
        /// <remarks>
        /// Its own kind rather than reusing <see cref="Pulled"/> for the same
        /// reason <see cref="Swapped"/> exists: the view plays a self-launched
        /// caster differently from a dragged victim, and the subject here is the
        /// caster rather than the ability's target.
        /// </remarks>
        Dashed = 11,

        /// <summary>
        /// A follow-up strike was set on an operator. Nothing has struck yet —
        /// it resolves at the caster's next upkeep, if the caster is still
        /// close enough (§6.5).
        /// </summary>
        /// <remarks>
        /// The <see cref="ChargeAttached"/> telegraph for Luka's Blind Spot, and its own
        /// kind for the same reason the two are separate events: a grenade
        /// that will go off wherever the target runs and a strike the target
        /// can outrun ask different things of the player who sees them.
        /// <see cref="EffectOutcome.Recipient"/> is the marked operator.
        /// </remarks>
        FollowUpMarked = 12,

        /// <summary>
        /// A field was projected onto the caster. Nothing has ticked yet — it
        /// first bills at the caster's next upkeep (§6.6).
        /// </summary>
        /// <remarks>
        /// The <see cref="ChargeAttached"/> telegraph for a self-anchored field:
        /// a Cryo Field the opponent cannot see is a trap rather than a zone to
        /// play around, and the <see cref="StatusKind.CryoField"/> badge keeps
        /// showing it on the board afterwards.
        /// <see cref="EffectOutcome.Recipient"/> is the caster carrying the field.
        /// </remarks>
        FieldProjected = 13,

        /// <summary>
        /// A watch was set on an operator. Nothing has struck yet — it trips
        /// the first time the target moves by dice before the caster's next
        /// upkeep, and lapses if it never does (§6.7).
        /// </summary>
        /// <remarks>
        /// The <see cref="ChargeAttached"/> telegraph for Kurbyn's Predator's
        /// Read: an unannounced watch is a trap, and the
        /// <see cref="StatusKind.Watched"/> badge keeps showing it on the
        /// board afterwards. Its own kind rather than a reused
        /// <see cref="FollowUpMarked"/> because the counterplay is the exact
        /// opposite — a follow-up asks the target to run, a watch asks it to
        /// stand still — and the view has to say which.
        /// <see cref="EffectOutcome.Recipient"/> is the watched operator.
        /// </remarks>
        WatchMarked = 14,

        /// <summary>
        /// Energy taken from a seat (§3.3). <see cref="EffectOutcome.Recipient"/>
        /// is the operator targeted, whose seat paid; <see cref="EffectOutcome.Amount"/>
        /// is what was actually taken. Emitted even when that is 0, so the view
        /// can say the pool was already dry.
        /// </summary>
        EnergyDrained = 15,

        /// <summary>
        /// The caster's seat was dealt dice (§6.8): <see cref="EffectOutcome.Amount"/>
        /// of them, set to <see cref="EffectOutcome.Duration"/> when that is not
        /// zero and re-rolled when it is. <see cref="EffectOutcome.Recipient"/>
        /// is the caster.
        /// </summary>
        /// <remarks>
        /// <b>The one outcome the engine acts on rather than only reports.</b>
        /// The dice are the engine's state, so the resolver declares what the
        /// cast is owed and <c>GameEngine</c> applies it — and the faces it ends
        /// up with are what the event carries, since a re-roll is not known
        /// until it is rolled.
        /// </remarks>
        DiceDealt = 16,

        /// <summary>
        /// A table was dealt on a cell (§7.7). Nothing has happened to anybody:
        /// it waits for traffic rather than for an upkeep.
        /// <see cref="EffectOutcome.Cell"/> carries where.
        /// </summary>
        TableDealt = 17
    }

    /// <summary>
    /// One thing an ability actually did, in the order it happened. The view
    /// turns this list into events and animations; the tests read it to assert
    /// on behaviour rather than on internal state.
    /// </summary>
    public readonly struct EffectOutcome
    {
        private EffectOutcome(
            EffectOutcomeKind kind, OperatorState recipient, DamageResult damage,
            int amount, StatusKind status, int duration, int progress,
            CellRef cell = default(CellRef))
        {
            Kind = kind;
            Recipient = recipient;
            Damage = damage;
            Amount = amount;
            Status = status;
            Duration = duration;
            Progress = progress;
            Cell = cell;
        }

        public EffectOutcomeKind Kind { get; }
        public OperatorState Recipient { get; }

        /// <summary>Meaningful for <see cref="EffectOutcomeKind.Damaged"/>.</summary>
        public DamageResult Damage { get; }

        /// <summary>Healing amount, for <see cref="EffectOutcomeKind.Healed"/>.</summary>
        public int Amount { get; }

        public StatusKind Status { get; }
        public int Duration { get; }

        /// <summary>Where a pulled, swapped or pushed operator was placed.</summary>
        public int Progress { get; }

        /// <summary>
        /// The painted cell, for <see cref="EffectOutcomeKind.BeaconPlaced"/>.
        /// Default for every other kind.
        /// </summary>
        public CellRef Cell { get; }

        public static EffectOutcome Damaged(OperatorState recipient, DamageResult damage) =>
            new EffectOutcome(EffectOutcomeKind.Damaged, recipient, damage, damage.AmountApplied, default, 0, 0);

        public static EffectOutcome Healed(OperatorState recipient, int amount) =>
            new EffectOutcome(EffectOutcomeKind.Healed, recipient, default, amount, default, 0, 0);

        public static EffectOutcome StatusApplied(OperatorState recipient, StatusKind status, int duration) =>
            new EffectOutcome(EffectOutcomeKind.StatusApplied, recipient, default, 0, status, duration, 0);

        public static EffectOutcome StatusRemoved(OperatorState recipient, StatusKind status) =>
            new EffectOutcome(EffectOutcomeKind.StatusRemoved, recipient, default, 0, status, 0, 0);

        public static EffectOutcome Pulled(OperatorState recipient, int progress) =>
            new EffectOutcome(EffectOutcomeKind.Pulled, recipient, default, 0, default, 0, progress);

        public static EffectOutcome Pushed(OperatorState recipient, int progress) =>
            new EffectOutcome(EffectOutcomeKind.Pushed, recipient, default, 0, default, 0, progress);

        public static EffectOutcome Swapped(OperatorState recipient, int progress) =>
            new EffectOutcome(EffectOutcomeKind.Swapped, recipient, default, 0, default, 0, progress);

        /// <summary>
        /// A beacon placed by <paramref name="caster"/> on <paramref name="cell"/>.
        /// <paramref name="totalDamage"/> is what it will divide among whoever it
        /// catches.
        /// </summary>
        public static EffectOutcome BeaconPlaced(OperatorState caster, CellRef cell, int totalDamage) =>
            new EffectOutcome(EffectOutcomeKind.BeaconPlaced, caster, default, totalDamage,
                default, 0, 0, cell);

        /// <summary>A zone deployed by <paramref name="caster"/> on <paramref name="cell"/>.</summary>
        public static EffectOutcome ZoneDeployed(OperatorState caster, CellRef cell, int detonationDamage) =>
            new EffectOutcome(EffectOutcomeKind.ZoneDeployed, caster, default, detonationDamage,
                default, 0, 0, cell);

        /// <summary>A charge attached to <paramref name="target"/>, which now carries it.</summary>
        public static EffectOutcome ChargeAttached(OperatorState target) =>
            new EffectOutcome(EffectOutcomeKind.ChargeAttached, target, default, 0, default, 0, 0);

        /// <summary>The caster placed at <paramref name="progress"/> by its own dash.</summary>
        public static EffectOutcome Dashed(OperatorState caster, int progress) =>
            new EffectOutcome(EffectOutcomeKind.Dashed, caster, default, 0, default, 0, progress);

        /// <summary>A follow-up strike set on <paramref name="target"/>.</summary>
        public static EffectOutcome FollowUpMarked(OperatorState target) =>
            new EffectOutcome(EffectOutcomeKind.FollowUpMarked, target, default, 0, default, 0, 0);

        /// <summary>A field projected onto <paramref name="caster"/>, who now carries it (§6.6).</summary>
        public static EffectOutcome FieldProjected(OperatorState caster) =>
            new EffectOutcome(EffectOutcomeKind.FieldProjected, caster, default, 0, default, 0, 0);

        /// <summary>A watch set on <paramref name="target"/>, which is now watched (§6.7).</summary>
        public static EffectOutcome WatchMarked(OperatorState target) =>
            new EffectOutcome(EffectOutcomeKind.WatchMarked, target, default, 0, default, 0, 0);

        public static EffectOutcome EnergyDrained(OperatorState recipient, int amount) =>
            new EffectOutcome(EffectOutcomeKind.EnergyDrained, recipient, default, amount, default, 0, 0);

        /// <summary>A table dealt by <paramref name="caster"/> on <paramref name="cell"/>.</summary>
        public static EffectOutcome TableDealt(OperatorState caster, CellRef cell, int stopDamage) =>
            new EffectOutcome(EffectOutcomeKind.TableDealt, caster, default, stopDamage,
                default, 0, 0, cell);

        /// <summary>
        /// Dice owed to the caster's seat: <paramref name="dice"/> of them, set
        /// to <paramref name="face"/>, or re-rolled when that is zero (§6.8).
        /// </summary>
        public static EffectOutcome DiceDealt(OperatorState caster, int dice, int face) =>
            new EffectOutcome(EffectOutcomeKind.DiceDealt, caster, default, dice, default, face, 0);

        public static EffectOutcome Executed(OperatorState recipient) =>
            new EffectOutcome(EffectOutcomeKind.Executed, recipient, default, 0, default, 0, 0);

        public override string ToString() =>
            Kind == EffectOutcomeKind.BeaconPlaced || Kind == EffectOutcomeKind.ZoneDeployed
                ? $"{Kind} -> {Cell}"
                : $"{Kind} -> {Recipient?.Name}";
    }
}