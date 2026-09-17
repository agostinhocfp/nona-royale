// Assets/Tests/EditMode/Unity/DisplaySettingsTests.cs
using NonaRoyale.Unity.View;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.View
{
    [TestFixture]
    public class DisplaySettingsTests
    {
        [Test]
        public void Defaults_AreBorderlessFullHdWithVSync()
        {
            var display = new DisplaySettings();

            Assert.AreEqual(ScreenMode.Fullscreen, display.Mode);
            Assert.AreEqual(1920, display.Width);
            Assert.AreEqual(1080, display.Height);
            Assert.IsTrue(display.VSync);
            Assert.AreEqual(60, display.FrameCap);
            Assert.IsTrue(display.IsDefault);
        }

        [Test]
        public void CycleMode_TogglesBetweenFullscreenAndWindowed()
        {
            var display = new DisplaySettings();

            display.CycleMode();
            Assert.AreEqual(ScreenMode.Windowed, display.Mode);

            display.CycleMode();
            Assert.AreEqual(ScreenMode.Fullscreen, display.Mode);
        }

        [Test]
        public void CycleResolution_StepsUp_AndWrapsAround()
        {
            var display = new DisplaySettings();

            display.CycleResolution();
            Assert.AreEqual(2560, display.Width);
            Assert.AreEqual(1440, display.Height);

            display.CycleResolution();
            Assert.AreEqual(3840, display.Width);
            Assert.AreEqual(2160, display.Height);

            display.CycleResolution();
            Assert.AreEqual(1280, display.Width);
            Assert.AreEqual(720, display.Height);
        }

        [Test]
        public void CycleResolution_AnUnknownSizeJumpsToTheDefault()
        {
            var display = new DisplaySettings { Width = 3440, Height = 1440 };

            display.CycleResolution();

            Assert.AreEqual(DisplaySettings.DefaultWidth, display.Width);
            Assert.AreEqual(DisplaySettings.DefaultHeight, display.Height);
        }

        [Test]
        public void CycleFrameCap_StepsThroughTheCaps_AndWraps()
        {
            var display = new DisplaySettings();

            display.CycleFrameCap();
            Assert.AreEqual(120, display.FrameCap);

            display.CycleFrameCap();
            Assert.AreEqual(144, display.FrameCap);

            display.CycleFrameCap();
            Assert.AreEqual(0, display.FrameCap, "uncapped");

            display.CycleFrameCap();
            Assert.AreEqual(30, display.FrameCap);

            display.CycleFrameCap();
            Assert.AreEqual(60, display.FrameCap);
        }

        [Test]
        public void CycleFrameCap_AnUnknownCapJumpsToTheDefault()
        {
            var display = new DisplaySettings { FrameCap = 75 };

            display.CycleFrameCap();

            Assert.AreEqual(DisplaySettings.DefaultFrameCap, display.FrameCap);
        }

        [Test]
        public void Sanitize_RepairsValuesUnityCouldNotApply()
        {
            var display = new DisplaySettings
            {
                Mode = (ScreenMode)7,
                Width = 999,
                Height = 999,
                FrameCap = -3,
            };

            display.Sanitize();

            Assert.AreEqual(DisplaySettings.DefaultMode, display.Mode);
            Assert.AreEqual(DisplaySettings.DefaultWidth, display.Width);
            Assert.AreEqual(DisplaySettings.DefaultHeight, display.Height);
            Assert.AreEqual(DisplaySettings.DefaultFrameCap, display.FrameCap);
            Assert.IsTrue(display.IsDefault);
        }

        [Test]
        public void Sanitize_KeepsLegitimateChoices()
        {
            var display = new DisplaySettings
            {
                Mode = ScreenMode.Windowed,
                Width = 1366,
                Height = 768,
                VSync = false,
                FrameCap = 144,
            };

            display.Sanitize();

            Assert.AreEqual(ScreenMode.Windowed, display.Mode);
            Assert.AreEqual(1366, display.Width);
            Assert.AreEqual(768, display.Height);
            Assert.IsFalse(display.VSync);
            Assert.AreEqual(144, display.FrameCap);
        }

        [Test]
        public void Reset_RestoresEveryDefault()
        {
            var display = new DisplaySettings
            {
                Mode = ScreenMode.Windowed,
                Width = 1280,
                Height = 720,
                VSync = false,
                FrameCap = 30,
            };
            Assert.IsFalse(display.IsDefault);

            display.Reset();

            Assert.IsTrue(display.IsDefault);
            Assert.IsTrue(display.SameAs(new DisplaySettings()));
        }

        [Test]
        public void CopyFrom_CopiesEveryValue()
        {
            var source = new DisplaySettings
            {
                Mode = ScreenMode.Windowed,
                Width = 1600,
                Height = 900,
                VSync = false,
                FrameCap = 0,
            };
            var copy = new DisplaySettings();

            copy.CopyFrom(source);

            Assert.IsTrue(copy.SameAs(source));
            Assert.IsFalse(copy.IsDefault);
        }

        [Test]
        public void Labels_ReadWellOnTheRows()
        {
            var display = new DisplaySettings();

            Assert.AreEqual("FULLSCREEN", display.Summary());
            Assert.AreEqual("1920×1080", display.ResolutionLabel());
            Assert.AreEqual("60", display.FrameCapLabel());

            display.Mode = ScreenMode.Windowed;
            display.FrameCap = 0;
            Assert.AreEqual("WINDOWED", display.Summary());
            Assert.AreEqual("UNCAPPED", display.FrameCapLabel());
        }
    }
}
