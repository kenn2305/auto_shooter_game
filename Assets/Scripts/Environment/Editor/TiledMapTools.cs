using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tiled (.tmx/.tsx) → Unity pipeline, one folder per map (Assets/GameContent/Environment/Map_XX).
///
/// 1. Tools > Map > Build Map_XX Assets: slices the sprite sheets referenced by the Tiled tilesets
///    (grid tilesets per 32 px cell, object collections per tile sub-rectangle), then creates
///    one prefab per collection tile. Existing prefabs are never overwritten, so hand-tuned
///    colliders and sprites survive a rebuild.
/// 2. Tools > Map > Import Map_XX Into Scene: rebuilds the "Map_XX" object in the open scene from the
///    .tmx — tile layers become Tilemaps, tile objects become prefab instances, point objects become
///    markers — and points FlowFieldService at the map.
///
/// Conventions: 32 px = 1 unit = 1 cell, map centred on the world origin. Collection tile property
/// "kind": obstacle (sorted by its feet, collider only at the feet), plant (sorted by its feet, no
/// collider), decal (flat on the ground). Optional pivotX/pivotY = the feet inside the tile rect in px
/// (from the left / from the bottom; default bottom-centre). Grid tiles with a Tiled animation become
/// AnimatedTiles. A tile layer with the bool property "blocking" (water) is impassable: its tiles get
/// sprite colliders and cells whose tile "cover" ≥ <see cref="BlockingCover"/> are blocked for the flow field.
/// </summary>
public static class TiledMapTools
{
    private static string mapName = "Map_01";
    private static string MapRoot => $"Assets/GameContent/Environment/{mapName}";
    private static string TiledFolder => MapRoot + "/Tiled";
    private static string MapFile => $"{TiledFolder}/{mapName}.tmx";
    private static string TilesFolder => MapRoot + "/Tiles";
    private static string PrefabsFolder => MapRoot + "/Prefabs";

    /// <summary>A blocking-layer tile blocks navigation when at least this share of its cell is opaque.</summary>
    private const float BlockingCover = 0.25f;

    private const int PixelsPerUnit = 32;
    private const string ObstacleLayerName = "Ostacle";

    private const int GroundSortingOrder = -100;
    private const int DecalSortingOrder = -60;
    private const int ShadowSortingOrder = -50;
    // Bushes, sprouts and pebbles lie flat on the map: under EXP gems (-40), characters and obstacles (0).
    private const int DecorationSortingOrder = -45;
    private const int AmbientSortingOrder = 40;

    private const uint FlipHorizontal = 0x80000000;
    private const uint FlipVertical = 0x40000000;
    private const uint FlipDiagonal = 0x20000000;
    private const uint GidMask = 0x1FFFFFFF;

    // ───────────────────────────── Tileset model ─────────────────────────────

    private sealed class TsxTile
    {
        public int Id;
        public string Type;
        public RectInt Rect; // Tiled pixel rect (y down) inside the sheet
        public string Image; // project path of the sheet
        public readonly Dictionary<string, string> Properties = new();
        public readonly List<(int TileId, int DurationMs)> Animation = new();

        public string Prop(string key, string fallback = "") =>
            Properties.TryGetValue(key, out string v) ? v : fallback;

        public float PropFloat(string key) =>
            float.TryParse(Prop(key, "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
    }

    private sealed class TsxTileset
    {
        public string Name;
        public string Path;
        public int TileWidth;
        public int TileHeight;
        public int Columns;
        public int TileCount;
        public string Image; // grid tilesets only
        public int ImageWidth;
        public int ImageHeight;
        public bool IsCollection => string.IsNullOrEmpty(Image);
        public bool Blocking;
        public readonly Dictionary<int, TsxTile> Tiles = new();

        public string SpriteName(int id) => IsCollection ? Tiles[id].Type : $"{Name}_{id}";
    }

    private static TsxTileset LoadTsx(string tsxPath)
    {
        var doc = new XmlDocument();
        doc.Load(tsxPath);
        XmlElement root = doc.DocumentElement;
        string dir = System.IO.Path.GetDirectoryName(tsxPath);

        var ts = new TsxTileset
        {
            Name = root.GetAttribute("name"),
            Path = tsxPath,
            TileWidth = Int(root, "tilewidth"),
            TileHeight = Int(root, "tileheight"),
            Columns = Int(root, "columns"),
            TileCount = Int(root, "tilecount"),
        };

        if (root.SelectSingleNode("image") is XmlElement image)
        {
            ts.Image = ToProjectPath(dir, image.GetAttribute("source"));
            ts.ImageWidth = Int(image, "width");
            ts.ImageHeight = Int(image, "height");
        }

        foreach (XmlElement prop in root.SelectNodes("properties/property"))
        {
            if (prop.GetAttribute("name") == "blocking")
                ts.Blocking = prop.GetAttribute("value") == "true";
        }

        foreach (XmlElement tileEl in root.SelectNodes("tile"))
        {
            var tile = new TsxTile
            {
                Id = Int(tileEl, "id"),
                Type = tileEl.HasAttribute("type") ? tileEl.GetAttribute("type") : tileEl.GetAttribute("class"),
            };

            foreach (XmlElement prop in tileEl.SelectNodes("properties/property"))
                tile.Properties[prop.GetAttribute("name")] = prop.GetAttribute("value");

            foreach (XmlElement frame in tileEl.SelectNodes("animation/frame"))
                tile.Animation.Add((Int(frame, "tileid"), Int(frame, "duration")));

            if (tileEl.SelectSingleNode("image") is XmlElement tileImage)
            {
                tile.Image = ToProjectPath(dir, tileImage.GetAttribute("source"));
                int w = tileEl.HasAttribute("width") ? Int(tileEl, "width") : Int(tileImage, "width");
                int h = tileEl.HasAttribute("height") ? Int(tileEl, "height") : Int(tileImage, "height");
                tile.Rect = new RectInt(Int(tileEl, "x"), Int(tileEl, "y"), w, h);
            }

            ts.Tiles[tile.Id] = tile;
        }

        return ts;
    }

    // ───────────────────────────── Build assets ─────────────────────────────

    [MenuItem("Tools/Map/Build Map_01 Assets")]
    public static void BuildMap01Assets() => BuildAssets("Map_01");

    [MenuItem("Tools/Map/Build Map_02 Assets")]
    public static void BuildMap02Assets() => BuildAssets("Map_02");

    [MenuItem("Tools/Map/Import Map_01 Into Scene")]
    public static void ImportMap01() => ImportMap("Map_01");

    [MenuItem("Tools/Map/Import Map_02 Into Scene")]
    public static void ImportMap02() => ImportMap("Map_02");

    public static void BuildAssets(string map)
    {
        mapName = map;
        var tilesets = Directory.GetFiles(TiledFolder, "*.tsx").Select(p => LoadTsx(p.Replace('\\', '/'))).ToList();

        foreach (TsxTileset ts in tilesets.Where(t => !t.IsCollection))
            SliceGridTileset(ts);

        foreach (TsxTileset ts in tilesets.Where(t => t.IsCollection))
        {
            foreach (IGrouping<string, TsxTile> sheet in ts.Tiles.Values.Where(t => t.Image != null).GroupBy(t => t.Image))
                SliceCollectionSheet(sheet.Key, sheet.ToList());
        }

        int created = 0;
        foreach (TsxTileset ts in tilesets.Where(t => t.IsCollection))
            created += BuildPrefabs(ts);

        AssetDatabase.SaveAssets();
        Debug.Log($"[TiledMapTools] Assets built. {created} new prefab(s) in {PrefabsFolder}.");
    }

    private static void SliceGridTileset(TsxTileset ts)
    {
        byte[] alpha = ReadAlpha(ts.Image, out int texW, out int texH);
        var rects = new List<RectInt>();
        var names = new List<string>();
        int rows = ts.TileCount / Math.Max(1, ts.Columns);

        for (int id = 0; id < ts.TileCount; id++)
        {
            int col = id % ts.Columns;
            int row = id / ts.Columns;
            var rect = new RectInt(col * ts.TileWidth, row * ts.TileHeight, ts.TileWidth, ts.TileHeight);
            if (row >= rows || !HasPixels(alpha, texW, texH, rect))
                continue;

            rects.Add(rect);
            names.Add(ts.SpriteName(id));
        }

        ApplySprites(ts.Image, rects, names, rects.Select(_ => new Vector2(0.5f, 0.5f)).ToList());
    }

    private static void SliceCollectionSheet(string sheetPath, List<TsxTile> tiles)
    {
        var rects = new List<RectInt>();
        var names = new List<string>();
        var pivots = new List<Vector2>();
        foreach (TsxTile t in tiles)
        {
            Vector2 pivot = t.Prop("kind") == "shadow" ? new Vector2(0.5f, 0.5f) : FeetPivot(t);
            for (int i = 0; i < FrameCount(t); i++)
            {
                rects.Add(FrameRect(t, i));
                names.Add(FrameName(t, i));
                pivots.Add(pivot);
            }
        }

        ApplySprites(sheetPath, rects, names, pivots);
    }

    // Animated collection tiles: property "frames" (count), "frameColumns", "frameWidth", "frameHeight".
    // Frame i is the tile rect moved by (i % frameColumns, i / frameColumns) frames.
    private static int FrameCount(TsxTile tile) => Math.Max(1, (int)tile.PropFloat("frames"));

    private static RectInt FrameRect(TsxTile tile, int i)
    {
        if (i == 0)
            return tile.Rect;
        int columns = Math.Max(1, (int)tile.PropFloat("frameColumns"));
        int fw = (int)tile.PropFloat("frameWidth");
        int fh = (int)tile.PropFloat("frameHeight");
        return new RectInt(tile.Rect.x + i % columns * fw, tile.Rect.y + i / columns * fh, tile.Rect.width, tile.Rect.height);
    }

    private static string FrameName(TsxTile tile, int i) => i == 0 ? tile.Type : $"{tile.Type}_{i:00}";

    /// <summary>Normalised sprite pivot at the feet: pivotX/pivotY (px) or bottom-centre.</summary>
    private static Vector2 FeetPivot(TsxTile tile)
    {
        float x = tile.Properties.ContainsKey("pivotX") ? tile.PropFloat("pivotX") / tile.Rect.width : 0.5f;
        float y = tile.Properties.ContainsKey("pivotY") ? tile.PropFloat("pivotY") / tile.Rect.height : 0f;
        return new Vector2(x, y);
    }

    /// <summary>World offset from the Tiled anchor (bottom-centre of the rect) to the feet pivot.</summary>
    private static Vector2 FeetOffset(TsxTile tile)
    {
        Vector2 pivot = FeetPivot(tile);
        return new Vector2((pivot.x - 0.5f) * tile.Rect.width, pivot.y * tile.Rect.height) / PixelsPerUnit;
    }

    /// <summary>Slices a sheet (Tiled rects, y down) keeping the spriteID of sprites that already exist.</summary>
    private static void ApplySprites(string texturePath, List<RectInt> tiledRects, List<string> names, List<Vector2> pivots)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        if (importer == null)
        {
            Debug.LogError($"[TiledMapTools] Missing texture {texturePath}");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();

        int texHeight = ReadSize(texturePath).y;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        Dictionary<string, SpriteRect> existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r);
        var result = new List<SpriteRect>();

        for (int i = 0; i < tiledRects.Count; i++)
        {
            RectInt r = tiledRects[i];
            if (!existing.TryGetValue(names[i], out SpriteRect sprite))
            {
                sprite = new SpriteRect { name = names[i], spriteID = GUID.Generate() };
            }

            sprite.rect = new Rect(r.x, texHeight - r.y - r.height, r.width, r.height);
            sprite.alignment = SpriteAlignment.Custom;
            sprite.pivot = pivots[i];
            sprite.border = Vector4.zero;
            result.Add(sprite);
        }

        provider.SetSpriteRects(result.ToArray());

        var nameIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        nameIds?.SetNameFileIdPairs(result.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)).ToList());

        provider.Apply();
        importer.SaveAndReimport();
    }

    private static int BuildPrefabs(TsxTileset ts)
    {
        int obstacleLayer = LayerMask.NameToLayer(ObstacleLayerName);
        if (obstacleLayer < 0)
        {
            Debug.LogError($"[TiledMapTools] Layer '{ObstacleLayerName}' not found.");
            return 0;
        }

        int created = 0;
        foreach (TsxTile tile in ts.Tiles.Values)
        {
            string kind = tile.Prop("kind", "obstacle");
            if (kind == "shadow" || string.IsNullOrEmpty(tile.Type))
                continue;

            string folder = $"{PrefabsFolder}/{tile.Prop("group", "Misc")}";
            string path = $"{folder}/{tile.Type}.prefab";
            if (File.Exists(path))
                continue;

            EnsureFolder(folder);
            Sprite sprite = LoadSprite(tile.Image, tile.Type);
            if (sprite == null)
            {
                Debug.LogError($"[TiledMapTools] Sprite {tile.Type} not found in {tile.Image}");
                continue;
            }

            var root = new GameObject(tile.Type);
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;

            int frameCount = FrameCount(tile);
            if (frameCount > 1)
            {
                var frames = new Sprite[frameCount];
                for (int i = 0; i < frameCount; i++)
                    frames[i] = LoadSprite(tile.Image, FrameName(tile, i));
                root.AddComponent<SpriteFrameAnimator>().Setup(frames, Mathf.Max(1f, tile.PropFloat("fps")));
            }

            if (kind == "decal")
            {
                renderer.sortingOrder = DecalSortingOrder;
            }
            else if (kind == "ambient")
            {
                // Floating particles: drawn over everything, never block.
                renderer.sortingOrder = AmbientSortingOrder;
            }
            else if (kind == "plant")
            {
                // Bushes, sprouts and pebbles: drawn flat on the map under everything that moves, walkable (no collider).
                root.isStatic = true;
                renderer.sortingOrder = DecorationSortingOrder;
            }
            else
            {
                root.layer = obstacleLayer;
                root.isStatic = true;
                renderer.spriteSortPoint = SpriteSortPoint.Pivot;

                // Collider only at the feet so characters can walk behind the canopy / statue body.
                float width = Mathf.Max(4f, tile.PropFloat("colliderWidth")) / PixelsPerUnit;
                float height = Mathf.Max(4f, tile.PropFloat("colliderHeight")) / PixelsPerUnit;
                var collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(width, height);
                collider.offset = new Vector2(0f, height * 0.5f);

                string shadowName = tile.Prop("shadow");
                Sprite shadowSprite = string.IsNullOrEmpty(shadowName) ? null : FindShadowSprite(ts, shadowName);
                if (shadowSprite != null)
                {
                    var shadow = new GameObject("Shadow");
                    shadow.transform.SetParent(root.transform, false);
                    shadow.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
                    var shadowRenderer = shadow.AddComponent<SpriteRenderer>();
                    shadowRenderer.sprite = shadowSprite;
                    shadowRenderer.sortingOrder = ShadowSortingOrder;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            created++;
        }

        return created;
    }

    private static Sprite FindShadowSprite(TsxTileset ts, string shadowName)
    {
        TsxTile shadowTile = ts.Tiles.Values.FirstOrDefault(t => t.Type == shadowName);
        return shadowTile == null ? null : LoadSprite(shadowTile.Image, shadowName);
    }

    // ───────────────────────────── Import map ─────────────────────────────

    public static void ImportMap(string map)
    {
        mapName = map;
        if (!File.Exists(MapFile))
        {
            Debug.LogError($"[TiledMapTools] {MapFile} not found.");
            return;
        }

        var doc = new XmlDocument();
        doc.Load(MapFile);
        XmlElement mapEl = doc.DocumentElement;
        int mapW = Int(mapEl, "width");
        int mapH = Int(mapEl, "height");
        int tileW = Int(mapEl, "tilewidth");
        int tileH = Int(mapEl, "tileheight");
        int halfW = mapW / 2;
        int halfH = mapH / 2;

        var tilesets = new List<(int FirstGid, TsxTileset Tileset)>();
        foreach (XmlElement tsEl in mapEl.SelectNodes("tileset"))
        {
            string source = ToProjectPath(TiledFolder, tsEl.GetAttribute("source"));
            tilesets.Add((Int(tsEl, "firstgid"), LoadTsx(source)));
        }
        tilesets.Sort((a, b) => a.FirstGid.CompareTo(b.FirstGid));

        string previewName = mapName + " Preview";
        foreach (string existing in new[] { mapName, previewName })
        {
            GameObject old = GameObject.Find(existing);
            if (old != null)
                Undo.DestroyObjectImmediate(old);
        }

        var cellSize = new Vector3((float)tileW / PixelsPerUnit, (float)tileH / PixelsPerUnit, 0f);

        // 1. Editor-only preview of the whole map (so it can be seen and inspected in the Scene view).
        //    Tagged EditorOnly: stripped from builds, and MapStreamer disables it in Play mode.
        var preview = new GameObject(previewName) { tag = "EditorOnly" };
        Undo.RegisterCreatedObjectUndo(preview, "Import Tiled map");
        preview.AddComponent<Grid>().cellSize = cellSize;

        var tileCache = new Dictionary<uint, TileBase>();
        tileCover.Clear();
        var previewTilemaps = new List<Tilemap>();
        var blockingLayers = new List<bool>();
        int instanced = 0;
        var missingPrefabs = new HashSet<string>();

        foreach (XmlNode node in mapEl.ChildNodes)
        {
            if (node is not XmlElement layerEl)
                continue;

            if (layerEl.Name == "layer")
            {
                previewTilemaps.Add(BuildTileLayer(layerEl, preview.transform, mapW, mapH, halfW, halfH, tilesets, tileCache,
                    GroundSortingOrder + previewTilemaps.Count * 10));
                blockingLayers.Add(BoolProperty(layerEl, "blocking"));
            }
            else if (layerEl.Name == "objectgroup")
            {
                instanced += BuildObjectLayer(layerEl, preview.transform, tileW, tileH, halfW, halfH, tilesets, missingPrefabs);
            }
        }

        // Map property "wrap" (Tiled: Map > Map Properties) = none | horizontal | vertical | both (default both).
        string wrap = "both";
        foreach (XmlElement prop in mapEl.SelectNodes("properties/property"))
        {
            if (prop.GetAttribute("name") == "wrap")
                wrap = prop.GetAttribute("value").Trim().ToLowerInvariant();
        }
        bool wrapX = wrap == "both" || wrap == "horizontal";
        bool wrapY = wrap == "both" || wrap == "vertical";

        // 2. Baked data for streaming.
        StreamedMapData data = BakeStreamedData(mapName, preview.transform, previewTilemaps, blockingLayers, mapW, mapH,
            halfW, halfH, cellSize, wrapX, wrapY);

        // 3. Runtime map: small window tilemaps + pooled objects around the camera.
        var mapRoot = new GameObject(mapName);
        Undo.RegisterCreatedObjectUndo(mapRoot, "Import Tiled map");
        mapRoot.AddComponent<Grid>().cellSize = cellSize;

        var windows = new List<Tilemap>();
        int obstacleLayer = LayerMask.NameToLayer(ObstacleLayerName);
        for (int i = 0; i < previewTilemaps.Count; i++)
        {
            Tilemap source = previewTilemaps[i];
            var layer = new GameObject(source.name);
            layer.transform.SetParent(mapRoot.transform, false);
            var tilemap = layer.AddComponent<Tilemap>();
            var renderer = layer.AddComponent<TilemapRenderer>();
            var sourceRenderer = source.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = sourceRenderer.sortingOrder;
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.mode = TilemapRenderer.Mode.Chunk;

            if (blockingLayers[i])
            {
                // Water: the streamed window only holds tiles around the camera, so its collider stays small.
                // Tiles use their sprite outline, so the walkable part of a bank tile stays walkable.
                if (obstacleLayer >= 0)
                    layer.layer = obstacleLayer;
                layer.isStatic = true;
                layer.AddComponent<TilemapCollider2D>();
            }

            windows.Add(tilemap);
        }

        var objects = new GameObject("Objects");
        objects.transform.SetParent(mapRoot.transform, false);

        var streamer = mapRoot.AddComponent<MapStreamer>();
        var so = new SerializedObject(streamer);
        so.FindProperty("data").objectReferenceValue = data;
        so.FindProperty("objectsRoot").objectReferenceValue = objects.transform;
        so.FindProperty("editorPreview").objectReferenceValue = preview;
        SerializedProperty windowsProp = so.FindProperty("windowTilemaps");
        windowsProp.arraySize = windows.Count;
        for (int i = 0; i < windows.Count; i++)
            windowsProp.GetArrayElementAtIndex(i).objectReferenceValue = windows[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        AssignFlowFieldSource(streamer);
        EditorSceneManager.MarkSceneDirty(mapRoot.scene);

        foreach (string missing in missingPrefabs)
            Debug.LogWarning($"[TiledMapTools] No prefab named '{missing}' — run Tools > Map > Build {mapName} Assets.");

        Debug.Log($"[TiledMapTools] Imported {mapName}: {mapW}x{mapH} tiles, {instanced} object(s), " +
            $"{data.Prefabs.Length} prefab type(s), wrap X={wrapX} Y={wrapY}.");
        Selection.activeGameObject = mapRoot;
    }

    private static StreamedMapData BakeStreamedData(string mapName, Transform preview, List<Tilemap> tilemaps,
        List<bool> blockingLayers, int mapW, int mapH, int halfW, int halfH, Vector3 cellSize, bool wrapX, bool wrapY)
    {
        string path = $"{MapRoot}/{mapName}_Data.asset";
        var data = AssetDatabase.LoadAssetAtPath<StreamedMapData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<StreamedMapData>();
            AssetDatabase.CreateAsset(data, path);
        }

        data.Width = mapW;
        data.Height = mapH;
        data.CellSize = cellSize;
        data.WrapX = wrapX;
        data.WrapY = wrapY;

        data.TileLayers = new StreamedMapData.TileLayer[tilemaps.Count];
        for (int l = 0; l < tilemaps.Count; l++)
        {
            Tilemap tilemap = tilemaps[l];
            var palette = new List<TileBase>();
            var lookup = new Dictionary<TileBase, ushort>();
            var cells = new ushort[mapW * mapH];

            for (int y = 0; y < mapH; y++)
            {
                for (int x = 0; x < mapW; x++)
                {
                    TileBase tile = tilemap.GetTile(new Vector3Int(x - halfW, y - halfH, 0));
                    if (tile == null)
                        continue;

                    if (!lookup.TryGetValue(tile, out ushort id))
                    {
                        palette.Add(tile);
                        id = (ushort)palette.Count;
                        lookup[tile] = id;
                    }

                    cells[x + y * mapW] = id;
                }
            }

            data.TileLayers[l] = new StreamedMapData.TileLayer
            {
                Name = tilemap.name,
                SortingOrder = tilemap.GetComponent<TilemapRenderer>().sortingOrder,
                Palette = palette.ToArray(),
                Cells = cells,
                Blocking = blockingLayers[l],
                PaletteBlocks = palette
                    .Select(t => blockingLayers[l] && (!tileCover.TryGetValue(t, out float c) || c >= BlockingCover))
                    .ToArray(),
            };
        }

        var prefabs = new List<GameObject>();
        var prefabIds = new Dictionary<GameObject, ushort>();
        var placed = new List<StreamedMapData.PlacedObject>();
        Vector2 mapOffset = new Vector2(halfW * cellSize.x, halfH * cellSize.y);
        float left = 0f, right = 0f, up = 0f, down = 0f;

        foreach (Transform layer in preview)
        {
            foreach (Transform obj in layer)
            {
                var prefab = PrefabUtility.GetCorrespondingObjectFromSource(obj.gameObject);
                if (prefab == null)
                    continue; // markers and other non-prefab objects stay editor-only

                if (!prefabIds.TryGetValue(prefab, out ushort id))
                {
                    id = (ushort)prefabs.Count;
                    prefabs.Add(prefab);
                    prefabIds[prefab] = id;

                    foreach (SpriteRenderer sr in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        if (sr.sprite == null)
                            continue;
                        Bounds b = sr.sprite.bounds;
                        Vector3 min = sr.transform.TransformPoint(b.min) - prefab.transform.position;
                        Vector3 max = sr.transform.TransformPoint(b.max) - prefab.transform.position;
                        // Objects can be flipped, so the horizontal extent is symmetric.
                        float side = Mathf.Max(Mathf.Abs(min.x), Mathf.Abs(max.x));
                        left = Mathf.Max(left, side);
                        right = Mathf.Max(right, side);
                        up = Mathf.Max(up, max.y);
                        down = Mathf.Max(down, -min.y);
                    }
                }

                placed.Add(new StreamedMapData.PlacedObject
                {
                    Prefab = id,
                    Position = (Vector2)obj.localPosition + mapOffset,
                    FlipX = obj.localScale.x < 0f,
                });
            }
        }

        data.Prefabs = prefabs.ToArray();
        data.Objects = placed.ToArray();
        data.MaxExtentLeft = left;
        data.MaxExtentRight = right;
        data.MaxExtentUp = up;
        data.MaxExtentDown = down;

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        return data;
    }

    private static Tilemap BuildTileLayer(XmlElement layerEl, Transform parent, int mapW, int mapH, int halfW, int halfH,
        List<(int FirstGid, TsxTileset Tileset)> tilesets, Dictionary<uint, TileBase> tileCache, int sortingOrder)
    {
        var go = new GameObject(layerEl.GetAttribute("name"));
        go.transform.SetParent(parent, false);
        var tilemap = go.AddComponent<Tilemap>();
        var renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = sortingOrder;
        renderer.mode = TilemapRenderer.Mode.Chunk;

        uint[] gids = ReadLayerData((XmlElement)layerEl.SelectSingleNode("data"), mapW * mapH);
        var positions = new List<Vector3Int>(gids.Length);
        var tiles = new List<TileBase>(gids.Length);
        var flipped = new List<(Vector3Int Cell, Matrix4x4 Matrix)>();

        for (int i = 0; i < gids.Length; i++)
        {
            uint raw = gids[i];
            uint gid = raw & GidMask;
            if (gid == 0)
                continue;

            TileBase tile = GetTile(gid, tilesets, tileCache);
            if (tile == null)
                continue;

            var cell = new Vector3Int(i % mapW - halfW, halfH - 1 - i / mapW, 0);
            positions.Add(cell);
            tiles.Add(tile);

            if ((raw & (FlipHorizontal | FlipVertical | FlipDiagonal)) != 0)
                flipped.Add((cell, FlipMatrix(raw)));
        }

        tilemap.SetTiles(positions.ToArray(), tiles.ToArray());
        foreach ((Vector3Int cell, Matrix4x4 matrix) in flipped)
        {
            tilemap.SetTileFlags(cell, TileFlags.None);
            tilemap.SetTransformMatrix(cell, matrix);
        }

        tilemap.CompressBounds();
        return tilemap;
    }

    private static int BuildObjectLayer(XmlElement layerEl, Transform parent, int tileW, int tileH, int halfW, int halfH,
        List<(int FirstGid, TsxTileset Tileset)> tilesets, HashSet<string> missingPrefabs)
    {
        var layerRoot = new GameObject(layerEl.GetAttribute("name"));
        layerRoot.transform.SetParent(parent, false);
        var prefabCache = new Dictionary<string, GameObject>();
        int count = 0;

        foreach (XmlElement obj in layerEl.SelectNodes("object"))
        {
            float x = Float(obj, "x") / tileW - halfW;
            float y = halfH - Float(obj, "y") / tileH;
            float rotation = obj.HasAttribute("rotation") ? -Float(obj, "rotation") : 0f;
            string className = obj.HasAttribute("type") ? obj.GetAttribute("type") : obj.GetAttribute("class");
            GameObject instance = null;
            Vector2 feet = Vector2.zero;

            if (obj.HasAttribute("gid"))
            {
                uint raw = uint.Parse(obj.GetAttribute("gid"), CultureInfo.InvariantCulture);
                (int firstGid, TsxTileset ts) = FindTileset(raw & GidMask, tilesets);
                if (ts == null)
                    continue;

                int localId = (int)((raw & GidMask) - (uint)firstGid);
                if (ts.Tiles.TryGetValue(localId, out TsxTile tile))
                {
                    if (string.IsNullOrEmpty(className))
                        className = tile.Type;
                    // Tiled anchors the object at the rect's bottom-centre; the prefab pivot is the feet.
                    feet = FeetOffset(tile);
                    if ((raw & FlipHorizontal) != 0)
                        feet.x = -feet.x;
                }

                GameObject prefab = FindPrefab(className, prefabCache);
                if (prefab == null)
                {
                    missingPrefabs.Add(className);
                    continue;
                }

                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, layerRoot.transform);
                if ((raw & FlipHorizontal) != 0)
                {
                    Vector3 s = instance.transform.localScale;
                    instance.transform.localScale = new Vector3(-s.x, s.y, s.z);
                }
            }
            else if (obj.SelectSingleNode("point") != null)
            {
                string name = obj.GetAttribute("name");
                instance = new GameObject(string.IsNullOrEmpty(name) ? (string.IsNullOrEmpty(className) ? "Marker" : className) : name);
                instance.transform.SetParent(layerRoot.transform, false);
            }

            if (instance == null)
                continue;

            instance.transform.localPosition = new Vector3(x + feet.x, y + feet.y, 0f);
            instance.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            count++;
        }

        return count;
    }

    private static GameObject FindPrefab(string name, Dictionary<string, GameObject> cache)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (cache.TryGetValue(name, out GameObject cached))
            return cached;

        GameObject found = AssetDatabase.FindAssets($"{name} t:Prefab", new[] { PrefabsFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => System.IO.Path.GetFileNameWithoutExtension(p) == name)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .FirstOrDefault();
        cache[name] = found;
        return found;
    }

    private static TileBase GetTile(uint gid, List<(int FirstGid, TsxTileset Tileset)> tilesets, Dictionary<uint, TileBase> cache)
    {
        if (cache.TryGetValue(gid, out TileBase cached))
            return cached;

        (int firstGid, TsxTileset ts) = FindTileset(gid, tilesets);
        TileBase result = null;
        if (ts != null && !ts.IsCollection)
        {
            int localId = (int)(gid - (uint)firstGid);
            string spriteName = ts.SpriteName(localId);
            string folder = $"{TilesFolder}/{ts.Name}";
            string path = $"{folder}/{spriteName}.asset";
            ts.Tiles.TryGetValue(localId, out TsxTile info);
            // Blocking tilesets (water) collide with their sprite outline: a bank tile's land part stays walkable.
            Tile.ColliderType collider = ts.Blocking ? Tile.ColliderType.Sprite : Tile.ColliderType.None;

            if (info != null && info.Animation.Count > 1)
            {
                result = CreateOrUpdateAnimatedTile(ts, info, path, folder, collider);
            }
            else
            {
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                Sprite sprite = LoadSprite(ts.Image, spriteName);
                if (sprite == null)
                {
                    Debug.LogWarning($"[TiledMapTools] Sprite {spriteName} missing — run Tools > Map > Build {mapName} Assets.");
                }
                else if (tile == null)
                {
                    EnsureFolder(folder);
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.colliderType = collider;
                    AssetDatabase.CreateAsset(tile, path);
                }
                else if (tile.colliderType != collider)
                {
                    tile.colliderType = collider;
                    EditorUtility.SetDirty(tile);
                }

                result = tile;
            }

            if (result != null)
                tileCover[result] = info != null && info.Properties.ContainsKey("cover") ? info.PropFloat("cover") : 1f;
        }

        cache[gid] = result;
        return result;
    }

    /// <summary>Tile.cover of the last import, per tile asset (1 when the tileset gives none).</summary>
    private static readonly Dictionary<TileBase, float> tileCover = new();

    private static AnimatedTile CreateOrUpdateAnimatedTile(TsxTileset ts, TsxTile info, string path, string folder,
        Tile.ColliderType collider)
    {
        var sprites = new Sprite[info.Animation.Count];
        for (int i = 0; i < sprites.Length; i++)
        {
            sprites[i] = LoadSprite(ts.Image, ts.SpriteName(info.Animation[i].TileId));
            if (sprites[i] == null)
            {
                Debug.LogWarning($"[TiledMapTools] Animation frame {ts.SpriteName(info.Animation[i].TileId)} missing.");
                return null;
            }
        }

        var tile = AssetDatabase.LoadAssetAtPath<AnimatedTile>(path);
        if (tile == null)
        {
            EnsureFolder(folder);
            tile = ScriptableObject.CreateInstance<AnimatedTile>();
            AssetDatabase.CreateAsset(tile, path);
        }

        // Tiled frame duration (ms) → frames per second. Every tile starts at time 0 so neighbours stay in sync.
        float averageMs = (float)info.Animation.Average(f => Math.Max(1, f.DurationMs));
        tile.m_AnimatedSprites = sprites;
        tile.m_MinSpeed = tile.m_MaxSpeed = 1000f / averageMs;
        tile.m_AnimationStartTime = 0f;
        tile.m_AnimationStartFrame = 0;
        tile.m_TileColliderType = collider;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static bool BoolProperty(XmlElement element, string name)
    {
        foreach (XmlElement prop in element.SelectNodes("properties/property"))
        {
            if (prop.GetAttribute("name") == name)
                return prop.GetAttribute("value") == "true";
        }

        return false;
    }

    private static (int, TsxTileset) FindTileset(uint gid, List<(int FirstGid, TsxTileset Tileset)> tilesets)
    {
        for (int i = tilesets.Count - 1; i >= 0; i--)
        {
            if (gid >= tilesets[i].FirstGid)
                return tilesets[i];
        }

        return (0, null);
    }

    private static void AssignFlowFieldSource(MapStreamer streamer)
    {
        foreach (FlowFieldService service in UnityEngine.Object.FindObjectsByType<FlowFieldService>(FindObjectsSortMode.None))
        {
            var so = new SerializedObject(service);
            so.FindProperty("navigationSource").objectReferenceValue = streamer;
            so.FindProperty("navigationTileMap").objectReferenceValue = null;
            so.ApplyModifiedProperties();
            Debug.Log($"[TiledMapTools] FlowFieldService '{service.name}' now reads navigation from {streamer.name}.");
        }
    }

    // ───────────────────────────── Helpers ─────────────────────────────

    private static uint[] ReadLayerData(XmlElement data, int expected)
    {
        string encoding = data.GetAttribute("encoding");
        string compression = data.GetAttribute("compression");
        var result = new uint[expected];

        if (encoding == "csv")
        {
            string[] parts = data.InnerText.Split(new[] { ',', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < Math.Min(parts.Length, expected); i++)
                result[i] = uint.Parse(parts[i], CultureInfo.InvariantCulture);
            return result;
        }

        if (encoding == "base64")
        {
            byte[] bytes = Convert.FromBase64String(data.InnerText.Trim());
            if (compression == "zlib")
                bytes = Inflate(bytes, 2);
            else if (compression == "gzip")
                bytes = Gunzip(bytes);
            else if (!string.IsNullOrEmpty(compression))
                throw new NotSupportedException($"Tile layer compression '{compression}' — save the map with CSV layer format.");

            for (int i = 0; i < expected && i * 4 + 3 < bytes.Length; i++)
                result[i] = BitConverter.ToUInt32(bytes, i * 4);
            return result;
        }

        throw new NotSupportedException("XML tile layer encoding is not supported — save the map with CSV layer format.");
    }

    private static byte[] Inflate(byte[] bytes, int skip)
    {
        using var input = new MemoryStream(bytes, skip, bytes.Length - skip);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Gunzip(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static Matrix4x4 FlipMatrix(uint raw)
    {
        bool h = (raw & FlipHorizontal) != 0;
        bool v = (raw & FlipVertical) != 0;
        bool d = (raw & FlipDiagonal) != 0;
        Matrix4x4 m = Matrix4x4.identity;
        if (d)
            m = Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90)) * Matrix4x4.Scale(new Vector3(1, -1, 1));
        return Matrix4x4.Scale(new Vector3(h ? -1 : 1, v ? -1 : 1, 1)) * m;
    }

    private static Sprite LoadSprite(string texturePath, string spriteName) =>
        AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().FirstOrDefault(s => s.name == spriteName);

    private static byte[] ReadAlpha(string texturePath, out int width, out int height)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(texturePath));
        width = tex.width;
        height = tex.height;
        Color32[] pixels = tex.GetPixels32();
        UnityEngine.Object.DestroyImmediate(tex);

        // Texture rows are bottom-up; store top-down to match Tiled coordinates.
        var alpha = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                alpha[y * width + x] = pixels[(height - 1 - y) * width + x].a;
        }

        return alpha;
    }

    private static Vector2Int ReadSize(string texturePath)
    {
        var tex = new Texture2D(2, 2);
        tex.LoadImage(File.ReadAllBytes(texturePath));
        var size = new Vector2Int(tex.width, tex.height);
        UnityEngine.Object.DestroyImmediate(tex);
        return size;
    }

    private static bool HasPixels(byte[] alpha, int width, int height, RectInt rect)
    {
        for (int y = rect.y; y < Mathf.Min(height, rect.yMax); y++)
        {
            for (int x = rect.x; x < Mathf.Min(width, rect.xMax); x++)
            {
                if (alpha[y * width + x] > 0)
                    return true;
            }
        }

        return false;
    }

    private static string ToProjectPath(string baseDir, string relative)
    {
        string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, relative)).Replace('\\', '/');
        string project = System.IO.Path.GetFullPath(".").Replace('\\', '/') + "/";
        return full.StartsWith(project, StringComparison.OrdinalIgnoreCase) ? full.Substring(project.Length) : full;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }

    private static int Int(XmlElement el, string attr) =>
        el.HasAttribute(attr) ? int.Parse(el.GetAttribute(attr), CultureInfo.InvariantCulture) : 0;

    private static float Float(XmlElement el, string attr) =>
        el.HasAttribute(attr) ? float.Parse(el.GetAttribute(attr), CultureInfo.InvariantCulture) : 0f;
}
