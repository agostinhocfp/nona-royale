# Nona Royale — Audio (Stage 5)

> Location in repo: `docs/design/AUDIO.md` · Project copy: `claude/AUDIO.md`
> Status: **Open, 2026-09-16.** AU1, AU1b and AU1c are in the repo, awaiting Play Mode and one commit. AU2 next.
> Related: `NEXT_PHASES.md` (Stage 5), `MOTION.md` (Stage 3, whose presentation steps the sounds follow), `PRESENTATION.md` §3.1, `ART_PIPELINE.md` §8 (licensing)

## Goal

The game sounds like a casino after hours: dice, steps, hits, casts and interface clicks, short voice lines per operator (move, kill, death, quit, victory, and so on), and music for the menus and the match. Every level is adjustable and remembered.

Stage 4 (art hookup) was skipped for now: no finished Meshy renders are in hand. It can still jump the queue.

## Decisions (settled 2026-09-16)

1. **Placeholder effects are synthesized in code**, like the procedural sprites.
   - `SfxRecipes` builds every cue from tones, noise and filters at startup. No imported files, no licences.
   - A real clip named after the cue in `Assets/_Project/Audio/Resources/Audio/SFX/` (variants `<Cue>_2` … `<Cue>_8`) replaces that cue, and only that cue. Dropping the file in is the whole hookup.
   - **Where things live** (Claude's call, delegated by the designer):
     - Code: `Assets/_Project/Scripts/Unity/Audio/`, next to `View/` and `Composition/`, in the `NonaRoyale.Unity` assembly.
     - Sound files: the project's existing `Assets/_Project/Audio/` asset folder, as `Art/` holds images. `SFX/` and `Music/` moved to `Audio/Resources/Audio/`; AU2 adds `Voice/` there.
     - **`Resources`, not Addressables, for now.** Unity loads by name only from folders called `Resources`, and that keeps "no scene wiring". Unity advises against `Resources` for large content, because everything in them ships in the build and is indexed at startup. A few dozen short clips are well inside what it handles. Addressables become worth it if the audio grows large (full voice sets for every operator, long music) or needs downloading. The loader is one class, so the switch stays contained.
     - **The inner `Audio/` is a namespace.** Every `Resources` folder in the project, packages included (TextMesh Pro has one), shares one set of load names, so `Audio/SFX/HitBig` can't collide with some package's `SFX/HitBig`.
     - The same pattern is recommended for Stage 4: `Assets/_Project/Art/Resources/Art/Operators/`.
2. **Code-side buses, no AudioMixer asset.** A mixer can only be made in the editor, which breaks "one component, no scene wiring".
   - `AudioLevels` holds Master, Music, Effects, Voice and Mute. Each source plays at master × bus, on a squared (perceptual) curve.
   - Interface clicks follow the Effects slider.
   - A mixer asset can come later if real effects (reverb, snapshots) are wanted.
3. **Voice placeholders are "voice blips":** short synthesized chirps with a signature per operator, so the priority, cooldown and chance rules can be heard before any recording exists. Real `VoiceSet` clips replace them per operator.
4. **Music is a procedural noir loop.**
   - Match: a walking bass, brushes and ride, and sparse electric-piano comping in D minor at 88 bpm, 8 bars.
   - Title (also setup and draft): slower and sparser, 66 bpm.
   - Win: a short brass sting.
   - Files named `Title`, `Match` and `Win` in `Assets/_Project/Audio/Resources/Audio/Music/` replace them.
   - **Amended again (AU1c):** the designer's Lyria tracks now replace the placeholders (`Title`, `Match`, `Win`), and a fourth cue, `Showdown`, plays in a match's final stretch. The synthesized loops remain as fallbacks for any missing file.
   - **Amended the same day (AU1b):** the match music is now the designer's own song "Deborah" (artist name Stutt-), arranged as a Sicilian tarantella. The jazz loop moved to the title, and the first title loop was retired.
5. **Taken from the plan's recommendations** (the designer can still change these):
   - **Voice slots:** `deploy`, `move`, `cast`, `hit_taken`, `kill`, `death`, `victory`, `quit`.
   - **One voice at a time.** Priority: victory > death > kill > cast > hit taken > deploy > move. A higher line interrupts a lower one; a lower one is dropped.
   - A per-operator cooldown; move lines play by chance (about 30%). "Quit" plays when a match is abandoned through MAIN MENU.
   - Music ducks under voice lines.
   - **Pause:** music keeps playing at 35%, effects pause, interface clicks still sound.
   - **Settings:** sliders for Master, Music, Effects and Voice, plus Mute, on a new SOUND page reached from the settings page. All remembered.

## Increments

| #   | Increment | What it delivers |
| --- | --------- | ---------------- |
| AU1 | **Director, effects, music, settings** | `Unity/Audio/`: `Synth`, `SfxRecipes`, `MusicRecipes`, `AudioLevels`, `SoundBank`, `AudioDirector`. Effects on the presentation steps, the turn chime, interface clicks, music per screen, the win sting, pause behaviour. `UiKit.SliderRow`, the SOUND page, remembered levels. |
| AU1b | **"Deborah" tarantella** | `TarantellaScore` (the arrangement as data), `Synth.Pluck` and `Wave.Reed`, the tarantella renderer in `MusicRecipes`. The match loop; the jazz loop moves to the title. A WAV render and a MIDI file for a musician. |
| AU1c | **Lyria score** | The designer's Lyria tracks as the real music: loops cut and levelled, `Showdown` for a match's final stretch (`GameEngine.IsFinalStretch`), and a provenance record. |
| AU2 | **Voices** | `VoiceRules` (priority, cooldown, chance), `VoiceBlips` (per-operator signatures), `VoiceSet` (real clips by operator name), music ducking. Voice hooks on deploy, move, cast, hit, kill, death, victory and quit. Luka's line list. |

## Rules for this stage

- The view computes nothing: every sound is a response to an engine event or a command the engine accepted, played on the presentation step it belongs to.
- The synthesis code is plain C# with no Unity types, so it can be rendered to WAV and listened to outside the editor.
- Sounds use real time: pause stops effects on purpose, a hit-stop never bends them.
- Nothing ships with a sound whose licence is unrecorded. The synthesized placeholders are original.

## Log

- 2026-09-16 — **Stage opened** in the Stage 3 chat, after Stage 3 closed. HEAD: `feat(motion): hops, rises, knockout shatter, cast tells and idle`.
- 2026-09-16 — **Decisions 1–4 settled** with the designer, all as recommended. Decision 5 takes the plan's recommendations.
- 2026-09-16 — **AU1 built: director, effects, music, settings.**
  - **New `Unity/Audio/` (namespace `NonaRoyale.Unity.Audio`):**
    - `Synth`: oscillators (sine, triangle, square, saw), filtered noise, bells, soft drive, normalizing, and a small xorshift stream. Loop-aware writes let notes ring across a loop point.
    - `SfxRecipes` and `SoundCue`: 15 cues.
      - Dice: shake, land, doubles chime.
      - Movement: step (4 variants), rise.
      - Casts: cast (rising zap), cell cast (falling zap).
      - Hits: hit and big hit (3 variants each), heal, miss, block, knockout (glass over a boom).
      - Other: the turn bell and the interface click.
    - `MusicRecipes` and `MusicCue`: the match loop (21.8 s), the title loop (14.5 s), and the win sting (3.2 s).
    - `AudioLevels` and `AudioBus`: the volume model (decision 2).
    - `SoundBank`: real clips from `Resources/Audio/SFX` and `Resources/Audio/Music`, otherwise synthesis on worker threads, turned into clips a few per frame so startup doesn't stall. It also holds each cue's volume, pitch jitter and least gap between repeats (steps 45 ms, so a four-piece walk isn't a drum roll).
    - `AudioDirector`: 12 effect voices and 2 interface voices (the oldest is stolen when all are busy), two music sources for 1.2 s crossfades, and a sting source that dips the music for its length and brings it back over 1.5 s.
      - Pause uses `AudioListener.pause`; music and interface sources ignore it.
      - Effects are panned lightly by where they happen.
      - It adds an `AudioListener` only if the scene has none.
  - **`MatchBootstrap`:**
    - Builds the director at Start and loads the levels.
    - Plays the cue on each presentation step: shake with the throw, land and the doubles chime when the dice settle, a step on each hop, rise, cast or cell cast with the tell, hit or big hit (same threshold as the hit-stop), miss, block, heal, and knockout.
    - Rings the turn bell when a turn begins, and plays the win sting at the settle that ends the match, CPU Instant included.
    - `SyncAudio` runs every frame, including under cards. It picks the match loop while a live match is on screen or paused, the title loop everywhere else, and tells the director whether the pause menu is open.
    - Levels are saved when they change and no mouse button is held, so dragging a slider doesn't write to disk every frame.
  - **`UiKit`:**
    - `ButtonPressed`, a static event raised by every kit button. The root subscribes for the click and unsubscribes in `OnDestroy`. It's a plain event, not a service; nothing else reads it.
    - `SliderRow`: a label, a thin cyan track with a gold diamond handle (the whole box catches the pointer), and a percentage. It never rebuilds its page, which would end the drag.
  - **Settings:**
    - `ISettingsHost.Audio`.
    - `SettingsRows.Build` now opens with a "Sound" row showing the master level or MUTED.
    - `SettingsRows.BuildSound` holds Mute and the four sliders. Muting dims the sliders.
    - `SettingsStore.LoadAudio` and `SaveAudio` use `nr.audio.master`, `.music`, `.sfx`, `.voice` and `.mute`.
    - `PauseMenu` and `TitleScreen` gain a Sound page. Esc steps back from Sound to Settings to the main page.
  - **Checks:**
    - The view compiles with no warnings; 496 core tests pass; the sim compiles.
    - Every recipe was rendered to WAV in the cloud: no NaNs, no clicks at the edges, loop seams under 0.004. It was also checked on a spectrogram, where the cast zap's square wave showed aliasing and was switched to a triangle.
    - The volume model was exercised in a scratch harness.
    - The preview WAVs went to the designer before Play Mode.
    - The director and the slider are Unity code, so Play Mode is their test.
  - **Known limits:**
    - Synthesis uses worker threads, which WebGL doesn't support. That's fine for the PC, tablet and mobile targets; a WebGL build would need a main-thread fallback.
    - The first half second after Play can be silent while the clips build.
    - CPU Instant plays only the turn bells and the sting, because its batches skip the presentation steps.
- 2026-09-16 — **AU1 written to the repo** after the designer's hover-echo commit. The preview WAVs that had been dropped into `Resources/Audio/` were moved to `Claude outputs/au1_preview/` (not committed): their names match no cue, so the game ignored them, but anything under `Resources` ships in every build.
- 2026-09-16 — **AU1b built: "Deborah" as the match music.** The designer wrote and produced the song, and asked for a Sicilian tarantella arrangement of its melody and chords.
  - **Transcription** (cloud: `basic-pitch` on the full mix, plus librosa chroma):
    - The song is in C minor at 138 bpm.
    - Opening changes: Cm Cm Cm Cm Fm Fm Ab G, under an arpeggio figure (C Eb G C).
    - Verse: a sparse melody (C D Eb Ab G…) over an 808 riff walking C, C, D, F, F.
    - Around 2:20: a high lead line (C C D Eb F D C Eb C…) over Fm, Ab, G.
    - The designer confirmed the check render as "close enough".
    - Stem separation (demucs) was not possible: its model downloads are blocked from the cloud workspace.
  - **New `TarantellaScore`:** the arrangement as plain data, and the single source for the game, the WAV and the MIDI.
    - Both melodies are kept on their original sixteenth grid.
    - Each 4/4 beat becomes a dotted-quarter beat of 12/8. Within the beat, the sixteenths land on 0, 2, 3 and 4 of six, which turns even sixteenths into the siciliana's dotted lilt.
    - 126 to the dotted quarter, C minor, 26 bars (49.5 s).
    - Form: intro (2 bars) · A, the opening changes with the arpeggio on mandolin (8) · B, the verse melody on accordion over C–D–F (4) · C, the lead line on mandolin an octave down, over Fm Ab G7 and Gsus (8) · B′, the verse with the mandolin an octave above, turning home on G7 (4).
    - G is a dominant seventh (B natural), the harmonic-minor colour of the style.
    - Accompaniment: accordion bass on every dotted beat, root and fifth alternating; right-hand chords on the two off-eighths; tamburello strikes on the beat, lighter jingles between, a rolling shake from section C on, and the skin on beats 1 and 3.
  - **`Synth`:** `Pluck` (Karplus–Strong, fractional delay so high notes stay in tune) and `Wave.Reed` (a 30% pulse).
  - **`MusicRecipes`:** `Match` renders the score.
    - The mandolin is two courses about 4 cents apart; notes of three slots or more are played tremolo, one stroke a slot.
    - The accordion melody uses three musette reeds at 0 and ±14 cents; the chords use two reeds; the bass is a low reed plus its fundamental.
    - The tamburello is jingles (filtered noise and metallic partials) and a palm-struck skin.
    - Part levels are named constants, balanced by measuring each part alone: the mandolin and the accordion lead, the band sits under them.
    - `Title` now plays the former match jazz loop. The former title loop was removed.
  - **Checks:**
    - The view compiles with no warnings; 496 core tests pass.
    - The loop renders in about 0.9–1.5 s on a worker thread, with no NaNs and a loop seam of 0.002.
    - The designer has a WAV preview (the loop played twice) and a MIDI file: 12/8 with a tempo of 189 per quarter (126 per dotted quarter), key signature C minor, and six parts. General MIDI has no mandolin, so the mandolin part uses the banjo program.
  - **Honest limit:** the synthesized mandolin and accordion are convincing placeholders, not real players. For release, the MIDI goes to a musician or a proper sample library, and the result is dropped in as `Resources/Audio/Music/Match`.
- 2026-09-16 — **AU1c: the Lyria score.** The designer generated tracks with Lyria from `docs/audio/MUSIC_PROMPTS.md` and chose the slots: `Big Band Version` for Match, `Title` for Title, `Win` for the sting, `Showdown` for a match's final stretch, and a waltz kept as an alternate title.
  - **Loop cutting** (cloud, librosa):
    - Beat tracking, then a recurrence matrix over 4-bar windows to find passages the take genuinely repeats. Both the 4 bars before and the 4 bars after the join must match.
    - Then a frame-level alignment of chroma, onsets and spectrum places the join, a sample-level waveform match fine-tunes it, and an equal-power crossfade of 15 ms (40 ms for the waltzes) smooths it.
    - Lyria's long takes mostly don't repeat, so the loops are the longest real repeats, not the whole take.
    - Averaged feature similarity couldn't tell a join from one a beat late; the frame-level alignment is what fixed the bar position.
  - **Levels:** resampled from 48 kHz to 44.1 kHz; loops at −16 LUFS integrated with sample peaks at or below −1.5 dBFS; the sting at −14 LUFS.

      | File | Source take | Cut | Length | Repeat match | LUFS in → out |
      | --- | --- | --- | --- | --- | --- |
      | `Match` | Big Band Version.wav | 36.97–79.63 s | 42.7 s | 0.97 | -15.0 → -16.0 |
      | `Title` | Title.wav | 52.93–128.35 s | 75.4 s | 0.83 | -12.6 → -16.0 |
      | `Showdown` | Showdown.wav | 50.67–146.66 s | 96.0 s | 0.82 | -14.8 → -16.0 |
      | `Title_alt_waltz` | Classic Sicilian Waltz (Take 2).wav | 51.47–110.76 s | 59.3 s | 0.75 | -13.3 → -16.0 |
      | `Title_alt_waltz2` | Sicilian Waltz.wav | 40.08–120.08 s | 80.0 s | 0.67 | -15.6 → -17.1 |

  - **Win:** the take's ending, from the last pickup to the end of the final chord's decay (50.24–59.66 s, 9.4 s, 30 ms fade-in and 300 ms tail). The opening fanfare is parked as `Win_alt_opening`.
  - **Match is short:** the big band take repeats only one 24-bar section, so the loop is 42.7 s. If it wears thin, generate a take that restates its head, or loop it together with its other sections in a DAW.
  - **In `Assets/_Project/Audio/Resources/Audio/Music/`:** `Title.wav`, `Match.wav`, `Win.wav`, `Showdown.wav`.
  - **In `Claude outputs/lyria/processed/`:** `Title_alt_waltz` (Classic Sicilian Waltz, take 2; the better repeat match), `Title_alt_waltz2`, `Win_alt_opening`, and `seams/` (8 s either side of each join, for checking by ear). Rename an alternate to `Title.wav` to try it.
  - **Showdown:**
    - New `MusicCue.Showdown`. It is file-only: `MusicRecipes.CanSynthesize` is false for it, so without the file the match music carries on.
    - `MatchBootstrap.SyncAudio` picks it while a live match is on screen or paused and `GameEngine.IsFinalStretch` is true. Operators that reach home never leave, so it never switches back mid-match.
  - **Core:**
    - `GameEngine.IsFinalStretch`: some seat has every operator but one home, and the match isn't over. It's a presentation query, kept in the core because the view computes nothing.
    - `TurnStateMachine.Players`: a read-only list of seats.
    - 4 tests; **516 passing** (512 before, counting the designer's commits since). A mutation check on the threshold and on the match-over guard each failed a test.
  - **Provenance:** `docs/audio/PROVENANCE.md`. Model name, prompt variant and plan terms are for the designer to fill in before release.
  - **Checks:** the view compiles with no warnings, the sim compiles, and the joins show no clicks on a spectrogram. Whether each join sounds right can only be judged by ear, from the seam files.
- 2026-09-16 — **Kenney Casino Audio on the dice.** The designer added Kenney's Casino Audio (CC0) to `Claude outputs/kenney_casino-audio/`.
  - `DiceShake` (3 variants) comes from `dice-shake-1…3`: trimmed to 0.68 s so it ends before the dice land (the tumble is 0.75 s).
  - `DiceLand` (3 variants) comes from `dice-throw-1…3`.
  - All six are mono WAV at 44.1 kHz with a peak of −1 dBFS; a light limiter took off at most 6 dB. They are clickier than the synthesized rattle: their loudest 50 ms runs 4–8 dB quieter while peaking the same. Judge in Play Mode; `SoundBank.SpecOf` holds the volume if they need it.
  - The rest of the pack (cards, chips) isn't wired. Chip clacks could stand in for `Step`, and card sounds could serve a draft card flip; neither has been tried.
  - Recorded in `docs/audio/PROVENANCE.md`. The conversion script is a one-off; the table there lists every edit.
