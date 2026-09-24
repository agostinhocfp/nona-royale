// Assets/_Project/Scripts/Core/Abilities/EffectKind.cs
namespace NonaRoyale.Core.Abilities
{
    /// <summary>
    /// The complete vocabulary an ability is built from. Deliberately small:
    /// every ability on the alpha roster is a short list of these, and the
    /// remaining operators should be too.
    /// </summary>
    /// <remarks>
    /// When a new operator needs something this list cannot express, that is a
    /// signal to amend <c>COMBAT_SYSTEMS.md</c> and add a kind here — not to
    /// write a special case in one operator's stat block (§12, item 9).
    /// </remarks>
    public enum EffectKind
    {
        /// <summary>Damage through the pipeline, or straight to health when aimed at self.</summary>
        Damage = 0,

        /// <summary>Restore health, capped at maximum.</summary>
        Heal = 1,

        /// <summary>Apply a status for a duration.</summary>
        ApplyStatus = 2,

        /// <summary>Placement: move the target adjacent to the caster. Never collides (§7.4).</summary>
        PullToCaster = 3,

        /// <summary>
        /// Neutralize outright if the target is below a health fraction at cast
        /// time; otherwise deal the fallback damage instead.
        /// </summary>
        Execute = 4,

        /// <summary>
        /// Placement: caster and target exchange board cells. Never collides and
        /// triggers nothing, exactly as <see cref="PullToCaster"/> (§7.4).
        /// </summary>
        /// <remarks>
        /// Unlike every other kind, this one can be <i>illegal</i> for reasons
        /// targeting cannot see: a swap that would carry either operator off its
        /// own track is refused before the ability is paid for. See
        /// <c>AbilityResolver.TrySwapProgress</c>.
        /// </remarks>
        SwapWithCaster = 5,

        /// <summary>
        /// Removes every applied status from the recipient. Passives are
        /// untouched — a passive is who an operator is, not what it is carrying
        /// (§1.2, §5.1).
        /// </summary>
        /// <remarks>
        /// The first kind that <i>subtracts</i> from the status registry. That
        /// makes it the first whose interaction with other rules is a design
        /// question rather than an implementation one: a cleansed mark is gone,
        /// and its payout goes with it.
        /// </remarks>
        RemoveStatuses = 6,

        /// <summary>
        /// Placement: move the recipient away from the caster along the loop, by
        /// <c>AbilityEffect.Amount</c> cells. Never collides and triggers
        /// nothing (§7.4). Kian's Sonic Disrupter.
        /// </summary>
        /// <remarks>
        /// <b>The first placement kind that can move more than two operators,
        /// and it clamps rather than refuses.</b> §7.4's rule — one operator
        /// clamps, two refuse — was derived from a pull and a swap, both of
        /// which have a chosen target. This has none: it is a self-origin area,
        /// so refusing the whole cast because one of three enemies stands
        /// awkwardly gives the player nothing they could have done differently.
        /// The principle behind §7.4 is that <b>refusal requires an alternative
        /// the player could have chosen</b>, and there isn't one here.
        ///
        /// <b>Direction is away from the caster on the loop</b>, which means an
        /// enemy standing ahead of the caster is pushed <i>toward</i> its own
        /// home. That is known and accepted — the operator is meant to be
        /// punishing and swingy — but the forward clamp below is not optional.
        ///
        /// <b>It never carries anyone into a home column.</b> A forward push
        /// stops at the last outer-track cell. Without that clamp a 4-energy
        /// ability could finish an opponent's lap for them, which is the one
        /// outcome no amount of swinginess justifies. Backwards clamps at
        /// progress 0, matching <see cref="PullToCaster"/>.
        /// </remarks>
        PushFromCaster = 7,

        /// <summary>
        /// Paints a board cell. Nothing happens now; the effect resolves at the
        /// caster's next upkeep, striking whoever is standing there then
        /// (ADR-0006). Kian's Drone Strike.
        /// </summary>
        /// <remarks>
        /// <b>The first kind that names a place rather than an operator, and the
        /// first that does not resolve when it is cast.</b> Every other kind acts
        /// on a recipient the resolver already has in hand; this one has no
        /// recipient at all at cast time, which is the point — a beacon is a bet
        /// on where somebody will be, not a delayed hit on somebody chosen now.
        ///
        /// <c>Amount</c> carries the beam's total damage, divided among everyone
        /// it catches; <c>Radius</c> its spread around the painted cell.
        /// </remarks>
        PaintCell = 8,

        /// <summary>
        /// Deploys a lingering zone on a cell. It detonates at the caster's next
        /// upkeep and then bills again for a set number of that caster's turns
        /// (ADR-0007). Nuetu's Killzone.
        /// </summary>
        /// <remarks>
        /// <b>The sibling of <see cref="PaintCell"/>, and deliberately its
        /// opposite on two axes.</b> A beacon is one moment and splits its damage
        /// among everyone caught, so it is strongest against a lone target; a
        /// zone is a duration and bills each victim in full, so it is strongest
        /// against a crowd. Two cell abilities that felt the same would not have
        /// been worth two.
        ///
        /// <b>Only the detonation applies its status.</b> Stun blocks movement
        /// (§5.1), so a zone that stunned on every tick would hold an operator
        /// inside itself until it expired — nine energy to remove somebody from
        /// the game. The grenade crushes once; what lingers only grinds.
        /// </remarks>
        DeployZone = 9,

        /// <summary>
        /// Attaches a charge to the primary target. Nothing happens now; the
        /// charge follows the target and detonates at the caster's next upkeep,
        /// on whatever cell the target then occupies (§6.4). Sanity's Zero-Day.
        /// </summary>
        /// <remarks>
        /// <b>The sibling of <see cref="PaintCell"/> anchored to a victim rather
        /// than a place.</b> A beacon is a bet on where somebody will be; an
        /// attached charge is a delayed certainty that somebody will be struck
        /// wherever they go — which is exactly why it is cleanse-detachable
        /// (§5.10). The marker status is the counterplay, not decoration.
        ///
        /// <c>Amount</c> carries the splash dealt to every enemy in
        /// <c>Radius</c> of the detonation cell; <c>Stacks</c> the bonus added
        /// for the marked target itself; <c>Status</c>/<c>Duration</c> the
        /// status applied to everyone caught. Reused fields, the same trade
        /// <see cref="DeployZone"/> already makes.
        /// </remarks>
        AttachCharge = 10,

        /// <summary>
        /// The caster dashes along the track to the primary target, damaging
        /// every enemy on the traversed cells, and is <i>placed</i> one step
        /// past the target along the dash direction — one short of it, on the
        /// caster's side, when that cell is occupied (§7.6). Sanity's Collision.
        /// </summary>
        /// <remarks>
        /// <b>The first kind that repositions the caster without swapping.</b>
        /// Pull, push and swap all move somebody else (or exchange two pieces);
        /// this moves the caster, and the landing is placement rather than
        /// movement — it collides with nothing and triggers nothing (§7.4),
        /// which is also why the dash itself passes through occupants without
        /// contesting them (§7.1).
        ///
        /// <b>Only the path damage lives here.</b> What happens to the target
        /// itself — Collision's hit and stun — is declared as ordinary
        /// enemy-audience effects on the ability, so the cast-mode system
        /// filters them out for an ally target instead of the dash branching on
        /// it. <c>Amount</c> carries the per-enemy path damage.
        /// </remarks>
        DashToTarget = 11,

        /// <summary>
        /// Marks the primary target for a follow-up strike. At the caster's
        /// next upkeep, if the caster is still within <c>Radius</c> of the
        /// target, the target takes <c>Amount</c> — plus
        /// <c>HeavyBonus</c> against a heavy target. Otherwise nothing
        /// happens (§6.5). Luka's Blind Spot.
        /// </summary>
        /// <remarks>
        /// <b>The sibling of <see cref="AttachCharge"/> with the blast taken
        /// out and a condition put in.</b> A charge is a delayed certainty
        /// that follows its victim; a follow-up is a delayed <i>threat</i> the
        /// victim can walk away from. Same pending registry, same marker
        /// pattern, same cleanse counterplay (§5.13) — the victim gets two
        /// answers rather than one: run, or be cleansed.
        ///
        /// Proximity is measured along the track in either direction, the
        /// same distance targeting uses (§4.1), between the two operators as
        /// they stand at the caster's upkeep — before the caster moves.
        /// </remarks>
        FollowUp = 12,

        /// <summary>
        /// Projects a self-anchored field onto the caster. Nothing happens now;
        /// at each of the caster's owner-upkeeps while the field stands, every
        /// enemy within <c>Radius</c> of the caster's <i>current</i> cell takes
        /// <c>Amount</c> (§6.6). Mimi's Cryo Field.
        /// </summary>
        /// <remarks>
        /// <b>The sibling of <see cref="AttachCharge"/> anchored to the caster
        /// herself, and repeating.</b> A charge follows its victim and resolves
        /// once; a field follows its caster and bills every upkeep for its
        /// duration. Same pending registry, same marker pattern — the marker is
        /// <see cref="StatusKind.CryoField"/>, and a cleanse or the caster's
        /// neutralize ends the field by stripping it (§5.14).
        ///
        /// <c>Amount</c> carries the per-tick damage, <c>Radius</c> the field's
        /// reach, <c>Duration</c> the marker's span in the caster's own turns —
        /// which, self-applied on her turn, counts the cast turn as its first,
        /// so a field meant to tick twice lasts three (§5). Reused fields, the
        /// same trade <see cref="DeployZone"/> already makes.
        /// </remarks>
        ProjectField = 13,

        /// <summary>
        /// Sets a watch on the primary target. Nothing happens now; if the
        /// target moves <b>by dice</b> before the caster's owner's next upkeep,
        /// it takes <c>Amount</c>, once, and the watch is spent. If it never
        /// moves, the watch lapses at that upkeep (§6.7). Kurbyn's Predator's
        /// Read.
        /// </summary>
        /// <remarks>
        /// <b>The follow-up's mirror (§6.5): a strike conditional on what the
        /// target does — but resolved the moment it does it, not at upkeep.</b>
        /// A follow-up punishes a target that stayed close; a watch punishes
        /// the first dice movement and lapses if none came. Same pending
        /// registry, same marker pattern — the marker is
        /// <see cref="StatusKind.Watched"/>, and a cleanse strips it to cancel
        /// the watch (§5.15).
        ///
        /// <b>Placement never trips it</b> (§7.4): pulls, pushes, swaps, dashes
        /// and bounce-backs relocate the target without spending its move, and
        /// a watch that punished those would punish the victim for somebody
        /// else's action. Standing still, and being moved by somebody else,
        /// are the two escape hatches.
        ///
        /// <c>Amount</c> carries the damage; <c>DamageType</c> the type. No
        /// radius, no status rider, no heavy rule — the payload is one number.
        /// </remarks>
        Watch = 14,

        /// <summary>
        /// Puts the primary target's seat <c>Amount</c> deeper in debt to the
        /// caster, within the cap (§3.3, 2026-09-24). Revú's Leech Round.
        /// </summary>
        /// <remarks>
        /// <b>No energy moves, now or later.</b> The debt grows by interest as
        /// the seat ends its turns, until Sadist calls it or a collision burns
        /// it. It replaced an instant drain (2026-09-17), which gave the victim
        /// nothing to answer and the spectator nothing to follow.
        ///
        /// The first kind that reaches past an operator into a player. It needs
        /// the seats, which the resolver takes at composition; a resolver built
        /// without them refuses to run this kind.
        /// </remarks>
        IncurDebt = 15,

        /// <summary>
        /// Calls in the primary target's seat debt as damage (§3.3): the whole
        /// debt, at least <c>MinimumDamage</c>, to the target, and that figure
        /// divided by <c>Stacks</c> (rounded down) to enemies within
        /// <c>Radius</c> of it. The debt is then cleared. Revú's Sadist.
        /// </summary>
        /// <remarks>
        /// <b>One number, read at cast time from the target's seat, and the
        /// debtor can see it all along</b> — the seat's own debt is the hit it is
        /// risking, which is the readability rule the old "one damage per three
        /// energy missing" figure only approximated. A share of 0 is not dealt
        /// at all.
        /// </remarks>
        DebtDamage = 16,

        /// <summary>
        /// Changes the dice the caster's seat is holding: re-rolls
        /// <c>AbilityEffect.Amount</c> unspent dice, or sets that many to the
        /// face in <c>AbilityEffect.Stacks</c> when it is not zero (§6.8).
        /// Fortuna's Deal Again and Boxcars.
        /// </summary>
        /// <remarks>
        /// <b>The first kind whose subject is the roll rather than the board.</b>
        /// Every other kind acts on an operator, a cell or a seat's pool, all of
        /// which the resolver owns. The unspent dice belong to
        /// <c>GameEngine</c>, so the resolver declares the intent as a
        /// <c>DiceDealt</c> outcome and the engine carries it out — the same
        /// division the deferred registries already use, where a cast records
        /// something another service resolves.
        ///
        /// <b>The engine refuses the cast before it is paid for</b> when the seat
        /// is not holding the dice the effect needs, so a Boxcars thrown at a
        /// half-spent roll costs nothing (§6.8).
        ///
        /// <b>A dealt double is not a rolled one.</b> The doubles re-roll is
        /// decided when the dice leave the cup, so re-rolling never creates or
        /// destroys one; Boxcars grants its extra roll explicitly, and only
        /// inside <c>GameConfig.MaxRollsPerTurn</c>.
        /// </remarks>
        DealDice = 17,

        /// <summary>
        /// Deals a table on a cell. Nothing happens now and nothing happens at an
        /// upkeep either: the first enemy dice move that crosses or ends on the
        /// cell stops there and takes <c>AbilityEffect.Amount</c>, once per
        /// enemy operator, while it stands (§7.7, ADR-0007 Amendment 3).
        /// Fortuna's The Table.
        /// </summary>
        /// <remarks>
        /// <b>The third cell kind, and the only one that reads the cells a move
        /// passes through.</b> A beacon (<see cref="PaintCell"/>) is a bet on
        /// where somebody will be and a zone (<see cref="DeployZone"/>) is ground
        /// that grinds whoever stands in it — both resolve on their owner's
        /// upkeep, against whoever is there then. A table never resolves on a
        /// clock at all. It is a rule about traffic, and it is the only effect in
        /// the game that can shorten a move.
        ///
        /// <c>Amount</c> carries what a stopped mover is billed, <c>Stacks</c> how
        /// many of its owner's turns it stands for. Reused fields, the same trade
        /// <see cref="DeployZone"/> makes.
        /// </remarks>
        SetTable = 18,

        /// <summary>
        /// Draws every enemy within <c>Radius</c> of the target cell up to
        /// <c>Amount</c> cells toward it (§7.4, 2026-09-24). Placement, never
        /// movement: it collides with nothing and triggers nothing. Lethe's
        /// Eris' Exploit, ahead of its zone.
        /// </summary>
        /// <remarks>
        /// <b>Why it exists.</b> Eris' Exploit bills each victim for the rest of
        /// the crowd, and a dice race almost never makes a crowd: three enemies
        /// within its reach on 1% of her turns (<c>LETHE_ANALYSIS.md</c>). The
        /// draw makes the crowd the zone then punishes.
        ///
        /// <b>Only pieces on the outer loop move</b>, and each clamps the way
        /// every one-operator placement does: never behind its own start cell,
        /// never past its last loop cell into its home column. Safe cells do not
        /// hold a piece against it — placement is not damage.
        ///
        /// A cell kind: the resolver routes it with the target cell, like
        /// <see cref="DeployZone"/>, and it must be declared before the zone so
        /// the zone strikes the crowd it made.
        /// </remarks>
        DrawToCell = 19
    }
}