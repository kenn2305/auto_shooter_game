using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the integration (BFS distance) and direction fields toward the target.
///
/// The search can be limited to a square window of <c>searchRadius</c> cells around the target, so
/// the cost of a rebuild depends on that window and not on the map size (a 128x128 map is ~16k cells,
/// while enemies only ever live a screen or two away from the player). Only cells touched by the last
/// build are reset, never the whole grid. Works on wrapping grids: neighbours are resolved across the
/// map edges and directions come from neighbour offsets, not world positions.
/// </summary>
public sealed class FlowFieldSolver
{
    private static readonly int[] CardinalX = { 0, 1, 0, -1 };
    private static readonly int[] CardinalY = { 1, 0, -1, 0 };

    private static readonly int[] AllX = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private static readonly int[] AllY = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly Vector2[] AllDirections =
    {
        new Vector2(0f, 1f), new Vector2(1f, 1f).normalized, new Vector2(1f, 0f), new Vector2(1f, -1f).normalized,
        new Vector2(0f, -1f), new Vector2(-1f, -1f).normalized, new Vector2(-1f, 0f), new Vector2(-1f, 1f).normalized,
    };

    private readonly FlowFieldGrid grid;
    private readonly int[] queue;
    private readonly int[] relativeX;
    private readonly int[] relativeY;
    private readonly List<int> touched;

    public int TargetIndex { get; private set; } = -1;

    /// <summary>Window half-size used by the last build (0 = whole grid).</summary>
    public int SearchRadius { get; private set; }

    public FlowFieldSolver(FlowFieldGrid grid)
    {
        this.grid = grid;
        queue = new int[grid.CellCount];
        relativeX = new int[grid.CellCount];
        relativeY = new int[grid.CellCount];
        touched = new List<int>(grid.CellCount);
    }

    public bool Build(Vector3 targetWorldPosition, int searchRadius = 0)
    {
        ResetTouched();
        SearchRadius = Mathf.Max(0, searchRadius);

        if (!grid.TryWorldToIndex(targetWorldPosition, out int targetIndex))
        {
            TargetIndex = -1;
            return false;
        }

        if (!grid.IsWalkable(targetIndex) && !grid.TryFindNearestWalkableCell(targetIndex, out targetIndex))
        {
            TargetIndex = -1;
            return false;
        }

        TargetIndex = targetIndex;

        BuildIntegrationField();
        BuildDirectionField();

        return true;
    }

    /// <summary>True when the cell lies outside the window searched by the last build.</summary>
    public bool IsOutsideSearchWindow(int index)
    {
        if (SearchRadius <= 0 || TargetIndex < 0)
            return false;

        int width = grid.Width;
        int height = grid.Height;
        int dx = index % width - TargetIndex % width;
        int dy = index / width - TargetIndex / width;

        if (grid.WrapX)
            dx = WrapDelta(dx, width);
        if (grid.WrapY)
            dy = WrapDelta(dy, height);

        return Mathf.Abs(dx) > SearchRadius || Mathf.Abs(dy) > SearchRadius;
    }

    private void BuildIntegrationField()
    {
        int[] integration = grid.Integration;
        byte[] costs = grid.Costs;
        int width = grid.Width;
        int height = grid.Height;
        bool wrapX = grid.WrapX;
        bool wrapY = grid.WrapY;
        int radius = SearchRadius;

        int head = 0;
        int tail = 0;

        integration[TargetIndex] = 0;
        relativeX[TargetIndex] = 0;
        relativeY[TargetIndex] = 0;
        queue[tail++] = TargetIndex;
        touched.Add(TargetIndex);

        while (head < tail)
        {
            int current = queue[head++];
            int currentX = current % width;
            int currentY = current / width;
            int nextCost = integration[current] + 1;

            for (int k = 0; k < 4; k++)
            {
                int relX = relativeX[current] + CardinalX[k];
                int relY = relativeY[current] + CardinalY[k];

                if (radius > 0 && (relX > radius || relX < -radius || relY > radius || relY < -radius))
                    continue;

                int x = currentX + CardinalX[k];
                int y = currentY + CardinalY[k];

                if (!Resolve(ref x, width, wrapX) || !Resolve(ref y, height, wrapY))
                    continue;

                int neighbor = x + y * width;

                if (costs[neighbor] == FlowFieldGrid.BlockedCost || integration[neighbor] != FlowFieldGrid.UnreachableCost)
                    continue;

                integration[neighbor] = nextCost;
                relativeX[neighbor] = relX;
                relativeY[neighbor] = relY;
                queue[tail++] = neighbor;
                touched.Add(neighbor);
            }
        }
    }

    private void BuildDirectionField()
    {
        int[] integration = grid.Integration;
        byte[] costs = grid.Costs;
        Vector2[] directions = grid.Directions;
        int width = grid.Width;
        int height = grid.Height;
        bool wrapX = grid.WrapX;
        bool wrapY = grid.WrapY;

        for (int t = 0; t < touched.Count; t++)
        {
            int current = touched[t];

            if (current == TargetIndex)
                continue;

            int currentX = current % width;
            int currentY = current / width;
            int best = integration[current];
            int bestDirection = -1;

            for (int k = 0; k < 8; k++)
            {
                int x = currentX + AllX[k];
                int y = currentY + AllY[k];

                if (!Resolve(ref x, width, wrapX) || !Resolve(ref y, height, wrapY))
                    continue;

                int neighbor = x + y * width;

                if (costs[neighbor] == FlowFieldGrid.BlockedCost)
                    continue;

                int value = integration[neighbor];

                if (value >= best)
                    continue;

                // Diagonal moves must not cut a blocked corner.
                if (AllX[k] != 0 && AllY[k] != 0)
                {
                    int sideX = currentX + AllX[k];
                    int sideY = currentY + AllY[k];
                    int stayX = currentX;
                    int stayY = currentY;

                    if (!Resolve(ref sideX, width, wrapX) || !Resolve(ref sideY, height, wrapY))
                        continue;
                    if (costs[sideX + stayY * width] == FlowFieldGrid.BlockedCost
                        || costs[stayX + sideY * width] == FlowFieldGrid.BlockedCost)
                        continue;
                }

                best = value;
                bestDirection = k;
            }

            if (bestDirection >= 0)
                directions[current] = AllDirections[bestDirection];
        }
    }

    private void ResetTouched()
    {
        int[] integration = grid.Integration;
        Vector2[] directions = grid.Directions;

        for (int i = 0; i < touched.Count; i++)
        {
            int index = touched[i];
            integration[index] = FlowFieldGrid.UnreachableCost;
            directions[index] = Vector2.zero;
        }

        touched.Clear();
    }

    private static bool Resolve(ref int value, int size, bool wrap)
    {
        if (value >= 0 && value < size)
            return true;

        if (!wrap)
            return false;

        value = value < 0 ? value + size : value - size;
        return true;
    }

    private static int WrapDelta(int delta, int size)
    {
        delta %= size;
        if (delta > size / 2)
            delta -= size;
        else if (delta < -size / 2)
            delta += size;
        return delta;
    }
}
