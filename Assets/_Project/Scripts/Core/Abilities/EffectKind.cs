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
        DeployZone = 9
    }
}