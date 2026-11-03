using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class FlowFieldGrid
{
    private static readonly Vector2Int[] CardinalOffsets =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
    };

    public const byte WalkableCost = 1;
    public const byte BlockedCost = byte.MaxValue;
    public const int UnreachableCost = int.MaxValue;

    public Tilemap Tilemap { get; }

    public BoundsInt Bounds { get; private set; }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public int CellCount { get; private set; }

    public byte[] Costs { get; private set; }
    public int[] Integration { get; private set; }
    public Vector2[] Directions { get; private set; }

    private readonly Queue<int> nearestCellQueue;
    private readonly bool[] nearestCellVisited;

    public FlowFieldGrid(Tilemap tilemap)
    {
        Tilemap = tilemap;

        Bounds = tilemap.cellBounds;
        Width = Bounds.size.x;
        Height = Bounds.size.y;
        CellCount = Width * Height;

        Costs = new byte[CellCount];
        Integration = new int[CellCount];
        Directions = new Vector2[CellCount];

        nearestCellQueue = new Queue<int>();
        nearestCellVisited = new bool[CellCount];

        Array.Fill(Costs, WalkableCost);
        Array.Fill(Integration, UnreachableCost);
        Array.Fill(Directions, Vector2.zero);
    }

    public bool ContainsCell(Vector3Int cell)
    {
        return cell.x >= Bounds.xMin
            && cell.x < Bounds.xMax
            && cell.y >= Bounds.yMin
            && cell.y < Bounds.yMax;
    }

    public bool IsValidIndex(int index)
    {
        return index >= 0 && index < CellCount;
    }

    public int CellToIndex(Vector3Int cell)
    {
        int localX = cell.x - Bounds.xMin;
        int localY = cell.y - Bounds.yMin;

        return localX + localY * Width;
    }

    public Vector3Int IndexToCell(int index)
    {
        int localX = index % Width;
        int localY = index / Width;

        return new Vector3Int(localX + Bounds.xMin, localY + Bounds.yMin, 0);
    }

    public bool TryWorldToIndex(Vector3 worldPosition, out int index)
    {
        Vector3Int cell = Tilemap.WorldToCell(worldPosition);

        if (!ContainsCell(cell))
        {
            index = -1;
            return false;
        }

        index = CellToIndex(cell);
        return true;
    }

    public Vector3 IndexToWorldCenter(int index)
    {
        Vector3Int cell = IndexToCell(index);
        return Tilemap.GetCellCenterWorld(cell);
    }

    public bool IsWalkable(int index)
    {
        return IsValidIndex(index) && Costs[index] != BlockedCost;
    }

    public bool TryGetIndex(int localX, int localY, out int index)
    {
        if (localX < 0 || localX >= Width || localY < 0 || localY >= Height)
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
        {
            return false;
        }

        nearestCellQueue.Clear();
        Array.Clear(nearestCellVisited, 0, nearestCellVisited.Length);

        nearestCellVisited[startIndex] = true;
        nearestCellQueue.Enqueue(startIndex);

        while (nearestCellQueue.Count > 0)
        {
            int currentIndex = nearestCellQueue.Dequeue();

            if (IsWalkable(currentIndex))
            {
                walkableIndex = currentIndex;
                return true;
            }

            IndexToLocal(currentIndex, out int currentX, out int currentY);

            foreach (Vector2Int offset in CardinalOffsets)
            {
                int neighbourX = currentX + offset.x;
                int neighbourY = currentY + offset.y;

                if (!TryGetIndex(neighbourX, neighbourY, out int neighbourIndex))
                {
                    continue;
                }

                if (nearestCellVisited[neighbourIndex])
                {
                    continue;
                }

                nearestCellVisited[neighbourIndex] = true;
                nearestCellQueue.Enqueue(neighbourIndex);
            }
        }

        return false;
    }
}
