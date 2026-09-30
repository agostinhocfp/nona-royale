# tools/art/recolour_glow.py
# Repaints a chip portrait's lit twin from the house's cyan to an operator's own
# glow colour (ADR-0014, 2026-09-30), so the lit face agrees with the flare the
# code draws over it (OperatorGlow.cs).
#
# How: the lit twin is the unlit portrait plus light. Per pixel, the added light
# (lit - unlit) is split by least squares into a cyan part and a white part (the
# hot cores of the glow). Only the cyan part is swapped for the new colour, at the
# same brightness; the white cores, and anything else the edit changed, stay.
#
# Usage: python tools/art/recolour_glow.py <unlit.png> <lit_cyan.png> <RRGGBB> <out_lit.png>
# Needs numpy, pillow. Prints how many pixels carried the glow.
import sys
import numpy as np
from PIL import Image

CYAN = np.array([0x5F, 0xE0, 0xE8], np.float32) / 255.0   # UiTheme.Cyan, the colour the edits were painted in
LUMA = np.array([0.2126, 0.7152, 0.0722], np.float32)


def hex_colour(text):
    text = text.lstrip('#')
    return np.array([int(text[i:i + 2], 16) for i in (0, 2, 4)], np.float32) / 255.0


def recolour(unlit, lit, target):
    delta = lit - unlit

    # delta ~ a * CYAN + w * white, solved per pixel (2x2 normal equations).
    white = np.ones(3, np.float32)
    basis = np.stack([CYAN, white], axis=1)                 # 3x2
    solve = np.linalg.pinv(basis)                           # 2x3
    coeffs = delta.reshape(-1, 3) @ solve.T                 # N x 2
    a = np.clip(coeffs[:, 0], 0.0, None).reshape(delta.shape[:2])

    # The new colour at least as bright as the cyan it replaces; a pale colour is
    # kept at its own brightness, or it turns grey and the device reads as off.
    swapped = target * max(1.0, float(CYAN @ LUMA) / max(float(target @ LUMA), 1e-3))
    out = lit + a[..., None] * (swapped - CYAN)
    return np.clip(out, 0.0, 1.0), a


def main():
    unlit_path, lit_path, colour, out_path = sys.argv[1:5]
    unlit = np.asarray(Image.open(unlit_path).convert('RGB'), np.float32) / 255.0
    lit = np.asarray(Image.open(lit_path).convert('RGB'), np.float32) / 255.0
    if unlit.shape != lit.shape:
        sys.exit(f'{unlit_path} and {lit_path} differ in size')

    out, a = recolour(unlit, lit, hex_colour(colour))
    Image.fromarray((out * 255.0 + 0.5).astype(np.uint8)).save(out_path)
    print(f'{out_path}: glow on {int((a > 0.05).sum())} px')


if __name__ == '__main__':
    main()
