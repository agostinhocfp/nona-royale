# Replicates the Primitives.cs rasterization math 1:1 to preview the new
# Cross and Star silhouettes next to the existing operator shapes.
import math
from pathlib import Path

import numpy as np
from PIL import Image

SIZE = 96
CENTRE = (SIZE - 1) * 0.5


def rasterize(alpha_at, size=SIZE):
    img = np.zeros((size, size), dtype=np.float32)
    for y in range(size):
        for x in range(size):
            img[y, x] = alpha_at(x, y)
    return img


def polygon(sides, rotation_deg):
    circumradius = CENTRE - 1.5
    rotation = math.radians(rotation_deg)
    wedge = 2 * math.pi / sides
    apothem = circumradius * math.cos(math.pi / sides)

    def alpha(x, y):
        dx, dy = x - CENTRE, y - CENTRE
        distance = math.hypot(dx, dy)
        if distance < 0.0001:
            return 1.0
        angle = math.atan2(dy, dx) - rotation
        within = (angle % wedge) - wedge * 0.5
        edge = apothem / math.cos(within)
        return max(0.0, min(1.0, edge - distance + 1.0))

    return rasterize(alpha)


def disc():
    outer = CENTRE - 1.0

    def alpha(x, y):
        distance = math.hypot(x - CENTRE, y - CENTRE)
        return max(0.0, min(1.0, outer - distance + 1.0)) if distance <= outer else 0.0

    return rasterize(alpha)


def cross():
    outer = CENTRE - 1.5
    arm = outer * 0.36

    def alpha(x, y):
        ax, ay = abs(x - CENTRE), abs(y - CENTRE)
        horizontal = max(ax - outer, ay - arm)
        vertical = max(ay - outer, ax - arm)
        return max(0.0, min(1.0, 1.0 - min(horizontal, vertical)))

    return rasterize(alpha)


def star(points, rotation_deg):
    outer = CENTRE - 1.5
    inner = outer * 0.382
    rotation = math.radians(rotation_deg)
    half_wedge = math.pi / points
    dx = inner * math.cos(half_wedge) - outer
    dy = inner * math.sin(half_wedge)
    edge_cross = outer * dy

    def alpha(x, y):
        px, py = x - CENTRE, y - CENTRE
        distance = math.hypot(px, py)
        if distance < 0.0001:
            return 1.0
        angle = math.atan2(py, px) - rotation
        a = angle % (2 * half_wedge)
        if a > half_wedge:
            a = 2 * half_wedge - a
        denom = math.cos(a) * dy - math.sin(a) * dx
        edge = edge_cross / denom if denom > 0.0001 else outer
        return max(0.0, min(1.0, edge - distance + 1.0))

    return rasterize(alpha)


shapes = [
    ("Bouncer hex", polygon(6, 0)),
    ("Syla tri", polygon(3, 90)),
    ("Kurbyn dia", polygon(4, 0)),
    ("Mimi pent", polygon(5, 90)),
    ("Javi CROSS", cross()),
    ("Kian STAR", star(5, 90)),
    ("Sanity oct", polygon(8, 0)),
    ("Nuetu disc", disc()),
]

out = Path(__file__).parent
cell = SIZE + 8
sheet = Image.new("L", (cell * 4, cell * 2), 40)
for i, (name, img) in enumerate(shapes):
    tile = Image.fromarray((img * 255).astype(np.uint8), "L")
    sheet.paste(tile, ((i % 4) * cell + 4, (i // 4) * cell + 4))
sheet.save(out / "shapes_large.png")

# Board-scale check: Javi ~6 HP -> size ~0.57 of a cell; Kian 6 HP same.
# Render each at 28 px (roughly board scale) to confirm they stay distinct.
small = Image.new("L", (40 * 4, 40 * 2), 40)
for i, (name, img) in enumerate(shapes):
    tile = Image.fromarray((img * 255).astype(np.uint8), "L").resize((28, 28), Image.LANCZOS)
    small.paste(tile, ((i % 4) * 40 + 6, (i // 4) * 40 + 6))
small = small.resize((small.width * 4, small.height * 4), Image.NEAREST)
small.save(out / "shapes_board_scale.png")

print("saved:", out / "shapes_large.png")
print("saved:", out / "shapes_board_scale.png")
print("order:", [n for n, _ in shapes])
