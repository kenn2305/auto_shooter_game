"""Build the obstacle catalog and write Tiled tilesets (.tsx) for Map_01."""
import json
import os
import sys
from xml.sax.saxutils import quoteattr

from components import components

SRC = "E:/Map/Map_01"
OUT = sys.argv[1]  # Tiled folder inside Unity project

ROW_COLORS = ["Green", "Yellow", "DarkGreen", "Autumn", "Lime"]
ROW_TOPS = [0, 215, 430, 628, 822, 2000]


def row_of(y):
    for r in range(5):
        if ROW_TOPS[r] <= y < ROW_TOPS[r + 1]:
            return r
    return 4


catalog = []  # dicts: name, sheet, x,y,w,h, kind, colW, colH, shadow

# ---------- shadows ----------
_, _, shadow_comps = components(f"{SRC}/PlantShadow.png", 1)
shadow_comps.sort(key=lambda c: c["w"])
shadows = []
for i, c in enumerate(shadow_comps):
    s = dict(name=f"Shadow_{i + 1:02d}", sheet="PlantShadow.png", kind="shadow", **{k: c[k] for k in "xywh"})
    shadows.append(s)


def pick_shadow(target_w):
    return min(shadows, key=lambda s: abs(s["w"] - target_w))["name"]


# ---------- plants ----------
_, _, plant_comps = components(f"{SRC}/Plants.png", 1)
rows = {r: [] for r in range(5)}
for c in plant_comps:
    rows[row_of(c["y"])].append(c)
for r, comps in rows.items():
    color = ROW_COLORS[r]
    top = min(c["y"] for c in comps)
    trees = sorted([c for c in comps if c["h"] >= 95], key=lambda c: c["x"])
    pines = sorted([c for c in comps if c["h"] < 95 and c["w"] <= 34 and c["h"] >= 45], key=lambda c: c["x"])
    rest = [c for c in comps if c not in trees and c not in pines]
    stumps = sorted([c for c in rest if c["y"] - top > 90], key=lambda c: c["x"])
    bushes = sorted([c for c in rest if c not in stumps], key=lambda c: c["x"])
    assert len(trees) == 6 and len(pines) == 2 and len(stumps) == 2 and len(bushes) == 5, (r, len(trees), len(pines), len(stumps), len(bushes))
    for i, c in enumerate(trees):
        catalog.append(dict(name=f"Tree{i + 1:02d}_{color}", kind="obstacle", group="Trees", sheet="Plants.png",
                            colW=26 if c["w"] > 100 else 20, colH=12, shadow=pick_shadow(c["w"] * 0.62), **{k: c[k] for k in "xywh"}))
    for i, c in enumerate(bushes):
        catalog.append(dict(name=f"Bush{i + 1:02d}_{color}", kind="obstacle", group="Bushes", sheet="Plants.png",
                            colW=round(c["w"] * 0.75), colH=min(16, round(c["h"] * 0.45)), shadow=pick_shadow(c["w"] * 0.8), **{k: c[k] for k in "xywh"}))
    for i, c in enumerate(pines):
        catalog.append(dict(name=f"Pine{i + 1:02d}_{color}", kind="obstacle", group="Pines", sheet="Plants.png",
                            colW=14, colH=10, shadow=pick_shadow(c["w"] * 0.8), **{k: c[k] for k in "xywh"}))
    for i, c in enumerate(stumps):
        catalog.append(dict(name=f"Stump{i + 1:02d}_{color}", kind="obstacle", group="Stumps", sheet="Plants.png",
                            colW=round(c["w"] * 0.75), colH=10, shadow="", **{k: c[k] for k in "xywh"}))

# ---------- props ----------
_, _, prop_comps = components(f"{SRC}/Props.png", 2)


def find(x, y):
    for c in prop_comps:
        if abs(c["x"] - x) <= 3 and abs(c["y"] - y) <= 3:
            return c
    raise KeyError((x, y))


PROPS = [
    # name, x, y, kind, colW, colH
    ("Obelisk", 16, 16, "obstacle", 26, 14),
    ("BrokenPillar", 86, 56, "obstacle", 38, 16),
    ("StoneSlab", 152, 41, "obstacle", 32, 14),
    ("StatueLeft", 205, 16, "obstacle", 48, 24),
    ("StatueRight", 304, 16, "obstacle", 48, 24),
    ("RunePillar", 400, 64, "obstacle", 30, 14),
    ("PortalRuins", 16, 157, "obstacle", 140, 36),
    ("SmallPedestal", 208, 198, "obstacle", 28, 14),
    ("Gazebo", 335, 207, "obstacle", 76, 30),
    ("Rock01", 2, 376, "obstacle", 50, 26),
    ("Rock02", 113, 380, "obstacle", 50, 24),
    ("StoneDisc", 234, 261, "decal", 0, 0),
    ("StonePathCurve", 10, 263, "decal", 0, 0),
    ("StonePath", 220, 328, "decal", 0, 0),
    ("StoneCircle", 324, 394, "decal", 0, 0),
]
for name, x, y, kind, cw, ch in PROPS:
    c = find(x, y)
    catalog.append(dict(name=name, kind=kind, group="Ruins" if kind == "obstacle" else "Decals", sheet="Props.png",
                        colW=cw, colH=ch, shadow="", **{k: c[k] for k in "xywh"}))

for s in shadows:
    catalog.append(dict(name=s["name"], kind="shadow", group="Shadows", sheet="PlantShadow.png",
                        colW=0, colH=0, shadow="", **{k: s[k] for k in "xywh"}))

# ---------- write Obstacles.tsx ----------
SHEET_SIZE = {"Plants.png": (1024, 1024), "Props.png": (512, 512), "PlantShadow.png": (512, 512)}
maxw = max(c["w"] for c in catalog)
maxh = max(c["h"] for c in catalog)
lines = [
    '<?xml version="1.0" encoding="UTF-8"?>',
    f'<tileset version="1.10" tiledversion="1.12.2" name="Obstacles" tilewidth="{maxw}" tileheight="{maxh}" tilecount="{len(catalog)}" columns="0" objectalignment="bottom">',
    ' <grid orientation="orthogonal" width="1" height="1"/>',
]
for tid, c in enumerate(catalog):
    c["tileId"] = tid
    sw, sh = SHEET_SIZE[c["sheet"]]
    lines.append(f' <tile id="{tid}" type={quoteattr(c["name"])} x="{c["x"]}" y="{c["y"]}" width="{c["w"]}" height="{c["h"]}">')
    lines.append('  <properties>')
    lines.append(f'   <property name="kind" value={quoteattr(c["kind"])}/>')
    lines.append(f'   <property name="group" value={quoteattr(c["group"])}/>')
    lines.append(f'   <property name="colliderWidth" type="int" value="{c["colW"]}"/>')
    lines.append(f'   <property name="colliderHeight" type="int" value="{c["colH"]}"/>')
    lines.append(f'   <property name="shadow" value={quoteattr(c["shadow"])}/>')
    lines.append('  </properties>')
    lines.append(f'  <image source="../Sprites/{c["sheet"]}" width="{sw}" height="{sh}"/>')
    lines.append(' </tile>')
lines.append('</tileset>')
open(os.path.join(OUT, "Obstacles.tsx"), "w", encoding="utf-8").write("\n".join(lines) + "\n")


def grid_tsx(name, image, w, h):
    cols, rows = w // 32, h // 32
    return "\n".join([
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<tileset version="1.10" tiledversion="1.12.2" name="{name}" tilewidth="32" tileheight="32" tilecount="{cols * rows}" columns="{cols}">',
        f' <image source="../Sprites/{image}" width="{w}" height="{h}"/>',
        '</tileset>', ""])


open(os.path.join(OUT, "Grass.tsx"), "w", encoding="utf-8").write(grid_tsx("Grass", "Grass.png", 1024, 512))
open(os.path.join(OUT, "GrassDetail.tsx"), "w", encoding="utf-8").write(grid_tsx("GrassDetail", "GrassDetail.png", 1024, 512))

json.dump({"obstacles": catalog, "shadows": shadows}, open(os.path.join(OUT, "catalog.json"), "w"), indent=1)
print("catalog", len(catalog), "shadows", len(shadows))
for c in catalog[:15] + catalog[-15:]:
    print(c["tileId"], c["name"], c["w"], c["h"], c["colW"], c["colH"], c["shadow"])
