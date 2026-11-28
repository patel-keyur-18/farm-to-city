"""
Spec 032 (the realistic valley): the game's sprites rendered realistic at 8x. No baked shadows (the owner, 2026-11-27, after the device check: ADR 0183).

US-283 (the device check) rendered the family house (4 facings), here, and its wheat acre, now in real_crops.py with the other crops
(US-274). The people and portraits are in real_people.py (US-276), the animals, vehicles and machines in real_animals.py (US-277).
Later stories add their categories to SPRITES below.

Run headless from the repository root:
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P tools/blender/render_real.py -- [names]
With no names every sprite here is rendered. Output: unity/FarmToCity/Assets/Resources/Art/Blender/<name>.png
and an entry for each in sprites.json carrying its own `ppu` (the manifest's global pixelsPerUnit stays today's until every sprite is
re-rendered: then it becomes 1,056 and the per-sprite field goes). FTC_ART_OUT sends the files somewhere else (a preview).
"""
import importlib.util
import json
import math
import os
import random
import sys

os.environ["FTC_SCALE"] = "8"   # render.py reads it at import: 1,056 px an acre (spec 032 §3.2)

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location("samples", os.path.join(HERE, "samples.py"))
S = importlib.util.module_from_spec(_spec)
sys.modules["samples"] = S
_spec.loader.exec_module(S)
R = S.R
_gspec = importlib.util.spec_from_file_location("real_ground", os.path.join(HERE, "real_ground.py"))
G = importlib.util.module_from_spec(_gspec)
sys.modules["real_ground"] = G
_gspec.loader.exec_module(G)
_cspec = importlib.util.spec_from_file_location("real_crops", os.path.join(HERE, "real_crops.py"))
C = importlib.util.module_from_spec(_cspec)
sys.modules["real_crops"] = C
_cspec.loader.exec_module(C)
_tspec = importlib.util.spec_from_file_location("real_trees", os.path.join(HERE, "real_trees.py"))
T = importlib.util.module_from_spec(_tspec)
sys.modules["real_trees"] = T
_tspec.loader.exec_module(T)
_aspec = importlib.util.spec_from_file_location("real_animals", os.path.join(HERE, "real_animals.py"))
A = importlib.util.module_from_spec(_aspec)
sys.modules["real_animals"] = A
_aspec.loader.exec_module(A)
_pspec = importlib.util.spec_from_file_location("real_people", os.path.join(HERE, "real_people.py"))
P = importlib.util.module_from_spec(_pspec)
sys.modules["real_people"] = P
_pspec.loader.exec_module(P)

OUT = os.environ.get("FTC_ART_OUT") or os.path.join(HERE, "..", "..", "unity", "FarmToCity", "Assets", "Resources", "Art", "Blender")
R.OUT_DIR = OUT
S.OUT = OUT
PPU = 132 * R.SCALE
SUN_ELEVATION = 60.0     # degrees (spec 032 §3.1); azimuth as the game's other art


# ---------------------------------------------------------------- the scene

def start():
    """The realistic scene (Cycles, AgX, soft sun and sky) with the sun at 60 degrees."""
    sc, cam = S.start("real")
    G.reset()
    G.install(R)
    _real_toon = R.toon

    def toon(name, hexv, soft=False):
        """The edge tiles' tops (`flatgrass` ...) are the ground's seamless terrain; everything else is the sample's physically based material."""
        for kind in ("grass", "earth", "soil", "road", "paved"):
            if name == "flat" + kind:
                return G.top_material("grass" if kind in ("road", "paved") else kind, hexv)
        if name == "ridge":
            return G.top_material("ridge", hexv)
        if name in PATCHES:
            return G.patch_material(hexv)
        return _real_toon(name, hexv, soft)

    R.toon = toon

    # Bare patches overlap on the lawn; two coplanar tops make Cycles render the overlap black. Each disc of a batch gets its own tiny step (0.4 mm of an acre's width).
    plain_cone = getattr(R.Batch.cone, "plain", R.Batch.cone)
    steps = {"n": 0}

    def cone(self, mat, r1, r2, depth, m, segs=5):
        if mat.name.startswith("real_patch"):
            steps["n"] += 1
            m = R.Matrix.Translation((0, 0, 0.0004 * (steps["n"] % 40))) @ m
        plain_cone(self, mat, r1, r2, depth, m, segs)

    cone.plain = plain_cone
    R.Batch.cone = cone

    # Furrows: 18 mm ridges leave grooves so deep and narrow that the sky light never reaches them and they render black; a shallower ridge keeps them brown.
    def soil_ridges(b, top_z=0.003):
        for x in R.RIDGE_X:
            b.box(R.toon("ridge", R.SOIL_RIDGE), (0.075, 0.94, 0.010), R.Matrix.Translation((x, 0, top_z + 0.005)))

    R.soil_ridges = soil_ridges
    sun = next(o for o in bpy.data.objects if o.type == "LIGHT")
    horizontal = math.hypot(-0.35, -1.0)
    to_light = Vector((-0.35, -1.0, horizontal * math.tan(math.radians(SUN_ELEVATION)))).normalized()
    sun.rotation_euler = to_light.to_track_quat("Z", "Y").to_euler()
    return sc, cam


# ---------------------------------------------------------------- the subjects

def house(facing):
    objs = S.subject_house_detail()
    return R.refit(objs, R.FACINGS[facing])


def clump(b, rnd, x, y, stage, mats):
    """One clump of wheat at a growth stage: blades (g1, g2), green stalks with green ears (g3) or the ripe clump of the sample (ready)."""
    if stage == "ready":
        return S.wheat_clump(b, rnd, x, y, mats["ripe"])
    green, deep = mats["green"], mats["deep"]
    if stage in ("g1", "g2"):
        h = 0.045 if stage == "g1" else 0.085
        n = 9 if stage == "g1" else 16
        for k in range(n):
            a = k * math.tau / n + rnd.uniform(-0.3, 0.3)
            b.cone(green[k % 2], 0.0045, 0.0, h * rnd.uniform(0.8, 1.2), R.at(x + math.cos(a) * 0.004, y + math.sin(a) * 0.004, 0.02, tilt=0.38 + rnd.uniform(-0.1, 0.1), tilt_dir=a), segs=3)
        return
    for k in range(12):   # g3: tall green stalks, the ears in the boot
        a = k * math.tau / 12 + rnd.uniform(-0.4, 0.4)
        h = rnd.uniform(0.100, 0.135)
        m = R.at(x + math.cos(a) * 0.008, y + math.sin(a) * 0.008, 0.02, tilt=rnd.uniform(0.06, 0.2), tilt_dir=a * 0.3 + 0.8)
        b.cone(green[k % 2], 0.0030, 0.0020, h, m, segs=4)
        ear = mats["ear_green"]
        top = m @ Matrix.Translation((0, 0, h))
        for j in range(5):
            b.sphere(ear, 0.0055, top @ Matrix.Translation((0.0028 * (1 if j % 2 else -1), 0, 0.004 + j * 0.0045)), scale=(0.8, 0.7, 1.45), segs=6, rings=4)
    for k in range(5):
        a = rnd.uniform(0, math.tau)
        R.leaf(b, deep[k % 2], x, y, 0.03 + rnd.uniform(0, 0.04), rnd.uniform(0.05, 0.07), 0.007, a, rnd.uniform(-0.2, 0.35))


def wheat_mats(toon):
    """The wheat's materials, in the colours approved at the device check."""
    return {
        "ripe": ([toon("wheatStalkA", 0xD9B25A), toon("wheatStalkB", 0xE6C266), toon("wheatStalkC", 0xC99A3E)],
                 [toon("wheatEarA", 0xE0A12E), toon("wheatEarB", 0xF0BE48), toon("wheatEarC", 0xCC8A22)],
                 [toon("wheatLeafA", 0xBFA35C), toon("wheatLeafB", 0xA88E48)], toon("wheatAwn", 0xE2B455)),
        "green": [toon("wheatGreenA", 0x7CBF45), toon("wheatGreenB", 0x5FA238)],
        "deep": [toon("wheatDeepA", 0x5FA238), toon("wheatDeepB", 0x4C8C2E)],
        "ear_green": toon("wheatEarGreen", 0xA8CF5A),
    }


SPRITES = {}
for _f in R.FACINGS:
    SPRITES["family_house_" + _f] = lambda f=_f: house(f)


# ---------------------------------------------------------------- the ground and boundaries (US-273): today's models, realistic

GROUND_PREFIXES = ("bund", "corner", "edge", "fence", "grass", "ground", "hedge", "hill", "scrub", "street")
for _n in sorted(R.SPRITES):
    if _n.startswith(GROUND_PREFIXES):
        SPRITES[_n] = R.SPRITES[_n]


# Patterns for the ground sprites' own materials (the sample's table in samples.py has none for them): checked before the sample's, first match wins.
S.PATTERNS[:0] = [
    ("bundtop", ("grass", 110, 0.30)), ("bundworntop", ("grass", 110, 0.30)), ("bundtuft", ("grass", 140, 0.30)), ("bundworntuft", ("grass", 140, 0.30)),
    ("bundworn", ("earth", 90, 0.30)), ("bund", ("earth", 90, 0.30)),
    ("hillbush", ("leaf", 90, 0.35)), ("hilldry", ("earth", 60, 0.35)), ("hill", ("grass", 60, 0.45)), ("rock", ("stone", 70, 0.35)),
    ("hedge", ("leaf", 120, 0.35)), ("mudwall", ("earth", 80, 0.30)), ("gatewood", ("wood", 50, 0.25)), ("bamboo", ("wood", 80, 0.22)),
    ("scrub", ("leaf", 100, 0.40)), ("lantana", ("leaf", 100, 0.40)), ("tuft", ("grass", 160, 0.30)), ("reed", ("grass", 160, 0.30)), ("stem", ("grass", 160, 0.25)),
    ("asphalt", ("stone", 260, 0.25)), ("streetedge", ("stone", 160, 0.25)), ("vergestone", ("stone", 120, 0.30)), ("ghat", ("stone", 120, 0.30)),
    ("lime", ("plaster", 90, 0.18)), ("ochrered", ("plaster", 90, 0.20)),
    ("roadline", ("plaster", 120, 0.12)), ("road", ("earth", 90, 0.30)), ("rut", ("earth", 130, 0.30)), ("path", ("earth", 90, 0.30)), ("pat", ("earth", 90, 0.30)),
    ("worn", ("earth", 90, 0.30)), ("squareearth", ("earth", 90, 0.30)), ("usarpatch", ("earth", 90, 0.30)),
]
S.ROUGH.setdefault("earth", 1.0)


# Bare patches on a lawn: their palette colours are near white, and the sun would make chalk of them (spec 032 §3.1: the colour comes from today's palette).
PATCHES = ("path", "pat", "usarPatch", "scrubSand", "scrubDry", "scrubGreen", "worn", "squareEarth", "road", "rut")


def model_road(links, paved=False):
    """render.py's road acre without overlapping coplanar boxes (the arms start at the centre square's edge): Cycles turns overlapping coplanar tops black."""
    R_toon = R.toon
    o = R.ground_block("road", R.GRASS, R.GRASS_EDGE)
    b = R.Batch("paved" if paved else "road")
    dirt, rut = R_toon("road", R.ROAD), R_toon("rut", R.ROAD_RUT)
    if paved:
        dirt, rut = R_toon("asphalt", R.ASPHALT), R_toon("roadLine", R.ROAD_LINE)
    half = 0.5 + R.SEAM
    w = 0.40
    b.box(dirt, (w, w, 0.004), R.Matrix.Translation((0, 0, 0.003)))
    for bit, (dx, dy) in ((1, (1, 0)), (2, (-1, 0)), (4, (0, 1)), (8, (0, -1))):
        if not links & bit:
            continue
        length = half - w / 2
        cx, cy = dx * (w / 2 + length / 2), dy * (w / 2 + length / 2)
        size = (length, w, 0.004) if dx else (w, length, 0.004)
        b.box(dirt, size, R.Matrix.Translation((cx, cy, 0.003)))
        if paved:
            n = 3
            for i in range(n):
                t = w / 2 + (i + 0.5) / n * length
                ds = (0.11, 0.03, 0.002) if dx else (0.03, 0.11, 0.002)
                b.box(rut, ds, R.Matrix.Translation((dx * t, dy * t, 0.0055)))
            continue
        for k in (-0.09, 0.09):
            rs = (length, 0.035, 0.002) if dx else (0.035, length, 0.002)
            b.box(rut, rs, R.Matrix.Translation((cx + (0 if dx else k), cy + (k if dx else 0), 0.0055)))
    stones = R_toon("vergeStone", 0xC9C2B4)
    for x, y in ((0.36, 0.36), (-0.36, -0.36), (0.36, -0.36)):
        b.sphere(stones, 0.02, R.Matrix.Translation((x, y, 0.008)), scale=(1.2, 1, 0.6), segs=8, rings=5)
    return o + b.objects()


for _m in (3, 12, 11, 4, 8):
    SPRITES["ground_road_%d" % _m] = (lambda m=_m: (lambda: model_road(m)))()
    SPRITES["ground_paved_%d" % _m] = (lambda m=_m: (lambda: model_road(m, True)))()

def model_corner_stone():
    """The boundary stone a little larger: without the toon outline it would draw 6% smaller than today's (spec 032 AC-1: frames within 5%)."""
    o = [R.box("stone", (0.028, 0.028, 0.05), (0, 0, 0.025), R.toon("lime", 0xF2EBDD), bevel=0.005),
         R.box("top", (0.03, 0.03, 0.012), (0, 0, 0.047), R.toon("ochreRed", 0xC2452D), bevel=0.004)]
    return R.group(o, scale=1.7)


SPRITES["corner_stone"] = model_corner_stone


def smooth_plants(objs):
    """Leaves and bushes are built as low-poly blobs for the toon look: smooth-shade them and subdivide once so a realistic render doesn't show facets."""
    for o in objs:
        names = " ".join(sl.material.name.lower() for sl in o.material_slots if sl.material)
        if any(k in names for k in ("hillbush", "hedge", "scrub", "lantana", "bush")) and o.type == "MESH":   # not the tufts: thin cones go black when displaced
            for poly in o.data.polygons:
                poly.use_smooth = True
            if not any(m.type == "SUBSURF" for m in o.modifiers):
                mod = o.modifiers.new("subdiv", "SUBSURF")
                mod.levels = 1
                mod.render_levels = 2
                tex = bpy.data.textures.get("leafy") or bpy.data.textures.new("leafy", "CLOUDS")
                tex.noise_scale = 0.012          # a lumpy, leafy edge instead of a smooth blob
                tex.noise_depth = 2
                disp = o.modifiers.new("leafy", "DISPLACE")
                disp.texture = tex
                disp.strength = 0.006
                disp.mid_level = 0.5
    return objs


def _variant(base, v):
    def build():
        G.VARIANT = v
        try:
            return R.SPRITES[base]()
        finally:
            G.VARIANT = 0
    return build


def _frame(base, f):
    def build():
        G.FRAME = f
        try:
            return R.SPRITES[base]()
        finally:
            G.FRAME = 0
    return build


for _base in ("ground_grass", "ground_earth", "ground_soil", "ground_scrub"):     # three variants of the big kinds, picked per acre by the game
    for _v in (1, 2):
        SPRITES["%s_%d" % (_base, _v)] = _variant(_base, _v)
for _base in ("ground_water", "edge_water"):                                       # the river's flowing frames (12, looping)
    for _f in range(G.WATER_FRAMES):
        SPRITES["%s_%d" % (_base, _f)] = _frame(_base, _f)

# ---------------------------------------------------------------- the crops, the kitchen garden and the planting pit (US-274)

CROP_SPRITES = C.install(R, S, clump, wheat_mats)
SPRITES.update(CROP_SPRITES)

# ---------------------------------------------------------------- the trees (US-275)

TREE_SPRITES = T.install(R, S, C)
SPRITES.update(TREE_SPRITES)


# ---------------------------------------------------------------- people and portraits (US-276)

PEOPLE_SPRITES, PORTRAITS = P.install(R, S)
SPRITES.update(PEOPLE_SPRITES)


# ---------------------------------------------------------------- animals, vehicles and machines (US-277)

ANIMAL_SPRITES = A.install(R, S, P)
SPRITES.update(ANIMAL_SPRITES)


# ---------------------------------------------------------------- main

def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = argv or list(SPRITES) + list(PORTRAITS)
    os.makedirs(OUT, exist_ok=True)
    manifest_path = os.path.join(OUT, "sprites.json")
    with open(manifest_path) as f:
        data = json.load(f)
    manifest = {s["name"]: s for s in data["sprites"]}
    for name in names:
        sc, cam = start()
        if name in PORTRAITS:   # a card picture: its own framing, today's 256 px, no map scale
            info = P.render_portrait(sc, cam, name, PORTRAITS[name], OUT)
            info.update({"name": name, "ppu": data["pixelsPerUnit"]})   # today's scale, said out loud: render.py skips whatever has its own ppu
        else:
            objs = smooth_plants(S.finish(SPRITES[name]()))
            if name in CROP_SPRITES or name in TREE_SPRITES or name in PEOPLE_SPRITES or name in ANIMAL_SPRITES:
                objs = C.hold_frame(name, objs, cam)
            info = R.frame_and_render(sc, cam, name, objs)
            info.update({"name": name, "ppu": PPU})
        manifest[name] = info
        print("rendered", name, info)
    data["sprites"] = [manifest[k] for k in sorted(manifest)]
    with open(manifest_path, "w") as f:
        json.dump(data, f, indent=1)
        f.write("\n")


if __name__ == "__main__":
    main()
