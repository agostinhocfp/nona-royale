# MOBILE.md — the upright HUD

*Phase M, 2026-09-20. The target device is a Samsung Galaxy S26 Ultra held
upright: roughly 1440×3120 physical pixels at about 500 dpi, a punch-hole
camera at the top and a gesture bar at the bottom. Everything here is written
for that shape and checked against it, but nothing is written for that phone:
the rules are "portrait" and "touch", not "Samsung".*

The HUD before this phase was built for a wide window and only for a wide
window — a 220-unit rail down the left, a 78-unit strip down the right, a bar
across the top and a 150-unit tray across the bottom. Put that on a phone held
upright and the two side panels alone eat 62% of the width. Nothing was broken;
it was simply drawn for a screen the player does not have.

The whole phase is six decisions.

---

## M1 — One place decides the shape of the screen

`ScreenLayout` is static, sampled once a frame from `HudRoot`, and answers four
questions: is the window upright, how big is the safe area, is the pointer a
finger, and what reference resolution should the canvas be on. It ticks a
`Version` whenever an answer changes; every layer watches that and rebuilds
itself, and `MatchBootstrap.FrameCamera` re-solves the board's framing.

**Portrait is aspect < 0.88, and it is left again at 0.96.** Not 1.0 either
way: a square-ish window has the width for the side panels and reads better
with them, and the gap between the two thresholds is what stops a window
dragged across the line from rebuilding the HUD twice on the way.

**The reference resolution is 480×1040 upright, against 1920×1080 wide, and
the scaler expands rather than matching an axis.** Expand takes the smaller of
the two ratios, so the canvas is never smaller than the reference on either
axis and no widget can be cut off by a screen that is the wrong shape; a short,
wide portrait window — a tablet — gets extra canvas width rather than larger
widgets.

480 is not arbitrary. On a 1440-pixel-wide phone at ~500 dpi the screen is
about 463 Android dp across, so one canvas unit lands at just under one dp.
Every size already in the HUD then means what it says on the platform's own
scales: a 48-unit button is a 46 dp tap target, 18-unit body text is 17 dp
type. There is no second set of numbers to keep in step.

## M2 — The chrome is inset; the picture is not

`androidRenderOutsideSafeArea` stays on, because the room, the felt and the
lighting should run edge to edge and under the cut-out. What must not run under
it is anything a player reads or presses.

So `HudRoot` builds the canvas full-screen and then parents every layer inside
a rect pinned to `Screen.safeArea`. All the existing anchoring code is
unchanged and lands inside the safe area for free. `FrameCamera` adds the same
insets, in pixels, to what the panels reserve, so the board is fitted into the
same rectangle the chrome is — a cell never ends up behind a camera hole.

## M3 — Upright, the side panels lie down

| | wide | upright |
|---|---|---|
| top bar | one row, 56 | two rows, 96 — state over the turn's instruction |
| squad rail | column, 220 wide | band, 62 tall, under the top bar, scrolled sideways |
| history | strip, 78 wide | rail, 62 tall, above the tray, scrolled sideways |
| action tray | one row, 150 tall | block, 292 tall — aim line, dice + operator, abilities, Cast |
| turn button | board's bottom-right corner | screen's bottom-right corner, in a slot the tray leaves |

That leaves 528 units of height and the full 480 of width for the board, which
the framing fills at about 428×428 — the same 12% of air around it that the
wide layout keeps.

Two things are given up on purpose. The squad rail's band shows **your** seat's
three operators as tappable chips and each CPU seat as one folded group; a CPU
seat does not open upright, because its folded form already carries colour,
name, the three health-tinted silhouettes and the home count, and the rest was
only ever a convenience. And the draft's cards lose their three ability lines
upright, because at a third of a phone's width there is no room for them — they
move to the sheet a tap opens (M6).

The turn button is the one control that moves rather than reshapes. Over a
board framed edge to edge it would sit on playable cells, so upright it takes
the screen's own corner, and the tray holds a 168-unit slot open beside Cast so
the two cannot overlap. Both live controls end up under the thumb.

## M4 — A finger is a different pointer, not a smaller one

- **`EventSystem.IsPointerOverGameObject()` asks about pointer −1, the mouse.**
  No touch is ever that, so on a phone it answered "no" for every tap and every
  press on the tray fell through to the board underneath as well.
  `BoardPointer.IsOverHud` now asks with the finger's own id.
- **`Input.mousePosition` keeps reporting where a finger last was.** The hover
  test therefore left a piece lifted and a rail row washed for the rest of the
  turn. With no touch down, nothing is hovered.
- **The history chip's card is a tap, not a hover** — and only a tap: touch
  raises enter and exit around a press, so wiring both would flash the card
  under the finger.
- **The draft is tap to look, then press a button to pick** (M6). Reading an
  operator's abilities before taking it has to be possible, and two identical
  taps with very different consequences is not the way to buy that.
- **Key hints are dropped when there is no keyboard** — `Space`, `E`, `Esc`,
  `Enter`, the ability cards' 1–3 — and the prompts are rewritten as taps.
  `ScreenLayout.Key` / `KeyMarkup` / `WithKey` are the one filter.
- **Holding a finger hurries a CPU turn,** the way holding Space does.
- **No cursor is dressed on a touch screen.**

A laptop with a touchscreen keeps its mouse, so the test is "touch and no
mouse", never "touch".

## M5 — Every full-screen card fits the screen it is on

The cards were columns that grew to whatever they held: fine at 1080 units of
height, wrong on a phone, where the settings page is taller than the screen and
the rows past the fold could not be reached at all.

`ModalCard` and `PauseMenu` now put their column inside a viewport and take the
smaller of what the content wants and what the screen has, scrolling the rest —
and only scrolling when there is something to scroll, because a card that can
be dragged an inch when it all fits reads as broken. Widths are clamped to the
reference width less a margin. The title screen's menu stacks its three buttons
instead of setting them side by side; the results table drops the squad column,
which is the widest thing in the row and the only one the board has already
shown; the log overlay covers the board instead of opening into a column that
is not there; and the draft screen stops scaling its 1840-unit frame — at phone
width that lands near a quarter size, where nothing can be read.

## M6 — The draft, rebuilt rather than re-stacked

Turning the draft's side column on its side was not enough, and the arithmetic
says why. Stacked under the pool, its four seat rows and its detail panel wanted
about 510 units of height in the 250 that were left — so the detail, which is
exactly where an upright card's ability lines had been sent, was squeezed to
nothing. Each half needed its own answer.

**The seats became a strip of four tiles.** A seat row wanted 510 units of
width — a gem, a name and three 150-unit slots — in a 456-unit frame. A tile
carries the same seat in 106: colour, name, what it is doing, and its three
slots as pips. The slots lose the operator names they were printing, which the
pool above is already showing, and keep the one thing a slot must do: a filled
pip still clears on a tap in ALL PICK.

**The detail became a bottom sheet with the PICK button on it.** That is what
pays for the ability lines the tile cannot hold — one tap opens the operator in
full, over the lower half of the pool, and the pick is made from a labelled
button rather than by tapping the tile a second time. The sheet's text scrolls,
because three abilities and their descriptions run past its height and a mask
alone would cut the last one off silently. `Esc` closes the sheet before it
arms anything else, since it is the innermost thing open.

**This path is taken upright whether or not the pointer is a finger**, so the
editor at phone size behaves exactly as the phone does rather than quietly
taking the mouse's route. Hovering a card upright does nothing: a sheet that
opened under a passing mouse would be unusable.

**The footer wraps onto two rows** and drops its prose note. Five buttons need
about 950 units side by side; the header's subtitle already carries the state,
and the rules belong on the setup screen, not under a running clock.

Upright the screen now budgets: header 100, seat strip 76, pool 526 (twelve
operators as 145×124 tiles, three across), footer 104, spacing 30 — 836 of the
~942 the frame has inside the safe area.

---

## Player settings

*Revised the same day, after the first build on the device came up stretched
and reporting itself as not upright. The layout code was right — the editor at
1440×3120 was correct — so the fault was below it, in how the surface is sized
and rotated. Three settings changed together, listed in the order they are
worth suspecting.*

- **Resolution scaling is Disabled.** It was briefly Fixed DPI at 400, on the
  reasoning that a 2D board does not need 4.5 million pixels. Fixed DPI is also
  the one setting that sizes the render surface by hand, Unity's own tracker
  carries reports of it misbehaving on Android, and a hand-set surface that
  does not match the window is exactly what a stretch looks like. The
  optimisation was not asked for and is not worth a rendering bug; if it is
  wanted later, it should be measured on the device, on its own.
- **Vulkan pre-transform is on.** Android graphics API is Auto, which on this
  device picks Vulkan. With pre-transform off, the swapchain is left in the
  panel's native orientation and the compositor rotates it — which costs
  performance and, on some drivers, comes out wrong.
- **The orientation is Portrait, not auto-rotation.** A locked orientation has
  no rotation event to get wrong, and the app is upright from frame zero. This
  is the reversible one: `defaultScreenOrientation` back to `4` restores
  auto-rotation, and the HUD handles both arrangements either way — the setting
  is mobile-only, so the desktop build is unaffected. Upside-down portrait
  stays off regardless; it puts the speaker and the camera at the bottom.
- `androidRenderOutsideSafeArea` stays on (M2). The legacy Input Manager stays
  the active handler; `StandaloneInputModule` raises touches as pointer events,
  so the whole HUD is tappable without a second input path.

## What is not done

- The tilted board camera is left as a player setting upright. A tilt that
  cannot be solved at a 0.46 aspect falls back to the flat camera, which is the
  existing behaviour and is correct; whether the tilt is *wanted* on a phone is
  a design question, not a layout one.
- The dev panel (`ControlPanel`) draws over the board upright rather than
  reserving a column. It is a debugging tool and it is not skinned.
- The history rail drops its entries when the screen turns. They are a record
  of what has been shown, not state, and the next batch refills the rail.
- The draft's numbers above are arithmetic, not a screenshot. Type advances and
  wrapping are the part a layout pass cannot prove; the pool, the strip and the
  sheet all want a look on the device.
