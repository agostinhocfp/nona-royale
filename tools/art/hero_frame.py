# tools/art/hero_frame.py
# Cuts the hero frame out of the designer's hero-frame sheet (2026-09-29) into
# the two sprites HeroPortrait loads from Resources/Art/Hero/:
#   hero_frame.png      the frame, background removed, the inside of the gold ring clear
#   hero_frame_lit.png  the cyan arcs alone, taken from the sheet's lit frame, aligned to it
# Both are 640x640, centred on the gold ring.
#
# Usage: python tools/art/hero_frame.py <sheet.png> <out_dir>
# Needs numpy, scipy, opencv-python, pillow. The crop boxes, the ring and the
# lit frame's offset were measured on the 1536x1024 sheet; a new sheet needs
# them measured again (the script prints the fit).
import sys
import numpy as np, cv2
from PIL import Image
from scipy import ndimage as ndi

UNLIT_BOX = (790, 0, 1440, 545)   # the sheet's big frame without the cyan arcs
LIT_DX = 717 - 2                  # the big lit frame sits this far left, measured by alignment
N = 640                           # output canvas

sheet, out = sys.argv[1], sys.argv[2]
full = np.asarray(Image.open(sheet).convert('RGB')).astype(np.float32)
x0, y0, x1, y1 = UNLIT_BOX
crop = full[y0:y1, x0:x1]
h, w = crop.shape[:2]

# 1. The silhouette: flood the dark, smooth background in from the border;
#    the frame's bevelled edges stop it.
lum = crop.mean(-1)
grad = np.hypot(cv2.Sobel(lum, cv2.CV_32F, 1, 0, ksize=3), cv2.Sobel(lum, cv2.CV_32F, 0, 1, ksize=3))
free = ~((grad > 28) | (lum > 60))
lab, _ = ndi.label(free)
border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
fg = ndi.binary_fill_holes(~np.isin(lab, list(border)))
fg[:22] = False                   # the sheet's light haze above the top clasp
fg = ndi.binary_opening(fg, iterations=2)
lab, n = ndi.label(fg)
fg = lab == (np.argmax(ndi.sum(fg, lab, range(1, n + 1))) + 1)
fg = ndi.binary_fill_holes(ndi.binary_closing(fg, iterations=3))

# 2. The gold ring: fit a circle to its bright warm pixels, away from the clasps and pods.
ys, xs = np.where(fg)
cx0, cy0 = (xs.min() + xs.max()) / 2, (ys.min() + ys.max()) / 2
yy, xx = np.mgrid[0:h, 0:w]
ang = np.degrees(np.arctan2(yy - cy0, xx - cx0))
rr = np.hypot(xx - cx0, yy - cy0)
gold = ((crop[..., 0] - crop[..., 2]) > 35) & (lum > 95) & (rr > 195) & (rr < 222)
gold &= ~(np.abs(np.abs(ang) - 90) < 18) & ~((np.abs(ang) < 22) | (np.abs(ang) > 158))
x, y = xx[gold].astype(float), yy[gold].astype(float)
c = np.linalg.lstsq(np.c_[2 * x, 2 * y, np.ones_like(x)], x * x + y * y, rcond=None)[0]
cx, cy = c[0], c[1]
ring = np.sqrt(c[2] + cx * cx + cy * cy)
inner = ring - 6.5                # the ring's inner edge, from its radial profile
print(f'ring centre ({cx:.1f}, {cy:.1f}), radius {ring:.1f}, inner {inner:.1f} -> InnerRadius {inner + 1:.0f}/{N}')

rr = np.hypot(xx - cx, yy - cy)
alpha = np.clip(ndi.distance_transform_edt(fg) / 1.5, 0, 1) * np.clip(rr - inner + 0.5, 0, 1)

ox, oy = int(round(N / 2 - cx)), int(round(N / 2 - cy))
def save(rgb, a, name):
    canvas = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    canvas.paste(Image.fromarray(np.dstack([rgb, a * 255]).astype(np.uint8)), (ox, oy))
    canvas.save(f'{out}/{name}')
save(crop, alpha, 'hero_frame.png')

# 3. The arcs: cyan pixels of the lit frame, in the unlit frame's coordinates, with a soft glow.
lit = full[y0:y1, x0 - LIT_DX:x1 - LIT_DX]
core = np.clip((lit[..., 2] - lit[..., 0] - 40) / 120, 0, 1) * np.clip((lit[..., 1] - lit[..., 0] - 10) / 80, 0, 1)
core = ndi.gaussian_filter(core * (rr > inner - 2), 0.6)
glow = ndi.gaussian_filter(core, 5) * 2.2
a = np.clip(np.maximum(core, glow * 0.55), 0, 1)
hot = np.clip((lit.mean(-1) - 120) / 100, 0, 1)[..., None]
cyan = np.array([95, 224, 232], np.float32)
colour = cyan * (1 - hot) + np.array([210, 250, 255], np.float32) * hot
share = (core / np.maximum(a, 1e-4))[..., None]
save(np.where(a[..., None] > 0, colour * share + cyan * (1 - share), 0), a, 'hero_frame_lit.png')
