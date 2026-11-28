"""
US-273 (spec 032): the realistic ground. `render.py` builds every ground sprite out of `ground_block(name, top_hex, edge_hex, depth, top_z)`;
`install(R)` swaps that function for `block` here, so roads, banks, commons, the village and the rest all get a realistic top, and the
sprites' size and anchor stay exactly today's (the same boxes, new materials).

The top of a block is one procedural material:
  * a **seamless** noise (4 noises blended so the pattern repeats with the acre's own size): a macro layer shared by every variant, so
    neighbouring acres meet without a seam, and mid and fine layers that differ by `VARIANT` (three variants of each big kind are
    rendered, picked per acre by the game);
  * two colours (lush and dry) mixed by the macro layer, brightness from the mid and fine layers, a fine bump;
  * its roughness for what it is made of.
The side is a soil cut with horizontal strata. Water is a flat sheet of ripples that drift one whole wavelength along the acre's x axis over
`WATER_FRAMES` frames, so the loop never jumps and neighbouring acres meet.
"""
import math

import bpy

TILE_DEPTH = 0.41
WATER_FRAMES = 12
VARIANT = 0        # 0, 1, 2: which mid and fine layers the top uses
FRAME = 0          # the water's frame, 0 to WATER_FRAMES - 1

# kind -> (lush colour, dry colour, mid amplitude, fine amplitude, bump strength, roughness)
KINDS = {
    "ridge": (0x8A6440, 0x9E7648, 0.20, 0.24, 0.55, 1.0),        # the raised, drier crest of a furrow
    "grass": (0x7BA83C, 0x9DB84F, 0.22, 0.16, 0.30, 0.95),
    "commons": (0x93B546, 0xB5BC5C, 0.22, 0.16, 0.30, 0.95),
    "bank": (0x86B046, 0xA5AE55, 0.24, 0.18, 0.35, 0.95),
    "village": (0x82A846, 0xA8AE56, 0.24, 0.18, 0.28, 0.97),
    "square": (0x82A846, 0xA8AE56, 0.24, 0.18, 0.28, 0.97),
    "cattleGround": (0x9AB058, 0xB69E5E, 0.24, 0.18, 0.30, 0.97),
    "scrub": (0xB4B36A, 0xC9B880, 0.28, 0.20, 0.40, 0.98),
    "road": (0x7BA83C, 0x9DB84F, 0.22, 0.16, 0.30, 0.95),
    "earth": (0xD0B788, 0xDDC598, 0.18, 0.20, 0.45, 1.0),
    "soil": (0x6B4A2E, 0x7A5736, 0.20, 0.24, 0.50, 1.0),
    "water": (0x2F7A92, 0x6BB4C8, 0.0, 0.0, 0.0, 0.06),
}
SIDE = 0xA77D53

# What the sun and AgX do to a flat surface: measured with a swatch of each target colour (render the swatch, compare, scale the albedo by channel,
# seven times), so a ground acre renders as the palette it stands for (today's grass 0x8AB549, earth 0xD6BC90, ...) and not washed out.
GAIN = {
    "grass": (0.387, 0.536, 0.178), "earth": (0.671, 0.577, 0.335), "soil": (0.405, 0.365, 0.196), "scrub": (0.497, 0.535, 0.263),
    "side": (0.44, 0.411, 0.324), "water": (0.175, 0.464, 0.514),
}
GROUP = {"ridge": "soil", "commons": "grass", "bank": "grass", "village": "grass", "square": "grass", "cattleGround": "grass", "road": "grass"}


def _albedo(hexv, kind):
    g = GAIN[GROUP.get(kind, kind)]
    c = _lin(hexv)
    return (min(1.0, c[0] * g[0]), min(1.0, c[1] * g[1]), min(1.0, c[2] * g[2]), 1.0)


def _lin(hexv):
    def c(v):
        v /= 255.0
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    return (c((hexv >> 16) & 255), c((hexv >> 8) & 255), c(hexv & 255), 1.0)


def _math(nt, op, a, b=None, clamp=False):
    n = nt.nodes.new("ShaderNodeMath")
    n.operation = op
    n.use_clamp = clamp
    for i, v in enumerate((a, b)):
        if v is None:
            continue
        if isinstance(v, (int, float)):
            n.inputs[i].default_value = float(v)
        else:
            nt.links.new(v, n.inputs[i])
    return n.outputs[0]


def _smooth(nt, t):
    """3t^2 - 2t^3."""
    t2 = _math(nt, "MULTIPLY", t, t)
    return _math(nt, "SUBTRACT", _math(nt, "MULTIPLY", t2, 3.0), _math(nt, "MULTIPLY", t2, _math(nt, "MULTIPLY", t, 2.0)))


def uv(nt):
    """The block's own 0 to 1 across (Generated coordinates), as two sockets."""
    coord = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(coord.outputs["Generated"], sep.inputs[0])
    return sep.outputs["X"], sep.outputs["Y"], sep.outputs["Z"]


def tile_noise(nt, u, v, scale, w, detail=3.0, rough=0.55, gain=1.8):
    """A noise that repeats exactly over u and v in 0 to 1: four lookups blended by smoothed weights (f(1, v) = f(0, v))."""
    su, sv = _smooth(nt, u), _smooth(nt, v)
    acc = None
    for i in (0, 1):
        for j in (0, 1):
            uu = _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", u, float(i)), scale)
            vv = _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", v, float(j)), scale)
            comb = nt.nodes.new("ShaderNodeCombineXYZ")
            nt.links.new(uu, comb.inputs["X"])
            nt.links.new(vv, comb.inputs["Y"])
            comb.inputs["Z"].default_value = w
            noise = nt.nodes.new("ShaderNodeTexNoise")
            noise.inputs["Scale"].default_value = 1.0
            noise.inputs["Detail"].default_value = detail
            noise.inputs["Roughness"].default_value = rough
            nt.links.new(comb.outputs[0], noise.inputs["Vector"])
            wu = su if i else _math(nt, "SUBTRACT", 1.0, su)
            wv = sv if j else _math(nt, "SUBTRACT", 1.0, sv)
            term = _math(nt, "MULTIPLY", noise.outputs["Fac"], _math(nt, "MULTIPLY", wu, wv))
            acc = term if acc is None else _math(nt, "ADD", acc, term)
    return _math(nt, "ADD", _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", acc, 0.5), gain), 0.5, clamp=True)


def _mix_rgb(nt, a, b, fac, blend="MIX"):
    m = nt.nodes.new("ShaderNodeMix")
    m.data_type = "RGBA"
    m.blend_type = blend
    if isinstance(fac, (int, float)):
        m.inputs[0].default_value = fac
    else:
        nt.links.new(fac, m.inputs[0])
    for idx, v in ((6, a), (7, b)):
        if isinstance(v, tuple):
            m.inputs[idx].default_value = v
        else:
            nt.links.new(v, m.inputs[idx])
    return m.outputs[2]


def _grey(nt, v):
    c = nt.nodes.new("ShaderNodeCombineColor")
    for ch in ("Red", "Green", "Blue"):
        nt.links.new(v, c.inputs[ch])
    return c.outputs["Color"]


_cache = {}


def reset():
    _cache.clear()


def top_material(kind, top_hex):
    key = ("top", kind, top_hex, VARIANT, FRAME)
    if key in _cache:
        return _cache[key]
    lush_hex, dry_hex, mid_amp, fine_amp, bump_s, rough = KINDS.get(kind, KINDS["grass"])
    m = bpy.data.materials.new("real_" + kind)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    bsdf.inputs["Roughness"].default_value = rough
    u, v, _ = uv(nt)
    if kind == "water":
        _water(nt, bsdf, u, v, lush_hex, dry_hex)
    else:
        lush, dry = _albedo(lush_hex, kind), _albedo(dry_hex, kind)
        # Today's palette stays the average colour: top_hex pulls the pair towards what the model asked for.
        macro = tile_noise(nt, u, v, 2.0, 0.31, detail=2.0)                       # shared by every variant: acres meet
        mid = tile_noise(nt, u, v, 9.0, 1.7 + 6.1 * VARIANT, detail=3.0, gain=2.6)
        fine = tile_noise(nt, u, v, 42.0, 3.9 + 5.3 * VARIANT, detail=2.0, rough=0.6, gain=3.2)
        grain = tile_noise(nt, u, v, 190.0, 6.1 + 4.7 * VARIANT, detail=1.0, rough=0.7, gain=6.0)     # the blades' flecks: light and dark specks a pixel or two wide
        base = _mix_rgb(nt, dry, lush, macro)
        bright = _math(nt, "ADD", 1.0, _math(nt, "ADD", _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", mid, 0.5), mid_amp * 2),
                                              _math(nt, "ADD", _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", fine, 0.5), fine_amp * 2),
                                                    _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", grain, 0.5), fine_amp * 2.2))))
        nt.links.new(_mix_rgb(nt, base, _grey(nt, bright), 1.0, "MULTIPLY"), bsdf.inputs["Base Color"])
        bump = nt.nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = bump_s
        bump.inputs["Distance"].default_value = 0.004
        nt.links.new(_math(nt, "ADD", _math(nt, "MULTIPLY", grain, 0.6), _math(nt, "MULTIPLY", fine, 0.4)), bump.inputs["Height"])
        nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    _cache[key] = m
    return m


WAVES = ((3, 1, 2, 1.0), (5, -2, 3, 0.7), (8, 3, 4, 0.45), (13, -5, 5, 0.3))   # (waves across u, across v, whole waves moved in one loop, weight)


def _water(nt, bsdf, u, v, deep_hex, light_hex):
    """Ripples drifting along x. Each wave is a sine of whole numbers of wavelengths across the acre, bent by a seamless noise; in one loop of
    WATER_FRAMES frames it moves a whole number of wavelengths, so the last frame leads into the first and neighbouring acres meet."""
    t = FRAME / float(WATER_FRAMES)
    warp = tile_noise(nt, u, v, 3.0, 5.5, detail=3.0)
    acc, total = None, 0.0
    for m, n, speed, weight in WAVES:
        phase = _math(nt, "ADD", _math(nt, "MULTIPLY", _math(nt, "ADD", _math(nt, "MULTIPLY", u, float(m)), _math(nt, "MULTIPLY", v, float(n))), math.tau),
                      _math(nt, "SUBTRACT", _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", warp, 0.5), 3.4), speed * t * math.tau))
        term = _math(nt, "MULTIPLY", _math(nt, "SINE", phase), weight)
        acc = term if acc is None else _math(nt, "ADD", acc, term)
        total += weight
    height = _math(nt, "ADD", _math(nt, "MULTIPLY", acc, 0.5 / total), 0.5)          # 0 to 1
    macro = tile_noise(nt, u, v, 2.0, 8.9, detail=2.0)
    shade = _math(nt, "MULTIPLY", _math(nt, "ADD", _math(nt, "MULTIPLY", macro, 0.6), _math(nt, "MULTIPLY", height, 0.4)), 0.8, clamp=True)
    base = _mix_rgb(nt, _albedo(deep_hex, "water"), _albedo(light_hex, "water"), shade)
    # Crests: where a wave's height is highest the water is paler, like light through a thin ridge of water.
    crest = _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", height, 0.58), 3.0, clamp=True)
    base = _mix_rgb(nt, base, _albedo(0xCFEAF2, "water"), _math(nt, "MULTIPLY", crest, 0.75))
    nt.links.new(base, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.07
    bsdf.inputs["IOR"].default_value = 1.33
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 1.0
    bump.inputs["Distance"].default_value = 0.02
    nt.links.new(height, bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])


def side_material():
    key = ("side",)
    if key in _cache:
        return _cache[key]
    m = bpy.data.materials.new("real_side")
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    bsdf.inputs["Roughness"].default_value = 1.0
    coord = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(coord.outputs["Generated"], sep.inputs[0])
    # Strata: bands across the depth (z), bent by a noise.
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 7.0
    noise.inputs["Detail"].default_value = 5.0
    noise.inputs["Roughness"].default_value = 0.6
    nt.links.new(coord.outputs["Generated"], noise.inputs["Vector"])
    bands = _math(nt, "SINE", _math(nt, "ADD", _math(nt, "MULTIPLY", sep.outputs["Z"], 38.0), _math(nt, "MULTIPLY", noise.outputs["Fac"], 4.0)))
    shade = _math(nt, "ADD", 1.0, _math(nt, "ADD", _math(nt, "MULTIPLY", bands, 0.07), _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", noise.outputs["Fac"], 0.5), 0.34)))
    nt.links.new(_mix_rgb(nt, _albedo(SIDE, "side"), _grey(nt, shade), 1.0, "MULTIPLY"), bsdf.inputs["Base Color"])
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.5
    bump.inputs["Distance"].default_value = 0.004
    nt.links.new(noise.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    _cache[key] = m
    return m


def block(R, name, top_hex, edge_hex, depth=TILE_DEPTH, top_z=0.0):
    """render.py's `ground_block`, realistic: the same three boxes (so the sprite's size and anchor are unchanged) with a soil cut for the side and a
    seamless terrain for the top and its lip."""
    s = 1.0 + R.SEAM
    top = top_material(name, top_hex)
    return [R.box(name + "body", (s, s, depth - 0.01), (0, 0, top_z - 0.01 - (depth - 0.01) / 2), side_material(), outline=False),
            R.box(name + "lip", (s, s, 0.01), (0, 0, top_z - 0.005), top, outline=False),
            R.box(name + "top", (s - 0.035, s - 0.035, 0.002), (0, 0, top_z + 0.001), top, outline=False)]


def flat(R, original, kind):
    """The edge tile past the map: water is a flat sheet of the same ripples (the original's white ripple boxes are dropped)."""
    if kind == "water":
        s = 1.0 + R.SEAM
        return [R.box("flatwater", (s, s, 0.004), (0, 0, -0.002), top_material("water", 0), outline=False)]
    return original(kind)


def install(R):
    """Swap the ground builders in render.py for the realistic ones."""
    R.ground_block = lambda name, top_hex, edge_hex, depth=TILE_DEPTH, top_z=0.0: block(R, name, top_hex, edge_hex, depth, top_z)
    original = R.model_flat
    R.model_flat = lambda kind: flat(R, original, kind)


def patch_material(hexv, kind="earth"):
    """A bare patch on a lawn (a worn path, a dung pat, a saline patch, the square's earth): the palette's colour through the same sun and AgX as the ground, with a seamless speckle."""
    key = ("patch", hexv, kind)
    if key in _cache:
        return _cache[key]
    m = bpy.data.materials.new("real_patch")
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    bsdf.inputs["Roughness"].default_value = 1.0
    u, v, _ = uv(nt)
    mid = tile_noise(nt, u, v, 7.0, 2.3, detail=3.0, gain=2.4)
    fine = tile_noise(nt, u, v, 60.0, 5.1, detail=1.0, gain=5.0)
    bright = _math(nt, "ADD", 1.0, _math(nt, "ADD", _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", mid, 0.5), 0.4), _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", fine, 0.5), 0.3)))
    nt.links.new(_mix_rgb(nt, _albedo(hexv, kind), _grey(nt, bright), 1.0, "MULTIPLY"), bsdf.inputs["Base Color"])
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.4
    bump.inputs["Distance"].default_value = 0.004
    nt.links.new(fine, bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    _cache[key] = m
    return m
