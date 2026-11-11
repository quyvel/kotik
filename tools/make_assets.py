"""Sounds (a soft purr and a short meow) and the exe icon for Kotik. Run: python tools/make_assets.py (numpy + Pillow)."""
import math
import os
import wave

import numpy as np
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "build")
SR = 22050
rng = np.random.default_rng(7)


def save(name, x, peak):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype("<i2").tobytes())


def lp(x, cutoff):
    a = 1.0 - math.exp(-2 * math.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += a * (x[i] - acc)
        y[i] = acc
    return y


def bandpass(x, lo, hi):
    return lp(x, hi) - lp(x, lo)


def purr():
    """a soft purr: a low hum with a gentle ~24 Hz flutter, a slightly different tone on the in-breath and out-breath"""
    sec = 2.4
    t = np.arange(int(SR * sec)) / SR
    breath_in = (np.sin(2 * math.pi * t / 1.2) > 0).astype(float)          # 0.6 s in, 0.6 s out
    smooth = lp(breath_in, 6)
    flutter = 0.65 + 0.35 * np.sin(2 * math.pi * (23 + 3 * smooth) * t)    # not a hard on/off: no engine sound
    body = bandpass(rng.standard_normal(len(t)), 70, 220) * 3
    tone = np.sin(2 * math.pi * (48 + 6 * smooth) * t) * 0.35              # the chest hum
    x = (body + tone) * flutter * (0.55 + 0.45 * smooth)
    return lp(lp(lp(x, 320), 320), 320) * np.minimum(1, t / 0.3) * np.minimum(1, (sec - t) / 0.5)


def meow():
    sec = 0.62
    t = np.arange(int(SR * sec)) / SR
    f0 = 520 + 260 * np.sin(np.pi * t / sec) - 120 * t / sec           # rises then falls: "mi-aaa-u"
    ph = 2 * np.pi * np.cumsum(f0) / SR
    src = sum(np.sin(k * ph) / k ** 0.9 for k in range(1, 12))
    out = np.zeros_like(src)
    for fc, bw, g in [(900, 160, 1.0), (1600 + 600 * np.sin(np.pi * t / sec).mean(), 200, 0.6), (3000, 300, 0.25)]:
        r = math.exp(-math.pi * bw / SR)
        th = 2 * math.pi * fc / SR
        a1, a2 = -2 * r * math.cos(th), r * r
        y1 = y2 = 0.0
        y = np.empty_like(src)
        for i in range(len(src)):
            v = src[i] - a1 * y1 - a2 * y2
            y2, y1 = y1, v
            y[i] = v
        out += y * g
    env = np.minimum(1, t / 0.04) * np.minimum(1, (sec - t) / 0.18)
    return out * env


def icon():
    s = 256
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    orange, line = (236, 146, 64, 255), (120, 62, 26, 255)
    d.polygon([(48, 110), (70, 20), (120, 80)], fill=orange, outline=line)
    d.polygon([(208, 110), (186, 20), (136, 80)], fill=orange, outline=line)
    d.polygon([(64, 96), (74, 44), (106, 82)], fill=(240, 160, 170, 255))
    d.polygon([(192, 96), (182, 44), (150, 82)], fill=(240, 160, 170, 255))
    d.ellipse([28, 60, 228, 236], fill=orange, outline=line, width=6)
    d.ellipse([88, 150, 168, 220], fill=(252, 222, 184, 255))
    for x in (92, 164):
        d.ellipse([x - 18, 112, x + 18, 158], fill=(255, 255, 255, 255), outline=line, width=4)
        d.ellipse([x - 13, 118, x + 13, 154], fill=(120, 190, 80, 255))
        d.ellipse([x - 6, 120, x + 6, 152], fill=(20, 20, 24, 255))
        d.ellipse([x - 9, 120, x - 1, 128], fill=(255, 255, 255, 255))
    d.polygon([(118, 166), (138, 166), (128, 178)], fill=(235, 120, 140, 255))
    img.save(os.path.join(OUT, "app.ico"), sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    save("purr.wav", purr(), 0.16)
    save("meow.wav", meow(), 0.28)
    icon()
    print("ok")
