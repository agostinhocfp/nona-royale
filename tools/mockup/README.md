# tools/mockup

Still mockups of the board, outside Unity (`docs/design/VISUAL_PASS.md`). Not part of the build.

- `board_texture.py`: a numpy port of the board's procedural sprites (`BoardArt`, `BoardView`), composed in linear light. `python3 board_texture.py new flat out.png` renders the flat board; `PX=110 NOPIECES=1` makes the high-resolution texture the Blender scene uses. `old` renders the pre-G3 look for comparison; `lit` adds approximate room lights.
- `scene.py`: a Blender scene (tested with Blender 4.0 in the cloud workspace) that puts that texture on a table with thickness, stands operator renders up as cards, lights it like the game, and fits the camera to the in-match HUD's free area. `blender --background --python scene.py -- out.png room|void <tilt> <w> <h> <samples>`, with `FIT=hud|title|wide`, `MOCK_DIR` (the texture and its roughness, metal and bump maps) and `ART_DIR` (operator renders).
- `post.py`: exposure, bloom, split-tone grade, edge falloff, grain, and optionally the HUD footprint: `python3 post.py in.png out.png [hud] [nograde]`.

`maps.py <dir>` derives the roughness, metal and bump maps from `board_tex.png` in that folder.
