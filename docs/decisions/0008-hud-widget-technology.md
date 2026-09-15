# ADR-0008 — HUD widget technology: uGUI (Canvas)

- **Status:** Proposed (accept when the first uGUI increment lands)
- **Date:** 2026-09-15
- **Location:** `docs/decisions/0008-hud-widget-technology.md`
- **Relates to:** PRESENTATION §2 (the requirements table), §6 ("OnGUI… replaced by a real UI when there is something worth dressing" — this ADR is that sentence coming due), §7 (open items on scaling and layout), ADR-0004 (view holds no rules), the HUD phase brief of 2026-09-14 (session C).

## Context

The debug tray is IMGUI (`OnGUI` in `MatchBootstrap`). It answered the only question the build existed to answer — is the game fun — but PRESENTATION §7 lists what it cannot do: fixed 330px pixel width, no reflow, no resolution scaling. The largest gap against PRESENTATION §2 is enemy health: it is visible only inside a target list, only mid-cast. Closing that gap wants elements anchored to pieces on the board, which IMGUI has no story for.

The mandate is minimal: a stranger must be able to play unexplained. Not the art pass — but the widget system chosen here is the one the art pass will skin, so it is chosen once.

## Options considered

**(a) Stay IMGUI, restructure only.** Cheapest now. Solves nothing in §7, has no per-piece anchoring, and pushes the real migration into the art pass — the worst possible time, when layout, skinning and tech would all move at once.

**(b) uGUI (Canvas).** The mainstream Unity runtime UI. World-space and screen-space anchoring for per-piece elements is idiomatic; tutorial coverage is the deepest of the three (this is a first Unity project); the art pass skins it directly with sprites from the Scenario pipeline (ART_PIPELINE).

**(c) UI Toolkit.** Unity's strategic direction; USS styling is familiar to a web developer. But runtime gameplay-HUD ergonomics — world-anchored elements above pieces in particular — remain its weak flank, and community answers for game HUDs still skew uGUI. The styling familiarity buys less than it appears: the hard parts of this HUD are anchoring and pointer-event flow, not styling.

## Decision

**uGUI.** Reasoned, not measured. The deciding factors are the per-piece health elements (the single biggest §2 gap) and tutorial density for a Unity newcomer. If (c) matures on world-space HUDs, revisiting is possible because the view holds no rules (ADR-0004) — the cost of a future swap is layout work, never logic.

## Consequences

1. **`NonaRoyale.Unity.asmdef` gains assembly references** to `UnityEngine.UI` and `Unity.TextMeshPro` (uGUI is the `com.unity.ugui` package; TMP is its text solution — legacy `UI > Text` is not used). The assembly names are the same whether TMP ships inside `com.unity.ugui` (Unity 6) or as `com.unity.textmeshpro` (2022 LTS).
   **TMP Essential Resources must be imported once** (`Window > TextMeshPro > Import TMP Essential Resources`) and committed. A `TextMeshProUGUI` created from code takes its font from `TMP_Settings`, which lives in those resources; the editor prompts for the import only when a TMP component is added by hand, so a code-only HUD fails at runtime instead. This is the one imported asset the HUD depends on — a font, not art, so §6's "no art assets" stands.
2. **Everything is still created from code.** MatchBootstrap's contract — one component on an empty GameObject, no prefabs, no scene wiring — is kept. The HUD scaffold builds its own `Canvas`, `CanvasScaler` and `EventSystem` at runtime. The `EventSystem` is created only if none exists, so a scene that later gains one does not end up with two.
3. **Reference resolution 1920×1080**, `CanvasScaler` in Scale-With-Screen-Size, match 0.5. This closes PRESENTATION §7's "does not scale with resolution" item; edit the doc when the scaffold lands.
4. **The panel-rect click guard in `HandleBoardClick` is replaced** by the EventSystem's pointer-over-UI check. The guard's _reason_ (a board click through a button must not re-aim a strike) transfers; the rect arithmetic does not. **While both HUDs coexist (consequence 6), both guards apply** — the EventSystem cannot see IMGUI, so the rect guard still protects the OnGUI panel whenever it is visible. The rect guard is deleted in the same commit as the OnGUI path, not before.
5. **`FrameCamera`'s `PanelWidth` contract is renegotiated.** Its remarks (camera shifts _away_ from the panel; width is sized for, not just height) are paid-for lessons and transfer to whatever width the new panel reserves. The new width is in reference units, not pixels: `FrameCamera` takes it from the panel's `RectTransform` times the canvas `scaleFactor`, never from a constant.
6. **Removal order** (from the phase brief, binding): new HUD alongside the OnGUI panel behind the existing Tab toggle → stranger test passes on the new HUD → the OnGUI path deleted in one commit, logged. Never piecemeal.
7. **Input stays on the legacy Input Manager** (confirmed 2026-09-15). The whole view uses `UnityEngine.Input`, and a hot-seat mouse-and-keyboard game collects none of the new Input System's wins (rebinding, gamepads, device pairing). The runtime `EventSystem` therefore uses `StandaloneInputModule`. Revisit trigger: gamepad or mobile becomes a target.
8. **Zero core changes expected.** A HUD need that seems to require a core change is a missing query on `GameEngine`, added beside `LegalCellsFor`/`PreviewLandings` (PRESENTATION §1).
9. **Display-only graphics do not catch the pointer.** Every uGUI graphic that is not a control — per-piece health bars, status badges, labels, backgrounds that exist only to be seen — is created with `raycastTarget = false`. Otherwise a health bar floating over a piece makes the pointer-over-UI check (consequence 4) true, and the piece under it stops being clickable. This is the pointer-event flow named in option (c) as the hard part; it fails silently, so it is a creation-time default, not a per-widget decision.

## Revisit order

Three axes move in this phase — technology (this ADR), layout (what reserves screen edges), visibility (what is always-on vs on-demand). They will not be separable afterwards. If the phase fails the stranger test, question them in reverse: visibility first (cheapest to change), layout second, technology last — the tech choice is only wrong if per-piece anchoring or pointer-events themselves fight back, not if a label is in the wrong place.
