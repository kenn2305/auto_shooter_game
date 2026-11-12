"""Minimal pure-python PNG reader/writer (RGBA8 / RGB8 / palette, non-interlaced)."""
import struct
import zlib


def read_png(path):
    data = open(path, "rb").read()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", "not png"
    pos = 8
    idat = b""
    palette = None
    trns = None
    while pos < len(data):
        length, ctype = struct.unpack(">I4s", data[pos:pos + 8])
        chunk = data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if ctype == b"IHDR":
            w, h, depth, color, _, _, interlace = struct.unpack(">IIBBBBB", chunk)
        elif ctype == b"PLTE":
            palette = [tuple(chunk[i:i + 3]) for i in range(0, len(chunk), 3)]
        elif ctype == b"tRNS":
            trns = chunk
        elif ctype == b"IDAT":
            idat += chunk
        elif ctype == b"IEND":
            break
    assert depth == 8 and interlace == 0, (depth, interlace)
    channels = {6: 4, 2: 3, 3: 1, 0: 1, 4: 2}[color]
    raw = zlib.decompress(idat)
    stride = w * channels
    out = bytearray(w * h * 4)
    prev = bytearray(stride)
    p = 0
    for y in range(h):
        f = raw[p]
        line = bytearray(raw[p + 1:p + 1 + stride])
        p += 1 + stride
        bpp = channels
        if f == 1:
            for i in range(bpp, stride):
                line[i] = (line[i] + line[i - bpp]) & 255
        elif f == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 255
        elif f == 3:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + ((a + prev[i]) >> 1)) & 255
        elif f == 4:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                b = prev[i]
                c = prev[i - bpp] if i >= bpp else 0
                pa = abs(b - c); pb = abs(a - c); pc = abs(a + b - 2 * c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 255
        prev = line
        o = y * w * 4
        if channels == 4:
            out[o:o + w * 4] = line
        elif channels == 3:
            for x in range(w):
                out[o + x * 4:o + x * 4 + 3] = line[x * 3:x * 3 + 3]
                out[o + x * 4 + 3] = 255
        elif color == 3:
            for x in range(w):
                idx = line[x]
                r, g, b = palette[idx]
                a = trns[idx] if trns and idx < len(trns) else 255
                out[o + x * 4:o + x * 4 + 4] = bytes((r, g, b, a))
        elif color == 0:
            for x in range(w):
                v = line[x]
                out[o + x * 4:o + x * 4 + 4] = bytes((v, v, v, 255))
        elif color == 4:
            for x in range(w):
                v = line[x * 2]
                out[o + x * 4:o + x * 4 + 4] = bytes((v, v, v, line[x * 2 + 1]))
    return w, h, out


def write_png(path, w, h, rgba):
    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        raw += rgba[y * w * 4:(y + 1) * w * 4]
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    open(path, "wb").write(png)
