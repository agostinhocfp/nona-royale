// Assets/Tests/EditMode/Unity/LookBookRulesTests.cs
using System.Collections.Generic;
using NonaRoyale.Unity.View;
using NUnit.Framework;

namespace NonaRoyale.Unity.Tests.View
{
    /// <summary>
    /// The value ledger's rules, enforced on every recipe (OPERATOR_LOOKBOOK.md,
    /// LB3; ART_DIRECTION §3, §5, §5.1). A new recipe in <see cref="LookRoster"/>
    /// is tested by these without a line added here.
    /// </summary>
    /// <remarks>
    /// Rendered with the real palette from <c>UiTheme</c>, once per fixture:
    /// the rules are about the colours that ship, not a copy of them.
    /// </remarks>
    [TestFixture]
    public class LookBookRulesTests
    {
        /// <summary>
        /// Two 64 px silhouettes overlapping more than this are one shape to a
        /// squinting eye. Wide figures legitimately sit near 0.75 (Bouncer
        /// against Nuetu, 2026-09-21); the judging window flags that band
        /// in amber so it gets looked at before it gets close.
        /// </summary>
        private const float MaxOverlap = 0.85f;

        private readonly Dictionary<string, FigureImage> _standing = new Dictionary<string, FigureImage>();
        private readonly Dictionary<string, FigureImage> _seated = new Dictionary<string, FigureImage>();
        private readonly Dictionary<string, FigureImage> _powered = new Dictionary<string, FigureImage>();

        [OneTimeSetUp]
        public void RenderAll()
        {
            var palette = OperatorLookBook.Palette;
            foreach (var look in LookRoster.All)
            {
                _standing[look.Name] = FigureRasterizer.Render(look.Standing(palette), OperatorLookBook.Canvas);
                _seated[look.Name] = FigureRasterizer.Render(look.Seated(palette), OperatorLookBook.Canvas);
                _powered[look.Name] = FigureRasterizer.Render(look.Standing(palette), OperatorLookBook.Canvas, powered: true);
            }
        }

        private static IEnumerable<string> Names()
        {
            foreach (var look in LookRoster.All) yield return look.Name;
        }

        [Test]
        public void EveryRecipe_HasADistinctKey_ThatMatchesItsName()
        {
            var keys = new HashSet<string>();
            foreach (var look in LookRoster.All)
            {
                Assert.IsTrue(keys.Add(look.Key), $"{look.Name}: key '{look.Key}' is used twice");
                Assert.AreSame(look, LookRoster.Find(look.Name));
            }
        }

        [Test]
        public void EveryPaletteColour_IsFilledFromUiTheme()
        {
            // An unset FigureColour is transparent black, which draws as a
            // hole in the figure rather than failing loudly.
            var palette = OperatorLookBook.Palette;
            foreach (var field in typeof(LookBookPalette).GetFields())
            {
                var colour = (FigureColour)field.GetValue(palette);
                Assert.Greater(colour.A, 0f, $"LookBookPalette.{field.Name} is never set in OperatorLookBook");
            }
        }

        [TestCaseSource(nameof(Names))]
        public void NoCyanAtRest_StandingOrSeated(string name)
        {
            Assert.AreEqual(0, SilhouetteMetrics.Count(_standing[name], SilhouetteMetrics.IsCyan), "standing");
            Assert.AreEqual(0, SilhouetteMetrics.Count(_seated[name], SilhouetteMetrics.IsCyan), "seated");
        }

        [TestCaseSource(nameof(Names))]
        public void TheTell_BringsTheCyan(string name)
        {
            Assert.Greater(SilhouetteMetrics.Count(_powered[name], SilhouetteMetrics.IsCyan), 0,
                "a recipe needs a Powered layer, or LB4's cast tell has nowhere to go");
        }

        [TestCaseSource(nameof(Names))]
        public void OnlyFortuna_CarriesGilt(string name)
        {
            int gilt = SilhouetteMetrics.Count(_standing[name], SilhouetteMetrics.IsGilt);
            if (name == "Fortuna") Assert.Greater(gilt, 0, "gilt is Fortuna's whole distinction");
            else Assert.AreEqual(0, gilt, "operator metal is aged brass (§3)");
        }

        [TestCaseSource(nameof(Names))]
        public void Stands_OnItsFeet_AndFitsTheCanvas(string name)
        {
            var image = _standing[name];

            Assert.IsFalse(image.IsEmpty);
            Assert.LessOrEqual(image.OpaqueBottomRow, 3, "the feet sit at the canvas's figure origin");
            Assert.Less(image.OpaqueTopRow, image.Height - 1, "clipped at the top");

            for (int y = 0; y < image.Height; y++)
            {
                Assert.AreEqual(0, image.Alpha(0, y), "clipped at the left");
                Assert.AreEqual(0, image.Alpha(image.Width - 1, y), "clipped at the right");
            }
        }

        [TestCaseSource(nameof(Names))]
        public void TheSeatedCut_KeepsTheHead_AndDropsTheLegs(string name)
        {
            var standing = _standing[name];
            var seated = _seated[name];

            Assert.AreEqual(standing.OpaqueTopRow, seated.OpaqueTopRow, 2);
            Assert.Greater(seated.OpaqueBottomRow, standing.OpaqueBottomRow + 40);
        }

        [Test]
        public void NoTwoSilhouettes_AreTheSameShapeAtSquintScale()
        {
            var looks = LookRoster.All;
            var failures = new List<string>();

            for (int i = 0; i < looks.Count; i++)
            {
                var a = SilhouetteMetrics.At(_standing[looks[i].Name], SilhouetteMetrics.SquintHeight);

                for (int j = i + 1; j < looks.Count; j++)
                {
                    var b = SilhouetteMetrics.At(_standing[looks[j].Name], SilhouetteMetrics.SquintHeight);
                    float overlap = SilhouetteMetrics.Overlap(a, b);
                    if (overlap > MaxOverlap) failures.Add($"{looks[i].Name}/{looks[j].Name} {overlap:0.00}");
                }
            }

            Assert.IsEmpty(failures, "confusable at 64 px (§2.2 squint test): " + string.Join(", ", failures));
        }

        [Test]
        public void Overlap_IsOne_ForTheSameShape_AndLowerForDifferentOnes()
        {
            var a = SilhouetteMetrics.At(_standing["Bouncer"], SilhouetteMetrics.SquintHeight);
            var b = SilhouetteMetrics.At(_standing["Mimi"], SilhouetteMetrics.SquintHeight);

            Assert.AreEqual(1f, SilhouetteMetrics.Overlap(a, a), 1e-6f);
            Assert.Less(SilhouetteMetrics.Overlap(a, b), 0.6f);
        }
    }
}
