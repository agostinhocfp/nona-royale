# tools/mockup/post.py
# Post-process a mockup render: exposure, colour grade, bloom, vignette, grain,
# and optionally the in-match HUD footprint.
# Usage: python3 post.py in.png out.png [hud] [nograde]
import sys
import numpy as np
from PIL import Image, ImageFilter, ImageDraw

src, dst = sys.argv[1], sys.argv[2]
flags = set(sys.argv[3:])

im = Image.open(src).convert("RGB")
# Knock down path-tracing speckle first.
im = im.filter(ImageFilter.MedianFilter(3))
a = np.asarray(im).astype(np.float32) / 255.0
H, W = a.shape[:2]

def to_lin(c): return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
def to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)

lin = to_lin(a)

if "nograde" not in flags:
    # Exposure.
    lin *= float(__import__('os').environ.get('EXPOSURE', '1.7'))

    # Bloom: what passes a threshold, blurred wide, added back.
    bright = np.clip(lin - 0.55, 0, None)
    bimg = Image.fromarray((np.clip(bright, 0, 1) * 255).astype(np.uint8))
    bloom = (np.asarray(bimg.filter(ImageFilter.GaussianBlur(W * 0.012))).astype(np.float32) / 255.0 * 0.6 +
             np.asarray(bimg.filter(ImageFilter.GaussianBlur(W * 0.035))).astype(np.float32) / 255.0 * 0.45)
    lin = lin + bloom

    # Filmic-ish shoulder.
    lin = lin / (1.0 + lin * 0.55)

    g = to_srgb(lin)

    # Split tone: shadows toward warm aubergine, highlights toward warm gold.
    lum = (0.2126 * g[..., 0] + 0.7152 * g[..., 1] + 0.0722 * g[..., 2])[..., None]
    shadow_tint = np.array([0.075, 0.035, 0.055], np.float32)
    high_tint = np.array([1.04, 0.99, 0.9], np.float32)
    w_sh = np.clip(1.0 - lum * 3.0, 0, 1)
    g = g + shadow_tint * w_sh * 0.35
    g = g * (1 + (high_tint - 1) * np.clip(lum * 1.5 - 0.3, 0, 1))

    # Gentle S-curve.
    g = np.clip(g, 0, 1)
    g = g * g * (3 - 2 * g) * 0.35 + g * 0.65

    # Vignette.
    ys, xs = np.mgrid[0:H, 0:W]
    nx = (xs - W / 2) / (W / 2); ny = (ys - H * 0.45) / (H / 2)
    r = np.sqrt(nx * nx * 0.8 + ny * ny)
    vig = 1.0 - 0.55 * np.clip((r - 0.55) / 0.75, 0, 1) ** 1.6
    g = g * vig[..., None]

    # Grain.
    rng = np.random.default_rng(3)
    g = g + (rng.standard_normal((H, W, 1)).astype(np.float32) * 0.012)
    out = Image.fromarray((np.clip(g, 0, 1) * 255).astype(np.uint8))
else:
    out = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))

if "hud" in flags:
    # The in-match reservations at the 1080 reference, scaled to this image.
    s = H / 1080.0
    top, bottom, left, right = 56 * s, 196 * s, 290 * s, 78 * s
    ov = Image.new("RGBA", out.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    panel = (10, 7, 9, 225)
    line = (201, 154, 60, 128)
    d.rectangle([0, 0, W, top], fill=panel); d.line([0, top, W, top], fill=line, width=max(1, int(1.5 * s)))
    d.rectangle([0, H - bottom, W, H], fill=panel); d.line([0, H - bottom, W, H - bottom], fill=line, width=max(1, int(1.5 * s)))
    d.rectangle([0, top, left, H - bottom], fill=panel); d.line([left, top, left, H - bottom], fill=line, width=max(1, int(1.5 * s)))
    d.rectangle([W - right, top, W, H - bottom], fill=panel); d.line([W - right, top, W - right, H - bottom], fill=line, width=max(1, int(1.5 * s)))
    out = Image.alpha_composite(out.convert("RGBA"), ov).convert("RGB")

out.save(dst)
print("saved", dst)
