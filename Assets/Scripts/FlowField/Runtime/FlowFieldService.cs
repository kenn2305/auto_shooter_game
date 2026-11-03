using UnityEngine;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(-100)]
public sealed class FlowFieldService : MonoBehaviour
{
    public static FlowFieldService Instance { get; private set; }

    [Header("Reference")]
    [SerializeField] private Tilemap navigationTileMap;
    [SerializeField] private Transform target;

    [Header("Obstacle Baking")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private Vector2 agentSize = new Vector2(1f, 2f);
    [SerializeField, Min(0f)] private float obstaclePadding = 0.15f;
    [SerializeField] private CapsuleDirection2D agentDirection = CapsuleDirection2D.Vertical;

    private FlowFieldGrid grid;
    private FlowFieldSolver solver;

    private Vector3Int lastTargetCell;
    private bool hasLastTargetCell;

    public FlowFieldGrid Grid => grid;
    public Transform Target => target;
    public int TargetIndex => solver != null ? solver.TargetIndex : -1;
    public LayerMask ObstacleMask => obstacleMask;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
        if (navigationTileMap == null)
        {
            enabled = false;
            return;
        }

        if (target == null)
        {
            enabled = false;
            return;
        }

        grid = new FlowFieldGrid(navigationTileMap);

        BakeObstacles();

        solver = new FlowFieldSolver(grid);
    }

    private void Start()
    {
        if (!enabled)
            return;

        RebuildFlowField();
    }

    private void LateUpdate()
    {
        if (grid == null || solver == null || target == null)
            return;

        Vector3Int currentTargetCell = navigationTileMap.WorldToCell(target.position);

        if (hasLastTargetCell && currentTargetCell == lastTargetCell)
            return;

        RebuildFlowField();
    }

    public bool TryGetDirection(Vector2 worldPosition, out Vector2 direction)
    {
        direction = Vector2.zero;

        if (grid == null || solver == null || target == null)
            return false;

        if (!grid.TryWorldToIndex(worldPosition, out int index))
            return false;

        if (!grid.IsWalkable(index))
            return false;

        if (grid.Integration[index] == FlowFieldGrid.UnreachableCost)
            return false;

        if (index == solver.TargetIndex)
        {
            Vector2 targetOffset = (Vector2)target.position - worldPosition;

            if (targetOffset.sqrMagnitude <= 0.0001f)
                return false;

            direction = targetOffset.normalized;
            return true;
        }

        direction = grid.Directions[index];
        return direction.sqrMagnitude > 0.0001f;
    }

    public bool IsNearBlockedCell(Vector2 worldPosition, int cellRange = 1)
    {
        if (grid == null || !grid.TryWorldToIndex(worldPosition, out int index))
        {
            return true;
        }

        grid.IndexToLocal(index, out int centerX, out int centerY);

        int range = Mathf.Max(0, cellRange);

        for (int y = -range; y <= range; y++)
        {
            for (int x = -range; x <= range; x++)
            {
                if (!grid.TryGetIndex(centerX + x, centerY + y, out int nearbyIndex) || !grid.IsWalkable(nearbyIndex))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryGetNearestWalkablePosition(Vector2 worldPosition, out Vector2 walkablePosition)
    {
        walkablePosition = worldPosition;

        if (
            grid == null
            || !grid.TryWorldToIndex(worldPosition, out int startIndex)
            || !grid.TryFindNearestWalkableCell(startIndex, out int walkableIndex)
        )
        {
            return false;
        }

        walkablePosition = grid.IndexToWorldCenter(walkableIndex);

        return true;
    }

    public void RebuildObstacleData()
    {
        if (grid == null)
            return;

        BakeObstacles();

        RebuildFlowField();
    }

    private void BakeObstacles()
    {
        Vector2 paddedAgentSize = agentSize + Vector2.one * (obstaclePadding * 2f);

        FlowFieldObstacleBaker.Bake(grid, obstacleMask, paddedAgentSize, agentDirection);
    }

    private void RebuildFlowField()
    {
        lastTargetCell = navigationTileMap.WorldToCell(target.position);
        hasLastTargetCell = true;
        solver.Build(target.position);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
