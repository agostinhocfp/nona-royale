// Assets/_Project/Scripts/Editor/LookBookWindow.cs
using System.Collections.Generic;
using System.IO;
using NonaRoyale.Unity.View;
using UnityEditor;
using UnityEngine;

namespace NonaRoyale.EditorTools
{
    /// <summary>
    /// The look book's judging tools (OPERATOR_LOOKBOOK.md, LB3): every recipe
    /// on the surfaces it will stand on, at the size it will actually be, with
    /// the squint sheet and the ledger's rules beside it.
    /// </summary>
    /// <remarks>
    /// <b>Window → Nona Royale → Look Book.</b> No Play Mode, no scene: the
    /// recipes are plain C#, so this renders them directly. Reopen or press
    /// Rebuild after editing a recipe; Unity recompiles first anyway.
    ///
    /// <b>Drawn pixel for pixel.</b> The sheets are composed at the figure's
    /// real on-screen height (the LB0 screenshots put it near 90 px on a phone
    /// held upright and 64 px on a 1440p desktop) and shown unscaled, so the
    /// window judges what a player sees rather than the 280-texel render.
    ///
    /// <b>Export</b> writes the sheets to <c>Logs/LookBook/</c>, which git
    /// ignores, for attaching to a review.
    ///
    /// The backdrops are flat colours from <c>UiTheme</c>: the floor, the carpet
    /// as the table tints it, lit gold inlay, and two felts. The painted carpet
    /// and the board's lights are Play Mode's job (§5.1: check the board under
    /// the pieces).
    /// </remarks>
    public sealed class LookBookWindow : EditorWindow
    {
        private const float AmberOverlap = 0.75f;
        private const float RedOverlap = 0.85f;

        private static readonly int[] Heights = { 90, 64, 200 };
        private static readonly string[] HeightNames = { "Phone upright (≈90 px)", "Desktop 1440p (≈64 px)", "Inspect (200 px)" };

        private sealed class Row
        {
            public OperatorLook Look;
            public FigureImage Standing, Seated, Powered;
            public int Cyan, Gilt;
            public bool Fits;
        }

        private readonly List<Row> _rows = new List<Row>();

        // ── The rig (LB5) ───────────────────────────────────────────────

        private sealed class RigEntry
        {
            public OperatorRig Right, Left;
            public List<RigPartImage> PartsRight, PartsLeft, Powered;
            public FigureImage RestRight, RestLeft;
            public Texture2D Strip, Preview;
            public float FootDrift;
            public List<string> Seams;
            public int Cyan;
        }

        private readonly List<RigEntry> _rigs = new List<RigEntry>();
        private int _clipIndex;
        private bool _playing = true;
        private bool _facingLeft;
        private double _clipStart;
        private double _lastFrame;
        private Backdrop[] _backdrops;
        private Texture2D _contact, _squint;
        private SheetImage _contactSheet, _squintSheet;
        private float[,] _overlap;
        private int _heightIndex;
        private bool _showPowered;
        private double _buildMs;
        private Vector2 _scroll;

        [MenuItem("Window/Nona Royale/Look Book")]
        private static void Open() => GetWindow<LookBookWindow>("Look Book");

        private void OnEnable()
        {
            Rebuild();
            _clipStart = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            FreeTextures();
            FreeRigTextures();
        }

        /// <summary>About fifteen frames a second of preview, only while playing.</summary>
        private void Tick()
        {
            if (!_playing || _rigs.Count == 0) return;

            double now = EditorApplication.timeSinceStartup;
            if (now - _lastFrame < 1.0 / 15.0) return;
            _lastFrame = now;

            ComposePreviews((float)(now - _clipStart));
            Repaint();
        }

        private void Rebuild()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var palette = OperatorLookBook.Palette;
            var canvas = OperatorLookBook.Canvas;

            _rows.Clear();
            foreach (var look in LookRoster.All)
            {
                var row = new Row
                {
                    Look = look,
                    Standing = FigureRasterizer.Render(look.Standing(palette), canvas),
                    Seated = FigureRasterizer.Render(look.Seated(palette), canvas),
                    Powered = FigureRasterizer.Render(look.Standing(palette), canvas, powered: true),
                };

                row.Cyan = SilhouetteMetrics.Count(row.Standing, SilhouetteMetrics.IsCyan)
                           + SilhouetteMetrics.Count(row.Seated, SilhouetteMetrics.IsCyan);
                row.Gilt = SilhouetteMetrics.Count(row.Standing, SilhouetteMetrics.IsGilt);
                row.Fits = Fits(row.Standing);
                _rows.Add(row);
            }

            var silhouettes = new List<Silhouette>();
            foreach (var row in _rows) silhouettes.Add(SilhouetteMetrics.At(row.Standing, SilhouetteMetrics.SquintHeight));

            _overlap = new float[_rows.Count, _rows.Count];
            for (int i = 0; i < _rows.Count; i++)
                for (int j = 0; j < _rows.Count; j++)
                    _overlap[i, j] = i == j ? 1f : SilhouetteMetrics.Overlap(silhouettes[i], silhouettes[j]);

            _backdrops = new[]
            {
                new Backdrop("Floor", OperatorLookBook.F(UiTheme.CrossFloor)),
                new Backdrop("Carpet", OperatorLookBook.F(UiTheme.BloodVelvet * UiTheme.CarpetTint)),
                new Backdrop("Gold inlay", OperatorLookBook.F(UiTheme.Gold)),
                new Backdrop("Red felt", OperatorLookBook.F(UiTheme.SeatRed * 0.45f)),
                new Backdrop("Blue felt", OperatorLookBook.F(UiTheme.SeatBlue * 0.45f)),
            };

            ComposeSheets();
            BuildRigs(palette);
            _buildMs = clock.Elapsed.TotalMilliseconds;
        }

        private void ComposeSheets()
        {
            FreeTextures();

            var rows = new List<FigureImage[]>();
            var standing = new List<FigureImage>();
            foreach (var row in _rows)
            {
                rows.Add(_showPowered ? new[] { row.Standing, row.Seated, row.Powered } : new[] { row.Standing, row.Seated });
                standing.Add(row.Standing);
            }

            _contactSheet = LookSheet.Contact(rows, _backdrops, Heights[_heightIndex]);
            _squintSheet = LookSheet.Squint(standing);
            _contact = ToTexture(_contactSheet, "LookBook_Contact");
            _squint = ToTexture(_squintSheet, "LookBook_Squint");
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Rebuild", EditorStyles.toolbarButton, GUILayout.Width(60))) Rebuild();

                EditorGUI.BeginChangeCheck();
                _heightIndex = EditorGUILayout.Popup(_heightIndex, HeightNames, EditorStyles.toolbarPopup, GUILayout.Width(170));
                _showPowered = GUILayout.Toggle(_showPowered, "Show cast tell", EditorStyles.toolbarButton, GUILayout.Width(100));
                if (EditorGUI.EndChangeCheck())
                {
                    ComposeSheets();
                    ComposeRigStrips();
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label($"{_rows.Count} recipes · {_buildMs:0} ms", EditorStyles.miniLabel);
                if (GUILayout.Button("Export PNGs", EditorStyles.toolbarButton, GUILayout.Width(90))) Export();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            Heading("Contact sheet", "Standing and seated" + (_showPowered ? ", then the cast tell" : "") +
                                     ", on each surface, at " + HeightNames[_heightIndex] + ".");
            DrawContact();

            Heading("Squint sheet", "Pure black at 64 px (§2.2). If two could be the same shape, a recipe is wrong.");
            DrawPixelTrue(_squint);

            Heading("Silhouette overlap", $"Intersection over union at 64 px. Amber at {AmberOverlap:0.00}, a test failure above {RedOverlap:0.00}.");
            DrawOverlap();

            Heading("Ledger rules", "No cyan at rest (§5). Gilt is Fortuna's alone (§3). Nothing clipped by the canvas.");
            DrawRules();

            Heading("Rig (LB5)", "Three-quarter figures on the rig, assembled the way the board will assemble them. " +
                                 "The strip: " + string.Join(", ", RigPoseNames.Strip) + ", then the cast with its tell. Not on the board until LB5b.");
            DrawRigs();

            EditorGUILayout.EndScrollView();
        }

        private void DrawContact()
        {
            if (_contact == null || _rows.Count == 0) return;

            float ppp = EditorGUIUtility.pixelsPerPoint;
            float cellWidth = _contact.width / (float)_backdrops.Length / ppp;
            float rowHeight = _contact.height / (float)_rows.Count / ppp;
            const float nameWidth = 70f;

            var header = GUILayoutUtility.GetRect(nameWidth + _contact.width / ppp, 16f);
            for (int c = 0; c < _backdrops.Length; c++)
                GUI.Label(new Rect(header.x + nameWidth + c * cellWidth + 4f, header.y, cellWidth, 16f), _backdrops[c].Name, EditorStyles.miniLabel);

            var area = GUILayoutUtility.GetRect(nameWidth + _contact.width / ppp, _contact.height / ppp);
            for (int r = 0; r < _rows.Count; r++)
                GUI.Label(new Rect(area.x, area.y + r * rowHeight, nameWidth, rowHeight), _rows[r].Look.Name, EditorStyles.boldLabel);

            GUI.DrawTexture(new Rect(area.x + nameWidth, area.y, _contact.width / ppp, _contact.height / ppp), _contact);
        }

        private void DrawOverlap()
        {
            const float cell = 64f;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(cell);
                foreach (var row in _rows) GUILayout.Label(row.Look.Name, EditorStyles.miniBoldLabel, GUILayout.Width(cell));
            }

            var normal = EditorStyles.label.normal.textColor;
            for (int i = 0; i < _rows.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(_rows[i].Look.Name, EditorStyles.miniBoldLabel, GUILayout.Width(cell));
                    for (int j = 0; j < _rows.Count; j++)
                    {
                        float v = _overlap[i, j];
                        var style = new GUIStyle(EditorStyles.label);
                        style.normal.textColor = i == j ? Color.gray
                            : v > RedOverlap ? new Color(1f, 0.35f, 0.3f)
                            : v >= AmberOverlap ? new Color(1f, 0.72f, 0.2f)
                            : normal;
                        GUILayout.Label(i == j ? "—" : v.ToString("0.00"), style, GUILayout.Width(cell));
                    }
                }
            }
        }

        private void DrawRules()
        {
            foreach (var row in _rows)
            {
                bool giltOk = row.Look.Name == "Fortuna" ? row.Gilt > 0 : row.Gilt == 0;
                string line = $"{Mark(row.Cyan == 0)} no cyan at rest    {Mark(giltOk)} gilt rule    {Mark(row.Fits)} fits the canvas";

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(row.Look.Name, EditorStyles.boldLabel, GUILayout.Width(70));
                    GUILayout.Label(line);
                }
            }
        }

        private static string Mark(bool ok) => ok ? "✓" : "✗";

        private static void Heading(string title, string note)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(note, EditorStyles.wordWrappedMiniLabel);
        }

        private static void DrawPixelTrue(Texture2D texture)
        {
            if (texture == null) return;
            float ppp = EditorGUIUtility.pixelsPerPoint;
            var rect = GUILayoutUtility.GetRect(texture.width / ppp, texture.height / ppp, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, texture);
        }

        private void Export()
        {
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "LookBook");
            Directory.CreateDirectory(folder);

            string contact = Path.Combine(folder, $"contact_{Heights[_heightIndex]}px{(_showPowered ? "_tell" : "")}.png");
            File.WriteAllBytes(contact, _contact.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(folder, "squint_64px.png"), _squint.EncodeToPNG());

            Debug.Log($"[LookBook] Exported to {folder}");
            EditorUtility.RevealInFinder(contact);
        }

        private static bool Fits(FigureImage image)
        {
            if (image.IsEmpty || image.OpaqueTopRow >= image.Height - 1) return false;
            for (int y = 0; y < image.Height; y++)
                if (image.Alpha(0, y) != 0 || image.Alpha(image.Width - 1, y) != 0) return false;

            return true;
        }

        private static Texture2D ToTexture(SheetImage sheet, string name)
        {
            // Point-filtered and unscaled: the sheet is already at screen size.
            var texture = new Texture2D(sheet.Width, sheet.Height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixelData(sheet.Pixels, 0);
            texture.Apply(false, false);
            return texture;
        }

        private void BuildRigs(LookBookPalette palette)
        {
            FreeRigTextures();
            _rigs.Clear();

            foreach (var rig in RigRoster.All)
            {
                var entry = new RigEntry { Right = rig, Left = rig.Mirrored() };
                entry.PartsRight = RigComposer.RenderParts(entry.Right, palette, 96f, 4);
                entry.PartsLeft = RigComposer.RenderParts(entry.Left, palette, 96f, 4);
                entry.Powered = RigComposer.RenderParts(entry.Right, palette, 96f, 4, powered: true);
                entry.RestRight = RigComposer.Compose(entry.Right, entry.PartsRight, entry.Right.Pose(RigPoseNames.Rest), RigComposer.PoseCanvas);
                entry.RestLeft = RigComposer.Compose(entry.Left, entry.PartsLeft, entry.Left.Pose(RigPoseNames.Rest), RigComposer.PoseCanvas);
                entry.FootDrift = RigChecks.FootDrift(rig);
                entry.Seams = RigChecks.Seams(rig, entry.PartsRight, RigComposer.PoseCanvas);
                entry.Cyan = RigChecks.CyanAtRest(rig, entry.PartsRight, RigComposer.PoseCanvas);
                _rigs.Add(entry);
            }

            ComposeRigStrips();
            ComposePreviews(0f);
        }

        private void ComposeRigStrips()
        {
            var floor = new[] { _backdrops[0] };
            foreach (var entry in _rigs)
            {
                var poses = new List<FigureImage>();
                foreach (var name in RigPoseNames.Strip)
                    poses.Add(RigComposer.Compose(entry.Right, entry.PartsRight, entry.Right.Pose(name), RigComposer.PoseCanvas));

                poses.Add(RigComposer.Compose(entry.Right, entry.Powered, entry.Right.Pose(RigPoseNames.Cast), RigComposer.PoseCanvas));
                poses.Add(entry.RestLeft);

                if (entry.Strip != null) DestroyImmediate(entry.Strip);
                entry.Strip = ToTexture(LookSheet.Contact(new List<FigureImage[]> { poses.ToArray() }, floor, Heights[_heightIndex]), "LookBook_RigStrip");
            }
        }

        private void ComposePreviews(float seconds)
        {
            if (_backdrops == null) return;
            var floor = new[] { _backdrops[0] };
            string clip = RigClips.All[_clipIndex];

            foreach (var entry in _rigs)
            {
                var rig = _facingLeft ? entry.Left : entry.Right;
                var parts = _facingLeft ? entry.PartsLeft : entry.PartsRight;
                var image = RigComposer.Compose(rig, parts, RigClips.Sample(rig, clip, seconds), RigComposer.PoseCanvas);

                // Scaled against the rest pose, so a seated or knocked-down
                // figure shrinks with its pose instead of refilling the frame.
                var rest = _facingLeft ? entry.RestLeft : entry.RestRight;
                var sheet = LookSheet.Contact(new List<FigureImage[]> { new[] { rest, image } }, floor, Heights[_heightIndex]);

                if (entry.Preview != null) DestroyImmediate(entry.Preview);
                entry.Preview = ToTexture(sheet, "LookBook_RigPreview");
            }
        }

        private void DrawRigs()
        {
            if (_rigs.Count == 0) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                _clipIndex = EditorGUILayout.Popup(_clipIndex, RigClips.All, GUILayout.Width(90));
                _facingLeft = GUILayout.Toggle(_facingLeft, "Face left", GUILayout.Width(80));
                if (EditorGUI.EndChangeCheck())
                {
                    _clipStart = EditorApplication.timeSinceStartup;
                    ComposePreviews(0f);
                }

                _playing = GUILayout.Toggle(_playing, "Play", GUILayout.Width(50));
                GUILayout.FlexibleSpace();
            }

            foreach (var entry in _rigs)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(entry.Right.Name, EditorStyles.boldLabel);

                string seams = entry.Seams.Count == 0 ? "none" : string.Join(", ", entry.Seams);
                EditorGUILayout.LabelField(
                    $"{Mark(entry.FootDrift <= RigChecks.FootTolerance)} feet planted ({entry.FootDrift:0.000})    " +
                    $"{Mark(entry.Seams.Count == 0)} seams: {seams}    {Mark(entry.Cyan == 0)} no cyan at rest",
                    EditorStyles.wordWrappedLabel);

                DrawPixelTrue(entry.Strip);
                EditorGUILayout.LabelField("Rest, then the clip:", EditorStyles.miniLabel);
                DrawPixelTrue(entry.Preview);
            }
        }

        private void FreeRigTextures()
        {
            foreach (var entry in _rigs)
            {
                if (entry.Strip != null) DestroyImmediate(entry.Strip);
                if (entry.Preview != null) DestroyImmediate(entry.Preview);
                entry.Strip = null;
                entry.Preview = null;
            }
        }

        private void FreeTextures()
        {
            if (_contact != null) DestroyImmediate(_contact);
            if (_squint != null) DestroyImmediate(_squint);
            _contact = null;
            _squint = null;
        }
    }
}
