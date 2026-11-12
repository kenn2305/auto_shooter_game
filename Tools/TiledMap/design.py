"""Procedurally lay out Map_01 (128x128, 32 px tiles, wraps on both axes) and emit Tiled MCP calls."""
import json
import math
import random
import sys

TILED_DIR = sys.argv[1]
OUT_CALLS = sys.argv[2]
SEED = int(sys.argv[3]) if len(sys.argv) > 3 else 2026

random.seed(SEED)
N = 128
TS = 32
C = N / 2
GAP = 4.0          # min free tiles between two obstacle feet (corridor width)
EDGE = 1.0         # keep feet this far inside the map edge so nothing straddles the wrap seam
CLEAR_R = 10.0     # spawn clearing radius

cat = json.load(open(f"{TILED_DIR}/catalog.json"))["obstacles"]
by_name = {c["name"]: c for c in cat}


def foot(c):
    """Foot footprint (w, h) in tiles."""
    return max(c["colW"], 4) / TS, max(c["colH"], 4) / TS


placed = []   # (name, x, y) feet position in tiles (x right, y down), obstacles only
decals = []   # (name, x, y)


def wrap_d(a, b):
    d = abs(a - b) % N
    return min(d, N - d)


def fits(name, x, y, ignore_clearing=False):
    c = by_name[name]
    fw, fh = foot(c)
    if x - fw / 2 < EDGE or x + fw / 2 > N - EDGE or y - fh < EDGE or y > N - EDGE:
        return False
    if not ignore_clearing and math.hypot(x - C, y - C) < CLEAR_R + fw / 2:
        return False
    for (n2, x2, y2) in placed:
        fw2, fh2 = foot(by_name[n2])
        dx = wrap_d(x, x2) - (fw + fw2) / 2
        dy = wrap_d(y - fh / 2, y2 - fh2 / 2) - (fh + fh2) / 2
        if max(dx, dy) < GAP:
            return False
    return True


def place(name, x, y, ignore_clearing=False):
    if fits(name, x, y, ignore_clearing):
        placed.append((name, round(x * 2) / 2, round(y * 2) / 2))
        return True
    return False


def scatter(names, cx, cy, radius, count, tries=400):
    n = 0
    for _ in range(tries):
        if n >= count:
            break
        a = random.random() * math.tau
        r = radius * math.sqrt(random.random())
        if place(random.choice(names), cx + math.cos(a) * r, cy + math.sin(a) * r):
            n += 1
    return n


def trees(colors, kinds=("Tree",), sizes=range(1, 7)):
    out = []
    for color in colors:
        for k in kinds:
            rng = sizes if k == "Tree" else (range(1, 6) if k == "Bush" else range(1, 3))
            out += [f"{k}{i:02d}_{color}" for i in rng]
    return out


GREENS = ["Green", "DarkGreen", "Lime"]
AUTUMN = ["Autumn", "Yellow"]

# ── 1. Spawn clearing: stone circle under the player, portal ruins to the north ──
decals.append(("StoneCircle", C, C + 1.8))
place("PortalRuins", C, C - 9, ignore_clearing=True)
place("RunePillar", C - 8, C - 9.5, ignore_clearing=True)
place("RunePillar", C + 8, C - 9.5, ignore_clearing=True)

# ── 2. Ruins plaza (north-west) ──
RX, RY = 30, 30
decals += [("StonePath", RX, RY + 6), ("StonePathCurve", RX - 4, RY + 11), ("StoneDisc", RX, RY + 1)]
place("Gazebo", RX, RY - 3)
place("StatueLeft", RX - 6, RY + 2)
place("StatueRight", RX + 6, RY + 2)
place("Obelisk", RX - 11, RY - 4)
place("Obelisk", RX + 11, RY - 4)
for nm, dx, dy in [("BrokenPillar", -10, 7), ("StoneSlab", 10, 8), ("SmallPedestal", -5, -9), ("SmallPedestal", 5, -9),
                   ("Rock01", -14, 12), ("Rock02", 14, 13)]:
    place(nm, RX + dx, RY + dy)
scatter(trees(GREENS), RX, RY, 22, 10)

# ── 3. Rocky field (south-east) ──
SX, SY = 98, 96
decals += [("StoneDisc", SX, SY), ("StonePath", SX - 6, SY + 7)]
scatter(["Rock01", "Rock02", "BrokenPillar", "StoneSlab"], SX, SY, 12, 9)
scatter(trees(["Green", "Lime"], ("Bush", "Stump")), SX, SY, 18, 8)

# ── 4. Forest groves ──
scatter(trees(AUTUMN) + trees(AUTUMN, ("Bush",)), 100, 28, 17, 22)               # autumn grove (landmark)
scatter(trees(["DarkGreen"]) + trees(["DarkGreen"], ("Pine",)), 26, 100, 18, 24)  # dark pine forest
scatter(trees(["Green", "Lime"]), 110, 66, 13, 12)                               # east wood
scatter(trees(["Green", "DarkGreen"]), 14, 62, 12, 10)                           # west wood
scatter(trees(GREENS), 64, 112, 14, 12)                                          # south wood

# ── 5. Light scatter everywhere else ──
lone = trees(GREENS) + trees(GREENS, ("Bush", "Stump", "Pine")) + ["Rock01", "Rock02"]
for _ in range(2500):
    if len(placed) >= 230:
        break
    place(random.choice(lone), random.uniform(2, N - 2), random.uniform(2, N - 2))

# ── Ground: base grass + 2x2 patch stamps (outer border = base colour → seamless, wraps cleanly) ──
BASE = 0
STAMPS = [1, 102, 104, 193, 214, 216]
ground = [[BASE] * N for _ in range(N)]
used = [[False] * N for _ in range(N)]
stamps = 0
for _ in range(6000):
    if stamps >= 380:
        break
    x, y = random.randint(0, N - 2), random.randint(0, N - 2)
    if any(used[yy][xx] for yy in range(max(0, y - 1), min(N, y + 3)) for xx in range(max(0, x - 1), min(N, x + 3))):
        continue
    s = random.choice(STAMPS)
    for dy in range(2):
        for dx in range(2):
            ground[y + dy][x + dx] = s + dx + dy * 32
            used[y + dy][x + dx] = True
    stamps += 1

# ── Decor: grass tufts / flowers (single tiles, never touch the tile edge) ──
TUFTS = [r * 32 + c for r in range(16) for c in range(4, 10)]
FLOWERS = [r * 32 + c for r in range(16) for c in range(0, 4)]
decor = []
for y in range(N):
    for x in range(N):
        roll = random.random()
        near_center = math.hypot(x - C, y - C) < 16
        if roll < 0.045:
            decor.append((x, y, random.choice(TUFTS)))
        elif roll < (0.075 if near_center else 0.055):
            decor.append((x, y, random.choice(FLOWERS)))

# ── Emit MCP calls ──
calls = []
calls.append({"method": "create_map", "params": {"name": "Map_01.tmx", "width": N, "height": N, "tileWidth": TS, "tileHeight": TS,
                                                  "orientation": "orthogonal", "infinite": False}})
calls.append({"method": "rename_layer", "params": {"old_name": "Map_01", "new_name": "Ground"}})
for ts in ("Grass", "GrassDetail", "Obstacles"):
    calls.append({"method": "add_tileset", "params": {"name": ts, "file_path": f"{TILED_DIR}/{ts}.tsx"}})
calls.append({"method": "add_tile_layer", "params": {"name": "Decor", "above": "Ground"}})
calls.append({"method": "add_object_layer", "params": {"name": "Decals", "above": "Decor"}})
calls.append({"method": "add_object_layer", "params": {"name": "Obstacles", "above": "Decals"}})
calls.append({"method": "add_object_layer", "params": {"name": "Markers", "above": "Obstacles"}})

calls.append({"method": "fill_rect", "params": {"layer": "Ground", "x": 0, "y": 0, "width": N, "height": N, "tileset": "Grass", "tile_id": BASE}})
patch = [{"x": x, "y": y, "tileset": "Grass", "tile_id": ground[y][x]} for y in range(N) for x in range(N) if ground[y][x] != BASE]
calls.append({"method": "set_tiles_batch", "params": {"layer": "Ground", "tiles": patch}})
calls.append({"method": "set_tiles_batch", "params": {"layer": "Decor", "tiles": [{"x": x, "y": y, "tileset": "GrassDetail", "tile_id": t} for x, y, t in decor]}})


def obj_call(layer, name, x, y):
    c = by_name[name]
    return {"method": "add_object", "params": {"layer": layer, "tileset": "Obstacles", "tile_id": c["tileId"],
                                               "x": round(x * TS), "y": round(y * TS), "width": c["w"], "height": c["h"]}}


for name, x, y in decals:
    calls.append(obj_call("Decals", name, x, y))
# Sort by y so objects are drawn back-to-front in Tiled too.
for name, x, y in sorted(placed, key=lambda p: p[2]):
    calls.append(obj_call("Obstacles", name, x, y))
calls.append({"method": "add_object", "params": {"layer": "Markers", "name": "PlayerSpawn", "shape": "point", "x": C * TS, "y": C * TS}})

json.dump(calls, open(OUT_CALLS, "w"))

foot_area = sum(foot(by_name[n])[0] * foot(by_name[n])[1] for n, _, _ in placed)
print(f"obstacles={len(placed)} decals={len(decals)} stamps={stamps} decor={len(decor)} calls={len(calls)} "
      f"foot_cover={foot_area / (N * N) * 100:.2f}%")
from collections import Counter
print(Counter(n.split('_')[0].rstrip('0123456789') for n, _, _ in placed))
