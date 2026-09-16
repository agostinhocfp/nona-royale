# Nona Royale — Audio (Stage 5)

> Location in repo: `docs/design/AUDIO.md` · Project copy: `claude/AUDIO.md`
> Status: **Open, 2026-09-16.** Decisions settled. AU1 built, awaiting Play Mode. AU2 next.
> Related: `NEXT_PHASES.md` (Stage 5), `MOTION.md` (Stage 3, whose presentation steps the sounds follow), `PRESENTATION.md` §3.1, `ART_PIPELINE.md` §8 (licensing)

## Goal

The game sounds like a casino after hours: dice, steps, hits, casts and interface clicks, short voice lines per operator (move, kill, death, quit, victory, and so on), and music for the menus and the match. Every level is adjustable and remembered.

Stage 4 (art hookup) was skipped for now: no finished Meshy renders are in hand. It can still jump the queue.

## Decisions (settled 2026-09-16)

1. **Placeholder effects are synthesized in code**, like the procedural sprites.
   - `SfxRecipes` builds every cue from tones, noise and filters at startup. No imported files, no licences.
   - A real clip at `Resources/Audio/Sfx/<Cue>` (variants `<Cue>_2` … `<Cue>_8`) replaces that cue, and only that cue. Dropping the file in is the whole hookup.
2. **Code-side buses, no AudioMixer asset.** A mixer can only be made in the editor, which breaks "one component, no scene wiring".
   - `AudioLevels` holds Master, Music, Effects, Voice and Mute. Each source plays at master × bus, on a squared (perceptual) curve.
   - Interface clicks follow the Effects slider.
   - A mixer asset can come later if real effects (reverb, snapshots) are wanted.
3. **Voice placeholders are "voice blips":** short synthesized chirps with a signature per operator, so the priority, cooldown and chance rules can be heard before any recording exists. Real `VoiceSet` clips replace them per operator.
4. **Music is a procedural noir loop.**
   - Match: a walking bass, brushes and ride, and sparse electric-piano comping in D minor at 88 bpm, 8 bars.
   - Title (also setup and draft): slower and sparser, 66 bpm.
   - Win: a short brass sting.
   - Files at `Resources/Audio/Music/<Cue>` replace them.
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
    - `SoundBank`: real clips from `Resources`, otherwise synthesis on worker threads, turned into clips a few per frame so startup doesn't stall. It also holds each cue's volume, pitch jitter and least gap between repeats (steps 45 ms, so a four-piece walk isn't a drum roll).
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
