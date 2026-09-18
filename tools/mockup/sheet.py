# tools/mockup/sheet.py
# Lay graded mockups out as one contact sheet, so a set can be judged side by
# side in the chat (VISUAL_PASS.md).
# Usage: python3 sheet.py out.png <cols> in1.png in2.png ...
# Env: GAP (16), BG (0A0709), WIDTH (total sheet width; frames scale to fit)
import os
import sys

from PIL import Image

dst, cols = sys.argv[1], int(sys.argv[2])
srcs = sys.argv[3:]
GAP = int(os.environ.get("GAP", "16"))
BG = os.environ.get("BG", "0A0709")
TOTAL = int(os.environ.get("WIDTH", "2560"))

bg = tuple(int(BG[i:i + 2], 16) for i in (0, 2, 4))
frames = [Image.open(p).convert("RGB") for p in srcs]
rows = (len(frames) + cols - 1) // cols

cell_w = (TOTAL - GAP * (cols + 1)) // cols
cell_h = round(cell_w * frames[0].size[1] / frames[0].size[0])
sheet = Image.new("RGB", (TOTAL, GAP + rows * (cell_h + GAP)), bg)

for i, f in enumerate(frames):
    r, c = divmod(i, cols)
    sheet.paste(f.resize((cell_w, cell_h), Image.LANCZOS),
                (GAP + c * (cell_w + GAP), GAP + r * (cell_h + GAP)))

sheet.save(dst)
print("saved", dst, sheet.size)
