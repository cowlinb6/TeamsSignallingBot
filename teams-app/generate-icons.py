#!/usr/bin/env python3
"""Generate placeholder Teams app icons with the standard library only.

Teams requires two PNGs in the app package:
  * color.png   - 192x192, full-colour
  * outline.png -  32x32, transparent background, single-colour (white) glyph

These are PLACEHOLDERS. Replace them with real branding before submitting to
the Teams Store (the Store reviews icon quality).
"""
import struct
import zlib

ACCENT = (0x4F, 0x52, 0xB2, 255)  # matches manifest accentColor #4F52B2


def write_png(path, width, height, pixel):
    raw = bytearray()
    for y in range(height):
        raw.append(0)  # filter type 0 (None) for each scanline
        for x in range(width):
            raw += bytes(pixel(x, y, width, height))

    def chunk(typ, data):
        return (
            struct.pack(">I", len(data))
            + typ
            + data
            + struct.pack(">I", zlib.crc32(typ + data) & 0xFFFFFFFF)
        )

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)  # 8-bit RGBA
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", ihdr))
        f.write(chunk(b"IDAT", zlib.compress(bytes(raw), 9)))
        f.write(chunk(b"IEND", b""))


def ring(x, y, w, h, inner, outer, fg, bg):
    cx, cy = w / 2.0, h / 2.0
    d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
    return fg if inner < d < outer else bg


write_png("color.png", 192, 192,
          lambda x, y, w, h: ring(x, y, w, h, w * 0.26, w * 0.34, (255, 255, 255, 255), ACCENT))
write_png("outline.png", 32, 32,
          lambda x, y, w, h: ring(x, y, w, h, w * 0.28, w * 0.40, (255, 255, 255, 255), (0, 0, 0, 0)))

print("Wrote color.png (192x192) and outline.png (32x32)")
