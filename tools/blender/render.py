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
}


def model_person(kind, facing, pose="stand"):
    """A small villager with a big head, built facing -y (the viewer's lower left); facing 'front' or 'back' (walking away, to the upper left)."""
    p = PEOPLE[kind]
    soft = True
    def m(name, hexv):
        return toon(kind + "_" + name, hexv, soft=soft)
    skin, top, bottom, accent = m("skin", p["skin"]), m("top", p["top"]), m("bottom", p["bottom"]), m("accent", p["accent"])
    dark, hair = m("dark", 0x1E1410), m("hair", HAIR)
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
    if female:
        # The pallu: from the right hip across the chest to the left shoulder, falling behind.
        o.append(box("pallu", (0.018, 0.006, 0.058), (0.004, -0.019, 0.082), top, rot=Matrix.Rotation(math.radians(-38), 3, "Y")))
        o.append(box("pallufall", (0.016, 0.006, 0.05), (-0.016, 0.018, 0.075), top))
        o.append(box("palluedge", (0.004, 0.0065, 0.058), (0.012, -0.021, 0.082), accent, outline=False, rot=Matrix.Rotation(math.radians(-38), 3, "Y")))
    if p.get("wear") == "kurta" or p.get("tool") == "hoe":
        o.append(box("gamchha", (0.014, 0.008, 0.04), (-0.016, -0.019, 0.086), accent, rot_z=math.radians(8)))
    # Arms: sleeve, forearm, hand.
    for s in (-1, 1):
        sleeve = body if p["wear"] != "banyan" else skin
        o.append(sphere("shoulderball%d" % s, 0.0085, (s * 0.025, 0, 0.097), sleeve, segs=10, rings=6))
        if female or p["wear"] == "banyan":
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
            o.append(sphere("moustache", 0.0045, (0, -0.0300, hz - 0.0095), hair, scale=(2.0, 0.55, 0.65), segs=8, rings=6, outline=False))
        else:
            o.append(sphere("bindi", 0.0022, (0, -0.0300, hz + 0.0135), m("bindi", 0xC0262D), segs=6, rings=4, outline=False))
    # Hair and headwear.
    o.append(sphere("hair", 0.0328, (0, 0.0045, hz + 0.0065), hair, scale=(1.0, 0.96, 0.82), segs=20, rings=12))
    if female:
        o.append(sphere("bun", 0.0125, (0, 0.031, hz - 0.002), hair, segs=12, rings=8))
        for i, a in enumerate((-50, -15, 20, 55)):
            x, z = math.sin(math.radians(a)) * 0.0125, math.cos(math.radians(a)) * 0.0125
            o.append(sphere("gajra%d" % i, 0.0032, (x, 0.033, hz - 0.002 + z * 0.6), m("jasmine", 0xFFF8E8), segs=6, rings=4, outline=False))
    elif p.get("head") == "pagdi":
        o.append(cylinder("pagdi", 0.0335, 0.031, 0.020, (0, 0.003, hz + 0.019), accent, segs=18, smooth=True))
        o.append(sphere("pagdicrown", 0.027, (0, 0.004, hz + 0.028), accent, scale=(1, 1, 0.5), segs=16, rings=8))
        o.append(box("pagditail", (0.010, 0.004, 0.026), (0.010, 0.030, hz + 0.004), accent))
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
    # Tools.
    if p.get("tool") == "hoe":
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
    return group(o, rot_z=rot, scale=PERSON_SCALE, outline_factor=0.7)


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


def crop_mat(b, stage, young, ripe):
    """A low, leafy band along each ridge so a grown field reads as a carpet rather than stalks on bare soil."""
    if stage == "g1":
        return
    h = {"g2": 0.024, "g3": 0.038, "ready": 0.038}[stage]
    mat = ripe if stage == "ready" else young
    for x in RIDGE_X:
        # A half-buried rounded band along the ridge: an ellipse 0.045 wide and h tall, 0.9 long.
        m = Matrix.Translation((x, -0.45, 0.012)) @ Matrix.Rotation(math.radians(-90), 4, "X") @ Matrix.Diagonal((0.045, h, 1.0, 1.0))
        b.cone(mat, 1.0, 1.0, 0.9, m, segs=12)


CROPS = {"wheat": crop_wheat}
MATS = {"wheat": (0x5FA238, 0xC9A34E)}   # the leafy band under the plants: growing, ready


def model_crop(crop, stage):
    """A crop across a whole acre: plants on the ridges of the tilled soil, a little jitter so it reads as grown, not stamped."""
    import random
    rnd = random.Random(hash((crop, stage)) & 0xFFFF)
    b = Batch(crop)
    per_row = 10
    young, ripe = MATS[crop]
    crop_mat(b, stage, toon(crop + "Mat", young), toon(crop + "MatRipe", ripe))
    for x in RIDGE_X:
        for j in range(per_row):
            y = -0.42 + j * (0.84 / (per_row - 1)) + rnd.uniform(-0.012, 0.012)
            CROPS[crop](b, rnd, x + rnd.uniform(-0.01, 0.01), y, stage)
    return b.objects(outline_for=(), smooth=False)


# ---------------------------------------------------------------- trees (ADR 0041 §8)

def model_mango(stage):
    """A mango tree: a stout trunk with three limbs and a dense dome of dark leaves; in fruit, mangoes hang under the canopy."""
    import random
    rnd = random.Random(7)
    k = {"sapling": 0.30, "young": 0.62, "mature": 1.0, "ripe": 1.0}[stage]
    bark, dark, mid, light = toon("bark", 0x7A4E2E), toon("mangoDark", 0x2F7A35), toon("mangoMid", 0x3F9440), toon("mangoLight", 0x63AE4A)
    o = []
    if stage == "sapling":
        o.append(cylinder("stem", 0.010, 0.006, 0.16, (0, 0, 0.08), bark, segs=6))
        for i in range(5):
            a = i * math.tau / 5
            o.append(sphere("leaf%d" % i, 0.03, (math.cos(a) * 0.03, math.sin(a) * 0.03, 0.15 + (i % 2) * 0.02), mid, scale=(1.4, 0.6, 0.35), segs=8, rings=5))
        o.append(cylinder("stake", 0.004, 0.004, 0.18, (0.03, 0.02, 0.09), toon("bamboo", BAMBOO), segs=5))
        return group(o, scale=BUILDING_SCALE)
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
for _st in STAGES:
    SPRITES["crop_wheat_%s" % _st] = (lambda st=_st: (lambda: model_crop("wheat", st)))()
for _st in ("sapling", "young", "mature", "ripe"):
    SPRITES["tree_mango_%s" % _st] = (lambda st=_st: (lambda: model_mango(st)))()
ICONS = {"icon_coin": icon_coin, "icon_wheat": icon_wheat, "icon_gear": icon_gear}
SPRITES.update(ICONS)


# ---------------------------------------------------------------- framing and render

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
