// Assets/_Project/Scripts/Unity/View/DisplaySettings.cs
using System;

namespace NonaRoyale.Unity.View
{
    /// <summary>Windowed or borderless fullscreen.</summary>
    public enum ScreenMode
    {
        Windowed = 0,
        Fullscreen = 1
    }

    /// <summary>
    /// How the board is looked at (VISUAL_PASS.md, V1): straight down, or from
    /// a tilted perspective camera.
    /// </summary>
    /// <remarks>
    /// <b>Top-down stays</b>, by the designer's decision, and it is still the
    /// default: the tilt is the newer path, and a player whose machine or taste
    /// disagrees with it has somewhere to go. The tilt fits about half again as
    /// much board into the same screen (<see cref="TiltFraming"/>).
    ///
    /// <b>The tilt is archived, dormant (designer, 2026-10-03).</b> The pieces
    /// are chips lying on the table now, and the tilt was built for figures
    /// standing up out of it. <see cref="DisplaySettings.TiltOffered"/> is off:
    /// every screen frames top-down, the setting row is gone and a saved
    /// "tilted" is ignored. All the tilt code still compiles and runs if the
    /// switch is turned back on. To be settled before launch (VISUAL_PASS.md):
    /// almost certainly top-down only, and the tilt code then removed.
    /// </remarks>
    public enum BoardCamera
    {
        TopDown = 0,
        Tilted = 1
    }

    /// <summary>
    /// How the board is dressed (board skin BS2, BOARD_SKIN.md): Classic, as
    /// built through G10, or Deco, the skin being brought to the visual target.
    /// </summary>
    /// <remarks>
    /// <b>Both stay</b> (designer, 2026-09-27): the Deco skin is built beside
    /// Classic rather than over it, so none of Classic's work is undone while
    /// Deco is judged. Classic is the default until Deco is accepted.
    /// </remarks>
    public enum BoardSkin
    {
        Classic = 0,
        Deco = 1
    }

    /// <summary>
    /// How operators are drawn on the board (2026-09-29): as figures - a
    /// render where one exists, otherwise the look book's procedural figure -
    /// or as casino chips carrying a portrait (<see cref="ChipView"/>). The
    /// rigs that sat between those two went on 2026-10-01.
    /// </summary>
    /// <remarks>
    /// <b>Both stay</b> while the chips are playtested, like the board skin:
    /// the figures are not deleted. Chips are the default, because the chip
    /// is the format under test.
    /// </remarks>
    public enum PieceStyle
    {
        Figures = 0,
        Chips = 1
    }

    /// <summary>
    /// The player's display settings: screen mode, resolution, VSync, a
    /// frame cap (2026-09-17), the board camera (2026-09-18), the board
    /// skin (BS2, 2026-09-27) and the piece style (2026-09-29). Remembered
    /// between sessions.
    /// </summary>
    /// <remarks>
    /// <b>Plain C#.</b> Nothing here touches Unity; the composition root
    /// applies a changed set to <c>Screen</c>, <c>QualitySettings</c> and
    /// <c>Application</c>, which keeps the cycling and the validation
    /// testable outside the editor.
    ///
    /// <b>The resolution is the windowed size.</b> Borderless fullscreen
    /// always renders at the desktop resolution, so the stored size takes
    /// effect in windowed mode and is simply kept while fullscreen.
    ///
    /// <b>The frame cap applies with VSync off.</b> With VSync on the
    /// display paces the frames and the cap is ignored.
    /// </remarks>
    public sealed class DisplaySettings
    {
        public const ScreenMode DefaultMode = ScreenMode.Fullscreen;
        public const int DefaultWidth = 1920;
        public const int DefaultHeight = 1080;
        public const bool DefaultVSync = true;
        public const BoardCamera DefaultCamera = BoardCamera.TopDown;
        public const BoardSkin DefaultSkin = BoardSkin.Classic;
        public const PieceStyle DefaultPieces = PieceStyle.Chips;

        /// <summary>The cap with VSync off. 0 means uncapped.</summary>
        public const int DefaultFrameCap = 60;

        /// <summary>The sizes the resolution row cycles, smallest to largest.</summary>
        public static readonly int[] Widths = { 1280, 1366, 1600, 1920, 2560, 3840 };
        public static readonly int[] Heights = { 720, 768, 900, 1080, 1440, 2160 };

        /// <summary>The caps the frame-cap row cycles. 0 means uncapped.</summary>
        public static readonly int[] FrameCaps = { 30, 60, 120, 144, 0 };

        public ScreenMode Mode { get; set; } = DefaultMode;
        public int Width { get; set; } = DefaultWidth;
        public int Height { get; set; } = DefaultHeight;
        public bool VSync { get; set; } = DefaultVSync;
        public int FrameCap { get; set; } = DefaultFrameCap;

        /// <summary>Straight down, or the tilted view (VISUAL_PASS.md, V1).</summary>
        public BoardCamera Camera { get; set; } = DefaultCamera;

        /// <summary>
        /// Whether the tilted camera is offered at all. Off since 2026-10-03:
        /// archived, dormant. Read-only rather than const, so the code behind
        /// it still compiles without unreachable-code warnings.
        /// </summary>
        public static readonly bool TiltOffered = false;

        /// <summary>The camera the board is actually framed with: <see cref="Camera"/>, or top-down while the tilt is archived.</summary>
        public BoardCamera EffectiveCamera => TiltOffered ? Camera : BoardCamera.TopDown;

        /// <summary>Classic or Deco (BS2).</summary>
        public BoardSkin Skin { get; set; } = DefaultSkin;

        /// <summary>Figures or chips (2026-09-29).</summary>
        public PieceStyle Pieces { get; set; } = DefaultPieces;

        /// <summary>True when every value is the default, so Restore defaults has nothing to do.</summary>
        public bool IsDefault =>
            Mode == DefaultMode && Width == DefaultWidth && Height == DefaultHeight &&
            VSync == DefaultVSync && FrameCap == DefaultFrameCap && Camera == DefaultCamera &&
            Skin == DefaultSkin && Pieces == DefaultPieces;

        /// <summary>Back to the defaults.</summary>
        public void Reset()
        {
            Mode = DefaultMode;
            Width = DefaultWidth;
            Height = DefaultHeight;
            VSync = DefaultVSync;
            FrameCap = DefaultFrameCap;
            Camera = DefaultCamera;
            Skin = DefaultSkin;
            Pieces = DefaultPieces;
        }

        /// <summary>Copies every value from <paramref name="other"/>.</summary>
        public void CopyFrom(DisplaySettings other)
        {
            Mode = other.Mode;
            Width = other.Width;
            Height = other.Height;
            VSync = other.VSync;
            FrameCap = other.FrameCap;
            Camera = other.Camera;
            Skin = other.Skin;
            Pieces = other.Pieces;
        }

        public bool SameAs(DisplaySettings other) =>
            Mode == other.Mode && Width == other.Width && Height == other.Height &&
            VSync == other.VSync && FrameCap == other.FrameCap && Camera == other.Camera &&
            Skin == other.Skin && Pieces == other.Pieces;

        /// <summary>Cycles Windowed and Fullscreen.</summary>
        public void CycleMode() =>
            Mode = Mode == ScreenMode.Fullscreen ? ScreenMode.Windowed : ScreenMode.Fullscreen;

        /// <summary>Steps to the next larger resolution, wrapping. An unknown size jumps to the default.</summary>
        public void CycleResolution()
        {
            int index = IndexOf(Width, Height);
            if (index < 0)
            {
                Width = DefaultWidth;
                Height = DefaultHeight;
                return;
            }

            index = (index + 1) % Widths.Length;
            Width = Widths[index];
            Height = Heights[index];
        }

        /// <summary>Steps to the next cap, wrapping. An unknown cap jumps to the default.</summary>
        public void CycleFrameCap()
        {
            int index = Array.IndexOf(FrameCaps, FrameCap);
            FrameCap = index < 0 ? DefaultFrameCap : FrameCaps[(index + 1) % FrameCaps.Length];
        }

        /// <summary>Cycles the two board cameras.</summary>
        public void CycleCamera() =>
            Camera = Camera == BoardCamera.Tilted ? BoardCamera.TopDown : BoardCamera.Tilted;

        /// <summary>Cycles the two board skins (BS2).</summary>
        public void CycleSkin() =>
            Skin = Skin == BoardSkin.Deco ? BoardSkin.Classic : BoardSkin.Deco;

        /// <summary>Cycles the two piece styles (2026-09-29).</summary>
        public void CyclePieces() =>
            Pieces = Pieces == PieceStyle.Chips ? PieceStyle.Figures : PieceStyle.Chips;

        /// <summary>Repairs values Unity could not apply (a stray PlayerPrefs edit).</summary>
        public void Sanitize()
        {
            if (Mode != ScreenMode.Windowed && Mode != ScreenMode.Fullscreen) Mode = DefaultMode;
            if (IndexOf(Width, Height) < 0)
            {
                Width = DefaultWidth;
                Height = DefaultHeight;
            }

            if (Array.IndexOf(FrameCaps, FrameCap) < 0) FrameCap = DefaultFrameCap;
            if (Camera != BoardCamera.TopDown && Camera != BoardCamera.Tilted) Camera = DefaultCamera;
            if (Skin != BoardSkin.Classic && Skin != BoardSkin.Deco) Skin = DefaultSkin;
            if (Pieces != PieceStyle.Figures && Pieces != PieceStyle.Chips) Pieces = DefaultPieces;
        }

        /// <summary>"FULLSCREEN" or "WINDOWED", the settings row's summary.</summary>
        public string Summary() => Mode == ScreenMode.Fullscreen ? "FULLSCREEN" : "WINDOWED";

        /// <summary>"1920×1080", the resolution row's value.</summary>
        public string ResolutionLabel() => $"{Width}×{Height}";

        /// <summary>"60", or "UNCAPPED" for 0.</summary>
        public string FrameCapLabel() => FrameCap <= 0 ? "UNCAPPED" : FrameCap.ToString();

        /// <summary>"TILTED" or "TOP-DOWN", the board camera row's value.</summary>
        public string CameraLabel() => Camera == BoardCamera.Tilted ? "TILTED" : "TOP-DOWN";

        /// <summary>"CLASSIC" or "DECO", the board skin row's value.</summary>
        public string SkinLabel() => Skin == BoardSkin.Deco ? "DECO" : "CLASSIC";

        /// <summary>"CHIPS" or "FIGURES", the pieces row's value.</summary>
        public string PiecesLabel() => Pieces == PieceStyle.Chips ? "CHIPS" : "FIGURES";

        private static int IndexOf(int width, int height)
        {
            for (int i = 0; i < Widths.Length; i++)
                if (Widths[i] == width && Heights[i] == height) return i;
            return -1;
        }
    }
}
