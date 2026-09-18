# tools/mockup/maps.py
# Derives the Blender scene's roughness, metal and bump maps from the board texture.
# Usage: python3 maps.py <dir containing board_tex.png>
import sys
import numpy as np
from PIL import Image

d = sys.argv[1].rstrip("/") + "/"
im = np.asarray(Image.open(d + "board_tex.png").convert("RGB")).astype(float) / 255
r, g, b = im[..., 0], im[..., 1], im[..., 2]
mx = im.max(-1)

# Gold: warm hue and bright enough.
gold = ((r > g) & (g > b) & (r - b > 0.22) & (mx > 0.3)).astype(float)

# Felt: inside the four table circles (standard 15-cell board, table 16.6 cells across).
H = im.shape[0]
ys, xs = np.mgrid[0:H, 0:H]
S = 16.6
X = (xs + .5) / H * S - S / 2
Y = S / 2 - (ys + .5) / H * S
felt = np.zeros_like(r)
for cx, cy in [(-4.5, 4.5), (4.5, 4.5), (-4.5, -4.5), (4.5, -4.5)]:
    felt = np.maximum(felt, (np.hypot(X - cx, Y - cy) < 2.5 * 0.875).astype(float))
felt *= 1 - gold

rough = np.full_like(r, 0.32)
rough = np.where(felt > 0, 0.92, rough)
rough = np.where(gold > 0, 0.22, rough)

lum = 0.2126 * r + 0.7152 * g + 0.0722 * b
Image.fromarray((rough * 255).astype(np.uint8)).save(d + "board_rough.png")
Image.fromarray((gold * 255).astype(np.uint8)).save(d + "board_metal.png")
Image.fromarray((np.clip(lum * 3, 0, 1) * 255).astype(np.uint8)).save(d + "board_bump.png")
print("maps written to", d)
