"""Rebuild native ICOs from the checked-in, user-approved alpha masters.
Build-only Pillow dependency; application never runs this script.
"""
from pathlib import Path
from PIL import Image
import io
import struct

ROOT = Path(__file__).resolve().parents[1] / "src/KomuDesk.Desktop/Assets/Tray"
SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)

def png_bytes(im):
    out = io.BytesIO()
    im.save(out, "PNG", optimize=True)
    return out.getvalue()

def make_frame(alpha, size, ink):
    # Resample alpha separately; do not introduce a contrasting white halo.
    a = alpha.resize((size, size), Image.Resampling.LANCZOS)
    im = Image.new("RGBA", (size, size), (ink, ink, ink, 0))
    im.putalpha(a)
    return im

def dib(im):
    n = im.width
    # A conventional 32-bit BGRA DIB with unassociated alpha and an AND mask.
    # Legacy Windows readers can still use the mask for transparent pixels.
    stride = ((n + 31) // 32) * 4
    pixels = bytearray()
    mask = bytearray(stride * n)
    for y in range(n - 1, -1, -1):
        for x in range(n):
            r, g, b, a = im.getpixel((x, y))
            pixels.extend((b, g, r, a))
            if a == 0:
                mask[(n - 1 - y) * stride + x // 8] |= 128 >> (x % 8)
    return struct.pack("<IiiHHIIiiII", 40, n, n*2, 1, 32, 0, len(pixels), 0, 0, 0, 0) + pixels + mask

def write_ico(path, alpha, ink):
    frames = []
    for n in SIZES:
        im = make_frame(alpha, n, ink)
        # Standard small-size DIB and compressed large-size PNG frames.
        frames.append(dib(im) if n <= 64 else png_bytes(im))
    offset = 6 + 16 * len(frames)
    out = bytearray(struct.pack("<HHH", 0, 1, len(frames)))
    for n, frame in zip(SIZES, frames):
        out.extend(struct.pack("<BBBBHHII", n % 256, n % 256, 0, 0, 1, 32, len(frame), offset))
        offset += len(frame)
    for frame in frames:
        out.extend(frame)
    path.write_bytes(out)
    with Image.open(path) as check:
        assert set(check.ico.sizes()) == {(n, n) for n in SIZES}
        for n in SIZES:
            a = check.ico.getimage((n, n)).convert("RGBA").getchannel("A")
            hist = a.histogram()
            assert hist[0] > 0 and hist[255] > 0 and sum(hist[1:255]) > 0, (path, n)

def main():
    for i in range(1, 11):
        stem = f"cat-{i:02}"
        with Image.open(ROOT / "Masters" / (stem + ".png")) as master:
            alpha = master.convert("RGBA").getchannel("A")
        assert alpha.size == (128, 128)
        make_frame(alpha, 64, 38).save(ROOT / (stem + ".png"), optimize=True)
        write_ico(ROOT / (stem + ".ico"), alpha, 38)
        write_ico(ROOT / (stem + "-light.ico"), alpha, 245)
        print(stem, "nine sizes, both contrast variants")

if __name__ == "__main__":
    main()
