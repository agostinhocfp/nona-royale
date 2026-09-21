# Nona Royale — Sound-effect prompt pack

> Location in repo: `docs/audio/SFX_PROMPTS.md` · Draft v1, 2026-09-21 (AU3)
> Related: `docs/design/AUDIO.md` (decision 6, AU3), `PROVENANCE.md` (one row per file that ships), `MUSIC_PROMPTS.md` (the Lyria counterpart), `Unity/Audio/AbilitySounds.cs` (every slug)

## How a file gets into the game

1. Generate several takes of a prompt below, pick the best, and keep the raw takes in `Claude outputs/sfx/<Slug>/`.
2. Name the pick `<Slug>_<Moment>` (for example `Bouncer_VelvetRope_Tell.wav`, then `_2`, `_3` for variants) and drop it in `Assets/_Project/Audio/Resources/Audio/SFX/Abilities/`. That is the whole hookup: the file replaces the stand-in for that moment only.
3. Add a row to `PROVENANCE.md` **before** it ships.
4. Claude trims, darkens and levels it, the same way the Kenney files were done: the sound starts within 2 ms of the file, ends in silence, and matches the generic cue it replaces by A-weighted loudness (about −18 dBA over the loudest 50 ms).

**Moments:** `Tell` plays with the cast tell on the caster. `Impact` plays when the effect lands on an enemy, in place of the generic hit (once per cast, however many pieces it hits). `Assist` is the ally mode: it plays over the heal cue. The damage-type layer (Tech fizz, Atomic sub drop) is added by the game, so **don't put a boom under a prompt for an Atomic ability**; the game adds one.

## The tools (checked 2026-09-21)

- **ElevenLabs Sound Effects:** text to sound, up to 30 s, a loop toggle, 48 kHz WAV out. Not on the free plan. A third-party guide says output from paid plans is cleared for commercial use in games; confirm it in ElevenLabs' own terms before release. Set the duration yourself (the lengths below): left on auto, it pads.
- **Adobe Firefly, Generate Sound Effects:** describe the sound, or **perform it with your voice** and it follows your timing. Use that for anything with a rhythm (Velvet Rope's plates, Tagged From Above's two clicks). Outside enterprise plans, get the scope of "universally licensed" in writing.
- **Not:** Meta AudioGen / AudioCraft (weights licensed for non-commercial use), and the 8-bit generators (jsfxr, ChipTone): the toy sound AU1d removed.

## House style for every prompt

End every prompt with this line; it keeps the takes in the AU1d palette:

> Close-miked foley in a quiet, dry room, no music, no melody, no musical notes, no reverb tail, no voice, single sound effect.

Camp materials, so a player can name the camp with their eyes closed:

| Camp | Material | Never |
| --- | --- | --- |
| House (Bouncer, Kurbyn) | Aged brass, hydraulics, heavy mechanism; Kurbyn's rig is electrical | Sci-fi lasers, synth zaps |
| Contractors (Syla, Mimi, Javi, Kian, Nuetu, Sanity) | Each their own tech: obsidian glass, cryo, nanites, drones | Chimes, bells, anything that rings in tune |
| Owners (Lethe, Revú, Fortuna) | Money: clay chips, dice, coins, slow clockwork | Cash-register cartoon sounds |
| Luka | Cyan radio interference, scanlines dropping out | Clean digital beeps |

## The alpha three

### 1. Bouncer — Velvet Rope (`Bouncer_VelvetRope`)

**Tell** (0.6 s):
> Six heavy aged-brass plates unlocking one after another down a mechanical arm, each clack faster than the last, over a hydraulic hiss that swells and bleeds off, ending in one solid thunk.

**Impact** (0.8 s):
> A heavy brass clamp latching shut on a body, then a person dragged a short way across a felt card table, cloth under weight, a little grit.

### 2. Bouncer — All-In Mauling (`Bouncer_AllInMauling`)

**Impact** (0.4 s, make 3 takes):
> A single bare-knuckle punch to the torso of a big man, knuckles on cloth, a deep chest thump, short, dry and brutal.

**Assist** (0.35 s):
> A heavy open-palm clap on a man's shoulder through a thick suit jacket, then a grip on the cloth.

### 3. Syla — From the Hip (`Syla_FromTheHip`)

**Tell** (0.3 s):
> A small shard of black glass flicked from the wrist, a snap and a thin, fast cut through the air.

**Impact** (0.2 s, make 2 takes):
> A small shard of dark obsidian glass striking cloth and skin, one short dull tick, no ring.

### 4. Syla — Ace Shards (`Syla_AceShards`)

**Tell** (0.3 s):
> Five thin glass blades fanned out quickly like a hand of playing cards, soft flicks of air.

**Impact** (0.45 s):
> A burst of small dark glass fragments scattering outward across a felt table, dense at first then thinning, dull ticks, never chiming.

### 5. Syla — Tagged From Above (`Syla_TaggedFromAbove`)

**Tell** (0.6 s):
> Two soft mechanical lock clicks from a small rifle scope, then one held breath: a short inhale that stops, then silence.

### 6. Kurbyn — Dargin Pulse (`Kurbyn_DarginPulse`)

**Tell** (0.55 s):
> An electrical rig charging up, a low swelling hum with crackle that grows thicker and faster toward release.

**Impact** (0.8 s):
> A low electrical discharge thump, crackling sparks radiating outward and thinning, then a muffled high ringing like ears after a blast.

### 7. Kurbyn — Miracle Pull (`Kurbyn_MiraclePull`)

**Impact** (0.7 s):
> A heavy, dead blow to a body, then electrical crackle splashing outward to either side.

### 8. Kurbyn — Evasive Protocol (`Kurbyn_EvasiveProtocol`, passive)

**Impact** (0.3 s, make 2 takes; plays when he dodges):
> A body slipping sideways out of a blow, a quick rush of air with a brief flicker of electrical static.

## The other nine

Every ability already has a slug (`AbilitySounds.cs`), so a file works as soon as it is named. Write their prompts from the camp table and the operator's device in `docs/design/OPERATOR_LOOKBOOK.md`: Sanity's magnetic grenade, Kian's drone, Luka's Hermes ring, Fortuna's chips and dice, Revú's coin drain, Lethe's clockwork. For Sanity's Zero-Day, save a note for later: its tick should speed up each round, which needs a looped cue the game does not play yet.
