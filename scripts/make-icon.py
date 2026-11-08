#!/usr/bin/env python3
"""Write icon.png (256x256, 8-bit RGB): a yellow ring with a faint glow on a dark background."""
import math
import struct
import zlib

SIZE = 256
BG = (0x1E, 0x1E, 0x24)
GLOW = (0xFF, 0xFF, 0x33)
RADIUS = 90.0
HALF_WIDTH = 7.0
GLOW_REACH = 45.0
GLOW_PEAK = 0.22


def smoothstep(edge0, edge1, x):
    t = min(1.0, max(0.0, (x - edge0) / (edge1 - edge0)))
    return t * t * (3 - 2 * t)


def pixel(x, y):
    d = math.hypot(x + 0.5 - SIZE / 2, y + 0.5 - SIZE / 2)
    off = abs(d - RADIUS)
    ring = 1.0 - smoothstep(HALF_WIDTH - 1.0, HALF_WIDTH + 1.0, off)
    glow = GLOW_PEAK * (1.0 - smoothstep(0.0, GLOW_REACH, max(0.0, off - HALF_WIDTH)))
    a = max(ring, glow)
    return tuple(round(BG[i] + (GLOW[i] - BG[i]) * a) for i in range(3))


def chunk(tag, data):
    body = tag + data
    return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)


def main():
    rows = bytearray()
    for y in range(SIZE):
        rows.append(0)
        for x in range(SIZE):
            rows.extend(pixel(x, y))
    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(rows), 9))
    png += chunk(b"IEND", b"")
    with open("icon.png", "wb") as f:
        f.write(png)


if __name__ == "__main__":
    main()
