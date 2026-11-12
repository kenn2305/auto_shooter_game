import json
import sys
from collections import deque

from pngio import read_png


def components(path, dilate=3, min_px=20):
    w, h, px = read_png(path)
    mask = bytearray(w * h)
    for i in range(w * h):
        if px[i * 4 + 3] > 0:
            mask[i] = 1
    # dilate (box) to merge nearby fragments
    if dilate > 0:
        rowd = bytearray(w * h)
        for y in range(h):
            o = y * w
            run = -1
            for x in range(w):
                if mask[o + x]:
                    for k in range(max(0, x - dilate), min(w, x + dilate + 1)):
                        rowd[o + k] = 1
        dm = bytearray(w * h)
        for x in range(w):
            for y in range(h):
                if rowd[y * w + x]:
                    for k in range(max(0, y - dilate), min(h, y + dilate + 1)):
                        dm[k * w + x] = 1
    else:
        dm = mask
    label = [0] * (w * h)
    comps = []
    cur = 0
    for start in range(w * h):
        if dm[start] and not label[start]:
            cur += 1
            q = deque([start])
            label[start] = cur
            x0 = y0 = 10 ** 9
            x1 = y1 = -1
            count = 0
            while q:
                i = q.popleft()
                x, y = i % w, i // w
                if mask[i]:
                    count += 1
                    x0 = min(x0, x); x1 = max(x1, x); y0 = min(y0, y); y1 = max(y1, y)
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= nx < w and 0 <= ny < h:
                        j = ny * w + nx
                        if dm[j] and not label[j]:
                            label[j] = cur
                            q.append(j)
            if count >= min_px:
                comps.append({"x": x0, "y": y0, "w": x1 - x0 + 1, "h": y1 - y0 + 1, "px": count})
    comps.sort(key=lambda c: (c["y"] // 40, c["x"]))
    return w, h, comps


if __name__ == "__main__":
    path = sys.argv[1]
    d = int(sys.argv[2]) if len(sys.argv) > 2 else 3
    w, h, comps = components(path, d)
    print(json.dumps({"w": w, "h": h, "count": len(comps), "comps": comps}))
