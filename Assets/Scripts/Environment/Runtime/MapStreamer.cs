using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Endless map streaming (HoloCure style, chunked).
///
/// The map is baked into a StreamedMapData asset. At runtime only the chunks overlapping the camera
/// view (plus a small margin) exist: tiles are written into small "window" tilemaps and objects are
/// taken from per-prefab pools, so the number of live renderers/colliders stays at roughly one screen's
/// worth no matter how big the map is. The map repeats on the wrapped axes: chunk (cx, cy) shows base
/// chunk (cx mod N, cy mod M), and everything keeps real world coordinates (nothing is teleported).
///
/// Also provides navigation data to FlowFieldService (grid + obstacles baked from data), so the flow
/// field covers the whole map even though colliders only exist near the camera.
/// </summary>
[DefaultExecutionOrder(-200)] // Data must be ready before FlowFieldService.Awake.
[RequireComponent(typeof(Grid))]
public sealed class MapStreamer : MonoBehaviour, IFlowFieldMapSource
{
    [SerializeField] private StreamedMapData data;
    [Tooltip("Camera whose view decides which chunks are alive. Defaults to Camera.main.")]
    [SerializeField] private Camera viewCamera;

    [Header("Scene parts (set by the importer)")]
    [Tooltip("One window tilemap per data tile layer, same order.")]
    [SerializeField] private Tilemap[] windowTilemaps = Array.Empty<Tilemap>();
    [SerializeField] private Transform objectsRoot;
    [Tooltip("Full-map preview shown in the editor; disabled when the game runs (EditorOnly, stripped from builds).")]
    [SerializeField] private GameObject editorPreview;

    [Header("Streaming")]
    [Tooltip("Chunk size in cells. Must divide the map size (adjusted automatically if not).")]
    [SerializeField, Min(2)] private int chunkSize = 8;
    [Tooltip("Extra world units kept alive around the camera view (tiles and objects).")]
    [SerializeField, Min(0f)] private float viewMargin = 1f;
    [Tooltip("Tile chunks this far beyond the view are loaded ahead of time, a few per frame.")]
    [SerializeField, Min(0f)] private float prefetchDistance = 4f;
    [Tooltip("Max tile chunk writes (load or clear) per frame for prefetched chunks. " +
        "Chunks already inside the view are always written immediately.")]
    [SerializeField, Min(1)] private int tileChunkBudgetPerFrame = 3;

    private int cs;
    private int chunksX;
    private int chunksY;
    private Vector2 chunkWorld;
    private Vector2 mapOrigin;
    private Vector3Int originCell;

    private TileBase[] tileBuffer;
    private TileBase[] emptyBuffer;
    private List<int>[] objectsByChunk;
    private Stack<GameObject>[] pools;

    private readonly HashSet<long> liveTileChunks = new();
    private readonly Dictionary<long, List<GameObject>> liveObjectChunks = new();
    private readonly Stack<List<GameObject>> listPool = new();
    private readonly List<long> scratch = new();
    private readonly List<long> pendingTileLoads = new();
    private readonly List<long> pendingTileUnloads = new();
    private readonly Dictionary<GameObject, ushort> prefabIds = new();

    private RectInt lastTileRange = new RectInt(int.MinValue, 0, 0, 0);
    private RectInt lastObjectRange = new RectInt(int.MinValue, 0, 0, 0);

    public StreamedMapData Data => data;

    // ───────────────────────── IFlowFieldMapSource ─────────────────────────

    public Vector2 GridOrigin => mapOrigin;
    public Vector2 GridCellSize => data.CellSize;
    public int GridWidth => data.Width;
    public int GridHeight => data.Height;
    public bool WrapX => data.WrapX;
    public bool WrapY => data.WrapY;

    private void Awake()
    {
        if (data == null || data.Width <= 0 || data.Height <= 0)
        {
            Debug.LogError("MapStreamer has no map data.", this);
            enabled = false;
            return;
        }

        if (editorPreview != null)
            editorPreview.SetActive(false);

        if (viewCamera == null)
            viewCamera = Camera.main;
        if (objectsRoot == null)
            objectsRoot = transform;

        cs = Mathf.Clamp(chunkSize, 1, Mathf.Min(data.Width, data.Height));
        while (data.Width % cs != 0 || data.Height % cs != 0)
            cs--;

        chunksX = data.Width / cs;
        chunksY = data.Height / cs;
        chunkWorld = new Vector2(cs * data.CellSize.x, cs * data.CellSize.y);
        originCell = new Vector3Int(-data.Width / 2, -data.Height / 2, 0);
        mapOrigin = (Vector2)transform.position + new Vector2(originCell.x * data.CellSize.x, originCell.y * data.CellSize.y);

        tileBuffer = new TileBase[cs * cs];
        emptyBuffer = new TileBase[cs * cs];

        objectsByChunk = new List<int>[chunksX * chunksY];
        for (int i = 0; i < data.Objects.Length; i++)
        {
            Vector2 p = data.Objects[i].Position;
            int bx = Mathf.Clamp(Mathf.FloorToInt(p.x / chunkWorld.x), 0, chunksX - 1);
            int by = Mathf.Clamp(Mathf.FloorToInt(p.y / chunkWorld.y), 0, chunksY - 1);
            int key = bx + by * chunksX;
            (objectsByChunk[key] ??= new List<int>()).Add(i);
        }

        pools = new Stack<GameObject>[data.Prefabs.Length];
        for (int i = 0; i < pools.Length; i++)
            pools[i] = new Stack<GameObject>();
    }

    private void Start()
    {
        UpdateStreaming();
    }

    private void LateUpdate()
    {
        UpdateStreaming();
    }

    // ───────────────────────── Streaming ─────────────────────────

    private void UpdateStreaming()
    {
        // Each character carries its own camera: follow whichever one is rendering.
        if (viewCamera == null || !viewCamera.isActiveAndEnabled)
        {
            viewCamera = Camera.main;
            if (viewCamera == null)
                return;
        }

        Vector3 c = viewCamera.transform.position;
        float halfH = viewCamera.orthographicSize;
        float halfW = halfH * viewCamera.aspect;
        var view = Rect.MinMaxRect(c.x - halfW - viewMargin, c.y - halfH - viewMargin, c.x + halfW + viewMargin, c.y + halfH + viewMargin);

        var prefetchView = Rect.MinMaxRect(view.xMin - prefetchDistance, view.yMin - prefetchDistance,
            view.xMax + prefetchDistance, view.yMax + prefetchDistance);
        RectInt tileRange = ChunkRange(prefetchView);
        if (!tileRange.Equals(lastTileRange))
        {
            lastTileRange = tileRange;
            PlanTiles(tileRange);
        }

        ProcessTileQueue(ChunkRange(view));

        // An object is visible when its sprite (pivot ± extents) touches the view, so widen the pivot area.
        var objectView = Rect.MinMaxRect(
            view.xMin - data.MaxExtentRight, view.yMin - data.MaxExtentUp,
            view.xMax + data.MaxExtentLeft, view.yMax + data.MaxExtentDown);
        RectInt objectRange = ChunkRange(objectView);
        if (!objectRange.Equals(lastObjectRange))
        {
            lastObjectRange = objectRange;
            SyncObjects(objectRange);
        }
    }

    /// <summary>Inclusive chunk range (in unwrapped chunk coordinates) covering the rect.</summary>
    private RectInt ChunkRange(Rect rect)
    {
        int x0 = Mathf.FloorToInt((rect.xMin - mapOrigin.x) / chunkWorld.x);
        int y0 = Mathf.FloorToInt((rect.yMin - mapOrigin.y) / chunkWorld.y);
        int x1 = Mathf.FloorToInt((rect.xMax - mapOrigin.x) / chunkWorld.x);
        int y1 = Mathf.FloorToInt((rect.yMax - mapOrigin.y) / chunkWorld.y);

        if (!data.WrapX)
        {
            x0 = Mathf.Max(x0, 0);
            x1 = Mathf.Min(x1, chunksX - 1);
        }

        if (!data.WrapY)
        {
            y0 = Mathf.Max(y0, 0);
            y1 = Mathf.Min(y1, chunksY - 1);
        }

        return new RectInt(x0, y0, x1 - x0, y1 - y0); // width/height used as inclusive extents
    }

    private static bool InRange(RectInt r, int x, int y) =>
        x >= r.x && x <= r.x + r.width && y >= r.y && y <= r.y + r.height;

    private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
    private static int KeyX(long key) => (int)(key >> 32);
    private static int KeyY(long key) => (int)(uint)key;

    private int BaseChunk(int chunk, int count) => ((chunk % count) + count) % count;

    /// <summary>Queues the tile chunk loads/clears needed for the new range (nearest to the view first).</summary>
    private void PlanTiles(RectInt range)
    {
        pendingTileUnloads.Clear();
        foreach (long key in liveTileChunks)
        {
            if (!InRange(range, KeyX(key), KeyY(key)))
                pendingTileUnloads.Add(key);
        }

        pendingTileLoads.Clear();
        for (int y = range.y; y <= range.y + range.height; y++)
        {
            for (int x = range.x; x <= range.x + range.width; x++)
            {
                long key = Key(x, y);
                if (!liveTileChunks.Contains(key))
                    pendingTileLoads.Add(key);
            }
        }

        // Centre of the range first, so the chunks the camera is heading into come before the corners.
        float cx = range.x + range.width * 0.5f;
        float cy = range.y + range.height * 0.5f;
        pendingTileLoads.Sort((a, b) =>
        {
            float da = Mathf.Abs(KeyX(a) - cx) + Mathf.Abs(KeyY(a) - cy);
            float db = Mathf.Abs(KeyX(b) - cx) + Mathf.Abs(KeyY(b) - cy);
            return da.CompareTo(db);
        });
    }

    private void ProcessTileQueue(RectInt visibleRange)
    {
        int budget = tileChunkBudgetPerFrame;

        // Loads: anything already on screen is written now, prefetch chunks within the budget.
        for (int i = 0; i < pendingTileLoads.Count; i++)
        {
            long key = pendingTileLoads[i];
            bool urgent = InRange(visibleRange, KeyX(key), KeyY(key));
            if (!urgent && budget <= 0)
                continue;

            if (liveTileChunks.Add(key))
            {
                WriteTileChunk(KeyX(key), KeyY(key), clear: false);
                if (!urgent)
                    budget--;
            }

            pendingTileLoads.RemoveAt(i--);
        }

        // Clears are never urgent: a stale chunk outside the view is invisible.
        while (budget > 0 && pendingTileUnloads.Count > 0)
        {
            long key = pendingTileUnloads[pendingTileUnloads.Count - 1];
            pendingTileUnloads.RemoveAt(pendingTileUnloads.Count - 1);
            if (liveTileChunks.Remove(key))
            {
                WriteTileChunk(KeyX(key), KeyY(key), clear: true);
                budget--;
            }
        }
    }

    private void WriteTileChunk(int cx, int cy, bool clear)
    {
        var block = new BoundsInt(originCell.x + cx * cs, originCell.y + cy * cs, 0, cs, cs, 1);
        int bx = BaseChunk(cx, chunksX) * cs;
        int by = BaseChunk(cy, chunksY) * cs;

        for (int layer = 0; layer < windowTilemaps.Length && layer < data.TileLayers.Length; layer++)
        {
            Tilemap tilemap = windowTilemaps[layer];
            if (tilemap == null)
                continue;

            if (clear)
            {
                tilemap.SetTilesBlock(block, emptyBuffer);
                continue;
            }

            StreamedMapData.TileLayer source = data.TileLayers[layer];
            for (int y = 0; y < cs; y++)
            {
                int row = (by + y) * data.Width + bx;
                for (int x = 0; x < cs; x++)
                {
                    int value = source.Cells[row + x];
                    tileBuffer[x + y * cs] = value == 0 ? null : source.Palette[value - 1];
                }
            }

            tilemap.SetTilesBlock(block, tileBuffer);
        }
    }

    private void SyncObjects(RectInt range)
    {
        scratch.Clear();
        foreach (long key in liveObjectChunks.Keys)
        {
            if (!InRange(range, KeyX(key), KeyY(key)))
                scratch.Add(key);
        }

        foreach (long key in scratch)
        {
            List<GameObject> instances = liveObjectChunks[key];
            liveObjectChunks.Remove(key);
            ReleaseInstances(instances);
        }

        for (int y = range.y; y <= range.y + range.height; y++)
        {
            for (int x = range.x; x <= range.x + range.width; x++)
            {
                long key = Key(x, y);
                if (!liveObjectChunks.ContainsKey(key))
                    liveObjectChunks.Add(key, SpawnChunk(x, y));
            }
        }
    }

    private List<GameObject> SpawnChunk(int cx, int cy)
    {
        List<GameObject> instances = listPool.Count > 0 ? listPool.Pop() : new List<GameObject>(8);
        List<int> records = objectsByChunk[BaseChunk(cx, chunksX) + BaseChunk(cy, chunksY) * chunksX];
        if (records == null)
            return instances;

        // Offset of this repeat of the map (whole periods).
        Vector2 repeatOffset = new Vector2(
            Mathf.Floor((float)cx / chunksX) * data.WorldSize.x,
            Mathf.Floor((float)cy / chunksY) * data.WorldSize.y);

        for (int i = 0; i < records.Count; i++)
        {
            StreamedMapData.PlacedObject record = data.Objects[records[i]];
            GameObject prefab = data.Prefabs[record.Prefab];
            if (prefab == null)
                continue;

            Stack<GameObject> pool = pools[record.Prefab];
            GameObject instance;
            if (pool.Count > 0)
            {
                instance = pool.Pop();
            }
            else
            {
                instance = Instantiate(prefab, objectsRoot);
                DecorOcclusionFader.AttachIfOccluder(instance); // tall objects fade when someone is behind them
            }

            Transform t = instance.transform;
            Vector2 p = mapOrigin + repeatOffset + record.Position;
            t.SetPositionAndRotation(new Vector3(p.x, p.y, 0f), Quaternion.identity);
            Vector3 scale = prefab.transform.localScale;
            t.localScale = record.FlipX ? new Vector3(-scale.x, scale.y, scale.z) : scale;
            instance.SetActive(true);
            instances.Add(instance);
            prefabIds[instance] = record.Prefab;
        }

        return instances;
    }

    private void ReleaseInstances(List<GameObject> instances)
    {
        for (int i = 0; i < instances.Count; i++)
        {
            GameObject instance = instances[i];
            if (instance == null)
                continue;

            instance.SetActive(false);
            if (prefabIds.TryGetValue(instance, out ushort id))
                pools[id].Push(instance);
        }

        instances.Clear();
        listPool.Push(instances);
    }

    /// <summary>Number of pooled map objects currently alive (for profiling/debug).</summary>
    public int LiveObjectCount
    {
        get
        {
            int count = 0;
            foreach (List<GameObject> list in liveObjectChunks.Values)
                count += list.Count;
            return count;
        }
    }

    // ───────────────────────── Navigation baking ─────────────────────────

    public void BakeObstacles(FlowFieldGrid grid, LayerMask obstacleMask, Vector2 agentSize)
    {
        Array.Fill(grid.Costs, FlowFieldGrid.WalkableCost);

        var footprints = new List<Rect>[data.Prefabs.Length];
        Vector2 cell = data.CellSize;
        Vector2 half = agentSize * 0.5f;

        foreach (StreamedMapData.PlacedObject record in data.Objects)
        {
            List<Rect> rects = footprints[record.Prefab] ??= CollectFootprints(data.Prefabs[record.Prefab], obstacleMask);

            foreach (Rect r in rects)
            {
                float xMin = record.FlipX ? -r.xMax : r.xMin;
                float xMax = record.FlipX ? -r.xMin : r.xMax;

                // Cells whose centre is within the footprint grown by half the agent are blocked
                // (same rule as overlapping the agent capsule with the collider, slightly conservative).
                float minX = record.Position.x + xMin - half.x;
                float maxX = record.Position.x + xMax + half.x;
                float minY = record.Position.y + r.yMin - half.y;
                float maxY = record.Position.y + r.yMax + half.y;

                int x0 = Mathf.CeilToInt(minX / cell.x - 0.5f);
                int x1 = Mathf.FloorToInt(maxX / cell.x - 0.5f);
                int y0 = Mathf.CeilToInt(minY / cell.y - 0.5f);
                int y1 = Mathf.FloorToInt(maxY / cell.y - 0.5f);

                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        if (grid.TryGetIndex(x, y, out int index))
                            grid.Costs[index] = FlowFieldGrid.BlockedCost;
                    }
                }
            }
        }
    }

    /// <summary>Solid collider footprints of a prefab, as rects relative to its pivot.</summary>
    private static List<Rect> CollectFootprints(GameObject prefab, LayerMask mask)
    {
        var rects = new List<Rect>();
        if (prefab == null)
            return rects;

        Transform root = prefab.transform;
        foreach (Collider2D col in prefab.GetComponentsInChildren<Collider2D>(true))
        {
            if (col.isTrigger || !col.enabled || (mask.value & (1 << col.gameObject.layer)) == 0)
                continue;

            Vector2 offset = col.offset;
            Vector2 size;
            switch (col)
            {
                case BoxCollider2D box:
                    size = box.size + Vector2.one * (box.edgeRadius * 2f);
                    break;
                case CircleCollider2D circle:
                    size = Vector2.one * (circle.radius * 2f);
                    break;
                case CapsuleCollider2D capsule:
                    size = capsule.size;
                    break;
                case PolygonCollider2D polygon when polygon.points.Length > 0:
                    Vector2 min = polygon.points[0], max = polygon.points[0];
                    foreach (Vector2 p in polygon.points)
                    {
                        min = Vector2.Min(min, p);
                        max = Vector2.Max(max, p);
                    }
                    offset += (min + max) * 0.5f;
                    size = max - min;
                    break;
                default:
                    continue;
            }

            // World-sized offset from the prefab pivot (instances keep the prefab's scale; rotation ignored,
            // map obstacles are axis-aligned).
            Vector3 lossy = col.transform.lossyScale;
            Vector2 center = col.transform.TransformPoint(offset) - root.position;
            Vector2 scaled = Vector2.Scale(size, new Vector2(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y)));
            rects.Add(new Rect(center - scaled * 0.5f, scaled));
        }

        return rects;
    }
}
