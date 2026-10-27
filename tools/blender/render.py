"""
Farm to City sprite rig (ADR 0007, ADR 0039 §1).

Builds each low-poly model in code and renders it with one fixed 2:1 dimetric camera and light rig, toon shading and an
inverted-hull outline, to a transparent PNG at 4x the game's pixel density. Pivots (where the model's origin, its ground
point, lands in the image) go into one manifest that the game reads.

Run headless from the repository root:
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P tools/blender/render.py -- [names...]
With no names every sprite is rendered. Output: unity/FarmToCity/Assets/Resources/Art/Blender/<name>.png and sprites.json.

World scale: one map tile (an acre) is a 1 x 1 square on the ground plane (x, y), z up. The game's tile is 132 px wide,
so a unit along the camera's screen axes is 132 / sqrt(2) px at 1x.
"""
import bmesh
import bpy
import json
import math
import os
import sys
from mathutils import Matrix, Vector

SCALE = 4                                      # supersampling: the game shows sprites at 1/SCALE
PX_PER_UNIT = 132.0 / math.sqrt(2.0) * SCALE   # px per world unit in the camera plane
MARGIN_PX = 8 * SCALE
OUTLINE = 0.0065                               # world units after scaling; about 2 px at 1x
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..",
                       "unity", "FarmToCity", "Assets", "Resources", "Art", "Blender")
OUTLINE_RGB = (0x4A / 255.0, 0x33 / 255.0, 0x20 / 255.0)


def srgb(hexv):
    """Hex sRGB to linear RGBA (Blender's colour inputs are linear)."""
    def lin(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin((hexv >> 16) & 255), lin((hexv >> 8) & 255), lin(hexv & 255), 1.0)


# ---------------------------------------------------------------- scene, camera, light

def reset():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)
    sc = bpy.context.scene
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    sc.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.render.resolution_percentage = 100
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    try:
        sc.eevee.taa_render_samples = 32
    except AttributeError:
        pass
    world = bpy.data.worlds.get("World") or bpy.data.worlds.new("World")
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.0

    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("cam", cam_data)
    sc.collection.objects.link(cam)
    cam.rotation_euler = (math.radians(60), 0.0, math.radians(45))
    sc.camera = cam

    sun_data = bpy.data.lights.new("sun", "SUN")
    sun_data.energy = 3.2
    sun_data.angle = math.radians(8)
    sun = bpy.data.objects.new("sun", sun_data)
    sc.collection.objects.link(sun)
    # Light from the screen's upper left (Kenney's tiles are lit that way): the -y faces brightest, the +x faces darker.
    to_light = Vector((-0.35, -1.0, 1.25)).normalized()
    sun.rotation_euler = to_light.to_track_quat("Z", "Y").to_euler()
    return sc, cam


_materials = {}


def toon(name, hexv, soft=False):
    """Toon material: three light bands (warm shadow tint) times the base colour, emitted so no other light leaks in."""
    key = (name, hexv, soft)
    if key in _materials:
        return _materials[key]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    diffuse = nt.nodes.new("ShaderNodeBsdfDiffuse")
    diffuse.inputs["Color"].default_value = (1, 1, 1, 1)
    to_rgb = nt.nodes.new("ShaderNodeShaderToRGB")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    els = ramp.color_ramp.elements
    els[0].position = 0.0
    els[0].color = (0.64, 0.57, 0.64, 1) if soft else (0.50, 0.44, 0.52, 1)   # core shadow, cool-violet
    els[1].position = 0.16
    els[1].color = (0.84, 0.79, 0.79, 1) if soft else (0.76, 0.71, 0.72, 1)   # half shadow
    e = els.new(0.42)
    e.color = (1.0, 0.98, 0.94, 1)             # lit
    e = els.new(0.80)
    e.color = (1.12, 1.10, 1.02, 1)            # highlight
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs[0].default_value = 1.0
    mix.inputs[7].default_value = srgb(hexv)
    emit = nt.nodes.new("ShaderNodeEmission")
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(diffuse.outputs[0], to_rgb.inputs[0])
    nt.links.new(to_rgb.outputs[0], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], mix.inputs[6])
    # Soft contact darkening (under eaves, where parts meet), mapped so it never goes below 70%.
    ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
    ao.inputs["Distance"].default_value = 0.04
    ao_map = nt.nodes.new("ShaderNodeMapRange")
    ao_map.inputs["To Min"].default_value = 0.70
    ao_mul = nt.nodes.new("ShaderNodeMix")
    ao_mul.data_type = "RGBA"
    ao_mul.blend_type = "MULTIPLY"
    ao_mul.inputs[0].default_value = 1.0
    nt.links.new(ao.outputs["AO"], ao_map.inputs["Value"])
    nt.links.new(mix.outputs[2], ao_mul.inputs[6])
    nt.links.new(ao_map.outputs["Result"], ao_mul.inputs[7])
    nt.links.new(ao_mul.outputs[2], emit.inputs[0])
    nt.links.new(emit.outputs[0], out.inputs[0])
    _materials[key] = m
    return m


def outline_material():
    if "outline" in _materials:
        return _materials["outline"]
    m = bpy.data.materials.new("outline")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs[0].default_value = srgb((int(OUTLINE_RGB[0] * 255) << 16) | (int(OUTLINE_RGB[1] * 255) << 8) | int(OUTLINE_RGB[2] * 255))
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(emit.outputs[0], out.inputs[0])
    m.use_backface_culling = True
    _materials["outline"] = m
    return m


# ---------------------------------------------------------------- mesh helpers

def obj_from_bm(name, bm, mat, smooth=False, outline=True, thickness=OUTLINE):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(mat)
    if outline:
        o.data.materials.append(outline_material())
        mod = o.modifiers.new("outline", "SOLIDIFY")
        mod.thickness = thickness
        mod.offset = 1.0
        mod.use_flip_normals = True
        mod.use_rim = False
        mod.use_even_offset = True
        mod.material_offset = 1
    return o


def box(name, size, loc, mat, rot_z=0.0, bevel=0.0, rot=None, **kw):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=bm.edges[:] + bm.verts[:], offset=bevel, segments=1, affect="EDGES")
    bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(rot_z, 3, "Z") @ (rot if rot is not None else Matrix.Identity(3)), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    return obj_from_bm(name, bm, mat, **kw)


def cylinder(name, r1, r2, depth, loc, mat, segs=12, rot=None, smooth=False, cap=True, **kw):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=depth)
    if rot is not None:
        bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=rot, verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    return obj_from_bm(name, bm, mat, smooth=smooth, **kw)


def sphere(name, r, loc, mat, scale=(1, 1, 1), segs=12, rings=8, smooth=True, **kw):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=r)
    bmesh.ops.scale(bm, vec=Vector(scale), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    return obj_from_bm(name, bm, mat, smooth=smooth, **kw)


def poly_mesh(name, verts, faces, mat, **kw):
    bm = bmesh.new()
    vs = [bm.verts.new(v) for v in verts]
    for f in faces:
        bm.faces.new([vs[i] for i in f])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return obj_from_bm(name, bm, mat, **kw)


def hip_roof(name, w, d, h, ridge, z, mat, overhang=0.0, **kw):
    """A hipped roof over a w x d rectangle (centred at the origin) with a ridge of length `ridge` along x."""
    w2, d2 = w / 2 + overhang, d / 2 + overhang
    r2 = ridge / 2
    verts = [(-w2, -d2, z), (w2, -d2, z), (w2, d2, z), (-w2, d2, z), (-r2, 0, z + h), (r2, 0, z + h)]
    faces = [(0, 1, 5, 4), (1, 2, 5), (2, 3, 4, 5), (3, 0, 4), (0, 3, 2, 1)]
    return poly_mesh(name, verts, faces, mat, **kw)


def gable_roof(name, w, d, h, z, mat, overhang=0.0, **kw):
    """A gabled roof over a w x d rectangle, ridge along x."""
    w2, d2 = w / 2 + overhang, d / 2 + overhang
    verts = [(-w2, -d2, z), (w2, -d2, z), (w2, d2, z), (-w2, d2, z), (-w2, 0, z + h), (w2, 0, z + h)]
    faces = [(0, 1, 5, 4), (2, 3, 4, 5), (1, 2, 5), (3, 0, 4), (0, 3, 2, 1)]
    return poly_mesh(name, verts, faces, mat, **kw)


def group(objs, rot_z=0.0, scale=1.0, outline_factor=1.0):
    """Turns and scales a set of parts as one model about the origin; outlines stay the same width on screen."""
    m = Matrix.Diagonal((scale, scale, scale, 1.0)) @ Matrix.Rotation(rot_z, 4, "Z")
    for o in objs:
        o.matrix_world = m @ o.matrix_world
        for mod in o.modifiers:
            if mod.type == "SOLIDIFY":
                mod.thickness = OUTLINE * outline_factor / scale
    return objs


BUILDING_SCALE = 1.45   # buildings are drawn larger than life: a tile is an acre
PERSON_SCALE = 1.35     # about the height of the hut's door


# ---------------------------------------------------------------- palette (warm, sun-baked, Indian village)

MUD = 0xD9B07A        # lime-washed mud wall
MUD_DARK = 0xB98A55
PLINTH = 0xA87A50
THATCH = 0xE2B45A
THATCH_DARK = 0xC08A3A
WOOD = 0x8A5A34
WOOD_DARK = 0x6A4026
DOOR = 0x3E7C8C       # painted teal door, common in rural India
TRIM = 0xE8E2D2       # white lime trim
STONE = 0xB9AFA0
STONE_DARK = 0x8F877C
WATER = 0x3F7FA6
TILE = 0xC2603A       # terracotta
SKIN = 0xB57A55
HAIR = 0x2B1D16


# ---------------------------------------------------------------- models

def thatch_tiers(name, w, d, z, tiers, mat_a, mat_b, ridge=0.0, overhang=0.06, rise=0.06):
    """Thatch as stacked hipped tiers that shrink upward: reads as layered straw from far away."""
    o = []
    for i in range(tiers):
        t = i / tiers
        ww, dd = w * (1 - 0.55 * t), d * (1 - 0.70 * t)
        rr = max(0.0, ridge * (1 - 0.3 * t))
        o.append(hip_roof("%s%d" % (name, i), ww, dd, rise * 1.6, rr, z + i * rise, mat_a if i % 2 == 0 else mat_b,
                          overhang=overhang * (1 - 0.5 * t)))
    return o


def matka(name, loc, r=0.022):
    """A clay water pot."""
    x, y, z = loc
    return [sphere(name, r, (x, y, z + r * 0.9), toon("clay", 0xB4572E), scale=(1, 1, 0.9), segs=12, rings=8),
            cylinder(name + "neck", r * 0.45, r * 0.55, r * 0.4, (x, y, z + r * 1.85), toon("clay", 0xB4572E), segs=10, smooth=True)]


def model_hut():
    """The founder's home: a lime-washed mud house on a plinth, teal door, layered hipped thatch, pots by the door, a charpai."""
    o = []
    o.append(box("plinth", (0.50, 0.38, 0.04), (0, 0, 0.02), toon("plinth", PLINTH), bevel=0.008))
    o.append(box("walls", (0.44, 0.32, 0.19), (0, 0, 0.135), toon("mud", MUD)))
    o.append(box("skirt", (0.445, 0.325, 0.03), (0, 0, 0.055), toon("mud_dark", MUD_DARK), outline=False))
    # Door, frame and a painted threshold motif on the camera-facing long wall (-y).
    o.append(box("door", (0.08, 0.012, 0.13), (0.07, -0.163, 0.105), toon("door", DOOR)))
    o.append(box("doorframe", (0.10, 0.008, 0.145), (0.07, -0.159, 0.112), toon("trim", TRIM), outline=False))
    o.append(box("lintel", (0.12, 0.02, 0.016), (0.07, -0.167, 0.185), toon("wood", WOOD)))
    o.append(box("window", (0.07, 0.012, 0.06), (-0.11, -0.163, 0.155), toon("wood_dark", WOOD_DARK)))
    o.append(box("sill", (0.09, 0.02, 0.012), (-0.11, -0.168, 0.121), toon("trim", TRIM), outline=False))
    for i in (-1, 0, 1):
        o.append(box("bar%d" % i, (0.006, 0.014, 0.06), (-0.11 + i * 0.02, -0.165, 0.155), toon("wood", WOOD), outline=False))
    o.append(box("window2", (0.012, 0.07, 0.06), (0.223, 0.0, 0.155), toon("wood_dark", WOOD_DARK)))
    # Beam ends poking out under the eaves.
    for i in range(5):
        o.append(box("beam%d" % i, (0.014, 0.03, 0.014), (-0.18 + i * 0.09, -0.17, 0.222), toon("wood", WOOD), outline=False))
    # Thatch: a thick, darker fringe at the eave, then one continuous hipped roof, and a bound ridge.
    o.append(hip_roof("fringe", 0.44, 0.32, 0.035, 0.40, 0.215, toon("thatch_dark", THATCH_DARK), overhang=0.075))
    o.append(hip_roof("roof", 0.44, 0.32, 0.19, 0.22, 0.235, toon("thatch", THATCH), overhang=0.06))
    o.append(box("ridge", (0.24, 0.032, 0.03), (0, 0, 0.428), toon("thatch_dark", THATCH_DARK), bevel=0.01))
    for x in (-0.09, 0.0, 0.09):
        o.append(box("tie", (0.012, 0.04, 0.036), (x, 0, 0.428), toon("rope", 0xE8D9B0), outline=False))
    o.append(box("step", (0.12, 0.05, 0.02), (0.07, -0.215, 0.01), toon("plinth", PLINTH)))
    o += matka("pot1", (0.165, -0.215, 0.0))
    o += matka("pot2", (0.20, -0.20, 0.0), r=0.017)
    # A charpai (rope cot) against the -y wall, left of the window.
    cot = toon("wood", WOOD)
    for x in (-0.23, -0.10):
        for y in (-0.205, -0.25):
            o.append(box("leg", (0.012, 0.012, 0.04), (x, y, 0.02), cot, outline=False))
    o.append(box("cotframe", (0.15, 0.065, 0.012), (-0.165, -0.228, 0.045), cot))
    o.append(box("cotrope", (0.13, 0.05, 0.006), (-0.165, -0.228, 0.050), toon("rope", 0xE8D9B0), outline=False))
    return group(o, scale=BUILDING_SCALE)


def model_well():
    """The village well: a stone parapet, dark water, two posts with a crossbar, a pulley and a small terracotta roof."""
    o = []
    o.append(cylinder("parapet", 0.085, 0.085, 0.07, (0, 0, 0.035), toon("stone", STONE), segs=14))
    o.append(cylinder("lip", 0.093, 0.093, 0.016, (0, 0, 0.075), toon("stone_dark", STONE_DARK), segs=14))
    o.append(cylinder("water", 0.066, 0.066, 0.004, (0, 0, 0.082), toon("water", WATER), segs=14, outline=False))
    for s in (-1, 1):
        o.append(box("post%d" % s, (0.016, 0.016, 0.20), (s * 0.075, 0, 0.17), toon("wood", WOOD)))
    o.append(cylinder("axle", 0.008, 0.008, 0.17, (0, 0, 0.215), toon("wood_dark", WOOD_DARK), segs=8,
                      rot=Matrix.Rotation(math.radians(90), 3, "Y")))
    o.append(cylinder("pulley", 0.022, 0.022, 0.02, (0, 0, 0.215), toon("wood", WOOD), segs=12,
                      rot=Matrix.Rotation(math.radians(90), 3, "Y")))
    o.append(cylinder("rope", 0.003, 0.003, 0.11, (0, -0.02, 0.16), toon("rope", 0xE8D9B0), segs=6, outline=False))
    o.append(cylinder("bucket", 0.020, 0.015, 0.03, (0.05, -0.085, 0.095), toon("brass", 0xD9A93C), segs=10))
    o.append(gable_roof("roof", 0.20, 0.13, 0.06, 0.265, toon("tile", TILE), overhang=0.015))
    return group(o, rot_z=math.radians(-90), scale=BUILDING_SCALE)


def model_hand_hut(stage):
    """The hand's hut: a round mud hut with a layered conical thatch roof. stage 'done' or 'building' (a bamboo frame half up)."""
    o = []
    o.append(cylinder("plinth", 0.17, 0.17, 0.03, (0, 0, 0.015), toon("plinth", PLINTH), segs=18))
    if stage == "done":
        o.append(cylinder("wall", 0.14, 0.14, 0.15, (0, 0, 0.105), toon("mud", MUD), segs=18))
        o.append(cylinder("band", 0.142, 0.142, 0.028, (0, 0, 0.044), toon("mud_dark", MUD_DARK), segs=18, outline=False))
        o.append(box("door", (0.06, 0.03, 0.105), (0.0, -0.13, 0.082), toon("door", DOOR)))
        o.append(box("doorframe", (0.075, 0.02, 0.118), (0.0, -0.125, 0.088), toon("trim", TRIM), outline=False))
        thatch, dark = toon("thatch", THATCH), toon("thatch_dark", THATCH_DARK)
        o.append(cylinder("eave", 0.235, 0.215, 0.03, (0, 0, 0.19), dark, segs=18))
        o.append(cylinder("roof", 0.225, 0.0, 0.22, (0, 0, 0.31), thatch, segs=18))
        o.append(cylinder("band", 0.075, 0.062, 0.022, (0, 0, 0.36), dark, segs=18))
        o.append(cylinder("knot", 0.02, 0.006, 0.05, (0, 0, 0.43), dark, segs=8))
        o += matka("pot", (0.10, -0.14, 0.0), r=0.018)
    else:
        o.append(cylinder("wall", 0.14, 0.14, 0.07, (0, 0, 0.065), toon("mud", MUD), segs=18))
        for i in range(8):
            a = i * math.tau / 8
            base = Vector((math.cos(a) * 0.16, math.sin(a) * 0.16, 0.10))
            top = Vector((0, 0, 0.36))
            mid, d = (base + top) / 2, (top - base)
            rot = d.to_track_quat("Z", "Y").to_matrix()
            o.append(cylinder("pole%d" % i, 0.006, 0.006, d.length, tuple(mid), toon("bamboo", 0xC9A060), segs=6, rot=rot))
        o.append(cylinder("ring", 0.105, 0.105, 0.012, (0, 0, 0.215), toon("bamboo", 0xC9A060), segs=18, cap=False))
        for i, (x, y) in enumerate(((0.19, -0.10), (0.215, -0.04), (0.20, -0.07))):
            o.append(cylinder("bundle%d" % i, 0.02, 0.02, 0.11, (x, y, 0.025 + (i == 2) * 0.035), toon("thatch", THATCH), segs=8,
                              rot=Matrix.Rotation(math.radians(90), 3, "X") @ Matrix.Rotation(math.radians(20 * i), 3, "Y")))
        for i in range(3):
            o.append(box("brick%d" % i, (0.05, 0.03, 0.022), (-0.20, -0.08 + i * 0.002, 0.011 + i * 0.022), toon("mud_dark", MUD_DARK),
                         rot_z=math.radians(10 * i)))
    return group(o, scale=BUILDING_SCALE)


PEOPLE = {
    # Founder: off-white kurta, indigo pyjama, a red gamchha over the shoulder.
    "founder": dict(sex="m", skin=0xB57A55, top=0xF1E6CF, bottom=0x3F4E7A, accent=0xC8423A, wear="kurta"),
    # Farm hand (man): saffron pagdi, white shirt under a green vest, white dhoti.
    "hand_m": dict(sex="m", skin=0xA86E4A, top=0xF3EEE2, bottom=0xF3EEE2, accent=0xE58A1F, vest=0x6E8C3A, wear="dhoti", head="pagdi"),
    # Farm hand (woman): a maroon cotton saree with a gold border, a mustard blouse, hair in a bun.
    "hand_f": dict(sex="f", skin=0xA9714F, top=0x8E2A3F, bottom=0x8E2A3F, accent=0xE3B23C, blouse=0xD99A2B, wear="saree"),
    # Day labourer (man): a white banyan, a rolled-up dhoti, a blue gamchha, a hoe on the shoulder.
    "labourer_m": dict(sex="m", skin=0x9C6444, top=0xF5F1E6, bottom=0xE9DFC8, accent=0x2E7DBF, wear="banyan", tool="hoe"),
    # Day labourer (woman): an orange-red saree with a yellow border, a blue blouse, a basket at the hip.
    "labourer_f": dict(sex="f", skin=0xB07650, top=0xE2582C, bottom=0xE2582C, accent=0xF2C94C, blouse=0x2E6FBA, wear="saree", tool="basket"),
    # Your people (spec 014, US-212): a townsman in a sky-blue kurta and white pyjama with a maroon gamchha.
    "town_m": dict(sex="m", skin=0xA8714C, top=0x86B9DA, bottom=0xF1E6CF, accent=0x9A3B3B, wear="kurta"),
    # A townswoman: a leaf-green cotton saree with a pink border, a pink blouse, head uncovered, hair in a bun (approved art direction).
    "town_f": dict(sex="f", skin=0xB37853, top=0x3E8E5A, bottom=0x3E8E5A, accent=0xE86A92, blouse=0xE86A92, wear="saree"),
    # A boy: a yellow half-shirt and navy shorts, bare legs.
    "boy": dict(sex="m", skin=0xB07A52, top=0xF2C14E, bottom=0x34477A, accent=0xF2C14E, wear="shorts", size=0.72),
    # A girl: a pink frock with a white collar, two plaits with red ribbons.
    "girl": dict(sex="f", skin=0xB37A55, top=0xE45D7B, bottom=0xE45D7B, accent=0xFFFFFF, blouse=0xE45D7B, wear="frock", plaits=True, size=0.72),
    # An elder (man): a white kurta and dhoti-pyjama, white hair and moustache, a slight stoop and a walking stick.
    "elder_m": dict(sex="m", skin=0x9E6A48, top=0xF3EFE6, bottom=0xF3EFE6, accent=0xC9B48A, wear="kurta", hair=0xD8D4CC, stoop=12, tool="stick"),
    # An elder (woman): a cream saree with a maroon border, grey hair in a bun, head uncovered.
    "elder_f": dict(sex="f", skin=0xA77150, top=0xEFE4CC, bottom=0xEFE4CC, accent=0x8E2A3F, blouse=0xC9B48A, wear="saree", hair=0xBDB6AC, stoop=8),
}


def model_person(kind, facing, pose="stand"):
    """A small villager with a big head, built facing -y (the viewer's lower left); facing 'front' or 'back' (walking away, to the upper left)."""
    p = PEOPLE[kind]
    soft = True
    def m(name, hexv):
        return toon(kind + "_" + name, hexv, soft=soft)
    skin, top, bottom, accent = m("skin", p["skin"]), m("top", p["top"]), m("bottom", p["bottom"]), m("accent", p["accent"])
    dark, hair = m("dark", 0x1E1410), m("hair", p.get("hair", HAIR))
    o = []
    female = p["sex"] == "f"
    # Chappals.
    for s in (-1, 1):
        o.append(sphere("chappal%d" % s, 0.0085, (s * 0.010, -0.004, 0.003), m("chappal", 0x5A3A22), scale=(0.8, 1.45, 0.38), segs=10, rings=6))
    if p["wear"] == "saree":
        o.append(cylinder("skirt", 0.031, 0.020, 0.064, (0, 0, 0.034), bottom, segs=18, smooth=True))
        o.append(cylinder("hem", 0.0315, 0.030, 0.008, (0, 0, 0.006), accent, segs=18, smooth=True, outline=False))
        for i in (-1, 0, 1):
            o.append(box("pleat%d" % i, (0.004, 0.004, 0.05), (i * 0.006, -0.026, 0.032), m("pleat", p["top"] - 0x101010 if p["top"] > 0x101010 else p["top"]), outline=False))
    elif p["wear"] == "frock":
        for s in (-1, 1):
            o.append(cylinder("leg%d" % s, 0.0070, 0.0078, 0.040, (s * 0.009, 0, 0.022), skin, segs=10, smooth=True))
        o.append(cylinder("skirt", 0.030, 0.019, 0.034, (0, 0, 0.056), bottom, segs=18, smooth=True))
        o.append(cylinder("hem", 0.0305, 0.029, 0.006, (0, 0, 0.040), accent, segs=18, smooth=True, outline=False))
    elif p["wear"] == "shorts":
        for s in (-1, 1):
            o.append(cylinder("leg%d" % s, 0.0072, 0.0080, 0.042, (s * 0.010, 0, 0.024), skin, segs=10, smooth=True))
        o.append(cylinder("shorts", 0.024, 0.021, 0.022, (0, 0, 0.054), bottom, segs=16, smooth=True))
    else:
        leg = skin if p["wear"] in ("dhoti", "banyan") else bottom
        for s in (-1, 1):
            o.append(cylinder("leg%d" % s, 0.0080, 0.0088, 0.042, (s * 0.010, 0, 0.024), leg, segs=10, smooth=True))
        if p["wear"] == "kurta":
            o.append(cylinder("kurtahem", 0.026, 0.022, 0.034, (0, 0, 0.060), top, segs=16, smooth=True))
        else:
            o.append(cylinder("dhoti", 0.025, 0.021, 0.026, (0, 0, 0.055), bottom, segs=16, smooth=True))
            o.append(box("fold", (0.008, 0.004, 0.024), (0.004, -0.023, 0.052), bottom, outline=False))
    upper = len(o)  # everything from here up bends in the work pose
    # Torso and rounded shoulders.
    body = m("blouse", p["blouse"]) if female else top
    o.append(cylinder("torso", 0.019, 0.022, 0.036, (0, 0, 0.083), body, segs=16, smooth=True))
    o.append(sphere("shoulders", 0.022, (0, 0, 0.099), body, scale=(1.18, 0.82, 0.55), segs=16, rings=8))
    if p.get("vest"):
        o.append(cylinder("vest", 0.0205, 0.0228, 0.034, (0, 0.0005, 0.084), m("vest", p["vest"]), segs=16, smooth=True, outline=False))
    if female and p["wear"] == "saree":
        # The pallu: from the right hip across the chest to the left shoulder, falling behind.
        o.append(box("pallu", (0.018, 0.006, 0.058), (0.004, -0.019, 0.082), top, rot=Matrix.Rotation(math.radians(-38), 3, "Y")))
        o.append(box("pallufall", (0.016, 0.006, 0.05), (-0.016, 0.018, 0.075), top))
        o.append(box("palluedge", (0.004, 0.0065, 0.058), (0.012, -0.021, 0.082), accent, outline=False, rot=Matrix.Rotation(math.radians(-38), 3, "Y")))
    if p.get("wear") == "kurta" or p.get("tool") == "hoe":
        o.append(box("gamchha", (0.014, 0.008, 0.04), (-0.016, -0.019, 0.086), accent, rot_z=math.radians(8)))
    # Arms: sleeve, forearm, hand.
    for s in (-1, 1):
        sleeve = body if p["wear"] != "banyan" else skin
        if p["wear"] == "frock":
            sleeve = body
        o.append(sphere("shoulderball%d" % s, 0.0085, (s * 0.025, 0, 0.097), sleeve, segs=10, rings=6))
        if female or p["wear"] in ("banyan", "shorts"):
            o.append(cylinder("sleeve%d" % s, 0.0080, 0.0075, 0.014, (s * 0.027, 0, 0.088), sleeve, segs=10, smooth=True,
                              rot=Matrix.Rotation(math.radians(s * 10), 3, "Y")))
            o.append(cylinder("forearm%d" % s, 0.0066, 0.0058, 0.028, (s * 0.030, 0, 0.068), skin, segs=10, smooth=True,
                              rot=Matrix.Rotation(math.radians(s * 8), 3, "Y")))
        else:
            o.append(cylinder("arm%d" % s, 0.0076, 0.0068, 0.040, (s * 0.029, 0, 0.077), sleeve, segs=10, smooth=True,
                              rot=Matrix.Rotation(math.radians(s * 10), 3, "Y")))
        o.append(sphere("palm%d" % s, 0.0072, (s * 0.032, -0.001, 0.053), skin, segs=10, rings=6))
        if female:
            o.append(cylinder("bangle%d" % s, 0.0068, 0.0068, 0.003, (s * 0.0315, 0, 0.058), m("gold", 0xE8B83A), segs=10, outline=False))
    # Neck and head.
    o.append(cylinder("neck", 0.0075, 0.0075, 0.01, (0, 0, 0.108), skin, segs=10, smooth=True, outline=False))
    hz = 0.137
    o.append(sphere("head", 0.031, (0, 0, hz), skin, scale=(1, 0.96, 0.98), segs=20, rings=12))
    for s in (-1, 1):
        o.append(sphere("ear%d" % s, 0.0058, (s * 0.030, 0.002, hz - 0.002), skin, scale=(0.5, 1, 1.2), segs=8, rings=6, outline=False))
    if facing == "front":
        o.append(sphere("nose", 0.0042, (0.0, -0.0305, hz - 0.004), skin, segs=8, rings=6, outline=False))
        for s in (-1, 1):
            o.append(sphere("eye%d" % s, 0.0042, (s * 0.0115, -0.0275, hz + 0.003), dark, scale=(0.85, 0.5, 1.25), segs=10, rings=6, outline=False))
            o.append(sphere("glint%d" % s, 0.0012, (s * 0.0115 + 0.0012, -0.0293, hz + 0.0055), m("white", 0xFFFFFF), segs=6, rings=4, outline=False))
            o.append(box("brow%d" % s, (0.009, 0.002, 0.0022), (s * 0.0118, -0.0285, hz + 0.0115), hair, outline=False))
            o.append(sphere("blush%d" % s, 0.0048, (s * 0.018, -0.0245, hz - 0.009), m("blush", 0xD9826E), scale=(1, 0.4, 0.65), segs=8, rings=6, outline=False))
        o.append(sphere("mouth", 0.003, (0, -0.0292, hz - 0.0135), m("mouth", 0x7A2E26), scale=(1.4, 0.5, 0.6), segs=8, rings=4, outline=False))
        if not female:
            if p.get("size", 1.0) == 1.0:   # no moustache on a boy
                o.append(sphere("moustache", 0.0045, (0, -0.0300, hz - 0.0095), hair, scale=(2.0, 0.55, 0.65), segs=8, rings=6, outline=False))
        else:
            o.append(sphere("bindi", 0.0022, (0, -0.0300, hz + 0.0135), m("bindi", 0xC0262D), segs=6, rings=4, outline=False))
    # Hair and headwear.
    o.append(sphere("hair", 0.0328, (0, 0.0045, hz + 0.0065), hair, scale=(1.0, 0.96, 0.82), segs=20, rings=12))
    if p.get("plaits"):
        for s in (-1, 1):
            o.append(cylinder("plait%d" % s, 0.0055, 0.0045, 0.034, (s * 0.024, 0.016, hz - 0.030), hair, segs=8, smooth=True))
            o.append(sphere("ribbon%d" % s, 0.0050, (s * 0.024, 0.016, hz - 0.049), m("ribbon", 0xD8283A), scale=(1.4, 0.8, 0.8), segs=8, rings=5, outline=False))
    elif female:
        o.append(sphere("bun", 0.0125, (0, 0.031, hz - 0.002), hair, segs=12, rings=8))
        for i, a in enumerate((-50, -15, 20, 55) if not p.get("stoop") else ()):
            x, z = math.sin(math.radians(a)) * 0.0125, math.cos(math.radians(a)) * 0.0125
            o.append(sphere("gajra%d" % i, 0.0032, (x, 0.033, hz - 0.002 + z * 0.6), m("jasmine", 0xFFF8E8), segs=6, rings=4, outline=False))
    elif p.get("head") == "pagdi":
        o.append(cylinder("pagdi", 0.0335, 0.031, 0.020, (0, 0.003, hz + 0.019), accent, segs=18, smooth=True))
        o.append(sphere("pagdicrown", 0.027, (0, 0.004, hz + 0.028), accent, scale=(1, 1, 0.5), segs=16, rings=8))
        o.append(box("pagditail", (0.010, 0.004, 0.026), (0.010, 0.030, hz + 0.004), accent))
    if pose.startswith("walk"):
        # Four frames: contact, passing, contact (other foot), passing. Legs swing about the hip, arms against them,
        # a saree sways; the body rises a little when passing.
        phase = int(pose[4:])
        swing = (1.0, 0.0, -1.0, 0.0)[phase]
        hip, shoulder = Vector((0, 0, 0.046)), Vector((0, 0, 0.097))
        for part in o:
            n = part.name
            side = -1 if "-1" in n else (1 if n.endswith("1") else 0)
            if side and (n.startswith("leg") or n.startswith("chappal")):
                part.matrix_world = Matrix.Translation(hip) @ Matrix.Rotation(math.radians(24 * swing * side), 4, "X") @ Matrix.Translation(-hip) @ part.matrix_world
            elif side and any(n.startswith(a) for a in ("arm", "sleeve", "forearm", "palm", "bangle", "shoulderball")):
                part.matrix_world = Matrix.Translation(shoulder) @ Matrix.Rotation(math.radians(-20 * swing * side), 4, "X") @ Matrix.Translation(-shoulder) @ part.matrix_world
            elif n in ("skirt", "hem") or n.startswith("pleat"):
                part.matrix_world = Matrix.Translation(hip) @ Matrix.Rotation(math.radians(4 * swing), 4, "Y") @ Matrix.Translation(-hip) @ part.matrix_world
        if phase % 2 == 1:
            for part in o:
                part.matrix_world = Matrix.Translation((0, 0, 0.003)) @ part.matrix_world
    if pose == "work":
        # Bent over a hoe: the upper body leans 30° forward about the hips, the arms reach further, and the hoe's blade bites the
        # ground in front. Two frames (stand, work) alternate on the map as a hoeing motion.
        hip = Vector((0, 0, 0.062))
        bend = Matrix.Translation(hip) @ Matrix.Rotation(math.radians(40), 4, "X") @ Matrix.Translation(-hip)
        for part in o[upper:]:
            if any(part.name.startswith(n) for n in ("arm", "sleeve", "forearm", "palm", "bangle", "shoulderball")):
                sh = Vector((0, 0, 0.097))
                part.matrix_world = Matrix.Translation(sh) @ Matrix.Rotation(math.radians(25), 4, "X") @ Matrix.Translation(-sh) @ part.matrix_world
            part.matrix_world = bend @ part.matrix_world
        wood, iron = m("wood", WOOD), m("iron", 0x7C8288)
        # The handle starts between the hands, wherever the bend put them, and its blade bites the ground ahead.
        palms = [part for part in o if part.name.startswith("palm")]
        centres = [part.matrix_world @ (sum((v.co for v in part.data.vertices), Vector()) / len(part.data.vertices)) for part in palms]
        top = sum(centres, Vector()) / len(centres)
        foot = Vector((0.0, top.y - 0.06, 0.006))
        mid, d = (top + foot) / 2, foot - top
        o.append(cylinder("hoehandle", 0.0028, 0.0028, d.length, tuple(mid), wood, segs=8, rot=d.to_track_quat("Z", "Y").to_matrix()))
        o.append(box("hoeblade", (0.034, 0.02, 0.006), (0.0, foot.y - 0.004, 0.005), iron))
        rot = math.radians(30) if facing == "front" else math.radians(-115)
        return group(o, rot_z=rot, scale=PERSON_SCALE, outline_factor=0.7)
    if p.get("stoop"):
        # An elder's stoop: the upper body leans forward a little about the hips.
        hip = Vector((0, 0, 0.062))
        bend = Matrix.Translation(hip) @ Matrix.Rotation(math.radians(p["stoop"]), 4, "X") @ Matrix.Translation(-hip)
        for part in o[upper:]:
            part.matrix_world = bend @ part.matrix_world
    # Tools.
    if p.get("tool") == "stick":
        palm = next(part for part in o if part.name == "palm1")
        c = palm.matrix_world @ (sum((v.co for v in palm.data.vertices), Vector()) / len(palm.data.vertices))
        o.append(cylinder("stick", 0.0030, 0.0030, c.z + 0.012, (c.x + 0.004, c.y - 0.008, (c.z + 0.012) / 2), m("wood", WOOD), segs=8))
    elif p.get("tool") == "hoe":
        handle = cylinder("handle", 0.0028, 0.0028, 0.10, (0.020, 0.012, 0.105), m("wood", WOOD), segs=8,
                          rot=Matrix.Rotation(math.radians(58), 3, "X"))
        o.append(handle)
        o.append(box("blade", (0.022, 0.004, 0.018), (0.020, 0.054, 0.137), m("iron", 0x7C8288), rot=Matrix.Rotation(math.radians(58), 3, "X")))
    elif p.get("tool") == "basket":
        o.append(cylinder("basket", 0.018, 0.014, 0.014, (-0.035, -0.004, 0.058), m("cane", 0xB8873E), segs=14))
        o.append(cylinder("basketrim", 0.0185, 0.0185, 0.004, (-0.035, -0.004, 0.065), m("caneDark", 0x8C6228), segs=14, outline=False))
        for i in range(3):
            o.append(sphere("veg%d" % i, 0.0062, (-0.035 + (i - 1) * 0.008, -0.004, 0.068), m("veg", (0xD94A3A, 0x4FB544, 0xE8C14A)[i]), segs=8, rings=6, outline=False))
    rot = math.radians(30) if facing == "front" else math.radians(-115)
    return group(o, rot_z=rot, scale=PERSON_SCALE * p.get("size", 1.0), outline_factor=0.7)


SPRITES = {
    "hut": model_hut,
    "well": model_well,
    "hand_hut": lambda: model_hand_hut("done"),
    "hand_hut_building": lambda: model_hand_hut("building"),
}
for _k in PEOPLE:
    for _f in ("front", "back"):
        SPRITES["person_%s_%s" % (_k, _f)] = (lambda k=_k, f=_f: (lambda: model_person(k, f)))()
        SPRITES["person_%s_%s_work" % (_k, _f)] = (lambda k=_k, f=_f: (lambda: model_person(k, f, "work")))()
        for _p in range(4):
            SPRITES["person_%s_%s_walk%d" % (_k, _f, _p)] = (lambda k=_k, f=_f, p=_p: (lambda: model_person(k, f, "walk%d" % p)))()


# ---------------------------------------------------------------- yard props (ADR 0040)

BAMBOO = 0xC9A060


def model_fence(axis):
    """A low bamboo fence one tile edge long (1.0), along x or y, centred on the origin: posts and two rails."""
    o = []
    bamboo, dark = toon("bamboo", BAMBOO), toon("bamboo_dark", 0x9C7A3E)
    n = 7
    for i in range(n):
        u = -0.5 + i / (n - 1)
        x, y = (u, 0) if axis == "x" else (0, u)
        o.append(cylinder("post%d" % i, 0.008, 0.007, 0.075, (x, y, 0.0375), dark, segs=8))
    for z in (0.028, 0.058):
        rot = Matrix.Rotation(math.radians(90), 3, "Y") if axis == "x" else Matrix.Rotation(math.radians(90), 3, "X")
        o.append(cylinder("rail%.2f" % z, 0.0045, 0.0045, 1.0, (0, 0, z), bamboo, segs=8, rot=rot))
    return group(o, scale=1.0)


def model_haystack():
    """A rounded haystack on a low base with a binding rope and a pole through the top."""
    hay, dark = toon("hay", 0xE7C25A), toon("hay_dark", 0xC79A3A)
    o = [cylinder("base", 0.10, 0.11, 0.05, (0, 0, 0.025), dark, segs=16, smooth=True),
         sphere("stack", 0.105, (0, 0, 0.10), hay, scale=(1, 1, 1.05), segs=18, rings=12),
         cylinder("cap", 0.06, 0.012, 0.07, (0, 0, 0.215), dark, segs=14, smooth=True),
         cylinder("rope", 0.104, 0.104, 0.008, (0, 0, 0.09), toon("rope", 0xB08A50), segs=18, outline=False),
         cylinder("pole", 0.004, 0.004, 0.08, (0, 0, 0.26), toon("wood", WOOD), segs=6)]
    return group(o, scale=BUILDING_SCALE)


def model_tulsi():
    """A tulsi chaura: a whitewashed stepped planter with an ochre band and a holy basil bush."""
    white, ochre, leaf = toon("lime", 0xF2EBDD), toon("ochre", 0xD08A3A), toon("tulsi", 0x4F8F3A)
    o = [box("step", (0.10, 0.10, 0.02), (0, 0, 0.01), white, bevel=0.004),
         box("pot", (0.07, 0.07, 0.07), (0, 0, 0.055), white, bevel=0.004),
         box("band", (0.072, 0.072, 0.012), (0, 0, 0.07), ochre, outline=False),
         sphere("bush", 0.04, (0, 0, 0.115), leaf, scale=(1, 1, 0.9), segs=12, rings=8),
         sphere("bush2", 0.026, (0.015, -0.01, 0.145), toon("tulsi_light", 0x6FAE4E), segs=10, rings=6)]
    return group(o, scale=BUILDING_SCALE)


def model_woodpile():
    """Split firewood stacked against a post, with an axe in the chopping block."""
    log, end = toon("log", 0x8A5A34), toon("logend", 0xD9B07A)
    o = []
    for row in range(3):
        for i in range(4 - row):
            x = (i - (3 - row) / 2) * 0.034
            o.append(cylinder("log%d%d" % (row, i), 0.016, 0.016, 0.13, (x, 0, 0.016 + row * 0.029), log, segs=8,
                              rot=Matrix.Rotation(math.radians(90), 3, "X")))
    o.append(cylinder("block", 0.03, 0.032, 0.04, (0.11, -0.03, 0.02), end, segs=12))
    o.append(box("axehandle", (0.006, 0.006, 0.07), (0.11, -0.03, 0.07), toon("wood", WOOD), rot=Matrix.Rotation(math.radians(20), 3, "Y")))
    o.append(box("axehead", (0.024, 0.006, 0.014), (0.098, -0.03, 0.045), toon("iron", 0x7C8288)))
    return group(o, scale=BUILDING_SCALE)


SPRITES.update({
    "fence_x": lambda: model_fence("x"),
    "fence_y": lambda: model_fence("y"),
    "haystack": model_haystack,
    "tulsi": model_tulsi,
    "woodpile": model_woodpile,
})


# ---------------------------------------------------------------- batches (many small parts, one object per material)

class Batch:
    """Collects many small parts into one mesh per material: a field of 300 wheat stalks renders as a handful of objects."""

    def __init__(self, name):
        self.name = name
        self.parts = {}   # material -> bmesh

    def _bm(self, mat):
        if mat not in self.parts:
            self.parts[mat] = bmesh.new()
        return self.parts[mat]

    def cone(self, mat, r1, r2, depth, m, segs=5):
        bm = self._bm(mat)
        geom = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=depth)
        bmesh.ops.transform(bm, matrix=m @ Matrix.Translation((0, 0, depth / 2)), verts=geom["verts"])

    def sphere(self, mat, r, m, scale=(1, 1, 1), segs=8, rings=5):
        bm = self._bm(mat)
        geom = bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=r)
        bmesh.ops.transform(bm, matrix=m @ Matrix.Diagonal((scale[0], scale[1], scale[2], 1.0)), verts=geom["verts"])

    def box(self, mat, size, m):
        bm = self._bm(mat)
        geom = bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.transform(bm, matrix=m @ Matrix.Diagonal((size[0], size[1], size[2], 1.0)), verts=geom["verts"])

    def objects(self, outline_for=(), smooth=False, thickness=OUTLINE):
        objs = []
        for i, (mat, bm) in enumerate(self.parts.items()):
            objs.append(obj_from_bm("%s%d" % (self.name, i), bm, mat, smooth=smooth, outline=mat in outline_for, thickness=thickness))
        self.parts = {}
        return objs


def at(x, y, z, rot_z=0.0, tilt=0.0, tilt_dir=0.0):
    """A placement matrix: at (x, y, z), turned rot_z, leaning `tilt` radians toward the heading tilt_dir."""
    lean = Matrix.Rotation(tilt_dir, 4, "Z") @ Matrix.Rotation(tilt, 4, "X") @ Matrix.Rotation(-tilt_dir, 4, "Z")
    return Matrix.Translation((x, y, z)) @ lean @ Matrix.Rotation(rot_z, 4, "Z")


# ---------------------------------------------------------------- ground (ADR 0041 §8): blocks like Kenney's, which the approved look is based on

TILE_DEPTH = 0.41                        # Kenney's 33 px side at 1x
SEAM = 0.6 / (PX_PER_UNIT / SCALE)       # tops overlap their neighbours by about half a pixel: no seams
SIDE = 0xA77D53
GRASS, GRASS_EDGE = 0x8AB549, 0x7FA843
SOIL, SOIL_RIDGE, SOIL_GROOVE = 0xB0825A, 0xC29470, 0x8F6542
EARTH, EARTH_EDGE = 0xD6BC90, 0xC7AA7C
WATER_TOP = 0x8FCBDD


def ground_block(name, top_hex, edge_hex, depth=TILE_DEPTH, top_z=0.0):
    """A 1 x 1 acre block, its top at top_z, sides down to the tile's depth, a slightly darker lip round the top."""
    s = 1.0 + SEAM
    o = [box(name + "body", (s, s, depth - 0.01), (0, 0, top_z - 0.01 - (depth - 0.01) / 2), toon("side", SIDE), outline=False),
         box(name + "lip", (s, s, 0.01), (0, 0, top_z - 0.005), toon(name + "edge", edge_hex), outline=False),
         box(name + "top", (s - 0.035, s - 0.035, 0.002), (0, 0, top_z + 0.001), toon(name + "top", top_hex), outline=False)]
    return o


RIDGES = 7                               # furrow ridges across a tilled acre
RIDGE_X = [-0.39 + i * 0.13 for i in range(RIDGES)]


def soil_ridges(b, top_z=0.003):
    """Raised ridges between darker furrows, along y."""
    for x in RIDGE_X:
        b.box(toon("ridge", SOIL_RIDGE), (0.075, 0.94, 0.018), Matrix.Translation((x, 0, top_z + 0.009)))


def model_ground(kind):
    if kind == "grass":
        return ground_block("grass", GRASS, GRASS_EDGE)
    if kind == "earth":
        return ground_block("earth", EARTH, EARTH_EDGE)
    if kind == "water":
        return ground_block("water", WATER_TOP, 0x7FBCD0, depth=TILE_DEPTH - 0.2, top_z=-0.2)
    o = ground_block("soil", SOIL_GROOVE, SOIL_GROOVE)
    b = Batch("ridges")
    soil_ridges(b)
    return o + b.objects()


def model_grass_detail(variant):
    """Tufts and a few flowers on a grass acre: three variants so the lawn isn't one colour."""
    import random
    rnd = random.Random(70 + variant)
    b = Batch("tufts")
    greens = [toon("tuftA", 0x6FA03A), toon("tuftB", 0x8CC152), toon("tuftC", 0x5E9233)]
    for _ in range(16):
        x, y = rnd.uniform(-0.42, 0.42), rnd.uniform(-0.42, 0.42)
        g = rnd.choice(greens)
        for k in range(5):
            a = k * math.tau / 5 + rnd.uniform(-0.4, 0.4)
            b.cone(g, 0.011, 0.0, rnd.uniform(0.05, 0.075), at(x + math.cos(a) * 0.006, y + math.sin(a) * 0.006, 0.0, tilt=0.5, tilt_dir=a), segs=3)
    for _ in range(4):
        x, y = rnd.uniform(-0.36, 0.36), rnd.uniform(-0.36, 0.36)
        petal = toon("flower%d" % (_ % 2), (0xFFF6D0, 0xF4D35E)[_ % 2])
        b.cone(toon("stem", 0x5E9233), 0.003, 0.003, 0.035, Matrix.Translation((x, y, 0.0)), segs=3)
        for k in range(5):
            a = k * math.tau / 5
            b.sphere(petal, 0.011, Matrix.Translation((x + math.cos(a) * 0.012, y + math.sin(a) * 0.012, 0.038)), scale=(1, 1, 0.5), segs=6, rings=3)
        b.sphere(toon("flowerCentre", 0xE8A33A), 0.008, Matrix.Translation((x, y, 0.041)), segs=6, rings=3)
    return b.objects()


# ---------------------------------------------------------------- field crops (ADR 0041 §8): a whole acre per stage

STAGES = ("g1", "g2", "g3", "ready")


def crop_wheat(b, rnd, x, y, stage):
    """A clump of wheat: blades while young, then stalks with ears, green to gold."""
    green, deep = toon("wheatGreen", 0x7CBF45), toon("wheatDeep", 0x5FA238)
    ear_green, gold, ear_gold = toon("wheatEarGreen", 0xA8CF5A), toon("wheatGold", 0xE6C266), toon("wheatEar", 0xD9A53A)
    if stage in ("g1", "g2"):
        h = 0.045 if stage == "g1" else 0.085
        n = 4 if stage == "g1" else 7
        for k in range(n):
            a = k * math.tau / n + rnd.uniform(-0.3, 0.3)
            b.cone(green if k % 2 else deep, 0.009, 0.0, h * rnd.uniform(0.8, 1.15), at(x, y, 0.02, tilt=0.38, tilt_dir=a), segs=3)
        return
    ripe = stage == "ready"
    stalk, ear = (gold, ear_gold) if ripe else (green, ear_green)
    for k in range(7):
        a = k * math.tau / 7 + rnd.uniform(-0.4, 0.4)
        h = rnd.uniform(0.11, 0.14)
        tilt = rnd.uniform(0.10, 0.25) + (0.08 if ripe else 0.0)
        m = at(x + math.cos(a) * 0.01, y + math.sin(a) * 0.01, 0.02, tilt=tilt, tilt_dir=a)
        b.cone(stalk, 0.0042, 0.0032, h, m, segs=4)
        b.sphere(ear, 0.012, m @ Matrix.Translation((0, 0, h + 0.018)), scale=(0.85, 0.85, 2.3), segs=7, rings=5)
    for k in range(4):  # leaves at the foot
        a = k * math.tau / 4 + rnd.uniform(-0.3, 0.3)
        b.cone(deep if not ripe else toon("wheatLeafDry", 0xCDB25E), 0.009, 0.0, 0.06, at(x, y, 0.02, tilt=0.65, tilt_dir=a), segs=3)


def crop_mat(b, stage, young, ripe, low=False, width=0.045, y0=-0.45, length=0.9):
    """A low, leafy band along each ridge so a grown field reads as a carpet rather than stalks on bare soil."""
    if stage == "g1":
        return
    h = {"g2": 0.024, "g3": 0.038, "ready": 0.038}[stage] * (0.6 if low else 1.0)
    mat = ripe if stage == "ready" else young
    for x in RIDGE_X:
        # A half-buried rounded band along the ridge: an ellipse 0.045 wide and h tall, 0.9 long.
        m = Matrix.Translation((x, y0, 0.012)) @ Matrix.Rotation(math.radians(-90), 4, "X") @ Matrix.Diagonal((width, h, 1.0, 1.0))
        b.cone(mat, 1.0, 1.0, length, m, segs=12)


def leaf(b, mat, x, y, z, length, width, direction, tilt, rot=0.0):
    """A flat leaf: an ellipsoid lying out from (x, y, z) towards `direction`, raised `tilt` radians from the ground."""
    m = Matrix.Translation((x, y, z)) @ Matrix.Rotation(direction, 4, "Z") @ Matrix.Rotation(-tilt, 4, "Y") @ Matrix.Rotation(rot, 4, "X") \
        @ Matrix.Translation((length / 2, 0, 0))
    b.sphere(mat, 1.0, m, scale=(length / 2, width / 2, width / 6), segs=8, rings=4)


def crop_radish(b, rnd, x, y, stage):
    """Mooli: a rosette of long leaves; ready, the white root's green shoulder shows above the soil."""
    green, deep = toon("radishLeaf", 0x6DBE45), toon("radishDeep", 0x4E9A36)
    n, length = {"g1": (3, 0.035), "g2": (5, 0.06), "g3": (7, 0.08), "ready": (8, 0.09)}[stage]
    if stage == "ready":
        # The white root's shoulder stands proud of the soil, its top green where the sun reaches it.
        b.cone(toon("mooli", 0xF4F1E6), 0.019, 0.016, 0.04, Matrix.Translation((x, y, 0.0)), segs=12)
        b.cone(toon("mooliTop", 0xA8CF6A), 0.016, 0.006, 0.01, Matrix.Translation((x, y, 0.04)), segs=12)
    base = 0.045 if stage == "ready" else 0.02
    for k in range(n):
        a = k * math.tau / n + rnd.uniform(-0.3, 0.3)
        leaf(b, green if k % 2 else deep, x, y, base, length * rnd.uniform(0.85, 1.1), length * 0.42, a, rnd.uniform(0.5, 0.9))


def crop_rice(b, rnd, x, y, stage):
    """Paddy in standing water: clumps of blades, then panicles that droop gold."""
    green, deep, dry = toon("riceGreen", 0x74C04A), toon("riceDeep", 0x58A43A), toon("riceDry", 0xC9C06A)
    grain = toon("riceGrain", 0xDDB64A) if stage == "ready" else toon("riceGrainGreen", 0xA9CC5C)
    n, h = {"g1": (6, 0.045), "g2": (10, 0.08), "g3": (12, 0.115), "ready": (12, 0.115)}[stage]
    for k in range(n):
        a = k * math.tau / n + rnd.uniform(-0.3, 0.3)
        blade = (dry if k % 3 else deep) if stage == "ready" else (green if k % 2 else deep)
        b.cone(blade, 0.009, 0.0, h * rnd.uniform(0.85, 1.15), at(x, y, 0.02, tilt=0.32, tilt_dir=a), segs=3)
    if stage in ("g3", "ready"):
        for k in range(4):
            a = k * math.tau / 3 + rnd.uniform(-0.5, 0.5)
            droop = 0.9 if stage == "ready" else 0.5
            for j in range(4):  # a chain of grains bending outward from the top
                t = j / 3.0
                gx = x + math.cos(a) * (0.012 + 0.03 * t)
                gy = y + math.sin(a) * (0.012 + 0.03 * t)
                gz = 0.02 + h * 0.95 - droop * 0.035 * t * t
                b.sphere(grain, 0.009, Matrix.Translation((gx, gy, gz)), scale=(1, 1, 1.3), segs=6, rings=4)


def crop_potato(b, rnd, x, y, stage):
    """Leafy bushes on earthed-up ridges; flowers before harvest, then the haulm yellows and a few tubers show."""
    leafy, deep = toon("potatoLeaf", 0x5BAA3E), toon("potatoDeep", 0x3F8A32)
    yellow = toon("potatoHaulm", 0xB9A84E)
    n, r = {"g1": (3, 0.012), "g2": (6, 0.02), "g3": (8, 0.026), "ready": (6, 0.022)}[stage]
    top = 0.045
    for k in range(n):
        a = k * math.tau / n + rnd.uniform(-0.3, 0.3)
        d = r * rnd.uniform(0.4, 1.0)
        mat = yellow if stage == "ready" else (leafy if k % 2 else deep)
        b.sphere(mat, r, Matrix.Translation((x + math.cos(a) * d, y + math.sin(a) * d, top + r * 0.5 + rnd.uniform(0, 0.01))), scale=(1, 1, 0.75), segs=8, rings=5)
    if stage == "g3":
        for k in range(3):
            a = rnd.uniform(0, math.tau)
            b.sphere(toon("potatoFlower", (0xF6F2FA, 0xC9B4E6)[k % 2]), 0.0065, Matrix.Translation((x + math.cos(a) * r, y + math.sin(a) * r, top + r * 1.6)), segs=6, rings=4)
    if stage == "ready":
        for k in range(2):
            a = rnd.uniform(0, math.tau)
            b.sphere(toon("potatoTuber", 0xC9A06A), 0.011, Matrix.Translation((x + math.cos(a) * 0.03, y + math.sin(a) * 0.03, 0.03)), scale=(1.2, 1, 0.85), segs=8, rings=5)


def crop_lentils(b, rnd, x, y, stage):
    """Masoor: small feathery bushes, pale flowers, then tan, dry plants full of flat pods."""
    green, deep = toon("lentilLeaf", 0x8CC75C), toon("lentilDeep", 0x6AAE47)
    dry, pod = toon("lentilDry", 0xC2A464), toon("lentilPod", 0xA8874A)
    n, h = {"g1": (4, 0.035), "g2": (7, 0.06), "g3": (9, 0.08), "ready": (9, 0.075)}[stage]
    for k in range(n):
        a = k * math.tau / n + rnd.uniform(-0.4, 0.4)
        stem = at(x, y, 0.02, tilt=rnd.uniform(0.25, 0.5), tilt_dir=a)
        b.cone(dry if stage == "ready" else deep, 0.0035, 0.0025, h, stem, segs=3)
        for j in range(3):  # leaflets along the stem
            mat = dry if stage == "ready" else (green if j % 2 else deep)
            b.sphere(mat, 0.011, stem @ Matrix.Translation((0.006 * (1 if j % 2 else -1), 0, h * (0.45 + 0.25 * j))), scale=(1.4, 0.7, 0.5), segs=6, rings=4)
        if stage == "g3" and k % 2 == 0:
            b.sphere(toon("lentilFlower", 0xE8ECF8), 0.0075, stem @ Matrix.Translation((0, 0, h + 0.006)), segs=6, rings=4)
        if stage == "ready":
            b.sphere(pod, 0.009, stem @ Matrix.Translation((0.008, 0, h * 0.7)), scale=(1.5, 0.6, 0.9), segs=6, rings=4)


CROPS = {"wheat": crop_wheat, "radish": crop_radish, "rice": crop_rice, "potato": crop_potato, "lentils": crop_lentils}
# The band under the plants along each ridge (growing, ready); potato's is the earthed-up ridge itself; rice stands in water.
MATS = {"wheat": (0x5FA238, 0xC9A34E), "radish": None, "potato": (0x9A6B45, 0x9A6B45), "lentils": (0x6FA845, 0xB59A55), "rice": None}


def model_crop(crop, stage):
    """A crop across a whole acre: plants on the ridges of the tilled soil, a little jitter so it reads as grown, not stamped."""
    import random
    rnd = random.Random(sum(ord(ch) for ch in crop + stage))
    b = Batch(crop)
    per_row = 10
    if crop == "rice":
        # Standing water over the whole field, between the ridges, a little lighter at the rim.
        b.box(toon("paddyWater", 0x86BAC6), (0.95, 0.95, 0.004), Matrix.Translation((0, 0, 0.016)))
        b.box(toon("paddyBund", 0x9A6B45), (0.97, 0.97, 0.012), Matrix.Translation((0, 0, 0.008)))
    elif crop == "potato":
        for x in RIDGE_X:  # earthed-up ridges from the first sowing
            m = Matrix.Translation((x, -0.45, 0.012)) @ Matrix.Rotation(math.radians(-90), 4, "X") @ Matrix.Diagonal((0.05, 0.03, 1.0, 1.0))
            b.cone(toon("potatoRidge", 0x9A6B45), 1.0, 1.0, 0.9, m, segs=12)
    elif MATS[crop] is not None:
        young, ripe = MATS[crop]
        width = 0.06 if crop == "berseem" else 0.045   # berseem is broadcast: a near-continuous carpet
        if stage == "cut":   # the far half still standing, the near half cut back to a thin stubble band
            crop_mat(b, "ready", toon(crop + "Mat", young), toon(crop + "MatRipe", ripe), low=True, width=width, y0=0.0, length=0.45)
            crop_mat(b, "g2", toon(crop + "Stubble", 0x9A9A5A), None, low=True, width=width * 0.4, y0=-0.45, length=0.45)
        else:
            crop_mat(b, stage, toon(crop + "Mat", young), toon(crop + "MatRipe", ripe), low=crop in ("lentils", "berseem"), width=width)
    for x in RIDGE_X:
        for j in range(per_row):
            y = -0.42 + j * (0.84 / (per_row - 1)) + rnd.uniform(-0.012, 0.012)
            # A fodder field being cut a strip a day (spec 012): the near half cut back to stubble, the far half standing.
            st = ("stub" if y < 0.0 else "ready") if stage == "cut" else stage
            CROPS[crop](b, rnd, x + rnd.uniform(-0.01, 0.01), y, st)
    if stage == "cut":
        fodder_bundles(b, rnd, crop)
    return b.objects(outline_for=(), smooth=False)


# ---------------------------------------------------------------- trees (ADR 0041 §8)

def model_sapling(bark_hex, leaf_hexes, leaf_len, leaf_w):
    """A grafted sapling just planted: one slender stem with tiers of leaves, in a watering basin (thala) of dug earth."""
    import random
    rnd = random.Random(23)
    bark = toon("saplingBark", bark_hex)
    o = [cylinder("basin", 0.085, 0.085, 0.012, (0, 0, 0.006), toon("basinSoil", 0x7A5232), segs=16, outline=False),
         cylinder("bund", 0.10, 0.092, 0.02, (0, 0, 0.01), toon("basinBund", 0xA77D53), segs=16),
         cylinder("basinTop", 0.084, 0.084, 0.022, (0, 0, 0.011), toon("basinSoil", 0x7A5232), segs=16, outline=False)]
    o.append(cylinder("stem", 0.008, 0.005, 0.24, (0, 0, 0.12), bark, segs=6, rot=Matrix.Rotation(math.radians(4), 3, "X")))
    b = Batch("sapling")
    mats = [toon("saplingLeaf%d" % i, h) for i, h in enumerate(leaf_hexes)]
    for t, (z, n) in enumerate(((0.12, 3), (0.17, 4), (0.215, 4), (0.245, 3))):
        for i in range(n):
            a = i * math.tau / n + t * 0.7 + rnd.uniform(-0.2, 0.2)
            leaf(b, mats[(i + t) % 2], math.cos(a) * 0.006, math.sin(a) * 0.006, z, leaf_len * (0.8 if t == 3 else 1.0), leaf_w, a, 0.55 - 0.25 * (t < 2))
    o += b.objects(outline_for=tuple(b.parts), thickness=OUTLINE * 0.7)
    return group(o, scale=BUILDING_SCALE)


def model_mango(stage):
    """A mango tree: a stout trunk with three limbs and a dense dome of dark leaves; in fruit, mangoes hang under the canopy."""
    import random
    rnd = random.Random(7)
    k = {"sapling": 0.30, "young": 0.62, "mature": 1.0, "ripe": 1.0}[stage]
    bark, dark, mid, light = toon("bark", 0x7A4E2E), toon("mangoDark", 0x2F7A35), toon("mangoMid", 0x3F9440), toon("mangoLight", 0x63AE4A)
    o = []
    if stage == "sapling":
        return model_sapling(0x7A4E2E, (0x3F9440, 0x2F7A35), 0.075, 0.022)
    h = 0.30 * k
    o.append(cylinder("trunk", 0.045 * k, 0.03 * k, h, (0, 0, h / 2), bark, segs=10, smooth=True))
    for i in range(3):
        a = i * math.tau / 3 + 0.4
        base, tip = Vector((0, 0, h * 0.9)), Vector((math.cos(a) * 0.10 * k, math.sin(a) * 0.10 * k, h + 0.10 * k))
        mid_, d = (base + tip) / 2, tip - base
        o.append(cylinder("limb%d" % i, 0.022 * k, 0.012 * k, d.length, tuple(mid_), bark, segs=8, rot=d.to_track_quat("Z", "Y").to_matrix(), smooth=True))
    cz = h + 0.17 * k
    blobs = [(0, 0, 0.05, 0.20), (0.13, 0.03, -0.02, 0.15), (-0.12, 0.06, -0.01, 0.15), (0.03, -0.13, -0.02, 0.15),
             (-0.06, 0.13, 0.0, 0.14), (0.09, 0.11, 0.04, 0.13), (-0.09, -0.09, 0.03, 0.13), (0.0, 0.0, 0.14, 0.13)]
    for i, (x, y, z, r) in enumerate(blobs):
        mat = light if z > 0.1 else (dark if z < 0 else mid)
        o.append(sphere("canopy%d" % i, r * k, (x * k, y * k, cz + z * k), mat, scale=(1, 1, 0.82), segs=14, rings=9))
    if stage == "ripe":
        fruit, blush = toon("mango", 0xF4B33C), toon("mangoBlush", 0xE8723A)
        # The camera looks from +x, -y: hang the fruit round the front of the canopy, under its lower edge and on its face.
        for i in range(18):
            a = math.radians(-45) + math.radians(rnd.uniform(-85, 85))
            r = rnd.uniform(0.20, 0.27)
            x, y = math.cos(a) * r, math.sin(a) * r
            z = cz + rnd.uniform(-0.12, 0.02)
            o.append(sphere("mango%d" % i, 0.03, (x, y, z), fruit if i % 3 else blush, scale=(0.85, 0.75, 1.25), segs=10, rings=6))
            o.append(cylinder("stalk%d" % i, 0.0025, 0.0025, 0.03, (x, y, z + 0.045), bark, segs=4, outline=False))
    return group(o, scale=BUILDING_SCALE)


# ---------------------------------------------------------------- the kitchen garden's beds (ADR 0041 §8)

def model_bed(crop, stage):
    """A raised bed with a plank border and dark soil; garden crops in a 3 x 3 grid."""
    import random
    rnd = random.Random(sum(ord(ch) for ch in (crop or "") + (stage or "")))
    w = 0.46
    plank, plank_dark, soil = toon("plank", 0xB07A44), toon("plankDark", 0x8E5E32), toon("bedSoil", 0x6E4528)
    o = [box("soil", (w - 0.04, w - 0.04, 0.05), (0, 0, 0.025), soil, outline=False)]
    for i, (sx, sy, rx) in enumerate(((0, -w / 2, 0), (0, w / 2, 0), (-w / 2, 0, 1), (w / 2, 0, 1))):
        size = (0.026, w + 0.026, 0.06) if rx else (w + 0.026, 0.026, 0.06)
        o.append(box("plank%d" % i, size, (sx, sy, 0.03), plank if i % 2 == 0 else plank_dark, bevel=0.004))
    if crop is None:
        b = Batch("clods")
        for i in range(10):
            b.sphere(toon("clod", 0x5C3A22), 0.01, Matrix.Translation((rnd.uniform(-0.17, 0.17), rnd.uniform(-0.17, 0.17), 0.05)), scale=(1.3, 1, 0.5), segs=6, rings=4)
        return group(o + b.objects(), scale=1.0)
    b = Batch(crop)
    k = {"g1": 0.45, "g2": 0.7, "g3": 0.9, "ready": 1.0}[stage]
    for ix in (-1, 0, 1):
        for iy in (-1, 0, 1):
            x, y = ix * 0.13 + rnd.uniform(-0.01, 0.01), iy * 0.13 + rnd.uniform(-0.01, 0.01)
            GARDEN[crop](b, rnd, x, y, 0.05, k, stage == "ready")
    return group(o + b.objects(), scale=1.0)


def garden_radish(b, rnd, x, y, z, k, ready):
    for j in range(6):
        a = j * math.tau / 6 + rnd.uniform(-0.3, 0.3)
        leaf(b, toon("gRadish%d" % (j % 2), (0x6DBE45, 0x4E9A36)[j % 2]), x, y, z, 0.075 * k, 0.03 * k, a, 0.7)
    if ready:
        b.sphere(toon("redRadish", 0xD8344E), 0.02, Matrix.Translation((x, y, z + 0.006)), scale=(1, 1, 0.9), segs=10, rings=6)


def garden_spinach(b, rnd, x, y, z, k, ready):
    for j in range(7):
        a = j * math.tau / 7 + rnd.uniform(-0.3, 0.3)
        leaf(b, toon("spinach%d" % (j % 2), (0x2E8E45, 0x3FA455)[j % 2]), x, y, z, 0.07 * k, 0.05 * k, a, 0.55 + 0.25 * (j % 2))


def garden_coriander(b, rnd, x, y, z, k, ready):
    light, mid = toon("dhania", 0x86D45E), toon("dhaniaMid", 0x68B848)
    for j in range(7):
        a = j * math.tau / 7 + rnd.uniform(-0.3, 0.3)
        stem = at(x, y, z, tilt=0.5, tilt_dir=a)
        b.cone(mid, 0.0025, 0.002, 0.06 * k, stem, segs=3)
        for q in range(3):  # lacy leaflets
            b.sphere(light if q % 2 else mid, 0.011 * k, stem @ Matrix.Translation(((q - 1) * 0.008, 0, 0.06 * k)), scale=(1, 1, 0.45), segs=6, rings=4)
    if ready:
        for q in range(3):
            a = rnd.uniform(0, math.tau)
            b.sphere(toon("dhaniaFlower", 0xFFFFFF), 0.006, Matrix.Translation((x + math.cos(a) * 0.02, y + math.sin(a) * 0.02, z + 0.07)), segs=6, rings=4)


GARDEN = {"garden_radish": garden_radish, "garden_spinach": garden_spinach, "garden_coriander": garden_coriander}


# ---------------------------------------------------------------- more trees, the planting pit and the sale sign

def model_banana(stage):
    """A banana plant: a green pseudostem and big arching paddle leaves; in fruit, a hanging bunch and the purple bud."""
    import random
    rnd = random.Random(11)
    k = {"sapling": 0.35, "young": 0.65, "mature": 1.0, "ripe": 1.0}[stage]
    stem, leaf_mat, leaf_dark, dry = toon("bananaStem", 0x9DBB57), toon("bananaLeaf", 0x6CC24A), toon("bananaLeafDark", 0x4FA03C), toon("bananaDry", 0xB7A257)
    o = [cylinder("stem", 0.04 * k, 0.03 * k, 0.32 * k, (0, 0, 0.16 * k), stem, segs=10, smooth=True)]
    b = Batch("banana")
    n = 7 if k > 0.5 else 4
    top = 0.30 * k
    # No leaf points at the camera (from +x, -y): seen end-on a paddle leaf is a shapeless blob. They fan round the sides and back.
    cam = math.radians(-45)
    for i in range(n):
        a = cam + math.radians(40) + i * math.radians(280) / (n - 1) + rnd.uniform(-0.12, 0.12)
        mat = leaf_mat if i % 2 else leaf_dark
        # A long paddle in two parts: the inner half rises steeply, the outer half arches over and droops.
        rise = rnd.uniform(0.75, 1.05)
        L = 0.17 * k
        leaf(b, mat, 0, 0, top, L, 0.10 * k, a, rise)
        ex, ey, ez = math.cos(a) * L * math.cos(rise), math.sin(a) * L * math.cos(rise), top + L * math.sin(rise)
        leaf(b, mat, ex, ey, ez, L * 1.1, 0.10 * k, a, -rnd.uniform(0.35, 0.7))
        b.cone(stem, 0.006 * k, 0.004 * k, L, Matrix.Translation((0, 0, top)) @ Vector((math.cos(a) * math.cos(rise), math.sin(a) * math.cos(rise), math.sin(rise))).to_track_quat("Z", "Y").to_matrix().to_4x4(), segs=4)
    if k == 1.0:  # an old leaf, dried, hangs down the stem at the side
        leaf(b, dry, 0, 0, top - 0.01, 0.2, 0.08, cam + math.radians(95), -1.25)
    o += b.objects(outline_for=tuple(m for m in b.parts if m is not stem), thickness=OUTLINE * 0.7)
    if stage == "ripe":
        # The bunch hangs from the crown on a curved stalk, at the front where the camera sees it, tiers of fingers pointing up.
        fruit, bud, stalk = toon("banana", 0xE8D45A), toon("bananaBud", 0x7A2E4A), toon("bananaStalk", 0x8FA84A)
        bx, by = 0.075, -0.075
        o.append(cylinder("stalk", 0.006, 0.006, 0.20, (bx * 0.8, by * 0.8, top - 0.04), stalk, segs=6, rot=Matrix.Rotation(math.radians(20), 3, "X")))
        for r in range(4):
            for j in range(7):
                a = j * math.tau / 7 + r * 0.45
                o.append(sphere("finger%d%d" % (r, j), 0.017, (bx + math.cos(a) * 0.03, by + math.sin(a) * 0.03, top - 0.03 - r * 0.04), fruit,
                                scale=(0.65, 0.65, 1.7), segs=8, rings=5))
        o.append(sphere("bud", 0.026, (bx, by, top - 0.21), bud, scale=(0.8, 0.8, 1.5), segs=10, rings=6))
    return group(o, scale=BUILDING_SCALE)


def model_papaya(stage):
    """A papaya: one slender grey-green trunk with leaf scars, a crown of lobed leaves on long stalks, fruit hugging the trunk."""
    import random
    rnd = random.Random(13)
    k = {"sapling": 0.35, "young": 0.65, "mature": 1.0, "ripe": 1.0}[stage]
    trunk, leaf_mat, leaf_dark, stalk = toon("papayaTrunk", 0x9C9A7A), toon("papayaLeaf", 0x5DB04A), toon("papayaLeafDark", 0x46923A), toon("papayaStalk", 0xB8C46A)
    h = 0.45 * k
    o = [cylinder("trunk", 0.028 * k, 0.02 * k, h, (0, 0, h / 2), trunk, segs=10, smooth=True)]
    for i in range(int(6 * k)):
        o.append(cylinder("scar%d" % i, 0.029 * k, 0.029 * k, 0.004, (0, 0, h * (0.2 + 0.12 * i)), toon("papayaScar", 0x7E7C60), segs=10, outline=False))
    b = Batch("papaya")
    for i in range(9):
        a = i * math.tau / 9 + rnd.uniform(-0.25, 0.25)
        tilt = (0.95, 0.45, -0.1)[i % 3] + rnd.uniform(-0.15, 0.15)   # young leaves up, old ones level or hanging
        d = Vector((math.cos(a) * math.cos(tilt), math.sin(a) * math.cos(tilt), math.sin(tilt)))
        tip = Vector((0, 0, h)) + d * 0.12 * k
        b.cone(stalk, 0.0045 * k, 0.003 * k, 0.12 * k, Matrix.Translation((0, 0, h)) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4(), segs=4)
        for q in range(5):  # the five lobes of a big palmate leaf, following its stalk and drooping at the edges
            leaf(b, leaf_mat if (i + q) % 2 else leaf_dark, tip.x, tip.y, tip.z, 0.085 * k, 0.05 * k, a + (q - 2) * 0.45, tilt * 0.5 - 0.15 - 0.12 * abs(q - 2))
    o += b.objects(outline_for=tuple(m for m in b.parts if m is not stalk), thickness=OUTLINE * 0.7)
    if stage == "ripe":
        # Fruit hugs the trunk just under the crown, mostly on the side the camera sees.
        for i in range(9):
            a = math.radians(-45) + (i % 5 - 2) * 0.55
            o.append(sphere("papaya%d" % i, 0.036, (math.cos(a) * 0.05, math.sin(a) * 0.05, h - 0.06 - (i // 5) * 0.07),
                            toon("papayaFruit%d" % (i % 3 == 0), (0xF2A23A, 0x9CC24A)[i % 3 == 0]), scale=(0.8, 0.8, 1.3), segs=10, rings=6))
    return group(o, scale=BUILDING_SCALE)


def model_guava(stage):
    """A guava: a low tree with smooth, mottled bark, two stems, an open round canopy of lighter leaves; pale fruit when ripe."""
    import random
    rnd = random.Random(17)
    if stage == "sapling":
        return model_sapling(0xA9805A, (0x86C25A, 0x67AA45), 0.058, 0.032)
    k = 0.6 if stage == "young" else 0.85
    bark, light, mid = toon("guavaBark", 0xA9805A), toon("guavaLight", 0x86C25A), toon("guavaMid", 0x67AA45)
    o = []
    for s_ in (-1, 1):
        o.append(cylinder("stem%d" % s_, 0.025 * k, 0.016 * k, 0.26 * k, (s_ * 0.025 * k, 0, 0.13 * k), bark, segs=8, rot=Matrix.Rotation(math.radians(s_ * 8), 3, "Y"), smooth=True))
    cz = 0.33 * k
    for i, (x, y, z, r) in enumerate([(0, 0, 0.04, 0.16), (0.12, 0.02, -0.02, 0.12), (-0.11, 0.05, -0.01, 0.12), (0.02, -0.11, -0.02, 0.12), (0.05, 0.1, 0.02, 0.11)]):
        o.append(sphere("canopy%d" % i, r * k, (x * k, y * k, cz + z * k), light if z > 0.02 else mid, scale=(1, 1, 0.8), segs=14, rings=9))
    if stage == "ripe":
        for i in range(12):
            a = math.radians(-45) + math.radians(rnd.uniform(-85, 85))
            r = rnd.uniform(0.19, 0.23) * k
            o.append(sphere("guava%d" % i, 0.026, (math.cos(a) * r, math.sin(a) * r, cz + rnd.uniform(-0.12, 0.01) * k), toon("guavaFruit%d" % (i % 2), (0xF0E27A, 0xE2D45A)[i % 2]), segs=10, rings=6))
    return group(o, scale=BUILDING_SCALE)


def model_pit():
    """An empty planting place: a ring of stones round a dark, dug circle."""
    o = [cylinder("hole", 0.075, 0.075, 0.006, (0, 0, 0.003), toon("dug", 0x5C3A22), segs=16, outline=False)]
    b = Batch("stones")
    for i in range(11):
        a = i * math.tau / 11
        b.sphere(toon("stone%d" % (i % 2), (0xC9C2B4, 0xA8A194)[i % 2]), 0.017, Matrix.Translation((math.cos(a) * 0.09, math.sin(a) * 0.09, 0.008)), scale=(1.2, 1, 0.7), segs=8, rings=5)
    return o + b.objects(outline_for=tuple(b.parts), thickness=OUTLINE * 0.6)


def model_sign():
    """Land for sale: a wooden post with a plank board and a coin painted on it."""
    wood, board = toon("wood", WOOD), toon("signBoard", 0xF2DFB0)
    o = [box("post", (0.016, 0.016, 0.22), (0, 0, 0.11), wood),
         box("board", (0.13, 0.012, 0.09), (0, -0.012, 0.17), board, bevel=0.004),
         cylinder("coin", 0.026, 0.026, 0.004, (0, -0.02, 0.17), toon("coin", 0xFFC93C), segs=20, rot=Matrix.Rotation(math.radians(90), 3, "X"), outline=False),
         cylinder("coinRing", 0.018, 0.018, 0.005, (0, -0.0205, 0.17), toon("coinDark", 0xE0A21C), segs=20, rot=Matrix.Rotation(math.radians(90), 3, "X"), outline=False)]
    return group(o, rot_z=math.radians(45), scale=BUILDING_SCALE)


MARKER_SCALE = 2.2      # map bubbles read at a glance: about the painted bubbles' size


def model_marker(kind):
    """A map bubble: a white (or tinted) rounded bubble on a short tail, its symbol modelled on the face."""
    fill = {"ready": 0xFFFFFF, "spoil": 0xFFD27A, "pest": 0xFF8A80}[kind]
    m = Matrix.Rotation(math.radians(90), 3, "X")
    o = [cylinder("bubble", 0.07, 0.07, 0.03, (0, 0, 0.12), toon("bubble" + kind, fill), segs=28, rot=m),
         cylinder("tail", 0.03, 0.0, 0.05, (0, 0, 0.04), toon("bubble" + kind, fill), segs=10)]
    if kind == "ready":
        g = toon("tick", 0x4CC24A)
        o.append(box("tick1", (0.05, 0.022, 0.016), (-0.017, -0.02, 0.112), g, rot=Matrix.Rotation(math.radians(90), 3, "X") @ Matrix.Rotation(math.radians(-45), 3, "Z")))
        o.append(box("tick2", (0.09, 0.022, 0.016), (0.02, -0.02, 0.128), g, rot=Matrix.Rotation(math.radians(90), 3, "X") @ Matrix.Rotation(math.radians(48), 3, "Z")))
    elif kind == "spoil":
        c = toon("drop", 0xC2410C)
        o.append(box("bar", (0.02, 0.016, 0.06), (0, -0.02, 0.135), c))
        o.append(sphere("dot", 0.013, (0, -0.02, 0.087), c, segs=10, rings=6))
    else:
        c = toon("bug", 0x7A1F1F)
        o.append(sphere("bug", 0.03, (0, -0.02, 0.12), c, scale=(1.2, 0.5, 0.9), segs=12, rings=8))
        for s_ in (-1, 1):
            for z in (0.105, 0.135):
                o.append(box("leg", (0.035, 0.008, 0.006), (s_ * 0.045, -0.02, z), c, rot=Matrix.Rotation(math.radians(s_ * 20), 3, "Y")))
    return group(turn(o, Matrix.Rotation(math.radians(45), 4, "Z")), scale=MARKER_SCALE)


# ---------------------------------------------------------------- icons (ADR 0041 §8): rendered on the same rig, framed square

ICON_PX = 256
TO_CAMERA = Vector((0.612, -0.612, 0.5)).normalized()   # from the model towards the rig's camera


def facing_camera(tilt=0.35):
    """A rotation that turns a model's +z (a coin's face) towards the camera, then tips it back a little to show its edge."""
    q = Vector((0, 0, 1)).rotation_difference(TO_CAMERA)
    side = TO_CAMERA.cross(Vector((0, 0, 1))).normalized()
    return Matrix.Rotation(-tilt, 4, side) @ q.to_matrix().to_4x4()


def turn(objs, m):
    for o in objs:
        o.matrix_world = m @ o.matrix_world
    return objs


def icon_coin():
    gold, dark, light = toon("coin", 0xFFC93C), toon("coinDark", 0xE0A21C), toon("coinLight", 0xFFE07A)
    o = [cylinder("coin", 0.10, 0.10, 0.024, (0, 0, 0), gold, segs=32),
         cylinder("rim", 0.084, 0.084, 0.026, (0, 0, 0.0), dark, segs=32, outline=False),
         cylinder("face", 0.076, 0.076, 0.028, (0, 0, 0.0), light, segs=32, outline=False)]
    # A wheat ear embossed on the face.
    for i in range(5):
        for s in (-1, 1) if i < 4 else (0,):
            o.append(sphere("grain%d%d" % (i, s), 0.010, (s * 0.009, -0.032 + i * 0.016, 0.015), dark, scale=(0.75, 1.3, 0.4), segs=8, rings=5, outline=False))
    o.append(box("stem", (0.005, 0.05, 0.004), (0, -0.05, 0.015), dark, outline=False))
    return turn(o, facing_camera())


def icon_wheat():
    gold, ear, tie = toon("wheatGold", 0xE6C266), toon("wheatEar", 0xD9A53A), toon("tie", 0xB4572E)
    o = []
    for i in range(7):
        a = (i - 3) * 0.17
        m = Matrix.Rotation(a, 3, "Y")
        top = m @ Vector((0, 0, 0.20))
        o.append(cylinder("stalk%d" % i, 0.006, 0.006, 0.20, tuple(m @ Vector((0, 0, 0.10))), gold, segs=6, rot=m))
        o.append(sphere("ear%d" % i, 0.022, tuple(top + m @ Vector((0, 0, 0.035))), ear, scale=(0.7, 0.7, 2.2), segs=10, rings=7))
    o.append(cylinder("tie", 0.03, 0.03, 0.026, (0, 0, 0.085), tie, segs=12))
    # The sheaf stands in the x-z plane: turn that plane to face the camera.
    return turn(o, Matrix.Rotation(math.radians(45), 4, "Z"))


def icon_gear():
    steel, dark = toon("steel", 0xC9C2B4), toon("steelDark", 0x8C8478)
    o = [cylinder("disc", 0.085, 0.085, 0.04, (0, 0, 0), steel, segs=32),
         cylinder("hub", 0.034, 0.034, 0.046, (0, 0, 0), dark, segs=20, outline=False),
         cylinder("hole", 0.016, 0.016, 0.05, (0, 0, 0), toon("steelHole", 0x5E574E), segs=16, outline=False)]
    for i in range(8):
        a = i * math.tau / 8
        o.append(box("tooth%d" % i, (0.04, 0.034, 0.04), (math.cos(a) * 0.095, math.sin(a) * 0.095, 0), steel, rot_z=a, bevel=0.004))
    return turn(o, facing_camera(0.3))


SPRITES.update({
    "ground_grass": lambda: model_ground("grass"),
    "ground_soil": lambda: model_ground("soil"),
    "ground_earth": lambda: model_ground("earth"),
    "ground_water": lambda: model_ground("water"),
    "grass_detail_0": lambda: model_grass_detail(0),
    "grass_detail_1": lambda: model_grass_detail(1),
    "grass_detail_2": lambda: model_grass_detail(2),
})
for _c in ("wheat", "radish", "rice", "potato", "lentils"):
    for _st in STAGES:
        SPRITES["crop_%s_%s" % (_c, _st)] = (lambda c=_c, st=_st: (lambda: model_crop(c, st)))()
for _st in ("sapling", "young", "mature", "ripe"):
    SPRITES["tree_mango_%s" % _st] = (lambda st=_st: (lambda: model_mango(st)))()
# ---------------------------------------------------------------- the rest of the icons (ADR 0041 §8)

def star_mesh(name, r_out, r_in, depth, mat, loc=(0, 0, 0), points=5, **kw):
    """An extruded star in the x-y plane."""
    verts = []
    for i in range(points * 2):
        a = math.pi / 2 + i * math.pi / points
        r = r_out if i % 2 == 0 else r_in
        verts.append((math.cos(a) * r, math.sin(a) * r))
    n = len(verts)
    v3 = [(x + loc[0], y + loc[1], loc[2] - depth / 2) for x, y in verts] + [(x + loc[0], y + loc[1], loc[2] + depth / 2) for x, y in verts]
    faces = [tuple(range(n - 1, -1, -1)), tuple(range(n, 2 * n))] + [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
    return poly_mesh(name, v3, faces, mat, **kw)


def bowl(o, contents_hex, grain=0.009, n=70, seed=3):
    """A deep brass katori heaped above its rim with grain or dal: deep enough to read as a bowl, not a pie."""
    import random
    rnd = random.Random(seed)
    brass, brass_dark = toon("brass", 0xD9A93C), toon("brassDark", 0xB8862A)
    o.append(cylinder("bowl", 0.062, 0.10, 0.075, (0, 0, 0.0525), brass, segs=24, smooth=True))   # narrow at the foot, wide at the rim
    o.append(sphere("belly", 0.064, (0, 0, 0.022), brass, scale=(1, 1, 0.45), segs=20, rings=8))
    o.append(cylinder("foot", 0.042, 0.046, 0.014, (0, 0, 0.004), brass_dark, segs=20))
    o.append(cylinder("rim", 0.098, 0.098, 0.008, (0, 0, 0.09), brass_dark, segs=24, outline=False))
    o.append(sphere("heap", 0.094, (0, 0, 0.088), toon("heap", contents_hex), scale=(1, 1, 0.62), segs=20, rings=10))
    for i in range(n):
        a, r = rnd.uniform(0, math.tau), 0.088 * math.sqrt(rnd.uniform(0, 1))
        z = 0.088 + 0.058 * math.sqrt(max(0.0, 1 - (r / 0.094) ** 2))
        o.append(sphere("grain%d" % i, grain, (math.cos(a) * r, math.sin(a) * r, z), toon("grain%d" % (i % 2), (contents_hex, shade(contents_hex, 0.86))[i % 2]),
                        scale=(1.35, 0.9, 0.7), segs=6, rings=4, outline=False))
    return o


def shade(hexv, f):
    """A colour darkened by the factor f."""
    return (int(((hexv >> 16) & 255) * f) << 16) | (int(((hexv >> 8) & 255) * f) << 8) | int((hexv & 255) * f)


def leaf_obj(name, loc, length, width, direction, tilt, mat):
    b = Batch(name)
    leaf(b, mat, loc[0], loc[1], loc[2], length, width, direction, tilt)
    return b.objects(outline_for=(mat,))


def icon_good(good):
    o = []
    green, deep = toon("leafG", 0x5DAA45), toon("leafD", 0x3F8E3A)
    if good == "radish":
        o.append(cylinder("root", 0.03, 0.006, 0.20, (0, 0, -0.06), toon("mooli", 0xF4F1E6), segs=14, smooth=True, rot=Matrix.Rotation(math.radians(180), 3, "X")))
        o.append(cylinder("shoulder", 0.031, 0.03, 0.03, (0, 0, 0.035), toon("mooliTop", 0xA8CF6A), segs=14, smooth=True))
        for i in range(5):
            o += leaf_obj("l%d" % i, (0, 0, 0.05), 0.12, 0.05, i * math.tau / 5, 1.1, green if i % 2 else deep)
    elif good == "wheat":
        return icon_wheat()
    elif good == "rice":
        bowl(o, 0xF4F0E2, grain=0.008)
    elif good == "lentils":
        bowl(o, 0xE07A3A, grain=0.009, seed=5)
    elif good == "atta":
        # A steel paraat heaped with fine whole-wheat flour (spec 016).
        o.append(cylinder("paraat", 0.12, 0.10, 0.03, (0, 0, 0.015), toon("steel", 0xC9CDD2), segs=24, smooth=True))
        o.append(dome("heap", 0.10, 0.07, (0, 0, 0.028), toon("atta", 0xEADBBE)))
    elif good == "bran":
        # Chokar: coarse brown flakes in a jute sack's open mouth.
        o.append(cylinder("sackBody", 0.09, 0.10, 0.12, (0, 0, 0.06), toon("sack", 0xC7A06A), segs=16, smooth=True))
        o.append(dome("chokar", 0.088, 0.04, (0, 0, 0.12), toon("chokar", 0xA8743E)))
        o.append(cylinder("rim", 0.092, 0.092, 0.012, (0, 0, 0.12), toon("jute", 0x9C7A4C), segs=16, outline=False))
    elif good == "potato":
        for i, (x, y, z) in enumerate(((-0.05, 0.0, 0.04), (0.05, 0.01, 0.04), (0.0, -0.02, 0.10))):
            o.append(sphere("potato%d" % i, 0.06, (x, y, z), toon("potato", 0xC59A5A), scale=(1.25, 1, 0.85), segs=14, rings=9))
            o.append(sphere("eye%d" % i, 0.006, (x + 0.02, y - 0.05, z + 0.01), toon("potatoEye", 0x8C6A3A), segs=6, rings=4, outline=False))
    elif good in ("spinach", "coriander"):
        # A market bunch (gaddi): stems gathered and tied, leaves opening upward all round, not a flat fan.
        import random
        rnd = random.Random(31)
        stem_mat = toon("stem", 0x6FA03A)
        if good == "spinach":
            mats, n, L, W = (toon("spinach", 0x2E8E45), toon("spinach2", 0x3FA455)), 11, 0.15, 0.085
        else:
            mats, n, L, W = (toon("dhania", 0x86D45E), toon("dhaniaMid", 0x68B848)), 13, 0.0, 0.0
            for i in range(5):  # coriander comes to market with its roots on
                ra = i * math.tau / 5
                o.append(cylinder("root%d" % i, 0.004, 0.002, 0.05, (math.cos(ra) * 0.006, math.sin(ra) * 0.006, -0.02), toon("root", 0xD9C7A0),
                                  segs=4, rot=Matrix.Rotation(0.4, 3, Vector((-math.sin(ra), math.cos(ra), 0)))))
        b = Batch("bunch")
        for i in range(n):
            ga = i * 2.39996 + rnd.uniform(-0.2, 0.2)          # golden-angle spread round the bunch
            tilt = rnd.uniform(1.0, 1.35)                       # steeply up, a little open
            d = Vector((math.cos(ga) * math.cos(tilt), math.sin(ga) * math.cos(tilt), math.sin(tilt)))
            stem_len = rnd.uniform(0.07, 0.10)
            b.cone(stem_mat, 0.0045, 0.003, stem_len, d.to_track_quat("Z", "Y").to_matrix().to_4x4(), segs=4)
            tip = d * stem_len
            if good == "spinach":   # broad, spoon-shaped blades
                leaf(b, mats[i % 2], tip.x, tip.y, tip.z, L * rnd.uniform(0.85, 1.1), W, ga, tilt - 0.15, rot=rnd.uniform(-0.3, 0.3))
            else:                   # lacy sprigs: clusters of small round leaflets
                for q in range(8):
                    t = tip + d * (q * 0.008) + Vector((math.cos(ga + q * 1.3), math.sin(ga + q * 1.3), 0)) * rnd.uniform(0.008, 0.02)
                    b.sphere(mats[(i + q) % 2], 0.0105, Matrix.Translation(t) @ Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 1)).to_track_quat("Z", "Y").to_matrix().to_4x4(),
                             scale=(1.15, 0.9, 0.35), segs=9, rings=4)
        o += b.objects(outline_for=tuple(m for m in b.parts if m is not stem_mat), thickness=OUTLINE * 0.8)
        o.append(cylinder("tie", 0.017, 0.017, 0.016, (0, 0, 0.022), toon("tie", 0xB4572E), segs=12))
        return o
    elif good == "banana":
        for i in range(4):
            a = (i - 1.5) * 0.35
            m = Matrix.Rotation(a, 3, "Y") @ Matrix.Rotation(math.radians(-25), 3, "X")
            o.append(sphere("banana%d" % i, 0.03, tuple(m @ Vector((0, 0, 0.09))), toon("banana", 0xF2D84A), scale=(0.75, 0.75, 3.2), segs=10, rings=8))
        o.append(cylinder("crown", 0.02, 0.016, 0.04, (0, 0, 0.0), toon("bananaStalk", 0x8FA84A), segs=8))
        return turn(o, Matrix.Rotation(math.radians(45), 4, "Z"))
    elif good in ("papaya", "guava", "mango"):
        hexes = {"papaya": (0xF2A23A, (0.9, 0.9, 1.35), 0.07), "guava": (0xD8E07A, (1, 1, 1.05), 0.06), "mango": (0xF4B33C, (0.8, 0.95, 1.2), 0.065)}
        col, sc, r = hexes[good]
        o.append(sphere("fruit", r, (0, 0, r), toon(good, col), scale=sc, segs=18, rings=12))
        if good == "mango":
            o.append(sphere("blush", r * 0.6, (0.02, -0.035, r * 1.25), toon("mangoBlush", 0xE8723A), scale=(0.6, 0.4, 0.5), segs=10, rings=6, outline=False))
        if good == "papaya":
            o.append(sphere("green", r * 0.5, (0, -0.02, r * 1.9), toon("papayaGreen", 0x9CC24A), scale=(0.9, 0.9, 0.5), segs=10, rings=6, outline=False))
        o.append(cylinder("stalk", 0.005, 0.004, 0.04, (0, 0, r * 2.3), toon("stalk", 0x7A4E2E), segs=6))
        o += leaf_obj("leaf", (0, 0, r * 2.35), 0.10, 0.045, math.radians(-30), 0.25, green)
    return o


def icon_input(kind):
    o = []
    if kind == "seed":
        o.append(sphere("bag", 0.085, (0, 0, 0.08), toon("cloth", 0xE8D9B0), scale=(1, 1, 1.05), segs=16, rings=10))
        o.append(cylinder("neck", 0.03, 0.045, 0.05, (0, 0, 0.17), toon("cloth", 0xE8D9B0), segs=14, smooth=True))
        o.append(cylinder("tie", 0.034, 0.034, 0.014, (0, 0, 0.16), toon("tie", 0xB4572E), segs=14))
        import random
        rnd = random.Random(2)
        for i in range(9):
            o.append(sphere("seed%d" % i, 0.009, (rnd.uniform(-0.09, 0.09), rnd.uniform(-0.12, -0.06), 0.008), toon("seed", 0xC58A3C), scale=(1.3, 0.9, 0.7), segs=6, rings=4, outline=False))
    elif kind == "sapling":
        o.append(cylinder("pot", 0.06, 0.045, 0.08, (0, 0, 0.04), toon("clay", 0xB4572E), segs=16, smooth=True))
        o.append(cylinder("potRim", 0.066, 0.066, 0.014, (0, 0, 0.08), toon("clayDark", 0x934424), segs=16))
        o.append(cylinder("stem", 0.006, 0.005, 0.14, (0, 0, 0.15), toon("bark", 0x7A4E2E), segs=6))
        for i in range(5):
            o += leaf_obj("l%d" % i, (0, 0, 0.13 + i * 0.022), 0.07, 0.035, i * 2.4, 0.35, toon("leafG", 0x5DAA45) if i % 2 else toon("leafD", 0x3F8E3A))
    elif kind == "compost":
        # A cane basket (tokri) heaped high with crumbly dark compost, a few bits of straw in it: a mound, not a flat disc.
        import random
        rnd = random.Random(41)
        cane, cane_dark = toon("cane", 0xC08F48), toon("caneDark", 0x9A6E32)
        o.append(cylinder("basket", 0.07, 0.10, 0.08, (0, 0, 0.04), cane, segs=20, smooth=True))
        for i in range(3):   # woven bands
            r = 0.073 + 0.0095 * (2 * i + 1)
            o.append(cylinder("band%d" % i, r, r + 0.003, 0.008, (0, 0, 0.012 + i * 0.026), cane_dark, segs=20, outline=False))
        o.append(cylinder("rim", 0.103, 0.103, 0.012, (0, 0, 0.08), cane_dark, segs=20))
        o.append(sphere("heap", 0.095, (0, 0, 0.08), toon("compost", 0x4E3422), scale=(1, 1, 0.75), segs=18, rings=9))
        clods = (toon("compost1", 0x3E2818), toon("compost2", 0x6A4A30))
        for i in range(26):
            a, r = rnd.uniform(0, math.tau), 0.085 * math.sqrt(rnd.uniform(0, 1))
            z = 0.08 + 0.071 * math.sqrt(max(0.0, 1 - (r / 0.095) ** 2))
            o.append(sphere("clod%d" % i, rnd.uniform(0.010, 0.017), (math.cos(a) * r, math.sin(a) * r, z), clods[i % 2], scale=(1.2, 1, 0.7), segs=6, rings=4, outline=False))
        for i in range(6):
            a = rnd.uniform(0, math.tau)
            o.append(cylinder("straw%d" % i, 0.003, 0.003, 0.07, (math.cos(a) * 0.04, math.sin(a) * 0.04, 0.13), toon("straw", 0xE2C26A), segs=4, outline=False,
                              rot=Matrix.Rotation(1.25, 3, Vector((-math.sin(a), math.cos(a), 0)))))
    elif kind == "fertiliser":
        o.append(box("bag", (0.15, 0.08, 0.20), (0, 0, 0.10), toon("sackWhite", 0xF0EEE6), bevel=0.02))
        o.append(box("band", (0.152, 0.082, 0.04), (0, 0, 0.13), toon("sackBand", 0x3F8E3A), outline=False))
        o.append(box("fold", (0.13, 0.07, 0.02), (0, 0, 0.205), toon("sackWhite", 0xF0EEE6), bevel=0.006))
    elif kind == "spray":
        o.append(cylinder("tank", 0.06, 0.06, 0.16, (0, 0, 0.08), toon("tank", 0x3F7FA6), segs=18, smooth=True))
        o.append(cylinder("cap", 0.03, 0.03, 0.02, (0, 0, 0.17), toon("cap", 0xE0E0E0), segs=12))
        o.append(cylinder("pump", 0.006, 0.006, 0.07, (0, 0, 0.21), toon("steel", 0xC9C2B4), segs=6))
        o.append(box("handle", (0.06, 0.012, 0.012), (0, 0, 0.245), toon("steel", 0xC9C2B4)))
        o.append(cylinder("lance", 0.005, 0.005, 0.18, (0.09, 0, 0.12), toon("steel", 0xC9C2B4), segs=6, rot=Matrix.Rotation(math.radians(35), 3, "Y")))
        return turn(o, Matrix.Rotation(math.radians(45), 4, "Z"))
    return o


def cloud(o, name, mat, dz=0.0, scale=1.0):
    for i, (x, z, r) in enumerate(((-0.07, 0.0, 0.055), (0.0, 0.03, 0.075), (0.075, 0.005, 0.055), (0.0, -0.02, 0.06))):
        o.append(sphere("%s%d" % (name, i), r * scale, (x * scale, 0, (z + dz) * scale), mat, scale=(1, 0.75, 0.9), segs=16, rings=10))


def icon_weather(kind):
    o = []
    sun, rays = toon("sun", 0xFFC93C), toon("ray", 0xFFB020)
    if kind in ("sun", "heatwave"):
        core = sun if kind == "sun" else toon("hot", 0xFF8A3A)
        o.append(sphere("sun", 0.07, (0, 0, 0), core, segs=20, rings=12))
        for i in range(8):
            a = i * math.tau / 8
            o.append(cylinder("ray%d" % i, 0.016, 0.0, 0.05, (math.cos(a) * 0.11, 0, math.sin(a) * 0.11), rays if kind == "sun" else toon("hotRay", 0xE8562E), segs=8,
                              rot=Vector((math.cos(a), 0, math.sin(a))).to_track_quat("Z", "Y").to_matrix()))
        if kind == "heatwave":
            for i in range(3):
                for j in range(4):
                    o.append(sphere("haze%d%d" % (i, j), 0.012, (-0.06 + j * 0.04, -0.03, -0.14 - i * 0.03 + (0.008 if j % 2 else -0.008)), toon("haze", 0xE8562E), scale=(1.8, 0.6, 0.6), segs=8, rings=4, outline=False))
    elif kind == "cloudy":
        o.append(sphere("sunBehind", 0.055, (0.06, 0.04, 0.07), sun, segs=16, rings=10))
        cloud(o, "c", toon("cloud", 0xF4F6F8))
    else:
        dark = kind in ("heavy_rain", "storm")
        if kind != "frost":
            cloud(o, "c", toon("cloudDark" if dark else "cloud", 0x8E99A8 if dark else 0xE6EAEE))
        if kind == "frost":
            ice = toon("ice", 0xBFE6F5)
            for i in range(6):
                a = i * math.pi / 3
                m = Matrix.Rotation(a, 3, "Y")
                o.append(box("arm%d" % i, (0.018, 0.018, 0.16), tuple(m @ Vector((0, 0, 0.08))), ice, rot=m))
                for sgn in (-1, 1):
                    mm = m @ Matrix.Rotation(sgn * math.radians(40), 3, "Y")
                    o.append(box("twig%d%d" % (i, sgn), (0.012, 0.012, 0.05), tuple(m @ Vector((0, 0, 0.11)) + mm @ Vector((0, 0, 0.02))), ice, rot=mm))
        elif kind == "storm":
            bolt = toon("bolt", 0xFFD43A)
            verts = [(-0.01, 0, -0.04), (0.035, 0, -0.04), (0.005, 0, -0.10), (0.03, 0, -0.10), (-0.03, 0, -0.19), (-0.005, 0, -0.12), (-0.03, 0, -0.12)]
            v3 = [(x, -0.012, z) for x, _, z in verts] + [(x, 0.012, z) for x, _, z in verts]
            n = len(verts)
            faces = [tuple(range(n)), tuple(range(2 * n - 1, n - 1, -1))] + [(i, n + i, n + (i + 1) % n, (i + 1) % n) for i in range(n)]
            o.append(poly_mesh("bolt", v3, faces, bolt))
        else:
            drop = toon("drop", 0x4FA3D9)
            for i in range(5 if kind == "heavy_rain" else 3):
                x = -0.07 + i * (0.14 / (4 if kind == "heavy_rain" else 2))
                o.append(sphere("drop%d" % i, 0.013, (x, -0.02, -0.11 - (i % 2) * 0.03), drop, scale=(0.8, 0.8, 1.5), segs=10, rings=6))
    return turn(o, Matrix.Rotation(math.radians(45), 4, "Z"))


LEVEL_DEG = 45  # turns a flat icon so it sits level after facing the camera


def icon_ui(kind):
    o = []
    if kind == "sack":
        o.append(sphere("sack", 0.09, (0, 0, 0.085), toon("jute", 0xC9A46A), scale=(1, 0.9, 1.1), segs=16, rings=10))
        o.append(cylinder("neck", 0.03, 0.05, 0.06, (0, 0, 0.19), toon("jute", 0xC9A46A), segs=14, smooth=True))
        o.append(cylinder("tie", 0.034, 0.034, 0.014, (0, 0, 0.18), toon("tie", 0xB4572E), segs=14))
    elif kind == "basket":
        o.append(cylinder("basket", 0.11, 0.08, 0.08, (0, 0, 0.04), toon("cane", 0xB8873E), segs=20, smooth=True))
        o.append(cylinder("rim", 0.112, 0.112, 0.012, (0, 0, 0.08), toon("caneDark", 0x8C6228), segs=20, outline=False))
        for i, (x, y, col) in enumerate(((-0.04, 0.0, 0xE9506A), (0.04, 0.01, 0xFFB627), (0.0, -0.03, 0x58BF4A))):
            o.append(sphere("veg%d" % i, 0.04, (x, y, 0.1), toon("veg%d" % i, col), segs=12, rings=8))
    elif kind.startswith("star"):
        fill = {"star_2": 0xFFC93C, "star_1": 0xFFE7A0, "star_0": 0xD9D2C4}[kind]
        o.append(star_mesh("star", 0.11, 0.048, 0.03, toon(kind, fill)))
        return turn(o, facing_camera(0.15))
    elif kind == "letter":
        # Dadi's letter: a cream envelope, its flap edges in a darker line, a red wax seal; it faces the camera like the coin.
        paper, edge, wax = toon("paper", 0xFFF3D1), toon("paperEdge", 0xC9A46A), toon("wax", 0xC0392B)
        o.append(box("envelope", (0.22, 0.15, 0.014), (0, 0, 0), paper, bevel=0.004))
        for sgn in (-1, 1):
            o.append(box("flap%d" % sgn, (0.13, 0.008, 0.004), (sgn * 0.052, 0.034, 0.009), edge,
                         rot=Matrix.Rotation(sgn * math.radians(33), 3, "Z"), outline=False))
        o.append(cylinder("seal", 0.026, 0.026, 0.012, (0, 0.0, 0.012), wax, segs=18))
        o.append(cylinder("sealMark", 0.012, 0.012, 0.014, (0, 0.0, 0.013), toon("waxDark", 0x8F2B1E), segs=12, outline=False))
        return turn(turn(o, Matrix.Rotation(math.radians(LEVEL_DEG), 4, "Z")), facing_camera(0.25))
    elif kind == "bell":
        # The alerts: a brass bell with a knob and a clapper.
        brass, dark = toon("brass", 0xE6B33A), toon("brassDark", 0xA8781E)
        o.append(cylinder("skirt", 0.11, 0.075, 0.05, (0, 0, 0.03), brass, segs=28, smooth=True))
        o.append(cylinder("waist", 0.075, 0.05, 0.07, (0, 0, 0.09), brass, segs=28, smooth=True))
        o.append(sphere("crown", 0.051, (0, 0, 0.125), brass, scale=(1, 1, 0.75), segs=18, rings=10))
        o.append(cylinder("lip", 0.115, 0.115, 0.014, (0, 0, 0.006), dark, segs=28))
        o.append(cylinder("loop", 0.02, 0.02, 0.03, (0, 0, 0.17), dark, segs=12))
        o.append(sphere("clapper", 0.026, (0.02, 0, -0.012), dark, segs=10, rings=6))
        return turn(o, Matrix.Rotation(math.radians(-14), 4, "X"))
    elif kind == "cart":
        # The mandi trip: a tractor-trolley (ADR 0037) loaded with sacks, two wheels and a drawbar.
        wood, dark, tyre, jute = toon("trolley", 0x3F7FBF), toon("trolleyDark", 0x2B5A8A), toon("tyre", 0x3A3530), toon("jute", 0xC9A46A)
        o.append(box("bed", (0.26, 0.15, 0.03), (0, 0, 0.07), dark, bevel=0.004))
        for y in (-0.075, 0.075):
            o.append(box("side%d" % (y > 0), (0.26, 0.012, 0.06), (0, y, 0.1), wood, bevel=0.003))
        for x in (-0.13, 0.13):
            o.append(box("end%d" % (x > 0), (0.012, 0.15, 0.06), (x, 0, 0.1), wood, bevel=0.003))
        for y in (-0.09, 0.09):
            o.append(cylinder("wheel%d" % (y > 0), 0.055, 0.055, 0.03, (0, y, 0.055), tyre, segs=20, rot=Matrix.Rotation(math.radians(90), 3, "X")))
            o.append(cylinder("hub%d" % (y > 0), 0.02, 0.02, 0.034, (0, y, 0.055), toon("hubMetal", 0xC9C2B4), segs=12, rot=Matrix.Rotation(math.radians(90), 3, "X"), outline=False))
        o.append(box("drawbar", (0.12, 0.016, 0.016), (0.19, 0, 0.065), dark))
        for i, (x, y) in enumerate(((-0.06, -0.03), (0.05, 0.03), (-0.01, 0.0))):
            o.append(sphere("sack%d" % i, 0.05, (x, y, 0.15 + (0.03 if i == 2 else 0)), jute, scale=(1.1, 0.9, 0.8), segs=12, rings=8))
        return turn(o, Matrix.Rotation(math.radians(20), 4, "Z"))
    elif kind == "menu":
        # The Menu: a red bahi-khata (the shopkeeper's account book) tied with a gold cord, lying open a little.
        red, pages, cord = toon("khata", 0xB8322A), toon("pages", 0xFFF3D1), toon("cord", 0xE6B33A)
        o.append(box("cover", (0.17, 0.22, 0.018), (0, 0, 0), red, bevel=0.005))
        o.append(box("pages", (0.155, 0.205, 0.022), (0.006, 0, 0.014), pages, bevel=0.003))
        o.append(box("top", (0.17, 0.22, 0.012), (0, 0, 0.03), red, bevel=0.005))
        o.append(box("cord", (0.18, 0.012, 0.046), (0, 0.02, 0.016), cord, outline=False))
        o.append(box("label", (0.07, 0.045, 0.003), (0, -0.05, 0.037), pages, outline=False))
        return turn(turn(o, Matrix.Rotation(math.radians(LEVEL_DEG), 4, "Z")), facing_camera(0.3))
    elif kind == "orders":
        # The supermarket's orders: a kraft paper shop bag with a green band, greens and a radish poking out.
        kraft, band, green = toon("kraft", 0xC8955A), toon("shopBand", 0x2F9E5B), toon("leaf", 0x58BF4A)
        o.append(box("bag", (0.16, 0.10, 0.18), (0, 0, 0.09), kraft, bevel=0.006))
        o.append(box("band", (0.162, 0.102, 0.04), (0, 0, 0.10), band, outline=False))
        o.append(box("fold", (0.165, 0.105, 0.012), (0, 0, 0.18), toon("kraftDark", 0xA8743E), outline=False))
        for i, (x, y) in enumerate(((-0.04, 0.0), (0.0, 0.01), (0.04, -0.005))):
            leafy = leaf_obj("greens%d" % i, (x, y, 0.19), 0.09, 0.035, math.radians(90 + (i - 1) * 25), math.radians(70), green)
            o.extend(leafy if isinstance(leafy, list) else [leafy])
        o.append(sphere("radish", 0.03, (0.035, 0.0, 0.2), toon("radishRed", 0xE9506A), scale=(1, 1, 1.2), segs=12, rings=8))
        return turn(o, Matrix.Rotation(math.radians(20), 4, "Z"))
    elif kind == "close":
        white = toon("x", 0xFFF3D1)
        for a in (45, -45):
            o.append(box("bar%d" % a, (0.03, 0.03, 0.2), (0, 0, 0), white, rot=Matrix.Rotation(math.radians(a), 3, "Y"), bevel=0.008))
        return turn(o, Matrix.Rotation(math.radians(45), 4, "Z"))
    return o


SPRITES["bed_empty"] = lambda: model_bed(None, None)
for _c in GARDEN:
    for _st in STAGES:
        SPRITES["bed_%s_%s" % (_c, _st)] = (lambda c=_c, st=_st: (lambda: model_bed(c, st)))()
for _t, _f in (("banana", model_banana), ("papaya", model_papaya), ("guava", model_guava)):
    for _st in ("sapling", "young", "mature", "ripe"):
        SPRITES["tree_%s_%s" % (_t, _st)] = (lambda f=_f, st=_st: (lambda: f(st)))()
SPRITES.update({"tree_pit": model_pit, "sign_sale": model_sign,
                "marker_ready": lambda: model_marker("ready"), "marker_spoil": lambda: model_marker("spoil"), "marker_pest": lambda: model_marker("pest")})
ICONS = {"icon_coin": icon_coin, "icon_gear": icon_gear}
for _g in ("radish", "wheat", "rice", "potato", "lentils", "spinach", "coriander", "banana", "papaya", "guava", "mango", "atta", "bran"):
    ICONS["icon_good_" + _g] = (lambda g=_g: (lambda: icon_good(g)))()
for _i in ("seed", "sapling", "compost", "fertiliser", "spray"):
    ICONS["icon_" + _i] = (lambda i=_i: (lambda: icon_input(i)))()
for _w in ("sun", "cloudy", "rain", "heavy_rain", "heatwave", "frost", "storm"):
    ICONS["icon_weather_" + _w] = (lambda w=_w: (lambda: icon_weather(w)))()
for _u in ("sack", "basket", "star_0", "star_1", "star_2", "close", "letter", "bell", "cart", "menu", "orders"):
    ICONS["icon_" + _u] = (lambda u=_u: (lambda: icon_ui(u)))()
SPRITES.update(ICONS)


# ---------------------------------------------------------------- the village and the valley (spec 010, ADR 0052, 0053)

ROAD, ROAD_RUT, VERGE = 0xC9A574, 0xB08A5A, 0x7FA843
LIME, LIME_SHADE = 0xF2EBDC, 0xD8CDB8
SAFFRON = 0xE8892A


def model_road(links):
    """A road acre: a grass verge, a dirt road from the middle to each linked edge (1 = +x, 2 = -x, 4 = +y, 8 = -y), wheel ruts."""
    o = ground_block("road", GRASS, GRASS_EDGE)
    b = Batch("road")
    dirt, rut = toon("road", ROAD), toon("rut", ROAD_RUT)
    half = 0.5 + SEAM
    w = 0.40
    b.box(dirt, (w, w, 0.004), Matrix.Translation((0, 0, 0.003)))
    for bit, (dx, dy) in ((1, (1, 0)), (2, (-1, 0)), (4, (0, 1)), (8, (0, -1))):
        if not links & bit:
            continue
        length = half
        cx, cy = dx * length / 2, dy * length / 2
        size = (length, w, 0.004) if dx else (w, length, 0.004)
        b.box(dirt, size, Matrix.Translation((cx, cy, 0.003)))
        for k in (-0.09, 0.09):
            rs = (length, 0.035, 0.002) if dx else (0.035, length, 0.002)
            b.box(rut, rs, Matrix.Translation((cx + (0 if dx else k), cy + (k if dx else 0), 0.0055)))
    # A ditch line and a couple of verge stones along one side.
    stones = toon("vergeStone", 0xC9C2B4)
    for i, (x, y) in enumerate(((0.36, 0.36), (-0.36, -0.36), (0.36, -0.36))):
        b.sphere(stones, 0.02, Matrix.Translation((x, y, 0.008)), scale=(1.2, 1, 0.6), segs=8, rings=5)
    return o + b.objects()


def model_flat(kind):
    """An edge tile past the map: a flat top with no sides, so it fades into the meadow behind it (spec 010 §3.6)."""
    top = {"grass": GRASS, "water": WATER_TOP, "soil": SOIL_GROOVE, "earth": EARTH, "road": GRASS}[kind]
    s = 1.0 + SEAM
    o = [box("flat" + kind, (s, s, 0.004), (0, 0, -0.002), toon("flat" + kind, top), outline=False)]
    if kind == "water":
        b = Batch("ripples")
        ripple = toon("ripple", 0xB5E0EA)
        for i, (x, y) in enumerate(((-0.25, -0.2), (0.15, 0.1), (0.3, -0.3), (-0.1, 0.3))):
            b.box(ripple, (0.16, 0.018, 0.002), Matrix.Translation((x, y, 0.001)))
        o += b.objects()
    if kind == "soil":
        b = Batch("ridges")
        soil_ridges(b, top_z=0.0)
        o += b.objects()
    if kind == "road":
        b = Batch("flatRoad")
        b.box(toon("road", ROAD), (s, 0.40, 0.004), Matrix.Translation((0, 0, 0.001)))
        for k in (-0.09, 0.09):
            b.box(toon("rut", ROAD_RUT), (s, 0.035, 0.002), Matrix.Translation((0, k, 0.0035)))
        o += b.objects()
    return o


def model_bank(variant):
    """The river bank (row 0): grass with reeds along the river side; variant 1 has the stone washing steps (a ghat)."""
    o = ground_block("bank", 0x93BC55, GRASS_EDGE)
    b = Batch("reeds")
    reed, tip = toon("reed", 0x6E9A3A), toon("reedTip", 0xA8874A)
    import random
    rnd = random.Random(11 + variant)
    for i in range(26):
        x, y = rnd.uniform(-0.45, 0.45), rnd.uniform(-0.48, -0.30)
        h = rnd.uniform(0.07, 0.13)
        b.cone(reed, 0.008, 0.002, h, at(x, y, 0.0, tilt=rnd.uniform(0, 0.25), tilt_dir=rnd.uniform(0, math.tau)), segs=4)
        if i % 3 == 0:
            b.sphere(tip, 0.009, Matrix.Translation((x, y, h)), scale=(0.7, 0.7, 1.6), segs=6, rings=4)
    o += b.objects()
    if variant == 1:
        st = toon("ghat", STONE)
        for i in range(3):
            o.append(box("step%d" % i, (0.34, 0.08, 0.02), (0.05, -0.30 - i * 0.07, 0.01 - i * 0.012), st, bevel=0.004))
    return o


def model_commons():
    """The grazing commons: shorter, yellower grass, worn paths and a few dung pats where village animals graze."""
    o = ground_block("commons", 0xA3BF5A, 0x93AE4E)
    b = Batch("commons")
    path, pat, tuft = toon("path", 0xC2A878), toon("pat", 0x6B4A2E), toon("dryTuft", 0x8FAE45)
    b.box(path, (0.9, 0.06, 0.003), at(0, 0.05, 0.002, rot_z=math.radians(25)))
    for x, y in ((0.2, -0.2), (-0.25, 0.25), (0.3, 0.3)):
        b.sphere(pat, 0.018, Matrix.Translation((x, y, 0.004)), scale=(1.3, 1, 0.35), segs=8, rings=4)
    import random
    rnd = random.Random(5)
    for i in range(14):
        b.cone(tuft, 0.02, 0.0, 0.035, Matrix.Translation((rnd.uniform(-0.45, 0.45), rnd.uniform(-0.45, 0.45), 0)), segs=5)
    return o + b.objects()


def walls(name, w, d, h, mat, z=0.04):
    return box(name, (w, d, h), (0, 0, z + h / 2), mat)


def model_house(variant):
    """A village house: variant 0 a mud house with a terracotta tile roof, variant 1 a lime-plastered flat roof with a parapet."""
    o = [box("plinth", (0.42, 0.32, 0.04), (0, 0, 0.02), toon("plinth", PLINTH), bevel=0.006)]
    if variant == 0:
        o.append(walls("walls", 0.36, 0.26, 0.16, toon("mud", MUD)))
        o.append(gable_roof("roof", 0.36, 0.26, 0.10, 0.20, toon("tile", TILE), overhang=0.04))
    else:
        o.append(walls("walls", 0.36, 0.26, 0.18, toon("lime", LIME)))
        o.append(box("parapet", (0.37, 0.27, 0.03), (0, 0, 0.235), toon("limeShade", LIME_SHADE)))
        o.append(box("water_tank", (0.07, 0.07, 0.06), (0.1, 0.06, 0.28), toon("tank", 0x3A3A3A)))
    o.append(box("door", (0.07, 0.012, 0.11), (0.06, -0.131, 0.095), toon("door", (DOOR, 0x8C3E3E)[variant])))
    o.append(box("window", (0.06, 0.012, 0.05), (-0.09, -0.131, 0.13), toon("wood_dark", WOOD_DARK)))
    return group(o, scale=BUILDING_SCALE)


def shop(name, wall_hex, board_hex, awning_hex):
    """A one-room village shop: walls, a flat roof, a signboard over the open front and a cloth awning."""
    o = [box("plinth", (0.40, 0.30, 0.04), (0, 0, 0.02), toon("plinth", PLINTH), bevel=0.006),
         walls("walls", 0.34, 0.24, 0.17, toon(name + "wall", wall_hex)),
         box("roof", (0.36, 0.26, 0.02), (0, 0, 0.22), toon("limeShade", LIME_SHADE)),
         box("front", (0.22, 0.012, 0.11), (0.0, -0.121, 0.095), toon("shopDark", 0x4A3424)),
         box("board", (0.30, 0.014, 0.05), (0, -0.128, 0.19), toon(name + "board", board_hex), bevel=0.003)]
    aw = toon(name + "awning", awning_hex)
    o.append(poly_mesh("awning", [(-0.15, -0.125, 0.16), (0.15, -0.125, 0.16), (0.15, -0.21, 0.12), (-0.15, -0.21, 0.12)], [(0, 1, 2, 3)], aw))
    for x in (-0.14, 0.14):
        o.append(box("pole%d" % (x > 0), (0.01, 0.01, 0.12), (x, -0.205, 0.06), toon("wood", WOOD)))
    return o


def model_grocery():
    o = shop("grocery", MUD, 0xF2C94C, 0x2E7DBF)
    jute = toon("jute", 0xC9A86A)
    for i, x in enumerate((-0.10, -0.04, 0.02)):
        o.append(sphere("sack%d" % i, 0.035, (x, -0.17, 0.035), jute, scale=(1, 0.9, 1.2), segs=10, rings=6))
    return group(o, scale=BUILDING_SCALE)


def model_chemist():
    o = shop("chemist", LIME, 0xFFFFFF, 0x3F9A5A)
    green = toon("cross", 0x2F9E55)
    o.append(box("crossV", (0.012, 0.004, 0.04), (0, -0.137, 0.19), green, outline=False))
    o.append(box("crossH", (0.04, 0.004, 0.012), (0, -0.137, 0.19), green, outline=False))
    return group(o, scale=BUILDING_SCALE)


def model_dealer():
    o = shop("dealer", 0xE6D3A8, 0x3F9A5A, 0xD9A93C)
    sack, bag = toon("fertSack", 0xF5F1E6), toon("seedBag", 0x6E9A3A)
    for i in range(3):
        o.append(box("stack%d" % i, (0.12, 0.07, 0.03), (-0.20, -0.08, 0.055 + i * 0.03), sack if i % 2 == 0 else bag, bevel=0.006))
    return group(o, scale=BUILDING_SCALE)


def model_tea_stall():
    """A tea stall: a thatched lean-to over a counter, a kettle on a clay stove and a bench."""
    o = [box("counter", (0.22, 0.10, 0.08), (0, 0.02, 0.04), toon("wood", WOOD)),
         cylinder("stove", 0.03, 0.035, 0.04, (0.06, 0.02, 0.10), toon("clay", 0xB4572E), segs=10),
         sphere("kettle", 0.025, (0.06, 0.02, 0.14), toon("brass", 0xD9A93C), scale=(1, 1, 0.8), segs=10, rings=6),
         box("bench", (0.20, 0.05, 0.015), (0, -0.14, 0.05), toon("wood_dark", WOOD_DARK))]
    for x in (-0.09, 0.09):
        o.append(box("benchLeg%d" % (x > 0), (0.012, 0.04, 0.045), (x, -0.14, 0.022), toon("wood_dark", WOOD_DARK), outline=False))
    for x in (-0.12, 0.12):
        o.append(box("post%d" % (x > 0), (0.014, 0.014, 0.20), (x, 0.08, 0.10), toon("wood", WOOD)))
        o.append(box("front%d" % (x > 0), (0.014, 0.014, 0.15), (x, -0.07, 0.075), toon("wood", WOOD)))
    o.append(poly_mesh("thatch", [(-0.16, 0.11, 0.21), (0.16, 0.11, 0.21), (0.16, -0.11, 0.15), (-0.16, -0.11, 0.15)], [(0, 1, 2, 3)], toon("thatch", THATCH)))
    return group(o, scale=BUILDING_SCALE)


def model_temple():
    """A small village temple: a white plinth and sanctum under a curved shikhara, a saffron flag on top."""
    lime, shade = toon("lime", LIME), toon("limeShade", LIME_SHADE)
    o = [box("plinth", (0.30, 0.30, 0.05), (0, 0, 0.025), toon("stone", STONE), bevel=0.006),
         box("sanctum", (0.18, 0.18, 0.14), (0, 0, 0.12), lime),
         box("door", (0.06, 0.012, 0.09), (0, -0.091, 0.095), toon("templeDoor", 0xB8322A))]
    for i in range(4):
        r = 0.10 - i * 0.022
        o.append(cylinder("tier%d" % i, r, r * 0.85, 0.05, (0, 0, 0.215 + i * 0.045), shade if i % 2 else lime, segs=8))
    o.append(sphere("kalash", 0.018, (0, 0, 0.40), toon("gold", 0xE6B33A), segs=8, rings=6))
    o.append(box("flagpole", (0.006, 0.006, 0.12), (0.02, 0, 0.46), toon("wood", WOOD), outline=False))
    o.append(poly_mesh("flag", [(0.023, 0, 0.52), (0.10, 0, 0.50), (0.023, 0, 0.47)], [(0, 1, 2)], toon("saffron", SAFFRON)))
    return group(o, scale=BUILDING_SCALE)


def model_council_hall():
    """The council hall (panchayat bhavan): a flat-roofed hall with a pillared veranda, steps and a flag."""
    lime, shade = toon("lime", LIME), toon("limeShade", LIME_SHADE)
    o = [box("plinth", (0.46, 0.34, 0.05), (0, 0, 0.025), toon("stone", STONE), bevel=0.006),
         walls("walls", 0.40, 0.20, 0.17, lime, z=0.05),
         box("roof", (0.44, 0.30, 0.025), (0, -0.03, 0.235), shade),
         box("steps", (0.14, 0.06, 0.025), (0, -0.19, 0.012), toon("stone_dark", STONE_DARK))]
    for x in (-0.17, -0.06, 0.06, 0.17):
        o.append(cylinder("pillar%d" % int(x * 100), 0.014, 0.014, 0.17, (x, -0.13, 0.135), lime, segs=8))
    o.append(box("pole", (0.008, 0.008, 0.16), (0.18, 0.05, 0.33), toon("wood", WOOD), outline=False))
    for i, hexv in enumerate((0xFF9933, 0xFFFFFF, 0x138808)):
        o.append(box("band%d" % i, (0.07, 0.003, 0.016), (0.218, 0.05, 0.40 - i * 0.016), toon("flag%d" % i, hexv), outline=False))
    return group(o, scale=BUILDING_SCALE)


def model_school_plot():
    """The empty school plot: a low brick boundary with a gap for the gate and a board on a post."""
    brick = toon("brick", 0xB4572E)
    o = []
    for x, y, w, d in ((0, 0.30, 0.62, 0.03), (0, -0.30, 0.62, 0.03), (-0.30, 0, 0.03, 0.62), (0.30, 0.15, 0.03, 0.30)):
        o.append(box("wall%d%d" % (int(x * 10), int(y * 10)), (w, d, 0.04), (x, y, 0.02), brick))
    o.append(box("post", (0.014, 0.014, 0.16), (0.22, -0.22, 0.08), toon("wood", WOOD)))
    o.append(box("board", (0.12, 0.010, 0.07), (0.22, -0.23, 0.15), toon("signBoard", 0xF2DFB0), bevel=0.003))
    return group(o, scale=BUILDING_SCALE / 1.2)


def model_banyan():
    """The banyan with the threshing floor: a huge spreading canopy, aerial roots, a round platform round the trunk."""
    bark, dark, mid, light = toon("bark", 0x7A4E2E), toon("banyanDark", 0x2E6E33), toon("banyanMid", 0x3C8A3C), toon("banyanLight", 0x5CA84A)
    o = [cylinder("floor", 0.30, 0.30, 0.03, (0, 0, 0.015), toon("floor", 0xD6BC90), segs=24),
         cylinder("platform", 0.10, 0.10, 0.06, (0, 0, 0.03), toon("stone", STONE), segs=16),
         cylinder("trunk", 0.06, 0.045, 0.30, (0, 0, 0.18), bark, segs=10, smooth=True)]
    for i in range(6):
        a = i * math.tau / 6
        o.append(cylinder("root%d" % i, 0.008, 0.008, 0.25, (math.cos(a) * 0.17, math.sin(a) * 0.17, 0.15), bark, segs=5, outline=False))
    for i, (x, y, z, r) in enumerate(((0, 0, 0.42, 0.24), (0.18, 0.05, 0.38, 0.17), (-0.17, 0.08, 0.38, 0.17), (0.04, -0.18, 0.37, 0.17),
                                      (-0.08, 0.18, 0.40, 0.16), (0.0, 0.0, 0.55, 0.15))):
        o.append(sphere("canopy%d" % i, r, (x, y, z), light if z > 0.5 else (dark if i % 2 else mid), scale=(1, 1, 0.7), segs=14, rings=9))
    return group(o, scale=BUILDING_SCALE / 1.3)


def model_pond():
    """The village pond: an oval of water in a stone rim, steps down on the near side."""
    o = [cylinder("rim", 0.34, 0.34, 0.02, (0, 0, 0.01), toon("stone", STONE), segs=28),
         cylinder("water", 0.30, 0.30, 0.01, (0, 0, 0.022), toon("pond", WATER), segs=28, outline=False)]
    for i in range(2):
        o.append(box("step%d" % i, (0.14, 0.05, 0.015), (0.0, -0.31 - i * 0.04, 0.012 - i * 0.006), toon("stone_dark", STONE_DARK)))
    for i, (x, y) in enumerate(((0.1, 0.05), (-0.12, -0.08))):
        o.append(cylinder("lotus%d" % i, 0.04, 0.04, 0.004, (x, y, 0.029), toon("lotusLeaf", 0x4E9A4A), segs=10, outline=False))
    o.append(sphere("lotus", 0.018, (0.1, 0.05, 0.04), toon("lotus", 0xF4A6C0), segs=8, rings=5))
    return o


def model_hill(variant):
    """Hills behind the village (past column 0): a broad grassy mound with rocks and scrub; three sizes."""
    k = (1.0, 1.4, 1.8)[variant]
    grass, dry, rock = toon("hill", 0x7FA34A), toon("hillDry", 0x9DB35C), toon("rock", 0xA8A194)
    o = [sphere("mound", 0.9 * k, (0, 0, 0), grass, scale=(1.0, 0.8, 0.45), segs=20, rings=12),
         sphere("crest", 0.55 * k, (0.15 * k, 0.1 * k, 0.25 * k), dry, scale=(1.0, 0.8, 0.5), segs=16, rings=10)]
    b = Batch("hillScrub")
    bush = toon("hillBush", 0x4E7E34)
    import random
    rnd = random.Random(30 + variant)
    for i in range(9):
        a = rnd.uniform(0, math.tau)
        r = rnd.uniform(0.2, 0.7) * k
        x, y = math.cos(a) * r, math.sin(a) * r * 0.8
        z = 0.45 * k * max(0.0, 1 - (r / (0.9 * k)) ** 2) ** 0.5
        b.sphere(bush, 0.06 * k, Matrix.Translation((x, y, z)), scale=(1, 1, 0.8), segs=8, rings=5)
    for i in range(3):
        b.sphere(rock, 0.07 * k, Matrix.Translation((rnd.uniform(-0.4, 0.4) * k, rnd.uniform(-0.4, 0.4) * k, 0.25 * k)), scale=(1.3, 1, 0.7), segs=7, rings=5)
    return o + b.objects(outline_for=(bush,))


def model_scrub():
    """Scrub on the near edge: a cluster of thorny bushes and a lantana patch."""
    b = Batch("scrub")
    a_, b_, flower = toon("scrubA", 0x5E8A36), toon("scrubB", 0x7A9A40), toon("lantana", 0xF08A3C)
    import random
    rnd = random.Random(21)
    for i in range(7):
        x, y = rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3)
        b.sphere(a_ if i % 2 else b_, rnd.uniform(0.05, 0.08), Matrix.Translation((x, y, 0.04)), scale=(1, 1, 0.75), segs=8, rings=5)
    for i in range(5):
        b.sphere(flower, 0.012, Matrix.Translation((rnd.uniform(-0.25, 0.25), rnd.uniform(-0.25, 0.25), 0.09)), segs=6, rings=4)
    return b.objects(outline_for=(a_, b_))


def model_bund(axis):
    """A field bund (mendh): the raised earth ridge round your own fields, grass on top, one tile edge long along x or y."""
    b = Batch("bund")
    earth, top, tuft = toon("bund", 0xB98A55), toon("bundTop", 0x8CB84E), toon("bundTuft", 0x6FA03A)
    size = (1.0, 0.07, 0.035) if axis == "x" else (0.07, 1.0, 0.035)
    b.box(earth, size, Matrix.Translation((0, 0, 0.0175)))
    b.box(top, (size[0] * 0.98, size[1] * 0.7, 0.006), Matrix.Translation((0, 0, 0.036)))
    for i in range(6):
        u = -0.42 + i * 0.17
        x, y = (u, 0) if axis == "x" else (0, u)
        b.cone(tuft, 0.016, 0.0, 0.03, Matrix.Translation((x, y, 0.035)), segs=5)
    return b.objects(outline_for=(earth,))


SPRITES["bund_x"] = lambda: model_bund("x")
SPRITES["bund_y"] = lambda: model_bund("y")
for _m in (3, 12, 11, 4, 8):
    SPRITES["ground_road_%d" % _m] = (lambda m=_m: (lambda: model_road(m)))()
for _k in ("grass", "water", "soil", "earth", "road"):
    SPRITES["edge_" + _k] = (lambda k=_k: (lambda: model_flat(k)))()
SPRITES.update({
    "ground_bank_0": lambda: model_bank(0), "ground_bank_1": lambda: model_bank(1), "ground_commons": model_commons,
    "place_house_0": lambda: model_house(0), "place_house_1": lambda: model_house(1),
    "place_grocery": model_grocery, "place_chemist": model_chemist, "place_dealer": model_dealer, "place_tea_stall": model_tea_stall,
    "place_temple": model_temple, "place_council_hall": model_council_hall, "place_school_plot": model_school_plot,
    "place_banyan": model_banyan, "place_pond": model_pond,
    "hill_0": lambda: model_hill(0), "hill_1": lambda: model_hill(1), "hill_2": lambda: model_hill(2), "scrub": model_scrub,
})


# ---------------------------------------------------------------- the village redrawn (ADR 0061, US-206)
# Every place in four facings (s, w, e, n: the door towards -y, -x, +x, +y), a little smaller than the farm's buildings so each
# acre keeps a street, a front yard and a back yard; a third, two-storey house; the dairy co-op; streets, ground and props.

VILLAGE_SCALE = 0.8      # times each model's own scale: room round every building (owner, ADR 0061)
FACINGS = {"s": 0, "w": -90, "e": 90, "n": 180}


def refit(objs, rot_deg=0.0, factor=1.0):
    """Turns a built model about its origin and scales it again, keeping the outline's width on screen."""
    m = Matrix.Diagonal((factor, factor, factor, 1.0)) @ Matrix.Rotation(math.radians(rot_deg), 4, "Z")
    for o in objs:
        o.matrix_world = m @ o.matrix_world
        for mod in o.modifiers:
            if mod.type == "SOLIDIFY":
                mod.thickness /= factor
    return objs


def yard_pad(o, w, d, y0, mat_hex=0xCDB287):
    """A packed-earth front yard before the door (-y), flush with the ground."""
    o.append(box("pad", (w, d, 0.006), (0, y0, 0.003), toon("pad", mat_hex), outline=False))


def back_details(o, w, d, z, mat):
    """A window on the back and on each side wall, so a house turned away from the camera still reads as a home."""
    o.append(box("backWin", (0.05, 0.012, 0.045), (-0.04, d / 2 + 0.002, z), mat))
    for sx in (-1, 1):
        o.append(box("sideWin%d" % sx, (0.012, 0.045, 0.045), (sx * (w / 2 + 0.002), 0.0, z), mat))


def model_house_v(variant):
    """A village house: 0 small mud with a tile roof, 1 lime-washed with a flat roof and parapet, 2 a bigger two-storey house
    with a stair up the side; each with a packed-earth front yard and windows all round."""
    o = []
    dark = toon("wood_dark", WOOD_DARK)
    if variant == 2:
        w, d = 0.40, 0.28
        yard_pad(o, 0.40, 0.14, -0.21)
        o.append(box("plinth", (w + 0.04, d + 0.04, 0.04), (0, 0, 0.02), toon("plinth", PLINTH), bevel=0.006))
        o.append(walls("ground", w, d, 0.15, toon("lime", 0xEFE3CB)))
        o.append(box("band", (w + 0.01, d + 0.01, 0.02), (0, 0, 0.2), toon("ochreBand", 0xD39A4A), outline=False))
        o.append(box("upper", (w * 0.62, d, 0.12), (-w * 0.19, 0, 0.27), toon("lime", 0xEFE3CB)))
        o.append(box("parapet", (w + 0.01, d + 0.01, 0.025), (0, 0, 0.215), toon("limeShade", LIME_SHADE)))
        o.append(box("roof2", (w * 0.62 + 0.01, d + 0.01, 0.02), (-w * 0.19, 0, 0.34), toon("limeShade", LIME_SHADE)))
        o.append(box("door", (0.065, 0.012, 0.11), (0.08, -d / 2 - 0.003, 0.095), toon("door", 0x7A3E8C)))
        o.append(box("win1", (0.06, 0.012, 0.05), (-0.09, -d / 2 - 0.003, 0.13), dark))
        o.append(box("win2", (0.06, 0.012, 0.05), (-0.09, -d / 2 - 0.003, 0.28), dark))
        for i in range(5):   # the stair up the right side to the roof terrace
            o.append(box("stair%d" % i, (0.05, 0.03, 0.03), (w / 2 + 0.03, d / 2 - 0.04 - i * 0.035, 0.02 + i * 0.04), toon("stone", STONE)))
        back_details(o, w, d, 0.13, dark)
        o.append(box("tank", (0.06, 0.06, 0.05), (-0.14, 0.06, 0.375), toon("tank", 0x3A3A3A)))
        return refit(group(o, scale=BUILDING_SCALE), 0, VILLAGE_SCALE)
    w, d = (0.32, 0.24) if variant == 0 else (0.36, 0.26)
    yard_pad(o, w, 0.12, -d / 2 - 0.07)
    o.append(box("plinth", (w + 0.05, d + 0.05, 0.04), (0, 0, 0.02), toon("plinth", PLINTH), bevel=0.006))
    if variant == 0:
        o.append(walls("walls", w, d, 0.15, toon("mud", MUD)))
        o.append(gable_roof("roof", w, d, 0.10, 0.19, toon("tile", TILE), overhang=0.04))
    else:
        o.append(walls("walls", w, d, 0.18, toon("lime", LIME)))
        o.append(box("parapet", (w + 0.01, d + 0.01, 0.03), (0, 0, 0.235), toon("limeShade", LIME_SHADE)))
        o.append(box("water_tank", (0.07, 0.07, 0.06), (0.1, 0.06, 0.28), toon("tank", 0x3A3A3A)))
    o.append(box("door", (0.07, 0.012, 0.11), (0.06, -d / 2 - 0.003, 0.095), toon("door", (DOOR, 0x8C3E3E)[variant])))
    o.append(box("window", (0.06, 0.012, 0.05), (-0.08, -d / 2 - 0.003, 0.13), dark))
    back_details(o, w, d, 0.13, dark)
    return refit(group(o, scale=BUILDING_SCALE), 0, VILLAGE_SCALE)


def model_dairy():
    """The village dairy co-op (ADR 0061): a lime-washed collection centre with a blue band and a painted milk can, a raised
    platform where farmers pour their milk, cans waiting, a steel bulk-milk cooler on legs, and a weighing scale."""
    o = []
    yard_pad(o, 0.46, 0.16, -0.24)
    o.append(box("plinth", (0.44, 0.32, 0.05), (0, 0, 0.025), toon("stone", STONE), bevel=0.006))
    o.append(walls("walls", 0.38, 0.24, 0.17, toon("lime", LIME), z=0.05))
    o.append(box("band", (0.385, 0.245, 0.03), (0, 0, 0.17), toon("dairyBlue", 0x2E6FBA), outline=False))
    o.append(box("roof", (0.42, 0.30, 0.025), (0, -0.02, 0.235), toon("limeShade", LIME_SHADE)))
    o.append(box("door", (0.08, 0.012, 0.11), (-0.1, -0.123, 0.105), toon("door", 0x2E6FBA)))
    o.append(box("hatch", (0.10, 0.012, 0.06), (0.06, -0.123, 0.13), toon("wood_dark", WOOD_DARK)))
    o.append(box("counter", (0.14, 0.05, 0.012), (0.06, -0.145, 0.10), toon("concrete", CONCRETE)))
    # The painted can on the board above the hatch.
    o.append(box("board", (0.16, 0.012, 0.05), (0.0, -0.126, 0.21), toon("boardWhite", 0xF7F4EC), outline=False))
    o.append(cylinder("paintCan", 0.012, 0.012, 0.03, (0.0, -0.133, 0.21), toon("alu", 0xA9AFB5), segs=10, rot=Matrix.Rotation(math.radians(90), 3, "X"), outline=False))
    al, ald = toon("alu", 0xD5D9DD), toon("aluDark", 0xA9AFB5)
    for i, (x, y) in enumerate(((0.12, -0.20), (0.16, -0.17), (0.2, -0.205))):   # farmers' cans on the platform
        o.append(cylinder("can%d" % i, 0.018, 0.018, 0.045, (x, y, 0.0275), al, segs=12, smooth=True))
        o.append(cylinder("lid%d" % i, 0.012, 0.012, 0.012, (x, y, 0.056), ald, segs=10))
    # The bulk-milk cooler beside the building.
    o.append(cylinder("cooler", 0.055, 0.055, 0.10, (0.27, 0.06, 0.12), toon("steel", 0xC9CDD2), segs=18, smooth=True, rot=Matrix.Rotation(math.radians(90), 3, "Y")))
    for x in (0.23, 0.31):
        o.append(box("leg%d" % (x > 0.25), (0.01, 0.06, 0.07), (x, 0.06, 0.035), ald))
    o.append(box("scale", (0.04, 0.04, 0.03), (-0.2, -0.18, 0.015), toon("scale", 0x4A4A4A)))
    o.append(box("scaleTop", (0.05, 0.05, 0.006), (-0.2, -0.18, 0.033), ald, outline=False))
    back_details(o, 0.38, 0.24, 0.13, toon("wood_dark", WOOD_DARK))
    return refit(group(o, scale=BUILDING_SCALE), 0, VILLAGE_SCALE)


def model_peepal():
    """The peepal by the temple: a pale, twisting trunk with a broad, airy crown of light leaves and a red thread round it."""
    bark = toon("peepalBark", 0x9C8A70)
    o = [cylinder("trunk", 0.035, 0.026, 0.26, (0, 0, 0.13), bark, segs=10, smooth=True),
         cylinder("thread", 0.037, 0.037, 0.012, (0, 0, 0.07), toon("thread", 0xC0262D), segs=12, outline=False),
         cylinder("platform", 0.09, 0.09, 0.03, (0, 0, 0.015), toon("stone", STONE), segs=14)]
    import random
    rnd = random.Random(17)
    mats = (toon("peepalA", 0x5FA843), toon("peepalB", 0x78BE52), toon("peepalC", 0x4A9038))
    for i in range(14):
        a, r = rnd.uniform(0, math.tau), rnd.uniform(0.02, 0.17)
        z = 0.31 + rnd.uniform(-0.03, 0.07) + (0.17 - r) * 0.3
        o.append(sphere("crown%d" % i, rnd.uniform(0.06, 0.085), (math.cos(a) * r, math.sin(a) * r, z), mats[i % 3], scale=(1, 1, 0.75), segs=12, rings=8))
    return group(o, scale=1.25)


def model_handpump():
    """A green India Mark II handpump on a cement platform with a drain."""
    green = toon("pumpGreen", 0x3F8E5A)
    o = [cylinder("base", 0.06, 0.06, 0.012, (0, 0, 0.006), toon("concrete", CONCRETE), segs=16),
         box("drain", (0.03, 0.08, 0.006), (0, -0.09, 0.004), toon("concrete", CONCRETE), outline=False),
         cylinder("body", 0.016, 0.016, 0.11, (0, 0, 0.065), green, segs=10),
         box("spout", (0.012, 0.04, 0.012), (0, -0.025, 0.08), green),
         box("handle", (0.008, 0.008, 0.09), (0, 0.04, 0.12), toon("iron", 0x6C7278), rot=Matrix.Rotation(math.radians(55), 3, "X"))]
    return group(o, scale=1.3)


def model_charpai():
    """A charpai (rope cot) set out in a yard."""
    wood, rope = toon("wood", WOOD), toon("rope", 0xE8D9B0)
    o = [box("frame", (0.16, 0.08, 0.012), (0, 0, 0.045), wood), box("rope", (0.14, 0.064, 0.006), (0, 0, 0.05), rope, outline=False)]
    for x in (-0.07, 0.07):
        for y in (-0.032, 0.032):
            o.append(box("leg", (0.012, 0.012, 0.045), (x, y, 0.022), wood, outline=False))
    return group(o, rot_z=math.radians(20), scale=1.3)


def model_tethered(kind):
    """A buffalo or cow tethered at a peg in a house's yard, with a small manger."""
    o = model_animal(kind, "eat" if kind == "buffalo" else "stand")
    o.append(cylinder("peg", 0.006, 0.005, 0.05, (0.1, -0.2, 0.025), toon("wood", WOOD), segs=6))
    return o


def model_street(axis):
    """A village street, one acre long: packed earth with wheel ruts, grassy edges, a few stones; along x or y, centred."""
    b = Batch("street")
    dirt, rut, edge = toon("road", ROAD), toon("rut", ROAD_RUT), toon("streetEdge", 0xB89868)
    L = 1.0 + SEAM
    def sz(l, w, h): return (l, w, h) if axis == "x" else (w, l, h)
    b.box(edge, sz(L, 0.26, 0.004), Matrix.Translation((0, 0, 0.002)))
    b.box(dirt, sz(L, 0.22, 0.004), Matrix.Translation((0, 0, 0.003)))
    for k in (-0.05, 0.05):
        b.box(rut, sz(L, 0.022, 0.002), Matrix.Translation((0, k, 0.0055) if axis == "x" else (k, 0, 0.0055)))
    return b.objects()


def model_ground_village(variant):
    """A village acre's ground: grass worn to earth in patches where people walk and sit."""
    o = ground_block("village", 0x8DB24E, GRASS_EDGE)
    import random
    rnd = random.Random(60 + variant)
    b = Batch("worn")
    for i in range(6):
        x, y, r = rnd.uniform(-0.35, 0.35), rnd.uniform(-0.35, 0.35), rnd.uniform(0.06, 0.13)
        b.cone(toon("worn", 0xC9AE84), r, r * 0.95, 0.003, Matrix.Translation((x, y, 0.001)) @ Matrix.Diagonal((1.0, rnd.uniform(0.6, 1.0), 1.0, 1.0)), segs=12)
    tufts = (toon("tuftA", 0x6FA03A), toon("tuftB", 0x8CC152))
    for _ in range(10):
        x, y = rnd.uniform(-0.42, 0.42), rnd.uniform(-0.42, 0.42)
        for k in range(4):
            a = k * math.tau / 4 + rnd.uniform(-0.4, 0.4)
            b.cone(tufts[_ % 2], 0.009, 0.0, rnd.uniform(0.04, 0.06), at(x, y, 0.0, tilt=0.5, tilt_dir=a), segs=3)
    return o + b.objects()


def model_ground_square():
    """The banyan's square: packed earth across the acre with a grass rim."""
    o = ground_block("square", 0x8DB24E, GRASS_EDGE)
    b = Batch("square")
    b.cone(toon("squareEarth", 0xD3B98C), 0.42, 0.42, 0.003, Matrix.Translation((0, 0, 0.001)), segs=24)
    return o + b.objects()


def model_pond_big():
    """The village pond across most of its acre: water in a grassy bank, ghat steps down on the temple's side (-x), lotus."""
    o = [cylinder("bank", 0.44, 0.44, 0.012, (0, 0, 0.006), toon("pondBank", 0xA89A6A), segs=32),
         cylinder("water", 0.39, 0.39, 0.01, (0, 0, 0.012), toon("pond", WATER), segs=32, outline=False)]
    for i in range(3):
        o.append(box("ghat%d" % i, (0.04, 0.22, 0.012), (-0.40 + i * 0.035, 0.0, 0.018 - i * 0.005), toon("stone_dark" if i % 2 else "stone", STONE_DARK if i % 2 else STONE)))
    for i, (x, y) in enumerate(((0.12, 0.08), (-0.1, -0.15), (0.2, -0.12), (-0.02, 0.2))):
        o.append(cylinder("lotus%d" % i, 0.035, 0.035, 0.004, (x, y, 0.019), toon("lotusLeaf", 0x4E9A4A), segs=10, outline=False))
    o.append(sphere("lotus", 0.016, (0.12, 0.08, 0.03), toon("lotus", 0xF4A6C0), segs=8, rings=5))
    o.append(sphere("lotus2", 0.014, (0.2, -0.12, 0.028), toon("lotus", 0xF4A6C0), segs=8, rings=5))
    return o


VILLAGE_PLACES = {"grocery": model_grocery, "chemist": model_chemist, "dealer": model_dealer, "tea_stall": model_tea_stall,
                  "temple": model_temple, "council_hall": model_council_hall, "dairy": model_dairy}
for _f, _deg in FACINGS.items():
    for _v in range(3):
        SPRITES["village_house_%d_%s" % (_v, _f)] = (lambda v=_v, d=_deg: (lambda: refit(model_house_v(v), d)))()
    for _k, _fn in VILLAGE_PLACES.items():
        SPRITES["village_%s_%s" % (_k, _f)] = (lambda fn=_fn, d=_deg, k=_k: (lambda: refit(fn(), d, 1.0 if k == "dairy" else VILLAGE_SCALE)))()
SPRITES.update({"village_peepal": model_peepal, "village_handpump": model_handpump, "village_charpai": model_charpai,
                "village_buffalo": lambda: model_tethered("buffalo"), "village_cow": lambda: model_tethered("cow"),
                "street_x": lambda: model_street("x"), "street_y": lambda: model_street("y"),
                "ground_village_0": lambda: model_ground_village(0), "ground_village_1": lambda: model_ground_village(1),
                "ground_square": model_ground_square, "village_pond": model_pond_big,
                "village_banyan": lambda: refit(model_banyan(), 0, 0.85), "village_tree": lambda: refit(model_neem(), 0, 0.7),
                "village_haystack": lambda: refit(model_haystack(), 0, 0.75)})


# ---------------------------------------------------------------- the trader's truck and the supermarket's van (spec 011 §7)

VEHICLE_SCALE = 1.25


def wheels(o, xs, ys, r, name):
    tyre, hub = toon("tyre", 0x3A3530), toon("hubMetal", 0xC9C2B4)
    for x in xs:
        for y in ys:
            o.append(cylinder("%s%d%d" % (name, x > 0, y > 0), r, r, 0.028, (x, y, r), tyre, segs=18, rot=Matrix.Rotation(math.radians(90), 3, "Y")))
            o.append(cylinder("%shub%d%d" % (name, x > 0, y > 0), r * 0.45, r * 0.45, 0.031, (x, y, r), hub, segs=12, rot=Matrix.Rotation(math.radians(90), 3, "Y"), outline=False))


def model_truck(facing):
    """A small Indian goods truck (about 4 t): a saffron cab with a painted brow, an open wooden body with sacks; built front to -y."""
    cab, cabDark, glass = toon("truckCab", 0xF2A93B), toon("truckCabDark", 0xC77E1E), toon("glass", 0x2E4F6E)
    plank, plankDark, jute = toon("plank", 0x3F7FBF), toon("plankDark", 0x2B5A8A), toon("jute", 0xC9A46A)
    o = [box("chassis", (0.2, 0.52, 0.03), (0, 0.0, 0.065), toon("chassis", 0x3A3530))]
    o.append(box("cab", (0.21, 0.14, 0.15), (0, -0.19, 0.155), cab, bevel=0.01))
    o.append(box("brow", (0.215, 0.05, 0.035), (0, -0.215, 0.245), toon("browGreen", 0x2F9E5B), bevel=0.005))
    o.append(box("windscreen", (0.17, 0.012, 0.06), (0, -0.262, 0.185), glass, outline=False))
    o.append(box("grille", (0.15, 0.012, 0.045), (0, -0.262, 0.11), cabDark, outline=False))
    for x in (-0.07, 0.07):
        o.append(sphere("lamp%d" % (x > 0), 0.014, (x, -0.263, 0.095), toon("lamp", 0xFFF3C4), segs=10, rings=6, outline=False))
    o.append(box("bed", (0.21, 0.33, 0.025), (0, 0.075, 0.095), plankDark))
    for x in (-0.1, 0.1):
        o.append(box("side%d" % (x > 0), (0.012, 0.33, 0.09), (x, 0.075, 0.15), plank, bevel=0.003))
    o.append(box("tail", (0.21, 0.012, 0.09), (0, 0.24, 0.15), plank, bevel=0.003))
    o.append(box("headboard", (0.21, 0.012, 0.12), (0, -0.09, 0.165), plankDark, bevel=0.003))
    for i, (x, y) in enumerate(((-0.05, 0.0), (0.05, 0.03), (-0.04, 0.14), (0.045, 0.16), (0.0, 0.08))):
        o.append(sphere("sack%d" % i, 0.045, (x, y, 0.15 + (0.04 if i == 4 else 0)), jute, scale=(1.0, 1.2, 0.75), segs=12, rings=8))
    wheels(o, (-0.1, 0.1), (-0.17, 0.14), 0.042, "wheel")
    return group(o, rot_z=0.0 if facing == "front" else math.radians(-90), scale=VEHICLE_SCALE)


def model_van(facing):
    """The supermarket's small delivery van: white, a green stripe and a crate sign; built front to -y."""
    body, stripe, glass = toon("vanBody", 0xF4F1EA), toon("vanStripe", 0x2F9E5B), toon("glass", 0x2E4F6E)
    o = [box("body", (0.17, 0.30, 0.15), (0, 0.02, 0.135), body, bevel=0.012)]
    o.append(poly_mesh("nose", [(-0.085, -0.13, 0.06), (0.085, -0.13, 0.06), (0.085, -0.19, 0.06), (-0.085, -0.19, 0.06),
                                (-0.085, -0.13, 0.20), (0.085, -0.13, 0.20), (0.085, -0.16, 0.14), (-0.085, -0.16, 0.14)],
                       [(0, 1, 2, 3), (4, 5, 1, 0), (3, 2, 6, 7), (7, 6, 5, 4), (0, 3, 7, 4), (1, 5, 6, 2)], body))
    o.append(box("windscreen", (0.15, 0.01, 0.05), (0, -0.147, 0.17), glass, outline=False, rot=Matrix.Rotation(math.radians(-35), 3, "X")))
    for x in (-0.086, 0.086):
        o.append(box("stripe%d" % (x > 0), (0.004, 0.30, 0.03), (x, 0.02, 0.13), stripe, outline=False))
        o.append(box("sign%d" % (x > 0), (0.004, 0.09, 0.06), (x, 0.06, 0.17), toon("signYellow", 0xF2C94C), outline=False))
    wheels(o, (-0.085, 0.085), (-0.12, 0.11), 0.035, "vwheel")
    return group(o, rot_z=0.0 if facing == "front" else math.radians(-90), scale=VEHICLE_SCALE)


for _f in ("front", "back"):
    SPRITES["truck_" + _f] = (lambda f=_f: (lambda: model_truck(f)))()
    SPRITES["van_" + _f] = (lambda f=_f: (lambda: model_van(f)))()


# ---------------------------------------------------------------- cattle (spec 012, US-204)

ANIMAL_SCALE = 1.3              # beside the people (1.35): readable, and four fit an acre with room to spare
STRAW, STRAW_DARK = 0xEBD48A, 0xC9A85A   # wheat bhusa: paler than the yard's haystack
BRICK, BRICK_DARK = 0xB8613E, 0x93492E
TIN, TIN_RIB, RUST = 0xB4BCC0, 0x8F989D, 0xB07048
CONCRETE = 0xCFC8BA

ANIMALS = {
    # Murrah buffalo: jet black with a blue sheen, tightly coiled horns, a heavy body on short legs.
    "buffalo": dict(body=0x47424F, dark=0x2A2730, muzzle=0x66606E, horn=0x2E2A2A, hoof=0x1C1A1E, legs=0.058, slim=1.0, patches=False),
    # Crossbred cow (HF cross, the valley's commonest): white with black patches, a pink muzzle and udder, short horns.
    "cow": dict(body=0xF5F1EA, dark=0xD9D2C8, muzzle=0xE9A7A0, horn=0xE8DCC0, hoof=0x3A302A, legs=0.068, slim=0.9, patches=True),
}


def model_animal(kind, pose="stand"):
    """A buffalo or a cow built along y with the head at -y (toward the viewer), turned a little to show her side.
    pose 'stand', 'eat' (head down to the manger) or 'lie' (legs folded under)."""
    p = ANIMALS[kind]
    def m(name, hexv):
        return toon(kind + "_" + name, hexv, soft=True)
    body, dark, muzzle, horn, hoof = m("body", p["body"]), m("dark", p["dark"]), m("muzzle", p["muzzle"]), m("horn", p["horn"]), m("hoof", p["hoof"])
    eye_w, eye_d, glint = m("eyeWhite", 0xF2EBE2), m("eye", 0x120F14), m("glint", 0xFFFFFF)
    lie = pose == "lie"
    lh = p["legs"]
    drop = (lh - 0.012) if lie else 0.0          # lying, the body rests on the ground
    z0 = lh + 0.028 - drop                        # the body's centre height
    w = p["slim"]
    o, legs, head = [], [], []
    o.append(sphere("body", 0.05, (0, 0.005, z0), body, scale=(0.95 * w, 1.75, 0.85), segs=18, rings=10))
    o.append(sphere("rump", 0.045, (0, 0.052, z0 + 0.004), body, scale=(1.05 * w, 1.0, 0.95), segs=16, rings=10))
    o.append(sphere("chest", 0.043, (0, -0.05, z0 + 0.002), body, scale=(1.0 * w, 1.0, 1.0), segs=16, rings=10))
    if lie:
        for s in (-1, 1):
            legs.append(sphere("foldF%d" % s, 0.016, (s * 0.04 * w, -0.06, 0.016), body, scale=(0.9, 1.7, 0.6), segs=10, rings=6))
            legs.append(sphere("foldH%d" % s, 0.02, (s * 0.046 * w, 0.05, 0.02), body, scale=(0.9, 1.5, 0.7), segs=10, rings=6))
    else:
        for i, (x, y) in enumerate(((-0.026, -0.055), (0.026, -0.055), (-0.027, 0.058), (0.027, 0.058))):
            legs.append(cylinder("leg%d" % i, 0.0125, 0.0105, lh, (x * w, y, lh / 2 + 0.006), body if not p["patches"] else m("leg", 0xEFEAE2), segs=10, smooth=True))
            legs.append(cylinder("hoof%d" % i, 0.011, 0.0115, 0.012, (x * w, y, 0.006), hoof, segs=10))
        if p["patches"]:   # the udder
            o.append(sphere("udder", 0.022, (0, 0.045, z0 - 0.04), m("udder", 0xEFB0A8), scale=(1.0, 1.0, 0.7), segs=12, rings=8))
    o += legs
    # Neck, head, muzzle, eyes, ears, horns: these bow together to eat.
    hz = z0 + 0.02
    head.append(sphere("neck", 0.03, (0, -0.088, hz - 0.004), body, scale=(0.9 * w, 1.1, 1.0), segs=14, rings=8))
    head.append(sphere("head", 0.03, (0, -0.118, hz + 0.004), body, scale=(0.85, 1.2, 0.9), segs=16, rings=10))
    head.append(sphere("muzzle", 0.022, (0, -0.148, hz - 0.01), muzzle, scale=(1.0, 0.9, 0.8), segs=14, rings=8))
    for s in (-1, 1):
        head.append(sphere("nostril%d" % s, 0.0035, (s * 0.008, -0.166, hz - 0.006), eye_d, segs=6, rings=4, outline=False))
        head.append(sphere("eyeW%d" % s, 0.0062, (s * 0.021, -0.128, hz + 0.013), eye_w, segs=8, rings=6, outline=False))
        head.append(sphere("eye%d" % s, 0.0048, (s * 0.0225, -0.1315, hz + 0.0135), eye_d, segs=8, rings=6, outline=False))
        head.append(sphere("glint%d" % s, 0.0014, (s * 0.023, -0.1355, hz + 0.0155), glint, segs=6, rings=4, outline=False))
        ear_y, ear_z = (-0.106, hz + 0.012) if kind == "buffalo" else (-0.104, hz + 0.016)
        head.append(sphere("ear%d" % s, 0.013, (s * 0.037, ear_y, ear_z), body, scale=(1.7, 0.6, 0.7), segs=10, rings=6))
        if kind == "buffalo":
            # Murrah horns: a tight coil back and down beside the ear.
            c = Vector((s * 0.026, -0.1, hz + 0.022))
            for k in range(14):
                t = k / 13.0
                th = -0.5 + t * 4.4
                r = 0.016 * (1 - 0.4 * t)
                q = c + Vector((s * 0.012 * t, r * math.sin(th), r * math.cos(th)))
                head.append(sphere("horn%d_%d" % (s, k), 0.0062 * (1 - 0.55 * t), tuple(q), horn, segs=8, rings=6, outline=False))
        else:
            head.append(cylinder("horn%d" % s, 0.0055, 0.002, 0.022, (s * 0.022, -0.112, hz + 0.034), horn, segs=8,
                                 rot=Matrix.Rotation(math.radians(-s * 35), 3, "Y")))
    if p["patches"]:
        # Black patches: flattened discs set into the body so only their caps show.
        pt = m("patch", 0x2A2627)
        for i, (x, y, z, r) in enumerate(((0.038, 0.015, z0 + 0.012, 0.028), (-0.036, -0.03, z0 + 0.006, 0.024), (0.0, 0.06, z0 + 0.034, 0.026),
                                          (0.036, -0.062, z0 - 0.01, 0.018), (-0.03, 0.06, z0 - 0.004, 0.02))):
            n = Vector((x, 0, z - z0)).normalized()
            o.append(sphere("patch%d" % i, r, (x - n.x * 0.006, y, z - n.z * 0.006), pt, scale=(0.6 if abs(x) > 0.01 else 1.0, 1.0, 0.6 if abs(x) < 0.01 else 1.0), segs=12, rings=8, outline=False))
        head.append(sphere("headPatch", 0.016, (0.017, -0.12, hz + 0.012), pt, scale=(0.7, 1.0, 0.9), segs=10, rings=6, outline=False))
    # The tail, hanging from the rump to a tuft.
    if lie:   # the tail lies along the ground beside her
        o.append(cylinder("tail", 0.003, 0.0025, 0.06, (0.03, 0.1, 0.008), body, segs=6, rot=Matrix.Rotation(math.radians(80), 3, "Y")))
        o.append(sphere("tuft", 0.007, (0.06, 0.1, 0.008), dark, scale=(1.6, 1, 1), segs=8, rings=5))
    else:
        o.append(cylinder("tail", 0.003, 0.0025, 0.07, (0, 0.096, z0 - 0.02), body, segs=6, rot=Matrix.Rotation(math.radians(10), 3, "X")))
        o.append(sphere("tuft", 0.007, (0, 0.102, z0 - 0.055), dark, scale=(1, 1, 1.6), segs=8, rings=5))
    if pose == "eat":
        pivot = Vector((0, -0.07, hz))
        bow = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(38), 4, "X") @ Matrix.Translation(-pivot)
        for part in head:
            part.matrix_world = bow @ part.matrix_world
    o += head
    return group(o, rot_z=math.radians(18), scale=ANIMAL_SCALE, outline_factor=0.7)


# The cattle acre (ADR 0064): pieces placed spread out over the acre like a village baada, so animals sort among them.
# Where each piece and each animal stands, in world units from the acre's centre (+x lower right, +y upper right on screen).
CATTLE_ACRE = {
    "cattle_shed": (-0.04, 0.33),       # along the back, near the house; its open front faces into the acre
    "straw_stack": (0.38, 0.32),        # by the feed room, at the right corner
    "tree_neem": (-0.34, -0.32),        # shade at the left corner
    "water_trough": (0.36, -0.12),
    "compost_pit": (0.36, -0.38),       # the far corner, away from the house
}
# The four animals' places and their mangers (at each head, +0.06, -0.21): one lies in the neem's shade with no manger.
# The hedge runs round the edges, its gate on the yard's side (x = -0.5). Shown to the owner for approval (ADR 0064).
CATTLE_PLACES = [(-0.16, -0.24, "lie"), (-0.24, 0.12, "stand"), (0.18, 0.06, "eat"), (0.02, -0.08, "stand")]


def model_shed(part):
    """The cattle shed alone: a concrete floor open to the front, low brick back and end walls, three stall rails, straw bedding,
    a lean-to tin roof, the feed room at its right end. part 'done' or 'building' (pillars, a low wall, tin and bricks waiting)."""
    concrete, brick, brick_d = toon("concrete", CONCRETE), toon("brick", BRICK), toon("brickDark", BRICK_DARK)
    o = [box("floor", (0.50, 0.22, 0.025), (0, 0, 0.0125), concrete, bevel=0.004)]
    pillars = (-0.24, -0.08, 0.08)
    if part == "building":
        o.append(box("backwall", (0.36, 0.025, 0.045), (-0.07, 0.1, 0.047), brick))
        for i, x in enumerate(pillars):
            o.append(box("pillar%d" % i, (0.022, 0.022, 0.11), (x, -0.1, 0.08), brick))
        for i in range(4):
            o.append(box("sheet%d" % i, (0.16, 0.10, 0.005), (0.16, -0.02 + i * 0.002, 0.03 + i * 0.007), toon("tin", TIN), rot_z=math.radians(3 * i - 4)))
        for i in range(2):
            for j in range(2):
                o.append(box("brick%d%d" % (i, j), (0.045, 0.022, 0.02), (-0.18 + j * 0.05, 0.0, 0.035 + i * 0.02), brick_d))
        return group(o, scale=SHED_SCALE)
    o.append(box("backwall", (0.36, 0.025, 0.10), (-0.07, 0.1, 0.075), brick))
    o.append(box("endwall", (0.025, 0.2, 0.10), (-0.245, 0.0, 0.075), brick))
    for z in (0.05, 0.085):
        o.append(box("mortar%.3f" % z, (0.362, 0.027, 0.003), (-0.07, 0.1, z), brick_d, outline=False))
    for i, x in enumerate(pillars):
        o.append(box("pillar%d" % i, (0.022, 0.022, 0.18), (x, -0.1, 0.115), brick))
        o.append(box("backpost%d" % i, (0.022, 0.022, 0.15), (x, 0.1, 0.1), brick))
    bamboo = toon("bamboo", BAMBOO)
    for i, x in enumerate((-0.16, 0.0)):
        for z in (0.055, 0.085):
            o.append(cylinder("rail%d_%.2f" % (i, z), 0.004, 0.004, 0.17, (x, 0.0, z), bamboo, segs=8, rot=Matrix.Rotation(math.radians(90), 3, "X")))
    import random
    rnd = random.Random(12)
    for i in range(14):
        o.append(cylinder("bedding%d" % i, 0.0022, 0.0022, 0.045, (rnd.uniform(-0.22, 0.06), rnd.uniform(-0.08, 0.08), 0.03), toon("straw", STRAW), segs=4, outline=False,
                          rot=Matrix.Rotation(math.radians(88), 3, Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 0)).normalized())))
    o.append(box("feedroom", (0.12, 0.22, 0.17), (0.19, 0.0, 0.11), toon("lime", 0xE9DCC4)))
    o.append(box("feedskirt", (0.122, 0.222, 0.025), (0.19, 0.0, 0.037), toon("mud_dark", MUD_DARK), outline=False))
    o.append(box("door", (0.055, 0.01, 0.10), (0.19, -0.111, 0.075), toon("door", DOOR)))
    o.append(box("doorframe", (0.068, 0.006, 0.11), (0.19, -0.108, 0.08), toon("trim", TRIM), outline=False))
    o.append(box("window", (0.01, 0.06, 0.045), (0.251, 0.02, 0.14), toon("wood_dark", WOOD_DARK)))
    for i in (-1, 0, 1):
        o.append(box("bar%d" % i, (0.012, 0.004, 0.045), (0.252, 0.02 + i * 0.016, 0.14), toon("iron", 0x6C7278), outline=False))
    o.append(box("sign", (0.07, 0.006, 0.025), (0.19, -0.113, 0.16), toon("signBoard", 0xF2C94C), outline=False))
    tilt = Matrix.Rotation(math.radians(-11), 3, "X")
    o.append(box("roof", (0.56, 0.28, 0.01), (0, 0.0, 0.215), toon("tin", TIN), rot=tilt))
    for i in range(14):
        o.append(box("rib%d" % i, (0.005, 0.28, 0.005), (-0.26 + i * 0.04, 0.0, 0.222), toon("tinRib", TIN_RIB), rot=tilt, outline=False))
    o.append(box("rust", (0.1, 0.2, 0.003), (-0.14, 0.02, 0.2215), toon("rust", RUST), rot=tilt, outline=False))
    o.append(cylinder("drum", 0.03, 0.03, 0.08, (0.27, -0.09, 0.04), toon("drum", 0x2E6FBA), segs=16, smooth=True))
    o.append(cylinder("drumrim", 0.032, 0.032, 0.007, (0.27, -0.09, 0.08), toon("drumDark", 0x24589A), segs=16, outline=False))
    return group(o, scale=SHED_SCALE)


SHED_SCALE = 1.15
YARD_PROP_SCALE = 0.95   # the straw stack and the compost pit: life-like beside the shed, so the acre stays open


def model_manger():
    """One animal's place: a small concrete manger with fodder in it and a wooden tethering peg beside it."""
    concrete = toon("concrete", CONCRETE)
    o = [box("manger", (0.09, 0.045, 0.035), (0, 0, 0.0175), concrete, bevel=0.005),
         box("fill", (0.076, 0.032, 0.005), (0, 0, 0.036), toon("chaff", 0xC9B26A), outline=False),
         cylinder("peg", 0.005, 0.004, 0.045, (0.06, 0.012, 0.0225), toon("wood", WOOD), segs=6)]
    for i, x in enumerate((-0.022, 0.0, 0.024)):
        o.append(sphere("greens%d" % i, 0.008, (x, 0.0, 0.04), toon("fodderGreen", 0x6DB043), scale=(1.6, 0.8, 0.5), segs=6, rings=4, outline=False))
    return group(o, rot_z=math.radians(18), scale=1.0)


def model_water_trough():
    """A round cement water trough (a nand) with clear water and a steel bucket beside it."""
    o = [cylinder("trough", 0.055, 0.05, 0.035, (0, 0, 0.0175), toon("concrete", CONCRETE), segs=20),
         cylinder("water", 0.046, 0.046, 0.004, (0, 0, 0.034), toon("water", WATER), segs=20, outline=False),
         cylinder("bucket", 0.016, 0.012, 0.028, (0.075, -0.02, 0.014), toon("steel", 0xC9CDD2), segs=14, smooth=True)]
    return group(o, scale=1.3)


def model_neem():
    """A neem tree: a straight dark trunk and a rounded crown of small, fine leaves in clusters; it shades the cattle."""
    bark = toon("neemBark", 0x5E4430)
    o = [cylinder("trunk", 0.03, 0.022, 0.24, (0, 0, 0.12), bark, segs=9, smooth=True),
         cylinder("branch", 0.012, 0.008, 0.10, (0.03, 0, 0.24), bark, segs=6, rot=Matrix.Rotation(math.radians(35), 3, "Y"), outline=False)]
    import random
    rnd = random.Random(9)
    mats = (toon("neemDark", 0x3E7A34), toon("neemMid", 0x4F9440), toon("neemLight", 0x6CAE4E))
    for i in range(16):
        a, r = rnd.uniform(0, math.tau), rnd.uniform(0.0, 0.13)
        z = 0.30 + rnd.uniform(-0.03, 0.08) + (0.13 - r) * 0.4
        o.append(sphere("crown%d" % i, rnd.uniform(0.055, 0.08), (math.cos(a) * r, math.sin(a) * r, z), mats[2] if z > 0.37 else mats[i % 2], scale=(1, 1, 0.8), segs=12, rings=8))
    o.append(cylinder("shade", 0.17, 0.17, 0.002, (0.03, -0.03, 0.001), toon("shade", 0x7E9A48), segs=24, outline=False))
    return group(o, scale=1.3)


def model_hedge(axis, gate=False):
    """The cattle acre's edge, one tile side long: a low mud wall with thorny bushes along it; with a gate, a wooden gate in the middle."""
    wall, a_, b_ = toon("mudWall", 0xA8784A), toon("hedgeA", 0x5E8A36), toon("hedgeB", 0x739A40)
    o = []
    rot = 0.0 if axis == "x" else math.radians(90)
    segs = [(-0.5, -0.07), (0.07, 0.5)] if gate else [(-0.5, 0.5)]
    import random
    rnd = random.Random(31 if axis == "x" else 32)
    b = Batch("hedge")
    for s0, s1 in segs:
        mid, ln = (s0 + s1) / 2, s1 - s0
        o.append(box("wall%.2f" % s0, (ln, 0.022, 0.026), (mid, 0, 0.013), wall, bevel=0.005))
        n = int(ln / 0.06)
        for i in range(n):
            x = s0 + (i + 0.5) * ln / n + rnd.uniform(-0.01, 0.01)
            b.sphere(a_ if i % 2 else b_, rnd.uniform(0.018, 0.025), Matrix.Translation((x, rnd.uniform(-0.006, 0.006), 0.036)), scale=(1.3, 0.8, 0.85), segs=8, rings=5)
    o += b.objects(outline_for=(a_, b_))
    if gate:
        wood = toon("gateWood", 0x9A6A3C)
        for x in (-0.07, 0.07):
            o.append(box("gatepost%d" % (x > 0), (0.018, 0.018, 0.09), (x, 0, 0.045), wood))
        for z in (0.03, 0.06):
            o.append(box("gaterail%.2f" % z, (0.12, 0.008, 0.012), (0, 0, z), wood))
        o.append(box("gatebrace", (0.012, 0.008, 0.06), (0, 0, 0.045), wood, rot=Matrix.Rotation(math.radians(60), 3, "Y")))
    return group(o, rot_z=rot)


def model_ground_cattle():
    """The cattle acre's ground: poor sandy land (usar) under thin, dry grass, pale saline patches, and a beaten path."""
    o = ground_block("cattleGround", 0xA6B862, 0x95A858)
    import random
    rnd = random.Random(44)
    b = Batch("usar")
    for i in range(7):
        x, y, r = rnd.uniform(-0.35, 0.35), rnd.uniform(-0.35, 0.35), rnd.uniform(0.05, 0.11)
        b.cone(toon("usarPatch", 0xDCCFAE), r, r * 0.95, 0.003, Matrix.Translation((x, y, 0.001)) @ Matrix.Diagonal((1.0, rnd.uniform(0.6, 1.0), 1.0, 1.0)), segs=10)
    for i in range(9):   # the path from the gate on the yard's side to the shed, and round to the trough
        t = i / 8.0
        x, y = -0.46 + 0.5 * t, -0.02 + 0.22 * t * t
        b.cone(toon("path", 0xC9AE84), 0.05, 0.05, 0.003, Matrix.Translation((x, y, 0.0015)), segs=10)
    tufts = (toon("dryTuftA", 0x8FA048), toon("dryTuftB", 0xB3AE62))
    for _ in range(14):
        x, y = rnd.uniform(-0.42, 0.42), rnd.uniform(-0.42, 0.42)
        for k in range(4):
            a = k * math.tau / 4 + rnd.uniform(-0.4, 0.4)
            b.cone(tufts[_ % 2], 0.009, 0.0, rnd.uniform(0.03, 0.05), at(x, y, 0.0, tilt=0.55, tilt_dir=a), segs=3)
    return o + b.objects()


def dome(name, r, h, loc, mat, segs=20, rings=12):
    """The top half of a sphere, radius r, height h, its flat base at loc."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=r)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -1e-6], context="VERTS")
    bmesh.ops.scale(bm, vec=Vector((1, 1, h / r)), verts=bm.verts)
    bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    return obj_from_bm(name, bm, mat, smooth=True)


def model_straw_stack(level):
    """The straw stack (a bhusa kup): a brick base and a centre pole, the heap at three heights as stock grows; 'empty' just the base."""
    o = [cylinder("base", 0.14, 0.145, 0.025, (0, 0, 0.0125), toon("brickDark", BRICK_DARK), segs=18),
         cylinder("pole", 0.005, 0.005, 0.33, (0, 0, 0.165), toon("wood", WOOD), segs=6)]
    straw, dark = toon("straw", STRAW), toon("strawDark", STRAW_DARK)
    import random
    rnd = random.Random(7)
    if level == "empty":
        for i in range(10):
            a = rnd.uniform(0, math.tau)
            o.append(cylinder("bit%d" % i, 0.003, 0.003, 0.05, (math.cos(a) * 0.08, math.sin(a) * 0.08, 0.03), straw, segs=4, outline=False,
                              rot=Matrix.Rotation(math.radians(85), 3, Vector((-math.sin(a), math.cos(a), 0)))))
        return group(o, scale=YARD_PROP_SCALE)
    r, h = {"low": (0.12, 0.07), "mid": (0.13, 0.15), "high": (0.135, 0.25)}[level]
    o.append(dome("heap", r, h, (0, 0, 0.025), straw))
    o.append(cylinder("skirt", r * 1.02, r * 0.98, 0.03, (0, 0, 0.04), dark, segs=20, smooth=True, outline=False))
    # Loose wisps over the surface so it reads as straw, not a dome.
    for i in range(int(14 + 20 * h)):
        a, t = rnd.uniform(0, math.tau), rnd.uniform(0.15, 0.9)
        rr = r * math.sqrt(1 - t * t)
        z = 0.025 + h * t
        o.append(cylinder("wisp%d" % i, 0.0025, 0.002, 0.04, (math.cos(a) * rr, math.sin(a) * rr, z), dark if i % 3 else straw, segs=4, outline=False,
                          rot=Matrix.Rotation(rnd.uniform(0.6, 1.4), 3, Vector((-math.sin(a), math.cos(a), 0)))))
    if level == "high":   # a thatch cap tied at the top against the rain
        o.append(cylinder("cap", 0.07, 0.01, 0.06, (0, 0, 0.025 + h + 0.015), toon("thatch_dark", THATCH_DARK), segs=14, smooth=True))
        o.append(cylinder("tie", 0.035, 0.035, 0.008, (0, 0, 0.025 + h + 0.02), toon("rope", 0xB08A50), segs=14, outline=False))
    return group(o, scale=YARD_PROP_SCALE)


def model_compost_pit(level):
    """The compost pit beside the shed: a brick-lined pit, empty, half or heaped full of dung and straw; a spade when full."""
    brick, dark = toon("brick", BRICK), toon("pitDark", 0x3A2818)
    o = [box("rim", (0.30, 0.19, 0.025), (0, 0, 0.0125), brick, bevel=0.004),
         box("hole", (0.26, 0.15, 0.004), (0, 0, 0.026), dark, outline=False)]
    import random
    rnd = random.Random(23)
    if level > 0:
        hgt = 0.02 if level == 1 else 0.06
        o.append(sphere("heap", 0.12, (0, 0, 0.022), toon("compost", 0x4E3422), scale=(1.05, 0.6, hgt / 0.12), segs=18, rings=9))
        clods = (toon("compost1", 0x3E2818), toon("dung", 0x6A4A30))
        for i in range(10 + 12 * level):
            x, y = rnd.uniform(-0.11, 0.11), rnd.uniform(-0.06, 0.06)
            z = 0.022 + hgt * math.sqrt(max(0.0, 1 - (x / 0.126) ** 2 - (y / 0.072) ** 2))
            o.append(sphere("clod%d" % i, rnd.uniform(0.008, 0.014), (x, y, z), clods[i % 2], scale=(1.2, 1, 0.6), segs=6, rings=4, outline=False))
        for i in range(4 * level):
            x, y = rnd.uniform(-0.09, 0.09), rnd.uniform(-0.05, 0.05)
            o.append(cylinder("straw%d" % i, 0.0025, 0.0025, 0.045, (x, y, 0.03 + hgt * 0.7), toon("straw", STRAW), segs=4, outline=False,
                              rot=Matrix.Rotation(1.3, 3, Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 0)).normalized())))
    if level == 2:
        o.append(cylinder("spade", 0.003, 0.003, 0.12, (0.17, 0.02, 0.06), toon("wood", WOOD), segs=6, rot=Matrix.Rotation(math.radians(-15), 3, "Y")))
        o.append(box("blade", (0.03, 0.004, 0.035), (0.185, 0.02, 0.008), toon("iron", 0x7C8288)))
    return group(o, scale=YARD_PROP_SCALE)


def model_sign_locked():
    """A plot waiting for the Hamlet: a wooden board on a post with a brass padlock painted on."""
    wood, board = toon("wood", WOOD), toon("lockedBoard", 0xD9CBB0)
    brass = toon("brass", 0xE6B33A)
    o = [box("post", (0.016, 0.016, 0.22), (0, 0, 0.11), wood),
         box("board", (0.13, 0.012, 0.09), (0, -0.012, 0.17), board, bevel=0.004),
         box("lock", (0.034, 0.008, 0.028), (0, -0.021, 0.16), brass, outline=False),
         cylinder("shackle", 0.012, 0.012, 0.006, (0, -0.021, 0.18), toon("steel", 0x9AA0A6), segs=16, cap=False,
                  rot=Matrix.Rotation(math.radians(90), 3, "X"), outline=False),
         cylinder("keyhole", 0.0035, 0.0035, 0.004, (0, -0.0255, 0.161), toon("dark", 0x3A2A1A), segs=8, rot=Matrix.Rotation(math.radians(90), 3, "X"), outline=False)]
    return group(o, rot_z=math.radians(45), scale=BUILDING_SCALE)


def crop_berseem(b, rnd, x, y, stage):
    """Berseem (Egyptian clover): a dense low carpet of trifoliate leaves; ready, round white flower heads; cut, short stubble."""
    green, deep, light = toon("berseem", 0x62B847), toon("berseemDeep", 0x4E9E3A), toon("berseemLight", 0x8ED061)
    if stage == "stub":
        for k in range(3):
            a = rnd.uniform(0, math.tau)
            b.cone(toon("stubble", 0x9DB25A), 0.003, 0.0025, 0.014, at(x + math.cos(a) * 0.012, y + math.sin(a) * 0.012, 0.012), segs=4)
        b.sphere(light, 0.006, Matrix.Translation((x, y, 0.026)), scale=(1.3, 0.8, 0.4), segs=6, rings=3)
        return
    n, h = {"g1": (3, 0.022), "g2": (6, 0.036), "g3": (9, 0.05), "ready": (10, 0.056)}[stage]
    for k in range(n):
        a = k * math.tau / n + rnd.uniform(-0.4, 0.4)
        stem = at(x, y, 0.012, tilt=rnd.uniform(0.2, 0.55), tilt_dir=a)
        hh = h * rnd.uniform(0.8, 1.1)
        b.cone(deep, 0.0025, 0.002, hh, stem, segs=3)
        tip = stem @ Matrix.Translation((0, 0, hh))
        for q in range(3):   # three leaflets round the tip
            qa = q * math.tau / 3 + a
            b.sphere(green if (k + q) % 2 else light, 0.0085, tip @ Matrix.Translation((math.cos(qa) * 0.007, math.sin(qa) * 0.007, 0)), scale=(1.3, 0.85, 0.35), segs=6, rings=4)
        if stage == "ready" and k % 5 == 0:
            b.sphere(toon("berseemFlower", 0xF6F1E2), 0.008, tip @ Matrix.Translation((0, 0, 0.012)), segs=8, rings=5)


def crop_sorghum(b, rnd, x, y, stage):
    """Fodder sorghum (jowar): tall cane-like stalks with long arching strap leaves, cut green at flowering; cut, stubs re-sprouting."""
    leafc, deep, stalk = toon("sorghumLeaf", 0x5DA83C), toon("sorghumDeep", 0x3F8A32), toon("sorghumStalk", 0x7FB04A)
    if stage == "stub":
        for k in range(2):
            sx, sy = x + rnd.uniform(-0.01, 0.01), y + rnd.uniform(-0.01, 0.01)
            b.cone(toon("sorghumCut", 0xA9B866), 0.0055, 0.005, 0.022, Matrix.Translation((sx, sy, 0.012)), segs=6)
            leaf(b, leafc, sx, sy, 0.03, 0.03, 0.01, rnd.uniform(0, math.tau), 0.7)
        return
    h, nl = {"g1": (0.0, 3), "g2": (0.08, 5), "g3": (0.16, 8), "ready": (0.18, 8)}[stage]
    if stage == "g1":
        for k in range(nl):
            b.cone(leafc if k % 2 else deep, 0.008, 0.0, 0.045, at(x, y, 0.012, tilt=0.35, tilt_dir=k * math.tau / nl + rnd.uniform(-0.3, 0.3)), segs=3)
        return
    hh = h * rnd.uniform(0.88, 1.08)
    lean = at(x, y, 0.012, tilt=rnd.uniform(0.0, 0.08), tilt_dir=rnd.uniform(0, math.tau))
    b.cone(stalk, 0.008, 0.005, hh, lean, segs=6)
    for k in range(nl):
        t = 0.25 + 0.7 * k / nl
        p = lean @ Vector((0, 0, hh * t))
        a = k * 2.39996 + rnd.uniform(-0.3, 0.3)
        leaf(b, leafc if k % 2 else deep, p.x, p.y, p.z, 0.08 * (1.1 - 0.4 * t) + 0.025, 0.026, a, rnd.uniform(0.1, 0.45))
    if stage == "ready":
        top = lean @ Vector((0, 0, hh))
        b.sphere(toon("sorghumHead", 0xD8D49A), 0.011, Matrix.Translation(top + Vector((0, 0, 0.014))), scale=(0.8, 0.8, 1.8), segs=8, rings=5)


def fodder_bundles(b, rnd, crop):
    """Bundles of cut fodder lying where the day's cutting reached."""
    mat = toon("cutFodder", 0x9BCB5A)
    tie = toon("tie", 0xB4572E)
    for i, x in enumerate((-0.3, 0.05, 0.32)):
        m = Matrix.Translation((x, -0.02 + 0.03 * (i % 2), 0.035)) @ Matrix.Rotation(math.radians(90), 4, "Y") @ Matrix.Rotation(rnd.uniform(-0.4, 0.4), 4, "X")
        b.sphere(mat, 0.03, m, scale=(1.0, 1.0, 2.4), segs=10, rings=6)
        b.cone(tie, 0.031, 0.031, 0.01, m @ Matrix.Translation((0, 0, -0.005)), segs=10)


CROPS.update({"berseem": crop_berseem, "sorghum": crop_sorghum})
MATS.update({"berseem": (0x5FAE45, 0x5FAE45), "sorghum": (0x6AAE3E, 0x6AAE3E)})


def icon_cattle(kind):
    """The cattle icons: the animals, milk, straw, green fodder, cattle feed, and the two fodder crops."""
    import random
    rnd = random.Random(5)
    o = []
    if kind in ("buffalo", "cow"):
        return model_animal(kind, "stand")
    if kind == "milk":
        # An aluminium milk can (the dairy's can) with a lid and side handles.
        al, al_d = toon("alu", 0xD5D9DD), toon("aluDark", 0xA9AFB5)
        o.append(cylinder("can", 0.07, 0.07, 0.15, (0, 0, 0.075), al, segs=24, smooth=True))
        o.append(cylinder("shoulder", 0.07, 0.04, 0.04, (0, 0, 0.17), al, segs=24, smooth=True))
        o.append(cylinder("neck", 0.04, 0.04, 0.03, (0, 0, 0.205), al, segs=24, smooth=True))
        o.append(cylinder("lid", 0.046, 0.046, 0.016, (0, 0, 0.225), al_d, segs=24))
        o.append(cylinder("knob", 0.012, 0.012, 0.014, (0, 0, 0.24), al_d, segs=12))
        o.append(cylinder("band", 0.0715, 0.0715, 0.012, (0, 0, 0.06), al_d, segs=24, outline=False))
        o.append(sphere("milk", 0.03, (0.06, -0.075, 0.02), toon("milkWhite", 0xFFFDF6), scale=(1.4, 1.0, 0.35), segs=14, rings=6))
        for s in (-1, 1):
            o.append(box("handle%d" % s, (0.012, 0.035, 0.012), (s * 0.078, 0, 0.15), al_d))
        return turn(o, Matrix.Rotation(math.radians(20), 4, "Z"))
    if kind in ("straw", "green_fodder"):
        mats = (toon("straw", STRAW), toon("strawDark", STRAW_DARK)) if kind == "straw" else (toon("fodderG", 0x62B847), toon("fodderD", 0x4E9E3A))
        b = Batch(kind)
        for i in range(46):
            a, r = rnd.uniform(0, math.tau), 0.045 * math.sqrt(rnd.uniform(0, 1))
            b.cone(mats[i % 2], 0.0045, 0.003, 0.26 + rnd.uniform(-0.02, 0.02), Matrix.Translation((math.cos(a) * r, math.sin(a) * r, -0.13)), segs=4)
        if kind == "green_fodder":
            for i in range(10):   # leaves flaring out at the top of the bundle
                a = i * math.tau / 10
                leaf(b, mats[i % 2], math.cos(a) * 0.03, math.sin(a) * 0.03, 0.12, 0.08, 0.025, a, 0.9)
        o += b.objects(outline_for=tuple(b.parts), thickness=OUTLINE * 0.5)
        for z in (-0.06, 0.05):
            o.append(cylinder("tie%.2f" % z, 0.05, 0.05, 0.016, (0, 0, z), toon("tie", 0xB4572E), segs=18))
        return turn(o, Matrix.Rotation(math.radians(70), 4, "Y"))
    if kind == "cattle_feed":
        # A jute sack of cattle feed with a green printed band, a spill of pellets in front.
        jute = toon("jute", 0xC9A46A)
        o.append(sphere("sack", 0.09, (0, 0, 0.085), jute, scale=(1, 0.9, 1.1), segs=16, rings=10))
        o.append(cylinder("neck", 0.03, 0.05, 0.06, (0, 0, 0.19), jute, segs=14, smooth=True))
        o.append(cylinder("tie", 0.034, 0.034, 0.014, (0, 0, 0.18), toon("tie", 0xB4572E), segs=14))
        o.append(cylinder("band", 0.093, 0.093, 0.035, (0, 0, 0.09), toon("feedBand", 0x2F9E5B), segs=20, outline=False))
        for i in range(14):
            o.append(cylinder("pellet%d" % i, 0.008, 0.008, 0.018, (rnd.uniform(-0.03, 0.12), rnd.uniform(-0.13, -0.08), 0.008), toon("pellet", 0x8C6A3A), segs=8,
                              rot=Matrix.Rotation(math.radians(90), 3, Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), 0)).normalized()), outline=False))
        return o
    b = Batch(kind)
    if kind == "berseem":
        # A sprig of clover: trifoliate leaves on short stems, two round white flower heads.
        for k in range(7):
            a = k * 2.39996
            stem = at(0, 0, 0, tilt=0.35 + 0.15 * (k % 3), tilt_dir=a)
            b.cone(toon("berseemDeep", 0x4E9E3A), 0.004, 0.003, 0.14, stem, segs=4)
            tip = stem @ Matrix.Translation((0, 0, 0.14))
            for q in range(3):
                qa = q * math.tau / 3 + a
                b.sphere(toon("berseem", 0x62B847) if q % 2 else toon("berseemLight", 0x8ED061), 0.024, tip @ Matrix.Translation((math.cos(qa) * 0.02, math.sin(qa) * 0.02, 0)), scale=(1.3, 0.85, 0.35), segs=10, rings=6)
            if k in (2, 5):
                b.sphere(toon("berseemFlower", 0xF6F1E2), 0.022, tip @ Matrix.Translation((0, 0, 0.03)), segs=12, rings=8)
    else:
        # A sorghum stalk with strap leaves and a cream panicle.
        b.cone(toon("sorghumStalk", 0x7FB04A), 0.012, 0.008, 0.32, Matrix.Identity(4), segs=8)
        for k in range(6):
            t = 0.2 + 0.12 * k
            leaf(b, toon("sorghumLeaf", 0x5DA83C) if k % 2 else toon("sorghumDeep", 0x3F8A32), 0, 0, 0.32 * t, 0.2, 0.035, k * 2.39996, 0.35)
        b.sphere(toon("sorghumHead", 0xD8D49A), 0.025, Matrix.Translation((0, 0, 0.355)), scale=(0.8, 0.8, 1.9), segs=10, rings=7)
    return b.objects(outline_for=tuple(b.parts), thickness=OUTLINE * 0.8)


for _a in ANIMALS:
    for _p in ("stand", "eat", "lie"):
        SPRITES["animal_%s_%s" % (_a, _p)] = (lambda a=_a, p=_p: (lambda: model_animal(a, p)))()
SPRITES["cattle_shed"] = lambda: model_shed("done")
SPRITES["cattle_shed_building"] = lambda: model_shed("building")
SPRITES.update({"cattle_manger": model_manger, "water_trough": model_water_trough, "tree_neem": model_neem, "ground_cattle": model_ground_cattle,
                "hedge_x": lambda: model_hedge("x"), "hedge_y": lambda: model_hedge("y"),
                "hedge_gate_x": lambda: model_hedge("x", True), "hedge_gate_y": lambda: model_hedge("y", True)})
for _l in ("empty", "low", "mid", "high"):
    SPRITES["straw_stack_" + _l] = (lambda l=_l: (lambda: model_straw_stack(l)))()
for _l in range(3):
    SPRITES["compost_pit_%d" % _l] = (lambda l=_l: (lambda: model_compost_pit(l)))()
SPRITES["sign_locked"] = model_sign_locked
for _c in ("berseem", "sorghum"):
    for _st in STAGES + ("cut",):
        SPRITES["crop_%s_%s" % (_c, _st)] = (lambda c=_c, st=_st: (lambda: model_crop(c, st)))()
for _k, _n in (("buffalo", "icon_animal_buffalo"), ("cow", "icon_animal_cow"), ("milk", "icon_good_milk"), ("straw", "icon_good_straw"),
               ("green_fodder", "icon_good_green_fodder"), ("cattle_feed", "icon_good_cattle_feed"), ("berseem", "icon_good_berseem"),
               ("sorghum", "icon_good_sorghum")):
    ICONS[_n] = (lambda k=_k: (lambda: icon_cattle(k)))()
    SPRITES[_n] = ICONS[_n]


# ---------------------------------------------------------------- a living valley (ADR 0062, US-207)
# Neighbours' land varied by soil: mustard in Winter, sugarcane, poor sandy acres gone to scrub with a babul, tube-well rooms,
# farmsteads (a house, a thatched cattle shelter, haystacks) on acres not for sale; round your own land a hedge on a low mud wall,
# whitewashed corner stones, and fresh bunds against the neighbours' worn, grassed-over ones.

def crop_mustard(b, rnd, x, y, stage):
    """Sarson in bloom: leaves at the foot, then thin stems topped by many small yellow florets, so a field reads as a yellow carpet."""
    leaf_m, stem = toon("mustardLeaf", 0x5E9E3A), toon("mustardStem", 0x7FB24A)
    flowers = (toon("mustardFlower", 0xF6D32B), toon("mustardFlowerDeep", 0xE8BC1E), toon("mustardFlowerPale", 0xFBE46A))
    for k in range(4):
        leaf(b, leaf_m, x, y, 0.02, 0.05, 0.03, k * math.tau / 4 + rnd.uniform(-0.3, 0.3), 0.4)
    for k in range(4):
        a = k * math.tau / 4 + rnd.uniform(-0.4, 0.4)
        h = rnd.uniform(0.09, 0.13)
        m = at(x + math.cos(a) * 0.012, y + math.sin(a) * 0.012, 0.02, tilt=rnd.uniform(0.1, 0.3), tilt_dir=a)
        b.cone(stem, 0.0025, 0.0018, h, m, segs=4)
        for j in range(7):
            fa = j * math.tau / 7 + rnd.uniform(-0.5, 0.5)
            b.sphere(flowers[(k + j) % 3], 0.0105, m @ Matrix.Translation((math.cos(fa) * 0.009, math.sin(fa) * 0.009, h + rnd.uniform(-0.012, 0.008))),
                     scale=(1, 1, 0.8), segs=6, rings=4)


def crop_sugarcane(b, rnd, x, y, stage):
    """Ganna: a dense clump of tall canes hidden in long leaves that arch out and droop, the canes showing only near the ground."""
    cane = toon("cane", 0x8FA654)
    leaves = (toon("caneLeaf", 0x5DA53E), toon("caneLeafDeep", 0x468A33), toon("caneLeafLight", 0x78B84E))
    for k in range(3):
        a = k * math.tau / 3 + rnd.uniform(-0.5, 0.5)
        h = rnd.uniform(0.2, 0.26)
        m = at(x + math.cos(a) * 0.01, y + math.sin(a) * 0.01, 0.0, tilt=rnd.uniform(0.02, 0.08), tilt_dir=a)
        b.cone(cane, 0.007, 0.006, h, m, segs=5)
        for j in range(5):
            z = h * (0.45 + j * 0.13)
            pt = (m @ Matrix.Translation((0, 0, z))).to_translation()
            leaf(b, leaves[(k + j) % 3], pt[0], pt[1], pt[2], rnd.uniform(0.1, 0.15), 0.024, a + j * 2.4 + rnd.uniform(-0.3, 0.3), rnd.uniform(-0.1, 0.35))


CROPS.update({"mustard": crop_mustard, "sugarcane": crop_sugarcane})
MATS.update({"mustard": (0x5E9E3A, 0x5E9E3A), "sugarcane": (0x4E8E36, 0x4E8E36)})


def model_ground_scrub():
    """A poor, sandy acre gone to scrub: dry yellow-green grass with bare sandy patches and tufts of coarse grass."""
    import random
    rnd = random.Random(44)
    o = ground_block("scrub", 0xB4B36A, 0xA4A35C)
    b = Batch("scrubTop")
    sand, dry, green = toon("scrubSand", 0xD8C690), toon("scrubDry", 0xA9A04E), toon("scrubGreen", 0x7FA046)
    for i in range(9):
        b.sphere(sand, 1.0, Matrix.Translation((rnd.uniform(-0.38, 0.38), rnd.uniform(-0.38, 0.38), 0.002)),
                 scale=(rnd.uniform(0.06, 0.12), rnd.uniform(0.05, 0.09), 0.002), segs=10, rings=3)
    for _ in range(26):
        x, y = rnd.uniform(-0.43, 0.43), rnd.uniform(-0.43, 0.43)
        g = dry if rnd.random() < 0.6 else green
        for k in range(5):
            a = k * math.tau / 5 + rnd.uniform(-0.4, 0.4)
            b.cone(g, 0.01, 0.0, rnd.uniform(0.04, 0.065), at(x, y, 0.0, tilt=0.5, tilt_dir=a), segs=3)
    return o + b.objects()


def model_babul():
    """A babul (desert acacia) on poor land: a thin, leaning dark trunk forking into a wide, flat, two-tiered crown of fine leaves."""
    import random
    rnd = random.Random(12)
    bark = toon("babulBark", 0x4A3626)
    o = [cylinder("trunk", 0.016, 0.011, 0.18, (0.01, 0, 0.09), bark, segs=7, rot=Matrix.Rotation(math.radians(6), 3, "Y"))]
    for a in (0.6, 2.6, 4.4):
        o.append(cylinder("branch%.1f" % a, 0.008, 0.005, 0.12, (0.02 + math.cos(a) * 0.035, math.sin(a) * 0.035, 0.2), bark, segs=5,
                          rot=Matrix.Rotation(a, 3, "Z") @ Matrix.Rotation(math.radians(55), 3, "Y"), outline=False))
    b = Batch("babulCrown")
    mats = (toon("babulDark", 0x5F7A3A), toon("babulMid", 0x728E44), toon("babulLight", 0x89A653))
    for tier, (z, rr) in enumerate(((0.235, 0.15), (0.275, 0.1))):
        for i in range(16 if tier == 0 else 9):
            a, r = rnd.uniform(0, math.tau), rr * math.sqrt(rnd.uniform(0, 1))
            b.sphere(mats[(i + tier) % 3], rnd.uniform(0.03, 0.045), Matrix.Translation((0.02 + math.cos(a) * r, math.sin(a) * r, z + rnd.uniform(-0.008, 0.008))),
                     scale=(1.5, 1.5, 0.45), segs=10, rings=5)
    o += b.objects(outline_for=tuple(b.parts))
    return group(o, scale=1.2)


def model_tubewell():
    """A tube-well room in the fields: a small brick pump house with a flat roof; its pipe pours into a little tank and channel."""
    brick, roof, door = toon("brick", 0xB5623E), toon("slab", 0xC9C2B4), toon("door", 0x3F6E8A)
    cement, water, pipe = toon("cement", 0xBDB8AC), toon("tankWater", 0x86BAC6), toon("pipe", 0x5A5F66)
    o = [box("room", (0.15, 0.13, 0.11), (0, 0.03, 0.055), brick, bevel=0.004),
         box("roof", (0.17, 0.15, 0.014), (0, 0.03, 0.117), roof, bevel=0.003),
         box("door", (0.045, 0.006, 0.075), (-0.035, -0.036, 0.04), door),
         cylinder("pipe", 0.009, 0.009, 0.05, (0.035, -0.06, 0.065), pipe, segs=10, rot=Matrix.Rotation(math.radians(90), 3, "X")),
         box("tank", (0.11, 0.07, 0.035), (0.035, -0.115, 0.0175), cement, bevel=0.003),
         box("water", (0.095, 0.055, 0.004), (0.035, -0.115, 0.033), water, outline=False),
         sphere("gush", 0.016, (0.035, -0.092, 0.05), toon("gush", 0xD6EEF4), scale=(0.8, 1.2, 1.4), segs=8, rings=5, outline=False),
         box("channel", (0.028, 0.2, 0.01), (0.035, -0.25, 0.005), cement),
         box("channelWater", (0.018, 0.2, 0.004), (0.035, -0.25, 0.011), water, outline=False)]
    return group(o, scale=BUILDING_SCALE)


def model_chhappar():
    """A farmstead's cattle shelter: a low mud back wall, posts in front, a thick sloping thatch with a ragged edge, a manger."""
    wood, thatch, dark, mud = toon("wood", WOOD), toon("thatch", THATCH), toon("thatchDark", THATCH_DARK), toon("mud", 0xA8784A)
    o = [box("backWall", (0.2, 0.025, 0.13), (0, 0.06, 0.065), mud, bevel=0.004)]
    for x in (-0.085, 0.085):
        o.append(box("post%d" % (x > 0), (0.012, 0.012, 0.075), (x, -0.06, 0.0375), wood, outline=False))
    o.append(poly_mesh("roof", [(-0.11, -0.085, 0.07), (0.11, -0.085, 0.07), (0.11, 0.08, 0.15), (-0.11, 0.08, 0.15),
                                (-0.11, -0.085, 0.086), (0.11, -0.085, 0.086), (0.11, 0.08, 0.166), (-0.11, 0.08, 0.166)],
                       [(0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)], thatch))
    import random
    rnd = random.Random(8)
    bb = Batch("fringe")
    for i in range(14):
        x = -0.11 + i * 0.017
        bb.cone(dark, 0.007, 0.0, rnd.uniform(0.02, 0.03), Matrix.Translation((x, -0.085, 0.072)) @ Matrix.Rotation(math.pi, 4, "X"), segs=4)
    o += bb.objects()
    o.append(box("manger", (0.15, 0.03, 0.025), (0, 0.03, 0.0125), mud, bevel=0.004))
    return group(o, scale=BUILDING_SCALE)


def model_corner_stone():
    """A boundary stone at a corner of your land: a short whitewashed stone with a red-ochre top."""
    o = [box("stone", (0.028, 0.028, 0.05), (0, 0, 0.025), toon("lime", 0xF2EBDD), bevel=0.005),
         box("top", (0.03, 0.03, 0.012), (0, 0, 0.047), toon("ochreRed", 0xC2452D), bevel=0.004)]
    return group(o, scale=1.6)


def model_bund_worn(axis):
    """A neighbour's old bund: low, rounded and grassed over, so your fresh ones stand out against it."""
    b = Batch("bundWorn")
    earth, top, tuft = toon("bundWorn", 0xA08A5C), toon("bundWornTop", 0x86AE4C), toon("bundWornTuft", 0x74A040)
    size = (1.0, 0.06, 0.016) if axis == "x" else (0.06, 1.0, 0.016)
    b.box(earth, size, Matrix.Translation((0, 0, 0.008)))
    b.box(top, (size[0] * 0.98, size[1] * 0.9, 0.006), Matrix.Translation((0, 0, 0.017)))
    import random
    rnd = random.Random(61 if axis == "x" else 62)
    for i in range(10):
        u = rnd.uniform(-0.46, 0.46)
        x, y = (u, 0) if axis == "x" else (0, u)
        b.cone(tuft, 0.016, 0.0, 0.03, Matrix.Translation((x, y, 0.016)), segs=5)
    return b.objects(outline_for=(earth,))


SPRITES.update({"crop_mustard_g2": lambda: model_crop("mustard", "g3"), "crop_sugarcane_g2": lambda: model_crop("sugarcane", "g3"),
                "ground_scrub": model_ground_scrub, "tree_babul": model_babul, "tubewell": model_tubewell, "chhappar": model_chhappar,
                "corner_stone": model_corner_stone, "bund_worn_x": lambda: model_bund_worn("x"), "bund_worn_y": lambda: model_bund_worn("y")})


# ---------------------------------------------------------------- framing and render

# ---------------------------------------------------------------- your people's homes (spec 014, US-212)

HOUSE_SCALE = 0.70       # times BUILDING_SCALE: two homes with their courtyards and open ground between share an acre (owner, 2026-10-24)


def model_family_house(stage):
    """A family house: a two-room pucca house, lime-washed over brick with an ochre dado, a flat roof with a parapet and a
    terracotta-tiled verandah on two posts; in front a low courtyard wall (aangan) with a gate gap, a tulsi, a charpai and pots.
    stage 'done' or 'building' (the plinth, half-raised brick walls, bamboo scaffolding, a brick stack and a sand heap)."""
    o = []
    w, d = 0.31, 0.18                       # the house, its front (door side) towards -y: compact, doors full height
    cy = 0.06                               # the house sits at the back of its plot; the courtyard is in front
    ay0, ay1 = -0.17, cy - d / 2            # a shallow courtyard from the front wall to the house
    lime, dado = toon("famLime", 0xF3E9D6), toon("famDado", 0xC9794A)
    brick, plinth = toon("brick", 0xB5653E), toon("plinth", PLINTH)
    yard = toon("aangan", 0xD8BF92)
    o.append(box("yard", (w + 0.04, ay1 - ay0, 0.006), (0, (ay0 + ay1) / 2, 0.003), yard, outline=False))
    o.append(box("plinth", (w + 0.03, d + 0.03, 0.035), (0, cy, 0.0175), plinth, bevel=0.005))
    # The courtyard wall: three sides, a gap for the gate in the middle of the front.
    wall = brick if stage == "building" else toon("yardWall", 0xE6D6B6)
    cap = toon("wallCap", 0xB98A55)
    for sx in (-1, 1):
        o.append(box("sideWall%d" % sx, (0.018, ay1 - ay0, 0.05), (sx * (w / 2 + 0.01), (ay0 + ay1) / 2, 0.025), wall))
        o.append(box("frontWall%d" % sx, (w / 2 - 0.045, 0.018, 0.05), (sx * (w / 4 + 0.032), ay0, 0.025), wall))
        if stage == "done":
            o.append(box("cap%d" % sx, (w / 2 - 0.04, 0.024, 0.008), (sx * (w / 4 + 0.032), ay0, 0.054), cap, outline=False))
            o.append(box("gatePost%d" % sx, (0.022, 0.022, 0.075), (sx * 0.045, ay0, 0.0375), toon("famLime", 0xF3E9D6)))
    if stage == "building":
        # Half-raised brick walls on the plinth, bamboo poles round them, bricks and sand waiting.
        for sx in (-1, 1):
            o.append(box("wallSide%d" % sx, (0.02, d, 0.08), (sx * (w / 2 - 0.01), cy, 0.075), brick))
        o.append(box("wallBack", (w, 0.02, 0.10), (0, cy + d / 2 - 0.01, 0.085), brick))
        o.append(box("wallFrontL", (0.12, 0.02, 0.06), (-0.11, cy - d / 2 + 0.01, 0.065), brick))
        o.append(box("wallFrontR", (0.08, 0.02, 0.07), (0.13, cy - d / 2 + 0.01, 0.07), brick))
        bam = toon("bamboo", BAMBOO)
        for x in (-w / 2 - 0.02, 0.0, w / 2 + 0.02):
            for y in (cy - d / 2 - 0.02, cy + d / 2 + 0.02):
                o.append(cylinder("pole%d%d" % (int(x * 100), int(y * 100)), 0.004, 0.004, 0.2, (x, y, 0.1), bam, segs=6))
        for y in (cy - d / 2 - 0.02, cy + d / 2 + 0.02):
            o.append(cylinder("rail%d" % int(y * 100), 0.003, 0.003, w + 0.04, (0, y, 0.15), bam, segs=6, rot=Matrix.Rotation(math.radians(90), 3, "Y")))
        for i in range(3):
            for j in range(2):
                o.append(box("bricks%d%d" % (i, j), (0.05, 0.025, 0.018), (-0.09 + j * 0.055, -0.12, 0.009 + i * 0.018), brick))
        o.append(dome("sand", 0.045, 0.032, (0.09, -0.12, 0.006), toon("sand", 0xE2C98E)))
        return group(o, scale=BUILDING_SCALE * HOUSE_SCALE)
    # The house: walls with a dado, a flat roof and parapet, two doors and a window, a tiled verandah on two posts.
    o.append(box("walls", (w, d, 0.15), (0, cy, 0.035 + 0.075), lime))
    o.append(box("dado", (w + 0.004, d + 0.004, 0.04), (0, cy, 0.055), dado, outline=False))
    o.append(box("roof", (w + 0.012, d + 0.012, 0.016), (0, cy, 0.193), toon("roofSlab", 0xD8CDB8)))
    o.append(box("parapet", (w + 0.016, d + 0.016, 0.022), (0, cy, 0.212), toon("famLimeShade", 0xE2D5BC)))
    doorm = toon("famDoor", 0x2F6FA8)
    for x in (-0.075, 0.075):                       # two rooms, a door each
        o.append(box("door%d" % (x > 0), (0.055, 0.010, 0.10), (x, cy - d / 2 - 0.004, 0.085), doorm))
    o.append(box("window", (0.045, 0.010, 0.04), (0.0, cy - d / 2 - 0.004, 0.12), toon("wood_dark", WOOD_DARK)))
    back_details(o, w, d, 0.12, toon("wood_dark", WOOD_DARK))
    for part in o[-3:]:
        part.location.y += cy
    # The verandah: a sloped tile roof over the front, on two wooden posts.
    vy = cy - d / 2 - 0.045
    for x in (-w / 2 + 0.02, w / 2 - 0.02):
        o.append(cylinder("post%d" % (x > 0), 0.007, 0.007, 0.13, (x, vy - 0.02, 0.10), toon("wood", WOOD), segs=8))
    o.append(box("veranda", (w + 0.03, 0.10, 0.012), (0, vy + 0.005, 0.172), toon("tile", TILE),
                 rot=Matrix.Rotation(math.radians(-14), 3, "X")))
    o.append(box("verandaFloor", (w, 0.08, 0.012), (0, vy + 0.01, 0.041), plinth, outline=False))
    # In the courtyard: a tulsi and pots by the door (the charpai stands out on the open ground of the acre).
    o.append(box("tulsiBase", (0.035, 0.035, 0.045), (-0.10, -0.13, 0.0225), toon("famLime", 0xF3E9D6), bevel=0.004))
    o.append(sphere("tulsi", 0.022, (-0.10, -0.13, 0.064), toon("tulsiLeaf", 0x3F8A3A), segs=10, rings=6))
    o.extend(matka("pot1", (0.12, -0.13, 0.006)))
    # A black water tank on the roof, and washing on a line across the courtyard.
    o.append(cylinder("tank", 0.03, 0.03, 0.045, (-0.11, cy + 0.03, 0.245), toon("tank", 0x2E2E2E), segs=14))
    for x in (-w / 2 + 0.01, w / 2 - 0.01):
        o.append(cylinder("linePost%d" % (x > 0), 0.003, 0.003, 0.09, (x, -0.15, 0.045), toon("wood", WOOD), segs=6))
    o.append(cylinder("line", 0.0012, 0.0012, w - 0.02, (0, -0.15, 0.088), toon("rope", 0xE3CFA0), segs=6, outline=False,
                      rot=Matrix.Rotation(math.radians(90), 3, "Y")))
    for i, (x, c) in enumerate(((-0.03, 0xE2582C), (0.02, 0xF2C94C), (0.07, 0x2E6FBA))):
        o.append(box("cloth%d" % i, (0.035, 0.004, 0.03), (x, -0.15, 0.07), toon("cloth%d" % i, c), outline=False))
    return group(o, scale=BUILDING_SCALE * HOUSE_SCALE)


MILL_SCALE = 0.92        # times BUILDING_SCALE: the chakki shed at the back of its acre, its yard open in front (spec 016 §7)


def sack(name, loc, mat, rot=0.0):
    """A filled jute sack lying on its side: a soft box, tied at one end."""
    o = [box(name, (0.05, 0.032, 0.026), loc, mat, rot_z=rot, bevel=0.008)]
    o.append(box(name + "Tie", (0.008, 0.02, 0.016), (loc[0] + 0.028 * math.cos(rot), loc[1] + 0.028 * math.sin(rot), loc[2]), toon("jute", 0x9C7A4C),
                 rot_z=rot, outline=False))
    return o


def model_mill(stage):
    """A village atta chakki (spec 016 §3.1): a lime-washed brick shed with a maroon band and a yellow signboard, its rolling
    shutter up; inside, the stone chakki under its hopper, belt-driven by a blue electric motor, a steel flour tray at its spout;
    outside, jute sacks waiting, a platform scale, a bench for people waiting their turn, and the electric pole that feeds it.
    stage 'done' or 'building' (the plinth, half-raised walls, bamboo, bricks and the chakki in its crate)."""
    o = []
    w, d = 0.34, 0.22                        # the shed, its open front towards -y, at the back of the acre
    cy = 0.08
    lime, band = toon("millLime", 0xF1E7D2), toon("millBand", 0x8E2F2A)
    brick, plinth = toon("brick", 0xB5653E), toon("plinth", PLINTH)
    jute = toon("sack", 0xC7A06A)
    yard_pad(o, w + 0.18, 0.22, -0.14)
    o.append(box("plinth", (w + 0.03, d + 0.03, 0.035), (0, cy, 0.0175), plinth, bevel=0.005))
    if stage == "building":
        for sx in (-1, 1):
            o.append(box("wallSide%d" % sx, (0.02, d, 0.09), (sx * (w / 2 - 0.01), cy, 0.08), brick))
        o.append(box("wallBack", (w, 0.02, 0.11), (0, cy + d / 2 - 0.01, 0.09), brick))
        bam = toon("bamboo", BAMBOO)
        for x in (-w / 2 - 0.02, w / 2 + 0.02):
            for y in (cy - d / 2 - 0.02, cy + d / 2 + 0.02):
                o.append(cylinder("pole%d%d" % (int(x * 100), int(y * 100)), 0.004, 0.004, 0.21, (x, y, 0.105), bam, segs=6))
        crate = toon("crate", 0xB98A55)
        o.append(box("crate", (0.10, 0.08, 0.08), (0.12, -0.10, 0.04), crate, bevel=0.003))
        for z in (0.02, 0.06):
            o.append(box("slat%.2f" % z, (0.102, 0.082, 0.008), (0.12, -0.10, z), toon("crateDark", 0x8A6238), outline=False))
        for i in range(3):
            for j in range(2):
                o.append(box("bricks%d%d" % (i, j), (0.05, 0.025, 0.018), (-0.12 + j * 0.055, -0.10, 0.009 + i * 0.018), brick))
        o.append(dome("sand", 0.045, 0.032, (-0.02, -0.15, 0.006), toon("sand", 0xE2C98E)))
        return group(o, scale=BUILDING_SCALE * MILL_SCALE)
    # The shed: back and side walls, an open front with the shutter rolled up under a maroon band and the signboard.
    h = 0.17
    o.append(box("back", (w, 0.02, h), (0, cy + d / 2 - 0.01, 0.035 + h / 2), lime))
    for sx in (-1, 1):
        o.append(box("side%d" % sx, (0.02, d, h), (sx * (w / 2 - 0.01), cy, 0.035 + h / 2), lime))
        o.append(box("pier%d" % sx, (0.04, 0.03, h), (sx * (w / 2 - 0.02), cy - d / 2 + 0.015, 0.035 + h / 2), lime))
    o.append(box("bandFront", (w + 0.004, 0.03, 0.03), (0, cy - d / 2 + 0.015, 0.035 + h + 0.012), band, outline=False))
    o.append(box("shutter", (w - 0.08, 0.035, 0.025), (0, cy - d / 2 + 0.01, 0.035 + h - 0.045), toon("shutter", 0x8C949A)))
    o.append(box("floor", (w - 0.04, d - 0.03, 0.006), (0, cy + 0.01, 0.038), toon("concrete", CONCRETE), outline=False))
    o.append(box("roof", (w + 0.03, d + 0.05, 0.012), (0, cy - 0.01, 0.035 + h + 0.008), toon("tin", TIN), rot=Matrix.Rotation(math.radians(7), 3, "X")))
    for i in range(9):
        o.append(box("rib%d" % i, (0.005, d + 0.05, 0.004), (-w / 2 + 0.02 + i * 0.04, cy - 0.01, 0.035 + h + 0.015), toon("tinRib", TIN_RIB),
                     rot=Matrix.Rotation(math.radians(7), 3, "X"), outline=False))
    # The signboard on the fascia above the opening: yellow, a red stripe, a painted millstone.
    o.append(box("sign", (0.22, 0.008, 0.05), (0, cy - d / 2 - 0.012, 0.035 + h - 0.005), toon("millSign", 0xF2C94C)))
    o.append(box("signStripe", (0.22, 0.009, 0.01), (0, cy - d / 2 - 0.013, 0.035 + h - 0.024), toon("millSignRed", 0xC2412D), outline=False))
    o.append(cylinder("signWheel", 0.014, 0.014, 0.004, (-0.08, cy - d / 2 - 0.017, 0.035 + h - 0.002), toon("stoneSign", STONE_DARK), segs=14,
                      rot=Matrix.Rotation(math.radians(90), 3, "X"), outline=False))
    # The chakki: a stone base, two millstones in their housing, a hopper above, a spout into a steel tray.
    stone, stone_d = toon("stone", STONE), toon("stoneDark", STONE_DARK)
    mx, my = -0.05, cy - 0.03                 # near the opening, so the chakki reads from outside
    o.append(box("chakkiBase", (0.09, 0.09, 0.05), (mx, my, 0.065), stone_d, bevel=0.004))
    o.append(cylinder("housing", 0.045, 0.045, 0.05, (mx, my, 0.115), toon("housing", 0xD9D4C8), segs=18, smooth=True))
    o.append(cylinder("housingRim", 0.048, 0.048, 0.006, (mx, my, 0.14), toon("steelDark", 0x7E858B), segs=18, outline=False))
    o.append(poly_mesh("hopper", [(mx - 0.035, my - 0.035, 0.20), (mx + 0.035, my - 0.035, 0.20), (mx + 0.035, my + 0.035, 0.20), (mx - 0.035, my + 0.035, 0.20),
                                  (mx - 0.01, my - 0.01, 0.15), (mx + 0.01, my - 0.01, 0.15), (mx + 0.01, my + 0.01, 0.15), (mx - 0.01, my + 0.01, 0.15)],
                       [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)], toon("tinHopper", 0xA9B1B6)))
    o.append(box("spout", (0.02, 0.04, 0.012), (mx, my - 0.06, 0.085), toon("tinHopper", 0xA9B1B6), outline=False))
    o.append(cylinder("tray", 0.03, 0.026, 0.018, (mx, my - 0.085, 0.048), toon("steel", 0xC9CDD2), segs=16))
    o.append(cylinder("flour", 0.026, 0.026, 0.004, (mx, my - 0.085, 0.057), toon("flour", 0xF6EFDF), segs=16, outline=False))
    # The motor and its flat belt to the chakki's pulley.
    o.append(cylinder("motor", 0.025, 0.025, 0.06, (0.10, my + 0.02, 0.07), toon("motor", 0x2E6FBA), segs=14, smooth=True, rot=Matrix.Rotation(math.radians(90), 3, "X")))
    o.append(box("motorFoot", (0.07, 0.05, 0.012), (0.10, my + 0.02, 0.044), toon("steelDark", 0x7E858B)))
    o.append(cylinder("pulley", 0.02, 0.02, 0.012, (mx + 0.05, my + 0.02, 0.07), toon("steelDark", 0x7E858B), segs=12, rot=Matrix.Rotation(math.radians(90), 3, "X")))
    for z in (0.052, 0.088):
        o.append(box("belt%.3f" % z, (0.10, 0.006, 0.004), (0.025 + mx / 2 + 0.03, my + 0.02, z), toon("belt", 0x2B2B2B), outline=False))
    # Sacks inside, along the back wall: grain waiting and atta ground.
    for i, (x, y) in enumerate(((0.10, cy + 0.08), (0.135, cy + 0.075), (0.12, cy + 0.07))):
        o.extend(sack("inSack%d" % i, (x, y, 0.055 + (0.025 if i == 2 else 0.0)), jute, rot=math.radians(10 * i)))
    # Outside: farmers' sacks on the yard, a platform scale, a bench and the electric pole.
    for i, (x, y, r) in enumerate(((-0.17, -0.10, 0.2), (-0.13, -0.13, -0.3), (-0.155, -0.12, 1.2))):
        o.extend(sack("yardSack%d" % i, (x, y, 0.016 + (0.024 if i == 2 else 0.0)), jute, rot=r))
    o.append(box("scale", (0.06, 0.05, 0.012), (0.12, -0.10, 0.006), toon("scaleBase", 0x4A4A4A)))
    o.append(box("scaleTop", (0.065, 0.055, 0.004), (0.12, -0.10, 0.014), toon("steel", 0xC9CDD2), outline=False))
    o.append(box("scalePost", (0.008, 0.008, 0.07), (0.15, -0.08, 0.045), toon("scaleBase", 0x4A4A4A)))
    o.append(cylinder("scaleDial", 0.014, 0.014, 0.005, (0.15, -0.086, 0.08), toon("dial", 0xF7F4EC), segs=12, rot=Matrix.Rotation(math.radians(90), 3, "X")))
    wood = toon("wood", WOOD)
    o.append(box("bench", (0.11, 0.03, 0.008), (0.02, -0.17, 0.035), wood))
    for x in (-0.03, 0.07):
        o.append(box("benchLeg%d" % (x > 0), (0.008, 0.025, 0.032), (x, -0.17, 0.016), toon("wood_dark", WOOD_DARK)))
    o.append(cylinder("pole", 0.006, 0.007, 0.32, (0.22, cy + 0.02, 0.16), toon("pole", 0x8F877C), segs=8))
    o.append(box("crossarm", (0.07, 0.008, 0.008), (0.22, cy + 0.02, 0.305), toon("pole", 0x8F877C)))
    o.append(cylinder("wire", 0.0015, 0.0015, 0.08, (0.19, cy + 0.02, 0.26), toon("belt", 0x2B2B2B), segs=4, outline=False,
                      rot=Matrix.Rotation(math.radians(55), 3, "Y")))
    back_details(o, w, d, 0.13, toon("wood_dark", WOOD_DARK))
    for part in o[-3:]:
        part.location.y += cy
    return group(o, scale=BUILDING_SCALE * MILL_SCALE)


for _f, _deg in FACINGS.items():
    SPRITES["flour_mill_%s" % _f] = (lambda d=_deg: (lambda: refit(model_mill("done"), d)))()
    SPRITES["flour_mill_building_%s" % _f] = (lambda d=_deg: (lambda: refit(model_mill("building"), d)))()


def portrait(kind):
    """A card portrait: the villager from the shoulders up, framed as an icon."""
    objs = model_person(kind, "front")
    low = ("chappal", "skirt", "hem", "pleat", "leg", "kurtahem", "dhoti", "fold", "shorts", "stick", "handle", "blade", "basket", "veg")
    keep = []
    for o in objs:
        if any(o.name.startswith(n) for n in low):
            bpy.data.objects.remove(o, do_unlink=True)
        else:
            keep.append(o)
    return keep


def upright(tilt=0.15):
    """facing_camera(), turned about the face's normal so the model's +y is the screen's up (faces and smiles read upright)."""
    up = Vector((-0.3536, 0.3536, 0.8660))      # the camera's up in the world: Rz(45°) Rx(60°) (0, 1, 0)
    m = facing_camera(tilt)
    best = max(range(0, 360, 2), key=lambda a: (m @ Matrix.Rotation(math.radians(a), 4, "Z")).to_3x3() @ Vector((0, 1, 0)) @ up)
    return m @ Matrix.Rotation(math.radians(best), 4, "Z")


def icon_people(kind):
    """Population: three villagers' heads and shoulders; happiness: a round marigold-yellow smiling face. Built facing +z, top +y."""
    o = []
    dark = toon("smileDark", 0x4A2A16)
    if kind == "population":
        for i, (x, y, sk, shirt) in enumerate(((-0.075, 0.015, 0xA86E4A, 0x86B9DA), (0.075, 0.015, 0xB37853, 0x3E8E5A), (0.0, -0.025, 0xB07A52, 0xF2C14E))):
            z = 0.03 if i == 2 else 0.0               # the one in front is nearer
            o.append(sphere("pbody%d" % i, 0.055, (x, y - 0.075, z - 0.01), toon("pshirt%d" % i, shirt), scale=(1.1, 0.75, 0.6), segs=16, rings=10))
            o.append(sphere("phair%d" % i, 0.047, (x, y + 0.012, z - 0.012), toon("phair", 0x2B1D16), segs=16, rings=10))
            o.append(sphere("face%d" % i, 0.043, (x, y, z), toon("pface%d" % i, sk), segs=16, rings=10))
            for sx in (-1, 1):
                o.append(sphere("peye%d%d" % (i, sx), 0.006, (x + sx * 0.015, y + 0.004, z + 0.04), dark, scale=(0.8, 1.2, 0.5), segs=8, rings=5, outline=False))
            o.append(sphere("psmile%d" % i, 0.007, (x, y - 0.018, z + 0.04), toon("mouth", 0x7A2E26), scale=(1.8, 0.7, 0.5), segs=8, rings=5, outline=False))
        return turn(o, upright())
    face = toon("smile", 0xF6C744)
    o.append(cylinder("disc", 0.10, 0.10, 0.03, (0, 0, 0), face, segs=32))
    o.append(cylinder("discRim", 0.10, 0.10, 0.032, (0, 0, -0.004), toon("smileRim", 0xE0A21C), segs=32, outline=False))
    for sx in (-1, 1):
        o.append(sphere("eye%d" % sx, 0.013, (sx * 0.035, 0.028, 0.016), dark, scale=(0.8, 1.25, 0.4), segs=10, rings=6, outline=False))
        o.append(sphere("cheek%d" % sx, 0.016, (sx * 0.062, -0.014, 0.015), toon("cheek", 0xF08A5D), scale=(1, 0.7, 0.3), segs=10, rings=6, outline=False))
    for i in range(9):
        a = math.radians(205 + i * 16.25)        # the lower arc: a smile
        o.append(sphere("smile%d" % i, 0.008, (math.cos(a) * 0.05, math.sin(a) * 0.05 + 0.002, 0.016), dark, scale=(1, 1, 0.4), segs=8, rings=5, outline=False))
    return turn(o, upright(0.3))


for _f, _deg in FACINGS.items():
    SPRITES["family_house_%s" % _f] = (lambda d=_deg: (lambda: refit(model_family_house("done"), d)))()
    SPRITES["family_house_building_%s" % _f] = (lambda d=_deg: (lambda: refit(model_family_house("building"), d)))()
for _k in ("founder", "hand_m", "hand_f", "town_m", "town_f", "boy", "girl", "elder_m", "elder_f"):
    ICONS["portrait_" + _k] = (lambda k=_k: (lambda: portrait(k)))()
ICONS["icon_population"] = lambda: icon_people("population")
ICONS["icon_happiness"] = lambda: icon_people("happiness")
SPRITES.update(ICONS)
TOWN_PEOPLE = ("town_m", "town_f", "boy", "girl", "elder_m", "elder_f")


def frame_and_render(sc, cam, name, objs):
    """Fits the frame to the model, renders it, and returns the pivot (the world origin) in normalised image coordinates."""
    dg = bpy.context.evaluated_depsgraph_get()
    inv = cam.matrix_world.inverted()
    xs, ys = [], []
    for o in objs:
        ev = o.evaluated_get(dg)
        for v in ev.data.vertices:
            c = inv @ (ev.matrix_world @ v.co)
            xs.append(c.x)
            ys.append(c.y)
    o0 = inv @ Vector((0, 0, 0))
    xs.append(o0.x)
    ys.append(o0.y)
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    w = int(math.ceil((x1 - x0) * PX_PER_UNIT)) + 2 * MARGIN_PX
    h = int(math.ceil((y1 - y0) * PX_PER_UNIT)) + 2 * MARGIN_PX
    w += w % 2
    h += h % 2
    sc.render.resolution_x, sc.render.resolution_y = w, h
    cam.data.ortho_scale = max(w, h) / PX_PER_UNIT
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    # Move the camera within its own plane so the model's bounds sit in the middle of the frame.
    cam.location = (0, 0, 0)
    back = cam.matrix_world.to_3x3() @ Vector((0, 0, 10))
    cam.location = cam.matrix_world.to_3x3() @ Vector((cx, cy, 0)) + back
    bpy.context.view_layer.update()
    sc.render.filepath = os.path.join(OUT_DIR, name + ".png")
    bpy.ops.render.render(write_still=True)
    px = (o0.x - cx) * PX_PER_UNIT / w + 0.5
    py = (o0.y - cy) * PX_PER_UNIT / h + 0.5
    return {"w": w, "h": h, "pivotX": round(px, 5), "pivotY": round(py, 5)}


def frame_icon(sc, cam, name, objs):
    """Icons: the model's bounds fitted into a square of ICON_PX with a margin, centred."""
    dg = bpy.context.evaluated_depsgraph_get()
    inv = cam.matrix_world.inverted()
    xs, ys = [], []
    for o in objs:
        ev = o.evaluated_get(dg)
        for v in ev.data.vertices:
            c = inv @ (ev.matrix_world @ v.co)
            xs.append(c.x)
            ys.append(c.y)
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    span = max(x1 - x0, y1 - y0) * 1.12
    sc.render.resolution_x = sc.render.resolution_y = ICON_PX
    cam.data.ortho_scale = span
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    cam.location = (0, 0, 0)
    back = cam.matrix_world.to_3x3() @ Vector((0, 0, 10))
    cam.location = cam.matrix_world.to_3x3() @ Vector((cx, cy, 0)) + back
    # Outlines keep their on-screen width whatever the icon's size.
    for o in objs:
        for mod in o.modifiers:
            if mod.type == "SOLIDIFY":
                mod.thickness = mod.thickness * span / 0.45
    bpy.context.view_layer.update()
    sc.render.filepath = os.path.join(OUT_DIR, name + ".png")
    bpy.ops.render.render(write_still=True)
    return {"w": ICON_PX, "h": ICON_PX, "pivotX": 0.5, "pivotY": 0.5}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = argv or list(SPRITES)
    os.makedirs(OUT_DIR, exist_ok=True)
    manifest_path = os.path.join(OUT_DIR, "sprites.json")
    manifest = {}
    if os.path.exists(manifest_path):
        with open(manifest_path) as f:
            manifest = {s["name"]: s for s in json.load(f)["sprites"]}
    for name in names:
        sc, cam = reset()
        _materials.clear()
        objs = SPRITES[name]()
        info = frame_icon(sc, cam, name, objs) if name in ICONS else frame_and_render(sc, cam, name, objs)
        info["name"] = name
        manifest[name] = info
        print("rendered", name, info)
    with open(manifest_path, "w") as f:
        json.dump({"pixelsPerUnit": 132 * SCALE, "sprites": [manifest[k] for k in sorted(manifest)]}, f, indent=1)
        f.write("\n")


main()
