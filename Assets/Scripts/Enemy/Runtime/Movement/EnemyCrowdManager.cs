using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the movement of every enemy in one batched FixedUpdate.
///
/// Each physics step it snapshots all agent positions into flat arrays, buckets them per flow-field
/// cell (array-based linked lists, no per-step allocation), computes crowd separation from the
/// snapshot and calls <see cref="EnemyFlowFieldAgent.Simulate"/> directly. One Unity message for the
/// whole crowd instead of one FixedUpdate per enemy, and no Transform reads per neighbour pair.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class EnemyCrowdManager : MonoBehaviour
{
    public static EnemyCrowdManager Instance { get; private set; }

    [Header("Reference")]
    [SerializeField] private FlowFieldService flowField;

    [Header("Neighbour Search")]
    [SerializeField, Range(1, 3)] private int neighbourCellRange = 2;
    [Tooltip("Separation only uses this many neighbours (nearest cells first). Crowds piled on the player " +
        "can have 80+ enemies in range; a handful is enough to push apart and keeps the cost flat.")]
    [SerializeField, Range(1, 32)] private int maxNeighbours = 8;

    private Vector2Int[] searchOrder;

    private readonly HashSet<EnemyFlowFieldAgent> activeAgents = new();

    private EnemyFlowFieldAgent[] agents = new EnemyFlowFieldAgent[256];
    private Vector2[] positions = new Vector2[256];
    private int[] nextInBucket = new int[256];
    private int count;

    private int[] bucketHead;
    private readonly List<int> usedBuckets = new();

    private FlowFieldGrid grid;

    public int ActiveAgentCount => activeAgents.Count;

    /// <summary>Creates a manager when a scene has enemies but none was placed.</summary>
    public static EnemyCrowdManager EnsureInstance()
    {
        if (Instance == null)
        {
            var host = new GameObject("[EnemyCrowdManager]");
            host.AddComponent<EnemyCrowdManager>();
        }

        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;

        if (flowField == null)
        {
            flowField = FlowFieldService.Instance;
        }
    }

    private void Start()
    {
        InitializeBuckets();
    }

    private void FixedUpdate()
    {
        if (grid == null)
        {
            InitializeBuckets();

            if (grid == null)
            {
                return;
            }
        }

        Snapshot();

        float deltaTime = Time.fixedDeltaTime;
        for (int i = 0; i < count; i++)
        {
            EnemyFlowFieldAgent agent = agents[i];
            agent.Simulate(deltaTime, ComputeSeparation(i, agent.SeparationRadius));
        }
    }

    public void Register(EnemyFlowFieldAgent agent)
    {
        if (agent != null)
        {
            activeAgents.Add(agent);
        }
    }

    public void Unregister(EnemyFlowFieldAgent agent)
    {
        if (agent != null)
        {
            activeAgents.Remove(agent);
        }
    }

    private void InitializeBuckets()
    {
        if (flowField == null)
        {
            flowField = FlowFieldService.Instance;
        }

        if (flowField == null || flowField.Grid == null)
        {
            return;
        }

        grid = flowField.Grid;
        bucketHead = new int[grid.CellCount];
        System.Array.Fill(bucketHead, -1);

        // Cells around the agent, nearest first, so the neighbour cap keeps the closest enemies.
        var order = new List<Vector2Int>();
        for (int y = -neighbourCellRange; y <= neighbourCellRange; y++)
        {
            for (int x = -neighbourCellRange; x <= neighbourCellRange; x++)
            {
                order.Add(new Vector2Int(x, y));
            }
        }

        order.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
        searchOrder = order.ToArray();
    }

    private void Snapshot()
    {
        for (int i = 0; i < usedBuckets.Count; i++)
        {
            bucketHead[usedBuckets[i]] = -1;
        }

        usedBuckets.Clear();

        if (agents.Length < activeAgents.Count)
        {
            int size = Mathf.NextPowerOfTwo(activeAgents.Count);
            agents = new EnemyFlowFieldAgent[size];
            positions = new Vector2[size];
            nextInBucket = new int[size];
        }

        count = 0;
        foreach (EnemyFlowFieldAgent agent in activeAgents)
        {
            if (agent == null || !agent.isActiveAndEnabled || !agent.IsReady)
            {
                continue;
            }

            int i = count++;
            agents[i] = agent;
            Vector2 position = agent.Position;
            positions[i] = position;
            nextInBucket[i] = -1;

            if (grid.TryWorldToIndex(position, out int cell))
            {
                if (bucketHead[cell] < 0)
                {
                    usedBuckets.Add(cell);
                }

                nextInBucket[i] = bucketHead[cell];
                bucketHead[cell] = i;
            }
        }

        // Clear stale references beyond the live count so destroyed agents can be collected.
        for (int i = count; i < agents.Length && agents[i] != null; i++)
        {
            agents[i] = null;
        }
    }

    private Vector2 ComputeSeparation(int self, float radius)
    {
        if (radius <= 0f)
        {
            return Vector2.zero;
        }

        Vector2 selfPosition = positions[self];

        if (!grid.TryWorldToIndex(selfPosition, out int selfIndex))
        {
            return Vector2.zero;
        }

        grid.IndexToLocal(selfIndex, out int selfX, out int selfY);

        float radiusSquared = radius * radius;
        Vector2 separation = Vector2.zero;
        int used = 0;

        for (int s = 0; s < searchOrder.Length && used < maxNeighbours; s++)
        {
            if (!grid.TryGetIndex(selfX + searchOrder[s].x, selfY + searchOrder[s].y, out int bucket))
            {
                continue;
            }

            for (int other = bucketHead[bucket]; other >= 0 && used < maxNeighbours; other = nextInBucket[other])
            {
                if (other == self)
                {
                    continue;
                }

                Vector2 offset = selfPosition - positions[other];
                float distanceSquared = offset.sqrMagnitude;

                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                if (distanceSquared <= 0.0001f)
                {
                    // Stacked exactly: push apart sideways in a stable direction.
                    offset = new Vector2(self < other ? -1f : 1f, 0.25f);
                    distanceSquared = offset.sqrMagnitude;
                }

                float distance = Mathf.Sqrt(distanceSquared);
                float strength = 1f - distance / radius;

                separation += offset * (strength / distance);
                used++;
            }
        }

        return Vector2.ClampMagnitude(separation, 1f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
