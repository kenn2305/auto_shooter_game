using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public sealed class EnemyCrowdManager : MonoBehaviour
{
    public static EnemyCrowdManager Instance { get; private set; }

    [Header("Reference")]
    [SerializeField]
    private FlowFieldService flowField;

    [Header("Neighbour Search")]
    [SerializeField, Range(1, 3)]
    private int neighbourCellRange = 2;

    private readonly HashSet<EnemyFlowFieldAgent> activeAgents = new();

    private readonly List<int> usedBucketIndices = new();

    private List<EnemyFlowFieldAgent>[] buckets;

    private FlowFieldGrid grid;

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

        RebuildBuckets();
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

    public Vector2 GetSeparation(EnemyFlowFieldAgent self, float radius)
    {
        if (self == null || grid == null || buckets == null || radius <= 0f)
        {
            return Vector2.zero;
        }

        if (!grid.TryWorldToIndex(self.transform.position, out int selfIndex))
        {
            return Vector2.zero;
        }

        grid.IndexToLocal(selfIndex, out int selfX, out int selfY);

        float radiusSquared = radius * radius;
        Vector2 separation = Vector2.zero;

        for (int offsetY = -neighbourCellRange; offsetY <= neighbourCellRange; offsetY++)
        {
            for (int offsetX = -neighbourCellRange; offsetX <= neighbourCellRange; offsetX++)
            {
                int neighbourX = selfX + offsetX;

                int neighbourY = selfY + offsetY;

                if (!grid.TryGetIndex(neighbourX, neighbourY, out int bucketIndex))
                {
                    continue;
                }

                List<EnemyFlowFieldAgent> bucket = buckets[bucketIndex];

                if (bucket == null)
                {
                    continue;
                }

                for (int i = 0; i < bucket.Count; i++)
                {
                    EnemyFlowFieldAgent other = bucket[i];

                    if (other == null || other == self)
                    {
                        continue;
                    }

                    Vector2 offset = self.transform.position - other.transform.position;

                    float distanceSquared = offset.sqrMagnitude;

                    if (distanceSquared > radiusSquared)
                    {
                        continue;
                    }

                    if (distanceSquared <= 0.0001f)
                    {
                        float side = self.GetInstanceID() < other.GetInstanceID() ? -1f : 1f;

                        offset = new Vector2(side, 0.25f);

                        distanceSquared = offset.sqrMagnitude;
                    }

                    float distance = Mathf.Sqrt(distanceSquared);

                    float strength = 1f - distance / radius;

                    separation += offset.normalized * strength;
                }
            }
        }

        return Vector2.ClampMagnitude(separation, 1f);
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

        buckets = new List<EnemyFlowFieldAgent>[grid.CellCount];
    }

    private void RebuildBuckets()
    {
        ClearUsedBuckets();

        foreach (EnemyFlowFieldAgent agent in activeAgents)
        {
            if (agent == null || !agent.isActiveAndEnabled)
            {
                continue;
            }

            if (!grid.TryWorldToIndex(agent.transform.position, out int index))
            {
                continue;
            }

            List<EnemyFlowFieldAgent> bucket = buckets[index];

            if (bucket == null)
            {
                bucket = new List<EnemyFlowFieldAgent>(4);

                buckets[index] = bucket;
            }

            if (bucket.Count == 0)
            {
                usedBucketIndices.Add(index);
            }

            bucket.Add(agent);
        }
    }

    private void ClearUsedBuckets()
    {
        for (int i = 0; i < usedBucketIndices.Count; i++)
        {
            int index = usedBucketIndices[i];

            buckets[index]?.Clear();
        }

        usedBucketIndices.Clear();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
