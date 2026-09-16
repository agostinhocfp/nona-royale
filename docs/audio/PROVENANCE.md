# Nona Royale — Audio provenance

> Location in repo: `docs/audio/PROVENANCE.md`
> Rule (`AUDIO.md`): nothing ships with a sound whose licence is unrecorded. One row per file that ships.

## Generated in code (original, no third-party material)

| Asset | Source | Notes |
| --- | --- | --- |
| Every `SoundCue` effect | `Unity/Audio/SfxRecipes.cs` | Synthesized at runtime by the game |
| Fallback `Title` and `Match` loops, `Win` sting | `Unity/Audio/MusicRecipes.cs`, `TarantellaScore.cs` | `Match` is arranged from "Deborah", written and produced by the designer (artist name Stutt-) |

## Generated with Google Lyria

**To fill in before release:**
- the exact model (Lyria 3 Clip / 3.5 / 3 Pro) and where it was used (Gemini app, Flow, Vertex AI);
- the account or plan;
- the commercial-use terms that applied on the generation date;
- Steam's AI-content disclosure.

Every Lyria output carries a SynthID watermark.

| File in the game | Source take (`Claude outputs/lyria/`) | Generated | Model / surface | Prompt | Edits (Claude, 2026-09-16) |
| --- | --- | --- | --- | --- | --- |
| `Audio/Resources/Audio/Music/Match.wav` | `Big Band Version.wav` | 2026-09-16 | _TBD_ | `MUSIC_PROMPTS.md` §1, Variant B (to confirm) | Loop 36.97–79.63 s, 15 ms crossfade, 48→44.1 kHz, −16 LUFS |
| `Audio/Resources/Audio/Music/Title.wav` | `Title.wav` | 2026-09-16 | _TBD_ | _to confirm_ | Loop 52.93–128.35 s, 15 ms crossfade, 48→44.1 kHz, −16 LUFS |
| `Audio/Resources/Audio/Music/Showdown.wav` | `Showdown.wav` | 2026-09-16 | _TBD_ | `MUSIC_PROMPTS.md` §4 (to confirm) | Loop 50.67–146.66 s, 15 ms crossfade, 48→44.1 kHz, −16 LUFS |
| `Audio/Resources/Audio/Music/Win.wav` | `Win.wav` | 2026-09-16 | _TBD_ | `MUSIC_PROMPTS.md` §3 (to confirm) | Cut 50.24–59.66 s, 30 ms fade-in, 300 ms tail, 48→44.1 kHz, −14 LUFS |

Alternates in `Claude outputs/lyria/processed/` don't ship unless moved into `Resources`; add a row if one is.

## Third-party libraries

For each pack added, record its name, vendor, URL, licence (for example the Unity Asset Store EULA, CC0, or the Sonniss GDC licence), purchase date, and the files used.

| Pack | Vendor / URL | Licence | Obtained | Kept in |
| --- | --- | --- | --- | --- |
| Casino Audio 1.1 | Kenney Vleugels, https://kenney.nl/assets/casino-audio | CC0 1.0 (credit optional; the credits screen will name Kenney) | 2026-09-16, free download | `Claude outputs/kenney_casino-audio/` (not committed; `License.txt` there) |

| File in the game | Source file | Edits (Claude, 2026-09-16) |
| --- | --- | --- |
| `Audio/Resources/Audio/SFX/DiceShake.wav`, `_2`, `_3` | Casino Audio `dice-shake-1/2/3.ogg` | Mono, 44.1 kHz; start at the first sound; cut to 0.68 s with a 160 ms fade-out; peak-limited to −1 dBFS |
| `Audio/Resources/Audio/SFX/DiceLand.wav`, `_2`, `_3` | Casino Audio `dice-throw-1/2/3.ogg` | Mono, 44.1 kHz; start at the first sound; 80 ms fade-out; peak-limited to −1 dBFS |
