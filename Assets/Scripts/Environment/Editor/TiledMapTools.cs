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
/// Tiled (.tmx/.tsx) → Unity pipeline for Map_01.
///
/// 1. Tools > Map > Build Map_01 Assets: slices the sprite sheets referenced by the Tiled tilesets
///    (grid tilesets per 32 px cell, the Obstacles collection per tile sub-rectangle), then creates
///    one prefab per obstacle/decal tile. Existing prefabs are never overwritten, so hand-tuned
///    colliders and sprites survive a rebuild.
/// 2. Tools > Map > Import Map_01 Into Scene: rebuilds the "Map_01" object in the open scene from the
///    .tmx — tile layers become Tilemaps, tile objects become prefab instances, point objects become
///    markers — and points FlowFieldService at the Ground tilemap.
///
/// Conventions: 32 px = 1 unit = 1 cell, map centred on the world origin, obstacle prefab pivot is
/// the bottom-centre of the sprite (the feet) and only the feet carry a collider.
/// </summary>
public static class TiledMapTools
{
    private const string MapRoot = "Assets/GameContent/Environment/Map_01";
    private const string TiledFolder = MapRoot + "/Tiled";
    private const string MapFile = TiledFolder + "/Map_01.tmx";
    private const string TilesFolder = MapRoot + "/Tiles";
    private const string PrefabsFolder = MapRoot + "/Prefabs";

    private const int PixelsPerUnit = 32;
    private const string ObstacleLayerName = "Ostacle";

    private const int GroundSortingOrder = -100;
    private const int DecalSortingOrder = -60;
    private const int ShadowSortingOrder = -50;

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

        foreach (XmlElement tileEl in root.SelectNodes("tile"))
        {
            var tile = new TsxTile
            {
                Id = Int(tileEl, "id"),
                Type = tileEl.HasAttribute("type") ? tileEl.GetAttribute("type") : tileEl.GetAttribute("class"),
            };

            foreach (XmlElement prop in tileEl.SelectNodes("properties/property"))
                tile.Properties[prop.GetAttribute("name")] = prop.GetAttribute("value");

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
    public static void BuildAssets()
    {
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
        var pivots = tiles.Select(t => t.Prop("kind") == "shadow" ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f)).ToList();
        ApplySprites(sheetPath, tiles.Select(t => t.Rect).ToList(), tiles.Select(t => t.Type).ToList(), pivots);
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

            if (kind == "decal")
            {
                renderer.sortingOrder = DecalSortingOrder;
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

    [MenuItem("Tools/Map/Import Map_01 Into Scene")]
    public static void ImportMap()
    {
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

        string mapName = System.IO.Path.GetFileNameWithoutExtension(MapFile);
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
        var previewTilemaps = new List<Tilemap>();
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
        StreamedMapData data = BakeStreamedData(mapName, preview.transform, previewTilemaps, mapW, mapH, halfW, halfH,
            cellSize, wrapX, wrapY);

        // 3. Runtime map: small window tilemaps + pooled objects around the camera.
        var mapRoot = new GameObject(mapName);
        Undo.RegisterCreatedObjectUndo(mapRoot, "Import Tiled map");
        mapRoot.AddComponent<Grid>().cellSize = cellSize;

        var windows = new List<Tilemap>();
        foreach (Tilemap source in previewTilemaps)
        {
            var layer = new GameObject(source.name);
            layer.transform.SetParent(mapRoot.transform, false);
            var tilemap = layer.AddComponent<Tilemap>();
            var renderer = layer.AddComponent<TilemapRenderer>();
            var sourceRenderer = source.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = sourceRenderer.sortingOrder;
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.mode = TilemapRenderer.Mode.Chunk;
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
            Debug.LogWarning($"[TiledMapTools] No prefab named '{missing}' — run Tools > Map > Build Map_01 Assets.");

        Debug.Log($"[TiledMapTools] Imported {mapName}: {mapW}x{mapH} tiles, {instanced} object(s), " +
            $"{data.Prefabs.Length} prefab type(s), wrap X={wrapX} Y={wrapY}.");
        Selection.activeGameObject = mapRoot;
    }

    private static StreamedMapData BakeStreamedData(string mapName, Transform preview, List<Tilemap> tilemaps,
        int mapW, int mapH, int halfW, int halfH, Vector3 cellSize, bool wrapX, bool wrapY)
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

            if (obj.HasAttribute("gid"))
            {
                uint raw = uint.Parse(obj.GetAttribute("gid"), CultureInfo.InvariantCulture);
                (int firstGid, TsxTileset ts) = FindTileset(raw & GidMask, tilesets);
                if (ts == null)
                    continue;

                int localId = (int)((raw & GidMask) - (uint)firstGid);
                if (string.IsNullOrEmpty(className) && ts.Tiles.TryGetValue(localId, out TsxTile tile))
                    className = tile.Type;

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

            instance.transform.localPosition = new Vector3(x, y, 0f);
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
            result = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (result == null)
            {
                Sprite sprite = LoadSprite(ts.Image, spriteName);
                if (sprite != null)
                {
                    EnsureFolder(folder);
                    var tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.colliderType = Tile.ColliderType.None;
                    AssetDatabase.CreateAsset(tile, path);
                    result = tile;
                }
                else
                {
                    Debug.LogWarning($"[TiledMapTools] Sprite {spriteName} missing — run Tools > Map > Build Map_01 Assets.");
                }
            }
        }

        cache[gid] = result;
        return result;
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
