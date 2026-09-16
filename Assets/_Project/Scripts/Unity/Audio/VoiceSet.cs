// Assets/_Project/Scripts/Unity/Audio/VoiceSet.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.Audio
{
    /// <summary>
    /// One operator's voice: its lines per slot (AUDIO.md increment AU2).
    /// </summary>
    /// <remarks>
    /// <b>Real recordings or placeholders, never a mix.</b> If any file exists
    /// for the operator in <c>Assets/_Project/Audio/Resources/Audio/Voice/</c>,
    /// the set is real and a slot without files is silent: a recorded voice
    /// answered by a synthesized chirp would sound broken. With no files, every
    /// slot gets <see cref="VoiceBlips"/>.
    ///
    /// <b>File names:</b> <c>&lt;Operator&gt;_&lt;Slot&gt;</c>, then
    /// <c>_2</c>, <c>_3</c>… up to <c>_8</c> for variants, for example
    /// <c>Luka_Kill</c>, <c>Luka_Kill_2</c>. The operator part is the name as
    /// ASCII letters and digits (<see cref="VoiceBlips.FileKey"/>), the slot
    /// part is the <see cref="VoiceSlot"/> name. Variants are read until the
    /// first gap, so <c>_3</c> without <c>_2</c> is never found.
    /// </remarks>
    public sealed class VoiceSet
    {
        public const string Folder = "Audio/Voice/";

        /// <summary>Highest variant suffix looked for.</summary>
        public const int MaxFileVariants = 8;

        private static readonly int SlotCount = Enum.GetValues(typeof(VoiceSlot)).Length;

        private readonly List<AudioClip>[] _lines;

        private VoiceSet(string name, bool real)
        {
            Name = name;
            IsReal = real;
            _lines = new List<AudioClip>[SlotCount];
            for (int i = 0; i < SlotCount; i++) _lines[i] = new List<AudioClip>();
        }

        /// <summary>The operator's name as the game shows it.</summary>
        public string Name { get; }

        /// <summary>True when the lines come from recordings.</summary>
        public bool IsReal { get; }

        /// <summary>
        /// The operator's recorded set, or a placeholder set to be filled with
        /// blips (<paramref name="placeholder"/> true).
        /// </summary>
        public static VoiceSet Load(string name, out bool placeholder)
        {
            string key = VoiceBlips.FileKey(name);
            var real = new VoiceSet(name, real: true);
            bool any = false;

            if (key.Length > 0)
            {
                foreach (VoiceSlot slot in Enum.GetValues(typeof(VoiceSlot)))
                {
                    string path = $"{Folder}{key}_{slot}";
                    for (int n = 1; n <= MaxFileVariants; n++)
                    {
                        var clip = Resources.Load<AudioClip>(n == 1 ? path : $"{path}_{n}");
                        if (clip == null) break;

                        real.Add(slot, clip);
                        any = true;
                    }
                }
            }

            placeholder = !any;
            return any ? real : new VoiceSet(name, real: false);
        }

        public void Add(VoiceSlot slot, AudioClip clip)
        {
            if (clip != null) _lines[(int)slot].Add(clip);
        }

        /// <summary>A line for the slot, or null when it has none (yet).</summary>
        public AudioClip Pick(VoiceSlot slot, System.Random random)
        {
            var list = _lines[(int)slot];
            return list.Count == 0 ? null : list[random.Next(list.Count)];
        }
    }
}