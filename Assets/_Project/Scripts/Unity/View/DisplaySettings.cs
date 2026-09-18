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
    /// </remarks>
    public enum BoardCamera
    {
        TopDown = 0,
        Tilted = 1
    }

    /// <summary>
    /// The player's display settings: screen mode, resolution, VSync, a
    /// frame cap (2026-09-17) and the board camera (2026-09-18). Remembered
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

        /// <summary>True when every value is the default, so Restore defaults has nothing to do.</summary>
        public bool IsDefault =>
            Mode == DefaultMode && Width == DefaultWidth && Height == DefaultHeight &&
            VSync == DefaultVSync && FrameCap == DefaultFrameCap && Camera == DefaultCamera;

        /// <summary>Back to the defaults.</summary>
        public void Reset()
        {
            Mode = DefaultMode;
            Width = DefaultWidth;
            Height = DefaultHeight;
            VSync = DefaultVSync;
            FrameCap = DefaultFrameCap;
            Camera = DefaultCamera;
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
        }

        public bool SameAs(DisplaySettings other) =>
            Mode == other.Mode && Width == other.Width && Height == other.Height &&
            VSync == other.VSync && FrameCap == other.FrameCap && Camera == other.Camera;

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
        }

        /// <summary>"FULLSCREEN" or "WINDOWED", the settings row's summary.</summary>
        public string Summary() => Mode == ScreenMode.Fullscreen ? "FULLSCREEN" : "WINDOWED";

        /// <summary>"1920×1080", the resolution row's value.</summary>
        public string ResolutionLabel() => $"{Width}×{Height}";

        /// <summary>"60", or "UNCAPPED" for 0.</summary>
        public string FrameCapLabel() => FrameCap <= 0 ? "UNCAPPED" : FrameCap.ToString();

        /// <summary>"TILTED" or "TOP-DOWN", the board camera row's value.</summary>
        public string CameraLabel() => Camera == BoardCamera.Tilted ? "TILTED" : "TOP-DOWN";

        private static int IndexOf(int width, int height)
        {
            for (int i = 0; i < Widths.Length; i++)
                if (Widths[i] == width && Heights[i] == height) return i;
            return -1;
        }
    }
}
