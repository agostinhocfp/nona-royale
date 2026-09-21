# Nona Royale — Audio provenance

> Location in repo: `docs/audio/PROVENANCE.md`
> Prompts: `docs/audio/MUSIC_PROMPTS.md`; its section numbers are the ones cited below.
> Rule (`AUDIO.md`): nothing ships with a sound whose licence is unrecorded. One row per file that ships.

## Generated in code (original, no third-party material)

| Asset | Source | Notes |
| --- | --- | --- |
| Every `SoundCue` effect | `Unity/Audio/SfxRecipes.cs` | Synthesized at runtime by the game |
| Ability signature stand-ins (Bouncer, Syla, Kurbyn) and the damage-type layers | `Unity/Audio/SignatureRecipes.cs`, `SfxRecipes.cs` | Synthesized at runtime (AU3) |
| Placeholder voice lines for operators with no recordings | `Unity/Audio/VoiceBlips.cs` | Synthesized at runtime; no speech, no voice source |
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
| `Audio/Resources/Audio/Music/Match.wav` | `Match.wav` (formerly `Big Band Version.wav`) | 2026-09-16 | _TBD_ | `MUSIC_PROMPTS.md` §1, Variant B (to confirm) | **AU4 (2026-09-21): the full take**, 0.02–157.97 s, 10 ms fade-in, 0.5 s fade-out, 48→44.1 kHz, −16 LUFS, peaks ≤ −1.5 dBFS (limiter, 0.4 dB). Was the loop 36.97–79.63 s (AU1c) |
| `Audio/Resources/Audio/Music/Match_2.wav` | `Match_2.wav` | 2026-09-16 | _TBD_ | _to confirm_ | AU4: full take 0.00–177.15 s, same chain; limiter 2.6 dB on peaks |
| `Audio/Resources/Audio/Music/Match_3.wav` | `Match_3.wav` (same file as `Sicilian Noir (4_4 Meter).wav`) | 2026-09-16 | _TBD_ | _to confirm_ | AU4: full take 0.02–158.01 s, same chain; limiter 0.3 dB |
| `Audio/Resources/Audio/Music/Match_5.wav` | `Match_5.wav` (same file as `Title.wav`, the title's source take) | 2026-09-16 | _TBD_ | _to confirm_ | AU4: full take 0.34–151.91 s, same chain; −3.2 dB of gain, no limiting |
| `Audio/Resources/Audio/Music/Match_6.wav` | `Match_6.wav` (same file as `Sicilian Waltz.wav`) | 2026-09-16 | _TBD_ | _to confirm_ | AU4: full take 0.07–176.74 s, same chain; limiter 1.4 dB |
| `Audio/Resources/Audio/Music/Title.wav` | `Title.wav` | 2026-09-16 | _TBD_ | _to confirm_ | Loop 52.93–128.35 s, 15 ms crossfade, 48→44.1 kHz, −16 LUFS |
| `Audio/Resources/Audio/Music/Showdown.wav` | `Showdown.wav` | 2026-09-16 | _TBD_ | `MUSIC_PROMPTS.md` §4 (to confirm) | Loop 50.67–146.66 s, 15 ms crossfade, 48→44.1 kHz, −16 LUFS |
| `Audio/Resources/Audio/Music/Win.wav` | `Win.wav` | 2026-09-16 | _TBD_ | `MUSIC_PROMPTS.md` §3 (to confirm) | Cut 50.24–59.66 s, 30 ms fade-in, 300 ms tail, 48→44.1 kHz, −14 LUFS |

Alternates in `Claude outputs/lyria/processed/` don't ship unless moved into `Resources`; add a row if one is.

## Voice recordings

None yet. Add one row per file in `Resources/Audio/Voice/`: who voiced it (or which tool and voice generated it), the date, and the licence or consent. Spec and naming: `docs/audio/VOICE_LINES.md`.

## Third-party libraries

For each pack added, record its name, vendor, URL, licence (for example the Unity Asset Store EULA, CC0, or the Sonniss GDC licence), purchase date, and the files used.

| Pack | Vendor / URL | Licence | Obtained | Kept in |
| --- | --- | --- | --- | --- |
| Casino Audio 1.1 | Kenney Vleugels, https://kenney.nl/assets/casino-audio | CC0 1.0 (credit optional; the credits screen will name Kenney) | 2026-09-16, free download | `Claude outputs/kenney_casino-audio/` (not committed; `License.txt` there) |

| File in the game | Source file | Edits (Claude, 2026-09-16) |
| --- | --- | --- |
| `Audio/Resources/Audio/SFX/DiceShake.wav`, `_2`, `_3` | Casino Audio `dice-shake-1/2/3.ogg` | Mono, 44.1 kHz; start at the first sound; cut to 0.68 s with a 160 ms fade-out; peak-limited to −1 dBFS |
| `Audio/Resources/Audio/SFX/DiceLand.wav`, `_2`, `_3` | Casino Audio `dice-throw-1/2/3.ogg` | Mono, 44.1 kHz; start at the first sound; 80 ms fade-out; peak-limited to −1 dBFS |
| `Audio/Resources/Audio/SFX/Step.wav`, `_2`, `_3` | Casino Audio `chip-lay-1/2/3.ogg` | Mono, 44.1 kHz; start 2 ms before the first sound; cut to 110 ms (`chip-lay-3`: 65 ms, before its bounce) with a squared fade-out of 40 ms (25 ms); 12 dB/octave low-pass at 6 kHz; matched to one A-weighted level, loudest peak −1 dBFS |

## Generated sound effects (ElevenLabs, Adobe Firefly)

None ship yet. Prompts: `docs/audio/SFX_PROMPTS.md`.

**Before the first file ships, record:**
- the tool, the plan and the account;
- the commercial-use terms that applied on the generation date (checked at the source, not a third-party summary);
- whether the tool asks for attribution or an AI-content disclosure.

| File in the game | Source take | Generated | Tool / plan | Prompt (`SFX_PROMPTS.md` §) | Edits |
| --- | --- | --- | --- | --- | --- |
