"""
US-274: the field crops, the kitchen garden's beds and the planting pit, realistic with a full dense canopy (the owner, 2026-11-28).

Each crop keeps today's plant (render.py's CROPS functions: its shapes and colours) with three changes: leaves are pointed, folded blades instead of
ellipsoids; there are about three times as many plants (once grown a second set stands between the ridges, so the field closes over); and the old
toon "mat" bands are a scatter of small leaves. Paddy water is still (the owner). The beds keep their plank design with 5 x 5 plants instead of 3 x 3.
Wheat is not here: its realistic acre was approved at the device check (render_real.wheat).

Every sprite keeps today's frame and anchor (spec 032 AC-1): `hold_frame` widens a smaller model to today's recorded bounds with undrawn points
and reports any model that reaches past them.
"""
import json
import math
import os
import random

import bpy
from mathutils import Matrix, Vector

R = None   # render.py, set by install()
S = None   # samples.py
HERE = os.path.dirname(os.path.abspath(__file__))
MARGIN_UNITS = 8.0 * math.sqrt(2.0) / 132.0   # render.MARGIN_PX in world units (the same at any scale)

CROP_NAMES = ("amaranth", "berseem", "lentils", "maize", "methi", "mustard", "okra", "peas", "potato", "radish", "rice", "sorghum")
FODDER = ("berseem", "sorghum")
# The leaf scatter that closes the canopy: how high it reaches when grown, and the leaf's length. Rice has none (the water shows between the hills).
FILL = {"amaranth": (0.040, 0.028), "berseem": (0.040, 0.018), "lentils": (0.045, 0.018), "maize": (0.050, 0.045), "methi": (0.040, 0.018),
        "mustard": (0.050, 0.030), "okra": (0.050, 0.036), "peas": (0.048, 0.018), "potato": (0.055, 0.028), "sorghum": (0.050, 0.045),
        "sugarcane": (0.080, 0.055), "radish": (0.030, 0.030), "wheat": (0.045, 0.034)}

# What the crops' material names are made of (samples.PATTERNS has none for most of them). Only used while a crop or a bed is built.
PATTERNS = [
    ("pod", ("wheat", 90, 0.12)), ("grain", ("wheat", 90, 0.12)), ("dry", ("wheat", 90, 0.12)), ("stubble", ("wheat", 90, 0.15)), ("haulm", ("wheat", 90, 0.15)),
    ("husk", ("wheat", 90, 0.12)), ("tassel", ("wheat", 90, 0.12)),
    ("potatoridge", ("soil", 160, 0.30)), ("paddybund", ("earth", 90, 0.30)), ("bedsoil", ("soil", 160, 0.30)), ("clod", ("soil", 160, 0.30)), ("basin", ("earth", 110, 0.28)),
    ("plank", ("wood", 28, 0.09)), ("tuber", ("earth", 130, 0.20)), ("mooli", ("plaster", 120, 0.12)),
    ("flower", ("cloth", 420, 0.06)), ("heart", ("cloth", 420, 0.06)), ("silk", ("hair", 600, 0.30)),
]
LEAFY = ("amaranth", "berseem", "lentil", "maize", "methi", "mustard", "okra", "pea", "potato", "radish", "rice", "sorghum", "cane", "spinach", "dhania", "gradish", "fodder")


def real_leaf(b, mat, x, y, z, length, width, direction, tilt, rot=0.0):
    """A pointed leaf blade folded along its midrib and drooping at the tip, in place of render.py's flat ellipsoid (same arguments)."""
    m = Matrix.Translation((x, y, z)) @ Matrix.Rotation(direction, 4, "Z") @ Matrix.Rotation(-tilt, 4, "Y") @ Matrix.Rotation(rot, 4, "X")
    verts, faces = b.parts.setdefault(mat, ([], []))     # samples.FastBatch: plain lists of points and faces, one pair a material
    broad = width > 0.55 * length                          # spinach, potato: a blunt oval blade, hardly folded (a deep fold reads as folded paper)
    fold, droop = width * (0.08 if broad else 0.22), length * 0.12
    profile = ((0.0, 0.10), (0.16, 0.68), (0.36, 0.97), (0.58, 1.0), (0.78, 0.82), (0.92, 0.52), (1.0, 0.0)) if broad else ((0.0, 0.10), (0.22, 0.82), (0.48, 1.0), (0.76, 0.68), (1.0, 0.0))

    def point(v):
        verts.append((m @ Vector(v))[:])
        return len(verts) - 1

    rib, left, right = [], [], []
    for t, w in profile:
        mz = -droop * t * t
        rib.append(point((length * t, 0.0, mz)))
        if w > 0.0:
            left.append(point((length * t, width * 0.5 * w, mz + fold * w)))
            right.append(point((length * t, -width * 0.5 * w, mz + fold * w)))
    for side, flip in ((left, False), (right, True)):
        for i in range(len(side) - 1):
            quad = (rib[i], rib[i + 1], side[i + 1], side[i])
            faces.append(quad[::-1] if flip else quad)
        tip = (rib[-2], rib[-1], side[-1])
        faces.append(tip[::-1] if flip else tip)


def spots(rnd, stage, open_furrows=False):
    """Where the plants of an acre stand: along the seven ridges; from the second stage a second set in the furrows between them, so the canopy closes."""
    on_ridge = {"g1": 17, "g2": 17, "g3": 16, "ready": 16, "cut": 16}[stage]
    between = {"g1": 0, "g2": 9, "g3": 15, "ready": 15, "cut": 15}[stage]
    if open_furrows:
        between = 0
    rows = [(x, on_ridge) for x in R.RIDGE_X] + [(x + 0.065, between) for x in R.RIDGE_X[:-1] if between]
    for x, n in rows:
        for j in range(n):
            y = -0.42 + j * (0.84 / (n - 1)) + rnd.uniform(-0.012, 0.012)
            yield x + rnd.uniform(-0.012, 0.012), y


def fill_canopy(b, rnd, crop, stage, mats, y_from=-0.43):
    """Small leaves scattered between the plants, so a grown field reads as a closed canopy of leaves and not as bare stalks."""
    young, ripe = mats
    top, size = FILL[crop]
    count, high = {"g1": (0, 0.0), "g2": (450, 0.55), "g3": (1300, 1.0), "ready": (1300, 1.0)}[stage]
    if crop == "radish" and stage == "ready":
        count = 0                                                # the ripe roots show between the rosettes
    if crop in ("radish", "potato"):
        count //= 2
    for i in range(count):
        mat = (ripe if stage == "ready" else young)[i % 2]
        real_leaf(b, mat, rnd.uniform(-0.43, 0.43), rnd.uniform(y_from, 0.43), 0.012 + rnd.uniform(0.0, top * high), size * rnd.uniform(0.7, 1.2),
                  size * (0.18 if crop == "wheat" else 0.55), rnd.uniform(0, math.tau), rnd.uniform(-0.1, 0.65), rnd.uniform(-0.4, 0.4))


def shade(hexv, k):
    return (min(255, int(((hexv >> 16) & 255) * k)) << 16) | (min(255, int(((hexv >> 8) & 255) * k)) << 8) | min(255, int((hexv & 255) * k))


def real_rice(b, rnd, x, y, stage):
    """A hill of paddy: narrow upright blades; from the third stage thin panicles of small grains arching over, gold and dry when ready."""
    toon = R.toon
    ready = stage == "ready"
    blades = (toon("riceBladeDry", 0xC2B04E), toon("riceBladeTurning", 0x9AA63E)) if ready else (toon("riceGreen", 0x62AC34), toon("riceDeep", 0x478C2A))
    n, h = {"g1": (8, 0.050), "g2": (14, 0.090), "g3": (18, 0.120), "ready": (18, 0.115)}[stage]
    for i in range(n):
        a = i * math.tau / n + rnd.uniform(-0.4, 0.4)
        b.cone(blades[i % 2], 0.0045, 0.0, h * rnd.uniform(0.8, 1.15), R.at(x + math.cos(a) * 0.006, y + math.sin(a) * 0.006, 0.018, tilt=rnd.uniform(0.12, 0.42), tilt_dir=a), segs=3)
    if stage not in ("g3", "ready"):
        return
    grain = toon("riceGrain", 0xD9A83C) if ready else toon("riceGrainGreen", 0x9CBE42)
    stem = toon("riceStemDry", 0xBFA548) if ready else toon("riceStem", 0x74A834)
    for i in range(5):
        a = i * math.tau / 5 + rnd.uniform(-0.5, 0.5)
        droop = (1.0 if ready else 0.55) * rnd.uniform(0.8, 1.2)
        prev = Vector((x, y, 0.02 + h * 0.62))
        for j in range(1, 8):
            t = j / 7.0
            here = Vector((x + math.cos(a) * 0.036 * t, y + math.sin(a) * 0.036 * t, 0.02 + h * (0.62 + 0.52 * t) - droop * 0.052 * t * t))
            d = here - prev
            turn = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
            b.cone(stem, 0.0011, 0.0010, d.length, Matrix.Translation(prev) @ turn, segs=3)
            if j > 2:
                for side in (-1, 1):
                    b.sphere(grain, 0.0036, Matrix.Translation(here) @ turn @ Matrix.Translation((side * 0.0032, 0, 0)), scale=(0.75, 0.75, 1.75), segs=5, rings=4)
            prev = here


def real_potato(b, rnd, x, y, stage):
    """A potato plant on its ridge: stems fanning out, each ending in three broad leaflets; a few pale flowers before harvest, then the haulm yellows
    and the tubers show."""
    toon = R.toon
    ready = stage == "ready"
    leaves = (toon("potatoHaulm", 0xB9A84E), toon("potatoHaulmDeep", 0x8E8A3A)) if ready else (toon("potatoLeaf", 0x55A23A), toon("potatoDeep", 0x3A822C))
    n, r, size = {"g1": (4, 0.010, 0.022), "g2": (7, 0.018, 0.026), "g3": (10, 0.026, 0.030), "ready": (7, 0.026, 0.028)}[stage]
    for i in range(n):
        a = i * math.tau / n + rnd.uniform(-0.4, 0.4)
        d = r * rnd.uniform(0.3, 1.1)
        z = 0.050 + (r - d * 0.55) + rnd.uniform(-0.006, 0.008)
        px, py = x + math.cos(a) * d, y + math.sin(a) * d
        for spread in (-0.9, 0.0, 0.9):
            real_leaf(b, leaves[(i + (spread > 0)) % 2], px, py, z, size * (1.0 if spread == 0.0 else 0.8) * rnd.uniform(0.85, 1.15), size * 0.62,
                      a + spread, rnd.uniform(0.0, 0.45), rnd.uniform(-0.3, 0.3))
    if stage == "g3" and rnd.random() < 0.3:
        for i in range(3):
            a = rnd.uniform(0, math.tau)
            b.sphere(toon("potatoFlower", (0xF6F2FA, 0xC9B4E6)[i % 2]), 0.0050, Matrix.Translation((x + math.cos(a) * 0.012, y + math.sin(a) * 0.012, 0.092 + rnd.uniform(0, 0.006))), scale=(1, 1, 0.6), segs=6, rings=4)
    if ready:
        for i in range(2):
            a = rnd.uniform(0, math.tau)
            b.sphere(toon("potatoTuber", 0xB98E5A), 0.012, Matrix.Translation((x + math.cos(a) * 0.032, y + math.sin(a) * 0.032, 0.034)), scale=(1.25, 1, 0.85), segs=8, rings=5)


# Leaves in the sun under AgX come out pale and milky from the raw palette (the ground needed the same, real_ground.GAIN): each channel's reflectance is
# scaled down so a leaf renders as the palette's own green.
LEAF_GAIN = (0.52, 0.70, 0.36)
DRY_GAIN = (0.70, 0.62, 0.45)       # ripe masoor is a dull brown tan; from the raw palette it renders cream
YELLOW_GAIN = (0.22, 0.20, 0.002)    # sarson's flowers: AgX turns a bright reflected yellow to cream, so most of it comes from the glow
RED_GAIN = (0.62, 0.35, 0.40)       # chaulai's red: from the raw palette it renders pink


def leaf_colour(hexv, gains=LEAF_GAIN):
    out = 0
    for shift, gain in zip((16, 8, 0), gains):
        c = ((hexv >> shift) & 255) / 255.0
        lin = (c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4) * gain
        srgb = lin * 12.92 if lin <= 0.0031308 else 1.055 * lin ** (1 / 2.4) - 0.055
        out |= int(round(srgb * 255)) << shift
    return out


def calibrated(toon):
    """The material factory for a crop: green things get the leaf gain and a matt surface (a glossy leaf mirrors the pale sky)."""
    def make(name, hexv, soft=False):
        r, g = (hexv >> 16) & 255, (hexv >> 8) & 255
        green = g > r and g > (hexv & 255)
        if green:
            hexv = leaf_colour(hexv)
        elif name.startswith(("lentilDry", "lentilPod", "lentils", "mustardPod", "mustardStemDry")):     # ripe sarson too: browner than ripe wheat
            hexv = leaf_colour(hexv, DRY_GAIN)
        elif name.startswith("mustardFlower"):
            hexv = leaf_colour(hexv, YELLOW_GAIN)
        elif name.startswith("amaranth") and r > g * 1.4:
            hexv = leaf_colour(hexv, RED_GAIN)
        m = toon(name, hexv, soft)
        if name.startswith("mustardFlower"):    # a faint light of their own keeps sarson lemon-yellow through AgX (in the render only)
            glow = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
            glow.inputs["Emission Color"].default_value = (0.86, 0.56, 0.0, 1.0)
            glow.inputs["Emission Strength"].default_value = 0.42
            glow.inputs["Specular IOR Level"].default_value = 0.1
        if green:
            bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
            bsdf.inputs["Specular IOR Level"].default_value = 0.18
            bsdf.inputs["Roughness"].default_value = 0.8
        return m
    return make


def real_radish(b, rnd, x, y, stage):
    """Mooli: a rosette of long leaves; ready, only the top of the white root shows above the soil (today's tall white shoulder looks like a cup here)."""
    toon = R.toon
    green, deep = toon("radishLeaf", 0x6DBE45), toon("radishDeep", 0x4E9A36)
    n, length = {"g1": (4, 0.035), "g2": (6, 0.058), "g3": (8, 0.076), "ready": (9, 0.072)}[stage]
    if stage == "ready":
        b.sphere(toon("mooli", 0xEDE6CE), 0.0145, Matrix.Translation((x, y, 0.024)), scale=(1.0, 1.0, 1.15), segs=10, rings=7)
        n -= 3                                                   # fewer leaves over a ripe root, so the white shows
    base = 0.036 if stage == "ready" else 0.02
    for k in range(n):
        a = k * math.tau / n + rnd.uniform(-0.3, 0.3)
        real_leaf(b, green if k % 2 else deep, x, y, base, length * rnd.uniform(0.85, 1.1), length * 0.42, a, rnd.uniform(0.5, 0.9))


def still_water():
    """Paddy water: muddy, still and glossy, so it catches the sky between the plants."""
    m = bpy.data.materials.new("paddyStill")
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = S.srgb(0x4E7E80)
    bsdf.inputs["Roughness"].default_value = 0.07
    bsdf.inputs["IOR"].default_value = 1.33
    return m


def model_crop(crop, stage):
    """A crop across a whole acre at a stage, as the game's crop sprite (drawn over the soil tile)."""
    toon = R.toon
    rnd = random.Random(sum(ord(ch) for ch in crop + stage) + 274)
    b = R.Batch(crop)
    objs = []
    if crop == "rice":
        objs.append(R.box("paddyBund", (0.97, 0.97, 0.012), (0, 0, 0.008), toon("paddyBund", 0x8A5E3C)))
        objs.append(R.box("paddyWater", (0.94, 0.94, 0.004), (0, 0, 0.0165), still_water()))
    elif crop == "potato":
        for x in R.RIDGE_X:   # earthed-up ridges from the first sowing
            m = Matrix.Translation((x, -0.45, 0.012)) @ Matrix.Rotation(math.radians(-90), 4, "X") @ Matrix.Diagonal((0.05, 0.03, 1.0, 1.0))
            b.cone(toon("potatoRidge", 0x7E5736), 1.0, 1.0, 0.9, m, segs=12)
    if crop in FILL:
        young_hex, ripe_hex = R.MATS.get(crop) or (0x5FA238, 0x7AB048)
        if crop == "potato":
            young_hex, ripe_hex = 0x4E9A36, 0xA9A04A
        young = (toon(crop + "LeafFillA", young_hex), toon(crop + "LeafFillB", shade(young_hex, 0.80)))
        ripe = (toon(crop + "LeafFillRipeA", ripe_hex), toon(crop + "LeafFillRipeB", shade(ripe_hex, 0.80)))
        # A fodder field being cut: only the far half still stands.
        fill_canopy(b, rnd, crop, "ready" if stage == "cut" else stage, (young, ripe), y_from=0.0 if stage == "cut" else -0.43)
    plain_leaf = R.leaf
    R.leaf = real_leaf
    try:
        # Ripe mooli keeps its furrows open, so the white tops show in their rows.
        for x, y in spots(rnd, stage, open_furrows=crop == "radish" and stage == "ready"):
            st = ("stub" if y < 0.0 else "ready") if stage == "cut" else stage
            {"rice": real_rice, "potato": real_potato, "radish": real_radish}.get(crop, R.CROPS.get(crop))(b, rnd, x, y, st)
    finally:
        R.leaf = plain_leaf
    if stage == "cut":
        R.fodder_bundles(b, rnd, crop)
    return objs + b.objects()


def model_wheat(stage, clump, mats):
    """The wheat acre as dense as the other crops: the device check's clumps (render_real.clump), in the furrows too once grown, over a scatter of
    narrow leaves in place of the old rounded bands."""
    toon = R.toon
    rnd = random.Random(2711 + sum(ord(c) for c in stage))
    b = R.Batch("wheat")
    young = (toon("wheatLeafFillA", 0x6FB03E), toon("wheatLeafFillB", shade(0x6FB03E, 0.80)))
    ripe = (toon("wheatLeafFillRipeA", 0xD2A23C), toon("wheatLeafFillRipeB", shade(0xD2A23C, 0.80)))
    fill_canopy(b, rnd, "wheat", stage, (young, ripe))
    built = mats(toon)
    for x, y in spots(rnd, stage):
        clump(b, rnd, x, y, stage, built)
    return b.objects()


def model_bed(crop, stage):
    """The kitchen garden's raised bed: today's plank border, dark crumbly soil, and 5 x 5 plants (was 3 x 3)."""
    toon = R.toon
    rnd = random.Random(sum(ord(ch) for ch in (crop or "") + (stage or "")) + 274)
    w = 0.46
    plank, plank_dark, soil = toon("plank", 0x8E5C30), toon("plankDark", 0x734723), toon("bedSoil", 0x5E3A22)
    o = [R.box("soil", (w - 0.04, w - 0.04, 0.05), (0, 0, 0.025), soil, outline=False)]
    for i, (sx, sy, across) in enumerate(((0, -w / 2, 0), (0, w / 2, 0), (-w / 2, 0, 1), (w / 2, 0, 1))):
        size = (0.026, w - 0.026, 0.06) if across else (w + 0.026, 0.026, 0.06)   # the side planks stop at the others: overlapping faces render black
        o.append(R.box("plank%d" % i, size, (sx, sy, 0.03), plank if i % 2 == 0 else plank_dark, bevel=0.004))
    clods = R.Batch("clods")
    for i in range(10 if crop is None else 40):
        clods.sphere(toon("clod", 0x4E3020), rnd.uniform(0.006, 0.011), Matrix.Translation((rnd.uniform(-0.19, 0.19), rnd.uniform(-0.19, 0.19), 0.05)), scale=(1.3, 1, 0.55), segs=6, rings=4)
    o += clods.objects()
    if crop is None:
        return o
    b = R.Batch(crop)
    k = {"g1": 0.45, "g2": 0.7, "g3": 0.9, "ready": 1.0}[stage]
    plain_leaf = R.leaf
    R.leaf = real_leaf
    try:
        for ix in range(5):
            for iy in range(5):
                x, y = -0.16 + ix * 0.08 + rnd.uniform(-0.012, 0.012), -0.16 + iy * 0.08 + rnd.uniform(-0.012, 0.012)
                R.GARDEN[crop](b, rnd, x, y, 0.05, k * 0.82, stage == "ready")
    finally:
        R.leaf = plain_leaf
    return o + b.objects()


# ---------------------------------------------------------------- the frame today's sprite has

_sizes = None


def recorded(name):
    global _sizes
    if _sizes is None:
        with open(os.path.join(HERE, "..", "..", "docs", "art", "sprite-world-sizes.json")) as f:
            _sizes = {s["name"]: s for s in json.load(f)["sprites"]}
    return _sizes[name]


def hold_frame(name, objs, cam):
    """Widens the model with points the camera never draws until its bounds are today's, and says how far it reaches past them, if it does."""
    rec = recorded(name)
    k = math.sqrt(2.0)   # the table is in the game's units (132 px at 1x); the camera plane has 132 / sqrt(2) px a unit
    tx0, ty0 = -rec["px"] * k + MARGIN_UNITS, -rec["py"] * k + MARGIN_UNITS
    tx1, ty1 = (rec["w"] - rec["px"]) * k - MARGIN_UNITS, (rec["h"] - rec["py"]) * k - MARGIN_UNITS
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    inv = cam.matrix_world.inverted()
    o0 = inv @ Vector((0, 0, 0))
    xs, ys = [], []
    for o in objs:
        ev = o.evaluated_get(dg)
        for v in ev.data.vertices:
            c = inv @ (ev.matrix_world @ v.co)
            xs.append(c.x - o0.x)
            ys.append(c.y - o0.y)
    over = {"left": tx0 - min(xs), "right": max(xs) - tx1, "bottom": ty0 - min(ys), "top": max(ys) - ty1}
    past = {k: round(v, 4) for k, v in over.items() if v > 0.004}
    if past:
        print("PAST TODAY'S FRAME", name, past)
    turn = cam.matrix_world.to_3x3()
    me = bpy.data.meshes.new("framePad")
    me.from_pydata([tuple(turn @ Vector((x, y, 0))) for x in (tx0, tx1) for y in (ty0, ty1)], [], [])
    pad = bpy.data.objects.new("framePad", me)
    bpy.context.scene.collection.objects.link(pad)
    pad.visible_camera = False
    return objs + [pad]


def install(render, samples, wheat_clump_fn, wheat_mats):
    """Returns {sprite name: builder} for this story. Builders return the model's objects; the caller holds each to today's frame."""
    global R, S
    R, S = render, samples
    sprites = {}

    def with_patterns(build):
        def run():
            keep = list(S.PATTERNS)
            S.PATTERNS[:0] = PATTERNS
            S.PATTERNS.extend((k, ("leaf", 200, 0.28)) for k in LEAFY)   # last: a pod or a dry stem of a leafy crop is matched above first
            plain_toon = R.toon
            R.toon = calibrated(plain_toon)
            try:
                return build()
            finally:
                S.PATTERNS[:] = keep
                R.toon = plain_toon
        return run

    for crop in CROP_NAMES:
        stages = ("g1", "g2", "g3", "ready") + (("cut",) if crop in FODDER else ())
        for st in stages:
            sprites["crop_%s_%s" % (crop, st)] = with_patterns(lambda c=crop, s=st: model_crop(c, s))
    sprites["crop_sugarcane_g2"] = with_patterns(lambda: model_crop("sugarcane", "g3"))
    sprites["bed_empty"] = with_patterns(lambda: model_bed(None, None))
    for crop in ("garden_radish", "garden_spinach", "garden_coriander"):
        for st in ("g1", "g2", "g3", "ready"):
            sprites["bed_%s_%s" % (crop, st)] = with_patterns(lambda c=crop, s=st: model_bed(c, s))
    for st in ("g1", "g2", "g3", "ready"):
        sprites["crop_wheat_" + st] = with_patterns(lambda s=st: model_wheat(s, wheat_clump_fn, wheat_mats))
    sprites["tree_pit"] = with_patterns(R.SPRITES["tree_pit"])
    return sprites
