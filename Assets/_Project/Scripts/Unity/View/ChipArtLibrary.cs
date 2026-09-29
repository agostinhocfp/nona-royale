// Assets/_Project/Scripts/Unity/View/ChipArtLibrary.cs
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>An operator's chip portrait: the face at rest, its lit twin, and where the device sits on it.</summary>
    public sealed class ChipArt
    {
        /// <summary>The portrait with the device dark, already cut to a circle by the importer.</summary>
        public Sprite Unlit { get; }

        /// <summary>The same portrait with the device lit, or null when there is none (the flare alone plays).</summary>
        public Sprite Lit { get; }

        /// <summary>The device, in face units: -0.5..0.5 across the face's square, y up. Where the cast's flare is centred.</summary>
        public Vector2 Device { get; }

        public ChipArt(Sprite unlit, Sprite lit, Vector2 device)
        {
            Unlit = unlit;
            Lit = lit;
            Device = device;
        }
    }

    /// <summary>
    /// Finds chip portraits by name, and remembers what it found (chip
    /// pieces, 2026-09-29).
    /// </summary>
    /// <remarks>
    /// <b>The files are the generator's own</b>, dropped into
    /// <c>Assets/_Project/Art/Resources/Art/Chips/</c> as
    /// <c>&lt;name&gt;_chip_unlit</c> and <c>&lt;name&gt;_chip_lit</c>, the
    /// names they carry in <c>art/source/characters/</c>. The importer
    /// (<c>Editor/ChipArtImporter</c>) cuts them to a circle of
    /// <see cref="MaskRadius"/>, so the black outside the painted circle
    /// never shows. A missing unlit file means an emblem chip; a missing lit
    /// file means the cast shows the flare alone.
    ///
    /// <b>Device anchors are data about one image</b>, so they are kept here
    /// by name rather than guessed (<c>ART_PROMPTS.md</c> asset block 7). A
    /// new portrait with no anchor flares at the face's centre. If a
    /// portrait is regenerated, its anchor is measured again.
    /// </remarks>
    public static class ChipArtLibrary
    {
        public const string Folder = "Art/Chips/";

        /// <summary>
        /// The circle the importer keeps, as a fraction of the image's width.
        /// The painted circle in the generator's output reaches about 0.49;
        /// this stays just inside it, and the gilt ring covers the seam.
        /// </summary>
        public const float MaskRadius = 0.48f;

        /// <summary>
        /// Where each device sits, normalised with the origin at the image's
        /// top-left, as measured on the approved portraits (2026-09-29).
        /// </summary>
        private static readonly Dictionary<string, Vector2> Anchors = new Dictionary<string, Vector2>
        {
            { "luka", new Vector2(0.69f, 0.58f) },      // the signet ring
            { "bouncer", new Vector2(0.66f, 0.76f) },   // the gauntlet's fist
            { "syla", new Vector2(0.84f, 0.33f) },      // the drone at her fingertips
            { "kurbyn", new Vector2(0.473f, 0.283f) },  // the neural rig behind his ear
        };

        private static readonly Dictionary<string, ChipArt> Cache = new Dictionary<string, ChipArt>();

        /// <summary>The resource path of a portrait, e.g. <c>Art/Chips/luka_chip_unlit</c>.</summary>
        public static string PathFor(string operatorName, bool lit) =>
            Folder + OperatorArtNames.Key(operatorName) + (lit ? "_chip_lit" : "_chip_unlit");

        /// <summary>
        /// The device's position in face units (-0.5..0.5, y up) from an
        /// anchor measured on the image (0..1, origin top-left).
        /// </summary>
        public static Vector2 FaceUnits(Vector2 imageAnchor) =>
            new Vector2(imageAnchor.x - 0.5f, 0.5f - imageAnchor.y);

        /// <summary>The device's position in face units for an operator; the centre when none was measured.</summary>
        public static Vector2 DeviceFor(string operatorName) =>
            Anchors.TryGetValue(OperatorArtNames.Key(operatorName), out var anchor) ? FaceUnits(anchor) : Vector2.zero;

        /// <summary>Whether an anchor was measured for this operator.</summary>
        public static bool HasAnchor(string operatorName) => Anchors.ContainsKey(OperatorArtNames.Key(operatorName));

        /// <summary>The chip portrait, or null for an emblem chip.</summary>
        public static ChipArt For(string operatorName)
        {
            var key = OperatorArtNames.Key(operatorName);
            if (Cache.TryGetValue(key, out var cached) && (cached == null || cached.Unlit != null)) return cached;

            var unlit = Resources.Load<Sprite>(PathFor(operatorName, lit: false));
            var art = unlit != null
                ? new ChipArt(unlit, Resources.Load<Sprite>(PathFor(operatorName, lit: true)), DeviceFor(operatorName))
                : null;

            Cache[key] = art;
            return art;
        }

        /// <summary>Forgets every lookup, so the next one loads again. For tests and tools.</summary>
        public static void ClearCache() => Cache.Clear();
    }
}
