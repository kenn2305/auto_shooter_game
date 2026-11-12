"""Lay out Map_02 "Ancient Ruins" (128x128, 32 px tiles, wraps on both axes) and emit Tiled MCP calls.

Usage: python design_map02.py <Map_02/Tiled folder> <calls.json> [seed]

Layers: Ground (grass), Decor (stone plazas, dirt patches), Water (lakes, blocking), Decals, Obstacles, Markers.
Lakes are rectangles: straight banks are the island edge pieces turned inside out, corners are the
generated Lake* tiles. Some lakes hold the pack's 5x5 island sample with a tree on it.
"""
import json
import math
import random
import sys

TILED_DIR = sys.argv[1]
OUT_CALLS = sys.argv[2]
SEED = int(sys.argv[3]) if len(sys.argv) > 3 else 1902
random.seed(SEED)

N = 128
TS = 32
C = N // 2
cat = json.load(open(f"{TILED_DIR}/catalog.json"))
props = {p["name"]: p for p in cat["props"]}
WATER = cat["water"]
TCOLS = cat["terrainColumns"]


def tid(c, r):
    return r * TCOLS + c


# ───────────── occupancy ─────────────
blocked = [[False] * N for _ in range(N)]   # water and plaza cells: no trees on them
water_cells = [[False] * N for _ in range(N)]
plaza_cells = [[False] * N for _ in range(N)]


def wrap_d(a, b):
    d = abs(a - b) % N
    return min(d, N - d)


# ───────────── lakes ─────────────
water_tiles = {}   # (x, y) -> water tile name
lakes = []


def free_rect(x0, y0, w, h, margin):
    for y in range(y0 - margin, y0 + h + margin):
        for x in range(x0 - margin, x0 + w + margin):
            if blocked[y % N][x % N]:
                return False
    return math.hypot(x0 + w / 2 - C, y0 + h / 2 - C) > 18 + max(w, h) / 2


def add_lake(x0, y0, w, h, island=False):
    """Water fills [x0, x0+w) x [y0, y0+h); the bank ring is the outermost cells."""
    for y in range(y0, y0 + h):
        for x in range(x0, x0 + w):
            top, bottom, left, right = y == y0, y == y0 + h - 1, x == x0, x == x0 + w - 1
            if top and left:
                name = "LakeTL"
            elif top and right:
                name = "LakeTR"
            elif bottom and left:
                name = "LakeBL"
            elif bottom and right:
                name = "LakeBR"
            elif top:
                name = "IslandBottom"   # land above, water below
            elif bottom:
                name = "IslandTop"      # water above, land below
            elif left:
                name = "IslandRight"    # land left, water right
            elif right:
                name = "IslandLeft"     # water left, land right
            else:
                name = "Water"
            water_tiles[(x, y)] = name
            blocked[y][x] = True
            water_cells[y][x] = True
    if island:
        ix, iy = x0 + (w - 5) // 2, y0 + (h - 5) // 2
        sample = [
            ["Water", "IslandTL1", "IslandTop", "IslandTR1", "Water"],
            ["IslandTL2", "IslandTL3", None, "IslandTR3", "IslandTR2"],
            ["IslandLeft", None, None, None, "IslandRight"],
            ["IslandBL2", "IslandBL3", None, "IslandBR3", "IslandBR2"],
            ["Water", "IslandBL1", "IslandBottom", "IslandBR1", "Water"],
        ]
        for dy in range(5):
            for dx in range(5):
                name = sample[dy][dx]
                if name is None:
                    water_tiles.pop((ix + dx, iy + dy), None)
                else:
                    water_tiles[(ix + dx, iy + dy)] = name
        lakes.append((x0, y0, w, h, (ix + 2.5, iy + 3.4)))
    else:
        lakes.append((x0, y0, w, h, None))
    # keep a walkable bank around every lake
    for y in range(y0 - 2, y0 + h + 2):
        for x in range(x0 - 2, x0 + w + 2):
            blocked[y % N][x % N] = True


LAKES = [  # x, y, w, h, island
    (18, 14, 16, 11, True),
    (88, 20, 11, 8, False),
    (100, 70, 13, 13, True),
    (22, 84, 9, 7, False),
    (60, 98, 22, 7, False),
    (36, 52, 8, 6, False),
    (84, 46, 7, 9, False),
]
for x, y, w, h, isl in LAKES:
    assert free_rect(x, y, w, h, 3), (x, y)
    add_lake(x, y, w, h, isl)

# ───────────── stone plazas (9-slice of the pack's 5x5 plaza sample, cols 18-22 rows 8-12) ─────────────
decor = {}   # (x, y) -> tile id


def add_plaza(x0, y0, w, h):
    for r in range(h):
        rr = 8 if r == 0 else 12 if r == h - 1 else 9 + (r - 1) % 3
        for c in range(w):
            cc = 18 if c == 0 else 22 if c == w - 1 else 19 + (c - 1) % 3
            x, y = (x0 + c) % N, (y0 + r) % N
            decor[(x, y)] = tid(cc, rr)
            plaza_cells[y][x] = True


SPAWN_PLAZA = (C - 7, C - 8, 14, 13)
ALTAR_PLAZA = (55, 17, 14, 14)
TOWER_PLAZA = (32, 100, 12, 11)
FOUNTAIN_PLAZA = (105, 101, 11, 10)
PLAZAS = [SPAWN_PLAZA, ALTAR_PLAZA, TOWER_PLAZA, FOUNTAIN_PLAZA, (8, 44, 7, 9), (72, 73, 9, 8)]
for p in PLAZAS:
    add_plaza(*p)

# dirt / tall-grass patches: the 5x5 grass sample (cols 18-22, rows 0-4) blends into plain grass
for _ in range(400):
    if sum(1 for v in decor.values() if v == tid(20, 2)) >= 22:
        break
    x, y = random.randint(0, N - 6), random.randint(0, N - 6)
    if any(blocked[yy][xx] or plaza_cells[yy][xx] or (xx, yy) in decor
           for yy in range(y - 1, y + 6) for xx in range(x - 1, x + 6)):
        continue
    if math.hypot(x + 2.5 - C, y + 2.5 - C) < 12:
        continue
    for dy in range(5):
        for dx in range(5):
            decor[(x + dx, y + dy)] = tid(18 + dx, dy)

# ───────────── objects ─────────────
GAP = 3.0       # free tiles between two colliders (corridor width)
placed = []     # (name, fx, fy) feet in tiles, y down
plants = []
decals = []


def foot(name):
    p = props[name]
    return max(p["colW"], 4) / TS, max(p["colH"], 4) / TS


def on_water_or_plaza(x, y, r, allow_plaza=False):
    for yy in range(int(y - r - 1), int(y + r) + 1):
        for xx in range(int(x - r), int(x + r) + 1):
            if water_cells[yy % N][xx % N]:
                return True
            if not allow_plaza and plaza_cells[yy % N][xx % N]:
                return True
    return False


def fits(name, x, y, gap=GAP, allow_plaza=False, clear=10.0):
    fw, fh = foot(name)
    if x < 1.5 or x > N - 1.5 or y < 1.5 or y > N - 1.5:
        return False
    if math.hypot(x - C, y - C) < clear:
        return False
    if on_water_or_plaza(x, y, max(fw, 1.5), allow_plaza):
        return False
    for (n2, x2, y2) in placed:
        fw2, fh2 = foot(n2)
        dx = wrap_d(x, x2) - (fw + fw2) / 2
        dy = wrap_d(y - fh / 2, y2 - fh2 / 2) - (fh + fh2) / 2
        if max(dx, dy) < gap:
            return False
    return True


def place(name, x, y, **kw):
    x, y = round(x * 2) / 2, round(y * 2) / 2
    if fits(name, x, y, **kw):
        placed.append((name, x, y))
        return True
    return False


def scatter(names, cx, cy, radius, count, tries=600, **kw):
    n = 0
    for _ in range(tries):
        if n >= count:
            break
        a = random.random() * math.tau
        r = radius * math.sqrt(random.random())
        if place(random.choice(names), cx + math.cos(a) * r, cy + math.sin(a) * r, **kw):
            n += 1
    return n


def place_plant(name, x, y):
    if math.hypot(x - C, y - C) < 7 or on_water_or_plaza(x, y, 0.8, allow_plaza=True):
        return False
    for (_, x2, y2) in placed + plants:
        if wrap_d(x, x2) < 1.4 and wrap_d(y, y2) < 1.0:
            return False
    plants.append((name, round(x * 4) / 4, round(y * 4) / 4))
    return True


TREES = ["TreeAutumn01", "TreeAutumn02"]
SMALL_TREES = ["Sapling", "DeadTree"]
ROCKS = ["Rock01", "Rock02", "Rock03", "Rock04", "Rock05", "Rock06", "Rock07"]
SLABS = ["Slab01", "Slab02", "Slab03"]
BUSHES = ["BushTall", "BushRound", "BushRound", "SproutA", "SproutB"]

RUINS = ["PillarArches", "PillarSmall", "Pedestal", "BrokenWall", "BrokenWall", "BrickBlock", "ChaliceStone"]
ambient = []   # sparkles (no collider), drawn above everything


def landmark(name, x, y):
    """Hand-placed set piece: always placed (the layout keeps its area free)."""
    placed.append((name, x, y))


def sparkle_ring(cx, cy, r, n):
    for i in range(n):
        a = i / n * math.tau + random.uniform(-0.3, 0.3)
        ambient.append(("Sparkles", round((cx + math.cos(a) * r) * 4) / 4, round((cy + math.sin(a) * r * 0.7) * 4) / 4))


# ── landmarks ──
# Spawn courtyard: the animated shrine north of the player, pedestals and pillars framing it.
landmark("Shrine", C, C - 3)
landmark("Pedestal", C - 5, C - 4)
landmark("Pedestal", C + 5, C - 4)
landmark("ChaliceStone", C - 5, C - 2.5)
landmark("PillarArches", C - 6, C - 7)
landmark("PillarArches", C + 6, C - 7)
sparkle_ring(C, C - 4.5, 3.5, 4)
# Altar plaza (north): the big animated altar, spirit chalices beside its stairs, pillars at the corners.
landmark("Altar", 62, 28)
landmark("ChaliceSpirits", 58, 29.5)
landmark("ChaliceSpirits", 66, 29.5)
landmark("PillarSmall", 56.5, 20)
landmark("PillarSmall", 67.5, 20)
landmark("BrokenWall", 56, 25)
sparkle_ring(62, 25, 5, 6)
# Tower plaza (south-west) and fountain plaza (south-east).
landmark("BrickTower", 38, 108)
landmark("BrickBlock", 34.5, 103)
landmark("PillarSmall", 42.5, 102.5)
landmark("FountainStone", 110.5, 107)
landmark("Pedestal", 106.5, 103)
landmark("ChaliceSpirits", 106.5, 104.2)
sparkle_ring(110.5, 105.5, 3, 4)
# Fallen ruins field in the middle-east.
scatter(RUINS + ["Slab02", "Rock02"], 78, 60, 11, 14, gap=2.0)
scatter(RUINS, 12, 72, 8, 5, gap=2.0)

# ruins: rocks and slabs scattered on/around every plaza (stones may lie on the paving)
for (x0, y0, w, h) in PLAZAS:
    cx, cy = x0 + w / 2, y0 + h / 2
    for _ in range(2 if (x0, y0, w, h) == SPAWN_PLAZA else 4):
        a = random.random() * math.tau
        rx, ry = cx + math.cos(a) * (w / 2 + 0.5), cy + math.sin(a) * (h / 2 + 0.5)
        place(random.choice(SLABS + ROCKS), rx, ry, allow_plaza=True, clear=8.0)
    scatter(SLABS + ROCKS, cx, cy, max(w, h) * 0.9, 4, allow_plaza=True, gap=2.0, clear=8.0)

# trees on the islands (decor only, unreachable)
for (x0, y0, w, h, isl) in lakes:
    if isl:
        placed.append((random.choice(TREES), isl[0], isl[1]))

# autumn groves
GROVES = [(104, 34, 16, 20), (40, 30, 14, 14), (14, 112, 15, 16), (88, 116, 13, 12), (118, 88, 10, 9), (52, 78, 12, 12)]
for (gx, gy, r, n) in GROVES:
    scatter(TREES, gx, gy, r, n)
    scatter(SMALL_TREES, gx, gy, r * 1.1, n // 4, gap=2.0)

# rocky field
scatter(ROCKS + SLABS, 76, 60, 12, 6, gap=2.0)
scatter(ROCKS, 14, 70, 9, 4, gap=2.0)
# a few lone ruin pieces in the wild
for _ in range(400):
    if sum(1 for n, _, _ in placed if n in RUINS) >= 40:
        break
    place(random.choice(RUINS), random.uniform(3, N - 3), random.uniform(3, N - 3))

# lone trees / rocks everywhere else
lone = TREES * 3 + SMALL_TREES * 2 + ROCKS + SLABS
for _ in range(4000):
    if len(placed) >= 260:
        break
    place(random.choice(lone), random.uniform(2, N - 2), random.uniform(2, N - 2))

# bushes: no collider, dense near groves and lake banks
for (gx, gy, r, n) in GROVES:
    for _ in range(n * 2):
        a = random.random() * math.tau
        rr = r * 1.2 * math.sqrt(random.random())
        place_plant(random.choice(BUSHES), gx + math.cos(a) * rr, gy + math.sin(a) * rr)
for (x0, y0, w, h, _) in lakes:
    for _ in range(int((w + h) * 0.6)):
        side = random.randrange(4)
        if side == 0:
            x, y = random.uniform(x0, x0 + w), y0 - 1.2
        elif side == 1:
            x, y = random.uniform(x0, x0 + w), y0 + h + 0.8
        elif side == 2:
            x, y = x0 - 1.0, random.uniform(y0 + 1, y0 + h)
        else:
            x, y = x0 + w + 1.0, random.uniform(y0 + 1, y0 + h)
        place_plant(random.choice(BUSHES), x, y)
for _ in range(3000):
    if len(plants) >= 320:
        break
    place_plant(random.choice(BUSHES), random.uniform(1, N - 1), random.uniform(1, N - 1))

# gravel decals
for _ in range(500):
    if len(decals) >= 90:
        break
    x, y = random.uniform(1, N - 1), random.uniform(1, N - 1)
    if not on_water_or_plaza(x, y, 0.5, allow_plaza=True):
        decals.append((random.choice(["Gravel01", "Gravel02"]), round(x * 4) / 4, round(y * 4) / 4))

# ───────────── ground ─────────────
COMMON = [tid(c, r) for r in (5, 6) for c in range(18, 23)]
TUFTY = [tid(c, 7) for c in range(18, 23)]
ground = [[random.choice(COMMON) if random.random() < 0.85 else random.choice(TUFTY) for _ in range(N)] for _ in range(N)]

# ───────────── emit MCP calls ─────────────
calls = []
for name, value in (("wrap", "both"), ("theme", "Ancient Ruins")):
    calls.append({"method": "set_map_property", "params": {"name": name, "value": value}})
calls.append({"method": "set_property", "params": {"target": "layer:Water", "name": "blocking", "value": True}})


def tile_batches(layer, tileset, cells):
    cells = list(cells)
    for i in range(0, len(cells), 4000):
        calls.append({"method": "set_tiles_batch", "params": {"layer": layer, "tiles": [
            {"x": x, "y": y, "tileset": tileset, "tile_id": t} for (x, y, t) in cells[i:i + 4000]]}})


tile_batches("Ground", "Terrain", ((x, y, ground[y][x]) for y in range(N) for x in range(N)))
tile_batches("Decor", "Terrain", ((x, y, t) for (x, y), t in decor.items()))
tile_batches("Water", "Water", ((x % N, y % N, WATER[nm]) for (x, y), nm in water_tiles.items()))


def obj_call(layer, name, fx, fy):
    p = props[name]
    # Tiled anchors tile objects at the bottom-centre of their rect; the feet sit pivotX/pivotY inside it.
    ox = fx * TS - (p["pivotX"] - p["w"] / 2)
    oy = fy * TS + p["pivotY"]
    return {"method": "add_object", "params": {"layer": layer, "tileset": "Props", "tile_id": p["tileId"],
                                               "x": round(ox), "y": round(oy), "width": p["w"], "height": p["h"]}}


objs = [obj_call("Decals", n, x, y) for n, x, y in decals + ambient]
objs += [obj_call("Obstacles", n, x, y) for n, x, y in sorted(placed + plants, key=lambda p: p[2])]
objs.append({"method": "add_object", "params": {"layer": "Markers", "name": "PlayerSpawn", "shape": "point", "x": C * TS, "y": C * TS}})
calls += objs

json.dump(calls, open(OUT_CALLS, "w"))
from collections import Counter
print(f"lakes={len(lakes)} water_cells={len(water_tiles)} plazas={len(PLAZAS)} decor={len(decor)} "
      f"obstacles={len(placed)} plants={len(plants)} decals={len(decals)} calls={len(calls)}")
print(Counter(n for n, _, _ in placed))
