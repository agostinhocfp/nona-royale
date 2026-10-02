"""tools/art/make_fx_materials.py — CG16: the saved All In 1 Sprite Shader materials for the piece effects.

Writes <name>.mat and <name>.mat.meta into the folder given, in Unity's own
YAML. Only the properties these effects use are listed; Unity fills the rest
from the shader's defaults, as it does for BurnLit and ShineLit.
"""
import sys, uuid, os

PLAIN = "a36b7719ff0465b42ab1407d67672c5f"   # AllIn1SpriteShader (unlit)
URP2D = "202ffec9202f22e478034613d6beb49c"   # AllIn1Urp2dRenderer (lit)
NOISE = "7aad8c583ef292e48b06af0d1f2fab97"   # Textures/seamlessNoise.png

COMMON_FLOATS = {"_Alpha": 1, "_CullingOption": 0, "_MyDstMode": 10, "_MySrcMode": 5, "_ZTestMode": 4, "_ZWrite": 0}

EFFECTS = {
    "Grey": dict(
        keywords=["GREYSCALE_ON"],
        floats={"_GreyscaleBlend": 1, "_GreyscaleLuminosity": -0.04},
        colors={"_GreyscaleTintColor": (0.92, 0.95, 1, 1)},
        textures={},
    ),
    "Holo": dict(
        keywords=["GLITCH_ON", "HOLOGRAM_ON"],
        floats={"_HologramStripesAmount": 0.12, "_HologramUnmodAmount": 0, "_HologramStripesSpeed": 4.5,
                "_HologramMinAlpha": 0.15, "_HologramMaxAlpha": 0.8, "_HologramBlend": 1,
                "_GlitchAmount": 1.6, "_GlitchSize": 1, "_GlitchSpeed": 5},
        colors={"_HologramStripeColor": (1, 0.2, 0.85, 1)},
        textures={},
    ),
    "Shimmer": dict(
        keywords=["DISTORT_ON"],
        floats={"_DistortAmount": 0.6, "_DistortTexXSpeed": 2, "_DistortTexYSpeed": 4},
        colors={},
        textures={"_DistortTex": NOISE},
    ),
}


def colour(c):
    r, g, b, a = c
    return f"{{r: {r}, g: {g}, b: {b}, a: {a}}}"


def material(name, shader, effect, lit):
    floats = dict(COMMON_FLOATS)
    floats.update(effect["floats"])
    if lit:
        floats["_LitAmount"] = 1
    colors = {"_Color": (1, 1, 1, 1), "_MainTex_ScaleAndTiling": (1, 1, 0, 0)}
    colors.update(effect["colors"])
    for tex in effect["textures"]:
        colors[f"{tex}_ScaleAndTiling"] = (1, 1, 0, 0)

    tex_envs = dict(effect["textures"])
    tex_envs["_MainTex"] = None

    out = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!21 &2100000",
        "Material:",
        "  serializedVersion: 8",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_Name: {name}",
        f"  m_Shader: {{fileID: 4800000, guid: {shader}, type: 3}}",
        "  m_Parent: {fileID: 0}",
        "  m_ModifiedSerializedProperties: 0",
        "  m_ValidKeywords:",
        *[f"  - {k}" for k in sorted(effect["keywords"])],
        "  m_InvalidKeywords: []",
        "  m_LightmapFlags: 4",
        "  m_EnableInstancingVariants: 0",
        "  m_DoubleSidedGI: 0",
        "  m_CustomRenderQueue: -1",
        "  stringTagMap: {}",
        "  disabledShaderPasses: []",
        "  m_LockedProperties: ",
        "  m_SavedProperties:",
        "    serializedVersion: 3",
        "    m_TexEnvs:",
    ]
    for tex in sorted(tex_envs):
        guid = tex_envs[tex]
        ref = f"{{fileID: 2800000, guid: {guid}, type: 3}}" if guid else "{fileID: 0}"
        out += [f"    - {tex}:", f"        m_Texture: {ref}", "        m_Scale: {x: 1, y: 1}", "        m_Offset: {x: 0, y: 0}"]
    out += ["    m_Ints: []", "    m_Floats:"]
    out += [f"    - {k}: {floats[k]}" for k in sorted(floats)]
    out += ["    m_Colors:"]
    out += [f"    - {k}: {colour(colors[k])}" for k in sorted(colors)]
    out += ["  m_BuildTextureStacks: []", "  m_AllowLocking: 1", ""]
    return "\n".join(out)


def meta(guid):
    return "\n".join([
        "fileFormatVersion: 2",
        f"guid: {guid}",
        "NativeFormatImporter:",
        "  externalObjects: {}",
        "  mainObjectFileID: 2100000",
        "  userData: ",
        "  assetBundleName: ",
        "  assetBundleVariant: ",
        "",
    ])


folder = sys.argv[1]
for base, effect in EFFECTS.items():
    for suffix, shader, lit in (("Unlit", PLAIN, False), ("Lit", URP2D, True)):
        name = base + suffix
        path = os.path.join(folder, name + ".mat")
        if os.path.exists(path + ".meta"):
            sys.exit(f"{name} already exists; not overwriting its guid")
        with open(path, "w", newline="\n") as f:
            f.write(material(name, shader, effect, lit))
        with open(path + ".meta", "w", newline="\n") as f:
            f.write(meta(uuid.uuid4().hex))
        print("wrote", name)
