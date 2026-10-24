"""Original AKKK launcher art (no Blizzard assets): procedurally painted stone, metal and a dusk landscape.
   python3 make_akkk_art.py <emblem.png> <out folder>"""
import sys, numpy as np
from PIL import Image, ImageFilter, ImageDraw
rng = np.random.default_rng(7)
W, H = 1100, 690     # left: room for the open section (690x600); right: News, buttons; bottom bar

def noise(h, w, octaves=6, base=4, seed=0):
    r = np.random.default_rng(seed); out = np.zeros((h, w)); amp = 1; tot = 0
    for o in range(octaves):
        n = base * 2 ** o
        g = r.random((n + 1, int(n * w / h) + 2))
        im = Image.fromarray((g * 255).astype('uint8')).resize((w, h), Image.BICUBIC)
        out += np.asarray(im) / 255 * amp; tot += amp; amp *= 0.55
    return out / tot

def stone(h, w, tint, seed, cracks=True):
    n = noise(h, w, 7, 3, seed)
    a = np.dstack([n * t for t in tint])
    if cracks:
        c = noise(h, w, 5, 6, seed + 9)
        line = np.exp(-((c - 0.5) / 0.006) ** 2)            # thin dark veins
        a *= (1 - 0.35 * line)[:, :, None]
    return a

def bevel(a, d=4, light=1.5, dark=0.45):
    h, w = a.shape[:2]; a = a.copy()
    for i in range(d):
        f = light - (light - 1) * i / d
        a[i, i:w - i] *= f; a[i:h - i, i] *= f
        g = dark + (1 - dark) * i / d
        a[h - 1 - i, i:w - i] *= g; a[i:h - i, w - 1 - i] *= g
    return a

def to_img(a): return Image.fromarray(np.clip(a, 0, 255).astype('uint8'))

def horde_scene(img):
    """The survivors-genre picture: one hero in a lit clearing, ringed by an endless horde that fades into the dark."""
    cx, cy, R = 182, 320, 330                         # the scene sits left of the News panel
    im = to_img(img).convert('RGBA')
    L = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(L)
    yy, xx = np.mgrid[0:H, 0:W]
    # ground: dark grass/earth, a moonlit clearing around the hero (ellipse: seen from above at an angle)
    dist = np.hypot((xx - cx) / 1.0, (yy - cy) / 0.62)
    ground = stone(H, W, (34, 42, 26), 77, False)
    light = np.exp(-(dist / 120.0) ** 2)
    ground = ground * (0.35 + 1.4 * light)[:, :, None] + np.array([30, 26, 10])[None, None, :] * light[:, :, None]
    gmask = (yy > 150) & (xx < 360)
    soft = np.clip((yy - 150) / 60.0, 0, 1) * np.clip((360 - xx) / 40.0, 0, 1)
    base = np.asarray(im).astype(float)[:, :, :3]
    base = np.where(gmask[:, :, None], base * (1 - soft[:, :, None]) + ground * soft[:, :, None], base)
    im = to_img(base).convert('RGBA')
    d = ImageDraw.Draw(im)
    r = np.random.default_rng(5)
    # the horde: many silhouettes on rings, smaller and darker the farther out (back rows drawn first)
    foes = []
    for ring in range(9):
        rad = 72 + ring * 26
        n = int(rad * 0.55)
        for k in range(n):
            ang = 2 * np.pi * k / n + r.normal(0, 0.08) + ring * 0.3
            x = cx + np.cos(ang) * rad * (1 + r.normal(0, 0.05))
            y = cy + np.sin(ang) * rad * 0.62 * (1 + r.normal(0, 0.05))
            if x < 10 or x > 352 or y < 150 or y > 496: continue
            foes.append((y, x, ring, r.integers(0, 3)))
    foes.sort()
    for y, x, ring, kind in foes:
        sc = 1.0 - ring * 0.06
        fade = max(0.0, 1 - ring / 8.5)
        body = tuple(int(c * (0.25 + 0.75 * fade)) for c in [(70, 78, 60), (90, 70, 95), (60, 60, 70)][kind])
        h = 18 * sc; w = 8 * sc
        if kind == 2:                                # bats: wings
            d.polygon([(x - 9 * sc, y - h * 0.8), (x, y - h * 0.5), (x + 9 * sc, y - h * 0.8), (x + 4 * sc, y - h * 0.3), (x - 4 * sc, y - h * 0.3)], fill=body)
        else:                                        # ghouls and shades: body and head
            d.ellipse((x - w / 2, y - h, x + w / 2, y), fill=body)
            d.ellipse((x - w * 0.42, y - h - w * 0.7, x + w * 0.42, y - h + w * 0.1), fill=body)
            d.line((x - w / 2, y - h * 0.6, x - w * 0.95, y - h * 0.35), fill=body, width=max(1, int(2 * sc)))   # reaching arms
            d.line((x + w / 2, y - h * 0.6, x + w * 0.95, y - h * 0.35), fill=body, width=max(1, int(2 * sc)))
        eye = (255, int(60 + 40 * fade), 40) if kind != 1 else (120, 255, 120)
        ey = y - h - w * 0.3 if kind != 2 else y - h * 0.55
        if fade > 0.05 or r.random() < 0.5:
            d.point([(x - 1.5 * sc, ey), (x + 1.5 * sc, ey)], fill=eye)
    # experience gems scattered in the clearing
    for k in range(26):
        a = r.uniform(0, 2 * np.pi); rr = r.uniform(25, 90)
        gx, gy = cx + np.cos(a) * rr, cy + np.sin(a) * rr * 0.62
        col = [(90, 170, 255), (110, 255, 160), (255, 90, 120)][r.integers(0, 3)]
        d.polygon([(gx, gy - 4), (gx + 3, gy), (gx, gy + 4), (gx - 3, gy)], fill=col)
    # the hero: cloaked figure with a raised blade, an aura ring and orbiting fireballs
    glow = Image.new('RGBA', (W, H), (0, 0, 0, 0)); gd = ImageDraw.Draw(glow)
    gd.ellipse((cx - 62, cy - 40, cx + 62, cy + 40), outline=(255, 210, 120, 200), width=3)
    for k in range(5):
        a = 2 * np.pi * k / 5 + 0.4
        ox, oy = cx + np.cos(a) * 62, cy + np.sin(a) * 40 - 8
        gd.ellipse((ox - 7, oy - 7, ox + 7, oy + 7), fill=(255, 150, 40, 255))
        gd.ellipse((ox - 3, oy - 3, ox + 3, oy + 3), fill=(255, 245, 200, 255))
        gd.line((ox, oy, ox - np.sin(a) * 16, oy + np.cos(a) * 10), fill=(255, 110, 30, 170), width=4)   # trails
    halo = glow.filter(ImageFilter.GaussianBlur(6))
    im = Image.alpha_composite(im, halo); im = Image.alpha_composite(im, glow)
    d = ImageDraw.Draw(im)
    d.ellipse((cx - 13, cy - 3, cx + 13, cy + 5), fill=(10, 10, 8))                    # shadow
    d.polygon([(cx - 10, cy), (cx + 10, cy), (cx + 6, cy - 26), (cx - 6, cy - 26)], fill=(40, 60, 120))   # cloak
    d.polygon([(cx - 6, cy - 26), (cx + 6, cy - 26), (cx + 4, cy - 16), (cx - 4, cy - 16)], fill=(150, 130, 90))   # chest plate
    d.ellipse((cx - 5, cy - 36, cx + 5, cy - 25), fill=(210, 170, 130))                 # head
    d.chord((cx - 6, cy - 38, cx + 6, cy - 26), 180, 360, fill=(170, 140, 60))          # helm
    d.line((cx + 7, cy - 22, cx + 17, cy - 44), fill=(230, 235, 255), width=3)          # blade
    d.line((cx + 3, cy - 22, cx + 11, cy - 20), fill=(200, 160, 70), width=2)           # hilt
    # strikes landing on the front row
    for k in range(7):
        a = r.uniform(0, 2 * np.pi)
        sx, sy = cx + np.cos(a) * 74, cy + np.sin(a) * 46
        d.line((sx - 5, sy - 5, sx + 5, sy + 5), fill=(255, 255, 220), width=2)
        d.line((sx - 5, sy + 5, sx + 5, sy - 5), fill=(255, 220, 120), width=1)
    # vignette: the horde goes on into the dark
    dark = np.clip((np.hypot(xx - cx, (yy - cy) / 0.62) - 120) / 200.0, 0, 1)      # the horde fades into the night
    a = np.asarray(im.convert('RGB')).astype(float) * (1 - 0.8 * dark)[:, :, None]
    m = np.clip((yy - 140) / 70.0, 0, 1) * np.clip((362 - xx) / 50.0, 0, 1)         # soft edges toward sky and News
    return img * (1 - m[:, :, None]) + a * m[:, :, None]

def background(emblem):
    y = np.linspace(0, 1, H)[:, None]; x = np.linspace(0, 1, W)[None, :]
    # dusk sky: violet top to ember horizon
    sky = np.dstack([18 + 120 * y ** 2.2, 14 + 45 * y ** 2, 40 + 25 * y]) * np.ones((H, W, 1))
    sky += (noise(H, W, 5, 2, 3)[:, :, None] - 0.5) * 30
    st = rng.random((H, W)) > 0.9975
    sky[st & (y < 0.4).repeat(W, 1)] = [230, 225, 255]
    img = sky
    # three ridges of mountains
    for k, (base, amp, col, sd) in enumerate([(0.52, 0.18, (40, 30, 55), 11), (0.62, 0.14, (26, 22, 34), 12), (0.72, 0.10, (14, 12, 18), 13)]):
        prof = noise(60, W, 4, 2, sd)[30]
        top = (base - amp * (prof - 0.3) * 1.6) * H
        mask = (np.arange(H)[:, None] > top[None, :])
        tex = stone(H, W, (1, 1, 1), sd + 20, False)[:, :, 0][:, :, None]
        img = np.where(mask[:, :, None], np.array(col)[None, None, :] * (0.7 + 0.6 * tex), img)
    img = to_img(img).filter(ImageFilter.GaussianBlur(0.6))
    a = np.asarray(img).astype(float)
    # right side: darkened panel behind News / Launcher
    pan = np.zeros((H, W)); pan[100:565, 685:1092] = 1
    pan = np.asarray(Image.fromarray((pan * 255).astype('uint8')).filter(ImageFilter.GaussianBlur(14))) / 255
    a *= (1 - 0.6 * pan)[:, :, None]
    # header lines under "News" and "Launcher"
    for ly in (227, 469):
        a[ly, 693:1085] = [150, 120, 70]; a[ly + 1, 693:1085] = [20, 14, 8]
    # frame: carved stone border with metal studs
    frame = stone(H, W, (88, 80, 72), 41)
    m = np.zeros((H, W), bool); m[:58] = True; m[:, :8] = True; m[:, -8:] = True; m[578:] = True
    a = np.where(m[:, :, None], frame, a)
    for yy, hh in ((0, 58), (578, 112)):
        a[yy:yy + hh] = bevel(a[yy:yy + hh], 4, 1.35, 0.5)
    a[52:58] *= 0.55; a[578:583] = [120, 110, 90]; a[583:586] = [30, 26, 20]
    im = to_img(a); d = ImageDraw.Draw(im)
    d.line((8, 54, W - 8, 54), fill=(176, 140, 64), width=2)
    for sx in range(30, W, 92):                               # studs along the top bar
        for sy in (28,):
            d.ellipse((sx - 5, sy - 5, sx + 5, sy + 5), fill=(160, 150, 130), outline=(40, 34, 26))
            d.ellipse((sx - 3, sy - 4, sx + 1, sy), fill=(225, 215, 190))
    # PLAY box: dark recess with a gold trim
    d.rectangle((950, 596, 1092, 683), fill=(8, 7, 6), outline=(176, 140, 64), width=2)
    d.rectangle((954, 600, 1088, 679), outline=(70, 55, 28), width=1)
    # the game's emblem (Azeroth Survivors / Aethus) at the top
    e = Image.open(emblem).convert('RGBA'); e.thumbnail((150, 104))
    im.paste(e, (889 - e.width // 2 + 15, 97), e)           # above the News column, under the top buttons
    return im

def slab(w, h, tint, seed, state):
    s = stone(h, w, tint, seed)
    s = s * 0.75 + np.array(tint)[None, None, :] * 0.35
    s *= {'n': 1.0, 'h': 1.28, 'd': 0.72, 'o': 0.8}[state]
    if state == 'o': s = s.mean(2, keepdims=True).repeat(3, 2)
    s = bevel(s, 3, 1.6 if state != 'd' else 0.7, 0.5 if state != 'd' else 1.3)
    im = to_img(s).convert('RGBA'); d = ImageDraw.Draw(im)
    rim = (196, 160, 84) if state != 'o' else (110, 110, 110)
    d.rectangle((0, 0, w - 1, h - 1), outline=(18, 18, 22), width=1)
    d.rectangle((1, 1, w - 2, h - 2), outline=rim, width=1)
    for cx, cy in ((3, 3), (w - 4, 3), (3, h - 4), (w - 4, h - 4)):   # metal corner rivets
        d.ellipse((cx - 2, cy - 2, cx + 2, cy + 2), fill=rim)
    return im

def main():
    emblem, out = sys.argv[1], sys.argv[2]
    background(emblem).save(out + '/background.png')
    blue = (70, 92, 125); red = (130, 40, 32)
    for st, suf in (('n', ''), ('h', '_hover'), ('d', '_down')):
        slab(91, 27, blue, 51, st).save(out + '/btn%s.png' % suf)
        slab(159, 27, blue, 52, st).save(out + '/long%s.png' % suf)
    for st, suf in (('n', ''), ('h', '_hover'), ('d', '_down'), ('o', '_off')):
        slab(130, 74, red, 53, st).save(out + '/play%s.png' % suf)

main()
