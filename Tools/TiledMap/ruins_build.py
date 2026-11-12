"""Build Map_02 (Ancient Ruins pack) source assets + Tiled tilesets.

Usage: python ruins_build.py <pack Tilesets folder> <pack Props folder> <Map_02 folder in the Unity project>

Writes:
  Sprites/Terrain.png  copy of Tileset-Terrain2.png (grass + flat stone floor; the raised 3D platforms/stairs are never used)
  Sprites/Props.png    copy of Atlas-Props.png (trees, rocks, bushes; 3D pillars/walls/tower/fountain are never used)
  Sprites/Water.png    repacked animated water: one row per tile type, 7 frame columns, + 4 generated lake corners
  Tiled/Terrain.tsx, Tiled/Water.tsx (animated, blocking), Tiled/Props.tsx (image collection), Tiled/catalog.json
"""
import json
import os
import shutil
import sys
from xml.sax.saxutils import quoteattr

from pngio import read_png, write_png

TILESETS, PROPS, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
SPRITES = os.path.join(OUT, "Sprites")
TILED = os.path.join(OUT, "Tiled")
os.makedirs(SPRITES, exist_ok=True)
os.makedirs(TILED, exist_ok=True)

shutil.copyfile(os.path.join(TILESETS, "Tileset-Terrain2.png"), os.path.join(SPRITES, "Terrain.png"))
shutil.copyfile(os.path.join(PROPS, "Atlas-Props.png"), os.path.join(SPRITES, "Props.png"))


def repack(src_name, dst_name, fw, fh, cols):
    """Horizontal frame strip -> grid of `cols` columns (keeps sheets under Unity's 4096 px import size)."""
    w, h, d = read_png(os.path.join(PROPS, src_name))
    n = w // fw
    rows = (n + cols - 1) // cols
    W, H = min(n, cols) * fw, rows * fh
    out = bytearray(W * H * 4)
    for i in range(n):
        ox, oy = (i % cols) * fw, (i // cols) * fh
        for y in range(fh):
            s = (y * w + i * fw) * 4
            t = ((oy + y) * W + ox) * 4
            out[t:t + fw * 4] = d[s:s + fw * 4]
    write_png(os.path.join(SPRITES, dst_name), W, H, out)
    return n, cols, W, H


# Animated props (frame strips in the pack) -> grids. Frame i sits at column i % cols, row i // cols.
ANIM = {
    "Altar.png": ("altar 224x288 - standing on grass.png", 224, 288, 8),
    "Shrine.png": ("shrine or fountain 160x128-on grass.png", 160, 128, 8),
    "Chalice.png": ("golden chalice 64x64-spirits.png", 64, 64, 8),
    "Sparkles.png": ("atmospheric nature particles.png", 64, 64, 8),
}
anim_info = {dst: repack(src, dst, fw, fh, cols) + (fw, fh) for dst, (src, fw, fh, cols) in ANIM.items()}

# ───────────── Water: repack the animated sheet ─────────────
# Source layout (transparency version, 32 px grid): water strip row 1 (8 frames: frame 0 is the still
# frame used by the island sample, frames 1..7 loop). Each shore piece animates over 7 columns starting
# at column 5 (block A), 13 (block B) or 21 (block C); frame f of a shore piece matches water frame f+1.
FRAMES = 7
FRAME_MS = 140
src = read_png(os.path.join(TILESETS, "Tileset-Animated Terrains-8 frames- transparency.png"))


def tile(c, r):
    w, _, d = src
    return [[list(d[((r * 32 + y) * w + c * 32 + x) * 4:((r * 32 + y) * w + c * 32 + x) * 4 + 4]) for x in range(32)]
            for y in range(32)]


BLOCK = {"A": 5, "B": 13, "C": 21}


def shore(code):
    """'B3' -> 7 frames of block B row 3."""
    return [tile(BLOCK[code[0]] + f, int(code[1:])) for f in range(FRAMES)]


def is_water(p):
    return p[3] == 255 and p[2] > 150 and p[1] > 150 and p[0] < 150


def union_land(a, b):
    """Concave lake corner: land wherever either straight bank has land, rocks over water."""
    out = []
    for y in range(32):
        row = []
        for x in range(32):
            pa, pb = a[y][x], b[y][x]
            if pa[3] == 0 or pb[3] == 0:
                row.append([0, 0, 0, 0])
            elif is_water(pa):
                row.append(pb)
            else:
                row.append(pa)
        out.append(row)
    return out


# Island pieces are named after their cell in the pack's 5x5 island sample (land inside, water outside).
# Lake banks reuse them: a lake's top bank is an island's bottom edge, and so on.
ROWS = [("Water", [tile(1 + f, 1) for f in range(FRAMES)])]
ISLAND = {  # name: animated row, (cell in the 5x5 sample)
    "IslandTL1": "B4", "IslandTop": "B3", "IslandTR1": "B2",
    "IslandTL2": "B6", "IslandTL3": "B5", "IslandTR2": "A6",
    "IslandLeft": "B7", "IslandRight": "A5",
    "IslandBL2": "C2", "IslandBL3": "C3", "IslandBR3": "A3", "IslandBR2": "A4",
    "IslandBL1": "C4", "IslandBottom": "C5", "IslandBR1": "A2",
}
for name, code in ISLAND.items():
    ROWS.append((name, shore(code)))
# (3,4) of the sample has no animated row: a few rocks on land, mirrored partner of IslandTL3.
ROWS.append(("IslandTR3", [tile(3, 4)] * FRAMES))
corners = {"LakeTL": ("C5", "A5"), "LakeTR": ("C5", "B7"), "LakeBL": ("B3", "A5"), "LakeBR": ("B3", "B7")}
for name, (a, b) in corners.items():
    fa, fb = shore(a), shore(b)
    ROWS.append((name, [union_land(fa[f], fb[f]) for f in range(FRAMES)]))

W, H = FRAMES * 32, len(ROWS) * 32
img = bytearray(W * H * 4)
for r, (_, frames) in enumerate(ROWS):
    for f, t in enumerate(frames):
        for y in range(32):
            for x in range(32):
                i = ((r * 32 + y) * W + f * 32 + x) * 4
                img[i:i + 4] = bytes(t[y][x])
write_png(os.path.join(SPRITES, "Water.png"), W, H, img)


def opaque_fraction(t):
    return sum(1 for row in t for p in row if p[3] > 0) / 1024.0


water_ids = {}
lines = [f'<?xml version="1.0" encoding="UTF-8"?>',
         f'<tileset version="1.10" tiledversion="1.12.2" name="Water" tilewidth="32" tileheight="32" '
         f'tilecount="{FRAMES * len(ROWS)}" columns="{FRAMES}">',
         ' <properties>',
         '  <property name="blocking" type="bool" value="true"/>',
         ' </properties>',
         f' <image source="../Sprites/Water.png" width="{W}" height="{H}"/>']
for r, (name, frames) in enumerate(ROWS):
    tid = r * FRAMES
    water_ids[name] = tid
    lines.append(f' <tile id="{tid}" type="{name}">')
    lines.append('  <properties>')
    lines.append(f'   <property name="cover" type="float" value="{opaque_fraction(frames[0]):.3f}"/>')
    lines.append('  </properties>')
    if len({json.dumps(f) for f in frames}) > 1:
        lines.append('  <animation>')
        for f in range(FRAMES):
            lines.append(f'   <frame tileid="{tid + f}" duration="{FRAME_MS}"/>')
        lines.append('  </animation>')
    lines.append(' </tile>')
lines.append('</tileset>')
open(os.path.join(TILED, "Water.tsx"), "w", encoding="utf-8").write("\n".join(lines) + "\n")

# ───────────── Terrain (grid) ─────────────
tw, th, _ = read_png(os.path.join(SPRITES, "Terrain.png"))
open(os.path.join(TILED, "Terrain.tsx"), "w", encoding="utf-8").write(
    '<?xml version="1.0" encoding="UTF-8"?>\n'
    f'<tileset version="1.10" tiledversion="1.12.2" name="Terrain" tilewidth="32" tileheight="32" '
    f'tilecount="{(tw // 32) * (th // 32)}" columns="{tw // 32}">\n'
    f' <image source="../Sprites/Terrain.png" width="{tw}" height="{th}"/>\n'
    '</tileset>\n')

# ───────────── Props (image collection) ─────────────
# kind: obstacle = sorted by feet + collider at the feet (trees, rocks)
#       plant    = sorted by feet, no collider (bushes, sprouts)
#       decal    = flat on the ground, no collider (gravel)
# pivotX/pivotY: feet inside the rect, px from the left / from the bottom.
PROPS_DEF = [
    # name, x, y, w, h, kind, group, pivotX, pivotY, colW, colH
    ("TreeAutumn01", 10, 2, 101, 182, "obstacle", "Trees", 45, 14, 24, 10),
    ("TreeAutumn02", 138, 2, 101, 182, "obstacle", "Trees", 45, 14, 24, 10),
    ("Sapling", 288, 13, 32, 47, "obstacle", "Trees", 17, 4, 8, 5),
    ("DeadTree", 320, 14, 26, 49, "obstacle", "Trees", 14, 0, 6, 4),
    ("Slab01", 141, 200, 41, 21, "obstacle", "Rocks", 20, 0, 34, 10),
    ("Slab02", 135, 227, 47, 27, "obstacle", "Rocks", 23, 0, 40, 12),
    ("Slab03", 138, 264, 38, 20, "obstacle", "Rocks", 19, 0, 32, 9),
    ("Rock01", 42, 199, 17, 10, "obstacle", "Rocks", 8, 0, 14, 6),
    ("Rock02", 71, 232, 18, 12, "obstacle", "Rocks", 9, 0, 15, 7),
    ("Rock03", 42, 263, 17, 10, "obstacle", "Rocks", 8, 0, 14, 6),
    ("Rock04", 72, 200, 14, 8, "obstacle", "Rocks", 7, 0, 11, 5),
    ("Rock05", 42, 234, 12, 8, "obstacle", "Rocks", 6, 0, 10, 5),
    ("Rock06", 104, 234, 13, 8, "obstacle", "Rocks", 6, 0, 10, 5),
    ("Rock07", 72, 264, 14, 8, "obstacle", "Rocks", 7, 0, 11, 5),
    ("BushTall", 256, 0, 32, 64, "plant", "Bushes", 16, 7, 0, 0),
    ("BushRound", 265, 65, 50, 30, "plant", "Bushes", 25, 2, 0, 0),
    ("SproutA", 324, 70, 18, 22, "plant", "Bushes", 5, 3, 0, 0),
    ("SproutB", 356, 67, 27, 29, "plant", "Bushes", 15, 1, 0, 0),
    ("Gravel01", 106, 200, 9, 6, "decal", "Decals", 4, 0, 0, 0),
    ("Gravel02", 106, 264, 9, 6, "decal", "Decals", 4, 0, 0, 0),
    # ── ruins (Atlas-Props) ──
    ("PillarArches", 263, 114, 53, 76, "obstacle", "Ruins", 26, 2, 46, 16),
    ("PillarSmall", 327, 129, 51, 60, "obstacle", "Ruins", 25, 2, 44, 16),
    ("Pedestal", 389, 148, 53, 42, "obstacle", "Ruins", 26, 3, 46, 14),
    ("BrokenWall", 205, 209, 36, 71, "obstacle", "Ruins", 18, 2, 30, 14),
    ("BrickBlock", 259, 235, 90, 48, "obstacle", "Ruins", 45, 4, 76, 22),
    ("BrickTower", 359, 194, 144, 189, "obstacle", "Ruins", 72, 28, 112, 50),
    ("FountainStone", 83, 294, 123, 119, "obstacle", "Ruins", 61, 22, 100, 70),
    ("ChaliceStone", 16, 299, 29, 47, "obstacle", "Ruins", 14, 2, 14, 6),
]
# Animated props: (name, sheet, frame-local rect x, y, w, h, kind, group, pivotX, pivotY, colW, colH, fps)
ANIM_PROPS = [
    ("Altar", "Altar.png", 17, 0, 187, 284, "obstacle", "Ruins", 93, 69, 180, 145, 10),
    ("Shrine", "Shrine.png", 19, 6, 123, 119, "obstacle", "Ruins", 61, 22, 100, 70, 8),
    ("ChaliceSpirits", "Chalice.png", 16, 6, 30, 52, "obstacle", "Ruins", 15, 2, 14, 6, 8),
    ("Sparkles", "Sparkles.png", 0, 0, 64, 64, "ambient", "Ambient", 32, 32, 0, 0, 8),
]
pw, ph, _ = read_png(os.path.join(SPRITES, "Props.png"))
entries = [(n, "Props.png", pw, ph, x, y, w, h, k, g, pvx, pvy, cw, ch, None)
           for (n, x, y, w, h, k, g, pvx, pvy, cw, ch) in PROPS_DEF]
for (n, sheet, x, y, w, h, k, g, pvx, pvy, cw, ch, fps) in ANIM_PROPS:
    frames, cols, sw, sh, fw, fh = anim_info[sheet]
    entries.append((n, sheet, sw, sh, x, y, w, h, k, g, pvx, pvy, cw, ch, (frames, cols, fw, fh, fps)))
maxw = max(e[6] for e in entries)
maxh = max(e[7] for e in entries)
lines = ['<?xml version="1.0" encoding="UTF-8"?>',
         f'<tileset version="1.10" tiledversion="1.12.2" name="Props" tilewidth="{maxw}" tileheight="{maxh}" '
         f'tilecount="{len(entries)}" columns="0" objectalignment="bottom">',
         ' <grid orientation="orthogonal" width="1" height="1"/>']
catalog = []
for i, (name, sheet, sw, sh, x, y, w, h, kind, group, pvx, pvy, cw, ch, anim) in enumerate(entries):
    lines.append(f' <tile id="{i}" type={quoteattr(name)} x="{x}" y="{y}" width="{w}" height="{h}">')
    lines.append('  <properties>')
    lines.append(f'   <property name="kind" value="{kind}"/>')
    lines.append(f'   <property name="group" value="{group}"/>')
    lines.append(f'   <property name="pivotX" type="int" value="{pvx}"/>')
    lines.append(f'   <property name="pivotY" type="int" value="{pvy}"/>')
    if kind == "obstacle":
        lines.append(f'   <property name="colliderWidth" type="int" value="{cw}"/>')
        lines.append(f'   <property name="colliderHeight" type="int" value="{ch}"/>')
    if anim:
        frames, cols, fw, fh, fps = anim
        # Frame i = this rect moved to column i % frameColumns, row i / frameColumns of the sheet.
        lines.append(f'   <property name="frames" type="int" value="{frames}"/>')
        lines.append(f'   <property name="frameColumns" type="int" value="{cols}"/>')
        lines.append(f'   <property name="frameWidth" type="int" value="{fw}"/>')
        lines.append(f'   <property name="frameHeight" type="int" value="{fh}"/>')
        lines.append(f'   <property name="fps" type="float" value="{fps}"/>')
    lines.append('  </properties>')
    lines.append(f'  <image source="../Sprites/{sheet}" width="{sw}" height="{sh}"/>')
    lines.append(' </tile>')
    catalog.append(dict(name=name, tileId=i, w=w, h=h, kind=kind, group=group, pivotX=pvx, pivotY=pvy, colW=cw, colH=ch))
lines.append('</tileset>')
open(os.path.join(TILED, "Props.tsx"), "w", encoding="utf-8").write("\n".join(lines) + "\n")

json.dump({"props": catalog, "water": water_ids, "waterFrames": FRAMES, "terrainColumns": tw // 32},
          open(os.path.join(TILED, "catalog.json"), "w"), indent=1)
print(f"water rows={len(ROWS)} sheet={W}x{H} props={len(catalog)} terrain={tw}x{th}")
