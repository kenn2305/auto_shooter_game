using UnityEngine;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(-100)]
public sealed class FlowFieldService : MonoBehaviour
{
    public static FlowFieldService Instance { get; private set; }

    [Header("Reference")]
    [Tooltip("Map that provides the grid and obstacles from data (e.g. MapStreamer). " +
        "When set, the navigation tilemap and physics baking are not used.")]
    [SerializeField] private MonoBehaviour navigationSource;
    [Tooltip("Fallback grid source when no navigation source is set: the tilemap's bounds become the grid.")]
    [SerializeField] private Tilemap navigationTileMap;
    [Tooltip("What enemies chase. Optional: when empty or inactive (another character is playing), the active " +
        "player (PlayerHealth) is found at runtime, so every character works without re-wiring the scene.")]
    [SerializeField] private Transform target;

    [Header("Looping Map (tilemap source only)")]
    [Tooltip("Treat the navigation tilemap as repeating horizontally (map edges connect).")]
    [SerializeField] private bool wrapHorizontally;
    [Tooltip("Treat the navigation tilemap as repeating vertically (map edges connect).")]
    [SerializeField] private bool wrapVertically;

    [Header("Performance")]
    [Tooltip("Half-size (in cells) of the square around the target that the flow field is computed for. " +
        "Must cover the enemy spawn ring (camera half-size + max spawn distance). 0 = whole map. " +
        "Enemies outside the window walk straight toward the target until they enter it.")]
    [SerializeField, Min(0)] private int searchRadius = 32;

    [Header("Obstacle Baking")]
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private Vector2 agentSize = new Vector2(1f, 2f);
    [SerializeField, Min(0f)] private float obstaclePadding = 0.15f;
    [SerializeField] private CapsuleDirection2D agentDirection = CapsuleDirection2D.Vertical;

    private FlowFieldGrid grid;
    private FlowFieldSolver solver;
    private IFlowFieldMapSource mapSource;

    private const float TargetSearchInterval = 0.5f;

    private Vector2Int lastTargetCell;
    private bool hasLastTargetCell;
    private float nextTargetSearchTime;

    public FlowFieldGrid Grid => grid;
    public Transform Target => target;
    public int TargetIndex => solver != null ? solver.TargetIndex : -1;
    public int SearchRadius => searchRadius;
    public LayerMask ObstacleMask => obstacleMask;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;

        ResolveTarget();

        mapSource = navigationSource as IFlowFieldMapSource;

        if (mapSource != null)
        {
            grid = new FlowFieldGrid(mapSource.GridOrigin, mapSource.GridCellSize, mapSource.GridWidth,
                mapSource.GridHeight, mapSource.WrapX, mapSource.WrapY);
        }
        else if (navigationTileMap != null)
        {
            grid = new FlowFieldGrid(navigationTileMap, wrapHorizontally, wrapVertically);
        }
        else
        {
            Debug.LogError("FlowFieldService needs a navigation source or a navigation tilemap.", this);
            enabled = false;
            return;
        }

        BakeObstacles();

        solver = new FlowFieldSolver(grid);
    }

    private void Start()
    {
        if (!enabled || !ResolveTarget())
            return;

        RebuildFlowField();
    }

    private void LateUpdate()
    {
        if (grid == null || solver == null || !ResolveTarget())
            return;

        Vector2Int currentTargetCell = grid.WorldToLocalCell(target.position);

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
        {
            if (!solver.IsOutsideSearchWindow(index))
                return false;

            // Beyond the computed window: head straight for the target until the field takes over.
            Vector2 toTarget = (Vector2)target.position - worldPosition;
            direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.zero;
            return direction != Vector2.zero;
        }

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

        walkablePosition = grid.IndexToWorldCenterNear(walkableIndex, worldPosition);

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

        if (mapSource != null)
            mapSource.BakeObstacles(grid, obstacleMask, paddedAgentSize);
        else
            FlowFieldObstacleBaker.Bake(grid, obstacleMask, paddedAgentSize, agentDirection);
    }

    /// <summary>
    /// Keeps <see cref="target"/> on the character that is actually playing. A scene can hold several
    /// characters with only one active; a target left on an inactive one would make every enemy walk to
    /// where that hidden character stands. Returns false while no active player exists.
    /// </summary>
    private bool ResolveTarget()
    {
        if (target != null && target.gameObject.activeInHierarchy)
            return true;

        // Searching is only needed right after a swap; throttle it so a level without a player stays cheap.
        if (Time.unscaledTime < nextTargetSearchTime)
            return false;

        nextTargetSearchTime = Time.unscaledTime + TargetSearchInterval;

        PlayerHealth player = FindFirstObjectByType<PlayerHealth>(); // active objects only
        if (player == null)
            return false;

        target = player.transform;
        hasLastTargetCell = false; // new target: rebuild the field on this frame
        return true;
    }

    private void RebuildFlowField()
    {
        lastTargetCell = grid.WorldToLocalCell(target.position);
        hasLastTargetCell = true;
        solver.Build(target.position, searchRadius);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
