// Assets/_Project/Scripts/Core/Text/GuideCopy.cs
using System.Collections.Generic;

namespace NonaRoyale.Core.Text
{
    /// <summary>Which of the four camps an operator belongs to (OPERATORS.md, "The camps").</summary>
    public enum Camp { House = 0, Contractor = 1, Owner = 2, Alone = 3 }

    /// <summary>The hand-written part of one dossier: everything the rules lines cannot say.</summary>
    public sealed class GuideCopyEntry
    {
        public GuideCopyEntry(Camp camp, string tagline, string howToPlay, string howToBeat)
        {
            Camp = camp;
            Tagline = tagline;
            HowToPlay = howToPlay;
            HowToBeat = howToBeat;
        }

        public Camp Camp { get; }

        /// <summary>The "read at the table" line from <c>OPERATORS.md</c>.</summary>
        public string Tagline { get; }

        public string HowToPlay { get; }
        public string HowToBeat { get; }
    }

    /// <summary>
    /// The strategy copy for every operator (OPERATOR_GUIDE.md D3, OG5):
    /// drafted by Claude from <c>COMBAT_SYSTEMS.md</c>'s balance reasoning
    /// and <c>OPERATORS.md</c>'s "read at the table" lines, for the designer
    /// to rewrite in their own voice.
    /// </summary>
    /// <remarks>
    /// <b>No digits, anywhere in this file's strings</b> (D2). A number typed
    /// here is a number that will be wrong after the next balance pass;
    /// everything numeric is in the rules lines above these paragraphs. A test
    /// fails on any digit, and on any operator in the roster without an entry
    /// here.
    ///
    /// <b>What these paragraphs are for.</b> The rules line says what an
    /// ability does. These say when to use it and what beats it — the part a
    /// new player cannot work out from the numbers, and the part only design
    /// knowledge can write.
    ///
    /// <b>The camp lives here, not in <c>Core</c>'s rules,</b> because nothing
    /// in the rules knows about camps (OPERATORS.md: "a setting decision, not
    /// a mechanical one"). It is guide text that happens to be one word.
    /// </remarks>
    public static class GuideCopy
    {
        /// <summary>The camp's display name.</summary>
        public static string CampName(Camp camp)
        {
            switch (camp)
            {
                case Camp.House: return "The house";
                case Camp.Contractor: return "Contractor";
                case Camp.Owner: return "One of the Royale's owners";
                default: return "A camp of one";
            }
        }

        /// <summary>The entry for an operator by name, or null if nobody wrote one.</summary>
        public static GuideCopyEntry For(string operatorName) =>
            operatorName != null && Entries.TryGetValue(operatorName, out var entry) ? entry : null;

        /// <summary>Every written entry, keyed by operator name.</summary>
        public static IReadOnlyDictionary<string, GuideCopyEntry> All => Entries;

        private static readonly Dictionary<string, GuideCopyEntry> Entries = new Dictionary<string, GuideCopyEntry>
        {
            ["Bouncer"] = new GuideCopyEntry(Camp.House,
                "The one you route around.",
                "Stand in the lanes the enemy has to use and let your aura slow everyone who comes near. " +
                "Velvet Rope is Atomic, so it reaches operators that evasion or a shield would otherwise save, " +
                "and it drags them into your squad's reach; aimed at an ally, it pulls them out of trouble. " +
                "All-In Mauling costs you blood too, so spend it when the hit or the heal is worth it.",
                "Don't fight him where he wants to stand: go around him, or stay out of his reach. " +
                "His slow only works close to him and the rope is his only way to reach you, " +
                "so bait it out and move while it is cooling down."),

            ["Syla"] = new GuideCopyEntry(Camp.Contractor,
                "The one who finishes things.",
                "Open fights for others, don't start them alone. Ace Shards leaves everyone near you bleeding, " +
                "and From the Hip pays properly into a target that is already bleeding. " +
                "Tagged From Above marks a target and hides you; if your squad finishes the marked enemy while the mark holds, " +
                "everyone gets extra movement. She is fragile: never lead with her.",
                "Don't stand wounded in her reach, because everything she has is better against a hurt target. " +
                "Bleed resolves at your upkeep, so ending your turn on a safe cell makes it harmless. " +
                "Areas still find her while she is hidden, and she cannot take much."),

            ["Kurbyn"] = new GuideCopyEntry(Camp.House,
                "The one who is always already there.",
                "You are always hasted, so you choose where the fight happens. " +
                "Dive into a cluster with Dargin Pulse, which hits and stuns everything close, " +
                "then finish a wounded enemy with Miracle Pull: anything under half health is neutralized outright. " +
                "Evasion sometimes eats the first hit of a round, but never plan around it.",
                "Atomic damage ignores his evasion. Slows and stuns take away the mobility his kit is built on, " +
                "and Burdened cancels his haste outright — Nuetu is built for him. Keep wounded operators out of his reach, " +
                "because at half health they are one cast from the yard, and don't bunch up where his pulse can catch several."),

            ["Mimi"] = new GuideCopyEntry(Camp.Contractor,
                "The one who is never where you left her.",
                "Use your reach. Cryo-Pulse hits a target and everyone around it from further out than most can answer, " +
                "and leaves them bleeding and slowed. Cryo Field punishes anyone who stays near you. " +
                "When the fight arrives, Translocation swaps you out: with an enemy to escape, or with an ally to rescue them.",
                "She cannot outrun anything and cannot take much, so close in and hit her. " +
                "Cryo-Pulse is Tech, so a Tech Ward blocks it outright. Don't linger beside her while her field is up."),

            ["Javi"] = new GuideCopyEntry(Camp.House,
                "The one you have to kill twice.",
                "Keep your squad alive. Nanite Infusion heals an ally — or hurts an enemy and heals your allies standing near it. " +
                "Trauma Plate shields a friend before the hit lands, and Neural Purge clears every status from an ally: " +
                "stuns, slows, marks and charges alike. All three can be aimed at Javi himself.",
                "Kill him first, or kill through him: Atomic damage ignores his plates. " +
                "His cleanse only reaches his own side, and he can fix one thing a turn, so spread your pressure across his squad."),

            ["Kian"] = new GuideCopyEntry(Camp.Contractor,
                "The one who was aiming at where you were going to be.",
                "You never need to be near anyone. Inversion Matrix fires down the track ahead of you and stuns what it hits; " +
                "Sonic Disrupter hits, slows and shoves away everything close; Drone Strike paints any cell on the board " +
                "to go off at your next upkeep. Aim at where enemies will be, not where they are.",
                "He has no attack that picks out one target and no way to escape, so get next to him — " +
                "arrive with something to cast, because the disrupter will shove you back. " +
                "All of his damage is Tech: a Tech Ward blocks every hit. A painted cell is shown a round early; don't be on it."),

            ["Nuetu"] = new GuideCopyEntry(Camp.Contractor,
                "The one who gets back up on your money.",
                "Stay in the fight. Bio-Link Rage hits hard, heals you and leaves the target Burdened, which cancels haste — " +
                "he is the answer to fast operators. Plate yourself before a big hit, " +
                "and drop Killzone where enemies have to stand: it stuns everyone inside when it goes off, " +
                "the ground keeps hurting afterwards, and your Rage heals more while it is live.",
                "His reach is short and he cannot escape, so stay out of range and make him walk. " +
                "His plate stops Normal and Tech, not Atomic. Killzone goes off at his next upkeep: step out of the circle on your turn."),

            ["Sanity"] = new GuideCopyEntry(Camp.House,
                "The one the fight has to come to.",
                "You are slow on every roll, so make standing still a threat. Short Circuit stuns anyone right next to you. " +
                "Zero-Day sticks a charge on an enemy that goes off at your next upkeep wherever they run, " +
                "hitting and slowing everyone near them. Collision launches you along the track to a target, " +
                "through every enemy in the way, and stuns an enemy on arrival — or carries you to an ally.",
                "Don't stand next to him, and race him: he is the slowest piece on the board and can only chase with Collision. " +
                "A cleanse removes the Zero-Day charge before it goes off."),

            ["Luka"] = new GuideCopyEntry(Camp.Alone,
                "The one you have to outrun, not outlast.",
                "Pick one target and stay on it. Blind Spot puts you beside them and hits; " +
                "if they are still close at your next upkeep, you hit again, harder against big targets. " +
                "Hermes' Ring makes you immune to Tech damage for a while. " +
                "Vendetta's blows are Atomic, can crit, and heal you for what they take — your way back into a losing fight.",
                "After Blind Spot, move away on your turn: the second strike only lands if he is still close. " +
                "Nothing he carries shields him from Normal damage. His crits hit heavy targets harder, so tanks should not duel him alone."),

            ["Lethe"] = new GuideCopyEntry(Camp.Owner,
                "The one who decides where everyone stands.",
                "Keep your squad close: allies near you move faster, and you are always hasted yourself. " +
                "Nano Cell heals an ally and seals them in a shield that stops everything but Atomic, at the price of a stun — " +
                "move them first, then seal them. Eris' Exploit is for crowds: every enemy inside hurts for every other enemy inside, " +
                "now and again at your next upkeep.",
                "Spread out, because Eris' Exploit does nothing to a lone enemy. Atomic damage goes straight through Nano Cell. " +
                "Pull her squad away from her and they lose the haste."),

            ["Revú"] = new GuideCopyEntry(Camp.Owner,
                "The one who makes you pay to touch him.",
                "Lend first, collect later. Leech Round hurts and puts the target's side in debt; " +
                "they pay it from their pool when their turn ends, or it grows. Sadist deals whatever they owe, " +
                "and splashes the enemies around the target. Equilibrium halves expensive casts against you, " +
                "so enemies are pushed onto cheap ones — and every one they cast is energy not kept for the bill.",
                "Keep enough back to pay what you owe, or watch the debt grow toward his Sadist. " +
                "Cheap abilities hit him double: that is the way in. " +
                "Better still, land on him with the dice: collisions ignore his passive and burn your whole debt."),

            ["Fortuna"] = new GuideCopyEntry(Camp.Owner,
                "The one who deals the dice.",
                "You win the race, not the fight. Deal Again re-rolls your worst die; " +
                "Boxcars turns both dice into sixes and still earns the extra roll — two deploys, or a huge move. " +
                "The House Edge sells a die you don't want to move for energy. " +
                "The Table sits on the track and stops the first enemy dice move that tries to cross it.",
                "She cannot defend herself: no shield, no escape, nothing that stops a punch — get to her. " +
                "Revú's debt eats the energy she makes. Route around her tables, or move by placement: " +
                "pulls, pushes, swaps and dashes never trigger them."),
        };
    }
}
