"""
US-276 (spec 032 §3.1): the people, realistic. One body for every kind, 6 heads tall at today's height.

Limbs are single tubes along the pose's joints (no ball joints); parts of one material are fused into one surface (closed shoulders, a
nose that grows out of the face); cloth is laid on the body's own surface (saree, pallu, vest, gamchha) or built with folds (skirt, dhoti,
pagdi). Clothes and colours are render.py's PEOPLE, as approved in Sprint 5. Built facing -y, in the same model units as the old figure.
Used by render_real.py.
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

R = S = None

H = 0.172                      # today's figure's height before scaling
HD = H / 6                     # a head: 6 heads tall
HZ = H - HD * 0.52             # the head's centre
HA, HB, HC = 0.0116, 0.0122, 0.0139   # the head's half width, half depth, half height

MAP_KINDS = ("founder", "hand_m", "hand_f", "labourer_m", "labourer_f", "town_m", "town_f", "boy", "girl", "elder_m", "elder_f")
WORK_KINDS = ("founder", "hand_m", "hand_f", "labourer_m", "labourer_f", "town_m", "town_f")
PORTRAITS = ("founder", "hand_m", "hand_f", "labourer_m", "labourer_f", "town_m", "town_f", "boy", "girl", "elder_m", "elder_f",
             "advisor_farm", "advisor_bank", "advisor_people", "advisor_planner")
TURN = {"front": 30.0, "back": -115.0}   # degrees, the game's two facings
PORTRAIT_TURN = 30.0


# ---------------------------------------------------------------- mesh helpers: parts are (verts, faces) until they become objects

def loft(rings, cap=True, closed=True):
    n = len(rings[0])
    verts = [tuple(p) for ring in rings for p in ring]
    faces = []
    for i in range(len(rings) - 1):
        for j in range(n if closed else n - 1):
            a, b = i * n + j, i * n + (j + 1) % n
            faces.append((a, b, b + n, a + n))
    if cap:
        faces.append(tuple(range(n))[::-1])
        faces.append(tuple((len(rings) - 1) * n + j for j in range(n)))
    return verts, faces


def tube(pts, radii, n=14, ref=(0, 1, 0)):
    """A tapered round-ended tube along pts. radii: one a point, a number or (across, deep)."""
    pts = [Vector(p) for p in pts]
    rads = [r if isinstance(r, tuple) else (r, r) for r in radii]
    tans = [((pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()) for i in range(len(pts))]
    rings = []

    def ring(c, t, ru, rv):
        u = t.cross(Vector(ref))
        if u.length < 1e-6:
            u = t.cross(Vector((1, 0, 0)))
        u.normalize()
        v = t.cross(u).normalized()
        return [c + u * (ru * math.cos(k * math.tau / n)) + v * (rv * math.sin(k * math.tau / n)) for k in range(n)]

    for end, order in ((0, (1.35, 0.75)), (-1, (0.75, 1.35))):   # two shrinking rings at each end: a rounded cap
        c, t, (ru, rv) = pts[end], tans[end], rads[end]
        sign = -1 if end == 0 else 1
        caps = [ring(c + t * (sign * min(ru, rv) * math.sin(a)), t, ru * math.cos(a), rv * math.cos(a)) for a in order]
        if end == 0:
            rings += caps
            rings += [ring(pts[i], tans[i], *rads[i]) for i in range(len(pts))]
        else:
            rings += caps
    return loft(rings)


def ball(c, rx, ry=None, rz=None, n=18, m=12):
    ry, rz = rx if ry is None else ry, rx if rz is None else rz
    rings = []
    for i in range(1, m):
        a = -math.pi / 2 + math.pi * i / m
        rings.append([Vector(c) + Vector((rx * math.cos(a) * math.cos(k * math.tau / n), ry * math.cos(a) * math.sin(k * math.tau / n), rz * math.sin(a))) for k in range(n)])
    return loft(rings)


def obj(name, parts, mat, fuse=0.0, smooth=3, solid=0.0, bend=None, flat=False):
    """One object from several parts. fuse: a voxel size that melts overlapping closed parts into one surface."""
    verts, faces = [], []
    for v, f in parts:
        base = len(verts)
        verts += v
        faces += [tuple(i + base for i in face) for face in f]
    if bend:
        verts = [tuple(bend(Vector(v))) for v in verts]
    bm = bmesh.new()
    vs = [bm.verts.new(v) for v in verts]
    for face in faces:
        try:
            bm.faces.new([vs[i] for i in face])
        except ValueError:
            pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    if not flat:
        for p in me.polygons:
            p.use_smooth = True
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    if fuse:
        r = o.modifiers.new("fuse", "REMESH")
        r.mode = "VOXEL"
        r.voxel_size = fuse
        r.use_smooth_shade = True
        if smooth:
            s = o.modifiers.new("soften", "SMOOTH")
            s.factor = 0.5
            s.iterations = smooth
    if solid:
        s = o.modifiers.new("thick", "SOLIDIFY")
        s.thickness = solid
        s.offset = 0.0
    return o


def interp(table, x):
    """Linear interpolation over [(x, y...)] sorted by x."""
    if x <= table[0][0]:
        return table[0][1:]
    for a, b in zip(table, table[1:]):
        if x <= b[0]:
            t = (x - a[0]) / (b[0] - a[0])
            return tuple(p + (q - p) * t for p, q in zip(a[1:], b[1:]))
    return table[-1][1:]


def spline(pts, steps):
    """Catmull-Rom through points of any length."""
    out = []
    for i in range(len(pts) - 1):
        p0, p1, p2, p3 = pts[max(i - 1, 0)], pts[i], pts[i + 1], pts[min(i + 2, len(pts) - 1)]
        for k in range(steps):
            t = k / steps
            out.append(tuple(0.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t ** 3)
                             for a, b, c, d in zip(p0, p1, p2, p3)))
    out.append(tuple(pts[-1]))
    return out


# ---------------------------------------------------------------- the torso and the head as surfaces

#          z       half width  half depth
TORSO = [(0.086, 0.0146, 0.0102), (0.096, 0.0136, 0.0092), (0.105, 0.0126, 0.0086), (0.114, 0.0140, 0.0097), (0.123, 0.0162, 0.0110),
         (0.130, 0.0180, 0.0104), (0.1345, 0.0188, 0.0092), (0.1378, 0.0160, 0.0078), (0.1400, 0.0100, 0.0064), (0.1415, 0.0052, 0.0050)]


def torso_ring(z, off=0.0, n=40, wide=1.0):
    rx, ry = interp(TORSO, z)
    return [Vector(((rx * wide + off) * math.sin(k * math.tau / n), -(ry * wide + off) * math.cos(k * math.tau / n), z)) for k in range(n)]


#              angle from the front: the vest's top edge there (a scooped neck, two straps, open armholes, a higher back)
VEST_EDGE = [(0.0, 0.1262), (0.40, 0.1285), (0.66, 0.1335), (0.80, 0.1392), (1.04, 0.1392), (1.22, 0.1290), (1.57, 0.1222),
             (1.92, 0.1290), (2.10, 0.1392), (2.40, 0.1392), (2.60, 0.1345), (math.pi, 0.1318)]


def vest_surface(z0, off, n=144, steps=24):
    rings = []
    for i in range(steps + 1):
        ring = []
        for k in range(n):
            th = k * math.tau / n
            z = z0 + (interp(VEST_EDGE, front_angle(th))[0] - z0) * i / steps
            rx, ry = interp(TORSO, z)
            ring.append(Vector(((rx + off) * math.sin(th), -(ry + off) * math.cos(th), z)))
        rings.append(ring)
    return loft(rings, cap=False)


def torso_part(z0, z1, off=0.0, steps=18, cap=True, wide=1.0):
    return loft([torso_ring(z0 + (z1 - z0) * i / steps, off, wide=wide) for i in range(steps + 1)], cap=cap)


HEAD_EXP = 2.3


def head_point(th, t, off=0.0):
    """A point on the head: th round it from the face (0 is the nose), t from the chin (-1) to the crown (1)."""
    t = max(-1.0, min(1.0, t))
    s = (1 - abs(t) ** HEAD_EXP) ** (1 / HEAD_EXP)
    jaw = 1 - 0.30 * max(0.0, -t) ** 2
    depth = HB * (1.0 if math.cos(th) > 0 else 1.08) * (1 - 0.10 * max(0.0, -t) ** 2)
    p = Vector((HA * s * jaw * math.sin(th), -depth * s * math.cos(th), HZ + HC * t))
    if off:
        d = Vector((p.x / HA ** 2, p.y / HB ** 2, (p.z - HZ) / HC ** 2)).normalized()
        p += d * off
    return p


def head_cap(low, off, n=72, rows=18, wave=None):
    """A cap over the head from the line low(th) to the crown, standing off the skin by off(th, v) (v: 0 at the line, 1 at the crown)."""
    rings = []
    for i in range(rows + 1):
        v = i / rows
        ring = []
        for k in range(n):
            th = k * math.tau / n
            a = low(th)
            t = a + (0.999 - a) * v
            ring.append(head_point(th, t, off(th, v) + (wave(th, v) if wave else 0.0)))
        rings.append(ring)
    verts, faces = loft(rings, cap=False)
    faces.append(tuple(rows * n + k for k in range(n)))   # the crown closed
    return verts, faces


def front_angle(th):
    return abs(math.atan2(math.sin(th), math.cos(th)))


def face_line(th0, th1, t0, t1, radii, off=0.0003, arch=0.0):
    """A tapered stroke on the face (a brow, a lid, a lip, half a moustache)."""
    n = 9
    pts = [head_point(th0 + (th1 - th0) * i / (n - 1), t0 + (t1 - t0) * i / (n - 1) + arch * math.sin(math.pi * i / (n - 1)), off) for i in range(n)]
    return tube(pts, [interp([(0, radii[0]), (0.5, radii[1]), (1, radii[2])], i / (n - 1))[0] for i in range(n)], n=8, ref=(0, 0, 1))


# ---------------------------------------------------------------- the pose: joints, and the bend of the back

def pose_of(kind, pose):
    """The pose's joints. Side s: +1 is the person's left (+x), -1 their right."""
    p = R.PEOPLE[kind]
    bend_deg = 40.0 if pose == "work" else p.get("stoop", 0)
    hip_z = 0.094
    swing = 0.0
    lift = 0.0
    if pose.startswith("walk"):
        phase = int(pose[4:])
        swing = (1.0, 0.0, -1.0, 0.0)[phase]
        lift = 0.0028 if phase % 2 else 0.0

    def bend(v):
        w = max(0.0, min(1.0, (v.z - (hip_z - 0.006)) / 0.020))
        w = w * w * (3 - 2 * w)
        if not bend_deg or not w:
            return v + Vector((0, 0, lift))
        pivot = Vector((0, 0, hip_z))
        return pivot + Matrix.Rotation(math.radians(bend_deg * w), 3, "X") @ (v - pivot) + Vector((0, 0, lift))

    def rigid(v):   # the whole upper body's bend, for points that must not be eased (shoulders, the head's parts)
        pivot = Vector((0, 0, hip_z))
        return pivot + Matrix.Rotation(math.radians(bend_deg), 3, "X") @ (v - pivot) + Vector((0, 0, lift))

    legs, arms = {}, {}
    for s in (-1, 1):
        hip = Vector((s * 0.0070, 0, 0.090 + lift))
        knee = Vector((s * 0.0086, -0.0010, 0.049 + lift))
        ankle = Vector((s * 0.0092, 0.0012, 0.0080))
        if swing:
            m = Matrix.Rotation(math.radians(22 * swing * s), 3, "X")
            knee = hip + m @ (knee - hip)
            ankle = hip + m @ (ankle + Vector((0, 0, lift)) - hip)
            ankle.z = max(ankle.z - lift, 0.0080)
        elif pose.startswith("walk"):
            passing = s == (1 if int(pose[4:]) == 1 else -1)
            if passing:   # the leg swinging through: knee forward, foot off the ground
                knee += Vector((0, -0.007, 0.002))
                ankle += Vector((0, 0.004, 0.009))
        if pose == "drive":   # seated: thighs forward along the seat, shins down to the floor
            knee, ankle = Vector((s * 0.0128, -0.0400, 0.0905 + lift)), Vector((s * 0.0132, -0.0455, 0.0520))
        legs[s] = (hip, knee, ankle)

        sh = Vector((s * 0.0172, 0.0002, 0.1322))
        el = Vector((s * 0.0232, 0.0014, 0.1065))
        wr = Vector((s * 0.0246, -0.0028, 0.0815))
        if swing:
            m = Matrix.Rotation(math.radians(-18 * swing * s), 3, "X")
            el, wr = sh + m @ (el - sh), sh + m @ (wr - sh)
        sh2 = rigid(sh)
        if pose == "work":   # both hands on the hoe's handle, ahead of the knees
            wr2 = Vector((s * 0.0045, -0.0475 - 0.006 * (s < 0), 0.0880 - 0.011 * (s < 0)))
            el2 = (sh2 + wr2) / 2 + Vector((s * 0.0105, 0.003, -0.004))
        elif pose == "drive":   # both hands on the steering wheel
            wr2 = Vector((s * 0.0105, -0.0390, 0.1105))
            el2 = (sh2 + wr2) / 2 + Vector((s * 0.0080, 0.004, -0.009))
        else:
            el2, wr2 = sh2 + (el - sh), sh2 + (wr - sh)   # the arms hang straight down from a stooped shoulder
            tool = None if pose == "portrait" else p.get("tool")
            if tool == "hoe" and s == -1:      # the right hand holds the hoe on the shoulder
                el2, wr2 = sh2 + Vector((-0.0085, -0.002, -0.022)), sh2 + Vector((-0.0035, -0.0150, -0.012))
            elif tool == "basket" and s == 1:  # the left arm round the basket on the hip
                el2, wr2 = sh2 + Vector((0.0125, 0.006, -0.024)), sh2 + Vector((0.0235, -0.0085, -0.031))
            elif tool == "stick" and s == -1:
                el2, wr2 = sh2 + Vector((-0.0065, -0.003, -0.025)), sh2 + Vector((-0.0105, -0.0150, -0.043))
        arms[s] = (sh2, el2, wr2)
    return {"bend": bend, "rigid": rigid, "legs": legs, "arms": arms, "swing": swing, "lift": lift, "bend_deg": bend_deg}


def along(a, b, t):
    return a + (b - a) * t


def arm_part(sh, el, wr, s):
    inner = along(sh, Vector((0, sh.y, sh.z - 0.001)), 0.18)
    arm = tube([inner, sh, along(sh, el, 0.5), el, along(el, wr, 0.35), wr], [0.0036, 0.0044, 0.0043, 0.0036, 0.0036, 0.0025], n=14)
    down = (wr - el).normalized()
    palm = tube([wr, wr + down * 0.0050, wr + down * 0.0092], [(0.0019, 0.0025), (0.0022, 0.0034), (0.0014, 0.0022)], n=12, ref=(0, 1, 0))
    thumb = tube([wr + down * 0.0022 + Vector((-s * 0.001, -0.0024, 0)), wr + down * 0.0058 + Vector((-s * 0.0012, -0.0038, 0))], [0.0013, 0.0009], n=8)
    return [arm, palm, thumb]


def sleeve_part(sh, el, wr, reach, wide=0.0010):
    """A sleeve from inside the shoulder to reach of the arm (0.45: short; 1.0: the elbow; 1.9: nearly the wrist)."""
    inner = along(sh, Vector((0, sh.y, sh.z - 0.002)), 0.55)
    pts = [inner, sh, along(sh, el, min(reach, 1.0) * 0.5), along(sh, el, min(reach, 1.0))]
    rad = [0.0058, 0.0055 + wide, 0.0048 + wide, 0.0042 + wide]
    if reach > 1:
        pts += [along(el, wr, (reach - 1) * 0.5), along(el, wr, reach - 1)]
        rad += [0.0037 + wide, 0.0030 + wide]
    return tube(pts, rad, n=16)


def leg_part(hip, knee, ankle, hem=1.0):
    """The bare leg. Above the clothes' hem (a height) it is only a thin core, so no skin can push through the cloth as it moves."""
    calf = along(knee, ankle, 0.33) + Vector((0, 0.0012, 0))
    pts = [hip, along(hip, knee, 0.5), knee, calf, along(knee, ankle, 0.75), ankle]
    radii = [0.0072, 0.0060, 0.0044, 0.0049, 0.0032, 0.0027]
    return tube(pts, [r * (0.25 if q.z > hem + 0.003 else 1.0) for q, r in zip(pts, radii)], n=14)


def foot_parts(ankle, s):
    z = ankle.z - 0.0050
    heel = Vector((ankle.x, ankle.y + 0.0020, z))
    toe = Vector((ankle.x + s * 0.0010, ankle.y - 0.0095, z - 0.0006))
    foot = [tube([heel, along(heel, toe, 0.55), toe], [(0.0030, 0.0030), (0.0036, 0.0024), (0.0030, 0.0016)], n=12, ref=(0, 0, 1)),
            tube([ankle, heel + Vector((0, -0.002, 0.001))], [0.0027, 0.0028], n=10)]
    sole = tube([heel + Vector((0, 0.0012, -0.0026)), along(heel, toe, 0.55) + Vector((0, 0, -0.0024)), toe + Vector((0, -0.0012, -0.0016))],
                [(0.0038, 0.0009), (0.0043, 0.0009), (0.0036, 0.0008)], n=12, ref=(0, 0, 1))
    strap = tube([along(heel, toe, 0.62) + Vector((-0.0038, 0, -0.0006)), along(heel, toe, 0.62) + Vector((0, 0, 0.0028)),
                  along(heel, toe, 0.62) + Vector((0.0038, 0, -0.0006))], [0.0006] * 3, n=6)
    return foot, [sole, strap]


# ---------------------------------------------------------------- cloth

def skirt_rings(table, folds, pleats, sway, lift, n=180, steps=36, off=0.0):
    """A skirt from table [(z, half width, half depth)] with soft folds all round and knife pleats at the front, swaying as she walks."""
    z0, z1 = table[0][0], table[-1][0]
    rings = []
    for i in range(steps + 1):
        z = z0 + (z1 - z0) * i / steps
        rx, ry = interp(table, z)
        down = (z1 - z) / (z1 - z0)
        ring = []
        for k in range(n):
            th = k * math.tau / n
            a = math.atan2(math.sin(th), math.cos(th))
            f = 1 + folds * down * (0.65 * math.sin(7 * th + 1.3) + 0.35 * math.sin(13 * th))
            if pleats and abs(a) < 0.62:
                saw = ((a + 0.62) / 0.206) % 1.0
                f += pleats * (0.35 + 0.65 * down) * (saw - 0.5) * min(1.0, (0.62 - abs(a)) / 0.12)
            x, y = (rx + off) * f * math.sin(th), -(ry + off) * f * math.cos(th)
            ring.append(Vector((x + sway * 0.0022 * down, y - abs(sway) * 0.0050 * down * math.copysign(1, math.cos(th)), z + lift * (1 - down))))
        rings.append(ring)
    return rings


def wrap_ribbon(path, radius, widths, ripple=0.0, folds=3, across=7, steps=10):
    """A strip of cloth laid on the body: path is [(angle round the body, z, stand-off)], widths one a path point; radius(z) is the body's."""
    pts = spline([(th, z, off, w) for (th, z, off), w in zip(path, widths)], steps)
    SC = 0.015    # an angle's length on the body, for measuring across the strip
    rings = []
    for i, (th, z, off, w) in enumerate(pts):
        a, b = pts[max(i - 1, 0)], pts[min(i + 1, len(pts) - 1)]
        d = Vector(((b[0] - a[0]) * SC, b[1] - a[1])).normalized()
        nrm = Vector((-d.y, d.x))
        row = []
        for k in range(across):
            u = k / (across - 1) - 0.5
            s2, z2 = th * SC + nrm.x * u * w, z + nrm.y * u * w
            th2 = s2 / SC
            rx, ry = radius(min(z2, 0.1412))
            o = off + ripple * math.sin(u * folds * math.tau)
            row.append(Vector(((rx + o) * math.sin(th2), -(ry + o) * math.cos(th2), min(z2, 0.1428))))
        rings.append(row)
    return loft(rings, cap=False, closed=False)


def leg_wrap(hip, knee, ankle, bottom_z, r_top, r_end, folds=0.10, n=64, steps=18, band=None):
    """A dhoti's or pyjama's leg round the pose's leg, down to bottom_z, with folds. band: (from, to) as a share of its length."""
    path = [Vector((hip.x * 0.9, hip.y, hip.z + 0.006)), hip, knee]
    if bottom_z < knee.z:
        path.append(along(knee, ankle, min(1.0, (knee.z - bottom_z) / max(1e-6, knee.z - ankle.z))))
    else:
        path = [path[0], hip, along(hip, knee, min(1.0, (hip.z - bottom_z) / (hip.z - knee.z)))]
    pts = [Vector(p) for p in spline([tuple(p) for p in path], 8)]
    lengths = [0.0]
    for a, b in zip(pts, pts[1:]):
        lengths.append(lengths[-1] + (b - a).length)
    total = lengths[-1]
    lo, hi = band or (0.0, 1.0)
    rings = []
    for i in range(steps + 1):
        s = lo + (hi - lo) * i / steps
        d = s * total
        j = next(k for k in range(1, len(lengths)) if lengths[k] >= d - 1e-9)
        c = along(pts[j - 1], pts[j], (d - lengths[j - 1]) / max(1e-9, lengths[j] - lengths[j - 1]))
        r = r_top + (r_end - r_top) * s + (0.0004 if band else 0.0)
        rings.append([c + Vector((r * math.sin(k * math.tau / n), -r * math.cos(k * math.tau / n), 0)) *
                      (1 + folds * s * math.sin(6 * k * math.tau / n + 9 * s)) for k in range(n)])
    return loft(rings, cap=False)


# ---------------------------------------------------------------- the figure

def build(kind, pose="stand"):
    """The kind's parts, facing -y, unturned and unscaled."""
    p = R.PEOPLE[kind]
    female = p["sex"] == "f"
    wear = p["wear"]
    j = pose_of(kind, pose)
    bend, rigid, legs, arms, swing, lift = j["bend"], j["rigid"], j["legs"], j["arms"], j["swing"], j["lift"]

    def m(name, hexv):
        return R.toon(kind + "_r_" + name, hexv)

    def shade(hexv, k):
        return int((hexv >> 16 & 255) * k) << 16 | int((hexv >> 8 & 255) * k) << 8 | int((hexv & 255) * k)

    skin, top, bottom, accent = m("skin", p["skin"]), m("top", 0xE0A01C if kind == "boy" else p["top"]), m("bottom", p["bottom"]), m("accent", p["accent"])
    hair = m("hair", p.get("hair", R.HAIR))
    for n in hair.node_tree.nodes:
        if n.type == "BSDF_PRINCIPLED":
            n.inputs["Roughness"].default_value = 0.72   # oiled hair, not a shiny helmet
    body = m("blouse", p["blouse"]) if female else top
    leather, wood = m("leather", 0x5A3A22), m("wood", R.WOOD)
    o = []

    # ---- skin: the torso, the arms and the legs, each one surface
    o.append(obj("chestSkin", [torso_part(0.086, 0.1415, -0.0006)], skin, bend=bend))
    for s in (-1, 1):
        o.append(obj("arm%d" % s, arm_part(*arms[s], s), skin, fuse=0.00055, smooth=2))
        hip, knee, ankle = legs[s]
        if wear == "saree":   # only the ankle: the skirt hides the rest, and a swinging leg must not cut through it
            o.append(obj("leg%d" % s, [tube([along(knee, ankle, 0.86), ankle], [0.0027, 0.0027], n=12)], skin))
        else:
            hem = {"banyan": 0.046, "dhoti": 0.027, "shorts": 0.060, "frock": 0.060}.get(wear, 0.027 if kind == "elder_m" else 0.0125)
            o.append(obj("leg%d" % s, [leg_part(hip, knee, ankle, hem)], skin))
        foot, chappal = foot_parts(legs[s][2], s)
        o.append(obj("foot%d" % s, foot, skin, fuse=0.0005, smooth=2))
        o.append(obj("chappal%d" % s, chappal, leather))

    # ---- the head: skull, jaw, nose and ears fused, then the face drawn on it
    face = [loft([[head_point(k * math.tau / 48, -1 + 2 * i / 28) for k in range(48)] for i in range(1, 28)])]
    small = 0.72 if p.get("size", 1.0) < 1.0 else 1.0   # a child's nose
    face.append(tube([head_point(0, 0.10, -0.0004), head_point(0, -0.08, 0.0006 * small), head_point(0, -0.22, 0.0012 * small)], [0.0006, 0.0009 * small, 0.0012 * small], n=10))
    for s in (-1, 1):
        face.append(ball(head_point(s * 0.13, -0.235, 0.0001), 0.0009 * small))
        face.append(ball(head_point(s * 1.62, -0.06, 0.0006), 0.0012, 0.0021, 0.0034))
    face.append(tube([Vector((0, 0.001, 0.136)), Vector((0, 0.0006, HZ - HC * 0.55))], [0.0049, 0.0046], n=16))
    o.append(obj("head", face, skin, fuse=0.0004, smooth=2, bend=rigid))

    eye_white, iris = m("eyeWhite", 0xD8CFC4), m("eye", 0x1C100A)
    child = p.get("size", 1.0) < 1.0
    eye = 1.18 if child else 1.0
    brow = 0.75 if child else 1.0
    for s in (-1, 1):
        c = head_point(s * 0.40, 0.045, -0.0001)
        o.append(obj("eyeW%d" % s, [ball(c, 0.0019 * eye, 0.0008, 0.0011 * eye)], eye_white, bend=rigid))
        o.append(obj("iris%d" % s, [ball(head_point(s * 0.385, 0.040, 0.0005), 0.00095 * eye, 0.0006, 0.00100 * eye)], iris, bend=rigid))
        o.append(obj("lid%d" % s, [face_line(s * 0.22, s * 0.58, 0.108, 0.092, (0.00022, 0.00042, 0.00022), 0.0006, 0.030)], m("lash", 0x24150E), bend=rigid))
        o.append(obj("brow%d" % s, [face_line(s * 0.17, s * 0.66, 0.255, 0.215, (0.00048 * brow, 0.00046 * brow, 0.00012), 0.00025, 0.040)], hair, bend=rigid))
    lips = m("lips", 0x8E4034 if female else 0x7C4234)
    o.append(obj("lipUp", [face_line(-0.22, 0.22, -0.455, -0.455, (0.00018, 0.00058, 0.00018), 0.0001, 0.018)], lips, bend=rigid))
    o.append(obj("lipLow", [face_line(-0.18, 0.18, -0.525, -0.525, (0.00018, 0.00068, 0.00018), 0.0000, -0.035)], lips, bend=rigid))
    if not female and p.get("size", 1.0) == 1.0:
        for s in (-1, 1):
            o.append(obj("moustache%d" % s, [face_line(s * 0.015, s * 0.40, -0.350, -0.510, (0.00095, 0.00090, 0.00020), 0.0005, 0.030)], hair, bend=rigid))
    if female:
        o.append(obj("bindi", [ball(head_point(0, 0.33, 0.0002), 0.00075, 0.0005, 0.00075)], m("bindi", 0xB8202A), bend=rigid))
        if p.get("size", 1.0) == 1.0:
            for s in (-1, 1):
                o.append(obj("earring%d" % s, [ball(head_point(s * 1.60, -0.34, 0.0013), 0.0013)], m("gold", 0xE8B83A), bend=rigid))
    if p.get("glasses"):
        frame = m("frame", p["glasses"])
        parts = []
        for s in (-1, 1):
            c = head_point(s * 0.40, 0.035, 0.0017)
            across = Vector((math.cos(s * 0.40), math.sin(s * 0.40), 0))
            rim = [c + across * (0.0031 * math.cos(k * math.tau / 24)) + Vector((0, 0, 0.0025 * math.sin(k * math.tau / 24))) for k in range(25)]
            parts.append(tube(rim, [0.00032] * 25, n=6))
            parts.append(tube([c + across * (s * 0.0031), head_point(s * 1.55, 0.03, 0.0016)], [0.00028] * 2, n=6))
        parts.append(tube([head_point(-0.13, 0.07, 0.0022), head_point(0, 0.10, 0.0030), head_point(0.13, 0.07, 0.0022)], [0.00028] * 3, n=6))
        o.append(obj("glasses", parts, frame, bend=rigid))

    # ---- hair and headwear
    if female:
        line = [(0.0, 0.70), (0.45, 0.62), (0.9, 0.42), (1.25, 0.12), (1.60, -0.02), (2.0, -0.40), (2.6, -0.58), (math.pi, -0.62)]
    else:
        line = [(0.0, 0.68), (0.55, 0.66), (0.95, 0.44), (1.22, -0.02), (1.40, 0.28), (1.75, 0.24), (2.1, -0.28), (math.pi, -0.50)]

    def hairline(th):
        return interp(line, front_angle(th))[0]

    def hair_off(th, v):
        part = 0.0006 * math.exp(-(front_angle(th) / 0.06) ** 2) if female else 0.0   # a centre parting
        return 0.00035 + 0.0008 * min(1.0, v * 3) - part

    def strands(th, v):
        return 0.00010 * math.sin(46 * th + 5 * v) * min(1.0, v * 4) * max(0.0, 1 - v * 1.2)

    if p.get("head") == "pagdi":   # only what shows below the cloth
        o.append(obj("hair", [head_cap(lambda th: min(hairline(th), 0.30), lambda th, v: 0.0004)], hair, bend=rigid, solid=0.0003))
    else:
        o.append(obj("hair", [head_cap(hairline, hair_off, wave=strands)], hair, bend=rigid, solid=0.0005))
    if p.get("plaits"):
        for s in (-1, 1):
            path = [head_point(s * 2.0, -0.30, 0.001), Vector((s * 0.0125, 0.004, 0.139)), Vector((s * 0.0150, -0.006, 0.128)), Vector((s * 0.0140, -0.012, 0.114))]
            pts = [Vector(v) for v in spline([tuple(v) for v in path], 5)]
            o.append(obj("plait%d" % s, [ball(c, 0.0033 - 0.0012 * i / len(pts), rz=0.0026) for i, c in enumerate(pts)], hair, bend=rigid))
            o.append(obj("ribbon%d" % s, [ball(pts[-1] + Vector((s * dx, 0, -0.002)), 0.0026, 0.0014, 0.0017) for dx in (-0.0018, 0.0018)], m("ribbon", 0xC8202E), bend=rigid))
    elif female:
        o.append(obj("bun", [ball(head_point(math.pi, -0.22, 0.0030), 0.0060, 0.0050, 0.0055)], hair, bend=rigid))
    if p.get("head") == "pagdi":
        def edge(th):
            return interp([(0.0, 0.46), (0.9, 0.40), (1.7, 0.24), (math.pi, -0.02)], front_angle(th))[0]

        def turns(th, v):   # the wound cloth: ridges that slant across one another
            lap = math.sin(21 * v + 2.2 * math.sin(th))
            return 0.00075 * (abs(lap) ** 0.5) * (1 if lap > 0 else -0.3) * (1 - v ** 3)

        o.append(obj("pagdi", [head_cap(edge, lambda th, v: 0.0022 + 0.0018 * math.sin(math.pi * min(1.0, v * 1.15)), n=96, rows=40, wave=turns),
                               tube([head_point(k * math.tau / 40, edge(k * math.tau / 40), 0.0017) for k in range(41)], [0.0011] * 41, n=8)], accent, bend=rigid))
        o.append(obj("pagdiTail", [tube([head_point(2.85, 0.05, 0.0022), Vector((0.0045, 0.0150, 0.146)), Vector((0.0060, 0.0128, 0.130))],
                                        [(0.0030, 0.0007), (0.0042, 0.0007), (0.0048, 0.0006)], n=10)], accent, bend=bend))
    elif p.get("head") == "topi":
        cap = head_cap(lambda th: 0.50, lambda th, v: 0.0016 + 0.0010 * v, rows=14)
        peak = []
        for x, y, z in cap[0]:
            up = max(0.0, (z - HZ) / HC - 0.70) / 0.30   # towards the crown: pinched side to side, a ridge along front to back
            peak.append((x * (1 - 0.22 * up), y * (1 + 0.14 * up), min(z + 0.0006 * up, HZ + HC + 0.0022)))
        o.append(obj("topi", [(peak, cap[1])], m("topi", 0xE9E6DC), bend=rigid, solid=0.0006))

    # ---- the upper garment
    def sleeves(reach, wide=0.0010):
        return [sleeve_part(*arms[s], reach, wide) for s in (-1, 1)]

    def cloth(name, z0, z1, off, mat, parts, bent=True):
        """A torso garment with its sleeves fused on: the shoulders are one closed surface. The torso's part follows the back's bend."""
        v, f = torso_part(z0, z1, off + 0.0004)
        if bent:
            v = [tuple(bend(Vector(q))) for q in v]
        o.append(obj(name, [(v, f)] + parts, mat, fuse=0.0006, smooth=3))

    if female and wear == "saree":
        cloth("blouse", 0.114, 0.1398, 0.0007, body, sleeves(0.62, 0.0007))
    elif wear == "frock":
        cloth("bodice", 0.096, 0.1398, 0.0008, top, sleeves(0.40, 0.0016))
        collar = [(a, 0.1385 - 0.004 * math.cos(a) ** 8, 0.0017) for a in (-1.7, -0.9, -0.35, 0.0, 0.35, 0.9, 1.7)]
        o.append(obj("collar", [wrap_ribbon(collar, lambda z: interp(TORSO, z), [0.0034] * 7)], accent, bend=bend, solid=0.0005))
    elif wear == "banyan":
        o.append(obj("banyan", [vest_surface(0.095, 0.0009)], top, bend=bend, solid=0.0006))
    elif wear == "shorts":
        cloth("shirt", 0.097, 0.1398, 0.0010, top, sleeves(0.55, 0.0012))
    elif wear == "dhoti":
        cloth("shirt", 0.099, 0.1398, 0.0008, top, sleeves(0.62, 0.0009))
    else:   # kurta: one cloth from the shoulders to the knees, with long sleeves
        cloth("kurtaTop", 0.094, 0.1398, 0.0009, top, sleeves(1.78, 0.0008))
        hem = [(0.062, 0.0182, 0.0124), (0.078, 0.0174, 0.0116), (0.088, 0.0166, 0.0116), (0.0975, 0.0146, 0.0103)]
        rings = skirt_rings(hem, 0.045, 0.0, swing * 0.5, 0.0, steps=18)
        o.append(obj("kurtaHem", [loft([[bend(v) if v.z > 0.088 else v + Vector((0, 0, lift)) for v in ring] for ring in rings], cap=False)], top, solid=0.0007))
        placket = [(0.0, z, 0.0016) for z in (0.108, 0.120, 0.131, 0.1375)]
        o.append(obj("placket", [wrap_ribbon(placket, lambda z: interp(TORSO, z), [0.0022] * 4)], m("topShade", shade(p["top"], 0.86)), bend=bend, solid=0.0004))
        o.append(obj("buttons", [ball(Vector((0, -(interp(TORSO, z)[1] + 0.0022), z)), 0.00065) for z in (0.115, 0.123, 0.131)], m("button", 0x7A6248), bend=bend))
    if p.get("vest"):
        open_front = 0.07 if p.get("head") == "topi" else 0.12
        rows = []
        for i in range(25):
            z = 0.096 + (0.1385 - 0.096) * i / 24
            gap = open_front + max(0.0, z - 0.120) * 34
            ring = []
            for k in range(49):
                th = gap + (math.tau - 2 * gap) * k / 48
                rx, ry = interp(TORSO, z)
                ring.append(Vector(((rx + 0.0021) * math.sin(th), -(ry + 0.0021) * math.cos(th), z)))
            rows.append(ring)
        o.append(obj("vest", [loft(rows, cap=False, closed=False)], m("vest", p["vest"]), bend=bend, solid=0.0008))

    # ---- below the waist
    if wear == "saree":
        table = [(0.0045, 0.0212, 0.0176), (0.030, 0.0190, 0.0152), (0.060, 0.0172, 0.0132), (0.088, 0.0171, 0.0122), (0.1075, 0.0138, 0.0099)]
        sway = swing
        o.append(obj("sareeSkirt", [loft(skirt_rings(table, 0.060, 0.115, sway, lift), cap=False)], bottom, solid=0.0007))
        o.append(obj("sareeHem", [loft(skirt_rings([(0.0045, 0.0212, 0.0176), (0.0105, 0.0206, 0.0170)], 0.060, 0.115, sway, 0.0, steps=3, off=0.0005), cap=False)], accent, solid=0.0005))

        def on_body(z):
            return interp(TORSO, z) if z >= 0.105 else interp([(t[0], t[1], t[2]) for t in table], z)

        # The pallu: from the right hip, across the chest, over the left shoulder and down the back.
        path = [(-1.30, 0.1060, 0.0016), (-0.60, 0.1100, 0.0019), (0.02, 0.1185, 0.0021), (0.55, 0.1275, 0.0022), (0.95, 0.1355, 0.0022),
                (1.55, 0.1402, 0.0022), (2.30, 0.1360, 0.0022), (2.70, 0.1220, 0.0030), (2.80, 0.1000, 0.0050), (2.82, 0.0700, 0.0072)]
        widths = [0.0115, 0.0140, 0.0150, 0.0135, 0.0100, 0.0085, 0.0105, 0.0140, 0.0170, 0.0185]
        o.append(obj("pallu", [wrap_ribbon(path, on_body, widths, ripple=0.00034, folds=3, across=19)], m("topPallu", p["top"]), bend=bend, solid=0.0006))
        edge_path = [(th, z - w * 0.44 * (1 if th < 2.0 else -1), off + 0.0007) for (th, z, off), w in zip(path[:6], widths[:6])]
        o.append(obj("palluEdge", [wrap_ribbon(edge_path, on_body, [0.0018] * 6, across=4)], accent, bend=bend, solid=0.0004))
    elif wear == "frock":
        table = [(0.0560, 0.0225, 0.0185), (0.078, 0.0188, 0.0145), (0.097, 0.0145, 0.0102), (0.1050, 0.0133, 0.0096)]
        o.append(obj("frockSkirt", [loft(skirt_rings(table, 0.115, 0.0, swing * 0.6, lift, steps=22), cap=False)], bottom, solid=0.0007))
        o.append(obj("frockHem", [loft(skirt_rings([(0.0560, 0.0225, 0.0185), (0.0610, 0.0214, 0.0175)], 0.115, 0.0, swing * 0.6, lift, steps=2, off=0.0005), cap=False)], accent, solid=0.0005))
    elif wear == "shorts":
        o.append(obj("shortsTop", [torso_part(0.083 + lift, 0.099 + lift, 0.0014)], bottom))
        for s in (-1, 1):
            o.append(obj("shortsLeg%d" % s, [leg_wrap(*legs[s], 0.060, 0.0088, 0.0068, folds=0.04)], bottom, solid=0.0007))
    elif wear == "kurta" and kind != "elder_m":
        for s in (-1, 1):
            o.append(obj("pyjama%d" % s, [leg_wrap(*legs[s], 0.0125, 0.0080, 0.0038, folds=0.07)], bottom, solid=0.0007))
    else:   # a dhoti (with a shirt, a banyan or an elder's kurta)
        short = wear == "banyan"   # rolled up for work
        end = 0.046 if short else 0.027
        if wear != "kurta":
            o.append(obj("dhotiWaist", [loft(skirt_rings([(0.084, 0.0166, 0.0120), (0.093, 0.0155, 0.0112), (0.1035, 0.0139, 0.0100)], 0.03, 0.0, 0.0, lift, steps=8), cap=True)], bottom))
        for s in (-1, 1):
            o.append(obj("dhoti%d" % s, [leg_wrap(*legs[s], end, 0.0093, 0.0066 if short else 0.0056, folds=0.13)], bottom, solid=0.0007))
            o.append(obj("dhotiBorder%d" % s, [leg_wrap(*legs[s], end, 0.0093, 0.0066 if short else 0.0056, folds=0.13, steps=2, band=(0.93, 0.985))],
                         m("accentBorder", 0x2E6FBA if p.get("head") == "pagdi" else shade(p["bottom"], 0.72)), solid=0.0005))
        if wear != "kurta" and pose != "drive":   # the pleats tucked in at the front, falling between the legs
            fall = []
            for i in range(14):
                t = i / 13
                z = 0.101 - t * (0.101 - (end + 0.010)) + lift
                w = 0.0040 + 0.0050 * t
                fall.append([Vector(((k / 12 - 0.5) * w * 2 * 0.8, -(0.0120 + 0.0022 * math.sin(math.pi * t)) - 0.0011 * abs(((k / 12) * 4) % 1 - 0.5), z)) for k in range(13)])
            o.append(obj("dhotiFall", [loft(fall, cap=False, closed=False)], m("bottomFall", shade(p["bottom"], 0.95)), solid=0.0006, flat=True))
    if p.get("wear") == "kurta" and p.get("head") != "topi" and kind != "elder_m" or p.get("tool") == "hoe" or p.get("head") == "topi":
        sc = [(0.42, 0.1000, 0.0030), (0.55, 0.1180, 0.0030), (0.78, 0.1330, 0.0030), (1.30, 0.1402, 0.0030), (2.35, 0.1330, 0.0030), (2.55, 0.1140, 0.0032)]
        o.append(obj("gamchha", [wrap_ribbon(sc, lambda z: interp(TORSO, z), [0.0074, 0.0070, 0.0062, 0.0056, 0.0066, 0.0080], ripple=0.0005, folds=2, across=13)],
                     m("accentGamchha", p["accent"]), bend=bend, solid=0.0008))
    elif p.get("head") == "pagdi" and kind == "hand_m":
        sc = [(0.42, 0.1060, 0.0036), (0.55, 0.1210, 0.0034), (0.80, 0.1345, 0.0034), (1.30, 0.1405, 0.0034), (2.35, 0.1335, 0.0034), (2.55, 0.1160, 0.0036)]
        o.append(obj("gamchha", [wrap_ribbon(sc, lambda z: interp(TORSO, z), [0.0060, 0.0058, 0.0054, 0.0052, 0.0060, 0.0072], ripple=0.0005, folds=2, across=13)],
                     m("accentGamchha", 0xB93A30), bend=bend, solid=0.0008))
    if female and p.get("size", 1.0) == 1.0:
        for s in (-1, 1):
            sh, el, wr = arms[s]
            axis = (wr - el).normalized()
            for k, hexv in enumerate((0xC8202E, 0xE8B83A, 0x24784A)):
                c = wr - axis * (0.0016 + 0.0019 * k)
                u = axis.orthogonal().normalized()
                v = axis.cross(u)
                ring = [c + (u * math.cos(i * math.tau / 20) + v * math.sin(i * math.tau / 20)) * 0.0033 for i in range(21)]
                o.append(obj("bangle%d_%d" % (s, k), [tube(ring, [0.00055] * 21, n=6)], m("bangle%d" % k, hexv)))

    # ---- tools
    iron = m("iron", 0x6E747A)
    tool = p.get("tool")
    if pose == "work":
        top_end, foot = Vector((0.0004, -0.0400, 0.0975)), Vector((0.0000, -0.0745, 0.0055))
        o.append(obj("hoeHandle", [tube([top_end, foot], [0.0017, 0.0019], n=10)], wood))
        o.append(obj("hoeBlade", [tube([foot + Vector((-0.0085, -0.002, -0.001)), foot + Vector((0.0085, -0.002, -0.001))], [(0.0075, 0.0016)] * 2, n=8, ref=(0, 0, 1))], iron))
    elif tool == "hoe":
        wr = arms[-1][2]
        a, b = wr + Vector((0.0022, -0.006, -0.008)), wr + Vector((0.0030, 0.050, 0.047))
        o.append(obj("hoeHandle", [tube([a, b], [0.0017, 0.0017], n=10)], wood))
        o.append(obj("hoeBlade", [tube([b + Vector((-0.0075, 0.0, 0.002)), b + Vector((0.0075, 0.0, 0.002))], [(0.0020, 0.0085)] * 2, n=8, ref=(0, 1, 0))], iron))
    elif tool == "stick":
        wr = arms[-1][2]
        grip = wr + Vector((0, 0, -0.005))
        o.append(obj("stick", [tube([grip + Vector((0, 0, 0.010)), grip, Vector((grip.x - 0.003, grip.y - 0.008, 0.002))], [0.0018, 0.0017, 0.0015], n=10)], wood))
    if tool == "basket" and pose not in ("work", "drive"):
        c = Vector((0.0315, -0.0040, 0.0930 + lift))
        n = 36
        rings = [[c + Vector(((0.0085 + 0.0055 * t + 0.0005 * math.sin(18 * k * math.tau / n + 9 * t)) * math.sin(k * math.tau / n),
                              -(0.0085 + 0.0055 * t) * math.cos(k * math.tau / n), -0.006 + 0.012 * t)) for k in range(n)] for t in (0, 0.2, 0.4, 0.6, 0.8, 1.0)]
        o.append(obj("basket", [loft(rings, cap=False), loft([rings[0], [c + Vector((0, 0, -0.006))] * n], cap=False),
                                tube(rings[-1] + [rings[-1][0]], [0.0011] * (n + 1), n=8)], m("bambooCane", 0xA87832), solid=0.0006))
        veg = (0xB83428, 0x3E8E34, 0xD9A62E)
        for i, (dx, dy) in enumerate(((-0.0065, -0.002), (0.0005, -0.0065), (0.0068, 0.000), (0.000, 0.0035), (-0.001, -0.001))):
            o.append(obj("veg%d" % i, [ball(c + Vector((dx, dy, 0.0058 + (i == 4) * 0.002)), 0.0042)], m("veg%d" % (i % 3), veg[i % 3])))
    return o


def turn(parts, degrees, scale):
    """Turns and scales the figure. Not R.group: it resets every solidify to the toon outline's width, and here solidify is the cloth's thickness."""
    m = Matrix.Diagonal((scale, scale, scale, 1.0)) @ Matrix.Rotation(math.radians(degrees), 4, "Z")
    for o in parts:
        o.matrix_world = m @ o.matrix_world
    return parts


def figure(kind, pose, facing):
    size = R.PEOPLE[kind].get("size", 1.0)
    return turn(build(kind, pose), TURN[facing], R.PERSON_SCALE * size)


# ---------------------------------------------------------------- the portrait: head and shoulders, seen nearly level

def render_portrait(sc, cam, name, kind, out_dir):
    size = R.PEOPLE[kind].get("size", 1.0)
    scale = R.PERSON_SCALE * size
    parts = build(kind, "portrait")
    for q in parts:   # the hoe over the shoulder and the stick would fill the card
        if q.name.startswith(("hoe", "stick", "basket", "veg")):
            bpy.data.objects.remove(q, do_unlink=True)
    parts = [q for q in bpy.context.scene.collection.objects if q.type == "MESH"]
    turn(parts, PORTRAIT_TURN, scale)
    cam.rotation_euler = (math.radians(80), 0.0, math.radians(45))
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    aim = cam.matrix_world.to_3x3()
    inv = aim.inverted()
    chest = 0.1160 * scale
    xs, ys = [], []
    head_x = []
    for q in parts:
        ev = q.evaluated_get(dg)
        for v in ev.data.vertices:
            w = ev.matrix_world @ v.co
            if w.z < chest:
                continue
            c = inv @ w
            ys.append(c.y)
            xs.append(c.x)
            if w.z > (HZ - HC) * scale:
                head_x.append(c.x)
    top_y, low_y = max(ys), min(ys)
    span = (top_y - low_y) * 1.07
    cx = (min(head_x) + max(head_x)) / 2
    cy = low_y + span / 2   # the frame's bottom edge cuts the chest
    sc.render.resolution_x = sc.render.resolution_y = R.ICON_PX
    cam.data.ortho_scale = span
    cam.location = aim @ Vector((cx, cy, 10))
    bpy.context.view_layer.update()
    sc.render.filepath = out_dir + "/" + name + ".png"
    bpy.ops.render.render(write_still=True)
    round_off(sc.render.filepath)
    return {"w": R.ICON_PX, "h": R.ICON_PX, "pivotX": 0.5, "pivotY": 0.5}


def round_off(path, foot=0.10, corner=0.20):
    """Clears the bottom tenth of a portrait and rounds the two corners of what is left, so the bust ends like a medallion's and the
    People button's word, which sits over the picture's lowest part, stays clear of it."""
    img = bpy.data.images.load(path)
    w, h = img.size
    px = list(img.pixels)   # rows from the bottom up
    r, base = corner * w, foot * h
    xs = [px[(y * w + x) * 4 + 3] for y in range(int(base), int(base + r)) for x in range(w)]
    cols = [x % w for x, a in enumerate(xs) if a > 0.5]
    left, right = (min(cols), max(cols)) if cols else (0, w - 1)
    for y in range(h):
        for x in range(w):
            dy = (y + 0.5) - base
            keep = 0.0 if dy < 0 else 1.0
            if 0 <= dy < r:
                dx = max(left + r - (x + 0.5), (x + 0.5) - (right - r), 0.0)
                if dx > 0:
                    keep = max(0.0, min(1.0, r - math.hypot(dx, r - dy) + 0.5))
            if keep < 1.0:
                px[(y * w + x) * 4 + 3] *= keep
    img.pixels = px
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def install(render, samples):
    """Returns ({sprite name: builder} for the map's pictures, {portrait name: kind})."""
    global R, S
    R, S = render, samples
    sprites = {}
    for kind in MAP_KINDS:
        for facing in ("front", "back"):
            for pose in ("stand", "walk0", "walk1", "walk2", "walk3") + (("work",) if kind in WORK_KINDS else ()):
                name = "person_%s_%s" % (kind, facing) + ("" if pose == "stand" else "_" + pose)
                sprites[name] = lambda k=kind, p=pose, f=facing: figure(k, p, f)
    return sprites, {"portrait_" + k: k for k in PORTRAITS}
