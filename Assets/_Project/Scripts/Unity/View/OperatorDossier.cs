// Assets/_Project/Scripts/Unity/View/OperatorDossier.cs
using System;
using NonaRoyale.Core.Abilities;
using NonaRoyale.Core.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NonaRoyale.Unity.View
{
    /// <summary>
    /// One operator, written out (OPERATOR_GUIDE.md §1, §4): who it is, what
    /// it carries, and what every ability does — the generated rules line with
    /// the flavour under it.
    /// </summary>
    /// <remarks>
    /// <b>One builder, three screens.</b> The title's OPERATORS page, the
    /// draft's detail panel and the draft's full dossier all call this, so an
    /// operator reads the same wherever it is met. The draft uses the compact
    /// form, which drops the flavour lines and the section headings to fit
    /// beside the pool.
    ///
    /// <b>Built once per operator shown, not per frame.</b> The callers
    /// rebuild it when the selection changes, never on a dirty flag that
    /// fires every frame — that pattern is what made every button flash
    /// white (GUI_PHASE, 2026-09-21).
    ///
    /// <b>No numbers are written here.</b> Health and speed are read off the
    /// definition; everything an ability does comes from
    /// <see cref="RulesText"/> (D2).
    /// </remarks>
    public static class OperatorDossier
    {
        /// <summary>
        /// Fills <paramref name="column"/> — a rect with a vertical layout that
        /// controls its children's heights — with the operator.
        /// </summary>
        /// <param name="onKeyword">Where a tapped keyword goes. Null leaves the keywords coloured but not linked.</param>
        /// <param name="compact">The draft's form: no flavour, no section headings, no header block.</param>
        public static void Build(RectTransform column, OperatorDefinition op, Action<string> onKeyword, bool compact = false)
        {
            if (column == null || op == null) return;

            var copy = GuideCopy.For(op.Name);

            if (!compact)
            {
                Header(column, op, copy);

                if (copy != null)
                {
                    var tagline = UiKit.Label(column, copy.Tagline, UiTheme.FontBody, UiTheme.TextNote, wrap: true);
                    tagline.fontStyle = FontStyles.Italic;
                }

                Divider(column);
            }

            var passives = RulesText.Passives(op);
            if (passives.Count > 0 || op.Aura != null)
            {
                if (!compact)
                    Section(column, passives.Count == 0 ? "Aura" : op.Aura != null ? "Passive and aura" : "Passive");

                foreach (var passive in passives)
                    Entry(column, passive.Name, "passive", passive.Line, null, onKeyword, compact);

                if (op.Aura != null)
                    Entry(column, op.Aura.Name, $"aura · r{op.Aura.Radius}", RulesText.ForAura(op.Aura), null, onKeyword, compact);
            }

            if (!compact) Section(column, "Abilities");

            foreach (var ability in op.Abilities)
                Entry(column, ability.Name, Meta(ability), RulesText.For(ability),
                    compact ? null : ability.Description, onKeyword, compact);

            // The half only design knowledge can write (OG5): when to use the
            // kit, and what beats it. Number-free by rule (D2).
            if (compact || copy == null) return;

            Section(column, "How to play");
            UiKit.Label(column, copy.HowToPlay, UiTheme.FontBody, UiTheme.TextNote, wrap: true);
            Section(column, "How to beat");
            UiKit.Label(column, copy.HowToBeat, UiTheme.FontBody, UiTheme.TextNote, wrap: true);
            UiKit.Space(column, height: 12f);
        }

        /// <summary>Cost, reach and cooldown, as the draft and the tray word them.</summary>
        public static string Meta(AbilityDefinition ability)
        {
            string reach = ability.HasUnlimitedRange ? "any cell"
                : ability.Targeting == AbilityTargeting.Cell ? $"cell r{ability.Range}"
                : ability.Range == 0 ? "self"
                : $"r{ability.Range}";
            string cooldown = ability.CooldownTurns > 0 ? $" · cd {ability.CooldownTurns}" : "";

            return $"<color=#{UiTheme.Hex(UiTheme.Cyan)}>{ability.EnergyCost}e</color> · {reach}{cooldown}";
        }

        /// <summary>
        /// The operator's picture: its rendered portrait where one exists, its
        /// board shape otherwise (D6). Nothing waits on art.
        /// </summary>
        public static void Icon(RectTransform box, OperatorDefinition op, float size, Color tint)
        {
            // A shape is scaled by health, as on the board and the draft card,
            // so a tank reads bigger than an assassin at a glance.
            var portrait = OperatorArtLibrary.Portrait(op.Name);
            float drawn = portrait != null
                ? size
                : size * Mathf.Lerp(0.7f, 1f, Mathf.InverseLerp(0.52f, 0.84f, PieceShape.SizeFor(op.MaxHealth)));
            var image = UiKit.Icon(box, portrait != null ? portrait : PieceShape.For(op.Name),
                portrait != null ? Color.white : tint, drawn);

            // The box has no layout group, so the icon's layout size is never
            // applied: its rect has to be sized here, or it keeps a new
            // RectTransform's 100 × 100 and swamps the row (2026-09-21).
            var rect = (RectTransform)image.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(drawn, drawn);
        }

        // ── Pieces ───────────────────────────────────────────────────────

        private static void Header(RectTransform column, OperatorDefinition op, GuideCopyEntry copy)
        {
            var head = UiKit.Rect("header", column);
            UiKit.Size(head, height: 84f);
            var row = UiKit.Row(head, 16f);
            row.childForceExpandHeight = true;

            var iconBox = UiKit.Rect("icon", head);
            UiKit.Fixed(iconBox, 80f, 80f);
            Icon(iconBox, op, 76f, UiTheme.GoldBright);

            var names = UiKit.Rect("names", head);
            UiKit.Size(names, flexibleWidth: 1f);
            var nameColumn = UiKit.Column(names, 2f);
            nameColumn.childAlignment = TextAnchor.MiddleLeft;

            var name = UiKit.Label(names, op.Name.ToUpperInvariant(), 30f, UiTheme.GoldBright, bold: true);
            UiFonts.ApplyDisplay(name);
            name.characterSpacing = 6f;

            // The display face has no ellipsis glyph, so a label that ellipsizes
            // draws nothing at all — the wordmark and the draft clock set this
            // for the same reason.
            name.overflowMode = TextOverflowModes.Overflow;
            UiKit.Size(name, height: 38f);

            string roleLine = OperatorCopy.Role(op.Name).ToUpperInvariant() +
                (copy != null ? "  ·  " + GuideCopy.CampName(copy.Camp).ToUpperInvariant() : "");
            var role = UiKit.Label(names, roleLine, 13f, UiTheme.Heading, bold: true);
            role.characterSpacing = UiTheme.HeadingSpacing;
            UiKit.Size(role, height: 18f);

            var numbers = UiKit.Label(names,
                $"<color=#{UiTheme.Hex(UiTheme.TextDim)}>HEALTH</color> <b>{op.MaxHealth}</b>     " +
                $"<color=#{UiTheme.Hex(UiTheme.TextDim)}>SPEED</color> <b>×{op.BaseSpeed:0.0}</b>",
                UiTheme.FontSmall);
            UiKit.Size(numbers, height: 22f);
        }

        private static void Divider(RectTransform column)
        {
            UiKit.Space(column, height: 2f);
            UiKit.Divider(column, vertical: false);
            UiKit.Space(column, height: 2f);
        }

        private static void Section(RectTransform column, string word)
        {
            UiKit.Space(column, height: 6f);
            UiKit.Size(UiKit.Heading(column, word), height: 18f);
        }

        /// <summary>A name with its meta on the right, the rules line under it, and the flavour under that.</summary>
        private static void Entry(RectTransform column, string name, string meta, RulesLine rules, string flavour,
            Action<string> onKeyword, bool compact)
        {
            var top = UiKit.Rect("entry", column);
            UiKit.Size(top, height: compact ? 24f : 28f);
            var row = UiKit.Row(top, 8f);
            row.childForceExpandHeight = true;

            var title = UiKit.Label(top, name, compact ? UiTheme.FontBody : UiTheme.FontLarge, UiTheme.Text, bold: true);
            UiKit.Size(title, flexibleWidth: 1f);
            UiKit.Label(top, meta, UiTheme.FontSmall, UiTheme.TextDim, TextAlignmentOptions.MidlineRight);

            var line = UiKit.Label(column, RulesMarkup.For(rules, linked: onKeyword != null),
                compact ? 15f : UiTheme.FontBody, UiTheme.TextNote, wrap: true);
            if (onKeyword != null) KeywordLinks.Attach(line, onKeyword);

            if (!string.IsNullOrEmpty(flavour))
            {
                var words = UiKit.Label(column, flavour, UiTheme.FontSmall, UiTheme.TextDim, wrap: true);
                words.fontStyle = FontStyles.Italic;
            }

            UiKit.Space(column, height: compact ? 4f : 8f);
        }
    }
}
