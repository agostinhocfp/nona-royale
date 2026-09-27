// Assets/_Project/Scripts/Unity/View/BoardSprites.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// One slot of the board-skin contract: a file name, its size and whether
    /// the code tints it per seat (HANDOFF_board_assets.md §1).
    /// </summary>
    public readonly struct BoardSlot
    {
        /// <summary>The file name without extension, and the constant's value.</summary>
        public string Name { get; }

        /// <summary>The square size the art is painted at, in pixels.</summary>
        public int SizePx { get; }

        /// <summary>
        /// Painted as neutral grey around 50 %, and multiplied by the seat
        /// colour in code. False means the art carries its own colour.
        /// </summary>
        public bool Tinted { get; }

        public BoardSlot(string name, int sizePx, bool tinted)
        {
            Name = name;
            SizePx = sizePx;
            Tinted = tinted;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Painted board sprites, found by slot name (board skin BS1).
    /// </summary>
    /// <remarks>
    /// <b>The code side of the contract.</b> <c>HANDOFF_board_assets.md</c> §1
    /// lists every slot; <see cref="Slots"/> is that table in code, and an
    /// EditMode test holds the two together. Files live under
    /// <c>Assets/_Project/Art/Resources/Art/Board/</c>, named exactly after
    /// the slot. The three tileable textures in the same folder
    /// (<c>board_marble</c>, <c>board_felt</c>, <c>board_carpet</c>) are not
    /// slots here: <see cref="BoardTextures"/> owns them.
    ///
    /// <b>Optional, like every painted asset.</b> An empty slot is never an
    /// error: <see cref="Get"/> returns null and the caller draws its
    /// procedural surface. Filling a slot later needs no code change.
    ///
    /// <b>Size-agnostic.</b> The importer (<c>Editor/BoardSpriteImporter</c>)
    /// sets pixels-per-unit to the image's width, so a sprite is one unit
    /// across. Callers still scale by <see cref="UnitScale"/>, which reads
    /// the sprite's bounds, so an image at the wrong size or an Inspector
    /// edit can't misplace anything: the code owns geometry.
    ///
    /// <b>Cached per play session.</b> The cache is cleared on entering Play
    /// Mode, so a file dropped in while stopped shows on the next Play.
    /// </remarks>
    public static class BoardSprites
    {
        /// <summary>The Resources folder, shared with <see cref="BoardTextures"/>.</summary>
        public const string Folder = BoardTextures.Folder;

        // The board.
        public const string CellTrack = "board_cell_track";
        public const string CellHome = "board_cell_home";
        public const string StartEmblem = "board_start_emblem";
        public const string Medallion = "board_medallion";
        public const string MedallionEmblem = "board_medallion_emblem";
        public const string CornerWedge = "board_corner_wedge";

        // The yard tables.
        public const string TableFelt = "yard_table_felt";
        public const string TableRim = "yard_table_rim";
        public const string TableEmblem = "yard_table_emblem";
        public const string Chair = "yard_chair";

        // Props on the table margin.
        public const string Candle = "prop_candle";
        public const string Chips = "prop_chips";
        public const string Plant = "prop_plant";
        public const string Instrument = "prop_instrument";

        /// <summary>Every slot in the contract, in its table's order.</summary>
        public static readonly IReadOnlyList<BoardSlot> Slots = new[]
        {
            new BoardSlot(CellTrack, 256, tinted: false),
            new BoardSlot(CellHome, 256, tinted: true),
            new BoardSlot(StartEmblem, 256, tinted: true),
            new BoardSlot(Medallion, 512, tinted: false),
            new BoardSlot(MedallionEmblem, 512, tinted: false),
            new BoardSlot(CornerWedge, 256, tinted: false),
            new BoardSlot(TableFelt, 1024, tinted: true),
            new BoardSlot(TableRim, 1024, tinted: false),
            new BoardSlot(TableEmblem, 256, tinted: false),
            new BoardSlot(Chair, 256, tinted: true),
            new BoardSlot(Candle, 256, tinted: false),
            new BoardSlot(Chips, 256, tinted: false),
            new BoardSlot(Plant, 512, tinted: false),
            new BoardSlot(Instrument, 512, tinted: false),
        };

        private static readonly Dictionary<string, BoardSlot> ByName = Index();
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Warned = new HashSet<string>();

        /// <summary>True when <paramref name="name"/> is a slot in the contract.</summary>
        public static bool IsSlot(string name) => name != null && ByName.ContainsKey(name);

        /// <summary>The slot called <paramref name="name"/>, if the contract has one.</summary>
        public static bool TryGetSlot(string name, out BoardSlot slot)
        {
            if (name != null && ByName.TryGetValue(name, out slot)) return true;
            slot = default;
            return false;
        }

        /// <summary>
        /// The slot's sprite, or null when the slot is empty (the caller draws
        /// its procedural surface).
        /// </summary>
        /// <exception cref="ArgumentException">
        /// <paramref name="slot"/> is not in the contract. A misspelt slot is a
        /// code bug, so it fails loudly instead of looking empty forever.
        /// </exception>
        public static Sprite Get(string slot)
        {
            if (!IsSlot(slot))
                throw new ArgumentException($"'{slot}' is not a board sprite slot (see BoardSprites.Slots).", nameof(slot));

            // A cached null means "looked, empty". A cached sprite the editor
            // has since destroyed compares equal to null without being null,
            // and is looked up again.
            if (Cache.TryGetValue(slot, out var cached) && (ReferenceEquals(cached, null) || cached != null))
                return cached;

            var sprite = Resources.Load<Sprite>(Folder + slot);
            if (sprite == null) WarnIfNotASprite(slot);

            Cache[slot] = sprite;
            return sprite;
        }

        /// <summary>True when the slot has art.</summary>
        public static bool Has(string slot) => Get(slot) != null;

        /// <summary>The slots that have art, in the contract's order.</summary>
        public static IEnumerable<string> Filled()
        {
            foreach (var slot in Slots)
                if (Has(slot.Name)) yield return slot.Name;
        }

        /// <summary>
        /// The uniform scale that draws <paramref name="sprite"/> one world unit
        /// across, whatever its pixels-per-unit. Multiply by the slot's size.
        /// </summary>
        public static float UnitScale(Sprite sprite)
        {
            if (sprite == null) return 1f;
            float width = sprite.bounds.size.x;
            return width > 1e-5f ? 1f / width : 1f;
        }

        /// <summary>
        /// Uses <paramref name="sprite"/> for a slot as if it had been found on
        /// disk; null records "empty". For tests and tools.
        /// </summary>
        public static void Register(string slot, Sprite sprite)
        {
            if (!IsSlot(slot))
                throw new ArgumentException($"'{slot}' is not a board sprite slot (see BoardSprites.Slots).", nameof(slot));
            Cache[slot] = sprite;
        }

        /// <summary>Forgets every lookup, so the next one loads again. For tests and tools.</summary>
        public static void ClearCache()
        {
            Cache.Clear();
            Warned.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => ClearCache();

        /// <summary>
        /// A file that is there but not imported as a sprite (dropped in with
        /// its settings changed, or imported before the importer existed)
        /// would otherwise look like an empty slot. Say so, once.
        /// </summary>
        private static void WarnIfNotASprite(string slot)
        {
            if (Warned.Contains(slot)) return;
            if (Resources.Load<Texture2D>(Folder + slot) == null) return;

            Warned.Add(slot);
            Debug.LogWarning($"[BoardSprites] {Folder}{slot} is not imported as a Sprite, so the slot stays procedural. " +
                             "Set Texture Type to Sprite (2D and UI), or delete its .meta so BoardSpriteImporter sets it up.");
        }

        private static Dictionary<string, BoardSlot> Index()
        {
            var index = new Dictionary<string, BoardSlot>(StringComparer.Ordinal);
            foreach (var slot in Slots) index[slot.Name] = slot;
            return index;
        }
    }
}
