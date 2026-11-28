"""
US-275: the trees and orchards, realistic and built leaf by leaf (the owner, 2026-11-28).

Each tree keeps today's design (render.py's trunks, where its crown's clumps stand, its stages), so it keeps its size and anchor; each toon blob
becomes a clump of sprays of real-shaped leaves with the branches showing through the gaps. Fruit is about half the length of the toon art's, still several
times life size beside the tree (any smaller doesn't show at the game's zoom), in bunches on the side the camera sees. No shadows (ADR 0183): the neem's flat shade disc becomes a scatter of fallen leaves.
An orchard has no picture of its own: the game repeats these.
"""
import math
import random

import bpy
from mathutils import Matrix, Vector

R = None   # render.py, S = samples.py, C = real_crops.py: set by install()
S = None
C = None

FRONT = math.radians(-45)   # the camera looks from +x, -y

PATTERNS = [("bark", ("stone", 85, 0.34)), ("basin", ("earth", 110, 0.28)), ("floor", ("earth", 90, 0.30))]

# Fruit under AgX washes out from the raw palette, like sarson's flowers: (per-channel gains, glow in the render only).
FRUIT = {
    "mango": (0xF4B33C, (0.80, 0.55, 0.10), 0.16), "mangoBlush": (0xE8723A, (0.75, 0.45, 0.20), 0.10),
    "guava": (0xE2DA62, (0.62, 0.66, 0.16), 0.12), "guavaPale": (0xF0E27A, (0.66, 0.64, 0.20), 0.14),
    "apple": (0xD8362B, (0.80, 0.45, 0.40), 0.04), "appleDeep": (0xB42820, (0.80, 0.45, 0.40), 0.03),
    "banana": (0xE8D040, (0.72, 0.62, 0.06), 0.16), "bananaGreen": (0xB8C040, (0.55, 0.62, 0.20), 0.05),
    "papaya": (0xF0982E, (0.78, 0.50, 0.10), 0.10), "papayaGreen": (0x8CB43C, (0.50, 0.68, 0.30), 0.0),
}


def fruit(name):
    hexv, gains, glow = FRUIT[name]
    m = S.real_material("fruit_" + name, C.leaf_colour(hexv, gains))
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Roughness"].default_value = 0.55
    bsdf.inputs["Specular IOR Level"].default_value = 0.25
    if glow:
        r, g, b = S.srgb(hexv)[:3]
        bsdf.inputs["Emission Color"].default_value = (r, g, b, 1.0)
        bsdf.inputs["Emission Strength"].default_value = glow
    return m


# ---------------------------------------------------------------- parts

def aim(a, b):
    """A matrix at `a` whose z axis points at `b`, and the distance: for a cone from one point to another."""
    a, b = Vector(a), Vector(b)
    d = b - a
    return Matrix.Translation(a) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4(), d.length


def stick(batch, mat, a, b, r1, r2, segs=5):
    m, n = aim(a, b)
    batch.cone(mat, r1, r2, n, m, segs=segs)


def limb(batch, mat, a, b, r1, r2, rnd, bend=0.12, segs=7):
    """A limb in two pieces with a knee, so a trunk's branches don't look like straws."""
    a, b = Vector(a), Vector(b)
    k = (b - a).length * bend
    mid = a.lerp(b, 0.5) + Vector((rnd.uniform(-k, k), rnd.uniform(-k, k), rnd.uniform(-k * 0.4, k)))
    stick(batch, mat, a, mid, r1, (r1 + r2) / 2, segs)
    stick(batch, mat, mid, b, (r1 + r2) / 2, r2, segs)


def blade(batch, mat, p, length, width, direction, tilt, rnd):
    C.real_leaf(batch, mat, p[0], p[1], p[2], length, width, direction, tilt, rnd.uniform(-0.7, 0.7))


def rosette(batch, rnd, mat, p, out, spec):
    """A spray of leaves at a twig's end, fanned round the way it points (`out`) and hanging a little."""
    n, length, width, droop = spec
    for i in range(n):
        a = out + (i - (n - 1) / 2) * (4.4 / n) + rnd.uniform(-0.35, 0.35)
        blade(batch, mat, p, length * rnd.uniform(0.8, 1.18), width * rnd.uniform(0.85, 1.15), a, rnd.uniform(-droop, 0.45), rnd)


def compound(batch, rnd, mat, stalk, p, out, spec):
    """A neem or babul leaf: a thin midrib with leaflets in pairs."""
    pairs, length, width, reach = spec
    tilt = rnd.uniform(-0.55, 0.35)
    d = Vector((math.cos(out) * math.cos(tilt), math.sin(out) * math.cos(tilt), math.sin(tilt)))
    tip = Vector(p) + d * reach
    stick(batch, stalk, p, tip, 0.0013, 0.0007, segs=3)
    for i in range(pairs):
        at = Vector(p).lerp(tip, (i + 0.6) / pairs)
        for side in (-1, 1):
            blade(batch, mat, at, length * rnd.uniform(0.85, 1.15), width, out + side * rnd.uniform(0.7, 1.15), tilt * 0.5 - 0.1, rnd)
    blade(batch, mat, tip, length, width, out, tilt - 0.15, rnd)


def crown(batch, rnd, clumps, mats, wood, density, leaves, squash=0.82, hollow=0.62, twig=0.0022, make=None):
    """
    Leaf sprays over each of today's blobs (x, y, z, radius): most on the shell, some inside so the sky doesn't show straight through,
    a twig from the clump's heart to each shell spray. `mats` is (shaded, middle, lit).
    """
    for n, (cx, cy, cz, r) in enumerate(clumps):
        count = max(6, int(density * r * r))
        for i in range(count):
            u = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1))).normalized()
            deep = rnd.uniform(hollow, 1.0)
            p = Vector((cx + u.x * r * deep, cy + u.y * r * deep, cz + u.z * r * deep * squash))
            if any(j != n and (p - Vector(o[:3])).length < o[3] * 0.55 for j, o in enumerate(clumps)):
                continue    # buried inside a neighbour: never seen
            lit = u.z * 0.7 + (u.x - u.y) * 0.2 + rnd.uniform(-0.35, 0.35)
            mat = mats[2] if lit > 0.42 else (mats[0] if lit < -0.18 or deep < 0.74 else mats[1])
            out = math.atan2(u.y, u.x) + rnd.uniform(-0.6, 0.6)
            if make:
                make(mat, p, out)
            else:
                rosette(batch, rnd, mat, p, out, leaves)
            if i % 4 == 0:
                stick(batch, wood, (cx, cy, cz - r * 0.2), p, twig, twig * 0.4, segs=3)


def bunches(batch, fruits, rnd, clumps, count, each, shape, stalk, squash=0.82, reach=1.04, hang=0.035):
    """Fruit hanging in bunches under and on the face of the crown, on the side the camera sees (in their own batch, smooth-shaded)."""
    front = [c for c in clumps if (c[0] - c[1]) > -0.1 * max(1e-6, c[3])] or list(clumps)
    placed = []
    tries = 0
    while len(placed) < count and tries < 500:
        tries += 1
        cx, cy, cz, r = rnd.choice(front)
        a = FRONT + rnd.uniform(-1.15, 1.15)
        el = rnd.uniform(-0.75, 0.2)
        p = Vector((cx + math.cos(a) * math.cos(el) * r * reach, cy + math.sin(a) * math.cos(el) * r * reach, cz + math.sin(el) * r * squash * reach))
        if any(j[3] > 0 and (p - Vector(j[:3])).length < j[3] * 0.82 for j in clumps) or any((p - q).length < 0.05 for q in placed):
            continue
        placed.append(p)
        stick(batch, stalk, p + Vector((0, 0, hang)), p, 0.0018, 0.0014, segs=3)
        for i in range(rnd.randint(*each)):
            q = p + Vector((rnd.uniform(-0.016, 0.016), rnd.uniform(-0.016, 0.016), -rnd.uniform(0.0, 0.026)))
            shape(fruits, rnd, q)


def ball(mats, radius, scale):
    def shape(batch, rnd, q):
        tip = Matrix.Rotation(rnd.uniform(-0.25, 0.25), 4, "X") @ Matrix.Rotation(rnd.uniform(-0.25, 0.25), 4, "Y")
        batch.sphere(mats[rnd.randrange(len(mats))], radius * rnd.uniform(0.88, 1.12), Matrix.Translation(q) @ tip, scale=scale, segs=10, rings=7)
    return shape


def unshaded(m, own=0.62):
    """A floor that shows no cast shade (the owner wants none, ADR 0183): most of its colour is its own light, so a shadow barely marks it."""
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    base = bsdf.inputs["Base Color"]
    glow = bsdf.inputs["Emission Color"]
    if base.is_linked:
        source = base.links[0].from_socket
        dim = m.node_tree.nodes.new("ShaderNodeVectorMath")
        dim.operation = "SCALE"
        dim.inputs["Scale"].default_value = 1.0 - own
        m.node_tree.links.new(source, dim.inputs[0])
        m.node_tree.links.new(source, glow)
        m.node_tree.links.new(dim.outputs[0], base)
    else:
        c = base.default_value[:]
        glow.default_value = c
        base.default_value = (c[0] * (1.0 - own), c[1] * (1.0 - own), c[2] * (1.0 - own), 1.0)
    bsdf.inputs["Emission Strength"].default_value = own
    bsdf.inputs["Specular IOR Level"].default_value = 0.05
    return m


def smooth_fruit():
    return R.Batch("fruit")


def basin(toon):
    """The sapling's watering basin (today's thala): a dug floor inside a low crumbly ring."""
    soil, ring = unshaded(toon("basinSoil", 0x6E4A2E)), toon("basinRim", 0x9A744E)
    o = [R.cylinder("basinFloor", 0.084, 0.084, 0.016, (0, 0, 0.008), soil, segs=24, outline=False)]
    b = R.Batch("basinRim")
    rnd = random.Random(5)
    for i in range(46):
        a = i * math.tau / 46
        b.sphere(ring, rnd.uniform(0.011, 0.016), Matrix.Translation((math.cos(a) * 0.091, math.sin(a) * 0.091, 0.012)), scale=(1.2, 1.2, 0.75), segs=7, rings=5)
    return o + b.objects()


# ---------------------------------------------------------------- the fruit trees

def sapling(bark_hex, hexes, spec, seed=23):
    toon = R.toon
    rnd = random.Random(seed)
    bark = toon("saplingBark", bark_hex)
    b = R.Batch("sapling")
    mats = [toon("saplingLeaf%d" % i, h) for i, h in enumerate(hexes)]
    stick(b, bark, (0, 0, 0), (0.006, -0.004, 0.25), 0.008, 0.004, segs=7)
    length, width = spec
    for t, (z, n) in enumerate(((0.115, 3), (0.155, 4), (0.195, 5), (0.232, 5), (0.256, 4))):
        for i in range(n):
            a = i * math.tau / n + t * 0.7 + rnd.uniform(-0.25, 0.25)
            blade(b, mats[(i + t) % 2], (math.cos(a) * 0.006, math.sin(a) * 0.006, z), length * (0.75 if t == 4 else rnd.uniform(0.9, 1.15)), width,
                  a, (0.7 if t == 4 else 0.05 + 0.1 * t) + rnd.uniform(-0.25, 0.2), rnd)
    return R.group(basin(toon) + b.objects(), scale=R.BUILDING_SCALE)


MANGO_CLUMPS = [(0, 0, 0.05, 0.20), (0.13, 0.03, -0.02, 0.15), (-0.12, 0.06, -0.01, 0.15), (0.03, -0.13, -0.02, 0.15),
                (-0.06, 0.13, 0.0, 0.14), (0.09, 0.11, 0.04, 0.13), (-0.09, -0.09, 0.03, 0.13), (0.0, 0.0, 0.14, 0.13)]
GUAVA_CLUMPS = [(0, 0, 0.04, 0.16), (0.12, 0.02, -0.02, 0.12), (-0.11, 0.05, -0.01, 0.12), (0.02, -0.11, -0.02, 0.12), (0.05, 0.1, 0.02, 0.11)]
APPLE_CLUMPS = [(0, 0, 0.05, 0.15), (0.1, 0.03, -0.01, 0.12), (-0.1, 0.04, -0.01, 0.12), (0.03, -0.1, -0.02, 0.11), (0.04, 0.1, 0.0, 0.11)]


def placed_clumps(clumps, k, cz, shrink=0.88):
    """Today's blobs at a stage's size, a little smaller because the leaves reach past them."""
    return [(x * k, y * k, cz + z * k, r * k * shrink) for x, y, z, r in clumps]


def mango(stage):
    if stage == "sapling":
        return sapling(0x7A4E2E, (0x3F9440, 0x2F7A35), (0.078, 0.021))
    toon = R.toon
    rnd = random.Random(7)
    k = 0.62 if stage == "young" else 1.0
    bark = toon("bark", 0x6E4A32)
    mats = (toon("mangoLeafDark", 0x24662C), toon("mangoLeafMid", 0x348036), toon("mangoLeafLight", 0x5AA03E))
    b = R.Batch("mango")
    h = 0.30 * k
    stick(b, bark, (0, 0, 0), (0, 0, h * 0.92), 0.047 * k, 0.032 * k, segs=14)
    clumps = placed_clumps(MANGO_CLUMPS, k, h + 0.17 * k, shrink=0.77)
    for x, y, z, r in clumps:
        limb(b, bark, (0, 0, h * 0.86), (x * 0.9, y * 0.9, z - r * 0.25), 0.019 * k, 0.006 * k, rnd)
    crown(b, rnd, clumps, mats, bark, 2400, (6, 0.058 * (0.85 if k < 1 else 1), 0.017, 0.75))
    f = smooth_fruit()
    if stage == "ripe":
        bunches(b, f, rnd, clumps, 13, (3, 5), ball((fruit("mango"), fruit("mango"), fruit("mangoBlush")), 0.0125, (0.82, 0.74, 1.35)), bark)
    return R.group(b.objects() + f.objects(smooth=True), scale=R.BUILDING_SCALE)


def guava(stage):
    if stage == "sapling":
        return sapling(0xA9805A, (0x86C25A, 0x67AA45), (0.056, 0.031))
    toon = R.toon
    rnd = random.Random(17)
    k = 0.6 if stage == "young" else 0.85
    bark, patch = toon("guavaBark", 0xB08A66), toon("guavaBarkPeel", 0xC4A27E)   # guava bark peels in pale patches
    mats = (toon("guavaLeafDark", 0x4C8E36), toon("guavaLeafMid", 0x67AA45), toon("guavaLeafLight", 0x8EC456))
    b = R.Batch("guava")
    clumps = placed_clumps(GUAVA_CLUMPS, k, 0.33 * k)
    for s in (-1, 1):
        top = (s * 0.055 * k, 0.0, 0.27 * k)
        stick(b, bark, (s * 0.018 * k, 0, 0), top, 0.025 * k, 0.014 * k, segs=10)
        for i in range(5):
            t = rnd.uniform(0.1, 0.8)
            p = Vector((s * 0.018 * k, 0, 0)).lerp(Vector(top), t)
            r = 0.025 * k - 0.011 * k * t
            b.sphere(patch, r * 0.55, Matrix.Translation(p + Vector((math.cos(FRONT) * r * 0.75, math.sin(FRONT) * r * 0.75, 0))), scale=(0.8, 0.8, 1.7), segs=7, rings=5)
    for x, y, z, r in clumps:
        limb(b, bark, (math.copysign(0.04 * k, x or 1), 0, 0.22 * k), (x * 0.9, y * 0.9, z - r * 0.2), 0.011 * k, 0.004 * k, rnd)
    crown(b, rnd, clumps, mats, bark, 2300, (5, 0.040, 0.024, 0.55), squash=0.8)
    f = smooth_fruit()
    if stage == "ripe":
        bunches(b, f, rnd, clumps, 14, (2, 3), ball((fruit("guava"), fruit("guavaPale")), 0.0120, (1.0, 1.0, 1.1)), bark, squash=0.8, reach=1.0, hang=0.016)
    return R.group(b.objects() + f.objects(smooth=True), scale=R.BUILDING_SCALE)


def apple(stage):
    if stage == "sapling":
        return sapling(0x8A6A4C, (0x6FAA46, 0x528E38), (0.052, 0.028))
    toon = R.toon
    rnd = random.Random(23)
    k = 0.6 if stage == "young" else 0.85
    bark = toon("appleBark", 0x7C6450)
    mats = (toon("appleLeafDark", 0x38702C), toon("appleLeafMid", 0x4E8E36), toon("appleLeafLight", 0x74AC48))
    b = R.Batch("apple")
    stick(b, bark, (0, 0, 0), (0, 0, 0.22 * k), 0.032 * k, 0.021 * k, segs=12)
    clumps = placed_clumps(APPLE_CLUMPS, k, 0.32 * k)
    for x, y, z, r in clumps:
        limb(b, bark, (0, 0, 0.19 * k), (x * 0.9, y * 0.9, z - r * 0.15), 0.013 * k, 0.004 * k, rnd)
    crown(b, rnd, clumps, mats, bark, 2700, (5, 0.034, 0.020, 0.45), squash=0.85)
    f = smooth_fruit()
    if stage == "ripe":
        bunches(b, f, rnd, clumps, 15, (2, 3), ball((fruit("apple"), fruit("apple"), fruit("appleDeep")), 0.0122, (1.0, 1.0, 0.95)), bark, squash=0.85, reach=1.0, hang=0.016)
    return R.group(b.objects() + f.objects(smooth=True), scale=R.BUILDING_SCALE)


def paddle(batch, rnd, mat, rib, base, heading, length, width, rise, sag):
    """A banana leaf: a stalk, then a long broad blade arching over, split along its edges into strips by the wind."""
    n = 16
    side = Vector((-math.sin(heading), math.cos(heading), 0.0))
    spine = [Vector(base)]
    for i in range(1, n + 1):
        el = rise - sag * (i / n) ** 1.6
        spine.append(spine[-1] + Vector((math.cos(heading) * math.cos(el), math.sin(heading) * math.cos(el), math.sin(el))) * (length / n))

    def half(t):
        if t < 0.18:
            return 0.0
        u = (t - 0.18) / 0.82
        return width * 0.5 * min(1.0, math.sin(math.pi * min(0.97, u * 0.86 + 0.07)) * 1.18)

    verts, faces = batch.parts.setdefault(mat, ([], []))
    for sgn in (1, -1):
        for i in range(n):
            a, c = spine[i], spine[i + 1]
            w0, w1 = half(i / n), half((i + 1) / n)
            if w0 == 0.0 and w1 == 0.0:
                continue
            torn = rnd.random() < 0.30
            lag = (c - a) * (rnd.uniform(0.18, 0.42) if torn else 0.0)
            hang = rnd.uniform(0.004, 0.018) if torn else 0.002
            base_i = len(verts)
            verts.extend([a[:], c[:], (c + side * sgn * w1 - lag * 0.4 + Vector((0, 0, w1 * 0.22 - hang)))[:],
                          (a + side * sgn * w0 + lag * 0.6 + Vector((0, 0, w0 * 0.22 - hang)))[:]])
            quad = (base_i, base_i + 1, base_i + 2, base_i + 3)
            faces.append(quad if sgn > 0 else quad[::-1])
    for i in range(n):
        stick(batch, rib, spine[i] + Vector((0, 0, 0.0015)), spine[i + 1] + Vector((0, 0, 0.0015)), 0.0050 * (1 - i / n) + 0.0012, 0.0050 * (1 - (i + 1) / n) + 0.0012, segs=5)


def banana(stage):
    toon = R.toon
    rnd = random.Random(11)
    k = {"sapling": 0.35, "young": 0.65, "mature": 1.0, "ripe": 1.0}[stage]
    stem, sheath, sheath_dry = toon("bananaStemBark", 0x8CAA4C), toon("bananaSheathBark", 0x6E8A3A), toon("bananaPeelBark", 0x9A8250)
    leaves = (toon("bananaLeafA", 0x5CB440), toon("bananaLeafB", 0x479A36), toon("bananaLeafC", 0x74C24A))
    rib, dry = toon("bananaRib", 0xAECB6A), toon("bananaDryLeaf", 0xA88E4E)
    b = R.Batch("banana")
    top = 0.30 * k
    stick(b, stem, (0, 0, 0), (0, 0, top + 0.02 * k), 0.037 * k, 0.026 * k, segs=14)
    for i in range(7):                       # the pseudostem is wrapped leaf bases: each sheath ends in a slanting edge
        a = FRONT + rnd.uniform(-1.4, 1.4)
        z0, z1 = rnd.uniform(0.0, 0.5) * top, rnd.uniform(0.55, 0.95) * top
        r0, r1 = (0.040 - 0.014 * z0 / top) * k, (0.040 - 0.014 * z1 / top) * k
        stick(b, sheath if i % 3 else sheath_dry, (math.cos(a) * r0 * 0.93, math.sin(a) * r0 * 0.93, z0),
              (math.cos(a + 0.5) * r1 * 0.93, math.sin(a + 0.5) * r1 * 0.93, z1), 0.009 * k, 0.002 * k, segs=6)
    n = 8 if k > 0.5 else 5
    for i in range(n):                       # none points at the camera: seen end-on a paddle is a shapeless blob
        a = FRONT + math.radians(40) + i * math.radians(280) / (n - 1) + rnd.uniform(-0.12, 0.12)
        paddle(b, rnd, leaves[i % 3], rib, (0, 0, top), a, 0.282 * k * rnd.uniform(0.92, 1.04), 0.108 * k, rnd.uniform(0.75, 1.05), rnd.uniform(1.4, 2.1))
    paddle(b, rnd, leaves[2], rib, (0, 0, top), FRONT + math.pi, 0.15 * k, 0.034 * k, 1.5, 0.25)    # the new leaf, still upright and half rolled
    if k == 1.0:
        paddle(b, rnd, dry, sheath_dry, (0, 0, top - 0.02), FRONT + math.radians(98), 0.24, 0.07, -0.7, 0.8)
    f = smooth_fruit()
    if stage == "ripe":
        ripe, young, stalk, bud = fruit("banana"), fruit("bananaGreen"), toon("bananaStalk", 0x8FA84A), toon("bananaBud", 0x6E2440)
        bx, by = math.cos(FRONT) * 0.085, math.sin(FRONT) * 0.085
        limb(b, stalk, (0, 0, top + 0.01), (bx, by, top - 0.03), 0.009, 0.007, rnd, bend=0.3)
        stick(b, stalk, (bx, by, top - 0.03), (bx, by, top - 0.20), 0.007, 0.004)
        for hand in range(5):                # hands of fingers round the stalk, each finger curving up; the lowest still green
            z = top - 0.045 - hand * 0.030
            mat = young if hand == 4 else ripe
            m = 9 - hand // 2
            for j in range(m):
                a = j * math.tau / m + hand * 0.4
                p0 = Vector((bx + math.cos(a) * 0.010, by + math.sin(a) * 0.010, z))
                p1 = p0 + Vector((math.cos(a) * 0.028, math.sin(a) * 0.028, 0.000))
                p2 = p1 + Vector((math.cos(a) * 0.010, math.sin(a) * 0.010, 0.030))
                stick(f, mat, p0, p1, 0.0065, 0.0092, segs=7)
                f.sphere(mat, 0.0092, Matrix.Translation(p1), segs=7, rings=5)
                stick(f, mat, p1, p2, 0.0092, 0.0035, segs=7)
        f.sphere(bud, 0.024, Matrix.Translation((bx, by, top - 0.225)), scale=(0.85, 0.85, 1.45), segs=12, rings=8)
        stick(f, bud, (bx, by, top - 0.245), (bx, by, top - 0.285), 0.012, 0.001, segs=10)
    return R.group(b.objects() + f.objects(smooth=True), scale=R.BUILDING_SCALE)


def palmate(batch, rnd, mats, rib, tip, heading, tilt, size):
    """A papaya leaf: seven deep, toothed lobes spreading from the end of its stalk."""
    for q in range(-3, 4):
        reach = size * (1.0 - 0.11 * abs(q))
        a = heading + q * 0.62 + rnd.uniform(-0.1, 0.1)
        t = tilt - 0.12 - 0.10 * abs(q) + rnd.uniform(-0.1, 0.1)
        mat = mats[(q + 3) % 2]
        blade(batch, mat, tip, reach, reach * 0.34, a, t, rnd)
        d = Vector((math.cos(a) * math.cos(t), math.sin(a) * math.cos(t), math.sin(t)))
        stick(batch, rib, tip, Vector(tip) + d * reach * 0.7, 0.0012, 0.0004, segs=3)
        for side in (-1, 1):
            blade(batch, mat, Vector(tip) + d * reach * 0.45, reach * 0.34, reach * 0.14, a + side * 0.75, t - 0.1, rnd)


def papaya(stage):
    toon = R.toon
    rnd = random.Random(13)
    k = {"sapling": 0.35, "young": 0.65, "mature": 1.0, "ripe": 1.0}[stage]
    trunk, scar = toon("papayaTrunkBark", 0x948E72), toon("papayaScarBark", 0x635E48)
    leaves = (toon("papayaLeafA", 0x55A840), toon("papayaLeafB", 0x3E8A32))
    stalk, rib = toon("papayaStalk", 0xA8B456), toon("papayaRib", 0xB9CC78)
    b = R.Batch("papaya")
    h = 0.45 * k
    stick(b, trunk, (0, 0, 0), (0, 0, h), 0.029 * k, 0.019 * k, segs=14)
    for ring in range(int(13 * k)):           # the scars old leaves leave, in a spiral up the trunk
        z = h * (0.10 + 0.062 * ring)
        r = (0.029 - 0.010 * z / h) * k
        for j in range(4):
            a = FRONT - 1.6 + j * 1.05 + ring * 0.5
            b.sphere(scar, 0.0062 * k, Matrix.Translation((math.cos(a) * r * 0.80, math.sin(a) * r * 0.80, z + j * 0.004 * k)), scale=(1.0, 1.0, 0.45), segs=7, rings=4)
    count = 11 if k > 0.5 else 7
    for i in range(count):
        a = i * 2.4 + rnd.uniform(-0.2, 0.2)
        tilt = (0.95, 0.45, -0.1)[i % 3] + rnd.uniform(-0.15, 0.15)
        d = Vector((math.cos(a) * math.cos(tilt), math.sin(a) * math.cos(tilt), math.sin(tilt)))
        tip = Vector((0, 0, h)) + d * 0.125 * k
        limb(b, stalk, (0, 0, h - 0.01 * k), tip, 0.0048 * k, 0.0028 * k, rnd, bend=0.1, segs=5)
        palmate(b, rnd, leaves, rib, tip, a, tilt * 0.5, 0.082 * k)
    f = smooth_fruit()
    if stage == "ripe":
        ripe, green = fruit("papaya"), fruit("papayaGreen")
        for i in range(10):
            row = i // 4
            a = FRONT + (i % 4 - 1.5) * 0.62 + row * 0.3
            z = h - 0.055 - row * 0.048
            mat = green if row == 0 or i % 5 == 0 else ripe
            p = Matrix.Translation((math.cos(a) * 0.043, math.sin(a) * 0.043, z))
            f.sphere(mat, 0.0215, p, scale=(0.85, 0.85, 1.35), segs=12, rings=9)
            f.sphere(mat, 0.0135, p @ Matrix.Translation((0, 0, 0.022)), scale=(1, 1, 1.2), segs=10, rings=6)
    return R.group(b.objects() + f.objects(smooth=True), scale=R.BUILDING_SCALE)


# ---------------------------------------------------------------- the trees nobody plants: neem, babul, banyan

def litter(toon, centre, radius, count, hexes):
    """Fallen leaves and short tufts under a tree: what was today's flat shade disc."""
    rnd = random.Random(int(radius * 1000))
    b = R.Batch("litter")
    mats = [toon("litterLeaf%d" % i, h) for i, h in enumerate(hexes)]
    for i in range(count):
        a, r = rnd.uniform(0, math.tau), radius * rnd.random() ** 0.62
        blade(b, mats[i % len(mats)], (centre[0] + math.cos(a) * r, centre[1] + math.sin(a) * r, 0.002 + rnd.uniform(0, 0.004)),
              rnd.uniform(0.010, 0.020), 0.006, rnd.uniform(0, math.tau), rnd.uniform(-0.05, 0.3), rnd)
    return b.objects()


def neem_parts():
    toon = R.toon
    rnd = random.Random(9)
    bark = toon("neemBark", 0x54402E)
    mats = (toon("neemLeafDark", 0x33702C), toon("neemLeafMid", 0x488C38), toon("neemLeafLight", 0x6CAA46))
    stalk = toon("neemStalk", 0x7E9A48)
    b = R.Batch("neem")
    stick(b, bark, (0, 0, 0), (0, 0, 0.235), 0.032, 0.021, segs=12)
    clumps = [(0.0, 0.0, 0.385, 0.088)]
    for i in range(7):
        a = i * math.tau / 7 + 0.3
        clumps.append((math.cos(a) * 0.081, math.sin(a) * 0.081, 0.325 + rnd.uniform(-0.015, 0.03), 0.057))
    for x, y, z, r in clumps:
        limb(b, bark, (0, 0, 0.21), (x * 0.9, y * 0.9, z - r * 0.3), 0.012, 0.004, rnd)
    crown(b, rnd, clumps, mats, bark, 3600, None, squash=0.8, make=lambda mat, p, out: compound(b, rnd, mat, stalk, p, out + rnd.uniform(-0.5, 0.5), (4, 0.019, 0.0062, 0.036)))
    return b.objects() + litter(toon, (0.03, -0.03), 0.165, 1100, (0x4A6A2A, 0x5C7432, 0x6E5A2C, 0x3E5A26))


def neem():
    return R.group(neem_parts(), scale=1.3)


def village_tree():
    return R.refit(neem(), 0, 0.7)


def babul():
    toon = R.toon
    rnd = random.Random(12)
    bark, thorn = toon("babulBark", 0x45322A), toon("babulThorn", 0xD8CCB0)
    mats = (toon("babulLeafDark", 0x52702E), toon("babulLeafMid", 0x6A883A), toon("babulLeafLight", 0x8AA64C))
    stalk, bloom = toon("babulStalk", 0x7A8A44), fruit("banana")   # babul's little yellow puffs
    b = R.Batch("babul")
    fork = Vector((0.018, 0.0, 0.165))
    stick(b, bark, (0, 0, 0), fork, 0.018, 0.012, segs=9)
    clumps = []
    for tier, (z, rr, n) in enumerate(((0.232, 0.125, 7), (0.280, 0.075, 4))):
        for i in range(n):
            a = i * math.tau / n + tier * 0.8 + rnd.uniform(-0.2, 0.2)
            r = rr * rnd.uniform(0.65, 1.0)
            clumps.append((0.02 + math.cos(a) * r, math.sin(a) * r, z + rnd.uniform(-0.008, 0.01), 0.052))
    for x, y, z, r in clumps:
        limb(b, bark, fork, (x, y, z - 0.012), 0.0085, 0.0028, rnd, bend=0.22, segs=6)
        for t in (0.35, 0.6, 0.8):
            p = fork.lerp(Vector((x, y, z - 0.012)), t)
            a = rnd.uniform(0, math.tau)
            stick(b, thorn, p, p + Vector((math.cos(a) * 0.016, math.sin(a) * 0.016, rnd.uniform(-0.006, 0.012))), 0.0014, 0.0002, segs=3)
    crown(b, rnd, clumps, mats, bark, 7000, None, squash=0.36, hollow=0.35, twig=0.0014,
          make=lambda mat, p, out: compound(b, rnd, mat, stalk, p, out + rnd.uniform(-1.0, 1.0), (4, 0.0105, 0.0034, 0.026)))
    for i in range(24):
        x, y, z, r = rnd.choice(clumps)
        a = rnd.uniform(0, math.tau)
        b.sphere(bloom, 0.0038, Matrix.Translation((x + math.cos(a) * r * 0.8, y + math.sin(a) * r * 0.8, z + rnd.uniform(0.0, 0.02))), segs=6, rings=4)
    return R.group(b.objects(), scale=1.2)


def banyan():
    toon = R.toon
    rnd = random.Random(31)
    bark, root = toon("bark", 0x74543A), toon("banyanRootBark", 0x8E6E4E)
    mats = (toon("banyanLeafDark", 0x24602A), toon("banyanLeafMid", 0x34782F), toon("banyanLeafLight", 0x54983C))
    floor, stone, stone_dark = unshaded(toon("floor", 0xA8885C)), toon("stone", R.STONE), toon("stoneDark", R.STONE_DARK)
    o = [R.cylinder("floorDisc", 0.30, 0.30, 0.03, (0, 0, 0.015), floor, segs=48, outline=False),
         R.cylinder("platform", 0.100, 0.100, 0.052, (0, 0, 0.056), stone_dark, segs=32, outline=False),
         R.cylinder("platformTop", 0.108, 0.108, 0.012, (0, 0, 0.087), stone, segs=32, outline=False)]
    b = R.Batch("banyan")
    for i in range(26):                       # the platform's facing stones
        a = i * math.tau / 26
        b.box(stone, (0.020, 0.022, 0.040), Matrix.Translation((math.cos(a) * 0.099, math.sin(a) * 0.099, 0.058)) @ Matrix.Rotation(a, 4, "Z"))
    stick(b, bark, (0, 0, 0.06), (0, 0, 0.32), 0.056, 0.042, segs=14)
    for i in range(9):                        # the fluted trunk: buttress roots fused round it
        a = i * math.tau / 9 + rnd.uniform(-0.2, 0.2)
        limb(b, root, (math.cos(a) * 0.062, math.sin(a) * 0.062, 0.088), (math.cos(a + 0.3) * 0.030, math.sin(a + 0.3) * 0.030, rnd.uniform(0.24, 0.34)), 0.013, 0.008, rnd)
    clumps = [(0, 0, 0.43, 0.215), (0.18, 0.05, 0.385, 0.15), (-0.17, 0.08, 0.385, 0.15), (0.04, -0.18, 0.375, 0.15), (-0.08, 0.18, 0.40, 0.14), (0.0, 0.0, 0.54, 0.135)]
    for x, y, z, r in clumps:
        limb(b, bark, (0, 0, 0.27), (x * 0.95, y * 0.95, z - 0.07), 0.024, 0.008, rnd)
    for i in range(6):                        # prop roots down to the floor
        a = i * math.tau / 6
        x, y = math.cos(a) * 0.175, math.sin(a) * 0.175
        limb(b, root, (x * 0.92, y * 0.92, 0.36), (x, y, 0.025), 0.007, 0.010, rnd, bend=0.04, segs=6)
        for j in range(2):
            stick(b, root, (x * 0.92 + rnd.uniform(-0.02, 0.02), y * 0.92 + rnd.uniform(-0.02, 0.02), 0.36), (x + rnd.uniform(-0.012, 0.012), y + rnd.uniform(-0.012, 0.012), 0.03), 0.0025, 0.0025, segs=4)
    for i in range(70):                       # young aerial roots hanging short of the ground
        a, r = rnd.uniform(0, math.tau), rnd.uniform(0.11, 0.27)
        z = rnd.uniform(0.31, 0.36)
        x, y = math.cos(a) * r, math.sin(a) * r
        stick(b, root, (x, y, z), (x + rnd.uniform(-0.008, 0.008), y + rnd.uniform(-0.008, 0.008), z - rnd.uniform(0.05, 0.17)), 0.0016, 0.0006, segs=3)
    crown(b, rnd, clumps, mats, bark, 2300, (5, 0.044, 0.027, 0.5), squash=0.70)
    return R.group(o + b.objects(), scale=R.BUILDING_SCALE / 1.3)


def village_banyan():
    return R.refit(banyan(), 0, 0.85)


# ---------------------------------------------------------------- wiring

def install(render, samples, crops):
    """Returns {sprite name: builder} for this story; the caller holds each to today's frame (real_crops.hold_frame)."""
    global R, S, C
    R, S, C = render, samples, crops

    def with_patterns(build):
        def run():
            keep = list(S.PATTERNS)
            S.PATTERNS[:0] = PATTERNS
            plain_toon = R.toon
            R.toon = C.calibrated(plain_toon)
            try:
                return build()
            finally:
                S.PATTERNS[:] = keep
                R.toon = plain_toon
        return run

    sprites = {}
    for tree, fn in (("mango", mango), ("guava", guava), ("apple", apple), ("banana", banana), ("papaya", papaya)):
        for stage in ("sapling", "young", "mature", "ripe"):
            sprites["tree_%s_%s" % (tree, stage)] = with_patterns(lambda f=fn, s=stage: f(s))
    for name, fn in (("tree_neem", neem), ("village_tree", village_tree), ("tree_babul", babul), ("village_banyan", village_banyan), ("place_banyan", banyan)):
        sprites[name] = with_patterns(fn)
    return sprites
