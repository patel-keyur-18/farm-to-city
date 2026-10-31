"""Synthesizes the weather sounds (US-222, ADR 0083 §14): a soft rain loop and distant thunder.

Our own work, made from noise by this script, dedicated to the public domain (CC0 1.0). Standard library only, seeded, so
re-running it gives the same files:

    python3 tools/audio/weather_sounds.py unity/FarmToCity/Assets/Resources/Sounds
"""
import math
import random
import struct
import sys
import wave

RATE = 22050


def write(path, samples):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(32767 * 0.8 * s / peak)) for s in samples))


def lowpass(xs, cutoff):
    a = 1 - math.exp(-2 * math.pi * cutoff / RATE)
    y, out = 0.0, []
    for x in xs:
        y += a * (x - y)
        out.append(y)
    return out


def highpass(xs, cutoff):
    low = lowpass(xs, cutoff)
    return [x - l for x, l in zip(xs, low)]


def rain(seconds=8.0, fade=1.0):
    """Steady rain on leaves and roofs: band-passed noise with a soft patter of drops; the ends cross-fade, so it loops seamlessly."""
    r = random.Random(222)
    n = int((seconds + fade) * RATE)
    hiss = highpass(lowpass([r.uniform(-1, 1) for _ in range(n)], 5200), 400)
    patter = [0.0] * n
    for _ in range(int((seconds + fade) * 140)):  # drops a second
        at, size = r.randrange(n), r.uniform(0.2, 1.0)
        for k in range(int(0.012 * RATE)):
            if at + k < n:
                patter[at + k] += size * math.exp(-k / (0.0025 * RATE)) * r.uniform(-1, 1)
    patter = highpass(patter, 900)
    mix = [0.55 * h + 0.45 * p for h, p in zip(hiss, patter)]
    loop, f = int(seconds * RATE), int(fade * RATE)
    out = mix[:loop]
    for i in range(f):  # the tail fades into the head
        k = i / f
        out[i] = out[i] * k + mix[loop + i] * (1 - k)
    return out


def thunder(seconds=6.0):
    """Distant thunder: low brown noise swelling over a third of a second, a couple of rolls, then a long fade."""
    r = random.Random(7)
    n = int(seconds * RATE)
    brown, b = [], 0.0
    for _ in range(n):
        b = 0.995 * b + r.uniform(-1, 1) * 0.1
        brown.append(b)
    low = lowpass(lowpass(brown, 180), 220)
    rolls = [(0.0, 1.0), (0.9, 0.7), (1.9, 0.45)]
    out = []
    for i, x in enumerate(low):
        t = i / RATE
        env = 0.0
        for start, gain in rolls:
            u = t - start
            if u > 0:
                env += gain * min(1.0, u / 0.35) * math.exp(-u / 1.4)
        out.append(x * env)
    return out


if __name__ == "__main__":
    folder = sys.argv[1] if len(sys.argv) > 1 else "."
    write(folder + "/weather_rain.wav", rain())
    write(folder + "/weather_thunder.wav", thunder())
