using UnityEngine;

/// <summary>
/// A map that describes its navigation grid and obstacles from data, so the flow field does not
/// depend on a full-size tilemap or on physics colliders existing for the whole map (streamed maps
/// only keep the part around the camera alive).
/// </summary>
public interface IFlowFieldMapSource
{
    /// <summary>World position of the bottom-left corner of cell (0, 0).</summary>
    Vector2 GridOrigin { get; }
    Vector2 GridCellSize { get; }
    int GridWidth { get; }
    int GridHeight { get; }
    bool WrapX { get; }
    bool WrapY { get; }

    /// <summary>
    /// Marks blocked cells in <c>grid.Costs</c>: a cell is blocked when an agent of
    /// <paramref name="agentSize"/> centred on it would touch an obstacle collider on
    /// <paramref name="obstacleMask"/>.
    /// </summary>
    void BakeObstacles(FlowFieldGrid grid, LayerMask obstacleMask, Vector2 agentSize);
}
