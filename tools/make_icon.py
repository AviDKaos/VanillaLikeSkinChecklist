"""Draw the Thunderstore icon (256x256): a journal card of fish, owned ones colored, missing ones as silhouettes.

Usage: python -I tools/make_icon.py thunderstore/icon.png   (needs Pillow)
"""
import math
import sys

from PIL import Image, ImageDraw, ImageFilter

S = 1024  # draw large, downscale for smooth edges


def gradient(size, top, bottom):
    img = Image.new("RGB", (size, size))
    px = img.load()
    for y in range(size):
        t = y / (size - 1)
        c = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3))
        for x in range(size):
            px[x, y] = c
    return img


def fish(draw, cx, cy, w, color, outline=None, stripes=None):
    """A simple side-view fish facing right, centred on (cx, cy), body width w."""
    h = w * 0.55
    tail = w * 0.38
    body = [cx - w / 2 + tail * 0.35, cy - h / 2, cx + w / 2, cy + h / 2]
    tail_poly = [
        (body[0] + w * 0.08, cy),
        (cx - w / 2 - tail * 0.25, cy - h * 0.48),
        (cx - w / 2 - tail * 0.25, cy + h * 0.48),
    ]
    draw.polygon(tail_poly, fill=color, outline=outline)
    draw.ellipse(body, fill=color, outline=outline, width=6 if outline else 0)
    if stripes:
        for i in range(3):
            x = body[0] + (body[2] - body[0]) * (0.32 + i * 0.16)
            draw.line([(x, cy - h * 0.36), (x, cy + h * 0.36)], fill=stripes, width=int(w * 0.06))
    # eye
    ex, ey, er = body[2] - w * 0.2, cy - h * 0.12, w * 0.055
    eye = (255, 255, 255) if color != (24, 26, 34) else (70, 74, 86)
    draw.ellipse([ex - er, ey - er, ex + er, ey + er], fill=eye)
    if color != (24, 26, 34):
        draw.ellipse([ex - er * 0.45, ey - er * 0.45, ex + er * 0.45, ey + er * 0.45], fill=(20, 20, 28))


def main(out):
    img = gradient(S, (38, 132, 176), (10, 40, 70)).convert("RGBA")

    # soft "thought bubble" glow behind the card
    glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse([90, 90, S - 90, S - 90], fill=(255, 255, 255, 60))
    img = Image.alpha_composite(img, glow.filter(ImageFilter.GaussianBlur(60)))

    d = ImageDraw.Draw(img)
    card = [110, 110, S - 110, S - 110]
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle([c + 18 for c in card], radius=80, fill=(0, 0, 0, 110))
    img = Image.alpha_composite(img, shadow.filter(ImageFilter.GaussianBlur(18)))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(card, radius=80, fill=(246, 239, 222), outline=(255, 255, 255), width=10)

    black = (24, 26, 34)
    # rarity-ish colors: legendary gold, rare blue, common white/grey, plus a couple of patterned ones
    cells = [
        ((242, 182, 50), None), (black, None), ((60, 141, 222), (235, 245, 255)),
        ((230, 233, 238), (160, 166, 178)), ((224, 76, 92), None), (black, None),
        (black, None), ((72, 190, 120), (40, 120, 70)), ((242, 182, 50), (255, 236, 160)),
    ]
    inner = 70
    gx0, gy0 = card[0] + inner, card[1] + inner
    cell = (card[2] - card[0] - 2 * inner) / 3
    pad = 14
    for i, (color, stripes) in enumerate(cells):
        r, c = divmod(i, 3)
        x0 = gx0 + c * cell + pad
        y0 = gy0 + r * cell + pad
        x1 = x0 + cell - 2 * pad
        y1 = y0 + cell - 2 * pad
        tile = (214, 222, 230) if color == black else (255, 255, 255)
        d.rounded_rectangle([x0, y0, x1, y1], radius=34, fill=tile)
        fish(d, (x0 + x1) / 2 + 6, (y0 + y1) / 2, (x1 - x0) * 0.74, color, stripes=stripes)

    img = img.resize((256, 256), Image.LANCZOS).convert("RGB")
    img.save(out, optimize=True)
    print("wrote", out, img.size)


if __name__ == "__main__":
    main(sys.argv[1])
