using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Pools enemies of any number of prefab types (one ObjectPool per prefab).
/// The legacy single prefab of <see cref="EnemySpawnSetting.enemyPrefab"/> is still used by <see cref="Spawn(Vector2)"/>.
/// </summary>
public sealed class EnemyPool : MonoBehaviour
{
    [Header("Setting")]
    [SerializeField] private EnemySpawnSetting setting;

    private Transform enemyContainer;

    private readonly Dictionary<EnemyPoolMember, ObjectPool<EnemyPoolMember>> pools =
        new Dictionary<EnemyPoolMember, ObjectPool<EnemyPoolMember>>();

    private readonly Dictionary<EnemyPoolMember, int> activeByPrefab = new Dictionary<EnemyPoolMember, int>();
    private readonly HashSet<EnemyPoolMember> activeMembers = new HashSet<EnemyPoolMember>();

    public int CountActive => activeMembers.Count;

    public int CountInactive
    {
        get
        {
            int total = 0;
            foreach (ObjectPool<EnemyPoolMember> pool in pools.Values)
                total += pool.CountInactive;
            return total;
        }
    }

    public int CountAll => CountActive + CountInactive;

    /// <summary>Live enemies (read-only view; do not release while iterating).</summary>
    public IReadOnlyCollection<EnemyPoolMember> ActiveMembers => activeMembers;

    private void Awake()
    {
        if (setting == null)
        {
            Debug.LogError("EnemyPool chưa được gán EnemySpawnSetting.", this);
            enabled = false;
            return;
        }

        enemyContainer = transform;

        // With a phase director the roster prefabs are prewarmed instead of the legacy single prefab.
        if (setting.enemyPrefab != null && EnemyPhaseDirector.Instance == null)
            Prewarm(setting.enemyPrefab, setting.initialPoolSize);
    }

    public int CountActiveOf(EnemyPoolMember prefab)
    {
        return prefab != null && activeByPrefab.TryGetValue(prefab, out int count) ? count : 0;
    }

    public EnemyPoolMember Spawn(Vector2 position)
    {
        return Spawn(setting != null ? setting.enemyPrefab : null, position);
    }

    public EnemyPoolMember Spawn(EnemyPoolMember prefab, Vector2 position)
    {
        if (!enabled || prefab == null)
            return null;

        EnemyPoolMember enemy = GetPool(prefab).Get();
        activeMembers.Add(enemy);
        activeByPrefab[prefab] = CountActiveOf(prefab) + 1;
        enemy.SpawnAt(position);
        return enemy;
    }

    public void Release(EnemyPoolMember enemy)
    {
        if (enemy == null || !enemy.IsSpawned || !activeMembers.Remove(enemy))
            return;

        EnemyPoolMember prefab = enemy.SourcePrefab;
        if (prefab != null && pools.TryGetValue(prefab, out ObjectPool<EnemyPoolMember> pool))
        {
            activeByPrefab[prefab] = Mathf.Max(0, CountActiveOf(prefab) - 1);
            pool.Release(enemy);
        }
        else
        {
            enemy.Despawn();
        }
    }

    /// <summary>Creates <paramref name="count"/> inactive instances ahead of time.</summary>
    public void Prewarm(EnemyPoolMember prefab, int count)
    {
        if (prefab == null || count <= 0)
            return;

        ObjectPool<EnemyPoolMember> pool = GetPool(prefab);
        int missing = count - pool.CountInactive;
        if (missing <= 0)
            return;

        EnemyPoolMember[] buffer = new EnemyPoolMember[missing];
        for (int i = 0; i < missing; i++)
            buffer[i] = pool.Get();

        for (int i = missing - 1; i >= 0; i--)
            pool.Release(buffer[i]);
    }

    private ObjectPool<EnemyPoolMember> GetPool(EnemyPoolMember prefab)
    {
        if (pools.TryGetValue(prefab, out ObjectPool<EnemyPoolMember> pool))
            return pool;

        pool = new ObjectPool<EnemyPoolMember>(
            createFunc: () => CreateEnemy(prefab),
            actionOnGet: null,
            actionOnRelease: OnReleaseEnemy,
            actionOnDestroy: OnDestroyEnemy,
            collectionCheck: true,
            defaultCapacity: Mathf.Min(setting.initialPoolSize, 64),
            maxSize: setting.maximumPoolSize
        );

        pools.Add(prefab, pool);
        return pool;
    }

    private EnemyPoolMember CreateEnemy(EnemyPoolMember prefab)
    {
        EnemyPoolMember enemy = Instantiate(prefab, enemyContainer);

        enemy.name = prefab.name;
        enemy.Bind(this, prefab);
        enemy.gameObject.SetActive(false);

        return enemy;
    }

    private static void OnReleaseEnemy(EnemyPoolMember enemy)
    {
        enemy.Despawn();
    }

    private static void OnDestroyEnemy(EnemyPoolMember enemy)
    {
        if (enemy != null)
            Destroy(enemy.gameObject);
    }
}
