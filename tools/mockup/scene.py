# tools/mockup/scene.py
# Blender mockup: the Nona Royale table under a tilted perspective camera (VISUAL_PASS.md).
# Run: blender --background --python scene.py -- <out.png> <variant: room|void> <pitch_deg> <w> <h> <samples>
import bpy, sys, math, os
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
OUT, VARIANT, PITCH = argv[0], argv[1], float(argv[2])
W, H, SAMPLES = int(argv[3]), int(argv[4]), int(argv[5])
D = os.environ.get("MOCK_DIR", "/tmp/claude-0/mock") + "/"
ART = os.environ.get("ART_DIR", "Assets/_Project/Art/Resources/Art/Operators") + "/"
SIDE = 16.6

bpy.ops.wm.read_factory_settings(use_empty=True)
scn = bpy.context.scene
scn.render.engine = "CYCLES"
scn.cycles.samples = SAMPLES
scn.cycles.use_denoising = False
try:
    scn.cycles.denoiser = "OPENIMAGEDENOISE"
except Exception:
    pass
scn.render.resolution_x, scn.render.resolution_y = W, H
scn.render.film_transparent = False
_vt = [i.identifier for i in scn.view_settings.bl_rna.properties["view_transform"].enum_items]
scn.view_settings.view_transform = "AgX" if "AgX" in _vt else ("Filmic" if "Filmic" in _vt else "Standard")
print("VIEW", scn.view_settings.view_transform)
scn.view_settings.look = "None"
scn.render.image_settings.file_format = "PNG"

world = bpy.data.worlds.new("w"); scn.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.004, 0.003, 0.005, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 1.0


def img(path, alpha=False, colour=True):
    im = bpy.data.images.load(path)
    if not colour:
        im.colorspace_settings.name = "Non-Color"
    return im


def mat_board():
    m = bpy.data.materials.new("board"); m.use_nodes = True
    nt = m.node_tree; n = nt.nodes; l = nt.links
    bsdf = n["Principled BSDF"]
    tex = n.new("ShaderNodeTexImage"); tex.image = img(D + "board_tex.png")
    rough = n.new("ShaderNodeTexImage"); rough.image = img(D + "board_rough.png", colour=False)
    metal = n.new("ShaderNodeTexImage"); metal.image = img(D + "board_metal.png", colour=False)
    bump_t = n.new("ShaderNodeTexImage"); bump_t.image = img(D + "board_bump.png", colour=False)
    bump = n.new("ShaderNodeBump"); bump.inputs["Strength"].default_value = 0.25; bump.inputs["Distance"].default_value = 0.02
    # Gold reads brighter as metal: lift base colour where metallic.
    mix = n.new("ShaderNodeMixRGB"); mix.blend_type = "MULTIPLY"; mix.inputs[0].default_value = 0.0
    l.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    l.new(rough.outputs["Color"], bsdf.inputs["Roughness"])
    l.new(metal.outputs["Color"], bsdf.inputs["Metallic"])
    l.new(bump_t.outputs["Color"], bump.inputs["Height"])
    l.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    # A touch of emission so the painted glows (vault, safe cells) still read.
    em = n.new("ShaderNodeEmission"); em.inputs["Strength"].default_value = float(os.environ.get("BOARD_EMIT", "0.6"))
    l.new(tex.outputs["Color"], em.inputs["Color"])
    add = n.new("ShaderNodeAddShader")
    out = n["Material Output"]
    l.new(bsdf.outputs["BSDF"], add.inputs[0]); l.new(em.outputs["Emission"], add.inputs[1])
    l.new(add.outputs["Shader"], out.inputs["Surface"])
    return m


def mat(name, colour, rough=0.5, metal=0.0, emit=None, emit_strength=0.0, sheen=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*colour, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if sheen and "Sheen Weight" in b.inputs:
        b.inputs["Sheen Weight"].default_value = sheen
    if emit is not None:
        key = "Emission Color" if "Emission Color" in b.inputs else "Emission"
        b.inputs[key].default_value = (*emit, 1)
        b.inputs["Emission Strength"].default_value = emit_strength
    return m


def box(name, loc, size, material):
    bpy.ops.mesh.primitive_cube_add(location=loc)
    o = bpy.context.object; o.name = name
    o.scale = (size[0] / 2, size[1] / 2, size[2] / 2)
    o.data.materials.append(material)
    bpy.ops.object.transform_apply(scale=True)
    return o


def sprite(name, path, foot, height, cam_dir_xy, tilt):
    """An upright card with the render, feet at `foot`, turned to the camera and leaned back by `tilt`."""
    im = img(path)
    aspect = im.size[0] / im.size[1]
    bpy.ops.mesh.primitive_plane_add(size=1)
    o = bpy.context.object; o.name = name
    o.scale = (height * aspect, height, 1)
    bpy.ops.object.transform_apply(scale=True)
    # Move pivot to bottom edge.
    for v in o.data.vertices:
        v.co.y += height / 2
    o.rotation_euler = (math.radians(90 - tilt), 0, 0)
    o.location = foot
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; n = nt.nodes; l = nt.links
    b = n["Principled BSDF"]; b.inputs["Roughness"].default_value = 0.8
    t = n.new("ShaderNodeTexImage"); t.image = im
    l.new(t.outputs["Color"], b.inputs["Base Color"]); l.new(t.outputs["Alpha"], b.inputs["Alpha"])
    m.blend_method = "HASHED"
    o.data.materials.append(m)
    o.visible_shadow = True
    return o


def disc(name, loc, radius, colour, strength):
    bpy.ops.mesh.primitive_circle_add(vertices=48, radius=radius, fill_type="NGON", location=loc)
    o = bpy.context.object; o.name = name
    o.data.materials.append(mat(name, colour, rough=0.4, emit=colour, emit_strength=strength))
    return o


def light(name, kind, loc, energy, colour, size=0.5, rot=None, spot=None):
    ld = bpy.data.lights.new(name, kind); ld.energy = energy; ld.color = colour
    if kind == "AREA": ld.size = size
    else: ld.shadow_soft_size = size
    if spot: ld.spot_size = math.radians(spot); ld.spot_blend = 0.6
    o = bpy.data.objects.new(name, ld); o.location = loc
    if rot: o.rotation_euler = rot
    scn.collection.objects.link(o)
    return o


# ── Table ──────────────────────────────────────────────────────────────
bpy.ops.mesh.primitive_plane_add(size=SIDE, location=(0, 0, 0))
top = bpy.context.object; top.data.materials.append(mat_board())
lacquer = mat("lacquer", (0.018, 0.012, 0.012), rough=0.25)
gilt = mat("gilt", (0.79, 0.6, 0.24), rough=0.25, metal=1.0)
box("slab", (0, 0, -0.35), (SIDE + 0.1, SIDE + 0.1, 0.7), lacquer)
# A gilt band under the lip, and a recessed plinth.
box("band", (0, 0, -0.1), (SIDE + 0.16, SIDE + 0.16, 0.05), gilt)
box("plinth", (0, 0, -0.95), (SIDE - 0.8, SIDE - 0.8, 0.5), mat("plinth", (0.01, 0.008, 0.008), rough=0.5))

floor_z = -1.2

# ROOM picks what stands behind the table (VISUAL_PASS.md, V3). `salon` is the one
# the designer chose: a velvet wall, a tiered chandelier clear of the wordmark, and
# a ring of sconces on side columns. The four it was chosen from are kept so the
# comparison can be re-rendered: `parlour` (dense columns, curtains, chandelier),
# `vault` (a Deco relief wall and a vault door), `curtain` (the velvet wall alone)
# and `bare` (the carpet and one far glow).
ROOM = os.environ.get("ROOM", "parlour")
CARPET = float(os.environ.get("CARPET", "1.0"))

stone = mat("stone", (0.03, 0.025, 0.028), rough=0.35)
velvet = mat("velvet", (0.06, 0.008, 0.018), rough=0.8, sheen=1.0)


def carpet_floor(pattern=False):
    """The floor. With `pattern`, a faint gilt ring for the table's spill to catch."""
    c = (0.011 * CARPET, 0.005 * CARPET, 0.007 * CARPET)
    # No sheen: at the title's grazing angle, sheen turns the carpet into a milky
    # retroreflective sheet and the room stops reading as dark.
    m = mat("carpet", c, rough=0.95)
    if pattern:
        nt = m.node_tree; n = nt.nodes; l = nt.links
        b = n["Principled BSDF"]
        coord = n.new("ShaderNodeTexCoord")
        wave = n.new("ShaderNodeTexWave")
        wave.wave_type = "RINGS"
        wave.inputs["Scale"].default_value = 0.07
        wave.inputs["Distortion"].default_value = 1.6
        ramp = n.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].color = (*c, 1)
        ramp.color_ramp.elements[1].color = (c[0] + 0.055, c[1] + 0.036, c[2] + 0.017, 1)
        l.new(coord.outputs["Object"], wave.inputs["Vector"])
        l.new(wave.outputs["Fac"], ramp.inputs["Fac"])
        l.new(ramp.outputs["Color"], b.inputs["Base Color"])
    bpy.ops.mesh.primitive_plane_add(size=120, location=(0, 0, floor_z))
    bpy.context.object.data.materials.append(m)


def column(x, y, h=14, width=1.1, sconce=True):
    """A Deco column: shaft, stepped capital and base, a gilt fluting line, a sconce."""
    box("col", (x, y, floor_z + h / 2), (width, width, h), stone)
    for i, s in enumerate((width + 0.6, width + 0.35, width + 0.15)):
        box("cap", (x, y, floor_z + h - 0.2 - i * 0.35), (s, s, 0.35), stone)
        box("base", (x, y, floor_z + 0.18 + i * 0.3), (s, s, 0.3), stone)
    box("flute", (x, y - width / 2 - 0.01, floor_z + h / 2), (0.08, 0.02, h - 2), gilt)
    if not sconce:
        return
    light("sconce", "POINT", (x, y - 1.0, floor_z + 5.5), 35, (1.0, 0.72, 0.45), size=0.3)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.18, location=(x, y - 0.7, floor_z + 5.5))
    bpy.context.object.data.materials.append(mat("bulb", (1, 0.8, 0.5), emit=(1, 0.75, 0.45), emit_strength=25))


def curtain(x0, y, half_width, height, waves=(3.3, 0.32), cuts=60):
    """A hanging velvet panel, folded by a sine along its width."""
    bpy.ops.mesh.primitive_plane_add(size=1, location=(x0, y, floor_z + height / 2))
    c = bpy.context.object
    c.scale = (half_width * 2, height, 1)
    c.rotation_euler = (math.radians(90), 0, 0)
    bpy.ops.object.transform_apply(scale=True)
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.subdivide(number_cuts=cuts); bpy.ops.object.mode_set(mode="OBJECT")
    freq, amp = waves
    for v in c.data.vertices:
        v.co.z += amp * math.sin(v.co.x * freq) + amp * 0.25 * math.sin(v.co.x * freq * 2.75)
    c.data.materials.append(velvet)
    return c


def chandelier(cy, base_z, tiers=((2.4, 0.0), (1.7, 0.6), (1.0, 1.2)), scale=1.0, energy=120, cx=0.0):
    energy *= ROOM_LIGHT
    """Stepped gilt rings hung with small lamps."""
    for k, (rad, dz) in enumerate(tiers):
        rad *= scale
        z = base_z + dz * scale
        bpy.ops.mesh.primitive_torus_add(major_radius=rad, minor_radius=0.05 * scale, location=(cx, cy, z))
        bpy.context.object.data.materials.append(gilt)
        drops = max(4, 10 - 2 * k)
        for q in range(drops):
            a = 2 * math.pi * q / drops
            bpy.ops.mesh.primitive_uv_sphere_add(radius=0.09 * scale,
                                                 location=(cx + rad * math.cos(a), cy + rad * math.sin(a),
                                                           z - 0.12 * scale))
            bpy.context.object.data.materials.append(mat("drop", (1, 0.85, 0.6), emit=(1, 0.8, 0.55), emit_strength=12))
    top = base_z + tiers[-1][1] * scale
    box("chain", (cx, cy, top + 2 * scale), (0.05, 0.05, 4 * scale), gilt)
    light("chand_l", "POINT", (cx, cy, base_z - 0.4), energy, (1.0, 0.82, 0.55), size=2.0 * scale)


def deco_wall(y, half_width=26, height=20):
    """A back wall of recessed panels with thin gilt reeds between them: Deco relief, cheap."""
    box("wall", (0, y, floor_z + height / 2), (half_width * 2, 0.6, height), stone)
    panel = mat("relief_dark", (0.024, 0.019, 0.022), rough=0.45)
    for i in range(-6, 7):
        x = i * 3.8
        for h, dz in ((4.6, 3.4), (2.6, 8.6)):
            box("relief", (x, y - 0.33, floor_z + dz), (2.9, 0.06, h), panel)
        # A slim vertical gilt reed between the panels, and a cap rail above them.
        box("reed", (x + 1.9, y - 0.35, floor_z + height * 0.33), (0.07, 0.05, height * 0.56), gilt)
    box("rail", (0, y - 0.35, floor_z + 11.4), (half_width * 2, 0.06, 0.12), gilt)


def vault_door(y, cz=6.2, radius=2.5):
    """A round brass vault door set into the wall: rings, spokes, a small boss."""
    steel = mat("steel", (0.10, 0.095, 0.10), rough=0.28, metal=1.0)
    for r, t, m in ((radius, 0.45, gilt), (radius * 0.80, 0.40, steel), (radius * 0.40, 0.34, steel)):
        bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=r, depth=t, location=(0, y - 0.3 - t / 2, cz),
                                            rotation=(math.radians(90), 0, 0))
        bpy.context.object.data.materials.append(m)
    for q in range(8):
        a = 2 * math.pi * q / 8
        bpy.ops.mesh.primitive_cube_add(location=(radius * 0.55 * math.cos(a), y - 0.78, cz + radius * 0.55 * math.sin(a)))
        s = bpy.context.object
        s.scale = (0.07, 0.07, radius * 0.44)
        s.rotation_euler = (a, math.radians(90), 0)
        bpy.ops.object.transform_apply(scale=True)
        s.data.materials.append(gilt)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.3, location=(0, y - 0.88, cz))
    bpy.context.object.data.materials.append(gilt)
    # Two uplights washing the door.
    for x in (-4.5, 4.5):
        light("uplight", "SPOT", (x, y - 3.0, floor_z + 0.4), 700, (1.0, 0.78, 0.5), size=0.4,
              rot=(math.radians(-58), 0, 0), spot=50)


# Multiplies the room's own fixtures - the chandeliers and the velvet rakes -
# and never the table's pools. The V3 review's lesson was to light the table and
# not the room, so these are the only lights it is safe to turn up.
ROOM_LIGHT = float(os.environ.get("ROOM_LIGHT", "1"))


def rake_velvet(y, cz=8.0, energy=1400, reach=22.0):
    """Two spots raking along the cloth from either side.

    The folds are displaced in depth, so they all face the table equally: light
    from the front leaves the wall a flat maroon field (V3's first pass). Only
    light travelling *along* the wall shades one side of each fold.
    """
    for side in (-1, 1):
        light("rake", "SPOT", (side * reach, y - 4.0, cz), energy * ROOM_LIGHT, (1.0, 0.70, 0.52), size=0.5,
              rot=(math.radians(90), 0, math.radians(side * 81)), spot=42)


if VARIANT == "room":
    if ROOM == "salon":
        carpet_floor()
        # The velvet wall, lifted off near-black so the folds have something to shade.
        _v = float(os.environ.get("VELVET", "1"))
        velvet.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.082 * _v, 0.011 * _v, 0.026 * _v, 1)
        curtain(0, 17.5, 24, 21, waves=(1.15, 1.05), cuts=140)
        box("pelmet", (0, 17.2, floor_z + 20.2), (49, 0.9, 1.1), velvet)
        box("pelmet_rail", (0, 16.7, floor_z + 19.6), (49, 0.12, 0.16), gilt)
        # A skirting board at the hem, so the folds meet something instead of
        # floating: the wavy hem read as distant hills without it.
        box("skirting", (0, 16.4, floor_z + 0.28), (49, 0.5, 0.56), stone)
        rake_velvet(17.5)
        # A pair, flanking the wordmark instead of fighting it. One chandelier dead
        # centre lands exactly where NONA ROYALE goes (V3, first salon pass); two
        # keeps the Deco symmetry and leaves the middle of the upper frame free.
        for cx in (-9.5, 9.5):
            chandelier(10.5, 3.2, scale=1.05, energy=170, cx=cx)
        # A's ring of sconces, on side columns that frame the table.
        for y in (12.5, 4.5, -3.5):
            column(-15.5, y, width=1.35); column(15.5, y, width=1.35)
    elif ROOM == "parlour":
        carpet_floor()
        for x in (-15, -7.5, 0, 7.5, 15):
            column(x, 15)
        for y in (7, -1):
            column(-15.5, y); column(15.5, y)
        for x0 in (-11.25, -3.75, 3.75, 11.25):
            curtain(x0, 15.6, 3.2, 13)
        chandelier(10, 10.4)
    elif ROOM == "vault":
        carpet_floor()
        deco_wall(18.5)
        vault_door(18.2)
        for x in (-15.5, 15.5):
            column(x, 12, width=1.8)
            column(x, 1, width=1.8)
    elif ROOM == "curtain":
        carpet_floor()
        # One wall of velvet, folded at a longer wavelength so the folds read from far off.
        # Lifted off near-black, or the folds are a flat maroon field at this distance.
        velvet.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.10, 0.014, 0.030, 1)
        curtain(0, 17.5, 24, 21, waves=(1.15, 1.05), cuts=140)
        # A pelmet, so it reads as hung cloth and not a painted wall.
        box("pelmet", (0, 17.2, floor_z + 20.2), (49, 0.9, 1.1), velvet)
        box("pelmet_rail", (0, 16.7, floor_z + 19.6), (49, 0.12, 0.16), gilt)
        # Two grazing lights along the cloth: velvet only reads when the folds cast.
        for x in (-13, 13):
            light("graze", "SPOT", (x, 8.0, floor_z + 17.5), 900, (1.0, 0.74, 0.52), size=0.6,
                  rot=(math.radians(58), 0, 0), spot=60)
        chandelier(8.5, 7.0, scale=1.7, energy=260)
    else:  # bare
        carpet_floor(pattern=True)
        # No geometry at all: one dim, very wide wash from far back and above, so the
        # carpet's pattern is just legible and the table is the only object in the room.
        light("far", "AREA", (0, 26, 16), 900, (1.0, 0.80, 0.56), size=26,
              rot=(math.radians(52), 0, 0))
        light("floorfill", "AREA", (0, 2, 13), 260, (1.0, 0.86, 0.68), size=34,
              rot=(0, 0, 0))
else:
    bpy.ops.mesh.primitive_plane_add(size=120, location=(0, 0, floor_z))
    bpy.context.object.data.materials.append(mat("void", (0.006, 0.005, 0.006), rough=0.9))

# ── Figures ────────────────────────────────────────────────────────────
# FIGURES=0 leaves the table empty, for judging a room without the stand-in
# cast reading as the subject of the frame (V3, 2026-09-18).
FIGURES = os.environ.get("FIGURES", "1") != "0"
TILT = PITCH * 0.55  # lean the cards back part-way toward the camera
seats = {"red": (0.84, 0.27, 0.31), "blue": (0.32, 0.56, 0.88), "green": (0.34, 0.72, 0.44), "violet": (0.5, 0.4, 0.84)}
standing = [((1, 4), "red"), ((-1, -3), "blue"), ((5, -1), "green"), ((0, 6), "red"), ((-5, 1), "violet"), ((1, -5), "violet")]
for i, ((x, y), s) in enumerate(standing if FIGURES else []):
    disc(f"base{i}", (x, y, 0.012), 0.34, seats[s], 0.08)
    sprite(f"fig{i}", ART + "luka_standing.png", (x, y, 0.02), 1.55, None, TILT)
for k, (cx, cy) in enumerate([(-4.5, 4.5), (4.5, 4.5), (-4.5, -4.5), (4.5, -4.5)] if FIGURES else []):
    for j, ang in enumerate((180, 90, 0)):
        if (k + j) % 3 == 0: continue
        a = math.radians(ang)
        sx, sy = cx + math.cos(a) * 1.375, cy + math.sin(a) * 1.375
        sprite(f"seat{k}{j}", ART + "luka_seated.png", (sx, sy, 0.02), 1.15, None, TILT)

# ── Room light (the game's pools, as real lights) ──────────────────────
light("vault", "SPOT", (0, 0, 9), 3600, (1.0, 0.83, 0.56), size=1.5, spot=40)
for (x, y) in ((0, 4.5), (-4.5, 0), (0, -4.5), (4.5, 0)):
    light("arm", "SPOT", (x, y, 9), 2000, (1.0, 0.9, 0.74), size=1.5, spot=38)
for (x, y) in ((-4.5, 4.5), (4.5, 4.5), (-4.5, -4.5), (4.5, -4.5)):
    light("table", "SPOT", (x, y, 9), 2600, (1.0, 0.88, 0.7), size=1.2, spot=34)
# Key from upper left for the figures. A spot, not an area lamp: an area lamp this
# size washes the whole carpet, and a lit floor reads as a milky haze under the
# table instead of a dark room (found in the V3 title mockups).
light("key", "SPOT", (-12, -10, 14), 2600, (1.0, 0.86, 0.7), size=1.6,
      rot=(math.radians(45), 0, math.radians(-50)), spot=52)
# Cool rim from behind, narrowed for the same reason.
light("rim", "SPOT", (4, 12, 6), 500, (0.6, 0.85, 1.0), size=1.2,
      rot=(math.radians(65), 0, math.radians(162)), spot=60)
# Powered safe cells: small cyan lamps just above the floor.
for (x, y) in ((1, 7), (-1, 3), (-7, 1), (-3, -1), (-1, -7), (1, -3), (7, -1), (3, 1)):
    light("cyan", "POINT", (x, y, 0.35), 6, (0.37, 0.88, 0.91), size=0.2)

# ── Camera ─────────────────────────────────────────────────────────────
cam_d = bpy.data.cameras.new("cam"); cam_d.lens = float(os.environ.get("LENS", "30"))
cam = bpy.data.objects.new("cam", cam_d); scn.collection.objects.link(cam); scn.camera = cam
pitch = math.radians(PITCH)  # tilt from straight down

def place(dist, ty):
    target = Vector((0, ty, 0))
    cam.location = target + Vector((0, -math.sin(pitch) * dist, math.cos(pitch) * dist))
    cam.rotation_euler = (pitch, 0, 0)
    bpy.context.view_layer.update()

from bpy_extras.object_utils import world_to_camera_view
FIT = os.environ.get("FIT", "hud")
# Free rectangle in normalized view coords (x right, y up), from the 1080 reservations.
if FIT == "hud":
    fx0, fx1 = 290 / 1920, 1 - 78 / 1920
    fy0, fy1 = 196 / 1080, 1 - 56 / 1080
elif FIT == "title":
    fx0, fx1, fy0, fy1 = 0.2, 0.8, 0.04, 0.6
elif FIT == "title_top":
    # The band the chosen title layout leaves: under the wordmark, over the
    # button row, nearly the full width (VISUAL_PASS.md, V3).
    fx0, fx1, fy0, fy1 = 0.08, 0.92, 0.20, 0.76
else:
    fx0, fx1, fy0, fy1 = 0.06, 0.94, 0.06, 0.94
margin = 0.02
h = SIDE / 2 + 0.1
corners = [Vector((x, y, z)) for x in (-h, h) for y in (-h, h) for z in (0.0, -0.7)]
if FIT == "hud":
    cam_d.shift_x = -((fx0 + fx1) / 2 - 0.5)
    cam_d.shift_y = -((fy0 + fy1) / 2 - 0.5) * (H / W)
best = None
# The search range scales with the lens. A long lens needs proportionally more
# distance for the same framing, and 14-43 was tuned for the 30mm default: at
# the shipping camera's 96mm nothing in the old range fits, so the fit silently
# failed. k is 1 at 30mm, so every earlier render is reproduced exactly.
_k = cam_d.lens / 30.0
for dist in [(14 + 0.25 * i) * _k for i in range(120)]:
    for ty in [-4 + 0.1 * j for j in range(81)]:
        place(dist, ty)
        pts = [world_to_camera_view(scn, cam, c) for c in corners]
        xs = [p.x for p in pts]; ys = [p.y for p in pts]
        if min(xs) < fx0 + margin or max(xs) > fx1 - margin: continue
        if min(ys) < fy0 + margin or max(ys) > fy1 - margin: continue
        # Prefer the board as large as possible, centred vertically in the free band.
        area = (max(xs) - min(xs)) * (max(ys) - min(ys))
        off = abs((min(ys) + max(ys)) / 2 - (fy0 + fy1) / 2)
        score = area - off
        if best is None or score > best[0]:
            best = (score, dist, ty)
    if best is not None and best[1] < dist - 2 * _k:
        break
print("FIT", best)
place(best[1], best[2])

# Figures face the camera: rotate each card about Z toward it (after camera exists).
for o in bpy.data.objects:
    if o.name.startswith(("fig", "seat")):
        d = cam.location - o.location
        o.rotation_euler = (math.radians(90 - TILT), 0, math.atan2(d.x, -d.y))

scn.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("RENDERED", OUT)
