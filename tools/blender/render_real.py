"""
Spec 032 (the realistic valley): the game's sprites rendered realistic at 8x, with each building's shadow as its own sprite.

US-283 (the device check) renders three subjects: the family house (4 facings, with shadows), the wheat acre (4 stages) and the
farm hand and his wife (front and back, standing). Later stories add their categories to SPRITES below.

Run headless from the repository root:
    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup -P tools/blender/render_real.py -- [names]
With no names every sprite here is rendered. Output: unity/FarmToCity/Assets/Resources/Art/Blender/<name>.png (and <name>_shadow.png)
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
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location("samples", os.path.join(HERE, "samples.py"))
S = importlib.util.module_from_spec(_spec)
sys.modules["samples"] = S
_spec.loader.exec_module(S)
R = S.R

OUT = os.environ.get("FTC_ART_OUT") or os.path.join(HERE, "..", "..", "unity", "FarmToCity", "Assets", "Resources", "Art", "Blender")
R.OUT_DIR = OUT
S.OUT = OUT
PPU = 132 * R.SCALE
SUN_ELEVATION = 60.0     # degrees: a shadow is 0.58 x the height (spec 032 §3.1); azimuth as the game's other art
PEOPLE_HEADS = 6         # spec 032 §3.1


# ---------------------------------------------------------------- the scene

def start():
    """The realistic scene (Cycles, AgX, soft sun and sky) with the sun at 60 degrees."""
    sc, cam = S.start("real")
    sun = next(o for o in bpy.data.objects if o.type == "LIGHT")
    horizontal = math.hypot(-0.35, -1.0)
    to_light = Vector((-0.35, -1.0, horizontal * math.tan(math.radians(SUN_ELEVATION)))).normalized()
    sun.rotation_euler = to_light.to_track_quat("Z", "Y").to_euler()
    return sc, cam


def world_top(o):
    return max((o.matrix_world @ Vector(c)).z for c in o.bound_box)


def shadow_catcher(objs, pad):
    """A ground at z = 0 that shows only the shadows, big enough for the longest shadow (to the lower right)."""
    return S.catcher(objs, pad=pad)


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


def wheat(stage):
    """A wheat acre at a stage, as the game's crop sprite (the crop alone: the ground tile is drawn under it)."""
    toon = R.toon
    rnd = random.Random(2711 + sum(ord(c) for c in stage))
    b = R.Batch("wheat")
    young, ripe = 0x6FB03E, 0xD2A23C
    R.crop_mat(b, stage, toon("wheatCanopyG", young), toon("wheatCanopy", ripe), width=0.07)
    mats = {
        "ripe": ([toon("wheatStalkA", 0xD9B25A), toon("wheatStalkB", 0xE6C266), toon("wheatStalkC", 0xC99A3E)],
                 [toon("wheatEarA", 0xE0A12E), toon("wheatEarB", 0xF0BE48), toon("wheatEarC", 0xCC8A22)],
                 [toon("wheatLeafA", 0xBFA35C), toon("wheatLeafB", 0xA88E48)], toon("wheatAwn", 0xE2B455)),
        "green": [toon("wheatGreenA", 0x7CBF45), toon("wheatGreenB", 0x5FA238)],
        "deep": [toon("wheatDeepA", 0x5FA238), toon("wheatDeepB", 0x4C8C2E)],
        "ear_green": toon("wheatEarGreen", 0xA8CF5A),
    }
    per_row = 17 if stage in ("g3", "ready") else 14
    for x in R.RIDGE_X:
        for j in range(per_row):
            y = -0.43 + j * (0.86 / (per_row - 1)) + rnd.uniform(-0.01, 0.01)
            clump(b, rnd, x + rnd.uniform(-0.012, 0.012), y, stage, mats)
    return b.objects()


def foot_pad(depth=0.0197):
    """An invisible point just under the feet: today's figure has an outline below them, so its frame reaches a hair lower than a realistic one's.
    The point only widens the frame (a mesh of one vertex, never drawn), so the pivot keeps today's distance from the frame's bottom (spec 032 AC-1)."""
    me = bpy.data.meshes.new("footPad")
    me.from_pydata([(0.0, 0.0, -depth)], [], [])
    o = bpy.data.objects.new("footPad", me)
    bpy.context.scene.collection.objects.link(o)
    o.visible_camera = False
    return o


def person(kind, facing):
    """The farm hand or his wife, 6 heads tall at today's height, standing; 'back' turns away as the game's walking-away frames do."""
    objs = S.person_real(kind, heads=PEOPLE_HEADS)
    if facing == "back":
        R.refit(objs, math.degrees(math.radians(-115) - S.FRONT))
    return objs + [foot_pad()]


SPRITES = {}
for _f in R.FACINGS:
    SPRITES["family_house_" + _f] = (lambda f=_f: house(f), True)
for _st in R.STAGES:
    SPRITES["crop_wheat_" + _st] = (lambda st=_st: wheat(st), False)
for _k in ("hand_m", "hand_f"):
    for _f in ("front", "back"):
        SPRITES["person_%s_%s" % (_k, _f)] = (lambda k=_k, f=_f: person(k, f), False)


# ---------------------------------------------------------------- the shadow pass

def crop_shadow(path, info):
    """Alpha under 6% is dropped, the colour made black (the game tints it and sets its alpha), and the frame cut to the shadow with its own pivot."""
    img = bpy.data.images.load(path)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    px[px[:, :, 3] < 0.06, 3] = 0.0
    px[:, :, :3] = 0.0
    ys, xs = np.nonzero(px[:, :, 3] > 0)
    m = 2 * R.SCALE
    x0, x1, y0, y1 = max(0, xs.min() - m), min(w, xs.max() + 1 + m), max(0, ys.min() - m), min(h, ys.max() + 1 + m)
    cw, ch = x1 - x0, y1 - y0
    cw += cw % 2
    ch += ch % 2
    out = bpy.data.images.new("shadow", cw, ch, alpha=True)
    canvas = np.zeros((ch, cw, 4), dtype=np.float32)
    canvas[:y1 - y0, :x1 - x0] = px[y0:y1, x0:x1]
    out.pixels = canvas.ravel().tolist()
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(img)
    bpy.data.images.remove(out)
    return {"w": int(cw), "h": int(ch), "pivotX": round(float((info["pivotX"] * w - x0) / cw), 5), "pivotY": round(float((info["pivotY"] * h - y0) / ch), 5)}


def render_shadow(sc, cam, name, objs):
    """The building hidden from the camera but still casting, over a shadow catcher: the shadow alone, in its own frame (spec 032 §3.3)."""
    casters = []
    for o in objs:
        o.visible_camera = False
        if world_top(o) < 0.012:    # flat ground (a yard's pad) casts nothing: the catcher lies under it
            o.visible_shadow = False
        else:
            casters.append(o)
    # Only the sun's shadow: the sky's light is occluded under every overhang and corner, which as a shadow sprite would be a smudge that no sun casts.
    bpy.context.scene.world.node_tree.nodes["Background"].inputs[1].default_value = 0.0
    tall = max(world_top(o) for o in casters)
    catcher = shadow_catcher(objs, pad=tall * 1.2)
    info = R.frame_and_render(sc, cam, name + "_shadow", [catcher])
    return crop_shadow(os.path.join(OUT, name + "_shadow.png"), info)


# ---------------------------------------------------------------- main

def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = argv or list(SPRITES)
    os.makedirs(OUT, exist_ok=True)
    manifest_path = os.path.join(OUT, "sprites.json")
    with open(manifest_path) as f:
        data = json.load(f)
    manifest = {s["name"]: s for s in data["sprites"]}
    for name in names:
        build, shadow = SPRITES[name]
        sc, cam = start()
        objs = S.finish(build())
        info = R.frame_and_render(sc, cam, name, objs)
        info.update({"name": name, "ppu": PPU})
        if shadow:
            info["shadow"] = True
        manifest[name] = info
        print("rendered", name, info)
        if shadow:
            sinfo = render_shadow(sc, cam, name, objs)
            sinfo.update({"name": name + "_shadow", "ppu": PPU})
            manifest[name + "_shadow"] = sinfo
            print("rendered", name + "_shadow", sinfo)
    data["sprites"] = [manifest[k] for k in sorted(manifest)]
    with open(manifest_path, "w") as f:
        json.dump(data, f, indent=1)
        f.write("\n")


if __name__ == "__main__":
    main()
