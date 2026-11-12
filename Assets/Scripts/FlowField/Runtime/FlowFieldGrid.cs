using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Navigation grid (axis-aligned, rectangular) used by the flow field. It is defined by a world origin
/// (bottom-left corner of local cell 0,0), a cell size and a cell count, so it can come from a Tilemap
/// or straight from map data (streamed maps have no full-size tilemap at runtime).
/// </summary>
public sealed class FlowFieldGrid
{
    private static readonly int[] CardinalX = { 0, 1, 0, -1 };
    private static readonly int[] CardinalY = { 1, 0, -1, 0 };

    public const byte WalkableCost = 1;
    public const byte BlockedCost = byte.MaxValue;
    public const int UnreachableCost = int.MaxValue;

    /// <summary>Cell coordinates covered by the grid (tilemap cells, or 0..Width/Height for data grids).</summary>
    public BoundsInt Bounds { get; }

    public int Width { get; }
    public int Height { get; }
    public int CellCount { get; }

    /// <summary>World position of the bottom-left corner of local cell (0, 0).</summary>
    public Vector2 Origin { get; }
    public Vector2 CellSize { get; }

    public byte[] Costs { get; }
    public int[] Integration { get; }
    public Vector2[] Directions { get; }

    /// <summary>
    /// When set, the grid is a torus on that axis: positions outside the grid map back into it and
    /// neighbours across the edge are connected. Used by looping maps.
    /// </summary>
    public bool WrapX { get; }
    public bool WrapY { get; }

    /// <summary>World-space size of one full map repeat.</summary>
    public Vector2 Period { get; }

    private readonly int[] nearestCellQueue;
    private readonly bool[] nearestCellVisited;
    private readonly List<int> nearestCellTouched = new();

    public FlowFieldGrid(Tilemap tilemap, bool wrapX = false, bool wrapY = false)
        : this(
            tilemap.CellToWorld(tilemap.cellBounds.min),
            tilemap.layoutGrid != null ? (Vector2)tilemap.layoutGrid.cellSize : Vector2.one,
            tilemap.cellBounds,
            wrapX,
            wrapY
        )
    {
    }

    public FlowFieldGrid(Vector2 origin, Vector2 cellSize, int width, int height, bool wrapX = false, bool wrapY = false)
        : this(origin, cellSize, new BoundsInt(0, 0, 0, width, height, 1), wrapX, wrapY)
    {
    }

    private FlowFieldGrid(Vector2 origin, Vector2 cellSize, BoundsInt bounds, bool wrapX, bool wrapY)
    {
        Origin = origin;
        CellSize = cellSize;
        Bounds = bounds;
        Width = bounds.size.x;
        Height = bounds.size.y;
        WrapX = wrapX;
        WrapY = wrapY;
        Period = new Vector2(Width * cellSize.x, Height * cellSize.y);
        CellCount = Width * Height;

        Costs = new byte[CellCount];
        Integration = new int[CellCount];
        Directions = new Vector2[CellCount];
        nearestCellQueue = new int[CellCount];
        nearestCellVisited = new bool[CellCount];

        Array.Fill(Costs, WalkableCost);
        Array.Fill(Integration, UnreachableCost);
    }

    public bool IsValidIndex(int index)
    {
        return index >= 0 && index < CellCount;
    }

    /// <summary>Unwrapped local cell of a world position (may be outside the grid).</summary>
    public Vector2Int WorldToLocalCell(Vector2 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt((worldPosition.x - Origin.x) / CellSize.x),
            Mathf.FloorToInt((worldPosition.y - Origin.y) / CellSize.y)
        );
    }

    public bool TryWorldToIndex(Vector3 worldPosition, out int index)
    {
        Vector2Int local = WorldToLocalCell(worldPosition);
        return TryGetIndex(local.x, local.y, out index);
    }

    public Vector3 IndexToWorldCenter(int index)
    {
        int localX = index % Width;
        int localY = index / Width;
        return new Vector3(Origin.x + (localX + 0.5f) * CellSize.x, Origin.y + (localY + 0.5f) * CellSize.y, 0f);
    }

    /// <summary>
    /// Centre of the cell, taken from the map repeat closest to <paramref name="near"/>. On a
    /// non-wrapping grid this is the same as IndexToWorldCenter.
    /// </summary>
    public Vector3 IndexToWorldCenterNear(int index, Vector2 near)
    {
        Vector3 center = IndexToWorldCenter(index);

        if (WrapX)
            center.x += Mathf.Round((near.x - center.x) / Period.x) * Period.x;
        if (WrapY)
            center.y += Mathf.Round((near.y - center.y) / Period.y) * Period.y;

        return center;
    }

    public bool IsWalkable(int index)
    {
        return (uint)index < (uint)CellCount && Costs[index] != BlockedCost;
    }

    public bool TryGetIndex(int localX, int localY, out int index)
    {
        if (WrapX && (uint)localX >= (uint)Width)
            localX = ((localX % Width) + Width) % Width;
        if (WrapY && (uint)localY >= (uint)Height)
            localY = ((localY % Height) + Height) % Height;

        if ((uint)localX >= (uint)Width || (uint)localY >= (uint)Height)
        {
            index = -1;
            return false;
        }

        index = localX + localY * Width;
        return true;
    }

    public void IndexToLocal(int index, out int localX, out int localY)
    {
        localX = index % Width;
        localY = index / Width;
    }

    public bool TryFindNearestWalkableCell(int startIndex, out int walkableIndex)
    {
        walkableIndex = -1;

        if (!IsValidIndex(startIndex))
            return false;

        // Only the cells visited by this search are reset, never the whole grid.
        for (int i = 0; i < nearestCellTouched.Count; i++)
            nearestCellVisited[nearestCellTouched[i]] = false;
        nearestCellTouched.Clear();

        int head = 0;
        int tail = 0;
        nearestCellVisited[startIndex] = true;
        nearestCellTouched.Add(startIndex);
        nearestCellQueue[tail++] = startIndex;

        while (head < tail)
        {
            int currentIndex = nearestCellQueue[head++];

            if (IsWalkable(currentIndex))
            {
                walkableIndex = currentIndex;
                return true;
            }

            IndexToLocal(currentIndex, out int currentX, out int currentY);

            for (int k = 0; k < 4; k++)
            {
                if (!TryGetIndex(currentX + CardinalX[k], currentY + CardinalY[k], out int neighbourIndex))
                    continue;

                if (nearestCellVisited[neighbourIndex])
                    continue;

                nearestCellVisited[neighbourIndex] = true;
                nearestCellTouched.Add(neighbourIndex);
                nearestCellQueue[tail++] = neighbourIndex;
            }
        }

        return false;
    }
}
