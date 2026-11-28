"""
US-277 (spec 032 §3.9): the animals, vehicles and machines, realistic.

An animal is one coat: barrel, neck, head and legs fused into one surface, with its muzzle, eyes, ears, horns, hooves, udder and tail on
it. Vehicles are bevelled painted steel on treaded tyres. Built facing -y at the old models' places and measures, so every picture keeps
its size. Used by render_real.py.
"""
import math

import bpy
from mathutils import Matrix, Vector

R = S = P = None

DRIVER_KIND = "hand_m"     # whose clothes the tractor's driver wears (chosen at the preview, spec 032 §3.9)


# ---------------------------------------------------------------- materials

def surface(name, hexv, rough=0.8, metal=0.0, grain=0.0, grain_scale=200.0, coat=0.0):
    """A principled surface; grain: a fine mottle and bump (hair, wood, jute)."""
    key = ("animal", name, hexv)
    if key in R._materials:
        return R._materials[key]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = R.srgb(hexv)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if coat:
        bsdf.inputs["Coat Weight"].default_value = coat
        bsdf.inputs["Coat Roughness"].default_value = 0.15
    if grain:
        coord = nt.nodes.new("ShaderNodeTexCoord")
        noise = nt.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = grain_scale
        noise.inputs["Detail"].default_value = 5.0
        nt.links.new(coord.outputs["Object"], noise.inputs["Vector"])
        ramp = nt.nodes.new("ShaderNodeMapRange")
        ramp.inputs["From Min"].default_value, ramp.inputs["From Max"].default_value = 0.35, 0.65
        ramp.inputs["To Min"].default_value, ramp.inputs["To Max"].default_value = 1.0 - grain, 1.0 + grain * 0.5
        nt.links.new(noise.outputs["Fac"], ramp.inputs["Value"])
        tint = nt.nodes.new("ShaderNodeVectorMath")
        tint.operation = "SCALE"
        tint.inputs[0].default_value = R.srgb(hexv)[:3]
        nt.links.new(ramp.outputs["Result"], tint.inputs["Scale"])
        nt.links.new(tint.outputs[0], bsdf.inputs["Base Color"])
        bump = nt.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = 0.25
        bump.inputs["Distance"].default_value = 0.002
        nt.links.new(noise.outputs["Fac"], bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    R._materials[key] = m
    return m


def patched(name, white, black, offset):
    """The crossbred cow's coat: big irregular black patches on white, fixed to the model."""
    key = ("animal", name, white, black)
    if key in R._materials:
        return R._materials[key]
    m = surface(name, white, rough=0.75)
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    coord = nt.nodes.new("ShaderNodeTexCoord")
    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.inputs["Location"].default_value = offset
    nt.links.new(coord.outputs["Object"], mapping.inputs["Vector"])
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 17.0
    noise.inputs["Detail"].default_value = 2.0
    noise.inputs["Distortion"].default_value = 0.4
    nt.links.new(mapping.outputs["Vector"], noise.inputs["Vector"])
    step = nt.nodes.new("ShaderNodeMapRange")
    step.inputs["From Min"].default_value, step.inputs["From Max"].default_value = 0.515, 0.525
    nt.links.new(noise.outputs["Fac"], step.inputs["Value"])
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.inputs[6].default_value = R.srgb(white)
    mix.inputs[7].default_value = R.srgb(black)
    nt.links.new(step.outputs["Result"], mix.inputs[0])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    R._materials[key] = m
    return m


# ---------------------------------------------------------------- cattle

#            leg length, body width, coat, dark, muzzle, horn, hoof, udder
CATTLE = {
    # Murrah buffalo: black with a soft blue sheen, tightly coiled horns, a heavy body on short legs.
    "buffalo": dict(legs=0.058, w=1.0, coat=0x1B191B, dark=0x0F0D0F, muzzle=0x3A3438, horn=0x2E2924, hoof=0x0F0D0E, udder=0x40353A, rough=0.68),
    # Crossbred cow (HF cross): white with black patches, a pink muzzle and udder, short horns.
    "cow": dict(legs=0.068, w=0.9, coat=0xE9E4DA, dark=0x211E1F, muzzle=0xC98E86, horn=0xD8CCAE, hoof=0x372E28, udder=0xD9A097, rough=0.75),
    # Draught bullock (Hariana type): grey-white, a hump over the shoulders, a dewlap, long upright horns.
    "bullock": dict(legs=0.074, w=0.95, coat=0xCDC6B8, dark=0x6F685E, muzzle=0x3D3833, horn=0x453B30, hoof=0x24201C, udder=None, rough=0.78),
}

#      y along the body, half width, the back above the middle, the belly below it
BARREL = [(-0.088, 0.010, 0.016, 0.014), (-0.076, 0.027, 0.033, 0.030), (-0.058, 0.036, 0.047, 0.037), (-0.035, 0.042, 0.038, 0.042),
          (-0.004, 0.049, 0.034, 0.046), (0.030, 0.049, 0.035, 0.043), (0.060, 0.044, 0.042, 0.030), (0.080, 0.036, 0.040, 0.020),
          (0.094, 0.022, 0.030, 0.008), (0.101, 0.008, 0.021, -0.006)]


def barrel(z0, w, n=40):
    pts = P.spline(BARREL, 4)
    rings = []
    for y, half, top, belly in pts:
        ring = []
        for k in range(n):
            a = k * math.tau / n
            c, s = math.cos(a), math.sin(a)
            x = half * w * math.copysign(abs(c) ** 0.82, c)
            z = z0 + (top if s > 0 else belly) * math.copysign(abs(s) ** 0.86, s)
            ring.append(Vector((x, y, z)))
        rings.append(ring)
    return P.loft(rings)


def moved(part, fn):
    return [tuple(fn(Vector(v))) for v in part[0]], part[1]


def animal(kind, pose="stand"):
    """One animal along y, head at -y: its parts, unturned and unscaled."""
    p = CATTLE[kind]
    w, lh = p["w"], p["legs"]
    lie = pose == "lie"
    z0 = lh + 0.028 - ((lh - 0.019) if lie else 0.0)   # lying, the belly rests on the ground
    hz = z0 + 0.020
    pivot = Vector((0, -0.064, z0 + 0.016))
    bow_deg = 40.0 if pose == "eat" else 0.0

    def bow(v):   # the neck and head lower to the manger, easing in at the shoulders
        t = max(0.0, min(1.0, (-v.y - 0.058) / 0.040))
        t = t * t * (3 - 2 * t)
        return pivot + Matrix.Rotation(math.radians(bow_deg * t), 3, "X") @ (v - pivot) if t else v

    def head_bow(v):   # the head and everything on it turn as one
        return pivot + Matrix.Rotation(math.radians(bow_deg), 3, "X") @ (v - pivot)

    if kind == "cow":
        coat = patched("cowCoat", p["coat"], p["dark"], (0.31, 0.12, 0.07))
    else:
        coat = surface(kind + "Coat", p["coat"], rough=p["rough"], grain=0.10, grain_scale=260.0)
    dark = surface(kind + "Dark", p["dark"], rough=0.8, grain=0.12, grain_scale=300.0)
    muzzle = surface(kind + "Muzzle", p["muzzle"], rough=0.5)
    horn = surface(kind + "Horn", p["horn"], rough=0.45, grain=0.18, grain_scale=500.0)
    hoof = surface(kind + "Hoof", p["hoof"], rough=0.5)
    eye = surface("cattleEye", 0x0E0907, rough=0.12)
    o = []

    # ---- the coat: barrel, neck, skull, jaw and legs in one surface
    body = [barrel(z0, w)]
    for s in (-1, 1):   # hip and pin bones, the shoulder blades
        body.append(P.ball((s * 0.025 * w, 0.070, z0 + 0.026), 0.015, 0.018, 0.013))
        body.append(P.ball((s * 0.022 * w, -0.052, z0 + 0.012), 0.017, 0.022, 0.026))
    neck = P.tube([(0, -0.056, z0 + 0.008), (0, -0.082, hz + 0.006), (0, -0.108, hz + 0.012)],
                  [(0.024 * w, 0.033), (0.017 * w, 0.025), (0.014, 0.018)], n=24, ref=(0, 0, 1))
    skull = P.tube([(0, -0.106, hz + 0.015), (0, -0.121, hz + 0.011), (0, -0.136, hz - 0.001), (0, -0.152, hz - 0.015)],
                   [(0.016, 0.015), (0.020, 0.0165), (0.0135, 0.0125), (0.0115, 0.0095)], n=24, ref=(0, 0, 1))
    jaw = P.tube([(0, -0.110, hz - 0.004), (0, -0.128, hz - 0.012), (0, -0.147, hz - 0.022)], [(0.012, 0.010), (0.010, 0.008), (0.007, 0.006)], n=16, ref=(0, 0, 1))
    body += [moved(neck, bow), moved(skull, bow), moved(jaw, bow)]
    for s in (-1, 1):
        fx, hx = s * 0.025 * w, s * 0.027 * w
        if lie:   # forelegs folded under the chest, hind legs along the belly
            body.append(P.tube([(fx, -0.052, 0.032), (fx * 1.35, -0.104, 0.0125), (fx * 1.95, -0.082, 0.0095), (fx * 2.05, -0.064, 0.0095)], [0.0155, 0.0115, 0.0072, 0.0070], n=14))
            body.append(P.tube([(hx * 1.0, 0.064, 0.036), (hx * 1.9, 0.024, 0.0150), (hx * 2.1, 0.050, 0.0100), (hx * 2.0, 0.072, 0.0095)], [0.0230, 0.0140, 0.0082, 0.0072], n=14))
        else:
            body.append(P.tube([(fx, -0.054, z0 - 0.006), (fx, -0.056, z0 - 0.034), (fx, -0.059, lh * 0.52), (fx, -0.056, lh * 0.30), (fx, -0.058, 0.019), (fx, -0.061, 0.011)],
                               [0.0175, 0.0120, 0.0090, 0.0064, 0.0076, 0.0070], n=14))
            body.append(P.tube([(hx * 0.8, 0.058, z0 + 0.006), (hx, 0.054, z0 - 0.028), (hx, 0.074, lh * 0.60), (hx, 0.066, lh * 0.30), (hx, 0.065, 0.019), (hx, 0.062, 0.011)],
                               [0.0230, 0.0160, 0.0098, 0.0066, 0.0078, 0.0070], n=14))
    if kind == "bullock":   # the zebu's hump over the shoulders and the dewlap down the neck
        body.append(P.ball((0, -0.047, z0 + 0.050), 0.017, 0.026, 0.024))
        fold = 0.45 if lie else 1.0   # lying, the dewlap bunches up
        body.append(moved(P.tube([(0, -0.136, hz - 0.022), (0, -0.110, hz - 0.016 - 0.010 * fold), (0, -0.086, z0 - 0.026 * fold), (0, -0.064, z0 - 0.030 * fold)],
                                 [(0.0030, 0.004), (0.0042, 0.015 * fold), (0.0048, 0.018 * fold), (0.006, 0.010 * fold)], n=14, ref=(0, 0, 1)), bow))
    o.append(P.obj("coat", body, coat, fuse=0.0017, smooth=5))

    # ---- hooves
    for i, (x, y) in enumerate(((-0.025 * w, -0.062), (0.025 * w, -0.062), (-0.027 * w, 0.061), (0.027 * w, 0.061))):
        if lie:
            fx, hx = (-0.025 * w if x < 0 else 0.025 * w), (-0.027 * w if x < 0 else 0.027 * w)
            at = (fx * 2.05, -0.057, 0.0085) if y < 0 else (hx * 1.98, 0.079, 0.0085)
            o.append(P.obj("hoof%d" % i, [P.ball(at, 0.0075, 0.0095, 0.0060)], hoof))
        else:
            o.append(R.cylinder("hoof%d" % i, 0.0090, 0.0068, 0.013, (x, y, 0.0065), hoof, segs=18, smooth=True, outline=False))

    # ---- the face
    o.append(P.obj("muzzle", [P.tube([(0, -0.147, hz - 0.012), (0, -0.157, hz - 0.019), (0, -0.163, hz - 0.023)], [(0.0128, 0.0108), (0.0122, 0.0104), (0.0086, 0.0078)],
                                     n=24, ref=(0, 0, 1))], muzzle, bend=head_bow))
    for s in (-1, 1):
        o.append(P.obj("nostril%d" % s, [P.ball((s * 0.0068, -0.1655, hz - 0.019), 0.0026, 0.0018, 0.0021)], eye, bend=head_bow))
        o.append(P.obj("eye%d" % s, [P.ball((s * 0.0180, -0.1235, hz + 0.0105), 0.0030, 0.0042, 0.0030)], eye, bend=head_bow))
        o.append(P.obj("lid%d" % s, [P.tube([(s * 0.0176, -0.1170, hz + 0.0135), (s * 0.0200, -0.1232, hz + 0.0150), (s * 0.0178, -0.1295, hz + 0.0125)],
                                            [0.0016, 0.0020, 0.0012], n=8)], coat if kind != "cow" else dark, bend=head_bow))
        droop = {"buffalo": -0.006, "cow": 0.006, "bullock": 0.000}[kind]
        ear = P.tube([(s * 0.014, -0.108, hz + 0.012), (s * 0.030, -0.104, hz + 0.012 + droop * 0.5), (s * 0.050, -0.101, hz + 0.010 + droop)],
                     [(0.0040, 0.0035), (0.0095, 0.0026), (0.0018, 0.0012)], n=16, ref=(0, 0, 1))
        o.append(P.obj("ear%d" % s, [ear], coat if kind != "cow" else dark, bend=head_bow))
        if kind == "buffalo":   # Murrah: back, down and curling up again beside the ear
            path = [(s * 0.011, -0.108, hz + 0.027), (s * 0.028, -0.100, hz + 0.032), (s * 0.039, -0.086, hz + 0.024), (s * 0.039, -0.082, hz + 0.008),
                    (s * 0.032, -0.090, hz + 0.001), (s * 0.028, -0.098, hz + 0.009), (s * 0.029, -0.096, hz + 0.017)]
            radii = (0.0072, 0.0016)
        elif kind == "bullock":
            path = [(s * 0.012, -0.109, hz + 0.026), (s * 0.027, -0.109, hz + 0.039), (s * 0.035, -0.111, hz + 0.056), (s * 0.031, -0.116, hz + 0.073)]
            radii = (0.0068, 0.0012)
        else:
            path = [(s * 0.013, -0.109, hz + 0.026), (s * 0.025, -0.109, hz + 0.032), (s * 0.028, -0.113, hz + 0.043)]
            radii = (0.0046, 0.0010)
        pts = [Vector(v) for v in P.spline(path, 6)]
        o.append(P.obj("horn%d" % s, [P.tube(pts, [radii[0] + (radii[1] - radii[0]) * i / (len(pts) - 1) for i in range(len(pts))], n=12)], horn, bend=head_bow))

    # ---- the udder and the tail
    if p["udder"] and not lie:
        skin = surface(kind + "Udder", p["udder"], rough=0.6)
        parts = [P.ball((0, 0.040, z0 - 0.040), 0.023 * w, 0.026, 0.017)]
        for x in (-0.010, 0.010):
            for y in (0.030, 0.050):
                parts.append(P.tube([(x, y, z0 - 0.052), (x, y, z0 - 0.065)], [0.0032, 0.0024], n=10))
        o.append(P.obj("udder", parts, skin, fuse=0.0010, smooth=2))
    if lie:
        path = [(0, 0.098, z0 + 0.016), (0.018, 0.110, z0 - 0.006), (0.046, 0.100, 0.008), (0.062, 0.078, 0.007)]
        tuft_at, tuft = (0.066, 0.069, 0.007), (0.0060, 0.0140, 0.0055)
    else:
        path = [(0, 0.097, z0 + 0.019), (0, 0.108, z0 + 0.006), (0, 0.107, z0 - 0.020), (0, 0.103, z0 - 0.046)]
        tuft_at, tuft = (0, 0.103, z0 - 0.056), (0.0062, 0.0062, 0.0150)
    pts = [Vector(v) for v in P.spline(path, 6)]
    o.append(P.obj("tail", [P.tube(pts, [0.0048 - 0.0030 * i / (len(pts) - 1) for i in range(len(pts))], n=10)], coat))
    o.append(P.obj("tuft", [P.ball(tuft_at, *tuft)], dark))
    return o


ANIMAL_TURN, ANIMAL_SCALE = 18.0, 1.3


def placed(parts, degrees, scale, shift=(0, 0, 0)):
    m = Matrix.Translation(shift) @ Matrix.Diagonal((scale, scale, scale, 1.0)) @ Matrix.Rotation(math.radians(degrees), 4, "Z")
    for q in parts:
        q.matrix_world = m @ q.matrix_world
    return parts


def model_animal(kind, pose):
    return placed(animal(kind, pose), ANIMAL_TURN, ANIMAL_SCALE)


def rope_mat():
    return surface("rope", 0xB59E6E, rough=0.9, grain=0.2, grain_scale=600.0)


def model_tethered(kind):
    """A neighbour's cow or buffalo tied to a peg in the yard."""
    pose = "eat" if kind == "buffalo" else "stand"
    o = animal(kind, pose)
    wood = surface("pegWood", 0x7A5230, rough=0.85, grain=0.2, grain_scale=300.0)
    z0 = CATTLE[kind]["legs"] + 0.028
    neck = Vector((0, -0.090, z0 + 0.020 - (0.016 if pose == "eat" else 0.0)))
    top = Vector((0.074, -0.150, 0.037))
    ring = [neck + Vector((0.0185 * math.cos(k * math.tau / 28), -0.004 * math.sin(k * math.tau / 28), 0.027 * math.sin(k * math.tau / 28))) for k in range(29)]
    sag = [neck + Vector((0.016, 0, -0.018)), (neck + top) / 2 + Vector((0.014, 0.004, -0.040)), top]
    rope = [P.tube(ring, [0.0022] * 29, n=8), P.tube([Vector(v) for v in P.spline([tuple(v) for v in sag], 10)], [0.0018] * 21, n=8)]
    o.append(P.obj("rope", rope, rope_mat()))
    o.append(R.cylinder("peg", 0.0052, 0.0042, 0.048, (top.x, top.y, 0.024), wood, segs=14, smooth=True, outline=False))
    return placed(o, ANIMAL_TURN, ANIMAL_SCALE)


def model_plough_team():
    """Two bullocks under one yoke, the wooden plough behind them in the furrow (spec 013 §3.2). The ploughman is drawn by the game."""
    o = []
    turned = Matrix.Rotation(math.radians(18), 4, "Z")
    for s in (-1, 1):
        shift = turned @ Vector((s * 0.062 * ANIMAL_SCALE, 0, 0))
        o += placed(animal("bullock", "stand"), 18, ANIMAL_SCALE, shift)
    wood = surface("ploughWood", 0x8E6238, rough=0.75, grain=0.22, grain_scale=220.0)
    old = surface("yokeWood", 0x6B4A2C, rough=0.8, grain=0.22, grain_scale=220.0)
    iron = surface("ploughIron", 0x4A4D52, rough=0.45, metal=0.85, grain=0.15, grain_scale=300.0)
    z0 = CATTLE["bullock"]["legs"] + 0.028
    yz, yy = z0 + 0.047, -0.071
    tack = [P.obj("yoke", [P.tube([(-0.115, yy, yz - 0.004), (-0.062, yy, yz + 0.004), (0, yy, yz - 0.002), (0.062, yy, yz + 0.004), (0.115, yy, yz - 0.004)],
                                  [0.0058, 0.0075, 0.0068, 0.0075, 0.0058], n=14)], old)]
    for s in (-1, 1):   # the pins either side of each neck and the neck ropes
        for dx in (-0.028, 0.028):
            x = s * 0.062 + dx
            tack.append(P.obj("pin%d%d" % (s, dx > 0), [P.tube([(x, yy, yz + 0.012), (x * 1.0, yy - 0.002, yz - 0.036)], [0.0028, 0.0024], n=8)], old))
        loop = [(s * 0.062 - 0.028, yy, yz - 0.026), (s * 0.062, yy - 0.004, yz - 0.060), (s * 0.062 + 0.028, yy, yz - 0.026)]
        tack.append(P.obj("neckRope%d" % s, [P.tube([Vector(v) for v in P.spline(loop, 10)], [0.0022] * 21, n=8)], rope_mat()))
    tack.append(P.obj("beam", [P.tube([(0, yy - 0.004, yz - 0.004), (0, 0.040, 0.098), (0, 0.140, 0.062), (0, 0.205, 0.034)], [0.0055, 0.0065, 0.0078, 0.0090], n=14)], wood))
    tack.append(P.obj("body", [P.tube([(0, 0.186, 0.044), (0, 0.206, 0.022), (0, 0.226, 0.014)], [(0.0100, 0.012), (0.0130, 0.011), (0.0090, 0.008)], n=14, ref=(0, 0, 1))], wood))
    tack.append(P.obj("share", [P.tube([(0, 0.220, 0.012), (0, 0.190, 0.006), (0, 0.166, 0.002)], [(0.0105, 0.0035), (0.0075, 0.0030), (0.0010, 0.0010)], n=12, ref=(0, 0, 1))], iron))
    tack.append(P.obj("handle", [P.tube([(0, 0.216, 0.020), (0, 0.236, 0.068), (0, 0.254, 0.101)], [0.0072, 0.0055, 0.0045], n=12)], old))
    tack.append(P.obj("grip", [P.tube([(-0.016, 0.254, 0.101), (0.016, 0.254, 0.101)], [0.0042, 0.0042], n=10, ref=(0, 0, 1))], old))
    return o + placed(tack, 18, ANIMAL_SCALE)


# ---------------------------------------------------------------- vehicle parts

def paint(name, hexv):
    return surface(name, hexv, rough=0.38, coat=0.35)


def steel(name, hexv, rough=0.42):
    return surface(name, hexv, rough=rough, metal=0.75)


def rubber():
    return surface("tyre", 0x1E1C1B, rough=0.82, grain=0.10, grain_scale=400.0)


def glass():
    return surface("pane", 0x152634, rough=0.06, metal=0.35)


def slab(name, size, loc, mat, bevel=0.0025, rot=None):
    """A box with rounded edges."""
    o = R.box(name, size, loc, mat, rot=rot, outline=False)
    if bevel:
        mod = o.modifiers.new("round", "BEVEL")
        mod.width = min(bevel, min(size) * 0.45)
        mod.segments = 3
        for face in o.data.polygons:
            face.use_smooth = True
        mod.harden_normals = False
    return o


def rod(name, pts, r, mat, n=8):
    pts = [Vector(v) for v in pts]
    return P.obj(name, [P.tube(pts, [r] * len(pts), n=n)], mat)


def lathe(name, centre, profile, mat, n=48, smooth=True):
    """Turned round the x axis at centre: profile is [(along x, radius)]."""
    cx, cy, cz = centre
    rings = [[Vector((cx + dx, cy + r * math.cos(k * math.tau / n), cz + r * math.sin(k * math.tau / n))) for k in range(n)] for dx, r in profile]
    o = P.obj(name, [P.loft(rings, cap=False)], mat, flat=not smooth)
    return o


def wheel(name, x, y, r, width, rim_mat, lugs=0):
    """A treaded tyre on a dished rim, standing on the ground at (x, y)."""
    side = 1.0 if x >= 0 else -1.0
    h = width / 2
    parts = [lathe(name + "Tyre", (x, y, r), [(-h * 0.75, r * 0.60), (-h, r * 0.74), (-h, r * 0.88), (-h * 0.72, r * 0.975), (-h * 0.35, r), (h * 0.35, r),
                                              (h * 0.72, r * 0.975), (h, r * 0.88), (h, r * 0.74), (h * 0.75, r * 0.60)], rubber())]
    parts.append(lathe(name + "Rim", (x, y, r), [(side * h * 0.74, r * 0.605), (side * h * 0.92, r * 0.56), (side * h * 0.55, r * 0.50), (side * h * 0.50, r * 0.26),
                                                 (side * h * 0.88, r * 0.20), (side * h * 0.92, r * 0.001)], rim_mat))
    parts.append(lathe(name + "Back", (x, y, r), [(-side * h * 0.74, r * 0.605), (-side * h * 0.74, r * 0.001)], rubber()))
    bolt = steel("bolt", 0x8D9093)
    for k in range(6):
        a = k * math.tau / 6
        parts.append(P.obj(name + "Bolt%d" % k, [P.ball((x + side * h * 0.90, y + r * 0.145 * math.cos(a), r + r * 0.145 * math.sin(a)), r * 0.035)], bolt))
    for k in range(lugs):   # the tractor's chevron lugs
        for half in (-1, 1):
            a = (k + (0.0 if half < 0 else 0.5)) * math.tau / lugs
            lug = slab(name + "Lug%d_%d" % (k, half), (width * 0.56, r * 0.085, r * 0.075), (0, 0, 0), rubber(), bevel=0.0012)
            lug.matrix_world = (Matrix.Translation((x + half * width * 0.22, y, r)) @ Matrix.Rotation(a, 4, "X") @ Matrix.Translation((0, 0, r * 0.995))
                                @ Matrix.Rotation(half * math.radians(32), 4, "Z"))
            parts.append(lug)
    return parts


def fender(name, x, y, z, r, width, a0, a1, mat, n=24):
    """A mudguard arched over a wheel: from angle a0 to a1 (0 is behind, 90 the top)."""
    rings = []
    for i in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * i / n)
        ring = []
        for k in range(9):
            u = k / 8 - 0.5
            rr = r - 0.004 * (2 * u) ** 4
            ring.append(Vector((x + u * width, y + rr * math.cos(a), z + rr * math.sin(a))))
        rings.append(ring)
    return P.obj(name, [P.loft(rings, cap=False, closed=False)], mat, solid=0.004)


def pillow(half_w, half_l, half_h):
    """A filled sack lying down: flat-sided, square-shouldered, closed at the bottom end and drawn in toward its mouth."""
    rings = []
    for y, k in ((-1.00, 0.55), (-0.94, 0.86), (-0.70, 1.00), (0.00, 0.96), (0.62, 1.00), (0.86, 0.80), (1.00, 0.34)):
        ring = []
        for i in range(28):
            a = i * math.tau / 28
            c, s = math.cos(a), math.sin(a)
            ring.append(Vector((half_w * k * math.copysign(abs(c) ** 0.62, c), half_l * y, half_h * (0.45 + 0.55 * k) * math.copysign(abs(s) ** 0.72, s))))
        rings.append(ring)
    return P.loft(rings)


def sacks(prefix, spots, size=1.0):
    jute = surface("jute", 0x9C7A44, rough=0.95, grain=0.30, grain_scale=430.0)
    tie = surface("sackTie", 0x4E361C, rough=0.9)
    o = []
    for i, (x, y, z, lean) in enumerate(spots):
        mouth = [Vector((0, 0.044 * size, 0.0)), Vector((0, 0.054 * size, 0.003)), Vector((0, 0.064 * size, 0.004))]
        bag = P.obj("%sSack%d" % (prefix, i), [pillow(0.032 * size, 0.048 * size, 0.021 * size)], jute)
        neck = P.obj("%sMouth%d" % (prefix, i), [P.tube(mouth, [(0.012 * size, 0.009 * size), (0.006 * size, 0.005 * size), (0.015 * size, 0.004 * size)], n=14, ref=(0, 0, 1))], jute)
        string = rod("%sTie%d" % (prefix, i), [(-0.0085 * size, 0.0535 * size, 0.0075 * size), (0.0085 * size, 0.0535 * size, 0.0075 * size)], 0.0026 * size, tie)
        turn = Matrix.Translation((x, y, z)) @ Matrix.Rotation(lean, 4, "Z")
        for q in (bag, neck, string):
            q.matrix_world = turn @ q.matrix_world
            o.append(q)
    return o


# ---------------------------------------------------------------- the tractor, with its cultivator or trolley

SEAT_TOP, CANOPY_Z = 0.150, 0.270
ROOF = False               # False: an open tractor with a bar behind the seat, so the driver's face shows (chosen at the preview)
VEHICLE_SCALE = 1.25
MACHINE_DRAWN = 0.7        # the game draws machines at 70% (ADR 0088)
DRIVER_SHARE = 0.80        # the driver's size beside the people who walk: what fits under the canopy


def driver():
    """A man at the wheel: an existing kind's clothes, seated. He is part of this picture only."""
    parts = P.build(DRIVER_KIND, "drive")
    k = DRIVER_SHARE * R.PERSON_SCALE / (VEHICLE_SCALE * MACHINE_DRAWN)   # in the tractor model's units
    seat = Vector((0, 0.058, SEAT_TOP + 0.006))
    m = Matrix.Translation(seat) @ Matrix.Diagonal((k, k, k, 1.0)) @ Matrix.Translation((0, 0, -0.090))
    for q in parts:
        q.matrix_world = m @ q.matrix_world
    return parts


def tractor(tool):
    red, dark = paint("tractorRed", 0xB5301C), steel("tractorFrame", 0x3A3634, 0.55)
    cream, black = paint("tractorCream", 0xD9CCA6), surface("seatBlack", 0x151414, rough=0.6)
    metal, lamp = steel("tractorSteel", 0x9AA0A4, 0.3), surface("lampLens", 0xE8E2C0, rough=0.08, metal=0.2)
    rim = paint("rimYellow", 0xD2A03A)
    o = [slab("chassis", (0.062, 0.22, 0.034), (0, -0.035, 0.068), dark, 0.004),
         slab("engine", (0.050, 0.085, 0.040), (0, -0.075, 0.075), steel("engineIron", 0x55524F, 0.5), 0.008),
         slab("gearbox", (0.062, 0.095, 0.052), (0, 0.035, 0.083), dark, 0.012)]
    # the bonnet: a rounded hood, taller at the dash than at the nose
    rings = []
    for y, half, top in ((-0.178, 0.027, 0.128), (-0.172, 0.032, 0.134), (-0.130, 0.036, 0.139), (-0.060, 0.038, 0.147), (-0.018, 0.038, 0.151)):
        ring = []
        for k in range(21):
            a = math.pi * k / 20
            ring.append(Vector((half * math.copysign(abs(math.cos(a)) ** 0.55, math.cos(a)), y, 0.095 + (top - 0.095) * math.sin(a) ** 0.55)))
        rings.append(ring)
    o.append(P.obj("bonnet", [P.loft(rings)], red))
    o.append(slab("grille", (0.050, 0.006, 0.040), (0, -0.1795, 0.108), black, 0.002))
    for i in range(6):
        o.append(slab("slat%d" % i, (0.048, 0.003, 0.0026), (0, -0.1832, 0.093 + i * 0.0060), metal, 0.001))
    o.append(slab("nose", (0.052, 0.008, 0.008), (0, -0.178, 0.131), cream, 0.003))
    for s in (-1, 1):
        o.append(P.obj("headlamp%d" % s, [P.ball((s * 0.036, -0.168, 0.117), 0.0085, 0.0065, 0.0085)], lamp))
        o.append(slab("sideVent%d" % s, (0.003, 0.050, 0.016), (s * 0.0375, -0.100, 0.112), black, 0.001))
        o.append(slab("badge%d" % s, (0.003, 0.100, 0.005), (s * 0.0384, -0.095, 0.131), cream, 0.001))
    o.append(rod("exhaust", [(0.026, -0.070, 0.135), (0.026, -0.070, 0.212)], 0.0050, steel("exhaustSteel", 0x4A4542, 0.55), 12))
    o.append(rod("muffler", [(0.026, -0.070, 0.158), (0.026, -0.070, 0.190)], 0.0085, steel("mufflerSteel", 0x5A524C, 0.6), 14))
    o.append(slab("dash", (0.060, 0.016, 0.030), (0, -0.012, 0.150), red, 0.005))
    o.append(rod("column", [(0, -0.008, 0.155), (0, 0.012, 0.183)], 0.0035, dark))
    wheel_pts = [(0.017 * math.cos(k * math.tau / 28), 0.014 + 0.013 * math.sin(k * math.tau / 28), 0.184 + 0.011 * math.sin(k * math.tau / 28)) for k in range(29)]
    o.append(rod("steering", wheel_pts, 0.0025, black))
    o.append(slab("floor", (0.090, 0.075, 0.006), (0, 0.020, 0.092), dark, 0.002))
    o.append(slab("seat", (0.040, 0.034, 0.012), (0, 0.062, SEAT_TOP - 0.006), black, 0.005))
    o.append(slab("seatBack", (0.040, 0.009, 0.032), (0, 0.084, SEAT_TOP + 0.012), black, 0.004, rot=Matrix.Rotation(math.radians(-12), 3, "X")))
    o.append(rod("seatPost", [(0, 0.060, 0.095), (0, 0.062, SEAT_TOP - 0.010)], 0.006, dark))
    for s in (-1, 1):
        o.append(fender("rearGuard%d" % s, s * 0.060, 0.060, 0.065, 0.076, 0.034, 22, 150, red))
        o.append(slab("guardSide%d" % s, (0.004, 0.080, 0.022), (s * 0.042, 0.060, 0.128), red, 0.0015))
        o.append(P.obj("tailLamp%d" % s, [P.ball((s * 0.062, 0.128, 0.108), 0.006, 0.004, 0.006)], paint("tailRed", 0xE04A28)))
        o += wheel("rear%d" % (s > 0), s * 0.072, 0.060, 0.065, 0.040, rim, lugs=18)
        o += wheel("front%d" % (s > 0), s * 0.050, -0.140, 0.035, 0.022, rim)
        if ROOF:
            o.append(rod("post%d" % s, [(s * 0.050, 0.100, 0.140), (s * 0.052, 0.116, CANOPY_Z - 0.004)], 0.0030, metal))
    o.append(rod("frontAxle", [(-0.045, -0.140, 0.035), (0.045, -0.140, 0.035)], 0.0065, dark, 10))
    o.append(rod("rearAxle", [(-0.070, 0.060, 0.065), (0.070, 0.060, 0.065)], 0.0085, dark, 10))
    if ROOF:
        o.append(slab("canopy", (0.118, 0.092, 0.007), (0, 0.078, CANOPY_Z), cream, 0.0035))
        o.append(slab("canopyLip", (0.123, 0.097, 0.003), (0, 0.078, CANOPY_Z - 0.0035), red, 0.0012))
    else:   # the safety bar behind the seat
        o.append(rod("rollBar", [(-0.044, 0.100, 0.138), (-0.042, 0.108, 0.226), (-0.030, 0.110, 0.240), (0.030, 0.110, 0.240), (0.042, 0.108, 0.226), (0.044, 0.100, 0.138)],
                     0.0050, black, 10))
    if tool == "cultivator":
        iron = steel("tineIron", 0x4A4D52, 0.5)
        o.append(slab("toolBar", (0.175, 0.013, 0.013), (0, 0.146, 0.052), paint("implementRed", 0x8E2818), 0.002))
        o.append(slab("toolBar2", (0.140, 0.011, 0.011), (0, 0.162, 0.050), paint("implementRed", 0x8E2818), 0.002))
        for s in (-1, 1):
            o.append(rod("link%d" % s, [(s * 0.026, 0.096, 0.070), (s * 0.048, 0.146, 0.054)], 0.0038, dark))
        o.append(rod("topLink", [(0, 0.090, 0.115), (0, 0.144, 0.082), (0, 0.148, 0.056)], 0.0036, dark))
        for i in range(7):
            x, y = -0.074 + i * 0.0246, 0.144 + 0.015 * (i % 2)
            o.append(rod("tine%d" % i, [(x, y, 0.052), (x, y + 0.014, 0.030), (x, y + 0.002, 0.006), (x, y - 0.010, 0.001)], 0.0034, iron))
    elif tool == "trolley":
        side_red, floor_wood = paint("trolleyRed", 0x93301D), surface("trolleyPlank", 0x7A5232, rough=0.8, grain=0.25, grain_scale=180.0)
        o.append(rod("hitch", [(0, 0.100, 0.055), (0, 0.168, 0.052)], 0.0055, dark))
        o.append(slab("trolleyFrame", (0.130, 0.240, 0.012), (0, 0.282, 0.060), dark, 0.003))
        o.append(slab("trolleyBed", (0.155, 0.240, 0.012), (0, 0.282, 0.073), floor_wood, 0.002))
        for x in (-0.078, 0.078):
            o.append(slab("trolleySide%d" % (x > 0), (0.008, 0.240, 0.060), (x, 0.282, 0.108), side_red, 0.002))
            o.append(slab("trolleySideRail%d" % (x > 0), (0.011, 0.244, 0.006), (x, 0.282, 0.139), steel("railSteel", 0x7E7468, 0.5), 0.002))
            for i in range(5):
                o.append(slab("rib%d_%d" % (x > 0, i), (0.003, 0.007, 0.058), (x * 1.07, 0.182 + i * 0.050, 0.108), side_red, 0.001))
        for i, y in enumerate((0.164, 0.400)):
            o.append(slab("trolleyEnd%d" % i, (0.155, 0.008, 0.060), (0, y, 0.108), side_red, 0.002))
            o.append(slab("trolleyEndRail%d" % i, (0.160, 0.011, 0.006), (0, y, 0.139), steel("railSteel", 0x7E7468, 0.5), 0.002))
        for s in (-1, 1):
            o += wheel("trolley%d" % (s > 0), s * 0.090, 0.290, 0.040, 0.028, rim)
            o.append(fender("trolleyGuard%d" % s, s * 0.090, 0.290, 0.040, 0.048, 0.030, 25, 155, side_red))
    return o


def model_tractor(tool):
    parts = tractor(tool)
    if tool == "cultivator":   # at work: somebody drives it (spec 032 §3.9); parked, and on the screens, nobody
        parts += driver()
    return placed(parts, -30.0, VEHICLE_SCALE)


# ---------------------------------------------------------------- the tempo, the truck and the van

def model_tempo():
    blue, dark = paint("tempoBlue", 0x1F5C8E), steel("tempoFrame", 0x2A2D33, 0.55)
    cream, black = paint("tempoCream", 0xD9CCA6), surface("tempoBlack", 0x151414, rough=0.6)
    plank, rim = surface("tempoPlank", 0x7A5232, rough=0.8, grain=0.25, grain_scale=180.0), steel("tempoRim", 0xA8ABAD, 0.35)
    lamp = surface("lampLens", 0xE8E2C0, rough=0.08, metal=0.2)
    o = [slab("chassis", (0.092, 0.285, 0.022), (0, 0.028, 0.056), dark, 0.004)]
    rings = []   # the rounded one-man cab
    for z, y0, y1, half in ((0.066, -0.148, -0.052, 0.040), (0.095, -0.158, -0.050, 0.047), (0.128, -0.150, -0.050, 0.047), (0.168, -0.128, -0.052, 0.043), (0.176, -0.115, -0.060, 0.034)):
        ring = []
        for k in range(40):
            a = k * math.tau / 40
            c, s = math.cos(a), math.sin(a)
            ring.append(Vector((half * math.copysign(abs(s) ** 0.45, s), (y0 + y1) / 2 - (y1 - y0) / 2 * math.copysign(abs(c) ** 0.45, c), z)))
        rings.append(ring)
    o.append(P.obj("cab", [P.loft(rings)], blue))
    o.append(slab("screen", (0.066, 0.004, 0.042), (0, -0.1475, 0.141), glass(), 0.003, rot=Matrix.Rotation(math.radians(-27), 3, "X")))
    for s in (-1, 1):
        o.append(slab("sideGlass%d" % s, (0.003, 0.044, 0.036), (s * 0.0465, -0.100, 0.139), glass(), 0.004))
        o.append(P.obj("lamp%d" % s, [P.ball((s * 0.028, -0.158, 0.094), 0.008, 0.005, 0.008)], lamp))
        o.append(rod("mirror%d" % s, [(s * 0.046, -0.138, 0.128), (s * 0.053, -0.142, 0.134)], 0.0018, black))
        o.append(slab("mirrorPane%d" % s, (0.005, 0.004, 0.016), (s * 0.054, -0.143, 0.136), black, 0.002))
    o.append(slab("roof", (0.096, 0.088, 0.007), (0, -0.092, 0.178), cream, 0.004))
    o.append(slab("bumper", (0.080, 0.008, 0.012), (0, -0.156, 0.072), black, 0.003))
    o.append(slab("bed", (0.116, 0.168, 0.010), (0, 0.092, 0.074), plank, 0.002))
    for x in (-0.058, 0.058):
        o.append(slab("side%d" % (x > 0), (0.007, 0.168, 0.046), (x, 0.092, 0.100), blue, 0.002))
        o.append(slab("sideRail%d" % (x > 0), (0.010, 0.172, 0.005), (x, 0.092, 0.125), cream, 0.002))
        o.append(slab("stripe%d" % (x > 0), (0.002, 0.160, 0.006), (x * 1.075, 0.092, 0.101), cream, 0.0008))
        s = 1 if x > 0 else -1
        o += wheel("rear%d" % (x > 0), s * 0.063, 0.112, 0.040, 0.024, rim)
        o.append(fender("guard%d" % (x > 0), s * 0.063, 0.112, 0.040, 0.048, 0.025, 20, 160, dark))
    for i, y in enumerate((0.010, 0.175)):
        o.append(slab("end%d" % i, (0.118, 0.007, 0.046), (0, y, 0.100), blue, 0.002))
    o += wheel("front", 0.012, -0.120, 0.034, 0.022, rim)
    o.append(fender("frontGuard", 0.0, -0.120, 0.034, 0.042, 0.050, 25, 155, blue))
    o += sacks("tempo", ((-0.028, 0.062, 0.100, 0.2), (0.028, 0.066, 0.100, -0.25), (0.002, 0.128, 0.100, 1.5), (0.004, 0.086, 0.130, 1.2)), 0.82)
    for q in o:   # back a little, so the nose stays inside today's frame
        q.matrix_world = Matrix.Translation((0, 0.008, 0)) @ q.matrix_world
    return placed(o, -30.0, VEHICLE_SCALE)


def model_truck(facing):
    """The trader's 4 t goods truck: a saffron cab with a painted brow, a blue wooden body with sacks."""
    cab, brow = paint("truckCab", 0xCE7E04), paint("browGreen", 0x0E5A30)
    plank = surface("truckPlank", 0x2C6396, rough=0.55, grain=0.18, grain_scale=160.0)
    deep = surface("truckPlankDeep", 0x1F466C, rough=0.6, grain=0.18, grain_scale=160.0)
    dark, black = steel("truckFrame", 0x2B2826, 0.55), surface("truckBlack", 0x151414, rough=0.6)
    chrome, lamp = steel("truckChrome", 0xC4C8CA, 0.25), surface("lampLens", 0xEDE6C0, rough=0.08, metal=0.2)
    rim, cream = steel("truckRim", 0xB4AC9C, 0.45), paint("truckCream", 0xE6DAB4)
    o = [slab("chassis", (0.150, 0.505, 0.026), (0, 0.000, 0.070), dark, 0.004),
         slab("cab", (0.205, 0.130, 0.140), (0, -0.188, 0.158), cab, 0.022),
         slab("cabRoof", (0.196, 0.122, 0.012), (0, -0.186, 0.231), cab, 0.006),
         slab("brow", (0.208, 0.034, 0.032), (0, -0.232, 0.246), brow, 0.008),
         slab("browLine", (0.200, 0.004, 0.006), (0, -0.2505, 0.246), cream, 0.001),
         slab("grille", (0.130, 0.006, 0.046), (0, -0.2545, 0.116), black, 0.003),
         slab("bumper", (0.214, 0.018, 0.022), (0, -0.258, 0.078), chrome, 0.006)]
    for i in range(5):
        o.append(slab("slat%d" % i, (0.122, 0.003, 0.0034), (0, -0.2585, 0.099 + i * 0.0085), chrome, 0.001))
    for s in (-1, 1):
        o.append(slab("screen%d" % s, (0.084, 0.004, 0.056), (s * 0.046, -0.2545, 0.187), glass(), 0.004))
        o.append(slab("window%d" % s, (0.004, 0.072, 0.054), (s * 0.1032, -0.196, 0.187), glass(), 0.006))
        o.append(slab("doorLine%d" % s, (0.0015, 0.003, 0.098), (s * 0.1030, -0.150, 0.152), black, 0))
        o.append(slab("doorHandle%d" % s, (0.004, 0.018, 0.004), (s * 0.1042, -0.162, 0.150), chrome, 0.0015))
        o.append(P.obj("lamp%d" % s, [P.ball((s * 0.078, -0.256, 0.112), 0.012, 0.007, 0.012)], lamp))
        o.append(rod("mirrorArm%d" % s, [(s * 0.100, -0.240, 0.190), (s * 0.113, -0.248, 0.196)], 0.0022, black))
        o.append(slab("mirror%d" % s, (0.008, 0.005, 0.030), (s * 0.115, -0.249, 0.194), black, 0.002))
    o.append(slab("bedFloor", (0.206, 0.335, 0.016), (0, 0.075, 0.096), deep, 0.003))
    for x in (-0.100, 0.100):
        for i in range(3):   # three boards a side
            o.append(slab("board%d_%d" % (x > 0, i), (0.010, 0.335, 0.027), (x, 0.075, 0.118 + i * 0.029), plank, 0.002))
        for i in range(4):
            o.append(slab("stake%d_%d" % (x > 0, i), (0.005, 0.012, 0.096), (x * 1.075, -0.070 + i * 0.097, 0.150), deep, 0.0015))
    for i in range(3):
        o.append(slab("tail%d" % i, (0.206, 0.010, 0.027), (0, 0.242, 0.118 + i * 0.029), plank, 0.002))
        o.append(slab("head%d" % i, (0.206, 0.012, 0.036), (0, -0.092, 0.124 + i * 0.038), deep, 0.002))
    for s in (-1, 1):
        o.append(P.obj("tailLamp%d" % s, [P.ball((s * 0.082, 0.252, 0.083), 0.008, 0.004, 0.006)], paint("tailRed", 0xD83A1E)))
        for j, y in enumerate((-0.172, 0.142)):
            o += wheel("wheel%d%d" % (s > 0, j), s * 0.098, y, 0.043, 0.034, rim)
            o.append(fender("guard%d%d" % (s > 0, j), s * 0.098, y, 0.043, 0.052, 0.036, 20, 160, cab if j == 0 else dark))
    o += sacks("truck", ((-0.048, -0.030, 0.128, 0.15), (0.048, -0.020, 0.128, -0.2), (-0.046, 0.075, 0.128, -0.12), (0.047, 0.082, 0.128, 0.18),
                         (-0.045, 0.178, 0.128, 0.22), (0.046, 0.176, 0.128, -0.15), (-0.006, 0.026, 0.170, 1.45), (0.004, 0.134, 0.170, 1.70)), 1.06)
    return placed(o, 0.0 if facing == "front" else -90.0, VEHICLE_SCALE)


def model_van(facing):
    """The supermarket's small delivery van: white, a green stripe and a yellow crate sign."""
    body, green, yellow = paint("vanBody", 0xE4E1D8), paint("vanStripe", 0x1E7C46), paint("signYellow", 0xE3B02C)
    black, dark = surface("vanBlack", 0x151414, rough=0.6), steel("vanFrame", 0x2B2826, 0.55)
    chrome, lamp = steel("vanChrome", 0xC4C8CA, 0.25), surface("lampLens", 0xEDE6C0, rough=0.08, metal=0.2)
    rim = steel("vanRim", 0xB9BDC0, 0.35)
    # The body's side outline (y, z), front at -y: bumper, a short nose, the raked windscreen, the roof, the back doors.
    outline = [(-0.192, 0.066), (-0.196, 0.098), (-0.184, 0.126), (-0.160, 0.142), (-0.130, 0.196), (-0.110, 0.209), (0.150, 0.210),
               (0.168, 0.196), (0.171, 0.070), (0.150, 0.060), (-0.170, 0.060)]
    rings = [[Vector((x, y, z)) for y, z in outline] for x in (-0.085, 0.085)]
    shell = P.obj("shell", [P.loft(rings)], body, flat=True)
    mod = shell.modifiers.new("round", "BEVEL")
    mod.width, mod.segments, mod.limit_method = 0.012, 4, "ANGLE"
    for face in shell.data.polygons:
        face.use_smooth = True
    o = [shell, slab("skirt", (0.162, 0.300, 0.014), (0, -0.010, 0.058), dark, 0.003)]
    tilt = math.atan2(0.196 - 0.142, -0.130 + 0.160)
    o.append(slab("windscreen", (0.146, 0.060, 0.004), (0, -0.1466, 0.1700), glass(), 0.002, rot=Matrix.Rotation(tilt, 3, "X")))
    for s in (-1, 1):
        o.append(P.obj("window%d" % s, [P.loft([[Vector((s * x, y, z)) for y, z in ((-0.140, 0.152), (-0.118, 0.190), (-0.062, 0.192), (-0.062, 0.152))] for x in (0.0845, 0.0862)])], glass(), flat=True))
        o.append(slab("stripe%d" % s, (0.002, 0.318, 0.026), (s * 0.0860, -0.006, 0.122), green, 0.0008))
        o.append(slab("crate%d" % s, (0.002, 0.092, 0.050), (s * 0.0862, 0.060, 0.170), yellow, 0.0008))
        for i in range(3):
            o.append(slab("crateSlat%d_%d" % (s, i), (0.0022, 0.092, 0.003), (s * 0.0868, 0.060, 0.156 + i * 0.014), dark, 0))
        o.append(slab("doorLine%d" % s, (0.0015, 0.003, 0.120), (s * 0.0860, -0.050, 0.130), black, 0))
        o.append(slab("handle%d" % s, (0.003, 0.016, 0.004), (s * 0.0870, -0.066, 0.139), chrome, 0.0012))
        o.append(P.obj("lamp%d" % s, [P.ball((s * 0.056, -0.193, 0.102), 0.012, 0.006, 0.009)], lamp))
        o.append(P.obj("tailLamp%d" % s, [P.ball((s * 0.068, 0.172, 0.108), 0.007, 0.004, 0.012)], paint("tailRed", 0xD83A1E)))
        o.append(rod("mirrorArm%d" % s, [(s * 0.083, -0.140, 0.150), (s * 0.104, -0.146, 0.154)], 0.002, black))
        o.append(slab("mirror%d" % s, (0.007, 0.005, 0.022), (s * 0.106, -0.147, 0.154), black, 0.002))
        for j, y in enumerate((-0.122, 0.112)):
            o += wheel("vwheel%d%d" % (s > 0, j), s * 0.084, y, 0.035, 0.028, rim)
            o.append(fender("vguard%d%d" % (s > 0, j), s * 0.084, y, 0.035, 0.043, 0.012, 15, 165, black))
    o.append(slab("grille", (0.062, 0.004, 0.018), (0, -0.1955, 0.092), black, 0.002))
    o.append(slab("bumperFront", (0.172, 0.014, 0.018), (0, -0.192, 0.068), dark, 0.005))
    o.append(slab("bumperBack", (0.172, 0.012, 0.016), (0, 0.171, 0.068), dark, 0.004))
    o.append(slab("rearWindow", (0.120, 0.003, 0.040), (0, 0.1712, 0.168), glass(), 0.002))
    o.append(slab("rearLine", (0.002, 0.003, 0.110), (0, 0.1716, 0.128), black, 0))
    return placed(o, 0.0 if facing == "front" else -90.0, VEHICLE_SCALE)


# ----------------------------------------------------------------

def install(render, samples, people):
    """The 20 animal, vehicle and machine pictures' builders, by sprite name."""
    global R, S, P
    R, S, P = render, samples, people
    sprites = {}
    for kind in CATTLE:
        for pose in ("stand", "eat", "lie"):
            sprites["animal_%s_%s" % (kind, pose)] = (lambda k=kind, p=pose: (lambda: model_animal(k, p)))()
    sprites["village_cow"] = lambda: model_tethered("cow")
    sprites["village_buffalo"] = lambda: model_tethered("buffalo")
    sprites["plough_team"] = model_plough_team
    sprites["tempo"] = model_tempo
    sprites["tractor"] = lambda: model_tractor("none")
    sprites["tractor_cultivator"] = lambda: model_tractor("cultivator")
    sprites["tractor_trolley"] = lambda: model_tractor("trolley")
    for facing in ("front", "back"):
        sprites["truck_" + facing] = (lambda f=facing: (lambda: model_truck(f)))()
        sprites["van_" + facing] = (lambda f=facing: (lambda: model_van(f)))()
    return sprites
