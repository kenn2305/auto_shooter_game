using UnityEngine;

public static class FlowFieldObstacleBaker
{
    public static void Bake(
        FlowFieldGrid grid,
        LayerMask obstacleMask,
        Vector2 agentSize,
        CapsuleDirection2D agentDirection
    )
    {
        if (grid == null)
            return;

        for (int index = 0; index < grid.CellCount; ++index)
        {
            Vector2 worldCenter = grid.IndexToWorldCenter(index);

            Collider2D obstacle = Physics2D.OverlapCapsule(
                worldCenter,
                agentSize,
                agentDirection,
                0f,
                obstacleMask
            );

            grid.Costs[index] =
                obstacle == null ? FlowFieldGrid.WalkableCost : FlowFieldGrid.BlockedCost;
        }
    }
}
