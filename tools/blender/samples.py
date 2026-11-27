"""
US-271: the three graphics samples (ADR 0172 §7, spec 032): a family house, a wheat field and a farm hand (a man and a woman),
each drawn twice from the same models, camera and light direction as the game's sprites, so only the style differs:

  rich  - rich stylised: the game's toon look and warm outline, with painted texture on every surface and more modelled detail.
  real  - realistic: physically based materials, no outline, soft sun and sky light, true cast shadows.
  today - the game's sprites as they are, for the side-by-side.

Run headless from the repository root:
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P tools/blender/samples.py -- [rich|real] [house field people scene]
Output: docs/art/s29-samples/<style>_<subject>.png (not the game's Resources).
"""
import importlib.util
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("render", os.path.join(HERE, "render.py"))
R = importlib.util.module_from_spec(_spec)
sys.modules["render"] = R
_spec.loader.exec_module(R)

OUT = os.environ.get("FTC_SAMPLES_OUT") or os.path.join(HERE, "..", "..", "docs", "art", "s29-samples")
R.OUT_DIR = OUT
srgb = R.srgb
STYLE = "rich"
_toon = R.toon   # the game's toon material, kept: the rich style builds on it


# ---------------------------------------------------------------- surfaces: what each material is made of

# substring of the material's name -> (pattern, scale in 1/world units, strength)
PATTERNS = [
    ("skin", ("skin", 300, 0.10)), ("hair", ("hair", 600, 0.35)),
    ("wheat", ("wheat", 90, 0.10)), ("famlime", ("plaster", 90, 0.18)), ("yardwall", ("plaster", 90, 0.18)), ("lime", ("plaster", 90, 0.18)),
    ("dado", ("plaster", 120, 0.25)), ("roofslab", ("plaster", 70, 0.15)),
    ("brick", ("brick", 36, 0.22)), ("plinth", ("stone", 120, 0.25)), ("stone", ("stone", 120, 0.25)),
    ("tile", ("tile", 70, 0.20)), ("wallcap", ("stone", 120, 0.2)),
    ("wood", ("wood", 60, 0.25)), ("cot", ("wood", 60, 0.25)), ("bamboo", ("wood", 60, 0.2)), ("door", ("wood", 50, 0.18)),
    ("aangan", ("earth", 110, 0.22)), ("pad", ("earth", 110, 0.22)), ("sand", ("earth", 130, 0.25)),
    ("grass", ("grass", 140, 0.28)), ("tuft", ("grass", 140, 0.28)), ("soil", ("soil", 160, 0.28)), ("ridge", ("soil", 160, 0.28)),
    ("tulsi", ("leaf", 200, 0.3)), ("leaf", ("leaf", 200, 0.3)),
    ("top", ("cloth", 420, 0.10)), ("bottom", ("cloth", 420, 0.10)), ("blouse", ("cloth", 420, 0.10)), ("vest", ("cloth", 420, 0.10)),
    ("pleat", ("cloth", 420, 0.10)), ("accent", ("cloth", 420, 0.10)),
]
ROUGH = {"skin": 0.55, "hair": 0.45, "wheat": 0.5, "plaster": 0.95, "brick": 0.9, "stone": 0.9, "tile": 0.7, "wood": 0.75,
         "earth": 1.0, "grass": 0.85, "soil": 1.0, "leaf": 0.6, "cloth": 0.92, None: 0.8}


def pattern_of(name):
    n = name.lower()
    for key, spec in PATTERNS:
        if key in n:
            return spec
    return None


def _texture(nt, kind, scale):
    """A texture as two outputs: `col` (0..1, 0.5 is the base colour) and `height` (for bump). Object coordinates, so it is fixed to the model."""
    coord = nt.nodes.new("ShaderNodeTexCoord")
    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (scale, scale, scale)
    nt.links.new(coord.outputs["Object"], mapping.inputs["Vector"])
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 1.0
    noise.inputs["Detail"].default_value = 6.0
    noise.inputs["Roughness"].default_value = 0.6
    nt.links.new(mapping.outputs["Vector"], noise.inputs["Vector"])
    col = noise.outputs["Fac"]
    height = noise.outputs["Fac"]
    if kind in ("brick",):
        brick = nt.nodes.new("ShaderNodeTexBrick")
        brick.inputs["Scale"].default_value = 1.0
        brick.inputs["Mortar Size"].default_value = 0.03
        brick.inputs["Color1"].default_value = (0.62, 0.62, 0.62, 1)
        brick.inputs["Color2"].default_value = (0.42, 0.42, 0.42, 1)
        brick.inputs["Mortar Smooth"].default_value = 0.1
        nt.links.new(mapping.outputs["Vector"], brick.inputs["Vector"])
        mixer = nt.nodes.new("ShaderNodeMix")
        mixer.data_type = "FLOAT"
        mixer.inputs[0].default_value = 0.55
        nt.links.new(brick.outputs["Fac"], mixer.inputs[2])
        nt.links.new(noise.outputs["Fac"], mixer.inputs[3])
        col = mixer.outputs[0]
        height = brick.outputs["Fac"]
    elif kind in ("tile", "wood"):
        wave = nt.nodes.new("ShaderNodeTexWave")
        wave.wave_type = "BANDS"
        wave.bands_direction = "X" if kind == "tile" else "Y"
        wave.inputs["Scale"].default_value = 1.0 if kind == "tile" else 0.7
        wave.inputs["Distortion"].default_value = 2.0 if kind == "wood" else 0.5
        wave.inputs["Detail"].default_value = 3.0
        nt.links.new(mapping.outputs["Vector"], wave.inputs["Vector"])
        mixer = nt.nodes.new("ShaderNodeMix")
        mixer.data_type = "FLOAT"
        mixer.inputs[0].default_value = 0.6
        nt.links.new(noise.outputs["Fac"], mixer.inputs[2])
        nt.links.new(wave.outputs["Fac"], mixer.inputs[3])
        col = mixer.outputs[0]
        height = wave.outputs["Fac"]
    return col, height


def soften(nt):
    """Painted light instead of hard bands: the ramp blends between its steps, and a warm rim of light catches the edges turned away."""
    ramp = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "EASE"
    emit = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeEmission")
    src = emit.inputs[0].links[0].from_socket
    weight = nt.nodes.new("ShaderNodeLayerWeight")
    weight.inputs["Blend"].default_value = 0.35
    rim = nt.nodes.new("ShaderNodeMapRange")
    rim.inputs["From Min"].default_value, rim.inputs["From Max"].default_value = 0.55, 1.0
    rim.inputs["To Min"].default_value, rim.inputs["To Max"].default_value = 0.0, 0.22
    nt.links.new(weight.outputs["Facing"], rim.inputs["Value"])
    add = nt.nodes.new("ShaderNodeMix")
    add.data_type = "RGBA"
    add.blend_type = "SCREEN"
    nt.links.new(rim.outputs["Result"], add.inputs[0])
    nt.links.new(src, add.inputs[6])
    add.inputs[7].default_value = (1.0, 0.86, 0.62, 1)
    nt.links.new(add.outputs[2], emit.inputs[0])


def rich_material(name, hexv, soft=False):
    """The game's toon material with the surface's pattern painted into its base colour."""
    key = ("rich", name, hexv, soft)
    if key in R._materials:
        return R._materials[key]
    m = _toon(name, hexv, soft).copy()   # the game's toon material stays as it is for "today"
    nt = m.node_tree
    soften(nt)
    R._materials[key] = m
    spec = pattern_of(name)
    if spec is None:
        return m
    kind, scale, strength = spec
    base_mix = next(n for n in nt.nodes if n.bl_idname == "ShaderNodeMix" and n.blend_type == "MULTIPLY" and n.inputs[7].default_value[0] > 0 and not n.inputs[7].is_linked
                    and abs(n.inputs[7].default_value[0] - srgb(hexv)[0]) < 1e-5)
    col, _ = _texture(nt, kind, scale)
    # base * (1 + strength * (2 * tex - 1)), clamped
    ramp = nt.nodes.new("ShaderNodeMapRange")
    ramp.inputs["From Min"].default_value, ramp.inputs["From Max"].default_value = 0.3, 0.7
    ramp.inputs["To Min"].default_value, ramp.inputs["To Max"].default_value = 1.0 - strength * 1.4, 1.0 + strength * 0.6
    nt.links.new(col, ramp.inputs["Value"])
    tint = nt.nodes.new("ShaderNodeMix")
    tint.data_type = "RGBA"
    tint.blend_type = "MULTIPLY"
    tint.inputs[0].default_value = 1.0
    tint.inputs[6].default_value = srgb(hexv)
    grey = nt.nodes.new("ShaderNodeCombineColor")
    for ch in ("Red", "Green", "Blue"):
        nt.links.new(ramp.outputs["Result"], grey.inputs[ch])
    nt.links.new(grey.outputs["Color"], tint.inputs[7])
    nt.links.new(tint.outputs[2], base_mix.inputs[7])
    R._materials[key] = m
    return m


def real_material(name, hexv, soft=False):
    """A physically based material: the same colour, a pattern for its detail and bump, a roughness for what it is made of."""
    key = ("real", name, hexv, soft)
    if key in R._materials:
        return R._materials[key]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    spec = pattern_of(name)
    kind = spec[0] if spec else None
    base = srgb(hexv)
    bsdf.inputs["Base Color"].default_value = base
    bsdf.inputs["Roughness"].default_value = ROUGH.get(kind, 0.8)
    if kind == "skin":
        bsdf.inputs["Subsurface Weight"].default_value = 0.25
        bsdf.inputs["Subsurface Radius"].default_value = (1.0, 0.35, 0.2)
        bsdf.inputs["Subsurface Scale"].default_value = 0.004
    if kind in ("wheat", "leaf"):
        bsdf.inputs["Subsurface Weight"].default_value = 0.3
        bsdf.inputs["Subsurface Scale"].default_value = 0.002
    if spec:
        _, scale, strength = spec
        col, height = _texture(nt, kind, scale)
        ramp = nt.nodes.new("ShaderNodeMapRange")
        ramp.inputs["From Min"].default_value, ramp.inputs["From Max"].default_value = 0.3, 0.7
        ramp.inputs["To Min"].default_value, ramp.inputs["To Max"].default_value = 1.0 - strength * 1.4, 1.0 + strength * 0.6
        nt.links.new(col, ramp.inputs["Value"])
        tint = nt.nodes.new("ShaderNodeMix")
        tint.data_type = "RGBA"
        tint.blend_type = "MULTIPLY"
        tint.inputs[0].default_value = 1.0
        tint.inputs[6].default_value = base
        grey = nt.nodes.new("ShaderNodeCombineColor")
        for ch in ("Red", "Green", "Blue"):
            nt.links.new(ramp.outputs["Result"], grey.inputs[ch])
        nt.links.new(grey.outputs["Color"], tint.inputs[7])
        nt.links.new(tint.outputs[2], bsdf.inputs["Base Color"])
        bump = nt.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = 0.35 if kind not in ("skin", "cloth", "hair") else 0.08
        bump.inputs["Distance"].default_value = 0.004
        nt.links.new(height, bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    R._materials[key] = m
    return m


# ---------------------------------------------------------------- the scene for each style

def start(style):
    """A clean scene with the game's camera; the style sets the engine, the light and the material factory."""
    global STYLE
    STYLE = style
    sc, cam = R.reset()
    R._materials.clear()
    if style == "real":
        R.toon = real_material
        sc.render.engine = "CYCLES"
        sc.cycles.device = "CPU"
        sc.cycles.samples = 96
        sc.cycles.use_denoising = True
        sc.cycles.film_exposure = 1.0
        sc.view_settings.view_transform = "AgX"
        sc.view_settings.look = "AgX - Medium High Contrast"
        sc.view_settings.exposure = 0.15
        for o in bpy.data.objects:
            if o.type == "LIGHT":
                o.data.energy = 5.2
                o.data.color = (1.0, 0.92, 0.80)
                o.data.angle = math.radians(2.0)   # a soft sun: shadows with a penumbra
        w = sc.world
        bg = w.node_tree.nodes["Background"]
        bg.inputs[0].default_value = (0.55, 0.70, 0.92, 1)   # the sky's blue light fills the shade
        bg.inputs[1].default_value = 0.85
    elif style == "today":
        R.toon = _toon
    else:
        R.toon = rich_material
        try:
            sc.eevee.taa_render_samples = 64
            sc.eevee.use_gtao = True
        except AttributeError:
            pass
    return sc, cam


def finish(objs):
    """The style's last touches on a built model: realistic loses the outline; the rich outline is thinner and warmer."""
    for o in objs:
        for mod in list(o.modifiers):
            if mod.type == "SOLIDIFY" and STYLE != "today":
                if STYLE == "real":
                    o.modifiers.remove(mod)
                else:
                    mod.thickness *= 0.62
    return objs


def catcher(objs, pad=0.22):
    """A ground that shows only the shadows: realistic sprites carry their own soft cast shadow."""
    xs, ys = [], []
    for o in objs:
        for c in o.bound_box:
            v = o.matrix_world @ Vector(c)
            xs.append(v.x)
            ys.append(v.y)
    x0, x1, y0, y1 = min(xs) - pad, max(xs) + pad, min(ys) - pad, max(ys) + pad
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector((x1 - x0, y1 - y0, 0.001)), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(((x0 + x1) / 2, (y0 + y1) / 2, -0.0005)), verts=bm.verts)
    me = bpy.data.meshes.new("catcher")
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new("catcher", me)
    bpy.context.scene.collection.objects.link(o)
    o.is_shadow_catcher = True
    o.visible_glossy = False      # it only darkens: no light bounced off it into the model
    o.visible_diffuse = False
    return o


def clean_shadow(path):
    """The sky's faint occlusion on the catcher would show its square edge: shadow fainter than 6% is dropped."""
    import numpy as np
    img = bpy.data.images.load(path)
    px = np.array(img.pixels[:], dtype=np.float32).reshape(-1, 4)
    faint = (px[:, 3] < 0.06) & (px[:, :3].max(axis=1) < 0.03)
    px[faint, 3] = 0.0
    img.pixels = px.ravel().tolist()
    img.save()
    bpy.data.images.remove(img)
    crop(path)


def crop(path):
    """Cuts the empty margin a wide shadow catcher leaves (samples only: the game's sprites keep their pivots)."""
    import numpy as np
    img = bpy.data.images.load(path)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    ys, xs = np.nonzero(px[:, :, 3] > 0)
    m = R.MARGIN_PX
    x0, x1, y0, y1 = max(0, xs.min() - m), min(w, xs.max() + 1 + m), max(0, ys.min() - m), min(h, ys.max() + 1 + m)
    out = bpy.data.images.new("crop", x1 - x0, y1 - y0, alpha=True)
    out.pixels = px[y0:y1, x0:x1].ravel().tolist()
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()


GROUNDLESS = ("house", "hand_m", "hand_f")   # drawn on the map's ground: their shadow comes from a catcher


def render(name, objs):
    sc = bpy.context.scene
    cam = sc.camera
    objs = finish(objs)
    if STYLE == "real" and name in GROUNDLESS:
        objs = objs + [catcher(objs)]
    info = R.frame_and_render(sc, cam, "%s_%s" % (STYLE, name), objs)
    if STYLE == "real" and name in GROUNDLESS:
        clean_shadow(os.path.join(OUT, "%s_%s.png" % (STYLE, name)))
    print("rendered", STYLE, name, info)


# ---------------------------------------------------------------- a fast batch (R.Batch slows as its mesh grows: 18 ms a sphere in a field)

class FastBatch:
    """R.Batch's interface; each primitive is made once as a template and copied by matrix, the mesh built in one go at the end."""

    _templates = {}

    def __init__(self, name):
        self.name = name
        self.parts = {}   # material -> (verts list, faces list)

    @classmethod
    def _template(cls, key, make):
        if key not in cls._templates:
            bm = bmesh.new()
            make(bm)
            cls._templates[key] = ([tuple(v.co) for v in bm.verts], [tuple(v.index for v in f.verts) for f in bm.faces])
            bm.free()
        return cls._templates[key]

    def _add(self, mat, tpl, m):
        vs, fs = self.parts.setdefault(mat, ([], []))
        base = len(vs)
        verts, faces = tpl
        vs.extend((m @ Vector(v))[:] for v in verts)
        fs.extend(tuple(i + base for i in f) for f in faces)

    def cone(self, mat, r1, r2, depth, m, segs=5):
        tpl = self._template(("cone", r1, r2, depth, segs), lambda bm: bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=depth))
        self._add(mat, tpl, m @ Matrix.Translation((0, 0, depth / 2)))

    def sphere(self, mat, r, m, scale=(1, 1, 1), segs=8, rings=5):
        tpl = self._template(("sphere", r, segs, rings), lambda bm: bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=r))
        self._add(mat, tpl, m @ Matrix.Diagonal((scale[0], scale[1], scale[2], 1.0)))

    def box(self, mat, size, m):
        tpl = self._template(("box",), lambda bm: bmesh.ops.create_cube(bm, size=1.0))
        self._add(mat, tpl, m @ Matrix.Diagonal((size[0], size[1], size[2], 1.0)))

    def objects(self, outline_for=(), smooth=False, thickness=R.OUTLINE):
        objs = []
        for i, (mat, (vs, fs)) in enumerate(self.parts.items()):
            me = bpy.data.meshes.new("%s%d" % (self.name, i))
            me.from_pydata(vs, [], fs)
            bm = bmesh.new()
            bm.from_mesh(me)
            bpy.data.meshes.remove(me)
            objs.append(R.obj_from_bm("%s%d" % (self.name, i), bm, mat, smooth=smooth, outline=mat in outline_for, thickness=thickness))
        self.parts = {}
        return objs


R.Batch = FastBatch   # the game's models built here (grass tufts, crop canopy) use it too; render.py itself is unchanged


# ---------------------------------------------------------------- more detail: the same house, closer to a real one

def house_detail():
    """Today's family house (R.family_house_parts) plus what a real one has: bare brick where the lime has fallen, panelled doors with hinges,
    a barred window with open shutters, tile ribs and rafters on the verandah, coping on the parapet, a roof tank on a stand, a marigold
    garland, a tulsi on its chabutara, a charpai with its rope and quilt, dung cakes on the wall, gate leaves and a pebbled yard."""
    toon = R.toon
    box, cyl, sph = R.box, R.cylinder, R.sphere
    rnd = random.Random(271)
    o = R.family_house_parts("done")
    w, d, cy = 0.31, 0.18, 0.06
    fy = cy - d / 2                                     # the front wall's face
    brick, wood, dark = toon("brick", 0xB5653E), toon("wood", R.WOOD), toon("wood_dark", R.WOOD_DARK)
    iron = toon("ironBar", 0x3A3A3E)
    # Bare brick where the lime has fallen, low on the walls and at the corners.
    for i, (x, z, sx, sz) in enumerate(((-0.13, 0.062, 0.05, 0.035), (0.14, 0.07, 0.04, 0.045), (-0.02, 0.058, 0.03, 0.02), (0.05, 0.17, 0.035, 0.022))):
        o.append(box("patch%d" % i, (sx, 0.0025, sz), (x, fy - 0.0012, z), brick, outline=False))
    o.append(box("patchSide", (0.0025, 0.05, 0.04), (w / 2 + 0.0012, cy + 0.02, 0.07), brick, outline=False))
    # The two doors: boards, a cross batten, an iron hinge strap and a brass handle; a lintel over each.
    for x in (-0.075, 0.075):
        for k in (-2, -1, 0, 1, 2):
            o.append(box("board", (0.0022, 0.003, 0.098), (x + k * 0.0105, fy - 0.0105, 0.085), dark, outline=False))
        o.append(box("batten", (0.052, 0.004, 0.008), (x, fy - 0.0115, 0.06), wood, outline=False))
        o.append(box("strap", (0.03, 0.003, 0.004), (x - 0.01, fy - 0.0125, 0.11), iron, outline=False))
        o.append(sph("handle", 0.0028, (x + 0.02 * (1 if x < 0 else -1), fy - 0.013, 0.082), toon("brass", 0xD9A93C), segs=8, rings=5, outline=False))
        o.append(box("lintel", (0.07, 0.012, 0.01), (x, fy - 0.006, 0.142), wood))
    # The window: frame, four iron bars, two shutters swung open, a sill.
    wz = 0.12
    o.append(box("winFrame", (0.056, 0.006, 0.052), (0.0, fy - 0.006, wz), wood))
    for k in (-1.5, -0.5, 0.5, 1.5):
        o.append(cyl("bar", 0.0016, 0.0016, 0.046, (k * 0.0115, fy - 0.010, wz), iron, segs=5, outline=False))
    for sx in (-1, 1):
        o.append(box("shutter", (0.004, 0.026, 0.05), (sx * 0.0305, fy - 0.017, wz), toon("famDoor", 0x2F6FA8), rot_z=math.radians(sx * 32)))
    o.append(box("sill", (0.07, 0.016, 0.008), (0.0, fy - 0.008, wz - 0.03), toon("plinth", R.PLINTH)))
    # Marigold garland across both doors.
    b = R.Batch("garland")
    gold, orange = toon("marigoldA", 0xF2A91E), toon("marigoldB", 0xE5701A)
    for i in range(23):
        t = i / 22
        x = -0.115 + 0.23 * t
        z = 0.152 - 0.016 * math.sin(math.pi * t)
        b.sphere(gold if i % 2 else orange, 0.0048, Matrix.Translation((x, fy - 0.018, z)), scale=(1, 0.8, 1), segs=7, rings=4)
    o += b.objects()
    # Verandah roof: tile ribs along the slope, a ridge cap, rafters underneath.
    vy = fy - 0.045
    tilt = Matrix.Rotation(math.radians(-14), 3, "X")
    for k in range(15):
        x = -w / 2 - 0.015 + 0.0225 * k + 0.0112
        o.append(cyl("rib", 0.0048, 0.0048, 0.103, (x, vy + 0.004, 0.1795), toon("tile", R.TILE), segs=6,
                     rot=tilt @ Matrix.Rotation(math.radians(90), 3, "X"), outline=False))
    for k in range(6):
        x = -w / 2 + 0.01 + k * 0.059
        o.append(box("rafter", (0.008, 0.10, 0.008), (x, vy + 0.005, 0.158), wood, rot=tilt, outline=False))
    o.append(box("eaveBoard", (w + 0.03, 0.006, 0.014), (0, vy - 0.0415, 0.146), dark))
    # Parapet coping: a course of bricks on edge round the roof, and a rain spout.
    cap = toon("coping", 0xCFC3AE)
    cx = [(-w / 2 - 0.006 + 0.0175 * k) for k in range(19)]
    for x in cx:
        o.append(box("copeF", (0.0165, 0.026, 0.008), (x, cy - d / 2 - 0.008, 0.2275), cap, outline=False))
        o.append(box("copeB", (0.0165, 0.026, 0.008), (x, cy + d / 2 + 0.008, 0.2275), cap, outline=False))
    o.append(cyl("spout", 0.005, 0.005, 0.05, (w / 2 + 0.012, cy - 0.02, 0.18), iron, segs=8, rot=Matrix.Rotation(math.radians(90), 3, "X")))
    # Roof tank on a stand: ribbed black tank, lid, an inlet pipe.
    tank = toon("tank", 0x2B2D30)
    o.append(cyl("tankRib1", 0.0315, 0.0315, 0.006, (-0.11, cy + 0.03, 0.2475), tank, segs=18))
    o.append(cyl("tankRib2", 0.0315, 0.0315, 0.006, (-0.11, cy + 0.03, 0.2625), tank, segs=18))
    o.append(cyl("lid", 0.012, 0.012, 0.006, (-0.11, cy + 0.03, 0.272), toon("tankLid", 0x4A5560), segs=12))
    o.append(cyl("tankPipe", 0.0035, 0.0035, 0.05, (-0.11, cy + 0.075, 0.24), iron, segs=6))
    for sx in (-1, 1):
        for sy in (-1, 1):
            o.append(box("stand", (0.006, 0.006, 0.02), (-0.11 + sx * 0.02, cy + 0.03 + sy * 0.02, 0.232), wood, outline=False))
    # Dung cakes drying on the side wall.
    for r in range(3):
        for c in range(4):
            o.append(cyl("upla", 0.0095, 0.0095, 0.004, (w / 2 + 0.0045, cy - 0.04 + c * 0.021 + (r % 2) * 0.01, 0.075 + r * 0.02), toon("upla", 0x8A6E4A), segs=10,
                         rot=Matrix.Rotation(math.radians(90), 3, "Y"), outline=False))
    # The tulsi on a chabutara with a niche, many small leaves.
    lime = toon("famLime", 0xF3E9D6)
    o.append(box("chabutara", (0.04, 0.04, 0.05), (-0.10, -0.13, 0.025), lime, bevel=0.004))
    o.append(box("niche", (0.012, 0.004, 0.014), (-0.10, -0.152, 0.034), toon("niche", 0x6A4026), outline=False))
    o.append(box("chabutaraCap", (0.046, 0.046, 0.008), (-0.10, -0.13, 0.054), toon("famDado", 0xC9794A), outline=False))
    b = R.Batch("tulsiLeaves")
    greens = [toon("tulsiA", 0x3F8A3A), toon("tulsiB", 0x5FA34A), toon("tulsiC", 0x2F6E30)]
    for i in range(44):
        a = rnd.uniform(0, math.tau)
        r = rnd.uniform(0.0, 0.019)
        R.leaf(b, greens[i % 3], -0.10 + math.cos(a) * r * 0.5, -0.13 + math.sin(a) * r * 0.5, 0.062 + rnd.uniform(0, 0.04), 0.014, 0.008, a, rnd.uniform(0.3, 1.0))
    o += b.objects()
    # A charpai in the courtyard: legs, frame, crossing ropes, a folded quilt.
    cx0, cy0 = 0.10, -0.115
    for sx in (-1, 1):
        for sy in (-1, 1):
            o.append(cyl("cotLeg", 0.004, 0.0035, 0.034, (cx0 + sx * 0.06, cy0 + sy * 0.026, 0.017), wood, segs=6, outline=False))
    o.append(box("cotFrame", (0.13, 0.06, 0.006), (cx0, cy0, 0.036), wood))
    rope = toon("rope", 0xE3CFA0)
    for k in range(11):
        o.append(box("cotRopeA", (0.0016, 0.056, 0.0016), (cx0 - 0.058 + k * 0.0116, cy0, 0.0405), rope, outline=False))
    for k in range(5):
        o.append(box("cotRopeB", (0.126, 0.0016, 0.0016), (cx0, cy0 - 0.022 + k * 0.011, 0.0422), rope, outline=False))
    o.append(box("quilt", (0.05, 0.04, 0.012), (cx0 + 0.03, cy0, 0.048), toon("quilt", 0xB8403A), bevel=0.004))
    o.append(box("quiltStripe", (0.05, 0.008, 0.0125), (cx0 + 0.03, cy0, 0.0485), toon("quiltStripe", 0xF2C94C), outline=False))
    # Gate leaves, finials on the posts.
    for sx in (-1, 1):
        o.append(box("gateLeaf", (0.034, 0.006, 0.055), (sx * 0.021, -0.17, 0.0425), wood, rot_z=math.radians(sx * 24)))
        o.append(sph("finial", 0.007, (sx * 0.045, -0.17, 0.082), lime, segs=10, rings=6))
    # A pebbled yard.
    b = R.Batch("pebbles")
    peb = [toon("pebbleA", 0xB9AA8E), toon("pebbleB", 0xA39478), toon("pebbleC", 0xCDBFA3)]
    for i in range(70):
        b.sphere(peb[i % 3], 0.0045, Matrix.Translation((rnd.uniform(-0.14, 0.14), rnd.uniform(-0.165, -0.04), 0.004)), scale=(1.3, 1, 0.5), segs=6, rings=4)
    o += b.objects()
    return o


def subject_house_detail():
    return R.group(house_detail(), scale=R.BUILDING_SCALE * R.HOUSE_SCALE)


def wheat_clump(b, rnd, x, y, mats):
    """A ripe clump: twelve stalks of slightly different height and gold, each ear built of paired spikelets with awns, dry leaves at the foot."""
    stalks, ears, leaves, awn = mats
    for k in range(12):
        a = k * math.tau / 12 + rnd.uniform(-0.4, 0.4)
        h = rnd.uniform(0.105, 0.145)
        lean = rnd.uniform(0.06, 0.22) + 0.05           # the field leans a little with the wind
        m = R.at(x + math.cos(a) * 0.008, y + math.sin(a) * 0.008, 0.02, tilt=lean, tilt_dir=a * 0.3 + 0.8)
        b.cone(rnd.choice(stalks), 0.0030, 0.0020, h, m, segs=4)
        ear = rnd.choice(ears)
        nod = m @ Matrix.Translation((0, 0, h)) @ Matrix.Rotation(rnd.uniform(0.1, 0.35), 4, "X")
        for j in range(7):
            z = 0.004 + j * 0.0047
            side = 1 if j % 2 else -1
            b.sphere(ear, 0.0060, nod @ Matrix.Translation((side * 0.0030, 0, z)), scale=(0.8, 0.7, 1.45), segs=6, rings=4)
            b.cone(awn, 0.00055, 0.0, 0.026, nod @ Matrix.Translation((side * 0.003, 0, z + 0.003)) @ Matrix.Rotation(side * 0.32, 4, "Y"), segs=3)
        b.sphere(ear, 0.0040, nod @ Matrix.Translation((0, 0, 0.0375)), scale=(0.8, 0.7, 1.4), segs=6, rings=4)
    for k in range(5):
        a = rnd.uniform(0, math.tau)
        R.leaf(b, rnd.choice(leaves), x, y, 0.03 + rnd.uniform(0, 0.04), rnd.uniform(0.05, 0.07), 0.007, a, rnd.uniform(-0.2, 0.35))


def field_detail():
    """A ripe wheat acre: the tilled soil block and its ridges, then 7 rows of 17 clumps over a canopy of leaves, a few green late stalks and weeds at the edge."""
    toon = R.toon
    rnd = random.Random(2711)
    o = R.model_ground("soil")
    b = R.Batch("wheat")
    # The lower canopy: a band of dry leaves along each row, so the field reads as a carpet of gold.
    R.crop_mat(b, "ready", None, toon("wheatCanopy", 0xD2A23C), width=0.07)
    mats = ([toon("wheatStalkA", 0xD9B25A), toon("wheatStalkB", 0xE6C266), toon("wheatStalkC", 0xC99A3E)],
            [toon("wheatEarA", 0xE0A12E), toon("wheatEarB", 0xF0BE48), toon("wheatEarC", 0xCC8A22)],
            [toon("wheatLeafA", 0xBFA35C), toon("wheatLeafB", 0xA88E48)],
            toon("wheatAwn", 0xE2B455))
    for x in R.RIDGE_X:
        for j in range(17):
            y = -0.43 + j * (0.86 / 16) + rnd.uniform(-0.01, 0.01)
            wheat_clump(b, rnd, x + rnd.uniform(-0.012, 0.012), y, mats)
    weed = [toon("weedA", 0x6E9A3A), toon("weedB", 0x8AAE4A)]
    for _ in range(14):     # bathua and grass where the drill missed, along the edge
        x, y = rnd.choice((-0.46, 0.46)) if rnd.random() < 0.5 else rnd.uniform(-0.45, 0.45), 0
        y = rnd.uniform(-0.45, 0.45) if abs(x) > 0.4 else rnd.choice((-0.46, 0.46))
        for k in range(6):
            a = k * math.tau / 6 + rnd.uniform(-0.3, 0.3)
            b.cone(rnd.choice(weed), 0.006, 0.0, rnd.uniform(0.03, 0.05), R.at(x, y, 0.004, tilt=0.6, tilt_dir=a), segs=3)
    return o + b.objects()


# ---------------------------------------------------------------- people

FRONT = math.radians(30)          # the game's front-facing turn (R.model_person)


def _place(objs, scale):
    """Turns and scales parts built facing -y exactly as R.model_person's group does."""
    return R.group(objs, rot_z=FRONT, scale=scale, outline_factor=0.7)


def person_rich(kind):
    """Today's figure (big head, door-high) with the detail a closer look wants: a wrapped pagdi, borders, jewellery, buttons, hair strands."""
    toon = R.toon
    p = R.PEOPLE[kind]
    o = R.model_person(kind, "front")
    x = []
    m = lambda n, h: toon(kind + "_" + n, h, soft=True)
    hz = 0.137
    gold = m("gold", 0xE8B83A)
    hair = m("hair", p.get("hair", R.HAIR))
    if p["sex"] == "f":
        for sx in (-1, 1):
            x.append(R.sphere("earring", 0.0028, (sx * 0.031, -0.002, hz - 0.012), gold, segs=8, rings=5, outline=False))
            x.append(R.cylinder("anklet", 0.0090, 0.0090, 0.0018, (sx * 0.010, -0.002, 0.008), m("silver", 0xD8D8D0), segs=10, outline=False))
        x.append(R.sphere("nosepin", 0.0015, (0.006, -0.0318, hz - 0.004), gold, segs=6, rings=4, outline=False))
        for k in range(5):   # more pleats, and the border along the hem's front
            x.append(R.box("pleatX", (0.003, 0.004, 0.056), (-0.010 + k * 0.005, -0.0305 + abs(k - 2) * 0.0008, 0.032), m("pleatDeep", p["top"] - 0x181818), outline=False))
        x.append(R.box("palluBorder", (0.0035, 0.0068, 0.058), (-0.002, -0.0205, 0.082), m("accent", p["accent"]), outline=False,
                       rot=Matrix.Rotation(math.radians(-38), 3, "Y")))
        for k in range(6):   # strands combed back to the bun
            a = math.radians(-60 + k * 24)
            x.append(R.sphere("strand", 0.0026, (math.sin(a) * 0.026, 0.004 + math.cos(a) * 0.004, hz + 0.022), hair, scale=(0.6, 4.0, 0.6), segs=6, rings=4, outline=False))
    else:
        accent = m("accent", p["accent"])
        for k in range(3):   # the pagdi's turns, a shade darker between them
            x.append(R.cylinder("pagdiTurn", 0.0342 - k * 0.0012, 0.0335 - k * 0.0012, 0.0025, (0, 0.003, hz + 0.012 + k * 0.007), m("pagdiDeep", 0xB85A0E), segs=18,
                                rot=Matrix.Rotation(math.radians(6 - k * 6), 3, "X"), outline=False))
        x.append(R.cylinder("dhotiBorder", 0.0212, 0.0212, 0.0028, (0, 0, 0.0435), m("border", 0x2E6FBA), segs=16, outline=False))
        for k in range(3):
            x.append(R.sphere("button", 0.0014, (0.0, -0.0222, 0.072 + k * 0.009), m("button", 0xEDE6D2), segs=6, rings=4, outline=False))
        x.append(R.box("gamchha", (0.008, 0.005, 0.03), (0.016, -0.0195, 0.089), m("gamchha", 0xC8423A), rot_z=math.radians(-8)))
    return o + _place(x, R.PERSON_SCALE)


def limb(name, a, b, r1, r2, mat, segs=12):
    """A tapered limb from a to b with a round joint at each end."""
    a, b = Vector(a), Vector(b)
    d = b - a
    rot = d.to_track_quat("Z", "Y").to_matrix()
    return [R.cylinder(name, r1, r2, d.length, tuple((a + b) / 2), mat, segs=segs, rot=rot, smooth=True, outline=False),
            R.sphere(name + "J", r2, tuple(b), mat, segs=segs, rings=8, outline=False)]


OUTLINED = ("head", "jaw", "hair", "bun", "chest", "hips", "waist", "saree", "dhoti", "thigh", "shin", "upper", "fore", "sleeve", "hand", "foot",
            "pagdi", "vest", "pallu", "neck")


def outline(o, factor=0.7):
    """The rich style's warm outline on a part built without one."""
    o.data.materials.append(R.outline_material())
    mod = o.modifiers.new("outline", "SOLIDIFY")
    mod.thickness = R.OUTLINE * factor
    mod.offset = 1.0
    mod.use_flip_normals = True
    mod.use_rim = False
    mod.use_even_offset = True
    mod.material_offset = len(o.data.materials) - 1
    return o


def person_real(kind, heads=7.5, chunk=1.0):
    """A villager at the same height as today's figure. heads 7.5 is true proportion (the face is small: what realism costs); the rich style
    uses about 4 heads, a stylised figure with a readable face, its body pressed down to fit and a little stockier."""
    toon = R.toon
    p = R.PEOPLE[kind]
    f = p["sex"] == "f"
    m = lambda n, h: toon(kind + "_r_" + n, h)
    skin, top, bottom, accent = m("skin", p["skin"]), m("top", p["top"]), m("bottom", p["bottom"]), m("accent", p["accent"])
    hair, dark = m("hair", p.get("hair", R.HAIR)), m("dark", 0x1E1410)
    H = 0.172                                   # today's figure's height before scaling
    hd = H / heads
    hz = H - hd * 0.52                          # the head's centre
    squash = (H - hd) / (H - H / 7.5)           # the body below the head, pressed to fit a bigger head
    o = []
    # Legs and feet (bare below a dhoti; under a saree only the feet).
    for s in (-1, 1):
        o += limb("thigh", (s * 0.0085, 0, 0.088), (s * 0.0090, -0.001, 0.048), 0.0072, 0.0058, skin)
        o += limb("shin", (s * 0.0090, -0.001, 0.048), (s * 0.0092, 0.0015, 0.008), 0.0052, 0.0038, skin)
        o.append(R.sphere("foot", 0.0042, (s * 0.0094, -0.0035, 0.004), skin, scale=(0.9, 2.0, 0.6), segs=10, rings=6, outline=False))
        o.append(R.sphere("chappal", 0.0050, (s * 0.0094, -0.0035, 0.0012), m("chappal", 0x5A3A22), scale=(1.0, 2.1, 0.25), segs=10, rings=5, outline=False))
    # Torso: waist to shoulders, flatter front to back.
    body = m("blouse", p["blouse"]) if f else top
    o.append(R.cylinder("hips", 0.0150, 0.0135, 0.022, (0, 0, 0.090), skin if f else bottom, segs=16, smooth=True, outline=False))
    o.append(R.cylinder("waist", 0.0128, 0.0160, 0.028, (0, 0, 0.115), skin if f else body, segs=16, smooth=True, outline=False))
    o[-1].scale = (1, 0.72, 1)
    o.append(R.sphere("chest", 0.0175, (0, 0, 0.133), body, scale=(1.18, 0.70, 0.62), segs=16, rings=10, outline=False))
    if f:
        o.append(R.cylinder("blouseBand", 0.0150, 0.0164, 0.010, (0, 0, 0.129), body, segs=16, smooth=True, outline=False))
        o[-1].scale = (1, 0.74, 1)
    o.append(R.cylinder("neck", 0.0042, 0.0046, 0.012, (0, 0, hz - hd * 0.55), skin, segs=10, smooth=True, outline=False))
    # Arms: a short sleeve, bare forearm, a hand.
    for s in (-1, 1):
        sh, el, wr = (s * 0.0215, 0, 0.138), (s * 0.0255, 0.001, 0.106), (s * 0.0262, -0.003, 0.078)
        sleeve = body
        o += limb("upper", sh, el, 0.0052, 0.0042, skin)
        o += limb("sleeve", sh, (s * 0.0232, 0.0005, 0.124), 0.0060, 0.0056, sleeve)
        o += limb("fore", el, wr, 0.0040, 0.0032, skin)
        o.append(R.sphere("hand", 0.0038, (s * 0.0266, -0.0035, 0.0715), skin, scale=(0.75, 0.9, 1.5), segs=10, rings=6, outline=False))
        if f:
            for k in range(3):
                o.append(R.cylinder("bangle", 0.0040, 0.0040, 0.0012, (s * 0.0261, -0.0028, 0.081 + k * 0.0018), m("bangle%d" % k, (0xD8283A, 0xE8B83A, 0x2E8B57)[k]), segs=10, outline=False))
    body_parts = list(o)
    o = []
    # The head: skull, jaw, nose, ears, eyes and brows, lips.
    o.append(R.sphere("head", hd * 0.42, (0, 0.001, hz), skin, scale=(0.92, 1.0, 1.14), segs=20, rings=14, outline=False))
    o.append(R.sphere("jaw", hd * 0.33, (0, -0.0025, hz - hd * 0.18), skin, scale=(0.95, 0.95, 0.9), segs=16, rings=10, outline=False))
    o.append(R.sphere("nose", hd * 0.09, (0, -hd * 0.43, hz - hd * 0.04), skin, scale=(0.8, 1.0, 1.4), segs=8, rings=6, outline=False))
    for s in (-1, 1):
        o.append(R.sphere("ear", hd * 0.10, (s * hd * 0.38, 0.002, hz), skin, scale=(0.45, 0.9, 1.4), segs=8, rings=6, outline=False))
        if heads < 5:   # a stylised face: a glint in each eye
            o.append(R.sphere("irisGlint", hd * 0.022, (s * hd * 0.15 + hd * 0.02, -hd * 0.425, hz + hd * 0.085), m("white", 0xFFFFFF), segs=6, rings=4, outline=False))
        o.append(R.sphere("iris", hd * (0.07 if heads < 5 else 0.045), (s * hd * 0.155, -hd * 0.40, hz + hd * 0.06), dark, segs=8, rings=6, outline=False))
        o.append(R.box("brow", (hd * 0.2, hd * 0.03, hd * 0.035), (s * hd * 0.16, -hd * 0.39, hz + hd * 0.15), hair, outline=False))
    o.append(R.sphere("lips", hd * 0.08, (0, -hd * 0.38, hz - hd * 0.22), m("lips", 0x8A3E32), scale=(1.7, 0.6, 0.6), segs=8, rings=5, outline=False))
    if f:
        o.append(R.sphere("hair", hd * 0.445, (0, 0.003, hz + hd * 0.06), hair, scale=(0.95, 1.0, 1.05), segs=20, rings=14, outline=False))
        o.append(R.sphere("bun", hd * 0.24, (0, hd * 0.46, hz - hd * 0.05), hair, segs=14, rings=10, outline=False))
        o.append(R.sphere("bindi", hd * 0.03, (0, -hd * 0.41, hz + hd * 0.2), m("bindi", 0xC0262D), segs=6, rings=4, outline=False))
        for s in (-1, 1):
            o.append(R.sphere("earring", hd * 0.05, (s * hd * 0.39, -0.0005, hz - hd * 0.2), m("gold", 0xE8B83A), segs=6, rings=4, outline=False))
        # The saree: the skirt from the waist to the ground, pleats fanned at the front, a border at the hem,
        # the pallu across the chest from the right hip to the left shoulder and down the back.
        sk = R.cylinder("saree", 0.0225, 0.0170, 0.090, (0, 0, 0.048), bottom, segs=20, smooth=True, outline=False)
        sk.scale = (1, 0.88, 1)
        o.append(sk)
        o.append(R.cylinder("hem", 0.0226, 0.0227, 0.005, (0, 0, 0.0055), accent, segs=20, smooth=True, outline=False))
        o[-1].scale = (1, 0.88, 1)
        for k in range(7):
            a = (k - 3) * 0.11
            o.append(R.box("pleat", (0.0032, 0.0028, 0.084), (math.sin(a) * 0.016, -0.0175 - abs(k - 3) * 0.0004, 0.051), m("pleat", max(0, p["top"] - 0x141414)),
                           rot=Matrix.Rotation(a * 0.6, 3, "Y"), outline=False))
        pallu = R.box("pallu", (0.017, 0.0035, 0.060), (0.002, -0.0128, 0.115), top, rot=Matrix.Rotation(math.radians(-40), 3, "Y"), outline=False)
        o.append(pallu)
        o.append(R.box("palluEdge", (0.003, 0.0038, 0.060), (0.0075, -0.0132, 0.112), accent, rot=Matrix.Rotation(math.radians(-40), 3, "Y"), outline=False))
        o.append(R.box("palluBack", (0.018, 0.004, 0.065), (-0.012, 0.0125, 0.102), top, rot=Matrix.Rotation(math.radians(8), 3, "Y"), outline=False))
    else:
        o.append(R.sphere("hair", hd * 0.44, (0, 0.003, hz + hd * 0.05), hair, scale=(0.95, 1.0, 0.95), segs=16, rings=10, outline=False))
        o.append(R.sphere("moustache", hd * 0.09, (0, -hd * 0.39, hz - hd * 0.14), hair, scale=(2.4, 0.6, 0.6), segs=8, rings=5, outline=False))
        # The pagdi: wound turns over the crown, the tail down the back.
        for k in range(3):
            o.append(R.cylinder("pagdi", hd * 0.50 - k * 0.0006, hd * 0.49 - k * 0.0006, hd * 0.13, (0, 0.0015, hz + hd * 0.20 + k * hd * 0.10), accent if k % 2 == 0 else m("pagdiDeep", 0xC66A12),
                                segs=18, smooth=True, rot=Matrix.Rotation(math.radians(8 - k * 5), 3, "X"), outline=False))
        o.append(R.sphere("pagdiTop", hd * 0.44, (0, 0.002, hz + hd * 0.44), accent, scale=(1, 1, 0.45), segs=16, rings=8, outline=False))
        o.append(R.box("pagdiTail", (hd * 0.25, 0.002, hd * 0.6), (0.004, hd * 0.48, hz), accent, outline=False))
        # The dhoti from the waist to mid-shin, draped between the legs, a border at the hem; the vest over the shirt; a gamchha.
        for sx in (-1, 1):   # the dhoti wrapped loose round each thigh to the knee, the pleated front falling between
            o += limb("dhotiLeg", (sx * 0.0080, 0, 0.094), (sx * 0.0098, -0.001, 0.046), 0.0105, 0.0088, bottom)
        o.append(R.cylinder("dhotiWaist", 0.0158, 0.0150, 0.014, (0, 0, 0.092), bottom, segs=18, smooth=True, outline=False))
        o[-1].scale = (1, 0.80, 1)
        for sx in (-1, 1):
            o.append(R.cylinder("dhotiBorder", 0.0090, 0.0090, 0.0025, (sx * 0.0098, -0.001, 0.047), m("border", 0x2E6FBA), segs=14, smooth=True, outline=False))
        if p.get("vest"):
            v = R.cylinder("vest", 0.0142, 0.0176, 0.030, (0, 0.0002, 0.118), m("vest", p["vest"]), segs=16, smooth=True, outline=False)
            v.scale = (1, 0.78, 1)
            o.append(v)
            o.append(R.sphere("vestChest", 0.0182, (0, 0.0002, 0.133), m("vest", p["vest"]), scale=(1.16, 0.72, 0.58), segs=16, rings=10, outline=False))
            o.append(R.box("vestOpen", (0.0035, 0.002, 0.036), (0.0, -0.0128, 0.124), top, outline=False))
        o.append(R.box("gamchha", (0.006, 0.003, 0.03), (0.012, -0.0125, 0.127), m("gamchha", 0xC8423A), rot=Matrix.Rotation(math.radians(-10), 3, "Y"), outline=False))
    head_names = ("head", "jaw", "nose", "ear", "iris", "brow", "lips", "hair", "bun", "bindi", "earring", "moustache", "pagdi")
    body_parts += [q for q in o if not q.name.startswith(head_names)]
    o = [q for q in o if q.name.startswith(head_names)]
    press = Matrix.Diagonal((chunk, chunk, squash, 1.0))
    for q in body_parts:
        q.matrix_world = press @ q.matrix_world
    o += body_parts
    if STYLE == "rich":
        for q in o:
            if q.name.startswith(OUTLINED):
                outline(q)
    return _place(o, R.PERSON_SCALE)


# ---------------------------------------------------------------- the subjects (today's models; detail is added below)

def subject_house():
    return R.model_family_house("done") if STYLE == "today" else subject_house_detail()


def subject_field():
    return R.model_ground("soil") + R.model_crop("wheat", "ready") if STYLE == "today" else field_detail()


def subject_person(kind):
    if STYLE == "today":
        return R.model_person(kind, "front")
    return person_real(kind) if STYLE == "real" else person_real(kind, heads=4.2, chunk=1.22)


def moved(objs, x, y):
    for o in objs:
        o.matrix_world = Matrix.Translation((x, y, 0)) @ o.matrix_world
    return objs


def subject_scene():
    """Four acres as the game shows them: the family house on its grass acre, two acres of ripe wheat behind, the hand and his wife on the lane acre in front."""
    o = []
    o += moved(R.model_ground("grass") + R.model_grass_detail(0), 0, 0)
    o += moved(subject_house(), 0, 0)
    o += moved(subject_field(), 0, 1)
    o += moved(subject_field(), 1, 1)
    o += moved(R.model_ground("earth"), 1, 0)          # the lane acre at the front
    o += moved(subject_person("hand_m"), 0.88, -0.14)
    o += moved(subject_person("hand_f"), 1.12, 0.10)
    return o


SUBJECTS = {
    "house": subject_house,
    "field": subject_field,
    "hand_m": lambda: subject_person("hand_m"),
    "hand_f": lambda: subject_person("hand_f"),
    "scene": subject_scene,
}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    styles = [a for a in argv if a in ("today", "rich", "real")] or ["today", "rich", "real"]
    names = [a for a in argv if a not in ("today", "rich", "real")] or list(SUBJECTS)
    os.makedirs(OUT, exist_ok=True)
    for style in styles:
        for name in names:
            start(style)
            render(name, SUBJECTS[name]())


if __name__ == "__main__":
    main()
