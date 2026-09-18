# tools/mockup/board_texture.py
# Numpy port of the G3 board sprites (BoardArt / BoardView / SceneLighting haze),
# composed in linear space under an approximation of the room lights.
import sys
import numpy as np
from PIL import Image

NEW = sys.argv[1] == "new"
LIGHTS = sys.argv[2] == "lit" if len(sys.argv) > 2 else True
OUT = sys.argv[3] if len(sys.argv) > 3 else f"/tmp/claude-0/prev/board_{sys.argv[1]}.png"
import os
PX = int(os.environ.get('PX', '64'))  # pixels per cell
GRID, ARM_CELLS, MARGIN = 15, 3, 0.8
SIDE = GRID + 2 * MARGIN
W = int(SIDE * PX)

ys, xs = np.mgrid[0:W, 0:W]
X = (xs + 0.5) / PX - SIDE / 2          # world, cells, x right
Y = SIDE / 2 - (ys + 0.5) / PX          # y up

def hexc(h, a=1.0):
    v = int(h, 16); return np.array([(v >> 16) / 255, (v >> 8 & 255) / 255, (v & 255) / 255, a])
def lin(c):
    c = np.asarray(c, float); rgb = c[..., :3]
    return np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)
def srgb(l):
    l = np.clip(l, 0, 1); return np.where(l <= 0.0031308, l * 12.92, 1.055 * l ** (1 / 2.4) - 0.055)
def lerp(a, b, t): return a + (b - a) * t
def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
def invlerp(a, b, v): return np.clip((v - a) / (b - a), 0, 1)
def ss01(t): return t * t * (3 - 2 * t)
def line(off, w): return np.clip(w * 0.5 - off + 0.5, 0, 1)
def cov(d): return np.clip(0.5 - d, 0, 1)
def band(d, inset, w): return line(np.abs(d + inset + w * 0.5), w)

GOLD, GOLDB, BRASS = hexc("C99A3C"), hexc("F4D98B"), hexc("7C5A1E")
CYAN = hexc("5FE0E8")
SEATS = {"red": np.array([.84, .27, .31, 1]), "blue": np.array([.32, .56, .88, 1]),
         "green": np.array([.34, .72, .44, 1]), "violet": np.array([.5, .4, .84, 1])}

img = np.zeros((W, W, 3)); img[:] = lin(hexc("08060A"))
light = np.ones((W, W))  # multiply-light map (applied to board layer)

def over(colour, alpha, lum=None):
    """Composite a tinted sprite: colour (sRGB rgba), alpha map, optional baked sRGB grey."""
    global img
    c = lin(colour)
    if lum is not None:
        c = c[None, None, :] * lin(np.stack([lum] * 3, -1))
    a = np.clip(alpha * colour[3], 0, 1)[..., None]
    img = img * (1 - a) + c * a

# ── noise (same hash as C#) ──
def hash2(x, y, seed):
    x = x.astype(np.int64); y = y.astype(np.int64)
    h = (x * 374761393 + y * 668265263 + seed * 144665 + 1013904223) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    h ^= h >> 16
    return (h & 0xFFFFFF) / 16777215.0
def vnoise(x, y, seed):
    ix, iy = np.floor(x), np.floor(y); fx, fy = x - ix, y - iy
    fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy)
    b = lerp(hash2(ix, iy, seed), hash2(ix + 1, iy, seed), fx)
    t = lerp(hash2(ix, iy + 1, seed), hash2(ix + 1, iy + 1, seed), fx)
    return lerp(b, t, fy)
def fbm(x, y, oct, seed):
    s, a, tot = 0, .5, 0
    for i in range(oct):
        s = s + a * vnoise(x, y, seed + i); tot += a; x = x * 2.03; y = y * 2.03; a *= .5
    return s / tot

# ── table ──
over(hexc("0E0B0E"), np.ones((W, W)))
if NEW:
    tpc = 512 / SIDE
    d = (np.maximum(np.abs(X), np.abs(Y)) - (SIDE / 2 - 0.3)) * tpc
    over(np.r_[GOLD[:3], .32], line(np.abs(d), 1.2))

# ── cross ──
PAD = .12
LEN, ARM = GRID / 2 + PAD, ARM_CELLS / 2 + PAD
def box(x, y, hx, hy):
    qx, qy = np.abs(x) - hx, np.abs(y) - hy
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0)
def cross_d(x, y, steps):
    ax, ay = np.abs(x), np.abs(y)
    d = np.minimum(np.maximum(ax - LEN, ay - ARM), np.maximum(ax - ARM, ay - LEN))
    if steps:
        for sw, sh in [(.42, .14), (.28, .28), (.14, .42)]:
            f = ARM - .5; hx, hy = (sw + .5) / 2, (sh + .5) / 2
            d = np.minimum(d, box(ax - (f + hx), ay - (f + hy), hx, hy))
    return d
ppc = 40
d = cross_d(X, Y, NEW) * ppc
ds = cross_d(X - .12, Y + .2, NEW) * ppc
over(np.array([0, 0, 0, .75]), 1 - ss01(invlerp(0, .9 * ppc, ds)))

crack = np.array([-ARM, -(ARM_CELLS / 2 + (LEN - ARM_CELLS / 2) * .45)])
def seg(x, y, a, b):
    ab = b - a; apx, apy = x - a[0], y - a[1]
    t = np.clip((apx * ab[0] + apy * ab[1]) / (ab @ ab), 0, 1)
    return np.hypot(apx - ab[0] * t, apy - ab[1] * t)
if NEW:
    cloud = fbm(X * 1.1, Y * 1.1, 3, 21)
    warp = fbm(X * .45 + 7.1, Y * .45 - 3.3, 3, 22)
    main = np.sin((X * .55 + Y * .35) * 2.1 + warp * 7)
    veins = 1 - ss01(np.clip(np.abs(main) / .07, 0, 1))
    fine = np.sin((X * -.3 + Y * .62) * 4.3 + warp * 11)
    threads = 1 - ss01(np.clip(np.abs(fine) / .05, 0, 1))
    lum = .84 + .08 * (cloud - .5) * 2 + .09 * veins + .045 * threads
    p1, p2, p3 = crack + [.06, -.035], crack + [.11, .015], crack + [.16, -.03]
    off = np.minimum(seg(X, Y, crack, p1), np.minimum(seg(X, Y, p1, p2), seg(X, Y, p2, p3)))
    lum *= 1 - .45 * line(off * ppc, .8)
    over(hexc("1B161E"), cov(d), np.minimum(lum, 1))
    # pattern
    inside = ss01(invlerp(-.15 * ppc, -.4 * ppc, d))
    r = np.hypot(X, Y); centre = ss01(invlerp(1.3, 2.1, r))
    step = 360 / 48; ang = np.degrees(np.arctan2(Y, X)); idx = np.round(ang / step)
    delta = np.radians(ang - idx * step)
    start = np.where(idx.astype(int) % 2 == 0, 1.8, 3.2)
    ray = np.where(r < start, 0, line(r * ppc * np.abs(np.sin(delta)), 1))
    ri = np.round((r - 1) * .5)
    ring = np.where(ri < 1, 0, line(np.abs(r - (1 + 2 * ri)) * ppc, 1.2))
    over(np.r_[GOLD[:3], .04], np.maximum(ray, ring) * inside * centre)
else:
    over(hexc("17131A"), cov(d))

# arm glow
def glow(cx, cy, size):
    rr = np.hypot(X - cx, Y - cy) / (size / 2); t = np.clip(1 - rr, 0, 1); return t * t
reach = GRID / 2
for dx, dy in [(0, 1), (-1, 0), (0, -1), (1, 0)]:
    over(np.r_[GOLD[:3], .07], glow(dx * reach * .6, dy * reach * .6, reach * .6))

# edge
if NEW:
    ea = band(d, 0, 1.4)
    near = (X <= -ARM + .1) & (X >= -ARM - .1) & (Y <= -ARM)
    along = Y - crack[1]
    tarnish = 1 - .5 * ss01(invlerp(.8, .45, np.abs(along)))
    gap = np.abs(along + .6 * (X + ARM)) * ppc
    ea = np.where(near, ea * tarnish * np.clip(gap - 1.4, 0, 1), ea)
    over(np.r_[lerp(BRASS, GOLD, .45)[:3], .9], ea)
else:
    over(np.r_[BRASS[:3], .95], band(d, 0, 2.2))

# lanes
lw = .035; ls, le = 1.7 / 2 + .15, reach - .6
la = .42 if NEW else .55
for dx, dy in [(0, 1), (-1, 0), (0, -1), (1, 0)]:
    if dx == 0:
        m = (np.abs(X) < lw / 2) & (dy * Y > ls) & (dy * Y < le)
    else:
        m = (np.abs(Y) < lw / 2) & (dx * X > ls) & (dx * X < le)
    over(np.r_[BRASS[:3], la], m.astype(float))

# cells: walk of a 15-grid cross (same as BoardLayout)
A = 6; c = 7; low, high, last, inner = 6, 8, 14, 9
walk = []
walk += [(high, r) for r in range(A - 1, -1, -1)] + [(c, 0)] + [(low, r) for r in range(A)]
walk += [(col, low) for col in range(A - 1, -1, -1)] + [(0, c)] + [(col, high) for col in range(A)]
walk += [(low, r) for r in range(inner, 15)] + [(c, last)] + [(high, r) for r in range(last, inner - 1, -1)]
walk += [(col, high) for col in range(inner, 15)] + [(last, c)] + [(col, low) for col in range(last, inner - 1, -1)]
shift = A + 1
def world(g): return (g[0] - c, c - g[1])
cellsz = .86
def tile(cx, cy, fill, inlay):
    lx, ly = np.abs(X - cx), np.abs(Y - cy)
    m = (lx < cellsz / 2) & (ly < cellsz / 2)
    over(fill, m.astype(float))
    # inlay: 64 texel tile, inset 6, cut 6, band 2.2 texels
    s = 64 / cellsz; px_, py_ = lx * s, ly * s; e = 32 - 6
    bx = np.maximum(px_ - e, py_ - e); cr = (px_ + py_ - (2 * e - 6)) / np.sqrt(2)
    over(inlay, band(np.maximum(bx, cr), 0, 2.2) * m)
starts = {0: "red", 13: "blue", 26: "green", 39: "violet"}
safe = {0, 8, 13, 21, 26, 34, 39, 47}
whisper = np.r_[hexc("2A2328")[:3], .35]
for i in range(52):
    cx, cy = world(walk[(i + shift) % 52])
    if i not in safe:
        tile(cx, cy, whisper, np.r_[GOLD[:3], .16]); continue
    over(np.r_[CYAN[:3], .13], glow(cx, cy, cellsz * 1.7))
    if i in starts:
        t = SEATS[starts[i]]
        tile(cx, cy, np.r_[lerp(whisper, t, .3)[:3], .45], np.r_[t[:3], .6])
    else:
        tile(cx, cy, whisper, np.r_[CYAN[:3], .45])
homes = {"red": lambda k: (c, 1 + k), "blue": lambda k: (1 + k, c), "green": lambda k: (c, last - 1 - k), "violet": lambda k: (last - 1 - k, c)}
for seat, f in homes.items():
    t = SEATS[seat]
    for k in range(6):
        u = k / 5
        tile(*world(f(k)), np.r_[t[:3], lerp(.05, .16, u)], np.r_[t[:3], lerp(.22, .5, u)])

# tables
yards = {"red": (2.5, 2.5), "blue": (2.5, 11.5), "green": (11.5, 11.5), "violet": (11.5, 2.5)}
diam = 5
for seat, g in yards.items():
    cx, cy = world(g); t = SEATS[seat]
    dx_, dy_ = (X - cx) / (diam / 2), (Y - cy) / (diam / 2); r = np.hypot(dx_, dy_)
    half = 128
    sd = np.hypot(X - cx - .1, Y - cy + .22) / (diam * 1.06 / 2) * 64
    over(np.array([0, 0, 0, .75]), 1 - ss01(invlerp(64 * .65 - 1, 63, sd)))
    fa = cov((r - .875) * half)
    lum = 1 - .5 * np.clip(r / .875, 0, 1) ** 1.8
    hl = np.clip(1 - np.hypot(dx_ + .18, dy_ - .22), 0, 1)
    lum *= .85 + .15 * hl
    lum *= 1 - .35 * ss01(invlerp(.72, .875, r))
    if NEW:
        tx = np.floor((dx_ + 1) * half); ty = np.floor((dy_ + 1) * half)
        grain = hash2(tx, ty, 11) * 2 - 1
        nap = vnoise((dx_ + 1) * half * .9, (dy_ + 1) * half * .14, 12) * 2 - 1
        lum *= 1 + .045 * grain + .035 * nap
    felt = np.r_[t[:3] * .62, 1]
    over(felt, fa, np.clip(lum, 0, 1))
    # dotted ring
    ph = np.mod(np.arctan2(dy_, dx_) / (2 * np.pi) * 64, 1)
    over(np.r_[GOLD[:3], .5], line(np.abs(r - .79) * half, 1.2) * (np.abs(ph - .5) < .22))
    # seats
    for sidx, angd in enumerate([180, 90, 0]):
        a = np.radians(angd); sx, sy = cx + np.cos(a) * 1.375, cy + np.sin(a) * 1.375
        over(np.array([0, 0, 0, .35]), (np.hypot(X - sx, Y - sy) < .23).astype(float))
    # rim
    outer = 1 - 1.5 / half; inner = .9
    ra = cov((r - outer) * half) * cov((inner - r) * half)
    tt = np.clip((r - inner) / (outer - inner), 0, 1)
    bead = .62 + .38 * np.sin(np.pi * tt) ** .7
    nx, ny = dx_ / np.maximum(r, 1e-4), dy_ / np.maximum(r, 1e-4)
    li = np.clip(.5 + .5 * (-.6 * nx + .8 * ny), 0, 1)
    if NEW:
        rl = bead * (.5 + .36 * li)
        ang = np.degrees(np.arctan2(dy_, dx_)); dd = (ang - 128 + 180) % 360 - 180
        rl += .42 * np.exp(-(dd * dd) / 81) * np.sin(np.pi * tt) ** 1.5
        tint = lerp(GOLD, GOLDB, .6)
    else:
        rl = bead * (.62 + .5 * li); tint = lerp(GOLD, GOLDB, .45)
    rl *= 1 - .45 * line(np.abs(r - (inner + .018)) * half, 1.2)
    over(np.r_[tint[:3], 1], ra, np.clip(rl, 0, 1))


# corner lamps (G4)
LAMPS = []
if len(sys.argv) > 4 and sys.argv[4] == "lamps":
    half = SIDE / 2 - 0.0; H = 1.7
    for i in range(4):
        x = half - 1.05; y = half - (H + .45) if i < 2 else -(half - .5)
        LAMPS.append((-x if i % 2 == 0 else x, y, i % 2 == 0))
    def tier(x, y, b, t, lo, up):
        tt = np.clip((y - b) / (t - b), 0, 1); hw = lerp(lo, up, tt); sl = (lo - up) / (t - b)
        return np.maximum((np.abs(x) - hw) / np.sqrt(1 + sl * sl), np.maximum(b - y, y - t))
    def lampd(x, y):
        d = box(x, y - .025, .24, .025)
        d = np.minimum(d, box(x, y - .07, .17, .02))
        d = np.minimum(d, box(x, y - .105, .09, .017))
        d = np.minimum(d, box(x, y - .37, .02, .26))
        d = np.minimum(d, box(x, y - .38, .045, .014))
        d = np.minimum(d, tier(x, y, .62, .74, .37, .28))
        d = np.minimum(d, tier(x, y, .74, .86, .24, .13))
        d = np.minimum(d, box(x, y - .62, .39, .012))
        d = np.minimum(d, (np.abs(x) + np.abs(y - .9) - .04) / np.sqrt(2))
        return d
    for (lx, ly, west) in LAMPS:
        u = (X - lx) / H; v = (Y - ly) / H
        pr = np.hypot((X - lx) / (H * 2.4 / 2), (Y - ly - .35 * H) / (H * 1.7 / 2)); pt = np.clip(1 - pr, 0, 1)
        over(np.r_[hexc("FFD9A0")[:3], .1], pt * pt)
        sd = np.hypot((X - lx) / (H * .6 / 2), (Y - ly) / (H * .12 / 2)) * 64
        over(np.array([0, 0, 0, .75]), 1 - ss01(invlerp(64 * .65 - 1, 63, sd)))
        m = (np.abs(u) < .41) & (v > -.01) & (v < 1.01)
        tex = 128
        dd = lampd(u, v) * tex
        over(hexc("040305"), np.where(m, cov(dd), 0))
        e = .5 / tex; ux = u if west else -u
        gx = lampd(ux + e, v) - lampd(ux - e, v); gy = lampd(ux, v + e) - lampd(ux, v - e)
        ln = np.maximum(np.hypot(gx, gy), 1e-9)
        facing = (.85 * gx + .45 * gy) / ln
        rim = band(dd, 0, 1.8) * ss01(invlerp(.15, .85, facing))
        over(np.r_[hexc("E8C38A")[:3], .32], np.where(m, rim, 0))
        over(np.r_[hexc("FFD9A0")[:3], .3], glow(lx, ly + .62 * H, H * .7))

# vault (plate + frame + glow)
over(np.r_[GOLDB[:3], .28], glow(0, 0, 1.7 * 3))
vs = 256 / 1.7; vx, vy = np.abs(X) * vs, np.abs(Y) * vs; h = 256 * .46; sc = 25.6
bx = np.maximum(vx - h, vy - h); cd = np.hypot(vx - h, vy - h); vd = np.maximum(bx, -(cd - sc))
over(hexc("0F0B08"), cov(vd))
over(np.r_[lerp(GOLD, GOLDB, .35)[:3], 1], band(vd, 0, 6))
rr = np.hypot(X, Y) * vs
over(np.r_[lerp(GOLD, GOLDB, .35)[:3], .9], line(np.abs(rr - 256 * .3), 5))

# haze (static pose)
if NEW:
    ext = GRID / 2
    for (hx, hy, size) in [(0, 0, ext * .75)] + [(dx * ext * .6, dy * ext * .6, ext * .6) for dx, dy in [(0, 1), (-1, 0), (0, -1), (1, 0)]]:
        u, v = (X - hx) / (size / 2), (Y - hy) / (size / 2); r = np.hypot(u, v)
        fo = 1 - ss01(invlerp(.3, 1, r))
        cl = ss01(invlerp(.3, .85, fbm(u * 2.4 + 3, v * 2.4, 4, 31)))
        over(np.r_[hexc("FFE6C4")[:3], .035], np.where(r < 1, cl * fo, 0))

# room light (multiply): ambient + pools
if LIGHTS:
    def pool(cx, cy, outer, inner_, inten):
        rr = np.hypot(X - cx, Y - cy)
        return inten * (1 - ss01(invlerp(inner_, outer, rr)))
    L = np.full((W, W, 1), .7) * lin(np.array([1, .96, .9, 1]))[None, None, :]
    ext = GRID / 2
    add = lambda col, m: col[None, None, :] * m[..., None]
    L = L + add(lin(np.array([1, .83, .56, 1])), pool(0, 0, ext * .42, ext * .42 * .25, .6))
    for dx, dy in [(0, 1), (-1, 0), (0, -1), (1, 0)]:
        L = L + add(lin(np.array([1, .9, .74, 1])), pool(dx * ext * .6, dy * ext * .6, ext * .6, ext * .21, .34))
    for g in yards.values():
        cx, cy = world(g)
        L = L + add(lin(np.array([1, .88, .7, 1])), pool(cx, cy, diam * .75, diam * .3, .4))
    for (lx, ly, w) in LAMPS:
        L = L + add(lin(np.array([1, .85, .62, 1])), pool(lx, ly + .25, 1.6, .48, .3))
    img = img * L

# a few figures (Default layer, unlit-ish ambient) for readability checks
for _ in ([] if os.environ.get('NOPIECES') else [0]):
  pass
for (gx, gy, seat) in ([] if os.environ.get("NOPIECES") else [(8, 3, "red"), (6, 10, "blue"), (12, 8, "green"), (7, 1, "violet"), (low, 11, "green")]):
    cx, cy = world((gx, gy)); t = SEATS[seat]
    dd = np.hypot(X - cx, Y - cy - .1)
    over(hexc("1C0E12"), (dd < .36).astype(float))
    over(np.r_[lerp(t, np.ones(4), .12)[:3], 1], (dd < .31).astype(float))
    over(np.r_[GOLDB[:3], 1], (np.hypot(X - cx, Y - cy + .02) < .06).astype(float))

Image.fromarray((srgb(img) * 255).astype(np.uint8)).save(OUT)
print("saved", OUT)
