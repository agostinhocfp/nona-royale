# Nona Royale — Audio (Stage 5)

> Location in repo: `docs/design/AUDIO.md` · Project copy: `claude/AUDIO.md`
> Status: **Closed, 2026-09-16.** AU1 to AU1f and AU2 committed after Play Mode.
> Related: `NEXT_PHASES.md` (Stage 5), `MOTION.md` (Stage 3, whose presentation steps the sounds follow), `PRESENTATION.md` §3.1, `ART_PIPELINE.md` §8 (licensing)

## Goal

The game sounds like a casino after hours: dice, steps, hits, casts and interface clicks, short voice lines per operator (move, kill, death, quit, victory, and so on), and music for the menus and the match. Every level is adjustable and remembered.

Stage 4 (art hookup) was skipped for now: no finished Meshy renders are in hand. It can still jump the queue.

## Decisions (settled 2026-09-16)

1. **Placeholder effects are synthesized in code**, like the procedural sprites.
   - `SfxRecipes` builds every cue from tones, noise and filters at startup. No imported files, no licences.
   - **Amended (AU1d): the palette is physical, not musical.** The designer found the first effects child-like, like a xylophone. Every synthesized cue is now a thing on a card table: felt, clay chips, a dealt card, a switch, a body blow. No cue plays a note or an interval.
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
   - **Superseded (AU1f): there is now a real AudioMixer.** The designer asked for one. It lives in `Resources` and loads by name, so "no scene wiring" still holds.
     - Groups: `Master` › `Music`, `Effects` › `Interface`, and `Voice`. Exposed faders: `MasterVolume`, `MusicVolume`, `EffectsVolume`, `VoiceVolume`.
     - The sliders keep their squared curve; each result becomes a fader in decibels (−80 dB floor). Mute sets Master to −80 dB.
     - Only the player's levels live on the mixer. Per-cue levels, crossfades, the sting dip, ducking and the pause dip stay on the sources.
     - If the asset is missing or incomplete, the director logs one warning and uses the AU1 code-side levels.
3. **Voice placeholders are "voice blips":** short synthesized chirps with a signature per operator, so the priority, cooldown and chance rules can be heard before any recording exists. Real `VoiceSet` clips replace them per operator.
   - **Settled in AU2:** an operator with any recorded file uses recordings only. A slot without a file is silent rather than a blip, because a recorded voice answered by a chirp sounds broken. File names and the recording spec are in `docs/audio/VOICE_LINES.md`.
4. **Music is a procedural noir loop.**
   - Match: a walking bass, brushes and ride, and sparse electric-piano comping in D minor at 88 bpm, 8 bars.
   - Title (also setup and draft): slower and sparser, 66 bpm.
   - Win: a short brass sting.
   - Files named `Title`, `Match` and `Win` in `Assets/_Project/Audio/Resources/Audio/Music/` replace them.
   - **Amended again (AU1c):** the designer's Lyria tracks now replace the placeholders (`Title`, `Match`, `Win`), and a fourth cue, `Showdown`, plays in a match's final stretch. The synthesized loops remain as fallbacks for any missing file.
   - **Amended the same day (AU1b):** the match music is now the designer's own song "Deborah" (artist name Stutt-), arranged as a Sicilian tarantella. The jazz loop moved to the title, and the first title loop was retired.
5. **Taken from the plan's recommendations** (the designer can still change these):
   - **Voice slots:** `deploy`, `move`, `cast`, `hit_taken`, `kill`, `death`, `victory`, `quit`. In code and file names: `Deploy`, `Move`, `Cast`, `HitTaken`, `Kill`, `Death`, `Victory`, `Quit`.
   - **One voice at a time.** Priority: victory > death > kill > cast > hit taken > deploy > move. A higher line interrupts a lower one; a lower one is dropped.
     - **Amended in AU2: moments wait instead of being dropped.** Kill, death, victory and quit are held for up to 1.5 s and play when the current line ends. Otherwise every knockout would drop the kill line behind the victim's death line. Quit ranks above victory; the two never meet.
   - A per-operator cooldown; move lines play by chance (about 30%). "Quit" plays when a match is abandoned through MAIN MENU.
     - **AU2 values:**
       - cooldown 3.5 s, which the moments ignore;
       - move chance 30%;
       - a 0.35 s breath after any line before the next lower-priority line may start.
     - **Quit also plays for NEW MATCH from the pause menu**, which abandons the match just the same.
   - Music ducks under voice lines.
   - **Pause:** music keeps playing at 35%, effects pause, interface clicks still sound.
   - **Settings:** sliders for Master, Music, Effects and Voice, plus Mute, on a new SOUND page reached from the settings page. All remembered.
     - **AU1f:** the Music default drops 30%, from 60% to 42%, because the score sat on top of the effects. A Restore defaults row closes the page.

## Increments

| #   | Increment | What it delivers |
| --- | --------- | ---------------- |
| AU1 | **Director, effects, music, settings** | `Unity/Audio/`: `Synth`, `SfxRecipes`, `MusicRecipes`, `AudioLevels`, `SoundBank`, `AudioDirector`. Effects on the presentation steps, the turn chime, interface clicks, music per screen, the win sting, pause behaviour. `UiKit.SliderRow`, the SOUND page, remembered levels. |
| AU1b | **"Deborah" tarantella** | `TarantellaScore` (the arrangement as data), `Synth.Pluck` and `Wave.Reed`, the tarantella renderer in `MusicRecipes`. The match loop; the jazz loop moves to the title. A WAV render and a MIDI file for a musician. |
| AU1c | **Lyria score** | The designer's Lyria tracks as the real music: loops cut and levelled, `Showdown` for a match's final stretch (`GameEngine.IsFinalStretch`), and a provenance record. |
| AU1d | **Grounded effects** | `Synth.Resonate` (noise through a gliding resonant band-pass) and `Synth.Darken`; every `SfxRecipes` cue rebuilt without pitched tones; per-cue levels rebalanced in `SoundBank`; `SfxRecipesTests`. |
| AU1e | **Chip steps** | Kenney `chip-lay-1…3` as `Step`, `Step_2`, `Step_3`: trimmed to the landing, darkened, level-matched; `SoundBank` step level 0.41. |
| AU1f | **Mixer** | `Audio/Resources/Audio/Mixer.mixer` (Master, Music, Effects › Interface, Voice; four exposed faders); `SoundMixer` routes every source and sets the faders, with the AU1 levels as fallback; Music default 42% (was 60%) with a one-time move for untouched saves; Restore defaults on the Sound page; `AudioLevelsTests`, `SoundMixerAssetTests`. |
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
- 2026-09-16 — **AU2 built: voices.** Everything is in the repo; Play Mode pending, then Stage 5 closes.
  - **New in `Unity/Audio/`:**
    - `VoiceRules`: one voice at a time, priority, held moments, cooldown, the breath, the move chance. Plain C#, on a clock the caller keeps.
    - `VoiceBlips`: per-operator `VoiceSignature`s and a syllable contour per slot. Plain C#, deterministic, seeded with a stable hash.
    - `VoiceCasting`: who speaks, read from the batch. The killer is the caster, else the operator that moved or collided, on the credited seat. The victor is the winner's last operator home, else the first of its squad.
    - `VoiceSet`: recorded lines per operator, from `Resources/Audio/Voice/<Operator>_<Slot>[_n]`.
  - **Changed:**
    - `SoundBank.WarmVoice` / `Voice`: voices load per operator; blips are synthesized on worker threads like the effects.
    - `AudioDirector`:
      - `Speak`, `WarmVoices` and `StopVoice`;
      - a voice source that pauses with the game;
      - a voice clock that stops in pause;
      - music ducking to `AudioLevels.DuckedMusic` (0.12 s down, 0.5 s back).
  - **Hooks in `MatchBootstrap`**, each on the presentation step it belongs to:
    - deploy on the rise, move on the walk, cast on the cast tell;
    - hit taken on the hit, for a target left standing;
    - death then kill on the knockout;
    - victory at the winning settle, beside the sting;
    - quit from the pause menu's MAIN MENU and NEW MATCH.
    - A new deal cuts any voice and warms the dealt operators' voices.
    - `PlayFeedback` now takes the batch's caster.
  - **Voices never hold the presentation queue.** They ride on steps that already exist.
    - At CPU speed Instant, batches settle without steps, so only the victory line is heard. That's intended.
    - The caster's `Cast` line usually outranks the target's `HitTaken` in the same exchange.
  - **Tests:** a new EditMode assembly, `NonaRoyale.Unity.EditTests` (`Assets/Tests/EditMode/Unity/`), references the Unity assembly. It covers the plain-C# audio classes: 16 `VoiceRules`, 10 `VoiceCasting` and 8 `VoiceBlips` tests. **556 passing** (522 before).
  - **Luka's line list:** `docs/audio/VOICE_LINES.md`. It has 24 lines across the 8 slots, voice direction, the file and recording spec, and a template for the other eight operators.
  - **Previews:** one reel per operator, all eight slots in priority order, rendered outside the editor. Sent in the chat, not committed.
- 2026-09-16 — **AU1d: grounded effects.** The designer found the interface and movement effects child-like, like a xylophone, and asked for something grounded, mature and subtle.
  - **Why they sounded that way:** the step was a 420 Hz sine tick, the click a 2.1 kHz triangle, doubles and the turn bell were bells a fourth and a third apart, heal was a C major arpeggio, and the knockout sprayed 26 sine pings. Clean pitches in the 400–6000 Hz range, decaying like struck bars, are what a xylophone is.
  - **The new rule:** bodies come from noise ringing a resonant filter, so nothing has a clean pitch. Sine tones remain only below about 120 Hz, as weight. Most cues are darkened so they sit under the music and the voices.
  - **New in `Synth`:**
    - `Resonate`: noise through a trapezoidal state-variable band-pass whose centre glides; unity gain at the centre, stable under any glide, declicked at the end. A burst of a few milliseconds rings it like a knock (ring ≈ Q / (π · f)); a slow swell with low Q is a whoosh.
    - `Darken`: a 12 dB/octave low-pass over a whole buffer.
  - **`SfxRecipes`**, on three building blocks (`Thud`, `Tick`, `Swish`, plus `Clack` for chips and dice):
    - `UiClick`: a bakelite switch, a dull click and the lever's faint return (3 variants, was 1).
    - `Step`: a weighted piece set down on felt.
    - `Rise`: a piece slid onto the cloth and set down.
    - `TurnStart`: a card dealt across the cloth, and a tap as it stops.
    - `Doubles`: two clay chips knocked together.
    - `CastTell`: a low pressure swell. `CastCell`: something thrown that lands with a dull thump.
    - `Heal`: a long exhale over a low, soft fifth (D3–A3). The only tonal cue, and it is below the old range.
    - `Hit` and `HitBig`: a slap over a chest, driven for density; the big one carries debris.
    - `Miss`: a close, low whoosh. `Block`: a dull plate knock over a braced thump, no ring.
    - `Knockout`: a heavy drop and a short, dark glass break made of noise shards.
    - The dice recipes use the same blocks, though the Kenney files still replace them.
  - **Levels (`SoundBank.SpecOf`):** balanced by A-weighted loudness (loudest 50 ms). Knockout and big hit on top, casts and hits next, the board below them, steps and the interface lowest. `CastTell` and `CastCell` now have their own lines. Doubles and the turn cue gain a little pitch jitter, since they are no longer notes.
  - **Checks:**
    - Every cue rendered outside the editor: no NaNs, every buffer ends in silence, and the spectrograms show no tonal lines except heal's low fifth.
    - Band energies were checked so steps and clicks keep content in 150–2500 Hz and stay audible on phone and tablet speakers.
    - `SfxRecipesTests` (7 tests) in `NonaRoyale.Unity.EditTests`: every cue builds short, clean and silent at the end; builds are deterministic; variants differ; `Resonate` stays bounded, passes its centre, rejects far frequencies and fades when cut short; `Darken` keeps lows and cuts highs. They pass with the `VoiceBlips` tests in a cloud harness (15 of 15). Mutants caught: a one-stage `Darken`, miswired filter feedback, a wrong centre frequency, a missing end fade, and identical variants.
    - `SoundBank` is Unity code; Play Mode is its test.
  - **Preview:** `Claude outputs/au1d_sfx_preview.zip` (not committed): a mock turn with the old palette, the new one, and the new one with Kenney chips as steps and a Kenney card slide as the turn cue; every cue on its own; spectrograms.
  - **Open:** if the synthesized steps or turn cue still feel artificial, the Kenney chips (`chip-lay-1…3`) and card slides are CC0 and already on disk; dropping them in as `Step`, `Step_2`… or `TurnStart` is the whole hookup, but they need a provenance row.
- 2026-09-16 — **AU1e: Kenney chips for the step.** After the AU1d preview, the designer asked which Kenney sound to use for steps and to proceed.
  - **The pick: `chip-lay-1…3`.** They are the only single-chip set-downs in the pack: one object meeting the cloth, which is what a piece landing is. `chips-stack`, `chips-collide` and `chips-handle` are chip against chip, which reads as betting, not moving. `chip-lay-3` is the most grounded (a low thump), `1` and `2` are brighter.
  - **Edits** (full row in `docs/audio/PROVENANCE.md`):
    - The originals open with 48–86 ms of silence, and the step fires when the hop lands (`OperatorPiece.Hop` raises `Stepped` at the end), so each file now starts 2 ms before its sound.
    - Cut short for walks at 8 hops a second (125 ms apart): 110 ms, and 65 ms for `chip-lay-3`, which ends before its second bounce.
    - A 12 dB/octave low-pass at 6 kHz takes the glassy ring off, to fit the AU1d palette.
    - The three are matched to one A-weighted level.
  - **Files:** `Audio/Resources/Audio/SFX/Step.wav`, `Step_2.wav`, `Step_3.wav`. As with the dice, they replace all four synthesized step variants. `SoundBank` still adds ±6% pitch jitter.
  - **Level:** `SoundBank.SpecOf(Step)` is 0.41, which plays the files at the synthesized step's level (about −30 dBA over the loudest 50 ms).
  - **Checks:** a six-hop walk rendered at 8 hops a second, synthesized and then the files. The `.meta` files come from Unity on import (default settings, like the dice).
- 2026-09-16 — **AU2, AU1d and AU1e committed** by the designer: `947169a` (voices) and `a17b8ee` (grounded effects and chip steps).
- 2026-09-16 — **AU1f: the mixer and a quieter music default.** The designer asked for sound settings that include a mixer, with the music starting about 30% lower. The Sound page already existed (AU1), so this adds the mixer under it.
  - **`Audio/Resources/Audio/Mixer.mixer`**, written by hand as Unity YAML (there is no public API to create a mixer from code):
    - `Master` › `Music`, `Effects` › `Interface`, `Voice`; each group has its Attenuation. One snapshot, left at 0 dB. Update mode: unscaled time.
    - Exposed: `MasterVolume`, `MusicVolume`, `EffectsVolume`, `VoiceVolume`. `Interface` isn't exposed; it sits under `Effects`, so clicks follow the Effects slider as before.
    - Unity writes its `.meta` on import.
  - **New `SoundMixer`** (Unity code): loads the asset, finds the four groups by exact path, checks the four faders, routes each source (`AudioSource.outputAudioMixerGroup`), and sets the faders every frame.
    - Exposed faders set from code ignore snapshots, so in Play Mode the Audio Mixer window shows the meters but the four faders follow the sliders. Effects added to a group, and the Interface fader, can still be tuned there.
    - Anything missing: one warning (`[Audio] …`), `Ready` stays false, and the director uses the AU1 levels.
  - **`AudioDirector`:** loads the mixer in `Bind` and routes every source (effects and clicks, both music sources and the sting, the voice). `Level(bus)` is 1 with the mixer, and the AU1 master × bus gain without it. Source volumes keep the cue level, fades, sting dip, duck and pause dip.
  - **`AudioLevels`:**
    - `DefaultMusic` is 0.42 (was 0.6, `FormerDefaultMusic`).
    - `MasterGain` (0 when muted), `BusGain`, `Decibels` (20·log10, clamped to −80…0 dB, NaN-safe), `Reset`, `IsDefault`.
    - `Version` 2 and `MigrateMusic`: a Music value saved before AU1f that still equals 60% was never chosen, so it loads as 42%. Anything else the player set stays.
  - **Settings:** `SettingsStore` reads and writes `nr.audio.version` (missing means 1). The Sound page gains **Restore defaults**, whose chip reads DEFAULT or RESET.
  - **Checks:**
    - The view compiles with no warnings.
    - `AudioLevelsTests` (9) run in the cloud harness: **583 passing** (574 before). Mutants caught: the migration applied to current saves, a wide migration tolerance, Mute not silencing Master, Reset leaving Mute on, IsDefault ignoring Mute, the dB floor, the 0 dB ceiling, and a wrong bus slider.
    - `SoundMixerAssetTests` (4) need the editor: the asset loads by name, every group exists, `Interface` sits under `Effects`, and every fader is exposed. They were compiled against the Unity DLLs but not run.
    - The mixer file parses as YAML. Whether Unity accepts it is the first Play Mode check.
  - **Play Mode watch-list:**
    - No `[Audio]` warning in the Console; the four `SoundMixerAssetTests` pass.
    - Window › Audio › Audio Mixer shows the Mixer with its groups; the meters move on music, effects, clicks and voices.
    - Each slider moves only its bus; Master and Mute move everything; clicks follow Effects.
    - Music starts at 42%, including on a machine whose saved Music was still 60%.
    - Ducking, the pause dip and the win sting behave as before. Clicks still sound in pause.
    - Restore defaults resets the sliders and unmutes, and the chip reads DEFAULT afterwards.
  - **If Unity rejects the mixer file:** delete it, create one with Assets › Create › Audio Mixer at the same path, named `Mixer`. Add `Music`, `Effects` and `Voice` under `Master`, then `Interface` under `Effects`. For Master, Music, Effects and Voice, right-click Volume in the Inspector, choose Expose, and rename the exposed parameters to the four names above.
- 2026-09-16 — **AU1f passed Play Mode** ("all sounds great"): the mixer imported, and the sliders, Mute, the music default and Restore defaults behave. Committed as `feat(audio): audio mixer with exposed bus faders and a quieter music default`.
- 2026-09-16 — **Stage 5 closed.** Carried forward: Luka's recordings from `docs/audio/VOICE_LINES.md`, then the other operators' lines from its template; optional mixer effects (a low-pass on music in pause, a light room reverb); the Kenney card slide as the turn cue or a draft card flip (needs a `PROVENANCE.md` row); a main-thread synthesis fallback if a WebGL build is ever wanted.
