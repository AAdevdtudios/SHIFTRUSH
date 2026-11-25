"""Comic treatment for an in-engine render: cel bands, ink lines, halftone shadows, lettering."""
import sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from scipy import ndimage

src, out, fonts = sys.argv[1], sys.argv[2], sys.argv[3]
im = Image.open(src).convert("RGB")
# Lift the night render so the cel bands have something to work with.
_a = np.asarray(im).astype(np.float32) / 255.0
im = Image.fromarray((np.clip(_a * 1.35, 0, 1) ** 0.8 * 255).astype(np.uint8))
W, H = im.size
a = np.asarray(im).astype(np.float32) / 255.0

# --- cel shading: quantise value into bands, keep hue, push saturation a little
hsv = np.asarray(im.convert("HSV")).astype(np.float32) / 255.0
v = hsv[..., 2]
v_s = ndimage.gaussian_filter(v, 1.2)
bands = np.array([0.12, 0.32, 0.56, 0.8, 0.97])
idx = np.digitize(v_s, [0.1, 0.26, 0.47, 0.7])
idx = ndimage.median_filter(idx, size=7)  # no speckle in the fog
vq = bands[idx]
hsv[..., 2] = vq
hsv[..., 1] = np.clip(hsv[..., 1] * 1.25, 0, 1)
cel = Image.fromarray((hsv * 255).astype(np.uint8), "HSV").convert("RGB")
c = np.asarray(cel).astype(np.float32) / 255.0

# --- ink: edges of the smoothed luminance plus edges between colour bands
lum = ndimage.gaussian_filter(a.mean(axis=2), 1.6)
gx = ndimage.sobel(lum, 1); gy = ndimage.sobel(lum, 0)
mag = np.hypot(gx, gy)
ink = mag > 0.3
band_edge = ndimage.sobel(idx.astype(np.float32), 1) ** 2 + ndimage.sobel(idx.astype(np.float32), 0) ** 2
ink |= (band_edge > 1.5) & (mag > 0.12)
ink = ndimage.binary_dilation(ink, iterations=1)
# Drop tiny specks: only lines that run a while.
lab, nlab = ndimage.label(ink)
sizes = ndimage.sum(ink, lab, range(1, nlab + 1))
keep = np.zeros(nlab + 1, bool); keep[1:] = sizes > 60
ink = keep[lab]

# --- halftone: dots in the shadows, on a 45 degree grid, sized by darkness
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
step = 9.0
u = (xx + yy) / math.sqrt(2); w = (xx - yy) / math.sqrt(2)
du = (u % step) - step / 2; dw = (w % step) - step / 2
dist = np.sqrt(du * du + dw * dw)
dark = np.clip((0.5 - vq) / 0.45, 0, 1)
dot = dist < (dark * step * 0.46)
shadow_tint = np.array([0.04, 0.02, 0.12])
c[dot & (vq < 0.5)] = c[dot & (vq < 0.5)] * 0.35 + shadow_tint * 0.65

# --- ink over everything
c[ink] = np.array([0.03, 0.02, 0.06])

# --- paper grain and a vignette
rng = np.random.default_rng(4)
grain = rng.normal(0, 0.025, (H, W, 1))
r = np.sqrt(((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2)
vig = np.clip(1.1 - r * 0.35, 0.6, 1)[..., None]
c = np.clip((c + grain) * vig, 0, 1)
panel = Image.fromarray((c * 255).astype(np.uint8))

# --- lettering
draw = ImageDraw.Draw(panel)
def font(name, size): return ImageFont.truetype(fonts + name, size)
black = font("BigShouldersDisplay-Black.ttf", 104)
bold = font("BigShouldersDisplay-Bold.ttf", 46)
mono = font("JetBrainsMono-Bold.ttf", 24)
INK = (8, 5, 16); CYAN = (46, 242, 255); MAGENTA = (255, 46, 154); PAPER = (250, 247, 240)

# Sound effect across the rotors, tilted.
sfx = Image.new("RGBA", (760, 200), (0, 0, 0, 0))
sd = ImageDraw.Draw(sfx)
sd.text((20, 20), "WHUP WHUP", font=black, fill=CYAN, stroke_width=9, stroke_fill=INK)
sfx = sfx.rotate(8, expand=True, resample=Image.BICUBIC)
panel.paste(sfx, (1150, 70), sfx)

# Caption box, top left: the scene.
cap_lines = ["NIGHT ONE.", "THE AIRSTRIP OUTSIDE THE CITY."]
x0, y0 = 46, 44
cw = 610; ch = 112
draw.rectangle([x0 + 8, y0 + 8, x0 + cw + 8, y0 + ch + 8], fill=MAGENTA)
draw.rectangle([x0, y0, x0 + cw, y0 + ch], fill=PAPER, outline=INK, width=5)
draw.text((x0 + 22, y0 + 14), cap_lines[0], font=bold, fill=INK)
draw.text((x0 + 22, y0 + 68), cap_lines[1], font=mono, fill=INK)

# Radio bubble from Slim, off panel: jagged outline means it comes over the radio.
bx0, by0, bx1, by1 = 40, 210, 760, 520
pts = []
cx, cy = (bx0 + bx1) / 2, (by0 + by1) / 2
rx, ry = (bx1 - bx0) / 2, (by1 - by0) / 2
n = 44
for i in range(n):
    ang = 2 * math.pi * i / n
    k = 1.0 if i % 2 == 0 else 0.9
    pts.append((cx + math.cos(ang) * rx * k, cy + math.sin(ang) * ry * k))
tail = [(bx0 + 120, by1 - 40), (bx0 + 10, by1 + 70), (bx0 + 190, by1 - 30)]
draw.polygon(tail, fill=PAPER, outline=INK)
draw.polygon(pts, fill=PAPER, outline=INK)
draw.line(pts + [pts[0]], fill=INK, width=6)
draw.line([tail[0], tail[1], tail[2]], fill=INK, width=6)
draw.polygon([(tail[0][0] + 6, tail[0][1] - 4), tail[1], (tail[2][0] - 6, tail[2][1] - 4)], fill=PAPER)
speaker = font("JetBrainsMono-Bold.ttf", 20)
draw.text((cx - 250, by0 + 66), "SLIM  //  CH13 RADIO", font=speaker, fill=MAGENTA)
line_font = font("BigShouldersDisplay-Bold.ttf", 40)
words = ["NOBODY EXPECTS ANYTHING.", "FINISH THE LAP AND THEY'LL", "REMEMBER THE NAME."]
for i, t in enumerate(words):
    draw.text((cx - 250, by0 + 98 + i * 44), t, font=line_font, fill=INK)

# Panel border.
draw.rectangle([0, 0, W - 1, H - 1], outline=INK, width=14)
panel.save(out)
print("saved", out, panel.size)
