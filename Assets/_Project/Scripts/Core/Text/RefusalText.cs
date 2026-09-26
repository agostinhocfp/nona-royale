// Assets/_Project/Scripts/Core/Text/RefusalText.cs
using System;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Model;
using NonaRoyale.Core.Services;

namespace NonaRoyale.Core.Text
{
    /// <summary>
    /// Why a cast was refused, in the player's words (LAUNCH_UI_PASS.md, G7b-2).
    /// </summary>
    /// <remarks>
    /// <b>Why it exists.</b> The engine used to refuse a cast with the enum's
    /// own name ("Blind Spot: IllegalTarget"). Those enums were made reasons
    /// so the view could explain itself (<see cref="AbilityRefusal"/>,
    /// <see cref="TargetingVerdict"/>). This is that explanation, written once
    /// in the core, so the toast and the log say the same thing.
    ///
    /// Every refusal names what to do about it where there is something to
    /// do: which pick is missing, how long a cooldown has left, how much energy
    /// is short. A refusal is how a player learns a rule the screen did not
    /// teach, so it should teach it.
    ///
    /// <b>It describes, it never decides.</b> The caller passes the numbers in
    /// (the turns left, the energy held); nothing here reads the board.
    /// </remarks>
    public static class RefusalText
    {
        /// <summary>A refused cast, as a sentence.</summary>
        /// <param name="turnsUntilReady">What the resolver says is left on the cooldown.</param>
        /// <param name="energyHeld">The seat's energy when the cast was tried.</param>
        public static string Ability(
            AbilityDefinition ability, OperatorState caster, AbilityRefusal refusal, TargetingVerdict verdict,
            int turnsUntilReady, int energyHeld)
        {
            if (ability == null) throw new ArgumentNullException(nameof(ability));

            string name = ability.Name;
            string who = caster?.Name ?? "The caster";

            switch (refusal)
            {
                case AbilityRefusal.OnCooldown:
                    return turnsUntilReady > 0
                        ? $"{name} is recharging: ready in {Turns(turnsUntilReady)}"
                        : $"{name} is recharging";

                case AbilityRefusal.InsufficientEnergy:
                    return $"{name} costs {ability.EnergyCost} energy; you have {energyHeld}";

                case AbilityRefusal.NoTarget:
                    return $"{name} needs a target: click an operator";

                case AbilityRefusal.NoCell:
                    return $"{name} needs a cell: click the board";

                case AbilityRefusal.CasterStunned:
                    return $"{who} is stunned";

                case AbilityRefusal.CasterOutOfPlay:
                    return $"{who} is out of the fight";

                case AbilityRefusal.IllegalTarget:
                    return verdict == TargetingVerdict.CasterOutOfPlay
                        ? $"{who} is out of the fight"
                        : $"{name}: {Targeting(verdict)}";

                default:
                    return $"{name} can't be cast now";
            }
        }

        /// <summary>Why a target or a cell can't be aimed at, as a clause.</summary>
        public static string Targeting(TargetingVerdict verdict)
        {
            switch (verdict)
            {
                case TargetingVerdict.CasterOutOfPlay: return "the caster is out of the fight";
                case TargetingVerdict.TargetOutOfPlay: return "that operator is out of the fight";
                case TargetingVerdict.OutOfRange: return "out of range";
                case TargetingVerdict.Stealthed: return "that operator is in Stealth";
                case TargetingVerdict.SwapWouldLeaveTheTrack: return "the swap would put someone off the track";
                case TargetingVerdict.WrongSide: return "it does nothing to that side";
                case TargetingVerdict.CellOutOfPlay: return "that cell is out of play";
                case TargetingVerdict.OnASafeCell: return "that operator is on a safe cell";
                case TargetingVerdict.AimedBehindFromSafeCell: return "you can't aim backwards from a safe cell";
                case TargetingVerdict.CannotTargetSelf: return "it can't target its own caster";
                default: return "that target isn't allowed";
            }
        }

        private static string Turns(int turns) => turns == 1 ? "1 turn" : turns + " turns";
    }
}
