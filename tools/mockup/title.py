# tools/mockup/title.py
# Lay the title screen's lockup over a graded room render, so a room variant is
# judged as the title screen and not as a bare room (VISUAL_PASS.md, V3).
#
# Everything is read off View/TitleScreen.cs and View/UiTheme.cs at the 1080
# reference and scaled to the image, so LAYOUT=center is what the screen does
# today: a 0.45 scrim, the warm glow behind the wordmark, NONA at 108 in
# GoldBright, ROYALE at 30 between two diamond-tipped rules, the tagline, then
# PLAY / SETTINGS / QUIT in a 520-wide column, the whole stack centred.
#
# LAYOUT=left and LAYOUT=top are the alternatives, for deciding whether the room
# behind is worth building: centred, the lockup sits on the board and hides it.
#
# Usage: python3 title.py in.png out.png [label]
# Env: LAYOUT=center|left|top, FONT, TAGLINE, SCRIM, LABEL=0
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

src, dst = sys.argv[1], sys.argv[2]
label = sys.argv[3] if len(sys.argv) > 3 else ""
FONT = os.environ.get("FONT", "/tmp/claude-0/v3/Cinzel.ttf")
TAGLINE = os.environ.get("TAGLINE", "Nine operators. One vault.")
LAYOUT = os.environ.get("LAYOUT", "center")

# UiTheme tokens.
GOLD = (0xC9, 0x9A, 0x3C)
GOLD_BRIGHT = (0xF4, 0xD9, 0x8B)
TEXT = (0xED, 0xE6, 0xDA)
TEXT_DIM = (0xA3, 0x9A, 0x8C)
CYAN = (0x5F, 0xE0, 0xE8)
CYAN_DEEP = (0x0C, 0x3A, 0x3E)
BUTTON_OFF = (0x12, 0x0E, 0x11)

CARD_W, BUTTON_H, SPACING = 520.0, 54.0, 8.0
DEFAULT_SCRIM = {"center": 0.45, "left": 0.30, "top": 0.20}
SCRIM = float(os.environ.get("SCRIM", DEFAULT_SCRIM.get(LAYOUT, 0.45)))

im = Image.open(src).convert("RGB")
W, H = im.size
s = H / 1080.0


def px(v):
    return v * s


def font(size, path=FONT):
    try:
        return ImageFont.truetype(path, max(8, int(px(size))))
    except Exception:
        return ImageFont.load_default()


# ── Where the lockup sits ──────────────────────────────────────────────
if LAYOUT == "left":
    cx = 0.27 * W
elif LAYOUT == "top":
    cx = W / 2
else:
    cx = W / 2

LOCKUP = os.environ.get("LOCKUP", "1") != "0"

# ── Scrim ──────────────────────────────────────────────────────────────
a = np.asarray(im).astype(np.float32)
ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)

if not LOCKUP:
    pass  # LOCKUP=0 labels the frame and nothing else, for a room-only sheet.
elif LAYOUT == "left":
    # A side gradient instead of a flat veil: dark under the column, clear over
    # the board, so the room and the table stay visible.
    veil = SCRIM * np.clip(1.0 - (xs / W - 0.10) / 0.48, 0.0, 1.0) ** 0.9 + 0.08
    a *= (1.0 - veil)[..., None]
else:
    a *= (1.0 - SCRIM)

# ── Warm glow behind the wordmark (DecoSprites.Glow, GoldBright at 0.16) ──
gw, gh = px(1100), px(760)
if LAYOUT == "top":
    gcx, gcy = cx, px(300)
elif LAYOUT == "left":
    gcx, gcy = cx, H / 2 - px(60)
else:
    gcx, gcy = cx, H / 2 - px(120)
if LOCKUP:
    r = np.sqrt(((xs - gcx) / (gw / 2)) ** 2 + ((ys - gcy) / (gh / 2)) ** 2)
    a += (np.array(GOLD_BRIGHT, np.float32) * 0.16)[None, None, :] * (np.clip(1.0 - r, 0.0, 1.0) ** 2)[..., None]

base = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(ov)


def tracked(text, f, x_centre, top, fill, spacing=0.0):
    widths = [d.textlength(ch, font=f) for ch in text]
    total = sum(widths) + spacing * (len(text) - 1)
    x = x_centre - total / 2
    for ch, w in zip(text, widths):
        d.text((x, top), ch, font=f, fill=fill)
        x += w + spacing


def diamond(dx, dy, w, h, fill):
    d.polygon([(dx, dy - h / 2), (dx + w / 2, dy), (dx, dy + h / 2), (dx - w / 2, dy)], fill=fill)


def button(text, key, primary, left, top, width, height):
    d.rectangle([left, top, left + width, top + height],
                fill=(*(CYAN_DEEP if primary else BUTTON_OFF), 235))
    d.rectangle([left, top, left + width, top + height],
                outline=(*(CYAN if primary else GOLD), 190 if primary else 128),
                width=max(1, int(px(1.5))))
    f = font(22)
    tw = d.textlength(text, font=f)
    d.text((left + (width - tw) / 2, top + height / 2 - px(15)), text, font=f,
           fill=(*(CYAN if primary else TEXT), 250))
    if key:
        fk = font(15)
        kw = d.textlength(key, font=fk)
        d.text((left + width - kw - px(16), top + height / 2 - px(10)), key, font=fk, fill=(*GOLD, 190))


def wordmark(top):
    """NONA, ROYALE between two diamond-tipped rules, the tagline. Returns the bottom."""
    tracked("NONA", font(108), cx, top - px(14), (*GOLD_BRIGHT, 255), spacing=px(6))
    y = top + px(112) + SPACING * s

    f_roy = font(30)
    roy_w = d.textlength("ROYALE", font=f_roy) + px(10) * 5
    mid = y + px(34) / 2
    d.text((cx - roy_w / 2, mid - px(19)), "ROYALE", font=f_roy, fill=(*GOLD, 255))
    for side in (-1, 1):
        x1 = cx + side * (roy_w / 2 + px(14))
        x0 = x1 + side * px(70)
        d.line([(x0, mid), (x1, mid)], fill=(*GOLD, 255), width=max(1, int(px(2))))
        diamond(x0, mid, px(15), px(10), (*GOLD_BRIGHT, 255))
    y += px(34) + SPACING * s

    f_tag = font(18)
    tw = d.textlength(TAGLINE, font=f_tag)
    d.text((cx - tw / 2, y - px(2)), TAGLINE, font=f_tag, fill=(*TEXT_DIM, 240))
    return y + px(26)


ITEMS = (("PLAY", "Enter", True), ("SETTINGS", "", False), ("QUIT", "", False))

if not LOCKUP:
    pass
elif LAYOUT == "top":
    # Wordmark high, buttons as one row along the bottom: the board keeps the middle.
    wordmark(px(70))
    bw, gap = px(300), px(18)
    row_w = bw * len(ITEMS) + gap * (len(ITEMS) - 1)
    x = cx - row_w / 2
    top = H - px(60) - px(BUTTON_H)
    for text, key, primary in ITEMS:
        button(text, key, primary, x, top, bw, px(BUTTON_H))
        x += bw + gap
else:
    # The stack ModalCard builds: slots plus spacing, centred vertically.
    slots = [px(112), px(34), px(26), px(26)] + [px(BUTTON_H)] * 3 + [px(18)]
    total = sum(slots) + SPACING * s * (len(slots) - 1)
    y = (H - total) / 2
    y = wordmark(y) + SPACING * s
    for text, key, primary in ITEMS:
        button(text, key, primary, cx - px(CARD_W) / 2, y, px(CARD_W), px(BUTTON_H))
        y += px(BUTTON_H) + SPACING * s

# ── Variant label, for the contact sheet ───────────────────────────────
if label and os.environ.get("LABEL", "1") != "0":
    f = font(26)
    pad = px(14)
    tw = d.textlength(label, font=f)
    box = [pad, pad, pad + tw + 2 * pad, pad + px(44)]
    d.rectangle(box, fill=(10, 7, 9, 230))
    d.rectangle(box, outline=(*GOLD, 128), width=max(1, int(px(1.5))))
    d.text((pad * 2, pad + px(9)), label, font=f, fill=(*GOLD_BRIGHT, 240))

Image.alpha_composite(base.convert("RGBA"), ov).convert("RGB").save(dst)
print("saved", dst, LAYOUT)
