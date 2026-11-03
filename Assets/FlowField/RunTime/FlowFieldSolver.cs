using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class FlowFieldSolver
{
    private static readonly Vector2Int[] CardinalOffsets =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
    };

    private static readonly Vector2Int[] AllOffsets =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 1),
        new Vector2Int(1, 0),
        new Vector2Int(1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(-1, 1),
    };

    private readonly FlowFieldGrid grid;
    private readonly Queue<int> openCells;

    public int TargetIndex { get; private set; } = -1;

    public FlowFieldSolver(FlowFieldGrid grid)
    {
        this.grid = grid;

        openCells = new Queue<int>(grid.CellCount);
    }

    public bool Build(Vector3 targetWorldPosition)
    {
        if (!grid.TryWorldToIndex(targetWorldPosition, out int targetIndex))
        {
            ClearResult();
            return false;
        }

        if (!grid.IsWalkable(targetIndex))
        {
            if (!grid.TryFindNearestWalkableCell(targetIndex, out targetIndex))
            {
                ClearResult();
                return false;
            }
        }

        TargetIndex = targetIndex;

        BuildIntegrationField();
        BuildDirectionField();

        return true;
    }

    private void BuildIntegrationField()
    {
        Array.Fill(grid.Integration, FlowFieldGrid.UnreachableCost);

        openCells.Clear();

        grid.Integration[TargetIndex] = 0;
        openCells.Enqueue(TargetIndex);

        while (openCells.Count > 0)
        {
            int currentIndex = openCells.Dequeue();
            int currentCost = grid.Integration[currentIndex];

            grid.IndexToLocal(currentIndex, out int currentX, out int currentY);

            foreach (Vector2Int offset in CardinalOffsets)
            {
                int neighborX = currentX + offset.x;
                int neighborY = currentY + offset.y;

                if (!grid.TryGetIndex(neighborX, neighborY, out int neighborIndex))
                {
                    continue;
                }

                if (!grid.IsWalkable(neighborIndex))
                {
                    continue;
                }

                if (grid.Integration[neighborIndex] != FlowFieldGrid.UnreachableCost)
                {
                    continue;
                }

                grid.Integration[neighborIndex] = currentCost + 1;

                openCells.Enqueue(neighborIndex);
            }
        }
    }

    private void BuildDirectionField()
    {
        Array.Fill(grid.Directions, Vector2.zero);

        for (int currentIndex = 0; currentIndex < grid.CellCount; ++currentIndex)
        {
            if (!grid.IsWalkable(currentIndex))
            {
                continue;
            }

            int currentIntergration = grid.Integration[currentIndex];

            if (currentIntergration == FlowFieldGrid.UnreachableCost)
            {
                continue;
            }

            if (currentIndex == TargetIndex)
            {
                continue;
            }

            grid.IndexToLocal(currentIndex, out int currentX, out int currentY);

            int bestIndex = -1;
            int bestIntergration = currentIntergration;

            foreach (Vector2Int offset in AllOffsets)
            {
                int neighborX = currentX + offset.x;
                int neighborY = currentY + offset.y;

                if (!grid.TryGetIndex(neighborX, neighborY, out int neighborIndex))
                    continue;
                if (!grid.IsWalkable(neighborIndex))
                    continue;
                if (IsDiagonal(offset) && IsDiagonalBlocked(currentX, currentY, offset))
                    continue;

                int neighborIntergration = grid.Integration[neighborIndex];

                if (neighborIntergration >= bestIntergration)
                    continue;

                bestIntergration = neighborIntergration;
                bestIndex = neighborIndex;
            }

            if (bestIndex < 0)
            {
                continue;
            }

            Vector3 currentWorld = grid.IndexToWorldCenter(currentIndex);
            Vector3 bestWorld = grid.IndexToWorldCenter(bestIndex);
            grid.Directions[currentIndex] = ((Vector2)(bestWorld - currentWorld)).normalized;
        }
    }

    private bool IsDiagonal(Vector2Int offset)
    {
        return offset.x != 0 && offset.y != 0;
    }

    private bool IsDiagonalBlocked(int currentX, int currentY, Vector2Int diagonalOffset)
    {
        bool hasHorizontal = grid.TryGetIndex(
            currentX + diagonalOffset.x,
            currentY,
            out int horizontalIndex
        );
        bool hasVertical = grid.TryGetIndex(
            currentX,
            currentY + diagonalOffset.y,
            out int verticalIndex
        );

        if (!hasHorizontal || !hasVertical)
            return true;

        return !grid.IsWalkable(horizontalIndex) || !grid.IsWalkable(verticalIndex);
    }

    private void ClearResult()
    {
        TargetIndex = -1;

        Array.Fill(grid.Integration, FlowFieldGrid.UnreachableCost);

        Array.Fill(grid.Directions, Vector2.zero);

        openCells.Clear();
    }
}
