// Assets/_Project/Scripts/Unity/Audio/VoiceRules.cs
using System;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>
    /// The moments an operator speaks (AUDIO.md decision 5), lowest priority
    /// first: a later slot interrupts an earlier one.
    /// </summary>
    /// <remarks>
    /// The names are also the file names of real recordings:
    /// <c>Audio/Voice/Luka_HitTaken</c>. Don't rename a slot without renaming
    /// the files.
    /// </remarks>
    public enum VoiceSlot
    {
        Move,
        Deploy,
        HitTaken,
        Cast,
        Kill,
        Death,
        Victory,

        /// <summary>A match abandoned from the pause menu.</summary>
        Quit,
    }

    /// <summary>What <see cref="VoiceRules.Request"/> decided.</summary>
    public enum VoiceVerdict
    {
        /// <summary>Not played, and forgotten.</summary>
        Drop,

        /// <summary>Play it now; nothing was speaking.</summary>
        Play,

        /// <summary>Play it now, cutting off the line that was speaking.</summary>
        Interrupt,

        /// <summary>Not yet: it plays when the current line ends, if that is soon (<see cref="VoiceRules.HoldSeconds"/>).</summary>
        Hold,
    }

    /// <summary>
    /// Who gets to speak, and when (AUDIO.md decision 5, increment AU2).
    /// </summary>
    /// <remarks>
    /// <b>One voice at a time.</b> A request while a line is playing
    /// interrupts it when its slot is higher, and is dropped otherwise.
    ///
    /// <b>Moments are never lost to chatter.</b> Kill, death, victory and quit
    /// ignore the per-operator cooldown, and one that arrives while an equal
    /// or higher line is playing is held and plays straight after it. That is
    /// what lets a knockout sound as the victim's death line followed by the
    /// attacker's kill line, where a plain priority rule would drop the kill
    /// line every single time.
    ///
    /// <b>Chatter is thinned.</b> Below the moments, an operator who spoke in
    /// the last <see cref="CooldownSeconds"/> stays quiet, nobody starts
    /// within <see cref="BreathSeconds"/> of a line ending, and a move is
    /// voiced only <see cref="MoveChance"/> of the time.
    ///
    /// <b>Plain C#.</b> Time is whatever clock the caller keeps (the director
    /// stops it while the game is paused, as the voice itself stops), and the
    /// dice come from the function it is given, so tests are exact.
    /// </remarks>
    public sealed class VoiceRules
    {
        /// <summary>Seconds an operator stays quiet after speaking, below the moments.</summary>
        public const double CooldownSeconds = 3.5;

        /// <summary>Seconds of quiet after any line before chatter may start.</summary>
        public const double BreathSeconds = 0.35;

        /// <summary>How long a held moment waits for its turn before it is dropped.</summary>
        public const double HoldSeconds = 1.5;

        /// <summary>The share of moves that get a line.</summary>
        public const double MoveChance = 0.3;

        private readonly Func<double> _roll;
        private readonly System.Collections.Generic.Dictionary<string, double> _lastSpoke =
            new System.Collections.Generic.Dictionary<string, double>();

        private bool _speaking;
        private VoiceSlot _slot;
        private double _endsAt = double.NegativeInfinity;

        private bool _held;
        private VoiceSlot _heldSlot;
        private string _heldSpeaker;
        private double _heldLength;
        private double _heldAt;

        /// <param name="roll">Returns a number in [0, 1) for each chance roll.</param>
        public VoiceRules(Func<double> roll)
        {
            _roll = roll ?? throw new ArgumentNullException(nameof(roll));
        }

        /// <summary>True when the slot is a moment: kill, death, victory or quit.</summary>
        public static bool IsMoment(VoiceSlot slot) => slot >= VoiceSlot.Kill;

        /// <summary>True while a line started through these rules is still playing.</summary>
        public bool Speaking(double now) => _speaking && now < _endsAt;

        /// <summary>
        /// Asks to play a line of <paramref name="length"/> seconds. On
        /// <see cref="VoiceVerdict.Play"/> or <see cref="VoiceVerdict.Interrupt"/>
        /// the line counts as started at <paramref name="now"/>.
        /// </summary>
        public VoiceVerdict Request(VoiceSlot slot, string speaker, double length, double now)
        {
            if (string.IsNullOrEmpty(speaker) || length <= 0.0) return VoiceVerdict.Drop;

            bool moment = IsMoment(slot);

            if (slot == VoiceSlot.Move && _roll() >= MoveChance) return VoiceVerdict.Drop;

            if (!moment && _lastSpoke.TryGetValue(speaker, out double last) && now - last < CooldownSeconds)
                return VoiceVerdict.Drop;

            if (Speaking(now))
            {
                if (slot > _slot)
                {
                    Start(slot, speaker, length, now);
                    return VoiceVerdict.Interrupt;
                }

                if (!moment) return VoiceVerdict.Drop;

                // Keep the most important moment waiting; a lesser one is dropped.
                if (_held && HeldAlive(now) && _heldSlot >= slot) return VoiceVerdict.Drop;

                _held = true;
                _heldSlot = slot;
                _heldSpeaker = speaker;
                _heldLength = length;
                _heldAt = now;
                return VoiceVerdict.Hold;
            }

            if (!moment && now < _endsAt + BreathSeconds) return VoiceVerdict.Drop;

            Start(slot, speaker, length, now);
            return VoiceVerdict.Play;
        }

        /// <summary>
        /// Starts the held moment once the line in front of it has ended.
        /// Call every frame; true means the caller should play it now.
        /// </summary>
        public bool TryRelease(double now, out VoiceSlot slot, out string speaker)
        {
            slot = default;
            speaker = null;

            if (!_held) return false;

            if (!HeldAlive(now))
            {
                _held = false;
                return false;
            }

            if (Speaking(now)) return false;

            _held = false;
            slot = _heldSlot;
            speaker = _heldSpeaker;
            Start(slot, speaker, _heldLength, now);
            return true;
        }

        /// <summary>Forgets the playing and held lines, for a line cut off from outside (a match torn down).</summary>
        public void Stop()
        {
            _speaking = false;
            _held = false;
            _endsAt = double.NegativeInfinity;
        }

        private bool HeldAlive(double now) => now - _heldAt <= HoldSeconds;

        private void Start(VoiceSlot slot, string speaker, double length, double now)
        {
            _speaking = true;
            _slot = slot;
            _endsAt = now + length;
            _lastSpoke[speaker] = now;
        }
    }
}