using UnityEngine;

public sealed class FlowFieldDebug : MonoBehaviour
{
    [Header("Debug Mode")]
    [SerializeField]
    private bool debugMode = true;

    [SerializeField]
    private FlowFieldServiece flowField;

    [SerializeField]
    private bool drawBlockedCells = true;

    [SerializeField]
    private bool drawDirections = true;

    [SerializeField]
    private bool drawTarget = true;

    [SerializeField, Range(0.1f, 0.8f)]
    private float directionLength = 0.45f;

    [SerializeField, Range(0.03f, 0.25f)]
    private float arrowHeadLength = 0.12f;

    [SerializeField, Range(0.02f, 0.2f)]
    private float arrowHeadWidth = 0.08f;

    private void Reset()
    {
        flowField = GetComponent<FlowFieldServiece>();
    }

    private void OnDrawGizmos()
    {
        if (!debugMode)
        {
            return;
        }

        ResolveFlowField();

        if (flowField == null || flowField.Grid == null)
        {
            return;
        }

        FlowFieldGrid grid = flowField.Grid;

        for (int index = 0;
             index < grid.CellCount;
             index++)
        {
            Vector3 center =
                grid.IndexToWorldCenter(index);

            if (!grid.IsWalkable(index))
            {
                if (drawBlockedCells)
                {
                    Gizmos.color =
                        new Color(1f, 0f, 0f, 0.4f);

                    Gizmos.DrawCube(
                        center,
                        new Vector3(0.9f, 0.9f, 0.05f)
                    );
                }

                continue;
            }

            if (!drawDirections)
            {
                continue;
            }

            Vector2 direction =
                grid.Directions[index];

            if (direction.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            Gizmos.color = Color.cyan;
            DrawArrow(center, direction.normalized);
        }

        if (drawTarget &&
            grid.IsValidIndex(flowField.TargetIndex))
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(
                grid.IndexToWorldCenter(flowField.TargetIndex),
                0.2f
            );
        }
    }

    private void ResolveFlowField()
    {
        if (flowField != null)
        {
            return;
        }

        flowField = GetComponent<FlowFieldServiece>();

        if (flowField == null && Application.isPlaying)
        {
            flowField = FlowFieldServiece.Instance;
        }
    }

    private void DrawArrow(Vector3 start, Vector2 direction)
    {
        Vector3 forward = new Vector3(direction.x, direction.y, 0f);
        Vector3 side = new Vector3(-direction.y, direction.x, 0f);
        Vector3 end = start + forward * directionLength;
        Vector3 headBase = end - forward * arrowHeadLength;

        Gizmos.DrawLine(start, end);
        Gizmos.DrawLine(end, headBase + side * arrowHeadWidth);
        Gizmos.DrawLine(end, headBase - side * arrowHeadWidth);
    }
}
