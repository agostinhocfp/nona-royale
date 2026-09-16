# Nona Royale — Music prompt pack (Lyria)

> Location in repo: `docs/audio/MUSIC_PROMPTS.md` (project copy: `claude/MUSIC_PROMPTS.md`) · Draft v1.1, 2026-09-16 (adds Variant C, the "Gentleman Italian Mafia" waltz)
> Related: `AUDIO.md` (decision 4 and AU1b), `TarantellaScore.cs` (the in-game arrangement of "Deborah"), `ART_PROMPTS.md` (the image counterpart)

## The three genres and where each plays

| Slot (file name)        | Plays on                                      | Genre              | Brief                                                                                                             |
| ----------------------- | --------------------------------------------- | ------------------ | ----------------------------------------------------------------------------------------------------------------- |
| `Title`                 | Title, setup, draft, results                  | **Noir jazz**      | After hours at the tables: smoky and patient, never sleepy                                                        |
| `Match`                 | A match in play (and paused, quieter)         | **Tarantella**     | "Deborah" as a Sicilian tarantella, with casino-underworld swagger (Variant C: a "Gentleman Italian Mafia" waltz) |
| `Win`                   | The sting over the music when someone wins    | **Big band**       | A brass payoff, a few seconds long                                                                                |
| _(optional)_ `Showdown` | Not in the game yet (see the end of this doc) | **Big band swing** | Mischievous, brassy, industrial swagger                                                                           |

Finished files go in `Assets/_Project/Audio/Resources/Audio/Music/`, named exactly `Title`, `Match` and `Win` (any extension Unity imports). Each replaces its generated placeholder with no code change.

## What Lyria can and can't do (checked 2026-09-16)

- **Models.**
  - **Lyria 3 Clip** makes 30-second clips; iterate with it.
  - **Lyria 3.5 / Lyria 3 Pro** makes full tracks of a couple of minutes (up to 3 minutes on Vertex); use it for the keepers.
  - **Output:** 44.1 kHz stereo MP3, or WAV on 3.5.
- **It follows** BPM ("126 BPM"), key ("in C minor"), "Instrumental only, no vocals", section tags (`[Intro]`, `[Verse]`…) and timestamps (`[0:00 - 0:10] Intro: …`).
- **It takes up to 10 reference images** (the Luka sheet or the board preview can set the mood). **It takes no audio**, so it can't hear "Deborah". The prompts below describe its melody, key and chords instead, and that gets you the song's shape, not its notes.
- **Artist names** are treated as loose inspiration at best, and prompts asking for a specific artist's voice or copyrighted lyrics are blocked. The prompts below describe the style in words rather than naming the Undermine soundtrack; that gives more control anyway.
- Every output carries a **SynthID** watermark.

## Workflow

1. Iterate on **Lyria 3 Clip** until the sound is right, then run the same prompt on **Lyria 3.5 / 3 Pro** for the full length. Make several takes; keep the best.
2. **Loops.** The prompts ask for no fade-in and no ending, but models add them anyway. In your DAW:
   - cut the loop on bar lines inside the body of the track;
   - add a 5–10 ms crossfade at the seam;
   - export 44.1 kHz WAV.
   - Bar lengths: 126 BPM in 6/8 (dotted quarter) is 0.952 s per bar; 70 BPM in 4/4 is 3.429 s per bar.
3. **Level.** Leave headroom: peak around −1 dBTP, and a moderate loudness (about −16 LUFS integrated). The game mixes music under the effects (Music slider ×0.6 by default).
4. **Unity import for music:**
   - Load Type **Streaming**, Compression **Vorbis** (quality about 70), Load In Background on.
   - The `Win` sting can be **Decompress On Load**.
5. **Provenance before it ships.** Record tool, model, date, prompt, seed and the edits you made, one line per file (for example `docs/audio/PROVENANCE.md`). Check Google's current terms for commercial use under the plan you generate on, and Steam's AI-content disclosure. `AUDIO.md` rule: nothing ships with an unrecorded licence.

---

## 1. `Match`: "Deborah" as a tarantella

The in-game version is at 126 dotted-quarter BPM in C minor; keep the replacement in that feel.

```
Instrumental only, no vocals. A fiery Sicilian tarantella with a noir casino-underworld edge, in C minor, fast 6/8 at 126 BPM counted in dotted quarters, relentless and dancing but dangerous, like a card game that might end in a knife fight.
Instrumentation: a lead mandolin with bright tremolo on long notes, a musette-tuned accordion (the wavering doubled-reed sound) playing the oom-pa-pa accompaniment and doubling melodies, a tamburello frame drum with rolling jingles and palm-struck skin accents on beats one and three, a plucked upright bass on the downbeats, and a clarinet answering the mandolin. Occasional low brass stabs add menace.
Harmony: C minor with a dominant G7 using B natural (harmonic minor colour). Main progression: C minor for four bars, F minor for two bars, A-flat major, then G7. The middle section cycles F minor, A-flat, G7, G suspended.
Melody: a restless hook that circles the tonic in the upper register, C, C, D, E-flat, F, D, C, E-flat, C, repeated with small variations, answered by a lower phrase C, D, E-flat, A-flat, G, C. Dotted, lilting siciliana rhythm inside the 6/8.
[0:00 - 0:08] Intro: accordion and tamburello alone, establishing the groove.
[0:08 - 0:40] A: mandolin plays rising and falling C minor arpeggios over the main progression.
[0:40 - 0:55] B: the accordion takes the lower melody, mandolin accompanies.
[0:55 - 1:25] C: the mandolin plays the high hook with tremolo; the tamburello shakes continuously; brass stabs on the F minor chords.
[1:25 - 1:45] B again, the mandolin doubling the accordion an octave higher, building energy.
[1:45 - 2:00] Turnaround on G7 straight back into the groove, no ending, no fade-out, steady tempo throughout so it can loop.
Warm, close, dry live-room recording, as if played in a smoky back room of an old casino.
```

**Variant B (tarantella meets big band).** This bridges it to the Win sting: replace the instrumentation paragraph with this one.

```
Instrumentation: lead mandolin with tremolo and a musette accordion carry the melody, driven by a full big band underneath: tight trumpet and trombone section stabs on the off-beats, baritone saxophone doubling the bass line, tamburello and a swinging drum kit on brushes locked to the 6/8.
```

**Variant C ("Gentleman Italian Mafia").** The old-world Sicilian crime family: a slow minor-key waltz. Elegant, sorrowful and menacing, where the tarantella is fiery. It keeps "Deborah"'s key, chords and melody shapes. The prompt describes the sound and avoids naming any film score, so Lyria can't lean on a famous theme; that also keeps the result yours to use.

```
Instrumental only, no vocals. An elegant, old-world Sicilian waltz for a gentleman crime family, in C minor, 3/4 at 96 BPM with a steady, dignified pulse. Mood: sorrowful, noble and quietly menacing; respect, loyalty and a threat never spoken aloud; candlelight in a private back room of a 1930s casino.
Instrumentation: a solo mandolin with slow, expressive tremolo carries the melody; a warm solo trumpet answers it, mournful and restrained; a small string section (violins, violas, cellos) sustains the harmony and swells on the phrase endings; a musette accordion breathes the waltz accompaniment (bass note on one, soft chords on two and three); a nylon-string guitar picks gentle arpeggios; a clarinet in its low register doubles the counter-melody; a plucked upright bass and a soft timpani roll under the climaxes. No drum kit.
Harmony: C minor with a dominant G7 using B natural. Main progression: C minor, C minor, F minor, F minor, A-flat major, A-flat major, G7, G7. The middle section: F minor, A-flat major, G7, G suspended, resolving back to C minor.
Melody: a slow, singing line that circles the tonic in long held notes, C, D, E-flat, F, then falling back through D to C, answered by a lower phrase C, D, E-flat, A-flat, G, C. Each phrase ends on a held note with mandolin tremolo.
[0:00 - 0:12] Intro: guitar arpeggios and accordion alone, establishing the waltz.
[0:12 - 0:45] A: the mandolin plays the melody over strings and waltz accompaniment.
[0:45 - 1:10] B: the solo trumpet takes the lower phrase, the strings answer.
[1:10 - 1:40] C: full ensemble, strings swelling, mandolin and trumpet in harmony, a timpani roll into the peak, then pulling back to a hush.
[1:40 - 2:00] The mandolin and accordion alone again, leading straight back to the start: no ending, no fade-out, no ritardando, steady tempo so it can loop.
Warm, intimate, cinematic recording with a natural hall reverb, lush but restrained.
```

**Same variant for the menus.** Use this prompt with "3/4 at 84 BPM" for `Title` instead of the noir jazz. A slower mafia waltz on the menus, then the tarantella in the match, reads as one family: the same key, the same melody, the dance speeding up once the game starts.

**Game fit.** The waltz can loop, but it's calmer than the tarantella. In a match, turns take seconds and CPU turns chain together, so the tarantella keeps more energy. The waltz fits the title and draft best.

## 2. `Title`: noir jazz

The in-game placeholder is in D minor, currently at 88 BPM; a slower tempo suits the menus better.

```
Instrumental only, no vocals. Slow noir jazz for the title screen of a stylish 1930s Art Deco casino-underworld game, in D minor at 70 BPM, 4/4 with a lazy swing.
Mood: after hours, smoky, elegant, quietly dangerous; the calm before the game begins. Never sleepy, never happy.
Instrumentation: a muted trumpet (harmon mute) carrying a sparse, lonely melody, a breathy tenor saxophone answering it, brushed snare and soft ride cymbal, a warm walking upright bass, and a Rhodes electric piano with gentle tremolo playing minor ninth chords. A distant vibraphone adds glints of light.
Harmony: D minor add 9, B-flat major 7, G minor 9, A7 suspended resolving to A7 flat 9, then back to D minor.
Structure: [Intro] Rhodes and bass alone for four bars. [Theme] muted trumpet melody over brushes. [Answer] tenor sax takes the phrase, trumpet comps softly. [Theme] trumpet returns with vibraphone. No fade-in, no ending, constant tempo, so it can loop seamlessly.
Intimate, warm, analogue recording with a little room reverb, like a late-night jazz club.
```

## 3. `Win`: big band sting

Generate as a Clip, then cut the best 5–8 seconds.

```
Instrumental only, no vocals. A short, triumphant big band jazz sting for winning a high-stakes casino game, in C major resolving from C minor, about 8 seconds long.
Full big band: a rising trumpet section fanfare, punchy trombone slides, a baritone saxophone growl underneath, a snare drum fill into one massive final chord (C major 6/9) held with a cymbal crash and a trumpet shake on top, then a crisp cut-off.
Brassy, bold, slightly mischievous, 1930s swagger with modern punch. Starts instantly, no intro.
```

**Two alternatives worth generating:**

- _Tarantella flavour:_ "…a mandolin tremolo flourish and an accordion glissando into the final chord…"
- _Noir flavour:_ "…a muted trumpet phrase answered by the full band's final hit…"

## 4. _(Optional)_ `Showdown`: big band swing

This is the brassy, mischievous, industrial swagger of the soundtrack you mentioned, described in words.

```
Instrumental only, no vocals. High-energy big band swing with a mischievous underworld swagger and an industrial edge, in F minor at 160 BPM, 4/4 swing.
Mood: a heist going loud; cocky, tense, fun, a crime syndicate throwing a party in a machine-filled vault.
Instrumentation: a punchy trumpet and trombone section trading riffs with a saxophone section, a growling baritone sax, a hard-swinging drum kit riding the cymbal, a walking upright bass, stride piano accents, and clanking metallic percussion like gears and pipes woven into the groove. Short brass shouts and plunger-muted trumpet wah effects.
Structure: [Intro] drum break and bari sax riff. [Head] brass section melody with call and response. [Solo] growling trumpet over stop-time hits. [Shout chorus] full band at maximum energy. No fade-out, steady tempo, loopable.
Big, bright, modern-sounding recording with a vintage big band arrangement.
```

**Where it could go.** The game has no slot for it yet. A small code change (a new `MusicCue` and a trigger) could play it:

- on the **draft** screen, which is energetic before the match; or
- as the **final stretch** of a match, once any seat has two operators home.

Say which and it goes into Stage 5.

---

## Things to feed Lyria alongside the text (optional)

- **Mood images:** `Claude outputs/board_preview.png` and `luka_hero_threequarter.png`.
- **A lead sheet for "Deborah":** Lyria 3 Pro on Vertex accepts PDFs. A melody-and-chords sheet made from `deborah_tarantella.mid` might nudge the melody closer. That's an experiment; don't expect note accuracy.
