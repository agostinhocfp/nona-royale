# tools/mockup

Still mockups of the board, outside Unity (`docs/design/VISUAL_PASS.md`). Not part of the build.

- `board_texture.py`: a numpy port of the board's procedural sprites (`BoardArt`, `BoardView`), composed in linear light. `python3 board_texture.py new flat out.png` renders the flat board; `PX=110 NOPIECES=1` makes the high-resolution texture the Blender scene uses. `old` renders the pre-G3 look for comparison; `lit` adds approximate room lights.
- `scene.py`: a Blender scene (tested with Blender 4.0 in the cloud workspace) that puts that texture on a table with thickness, stands operator renders up as cards, lights it like the game, and fits the camera to the in-match HUD's free area. `blender --background --python scene.py -- out.png room|void <tilt> <w> <h> <samples>`, with `FIT=hud|title|wide`, `MOCK_DIR` (the texture and its roughness, metal and bump maps) and `ART_DIR` (operator renders). `ROOM=parlour|vault|curtain|bare` picks what stands behind the table (V3), and `CARPET` scales the floor's darkness.
- `post.py`: exposure, bloom, split-tone grade, edge falloff, grain, and optionally the HUD footprint: `python3 post.py in.png out.png [hud] [nograde]`.
- `title.py`: lays the title screen's real lockup (`View/TitleScreen`) over a graded room render, so a room is judged as the title screen: `python3 title.py in.png out.png "label"`, with `LAYOUT=center|left|top` (centre is what the screen does today), `SCRIM`, `TAGLINE`, `FONT` (the game's Cinzel) and `LABEL=0`.
- `sheet.py`: a contact sheet, for comparing a set in one image: `python3 sheet.py out.png <cols> a.png b.png ...`, with `WIDTH` and `GAP`.

`maps.py <dir>` derives the roughness, metal and bump maps from `board_tex.png` in that folder.
