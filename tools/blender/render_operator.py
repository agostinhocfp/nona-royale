# tools/blender/render_operator.py
"""
Renders an operator's board sprites from a rigged Meshy/Mixamo GLB
(ADR-0009, ART_PIPELINE.md §2, ART_HOOKUP.md).

What it does, in order:
  1. Imports the GLB into an empty scene and stops its animation.
  2. Fixes the proportions (head up, legs down, optional arms) and bakes them
     into the rest pose, then stands the figure back on the floor.
  3. Poses and renders three images with a fixed orthographic camera at the
     board angle, toon shading, a warm key, a cool rim and a Freestyle outline:
       <name>_standing.png  512x768, full figure, arms down
       <name>_seated.png    512x512, waist up, forearms forward (the yard bust)
       <name>_portrait.png  512x512, head and shoulders
  4. Optionally saves the posed scene as a .blend, to tweak by hand.

With --reference it renders a Meshy multi-view sheet instead: the corrected
body in an A-pose, flat-lit (texture only), no outline, on plain grey, at eye
level, 1024x1024, into <out>/reference/:
       <name>_ref_front.png  <name>_ref_side.png  <name>_ref_back.png
       <name>_ref_threequarter.png
Upload those to Meshy (multi-view image to 3D) to regenerate the model at
6 heads natively, instead of stretching bones on every render.

Run it from the repo root (Windows):
  "C:\\Program Files\\Blender Foundation\\Blender 4.5\\blender.exe" --background ^
      --python tools\\blender\\render_operator.py -- ^
      --model art\\source\\characters\\luka\\luka_walk.glb --name luka

Options (after the lone "--"):
  --model PATH       the GLB (required)
  --name NAME        file-name stem, lowercase (required)
  --out DIR          output folder (default art/renders/<name>)
  --head 1.3         head scale; 1 keeps the model's
  --legs 0.83        leg length scale
  --arms 0.92        arm length scale
  --yaw 30           camera yaw from the figure's front, degrees (+ = figure's left)
  --pitch 25         camera pitch down, degrees (the board angle)
  --line 1.6         outline thickness, pixels
  --only standing    render one image: standing, seated or portrait
  --save-blend PATH  also save the posed scene
  --preview          quarter-size renders, few samples (for checking)
  --reference        render the Meshy multi-view sheet instead of the sprites

Written for Blender 4.0 and later. The engine is EEVEE (toon shading needs
Shader to RGB, which Cycles does not support).
"""

import argparse
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

OUTLINE = (0x1C / 255, 0x0E / 255, 0x12 / 255)
KEY_COLOUR = (1.0, 0.86, 0.70)
RIM_COLOUR = (0.55, 0.75, 1.0)
AMBIENT = (0.10, 0.09, 0.11)

# Plain grey behind the reference sheet: 115/255 in sRGB, as the concept crops.
REFERENCE_GREY = (0.171, 0.171, 0.171)

# Mixamo bone names, as Meshy exports them.
B = "mixamorig:"
HEAD = B + "Head"
HIPS = B + "Hips"
NECK = B + "Neck"
SPINE = (B + "Spine", B + "Spine1", B + "Spine2")
LEGS = (B + "LeftUpLeg", B + "RightUpLeg")
ARMS = (B + "LeftArm", B + "RightArm")


# ── Arguments ────────────────────────────────────────────────────────────

def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="render_operator.py")
    p.add_argument("--model", required=True)
    p.add_argument("--name", required=True)
    p.add_argument("--out", default=None)
    p.add_argument("--head", type=float, default=1.3)
    p.add_argument("--legs", type=float, default=0.83)
    p.add_argument("--arms", type=float, default=0.92)
    p.add_argument("--yaw", type=float, default=30.0)
    p.add_argument("--pitch", type=float, default=25.0)
    p.add_argument("--line", type=float, default=1.6)
    p.add_argument("--only", choices=("standing", "seated", "portrait"), default=None)
    p.add_argument("--save-blend", default=None)
    p.add_argument("--preview", action="store_true")
    p.add_argument("--reference", action="store_true")
    args = p.parse_args(argv)
    if args.out is None:
        args.out = os.path.join("art", "renders", args.name)
    return args


# ── Scene ────────────────────────────────────────────────────────────────

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_model(path):
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(path))
    arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
    # Only the skinned meshes: the importer also adds helper shapes (an
    # icosphere for bone display) that must not be framed or rendered.
    meshes = [o for o in bpy.context.scene.objects
              if o.type == "MESH" and any(m.type == "ARMATURE" and m.object == arm for m in o.modifiers)]
    for o in bpy.context.scene.objects:
        if o.type == "MESH" and o not in meshes:
            o.hide_render = True
    if not meshes:
        raise SystemExit("No mesh in " + path)

    # The clip would override every pose; the rest pose is the start point.
    if arm.animation_data:
        arm.animation_data.action = None
        for track in list(arm.animation_data.nla_tracks):
            arm.animation_data.nla_tracks.remove(track)
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    update()
    return arm, meshes


def update():
    bpy.context.view_layer.update()


def set_mode(obj, mode):
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    if mode != "OBJECT":
        bpy.ops.object.mode_set(mode=mode)


def bone_head(arm, name):
    return arm.matrix_world @ arm.pose.bones[name].head


def body_frame(arm):
    """The figure's forward, left and up, in world space, from its feet and hips."""
    up = Vector((0, 0, 1))
    left = bone_head(arm, LEGS[0]) - bone_head(arm, LEGS[1])
    left.z = 0
    left.normalize()
    # Right-handed: forward x left = up, so forward = left x up.
    forward = left.cross(up).normalized()

    # The toes should agree. If they do not, the model is mirrored.
    toe = bone_head(arm, B + "LeftToeBase") - bone_head(arm, B + "LeftFoot")
    toe.z = 0
    if toe.length > 1e-4 and toe.dot(forward) < 0:
        print("[render_operator] warning: the toes point backward; is the model mirrored?")
    return forward, left, up


def posed_points(objs):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    points = []
    for obj in objs:
        ev = obj.evaluated_get(depsgraph)
        mesh = ev.to_mesh()
        mw = ev.matrix_world
        points.extend(mw @ v.co for v in mesh.vertices)
        ev.to_mesh_clear()
    return points


# ── Proportions ──────────────────────────────────────────────────────────

def scale_bone_length(pb, factor):
    pb.scale = (1.0, factor, 1.0)


def fix_proportions(arm, meshes, head, legs, arms):
    """Scales in pose mode, then bakes the result into the mesh and the rest pose."""
    if head == 1 and legs == 1 and arms == 1:
        return

    pose = arm.pose.bones
    if head != 1:
        pose[HEAD].scale = (head, head, head)

    # A thigh scaled along its length shortens the shin with it. The feet
    # would inherit the squash, so they keep their own scale.
    if legs != 1:
        for name in LEGS:
            scale_bone_length(pose[name], legs)
        for side in ("Left", "Right"):
            arm.data.bones[B + side + "Foot"].inherit_scale = "NONE"

    if arms != 1:
        for name in ARMS:
            scale_bone_length(pose[name], arms)
        for side in ("Left", "Right"):
            arm.data.bones[B + side + "Hand"].inherit_scale = "NONE"
    update()

    # Bake: apply each mesh's armature modifier, apply the pose as rest,
    # then bind the meshes again.
    for mesh in meshes:
        mods = [m for m in mesh.modifiers if m.type == "ARMATURE"]
        for mod in mods:
            name = mod.name
            set_mode(mesh, "OBJECT")
            bpy.ops.object.modifier_apply(modifier=name)
            new = mesh.modifiers.new(name, "ARMATURE")
            new.object = arm

    set_mode(arm, "POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for bone in arm.data.bones:
        bone.inherit_scale = "FULL"
    update()

    # Stand it back on the floor.
    lowest = min(p.z for p in posed_points(meshes))
    root = arm
    while root.parent is not None:
        root = root.parent
    root.location.z -= lowest
    update()


# ── Posing ───────────────────────────────────────────────────────────────

def aim(arm, name, world_direction):
    """Turns a bone so it points along a world direction, keeping its head in place."""
    pb = arm.pose.bones[name]
    update()
    to_arm = arm.matrix_world.to_3x3().inverted()
    want = (to_arm @ Vector(world_direction)).normalized()
    current = pb.matrix.copy()
    have = (current.to_3x3() @ Vector((0, 1, 0))).normalized()
    turn = have.rotation_difference(want).to_matrix()
    turned = (turn @ current.to_3x3()).to_4x4()
    turned.translation = current.translation
    pb.matrix = turned
    update()


def rest_pose(arm):
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    update()


def pose_standing(arm):
    rest_pose(arm)
    forward, left, up = body_frame(arm)
    down = -up
    # The rest pose tips the head down; lift the face to the camera.
    aim(arm, NECK, up)
    aim(arm, HEAD, up - forward * 0.10)
    for side, out in (("Left", left), ("Right", -left)):
        aim(arm, B + side + "Arm", down + out * 0.22 + forward * 0.03)
        aim(arm, B + side + "ForeArm", down + out * 0.10 + forward * 0.22)
        aim(arm, B + side + "Hand", down + out * 0.05 + forward * 0.18)


def pose_a(arm):
    """The neutral A-pose Meshy expects: arms down and out, head level."""
    rest_pose(arm)
    forward, left, up = body_frame(arm)
    down = -up
    aim(arm, NECK, up)
    aim(arm, HEAD, up - forward * 0.10)
    for side, out in (("Left", left), ("Right", -left)):
        for bone in ("Arm", "ForeArm", "Hand"):
            aim(arm, B + side + bone, down + out * 0.75)


def pose_seated(arm):
    """Waist up, leaning in, forearms forward on an unseen table."""
    rest_pose(arm)
    forward, left, up = body_frame(arm)
    down = -up
    aim(arm, SPINE[1], up + forward * 0.12)
    aim(arm, SPINE[2], up + forward * 0.10)
    aim(arm, NECK, up)
    aim(arm, HEAD, up - forward * 0.14)
    for side, out in (("Left", left), ("Right", -left)):
        aim(arm, B + side + "Arm", down + forward * 0.30 + out * 0.15)
        aim(arm, B + side + "ForeArm", forward * 0.8 - out * 0.65 + up * 0.05)
        aim(arm, B + side + "Hand", forward * 0.5 - out * 0.6 - up * 0.05)


# ── Look ─────────────────────────────────────────────────────────────────

def set_engine(scene):
    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            scene.render.engine = engine
            return engine
        except TypeError:
            continue
    raise SystemExit("EEVEE is not available in this Blender.")


def toon_materials(meshes, flat=False):
    """Base colour texture × a three-step light ramp, as emission. Flat: the texture alone."""
    done = set()
    for mesh in meshes:
        for slot in mesh.material_slots:
            mat = slot.material
            if mat is None or mat.name in done:
                continue
            done.add(mat.name)
            mat.use_nodes = True
            nodes, links = mat.node_tree.nodes, mat.node_tree.links

            base = None
            colour = (0.8, 0.8, 0.8, 1.0)
            bsdf = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
            if bsdf is not None:
                socket = bsdf.inputs["Base Color"]
                colour = tuple(socket.default_value)
                if socket.is_linked:
                    base = socket.links[0].from_socket
            out = next((n for n in nodes if n.type == "OUTPUT_MATERIAL"), None) or nodes.new("ShaderNodeOutputMaterial")

            diffuse = nodes.new("ShaderNodeBsdfDiffuse")
            to_rgb = nodes.new("ShaderNodeShaderToRGB")
            ramp = nodes.new("ShaderNodeValToRGB")
            ramp.color_ramp.interpolation = "CONSTANT"
            stops = ramp.color_ramp.elements
            stops[0].position, stops[0].color = 0.0, (0.42, 0.40, 0.46, 1)
            stops[1].position, stops[1].color = 0.18, (0.78, 0.76, 0.78, 1)
            third = stops.new(0.55)
            third.color = (1.0, 1.0, 1.0, 1)

            multiply = nodes.new("ShaderNodeMixRGB")
            multiply.blend_type = "MULTIPLY"
            multiply.inputs["Fac"].default_value = 1.0
            emission = nodes.new("ShaderNodeEmission")

            links.new(diffuse.outputs["BSDF"], to_rgb.inputs["Shader"])
            links.new(to_rgb.outputs["Color"], ramp.inputs["Fac"])
            if base is not None:
                links.new(base, multiply.inputs["Color1"])
            else:
                multiply.inputs["Color1"].default_value = colour
            if flat:
                multiply.inputs["Color2"].default_value = (1.0, 1.0, 1.0, 1.0)
            else:
                links.new(ramp.outputs["Color"], multiply.inputs["Color2"])
            links.new(multiply.outputs["Color"], emission.inputs["Color"])
            links.new(emission.outputs["Emission"], out.inputs["Surface"])

            # Cut-out alpha from the texture, if the material had any.
            if hasattr(mat, "blend_method"):
                mat.blend_method = "OPAQUE"


def lights(arm, scene):
    forward, left, up = body_frame(arm)

    def sun(name, direction_to_light, colour, energy):
        data = bpy.data.lights.new(name, "SUN")
        data.color = colour
        data.energy = energy
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        # A sun shines along its local -Z.
        obj.rotation_euler = (-Vector(direction_to_light)).to_track_quat("-Z", "Y").to_euler()

    # Warm key from the upper left (as the viewer sees it), cool rim from behind.
    sun("Key", forward * 0.8 + left * 0.9 + up * 1.1, KEY_COLOUR, 3.2)
    sun("Rim", -forward * 1.0 - left * 0.6 + up * 0.5, RIM_COLOUR, 4.0)

    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (*AMBIENT, 1)
    scene.world = world


def outline(scene, thickness):
    scene.render.use_freestyle = True
    scene.render.line_thickness_mode = "ABSOLUTE"
    layer = bpy.context.view_layer
    layer.use_freestyle = True
    settings = layer.freestyle_settings
    lineset = settings.linesets[0] if settings.linesets else settings.linesets.new("Outline")
    lineset.select_by_visibility = True
    lineset.select_silhouette = True
    lineset.select_border = True
    # Creases draw scratch marks across painted cloth; the texture has its own folds.
    lineset.select_crease = False
    lineset.select_contour = True
    if lineset.linestyle is None:
        lineset.linestyle = bpy.data.linestyles.new("Outline")
    style = lineset.linestyle
    style.color = OUTLINE
    style.thickness = thickness


def render_settings(scene, preview):
    set_engine(scene)
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.resolution_percentage = 25 if preview else 100
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    eevee = scene.eevee
    for attr, value in (("taa_render_samples", 4 if preview else 32),):
        if hasattr(eevee, attr):
            setattr(eevee, attr, value)


# ── Camera ───────────────────────────────────────────────────────────────

def camera(scene):
    data = bpy.data.cameras.new("Board")
    data.type = "ORTHO"
    obj = bpy.data.objects.new("Board", data)
    scene.collection.objects.link(obj)
    scene.camera = obj
    return obj


def frame(cam, arm, points, yaw, pitch, width, height, margin=0.06):
    """Aims the camera at the points from the board angle and fits them, with a margin."""
    scene = bpy.context.scene
    scene.render.resolution_x = width
    scene.render.resolution_y = height

    forward, left, up = body_frame(arm)
    yaw_r, pitch_r = math.radians(yaw), math.radians(pitch)
    horizontal = forward * math.cos(yaw_r) + left * math.sin(yaw_r)
    toward_camera = (horizontal * math.cos(pitch_r) + up * math.sin(pitch_r)).normalized()

    print("[render_operator] framing %d points, z %.3f..%.3f" % (len(points), min(p.z for p in points), max(p.z for p in points)))
    centre = sum(points, Vector()) / len(points)
    cam.location = centre + toward_camera * 10.0
    cam.rotation_euler = (-toward_camera).to_track_quat("-Z", "Y").to_euler()
    update()

    # Fit in camera space.
    inv = cam.matrix_world.inverted()
    local = [inv @ p for p in points]
    xs = [p.x for p in local]
    ys = [p.y for p in local]
    span_x = max(xs) - min(xs)
    span_y = max(ys) - min(ys)
    # Ortho scale spans the longer image side.
    if width >= height:
        scale = max(span_x, span_y * width / height)
    else:
        scale = max(span_y, span_x * height / width)
    cam.data.ortho_scale = scale * (1 + 2 * margin)
    print("[render_operator] span %.3f x %.3f, ortho %.3f" % (span_x, span_y, cam.data.ortho_scale))

    # Centre the box.
    mid = Vector(((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, 0))
    cam.location = cam.matrix_world @ mid
    update()


def frame_bottom_at(cam, points_to_fit, cut_z, arm, yaw, pitch, width, height):
    """Frames the points above cut_z so the image's bottom edge falls at cut_z."""
    above = [p for p in points_to_fit if p.z >= cut_z]
    frame(cam, arm, above, yaw, pitch, width, height)


def render(path):
    scene = bpy.context.scene
    scene.render.filepath = os.path.abspath(path)
    bpy.ops.render.render(write_still=True)
    print("[render_operator] wrote", path)


def render_reference(cam, arm, meshes, name, out):
    """Front, side, back and three-quarter views on one scale, at eye level."""
    scene = bpy.context.scene
    scene.render.film_transparent = False
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024

    world = bpy.data.worlds.new("Reference")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (*REFERENCE_GREY, 1)
    scene.world = world

    pose_a(arm)
    points = posed_points(meshes)
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    centre = (low + high) / 2
    size = high - low
    # One scale for every view, so Meshy sees the same body size in each.
    cam.data.ortho_scale = max(size.z, math.hypot(size.x, size.y)) * 1.1

    forward, left, up = body_frame(arm)
    folder = os.path.join(out, "reference")
    os.makedirs(folder, exist_ok=True)
    for view, yaw in (("front", 0.0), ("side", 90.0), ("back", 180.0), ("threequarter", 35.0)):
        r = math.radians(yaw)
        toward_camera = (forward * math.cos(r) + left * math.sin(r)).normalized()
        cam.location = centre + toward_camera * 10.0
        cam.rotation_euler = (-toward_camera).to_track_quat("-Z", "Y").to_euler()
        update()
        render(os.path.join(folder, "%s_ref_%s.png" % (name, view)))


# ── Main ─────────────────────────────────────────────────────────────────

def main():
    args = parse_args()
    reset_scene()
    arm, meshes = import_model(args.model)
    fix_proportions(arm, meshes, args.head, args.legs, args.arms)

    scene = bpy.context.scene
    render_settings(scene, args.preview)
    cam = camera(scene)

    if args.reference:
        toon_materials(meshes, flat=True)
        render_reference(cam, arm, meshes, args.name, args.out)
        if args.save_blend:
            bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args.save_blend))
        return

    toon_materials(meshes)
    lights(arm, scene)
    outline(scene, args.line)
    os.makedirs(args.out, exist_ok=True)

    wanted = [args.only] if args.only else ["standing", "seated", "portrait"]

    if "standing" in wanted:
        pose_standing(arm)
        frame(cam, arm, posed_points(meshes), args.yaw, args.pitch, 512, 768)
        render(os.path.join(args.out, args.name + "_standing.png"))

    if "seated" in wanted:
        pose_seated(arm)
        hips_z = bone_head(arm, HIPS).z
        # The image stops a little above the hips: the table hides the rest.
        frame_bottom_at(cam, posed_points(meshes), hips_z + 0.05, arm, args.yaw, args.pitch, 512, 512)
        render(os.path.join(args.out, args.name + "_seated.png"))

    if "portrait" in wanted:
        pose_standing(arm)
        neck_z = bone_head(arm, NECK).z
        frame_bottom_at(cam, posed_points(meshes), neck_z - 0.12, arm, args.yaw * 0.7, 8.0, 512, 512)
        render(os.path.join(args.out, args.name + "_portrait.png"))

    if args.save_blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args.save_blend))


if __name__ == "__main__":
    main()
