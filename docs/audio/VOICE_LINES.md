# Nona Royale — Voice lines

> Location in repo: `docs/audio/VOICE_LINES.md`
> Status: **Luka written, 2026-09-16.** The other eight operators follow this template.
> Related: `docs/design/AUDIO.md` (decisions 3 and 5, increment AU2), `docs/design/OPERATORS.md` (who each operator is), `docs/audio/PROVENANCE.md` (where every file came from)

## How lines are used

Eight slots, lowest priority first. A higher slot cuts off a lower one; only one operator speaks at a time.

| Slot (file name part) | When it plays | Rules |
| --- | --- | --- |
| `Move` | The operator walks forward | 30% of moves; 3.5 s per-operator cooldown |
| `Deploy` | The operator rises onto the board | Cooldown |
| `HitTaken` | The operator is hit and still standing | Cooldown |
| `Cast` | The operator uses any ability | Cooldown |
| `Kill` | The operator's action knocks someone out | Always; plays straight after the victim's death line |
| `Death` | The operator is knocked out | Always |
| `Victory` | The operator's seat wins; the last one home speaks | Always, over the win sting |
| `Quit` | A live match is abandoned from the pause menu | Always |

A slot is not tied to one ability, so a `Cast` line has to fit all three of an operator's abilities.

## Files

- **Where:** `Assets/_Project/Audio/Resources/Audio/Voice/` (create the folder).
- **Names:** `<Operator>_<Slot>`, then `_2`, `_3` … `_8` for variants: `Luka_Kill.wav`, `Luka_Kill_2.wav`, `Luka_Kill_3.wav`. The operator part is ASCII letters and digits only (Revú would be `Revu`). Variants are read until the first missing number.
- **All or nothing per operator.** As soon as one file exists for an operator, the game stops using that operator's synthesized placeholder, and any slot without a file is silent. Deliver an operator's set in one go.
- **Format:**
  - mono WAV, 44.1 or 48 kHz, 16 or 24-bit;
  - trimmed to within 20 ms of the first sound, with no silence at the end;
  - dry, with no reverb or music under it;
  - peaks at −1 dBFS, and every line in a set at the same loudness.
- **Length:** 0.4–2 s. Keep `Move` under 0.8 s, since it plays in the middle of play. `Victory` can run to 2.5 s.
- **Unity import:** Load Type **Decompress On Load**, Compression **Vorbis** (quality about 70), Force To Mono on.
- **Provenance:** one row per file in `PROVENANCE.md`: who voiced it or which tool generated it, the voice used, the date, and the licence or terms. If you generate lines, use a designed voice, never a clone of a real person without their written consent.

## Luka

**Who he is** (`OPERATORS.md`): a bare-knuckle fighter from the house's fight nights, born in the Caucasus, working through a list of names from the night the house fixed his fight. Nobody pays him; to him it isn't a vendetta, it's a list.

**Voice direction:**
- Late twenties to thirties, mid-low register, a dry, slightly rough voice.
- A light Caucasus accent, never a caricature.
- Few words, flat and certain. He doesn't taunt and doesn't shout.
- The only heat is in the kill lines, and even there it's quiet.
- Think of a fighter talking between rounds, not an announcer.

**Energy by slot** (1 is under the breath, 5 is full voice): Move 1 · Deploy 2 · Cast 2 · HitTaken 3 · Kill 3 · Death 3 · Victory 4 · Quit 1

| File | Line | Direction |
| --- | --- | --- |
| `Luka_Deploy` | "Next name on the list." | Stands up, taping the last knuckle |
| `Luka_Deploy_2` | "Tape's on." | Short, matter-of-fact |
| `Luka_Deploy_3` | "The house knows I'm here." | Quiet satisfaction |
| `Luka_Move` | "Moving." | Barely voiced |
| `Luka_Move_2` | "Closer." | To himself |
| `Luka_Move_3` | "Keep your eyes on the door." | Low, a warning to nobody |
| `Luka_Move_4` | *(a short breath through the nose)* | Non-verbal |
| `Luka_Cast` | "You never saw me." | Close to the mic, almost a whisper |
| `Luka_Cast_2` | "Look away." | Flat command |
| `Luka_Cast_3` | "Now." | One sharp word |
| `Luka_HitTaken` | *(grunt)* "That's it?" | Pain first, then contempt |
| `Luka_HitTaken_2` | "I've taken worse." | Through the teeth |
| `Luka_HitTaken_3` | *(sharp exhale, teeth together)* | Non-verbal |
| `Luka_Kill` | "Crossed off." | Quiet, final |
| `Luka_Kill_2` | "One less name." | A little heat |
| `Luka_Kill_3` | "Stay down this time." | Leaning over the body |
| `Luka_Death` | "Not… tonight." | Breath cut short |
| `Luka_Death_2` | *(exhale)* "Back to the table." | Resigned, almost amused |
| `Luka_Death_3` | "The list… keeps." | Fading |
| `Luka_Victory` | "Tell the house: the book is closed." | Slow, every word placed |
| `Luka_Victory_2` | "Pay the winner." | The fight-night call, turned on them |
| `Luka_Victory_3` | "Every name. Every one." | Quiet triumph |
| `Luka_Quit` | "Another night, then." | Walking away |
| `Luka_Quit_2` | "The list will keep." | Unbothered |

That is 24 files, with 2–4 variants per slot. The death and kill lines are heard most often, so they get three each.

## Template for the other operators

Copy the Luka section. For each operator:
- write two lines of who they are (from `OPERATORS.md`) and the voice direction;
- give an energy per slot;
- write 2–3 lines per slot, 3–4 for `Move`, with one non-verbal line among them;
- make each `Cast` line fit all three of that operator's abilities;
- keep the house (Bouncer, Kurbyn, Javi, Sanity) formal and professional, the contractors paid and unbothered, and Luka personal.

Until an operator has files, the game voices them with a synthesized babble whose pitch and pace come from `VoiceBlips.SignatureOf`.
